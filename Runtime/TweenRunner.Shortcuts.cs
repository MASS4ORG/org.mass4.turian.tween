namespace Turian.Tweening;

/// <summary>How a path joins its waypoints.</summary>
public enum PathType
{
    /// <summary>Straight lines between waypoints.</summary>
    Linear,

    /// <summary>A smooth Catmull-Rom curve through every waypoint.</summary>
    CatmullRom,
}

public sealed partial class TweenRunner
{
    /// <summary>Tweens a float from <paramref name="from"/> to <paramref name="to"/>, handing each value to <paramref name="setter"/>.</summary>
    public Tween Value(float from, float to, float duration, Action<float> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);
        return Create(TweenKind.Float, null, new Vector4(to, 0f, 0f, 0f), duration, setter: setter).From(from);
    }

    /// <summary>Tweens a vector from <paramref name="from"/> to <paramref name="to"/>, handing each value to <paramref name="setter"/>.</summary>
    public Tween Value(Vector3 from, Vector3 to, float duration, Action<Vector3> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);
        return Create(TweenKind.Vector3, null, Pack(to), duration, setter: setter).From(from);
    }

    /// <summary>
    /// Turns a node by <paramref name="degrees"/> of Euler rotation from its current orientation, which unlike
    /// <see cref="Rotate"/> can go beyond a half turn.
    /// </summary>
    public Tween RotateBy(Node node, Vector3 degrees, float duration)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Create(TweenKind.RotateBy, node, Pack(degrees), duration);
    }

    /// <summary>Hops a node to <paramref name="to"/> in <paramref name="jumps"/> arcs, each <paramref name="height"/> high.</summary>
    public Tween Jump(Node node, Vector3 to, float height, int jumps, float duration)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfLessThan(jumps, 1);
        var tween = Create(TweenKind.Jump, node, Pack(to), duration);
        (states[tween.Index].Amount, states[tween.Index].Frequency) = (height, jumps);
        return tween;
    }

    /// <summary>
    /// Moves a node from where it is through <paramref name="waypoints"/> in order, and back to its start when
    /// <paramref name="closed"/>. The array is read while the tween plays, so leave it unchanged.
    /// </summary>
    public Tween MovePath(Node node, Vector3[] waypoints, float duration, PathType type = PathType.Linear,
        bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(waypoints);
        if (waypoints.Length == 0)
        {
            throw new ArgumentException("A path needs at least one waypoint.", nameof(waypoints));
        }

        var tween = Create(TweenKind.Path, node, default, duration);
        states[tween.Index].Waypoints = waypoints;
        states[tween.Index].Mode = (int)type | (closed ? 2 : 0);
        return tween;
    }

    /// <summary>
    /// Moves a node by <paramref name="by"/> on top of every other change, so several blendable tweens and plain
    /// moves add up instead of fighting; what it moved stays when it ends.
    /// </summary>
    public Tween BlendMove(Node node, Vector3 by, float duration) => Blend(TweenKind.BlendPosition, node, by, duration);

    /// <summary>Grows a node's scale by <paramref name="by"/> of itself, blending with other changes; see <see cref="BlendMove"/>.</summary>
    public Tween BlendScale(Node node, Vector3 by, float duration) => Blend(TweenKind.BlendScale, node, by, duration);

    /// <summary>Turns a node by <paramref name="degrees"/> of Euler rotation, blending with other changes; see <see cref="BlendMove"/>.</summary>
    public Tween BlendRotate(Node node, Vector3 degrees, float duration) =>
        Blend(TweenKind.BlendRotation, node, degrees, duration);

    Tween Blend(TweenKind kind, Node node, Vector3 amount, float duration)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Create(kind, node, Pack(amount), duration);
    }
}
