namespace Turian.Tweening;

public sealed partial class TweenRunner
{
    static Vector4 Read(in TweenState state) => state.Node is { } node ? ReadNode(state.Kind, node) : ReadProperty(in state);

    static Vector4 ReadNode(TweenKind kind, Node node) => kind switch
    {
        TweenKind.Position or TweenKind.Jump or TweenKind.Path => Pack(node.Position),
        TweenKind.WorldPosition => Pack(node.GlobalTransform.Position),
        TweenKind.Orientation or TweenKind.RotateBy => Pack(node.Orientation),
        _ => Pack(node.Scale),
    };

    static Vector4 ReadProperty(in TweenState state)
    {
        switch (state.Kind)
        {
            case TweenKind.Float:
                return new Vector4(((Func<float>)state.Getter!)(), 0f, 0f, 0f);
            case TweenKind.Vector3:
                return Pack(((Func<Vector3>)state.Getter!)());
            case TweenKind.Vector4:
                return ((Func<Vector4>)state.Getter!)();
            case TweenKind.Color:
                var color = ((Func<Color>)state.Getter!)();
                return new Vector4(color.R, color.G, color.B, color.A);
            default:
                return default;
        }
    }

    static float Shape(in TweenState state, float t) => state.Ease switch
    {
        Ease.Custom => state.EaseFunction?.Invoke(t) ?? t,
        _ => Easing.Evaluate(state.Ease, t, state.Overshoot, state.Period, state.Bezier),
    };

    static void Apply(ref TweenState state, float t, int loop)
    {
        if (state.Node is { IsDestroyed: true })
        {
            return;
        }

        var eased = Shape(in state, t);
        if (state.Kind >= TweenKind.Shake)
        {
            ApplyGameFeel(ref state, eased);
            return;
        }

        var step = state.LoopType == LoopType.Incremental ? loop : 0;
        switch (state.Kind)
        {
            case TweenKind.BlendPosition or TweenKind.BlendScale or TweenKind.BlendRotation:
                Offset(ref state, state.Node!, Shrink(state.To) * (eased + step));
                break;
            case TweenKind.RotateBy:
                state.Node!.Orientation = Unpack(state.From) * Euler(Shrink(state.To) * (eased + step));
                break;
            case TweenKind.Orientation:
                state.Node!.Orientation = Quaternion.Slerp(Unpack(state.From), Unpack(state.To), eased);
                break;
            case TweenKind.Path:
                state.Node!.Position = SamplePath(in state, eased);
                break;
            default:
                ApplyValue(ref state, Vector4.Lerp(state.From, state.To, eased) + (state.To - state.From) * step, eased);
                break;
        }
    }

    static void ApplyValue(ref TweenState state, Vector4 value, float eased)
    {
        switch (state.Kind)
        {
            case TweenKind.Position:
                state.Node!.Position = Shrink(value);
                break;
            case TweenKind.Jump:
                var height = eased >= 1f ? 0f : state.Amount * MathF.Abs(MathF.Sin(MathF.PI * state.Frequency * eased));
                state.Node!.Position = Shrink(value) + new Vector3(0f, height, 0f);
                break;
            case TweenKind.WorldPosition:
                state.Node!.GlobalTransform = state.Node.GlobalTransform with { Position = Shrink(value) };
                break;
            case TweenKind.Scale:
                state.Node!.Scale = Shrink(value);
                break;
            default:
                ApplyProperty(in state, value);
                break;
        }
    }

    static void ApplyProperty(in TweenState state, Vector4 value)
    {
        switch (state.Kind)
        {
            case TweenKind.Float:
                ((Action<float>)state.Setter!)(value.X);
                break;
            case TweenKind.Vector3:
                ((Action<Vector3>)state.Setter!)(Shrink(value));
                break;
            case TweenKind.Vector4:
                ((Action<Vector4>)state.Setter!)(value);
                break;
            case TweenKind.Color:
                ((Action<Color>)state.Setter!)(new Color(value.X, value.Y, value.Z, value.W));
                break;
        }
    }

    // The path starts where the node was and runs through its waypoints, back to the start when closed.
    static Vector3 SamplePath(in TweenState state, float progress)
    {
        var waypoints = state.Waypoints!;
        var closed = (state.Mode & 2) != 0;
        var count = waypoints.Length + 1;
        var segments = closed ? count : count - 1;
        var scaled = Math.Clamp(progress, 0f, 1f) * segments;
        var segment = Math.Min((int)scaled, segments - 1);
        var f = scaled - segment;
        var (p1, p2) = (PathPoint(in state, segment, closed), PathPoint(in state, segment + 1, closed));
        if ((state.Mode & 1) == 0)
        {
            return Vector3.Lerp(p1, p2, f);
        }

        var (p0, p3) = (PathPoint(in state, segment - 1, closed), PathPoint(in state, segment + 2, closed));
        return 0.5f * (2f * p1 + (p2 - p0) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f
                       + (3f * p1 - p0 - 3f * p2 + p3) * f * f * f);
    }

    // Point i of the path; beyond its ends an open path repeats its end point and a closed one wraps around.
    static Vector3 PathPoint(in TweenState state, int index, bool closed)
    {
        var count = state.Waypoints!.Length + 1;
        index = closed ? ((index % count) + count) % count : Math.Clamp(index, 0, count - 1);
        return index == 0 ? Shrink(state.From) : state.Waypoints[index - 1];
    }

    static Vector3 Shrink(Vector4 value) => new(value.X, value.Y, value.Z);

    static Vector4 Pack(Quaternion value) => new(value.X, value.Y, value.Z, value.W);

    static Quaternion Unpack(Vector4 value) => new(value.X, value.Y, value.Z, value.W);
}
