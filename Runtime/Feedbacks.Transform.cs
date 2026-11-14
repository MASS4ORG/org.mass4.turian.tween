namespace Turian.Tweening;

/// <summary>Kicks a node's position, rotation or scale and springs it back.</summary>
[TypeId("168ad715-0032-4b25-8e9e-00d419cbb661")]
public sealed class PunchFeedback : Feedback
{
    /// <summary>What to kick.</summary>
    public FeedbackProperty Property { get; set; } = FeedbackProperty.Scale;

    /// <summary>The kick: units for position, Euler degrees for rotation, a fraction of the current scale for scale.</summary>
    public Vector3 Amount { get; set; } = new(0.3f);

    /// <summary>Seconds until the node is back.</summary>
    public float Duration { get; set; } = 0.3f;

    /// <summary>How many half-oscillations the spring makes.</summary>
    public int Vibrato { get; set; } = 6;

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
        var (node, amount) = (context.Resolve(TargetName), Amount * context.Intensity);
        context.Own(Property switch
        {
            FeedbackProperty.Position => context.Tweens.PunchPosition(node, amount, Duration, Vibrato),
            FeedbackProperty.Rotation => context.Tweens.PunchRotation(node, amount, Duration, Vibrato),
            _ => context.Tweens.PunchScale(node, amount, Duration, Vibrato),
        });
    }
}

/// <summary>Shakes a node's position, rotation or scale with seeded smooth noise that fades out.</summary>
[TypeId("5cad2b40-d739-4260-baad-429fae20b9be")]
public sealed class ShakeFeedback : Feedback
{
    /// <summary>What to shake.</summary>
    public FeedbackProperty Property { get; set; } = FeedbackProperty.Position;

    /// <summary>The largest offset per axis: units for position, Euler degrees for rotation, a fraction of the current scale for scale.</summary>
    public Vector3 Strength { get; set; } = new(0.2f, 0.2f, 0f);

    /// <summary>Seconds the shake lasts.</summary>
    public float Duration { get; set; } = 0.4f;

    /// <summary>Noise samples per second; higher shakes faster.</summary>
    public float Frequency { get; set; } = 25f;

    /// <summary>Selects the noise, so a seeded shake replays identically.</summary>
    public int Seed { get; set; }

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
        var (node, strength) = (context.Resolve(TargetName), Strength * context.Intensity);
        context.Own(Property switch
        {
            FeedbackProperty.Position => context.Tweens.Shake(node, Duration, strength, Frequency, Seed),
            FeedbackProperty.Rotation => context.Tweens.ShakeRotation(node, Duration, strength, Frequency, Seed),
            _ => context.Tweens.ShakeScale(node, Duration, strength, Frequency, Seed),
        });
    }
}

/// <summary>Squashes or stretches a node along an axis and wobbles it back, keeping its volume.</summary>
[TypeId("85557631-8eee-4e96-afd6-b89b9305b733")]
public sealed class SquashStretchFeedback : Feedback
{
    /// <summary>The first deformation: positive stretches along the axis, negative squashes.</summary>
    public float Amount { get; set; } = 0.4f;

    /// <summary>The unit axis to deform along.</summary>
    public Vector3 Axis { get; set; } = Vector3.UnitY;

    /// <summary>Seconds until the node is back to its scale.</summary>
    public float Duration { get; set; } = 0.4f;

    /// <summary>How many half-oscillations the wobble makes.</summary>
    public int Wobbles { get; set; } = 4;

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context) => context.Own(context.Tweens.SquashStretch(
        context.Resolve(TargetName), Math.Max(Amount * context.Intensity, -0.99f), Duration, Axis, Wobbles));
}

/// <summary>Moves, turns or scales a node to a value, or by a value, with an easing curve.</summary>
[TypeId("456ce315-f206-4191-8d4d-5ed04107ac6e")]
public sealed class TransformFeedback : Feedback
{
    /// <summary>What to change.</summary>
    public FeedbackProperty Property { get; set; } = FeedbackProperty.Position;

    /// <summary>The end value: a local position, Euler degrees, or a scale.</summary>
    public Vector3 To { get; set; }

    /// <summary>Treats <see cref="To"/> as an offset from where the node is when the feedback starts.</summary>
    public bool Relative { get; set; }

    /// <summary>Seconds the change takes.</summary>
    public float Duration { get; set; } = 0.3f;

    /// <summary>An easing name as <see cref="Easing.TryParse"/> reads it, such as <c>ease-out-back</c>.</summary>
    public string EaseName { get; set; } = "ease-out";

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
        var node = context.Resolve(TargetName);
        var tween = Property switch
        {
            FeedbackProperty.Position => context.Tweens.Move(node, To, Duration),
            FeedbackProperty.Rotation when Relative => context.Tweens.RotateBy(node, To, Duration),
            FeedbackProperty.Rotation => context.Tweens.Rotate(node, Quaternion.CreateFromYawPitchRoll(
                To.Y * MathF.PI / 180f, To.X * MathF.PI / 180f, To.Z * MathF.PI / 180f), Duration),
            _ => context.Tweens.Scale(node, To, Duration),
        };
        if (Relative && Property != FeedbackProperty.Rotation)
        {
            tween.SetRelative();
        }

        if (Easing.TryParse(EaseName, out var ease, out var curve))
        {
            tween = ease == Ease.Bezier ? tween.SetEase(curve) : tween.SetEase(ease);
        }

        context.Own(tween);
    }
}

/// <summary>Does nothing for a while, so feedbacks that start after it wait.</summary>
[TypeId("6df59b0f-3b9b-4882-b449-0db27e4bf459")]
public sealed class PauseFeedback : Feedback
{
    /// <summary>Seconds to hold.</summary>
    public float Duration { get; set; } = 0.2f;

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
    }
}

/// <summary>Raises <see cref="FeedbackPlayer.EventRaised"/> at a moment of the sequence, to hook gameplay or other bricks in.</summary>
[TypeId("d67bf703-e5d0-4ef9-a183-e22aa8471bc0")]
public sealed class EventFeedback : Feedback
{
    /// <summary>The name handed to <see cref="FeedbackPlayer.EventRaised"/> listeners.</summary>
    public string EventName { get; set; } = "";

    /// <inheritdoc />
    public override void Play(FeedbackContext context) => context.Player.RaiseEvent(EventName);
}
