namespace Turian.Tweening;

public sealed partial class TweenRunner
{
    /// <summary>
    /// Puts a tween at <paramref name="position"/> seconds from its creation, delay included; a sequence places its
    /// children at their own times. Callbacks may create or kill tweens, so slots are re-read after each one.
    /// </summary>
    /// <returns>Whether the tween has played every loop.</returns>
    bool Evaluate(int index, float position)
    {
        if (position < states[index].Delay)
        {
            return false;
        }

        if (!states[index].Started)
        {
            Begin(index);
        }

        var (loop, t, done) = Progress(states[index], position - states[index].Delay);
        if (states[index].Kind == TweenKind.Sequence)
        {
            EvaluateSequence(index, loop, t);
        }
        else
        {
            states[index].LastLoop = loop;
            Apply(ref states[index], t, loop);
        }

        if (done && !states[index].Completed)
        {
            states[index].Completed = true;
            RemoveOffsets(ref states[index]);
            states[index].OnComplete?.Invoke();
        }

        return done;
    }

    void Begin(int index)
    {
        ref var state = ref states[index];
        state.Started = true;
        if (state.Kind == TweenKind.Sequence)
        {
            state.Duration = SequenceDuration(index);
        }
        else if (!state.HasFrom)
        {
            state.From = Read(in state);
        }

        if (state.Relative)
        {
            state.To = state.Kind == TweenKind.Orientation
                ? Pack(Unpack(state.From) * Unpack(state.To))
                : state.From + state.To;
        }

        state.OnStart?.Invoke();
    }

    static (int Loop, float T, bool Done) Progress(in TweenState state, float position)
    {
        if (state.Duration <= 0f)
        {
            return (0, 1f, true);
        }

        var loop = (int)MathF.Floor(position / state.Duration);
        var done = state.Loops >= 0 && loop >= state.Loops;
        float t;
        if (done)
        {
            loop = state.Loops - 1;
            t = 1f;
        }
        else
        {
            t = (position - loop * state.Duration) / state.Duration;
        }

        return (loop, state.LoopType == LoopType.Yoyo && (loop & 1) == 1 ? 1f - t : t, done);
    }

    void EvaluateSequence(int index, int loop, float t)
    {
        if (loop != states[index].LastLoop)
        {
            FinishLoop(index, states[index].LastLoop);
            // A yoyo's backward pass plays its children back without re-running their callbacks.
            if (states[index].LoopType != LoopType.Yoyo || (loop & 1) == 0)
            {
                ResetChildren(index);
            }
        }

        states[index].LastLoop = loop;
        EvaluateChildren(index, t * states[index].Duration);
    }

    void EvaluateChildren(int index, float local)
    {
        for (var child = states[index].FirstChild; child >= 0; child = states[child].NextSibling)
        {
            // Before its start, a started child rewinds to its start values; an instant child, such as a callback, waits.
            var start = states[child].Start;
            if (local >= start)
            {
                Evaluate(child, local - start);
            }
            else if (states[child].Started && states[child].Duration > 0f)
            {
                Evaluate(child, 0f);
            }
        }
    }

    // Plays the end of the loop a frame skipped past, so its last values land and its end-of-loop callbacks run.
    void FinishLoop(int index, int previous)
    {
        if (previous < 0)
        {
            return;
        }

        var backward = states[index].LoopType == LoopType.Yoyo && (previous & 1) == 1;
        EvaluateChildren(index, backward ? 0f : states[index].Duration);
    }

    void ResetChildren(int index)
    {
        for (var child = states[index].FirstChild; child >= 0; child = states[child].NextSibling)
        {
            states[child].Completed = false;
            states[child].LastLoop = -1;
            ResetChildren(child);
        }
    }

    float SequenceDuration(int index)
    {
        var end = 0f;
        for (var child = states[index].FirstChild; child >= 0; child = states[child].NextSibling)
        {
            ref var state = ref states[child];
            if (state.Kind == TweenKind.Sequence && !state.Started)
            {
                state.Duration = SequenceDuration(child);
            }

            if (!float.IsFinite(state.Total))
            {
                throw new InvalidOperationException("A tween in a sequence cannot loop forever.");
            }

            end = MathF.Max(end, state.Start + state.Total);
        }

        return end;
    }

    static Vector4 Read(in TweenState state) => state.Node is { } node ? ReadNode(state.Kind, node) : ReadProperty(in state);

    static Vector4 ReadNode(TweenKind kind, Node node) => kind switch
    {
        TweenKind.Position => Pack(node.Position),
        TweenKind.WorldPosition => Pack(node.GlobalTransform.Position),
        TweenKind.Orientation => Pack(node.Orientation),
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

    static void Apply(ref TweenState state, float t, int loop)
    {
        if (state.Node is { IsDestroyed: true })
        {
            return;
        }

        var eased = Easing.Evaluate(state.Ease, t, state.Bezier);
        if (state.Kind >= TweenKind.Shake)
        {
            ApplyGameFeel(ref state, eased);
            return;
        }

        var value = Vector4.Lerp(state.From, state.To, eased);
        if (state.LoopType == LoopType.Incremental)
        {
            value += (state.To - state.From) * loop;
        }

        switch (state.Kind)
        {
            case TweenKind.Position:
                state.Node!.Position = Shrink(value);
                break;
            case TweenKind.WorldPosition:
                state.Node!.GlobalTransform = state.Node.GlobalTransform with { Position = Shrink(value) };
                break;
            case TweenKind.Orientation:
                state.Node!.Orientation = Quaternion.Slerp(Unpack(state.From), Unpack(state.To), eased);
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

    static Vector3 Shrink(Vector4 value) => new(value.X, value.Y, value.Z);

    static Vector4 Pack(Quaternion value) => new(value.X, value.Y, value.Z, value.W);

    static Quaternion Unpack(Vector4 value) => new(value.X, value.Y, value.Z, value.W);
}
