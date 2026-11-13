namespace Turian.Tweening;

/// <summary>Starts tweens and game-feel effects on a node, played by the running session's <see cref="TweenRunner"/>.</summary>
public static class TweenExtensions
{
    /// <summary>Moves the node's local position to <paramref name="to"/>.</summary>
    public static Tween MoveTo(this Node node, Vector3 to, float duration) => TweenRunner.Of(node).Move(node, to, duration);

    /// <summary>Moves the node's world position to <paramref name="to"/>.</summary>
    public static Tween MoveToWorld(this Node node, Vector3 to, float duration) =>
        TweenRunner.Of(node).MoveWorld(node, to, duration);

    /// <summary>Turns the node's local orientation to <paramref name="to"/>.</summary>
    public static Tween RotateTo(this Node node, Quaternion to, float duration) =>
        TweenRunner.Of(node).Rotate(node, to, duration);

    /// <summary>Scales the node's local scale to <paramref name="to"/>.</summary>
    public static Tween ScaleTo(this Node node, Vector3 to, float duration) =>
        TweenRunner.Of(node).Scale(node, to, duration);

    /// <summary>Shakes the node's local position; see <see cref="TweenRunner.Shake"/>.</summary>
    public static Tween Shake(this Node node, float duration, Vector3 strength, float frequency = 25f, int seed = 0) =>
        TweenRunner.Of(node).Shake(node, duration, strength, frequency, seed);

    /// <summary>Kicks the node's local position and springs it back; see <see cref="TweenRunner.PunchPosition"/>.</summary>
    public static Tween PunchPosition(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchPosition(node, punch, duration, vibrato);

    /// <summary>Kicks the node's local scale and springs it back.</summary>
    public static Tween PunchScale(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchScale(node, punch, duration, vibrato);

    /// <summary>Kicks the node's local rotation, in Euler degrees, and springs it back.</summary>
    public static Tween PunchRotation(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchRotation(node, punch, duration, vibrato);

    /// <summary>Squashes or stretches the node along an axis, keeping its volume; see <see cref="TweenRunner.SquashStretch"/>.</summary>
    public static Tween SquashStretch(this Node node, float amount, float duration, Vector3 axis, int wobbles = 4) =>
        TweenRunner.Of(node).SquashStretch(node, amount, duration, axis, wobbles);
}
