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
            // A tween that never started has nothing to show; one that did plays back to its start values.
            if (!states[index].Started)
            {
                return false;
            }

            position = states[index].Delay;
        }

        var generation = states[index].Generation;
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

        if (!IsAlive(index, generation))
        {
            return done;
        }

        RaiseProgress(index, loop, done);
        if (done && IsAlive(index, generation) && !states[index].Completed)
        {
            states[index].Completed = true;
            RemoveOffsets(ref states[index]);
            states[index].OnComplete?.Invoke();
        }

        return done;
    }

    void RaiseProgress(int index, int loop, bool done)
    {
        var generation = states[index].Generation;
        states[index].OnUpdate?.Invoke();
        if (!IsAlive(index, generation))
        {
            return;
        }

        var finished = states[index].Duration <= 0f ? (done ? 1 : 0) : done ? states[index].Loops : loop;
        var fired = states[index].StepsFired;
        states[index].StepsFired = finished;
        if (finished <= fired || states[index].OnStepComplete is not { } step)
        {
            return;
        }

        for (var i = fired; i < finished; i++)
        {
            step();
        }
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

        if (state.Relative && SupportsRelative(state.Kind))
        {
            state.To = state.Kind == TweenKind.Orientation
                ? Pack(Unpack(state.From) * Unpack(state.To))
                : state.From + state.To;
        }

        state.OnStart?.Invoke();
    }

    static bool SupportsRelative(TweenKind kind) => kind is TweenKind.Position or TweenKind.WorldPosition
        or TweenKind.Orientation or TweenKind.Scale or TweenKind.Float or TweenKind.Vector3 or TweenKind.Vector4
        or TweenKind.Color or TweenKind.Jump;

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
            states[child].StepsFired = 0;
            ResetChildren(child);
        }
    }

    // Returns a whole tree to its unplayed state, as a restart or a rewind begins.
    void ResetTree(int index)
    {
        RemoveOffsets(ref states[index]);
        states[index].Completed = false;
        states[index].LastLoop = -1;
        states[index].StepsFired = 0;
        for (var child = states[index].FirstChild; child >= 0; child = states[child].NextSibling)
        {
            ResetTree(child);
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
}
