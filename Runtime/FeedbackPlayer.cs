namespace Turian.Tweening;

/// <summary>
/// Plays an authored list of <see cref="Feedback"/> effects together: punches, shakes, hit-stops, camera shake, light
/// flashes and events, each with its own delay, so a hit, a pickup or a jump gets its game feel without code. Call
/// <see cref="Play"/> from gameplay, or let <see cref="PlayOnStart"/> do it.
/// </summary>
[TypeId("3afbba97-cff1-47c3-b358-d098bdc21145")]
[ComponentContextMenu("Gameplay/Feedback Player")]
public sealed class FeedbackPlayer : Component
{
    Tween current;
    double readyAt;

    /// <summary>The effects to play, in order; each starts at its own delay.</summary>
    public List<Feedback> Feedbacks { get; set; } = [];

    /// <summary>Plays once when the game starts.</summary>
    public bool PlayOnStart { get; set; }

    /// <summary>Seconds to wait before the first feedback.</summary>
    public float InitialDelay { get; set; }

    /// <summary>Seconds after a play before the next one is accepted; plays in between are ignored.</summary>
    public float Cooldown { get; set; }

    /// <summary>Whether a play is accepted while the previous one is still running; when false it is ignored.</summary>
    public bool CanPlayWhilePlaying { get; set; } = true;

    /// <summary>Plays in real time, so the feedbacks keep going while game time is frozen, as during a hit-stop.</summary>
    public bool UnscaledTime { get; set; } = true;

    /// <summary>Scales every amplitude and strength of the feedbacks.</summary>
    public float Intensity { get; set; } = 1f;

    /// <summary>The session's tween runner; null outside a running game, where nothing plays.</summary>
    [InjectService(Optional = true), JsonIgnore, Hide]
    public TweenRunner? Tweens { get; private set; }

    /// <summary>Whether a play is running.</summary>
    [JsonIgnore, Hide]
    public bool IsPlaying => current.IsActive;

    /// <summary>Raised when a play has run every feedback.</summary>
    public event Action? Completed;

    /// <summary>Raised with the name of an <see cref="EventFeedback"/> when it plays, to hook gameplay to a moment.</summary>
    public event Action<string>? EventRaised;

    /// <summary>
    /// Plays the feedbacks. The returned handle can be awaited and is cancelled by <see cref="Stop"/>; it is an inactive
    /// handle when the play was refused by <see cref="Cooldown"/> or <see cref="CanPlayWhilePlaying"/>.
    /// </summary>
    /// <param name="intensity">A multiplier for this play, on top of <see cref="Intensity"/>.</param>
    public Tween Play(float intensity = 1f)
    {
        if (Tweens is not { } tweens || current.IsActive && !CanPlayWhilePlaying || tweens.UnscaledElapsed < readyAt)
        {
            return default;
        }

        readyAt = tweens.UnscaledElapsed + Cooldown;
        var context = new FeedbackContext(this, tweens, Intensity * intensity);
        var sequence = tweens.Sequence();
        var (end, previousEnd) = (0f, InitialDelay);
        foreach (var feedback in Feedbacks)
        {
            if (feedback is not { Enabled: true } || feedback.Chance < 1f && Random.Shared.NextSingle() >= feedback.Chance)
            {
                continue;
            }

            var start = (feedback.StartsAfterPrevious ? previousEnd : InitialDelay) + feedback.Delay;
            previousEnd = start + feedback.Length;
            end = MathF.Max(end, previousEnd);
            sequence.InsertCallback(start, () => feedback.Play(context));
        }

        sequence.Insert(0f, tweens.Delay(end));
        current = sequence.AsTween().SetUnscaledTime(UnscaledTime).SetId(this).OnComplete(RaiseCompleted);
        return current;
    }

    /// <summary>Stops the running play, ending every tween its feedbacks started and removing their offsets.</summary>
    public void Stop() => Tweens?.Kill(this);

    /// <inheritdoc />
    public override void OnStart()
    {
        if (PlayOnStart)
        {
            Play();
        }
    }

    /// <inheritdoc />
    public override void OnDisable() => Stop();

    internal void RaiseEvent(string name) => EventRaised?.Invoke(name);

    void RaiseCompleted() => Completed?.Invoke();
}
