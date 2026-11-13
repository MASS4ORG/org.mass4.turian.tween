namespace Turian.Tweening;

public sealed partial class TweenRunner
{
    float hitStopRemaining;
    double timeScaleBeforeHitStop;
    bool hitStopping;

    /// <summary>
    /// Shakes a node's local position with seeded smooth noise that fades out over <paramref name="duration"/>.
    /// The offset adds to whatever else moves the node, and is removed when the shake ends or is killed.
    /// </summary>
    /// <param name="node">The node to shake.</param>
    /// <param name="duration">Seconds the shake lasts.</param>
    /// <param name="strength">The largest offset per axis.</param>
    /// <param name="frequency">Noise samples per second; higher shakes faster.</param>
    /// <param name="seed">Selects the noise, so a seeded shake replays identically.</param>
    public Tween Shake(Node node, float duration, Vector3 strength, float frequency = 25f, int seed = 0) =>
        GameFeel(TweenKind.Shake, node, Pack(strength), duration, frequency, seed);

    /// <summary>Kicks a node's local position by <paramref name="punch"/> and springs it back to where it was.</summary>
    /// <param name="node">The node to punch.</param>
    /// <param name="punch">The kick, in local units.</param>
    /// <param name="duration">Seconds until the node is back.</param>
    /// <param name="vibrato">How many half-oscillations the spring makes.</param>
    public Tween PunchPosition(Node node, Vector3 punch, float duration, int vibrato = 6) =>
        GameFeel(TweenKind.PunchPosition, node, Pack(punch), duration, vibrato, 0);

    /// <summary>Kicks a node's local scale by <paramref name="punch"/> and springs it back.</summary>
    public Tween PunchScale(Node node, Vector3 punch, float duration, int vibrato = 6) =>
        GameFeel(TweenKind.PunchScale, node, Pack(punch), duration, vibrato, 0);

    /// <summary>Kicks a node's local rotation by <paramref name="punch"/> Euler degrees and springs it back.</summary>
    public Tween PunchRotation(Node node, Vector3 punch, float duration, int vibrato = 6) =>
        GameFeel(TweenKind.PunchRotation, node, Pack(punch), duration, vibrato, 0);

    /// <summary>
    /// Squashes or stretches a node along <paramref name="axis"/> and wobbles it back, keeping its volume: the other
    /// axes shrink as the axis grows.
    /// </summary>
    /// <param name="node">The node to deform.</param>
    /// <param name="amount">The first deformation: positive stretches along the axis, negative squashes.</param>
    /// <param name="duration">Seconds until the node is back to its scale.</param>
    /// <param name="axis">A unit axis, X, Y or Z.</param>
    /// <param name="wobbles">How many half-oscillations the wobble makes.</param>
    public Tween SquashStretch(Node node, float amount, float duration, Vector3 axis, int wobbles = 4)
    {
        if (amount <= -1f)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A squash cannot flatten a node to nothing.");
        }

        return GameFeel(TweenKind.SquashStretch, node, new Vector4(Vector3.Abs(axis), amount), duration, wobbles, 0);
    }

    /// <summary>
    /// Freezes game time for <paramref name="seconds"/> of real time, then restores the time scale in force before.
    /// A hit-stop during another extends it.
    /// </summary>
    /// <param name="seconds">Unscaled seconds the freeze lasts.</param>
    /// <param name="timeScale">The time scale during the freeze; zero stops game time.</param>
    public void HitStop(float seconds, float timeScale = 0f)
    {
        if (clock is null || seconds <= 0f)
        {
            return;
        }

        if (!hitStopping)
        {
            timeScaleBeforeHitStop = clock.TimeScale;
            hitStopping = true;
        }

        clock.TimeScale = timeScale;
        hitStopRemaining = MathF.Max(hitStopRemaining, seconds);
    }

    /// <summary>Whether a hit-stop is freezing game time.</summary>
    public bool IsHitStopping => hitStopping;

    Tween GameFeel(TweenKind kind, Node node, Vector4 to, float duration, float frequency, int seed)
    {
        ArgumentNullException.ThrowIfNull(node);
        var tween = Create(kind, node, to, duration);
        ref var state = ref states[tween.Index];
        state.Frequency = frequency;
        state.Seed = seed;
        state.Ease = Ease.Linear;
        return tween;
    }

    void AdvanceHitStop(float unscaled)
    {
        if (!hitStopping)
        {
            return;
        }

        hitStopRemaining -= unscaled;
        if (hitStopRemaining <= 0f)
        {
            EndHitStop();
        }
    }

    void EndHitStop()
    {
        if (hitStopping && clock is not null)
        {
            clock.TimeScale = timeScaleBeforeHitStop;
        }

        hitStopping = false;
        hitStopRemaining = 0f;
    }

    static void ApplyGameFeel(ref TweenState state, float t)
    {
        var node = state.Node!;
        var fade = 1f - t;
        switch (state.Kind)
        {
            case TweenKind.Shake:
                var time = t * state.Duration * state.Frequency;
                var shake = Shrink(state.To) * fade * new Vector3(
                    Noise(state.Seed, time), Noise(state.Seed + 7919, time), Noise(state.Seed + 15887, time));
                Offset(ref state, node, shake);
                break;
            case TweenKind.SquashStretch:
                var stretch = 1f + state.To.W * fade * MathF.Cos(t * state.Frequency * MathF.PI);
                var axis = Shrink(state.To);
                var multiplier = Vector3.One + axis * (stretch - 1f) + (Vector3.One - axis) * (1f / MathF.Sqrt(stretch) - 1f);
                node.Scale = node.Scale / Multiplier(state.Applied) * multiplier;
                state.Applied = Pack(multiplier);
                break;
            default:
                Offset(ref state, node, Shrink(state.To) * fade * MathF.Sin(t * state.Frequency * MathF.PI));
                break;
        }
    }

    // Replaces the offset this tween added last frame with a new one, leaving other changes to the node alone.
    static void Offset(ref TweenState state, Node node, Vector3 offset)
    {
        var previous = Shrink(state.Applied);
        switch (state.Kind)
        {
            case TweenKind.PunchScale:
                node.Scale += offset - previous;
                break;
            case TweenKind.PunchRotation:
                node.Orientation *= Quaternion.Inverse(Euler(previous)) * Euler(offset);
                break;
            default:
                node.Position += offset - previous;
                break;
        }

        state.Applied = Pack(offset);
    }

    static void RemoveOffsets(ref TweenState state)
    {
        if (state.Kind < TweenKind.Shake || state.Applied == Vector4.Zero || state.Node is not { IsDestroyed: false } node)
        {
            return;
        }

        if (state.Kind == TweenKind.SquashStretch)
        {
            node.Scale /= Multiplier(state.Applied);
            state.Applied = Vector4.Zero;
            return;
        }

        Offset(ref state, node, Vector3.Zero);
        state.Applied = Vector4.Zero;
    }

    static Vector3 Multiplier(Vector4 applied) => applied == Vector4.Zero ? Vector3.One : Shrink(applied);

    static Quaternion Euler(Vector3 degrees) => Quaternion.CreateFromYawPitchRoll(
        degrees.Y * MathF.PI / 180f, degrees.X * MathF.PI / 180f, degrees.Z * MathF.PI / 180f);

    /// <summary>Seeded 1D gradient noise in about [-1, 1], smooth in <paramref name="x"/> and zero at whole numbers.</summary>
    internal static float Noise(int seed, float x)
    {
        var cell = (int)MathF.Floor(x);
        var f = x - cell;
        var a = Gradient(seed, cell) * f;
        var b = Gradient(seed, cell + 1) * (f - 1f);
        var fade = f * f * f * (f * (f * 6f - 15f) + 10f);
        return (a + (b - a) * fade) * 2f;
    }

    static float Gradient(int seed, int cell)
    {
        var hash = unchecked((uint)(cell * 374761393 + seed * 668265263));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        hash ^= hash >> 16;
        return hash / (float)uint.MaxValue * 2f - 1f;
    }
}
