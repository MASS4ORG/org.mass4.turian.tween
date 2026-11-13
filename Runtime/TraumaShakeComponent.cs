namespace Turian.Tweening;

/// <summary>
/// Trauma-based shake for a camera or any node: hits add trauma, trauma decays over time, and the node is offset and
/// tilted by seeded smooth noise scaled by trauma squared, so small hits barely move it and big ones hit hard. The
/// offset adds to whatever else moves the node and is removed when the component is disabled.
/// </summary>
[TypeId("3f0c9b5e-5a7d-4f63-9d2e-6c1e8a4b7f20")]
public sealed class TraumaShakeComponent : Component
{
    Vector3 appliedOffset;
    Vector3 appliedAngle;
    float time;

    /// <summary>The largest positional offset per axis, at full trauma.</summary>
    public Vector3 MaxOffset { get; set; } = new(0.3f, 0.3f, 0f);

    /// <summary>The largest tilt per axis, in Euler degrees, at full trauma.</summary>
    public Vector3 MaxAngle { get; set; } = new(0f, 0f, 3f);

    /// <summary>Noise samples per second; higher shakes faster.</summary>
    public float Frequency { get; set; } = 20f;

    /// <summary>Trauma lost per second.</summary>
    public float Recovery { get; set; } = 1.5f;

    /// <summary>Selects the noise, so a seeded shake replays identically.</summary>
    public int Seed { get; set; }

    /// <summary>Whether the shake keeps going while game time is frozen, as during a hit-stop.</summary>
    public bool UnscaledTime { get; set; } = true;

    /// <summary>The current trauma, from 0 (still) to 1 (strongest).</summary>
    [JsonIgnore, Hide]
    public float Trauma { get; private set; }

    /// <summary>The session's tween runner, which supplies unscaled time.</summary>
    [InjectService(Optional = true), JsonIgnore, Hide]
    public TweenRunner? Tweens { get; private set; }

    /// <summary>Adds trauma, clamped to [0, 1].</summary>
    /// <param name="amount">How hard the hit was; 0.3 is a bump, 1 a blast.</param>
    public void AddTrauma(float amount) => Trauma = Math.Clamp(Trauma + amount, 0f, 1f);

    /// <inheritdoc />
    public override void OnLateUpdate(float deltaTime)
    {
        if (Node is not { } node)
        {
            return;
        }

        var step = UnscaledTime && Tweens is { } tweens ? tweens.UnscaledDeltaTime : deltaTime;
        time += step;
        var shake = Trauma * Trauma;
        var x = time * Frequency;
        var offset = MaxOffset * shake * new Vector3(
            TweenRunner.Noise(Seed, x), TweenRunner.Noise(Seed + 7919, x), TweenRunner.Noise(Seed + 15887, x));
        var angle = MaxAngle * shake * new Vector3(
            TweenRunner.Noise(Seed + 31, x), TweenRunner.Noise(Seed + 7951, x), TweenRunner.Noise(Seed + 15919, x));
        Apply(node, offset, angle);
        Trauma = MathF.Max(0f, Trauma - Recovery * step);
    }

    /// <inheritdoc />
    public override void OnDisable()
    {
        if (Node is { } node)
        {
            Apply(node, Vector3.Zero, Vector3.Zero);
        }
    }

    void Apply(Node node, Vector3 offset, Vector3 angle)
    {
        node.Position += offset - appliedOffset;
        node.Orientation *= Quaternion.Inverse(Tilt(appliedAngle)) * Tilt(angle);
        (appliedOffset, appliedAngle) = (offset, angle);
    }

    static Quaternion Tilt(Vector3 degrees) => Quaternion.CreateFromYawPitchRoll(
        degrees.Y * MathF.PI / 180f, degrees.X * MathF.PI / 180f, degrees.Z * MathF.PI / 180f);
}
