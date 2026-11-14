namespace Turian.Tweening;

/// <summary>Adds trauma to a <see cref="TraumaShakeComponent"/>, found on the target node or anywhere in its scene.</summary>
[TypeId("ed05c5f4-3c2d-4566-8445-fe67df2bfca8")]
public sealed class CameraShakeFeedback : Feedback
{
    /// <summary>How hard the hit is, from 0 to 1; 0.3 is a bump, 1 a blast.</summary>
    public float Trauma { get; set; } = 0.5f;

    /// <inheritdoc />
    public override void Play(FeedbackContext context) =>
        context.Find<TraumaShakeComponent>(TargetName)?.AddTrauma(Trauma * context.Intensity);
}

/// <summary>Freezes or slows game time for a moment, then eases it back.</summary>
[TypeId("9df9eb92-a9b5-4bc5-a8dc-285d39070dbd")]
public sealed class HitStopFeedback : Feedback
{
    /// <summary>Real seconds the freeze lasts.</summary>
    public float Seconds { get; set; } = 0.08f;

    /// <summary>The time scale during the freeze; 0 stops game time, a small value is slow motion.</summary>
    public float TimeScale { get; set; }

    /// <summary>Real seconds over which the time scale eases back afterwards.</summary>
    public float RecoverSeconds { get; set; }

    /// <inheritdoc />
    public override float Length => Seconds + RecoverSeconds;

    /// <inheritdoc />
    public override void Play(FeedbackContext context) => context.Tweens.HitStop(Seconds, TimeScale, RecoverSeconds);
}

/// <summary>Flashes a light's intensity and, optionally, its color, then returns it.</summary>
[TypeId("6a94c2b5-5206-4234-a757-0d61a06f6a33")]
public sealed class LightFlashFeedback : Feedback
{
    /// <summary>The intensity at the peak of the flash.</summary>
    public float PeakIntensity { get; set; } = 5f;

    /// <summary>Whether the color flashes too.</summary>
    public bool FlashColor { get; set; }

    /// <summary>The color at the peak of the flash, as RGBA.</summary>
    public Vector4 PeakColor { get; set; } = new(1f);

    /// <summary>Seconds from the start of the flash until the light is back.</summary>
    public float Duration { get; set; } = 0.2f;

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
        if (context.Find<LightComponent>(TargetName) is not { } light)
        {
            return;
        }

        context.Own(light.IntensityTo(PeakIntensity * context.Intensity, Duration / 2f).SetLoops(2, LoopType.Yoyo));
        if (FlashColor)
        {
            context.Own(light.ColorTo(PeakColor, Duration / 2f).SetLoops(2, LoopType.Yoyo));
        }
    }
}

/// <summary>Zooms a camera's field of view for a moment and returns it, for impact or speed.</summary>
[TypeId("c9587067-416b-4961-a51b-5cd12f865c2f")]
public sealed class FieldOfViewFeedback : Feedback
{
    /// <summary>Degrees added to the field of view at the peak; negative zooms in.</summary>
    public float Degrees { get; set; } = -6f;

    /// <summary>Seconds from the start until the field of view is back.</summary>
    public float Duration { get; set; } = 0.25f;

    /// <inheritdoc />
    public override float Length => Duration;

    /// <inheritdoc />
    public override void Play(FeedbackContext context)
    {
        if (context.Find<CameraComponent>(TargetName) is not { } camera)
        {
            return;
        }

        var peak = camera.FieldOfViewDegrees + Degrees * context.Intensity;
        context.Own(camera.FieldOfViewTo(peak, Duration / 2f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo));
    }
}
