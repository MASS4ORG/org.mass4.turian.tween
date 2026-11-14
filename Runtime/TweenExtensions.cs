namespace Turian.Tweening;

/// <summary>Starts tweens and game-feel effects on a node, played by the running session's <see cref="TweenRunner"/>.</summary>
public static partial class TweenExtensions
{
    /// <summary>Moves the node's local position to <paramref name="to"/>.</summary>
    public static Tween MoveTo(this Node node, Vector3 to, float duration) => TweenRunner.Of(node).Move(node, to, duration);

    /// <summary>Moves the node's world position to <paramref name="to"/>.</summary>
    public static Tween MoveToWorld(this Node node, Vector3 to, float duration) =>
        TweenRunner.Of(node).MoveWorld(node, to, duration);

    /// <summary>Turns the node's local orientation to <paramref name="to"/>.</summary>
    public static Tween RotateTo(this Node node, Quaternion to, float duration) =>
        TweenRunner.Of(node).Rotate(node, to, duration);

    /// <summary>Turns the node by Euler <paramref name="degrees"/>, however far; see <see cref="TweenRunner.RotateBy"/>.</summary>
    public static Tween RotateBy(this Node node, Vector3 degrees, float duration) =>
        TweenRunner.Of(node).RotateBy(node, degrees, duration);

    /// <summary>Scales the node's local scale to <paramref name="to"/>.</summary>
    public static Tween ScaleTo(this Node node, Vector3 to, float duration) =>
        TweenRunner.Of(node).Scale(node, to, duration);

    /// <summary>Hops the node to <paramref name="to"/> in arcs; see <see cref="TweenRunner.Jump"/>.</summary>
    public static Tween JumpTo(this Node node, Vector3 to, float height, int jumps, float duration) =>
        TweenRunner.Of(node).Jump(node, to, height, jumps, duration);

    /// <summary>Moves the node through waypoints; see <see cref="TweenRunner.MovePath"/>.</summary>
    public static Tween MovePath(this Node node, Vector3[] waypoints, float duration, PathType type = PathType.Linear,
        bool closed = false) => TweenRunner.Of(node).MovePath(node, waypoints, duration, type, closed);

    /// <summary>Moves the node by an offset that adds to other changes; see <see cref="TweenRunner.BlendMove"/>.</summary>
    public static Tween MoveBlend(this Node node, Vector3 by, float duration) =>
        TweenRunner.Of(node).BlendMove(node, by, duration);

    /// <summary>Grows the node by a fraction of its scale, blending with other changes; see <see cref="TweenRunner.BlendMove"/>.</summary>
    public static Tween ScaleBlend(this Node node, Vector3 by, float duration) =>
        TweenRunner.Of(node).BlendScale(node, by, duration);

    /// <summary>Turns the node by Euler degrees that add to other changes; see <see cref="TweenRunner.BlendMove"/>.</summary>
    public static Tween RotateBlend(this Node node, Vector3 degrees, float duration) =>
        TweenRunner.Of(node).BlendRotate(node, degrees, duration);

    /// <summary>Shakes the node's local position; see <see cref="TweenRunner.Shake"/>.</summary>
    public static Tween Shake(this Node node, float duration, Vector3 strength, float frequency = 25f, int seed = 0) =>
        TweenRunner.Of(node).Shake(node, duration, strength, frequency, seed);

    /// <summary>Shakes the node's local rotation, in Euler degrees; see <see cref="TweenRunner.ShakeRotation"/>.</summary>
    public static Tween ShakeRotation(this Node node, float duration, Vector3 strength, float frequency = 25f,
        int seed = 0) => TweenRunner.Of(node).ShakeRotation(node, duration, strength, frequency, seed);

    /// <summary>Shakes the node's local scale; see <see cref="TweenRunner.ShakeScale"/>.</summary>
    public static Tween ShakeScale(this Node node, float duration, Vector3 strength, float frequency = 25f,
        int seed = 0) => TweenRunner.Of(node).ShakeScale(node, duration, strength, frequency, seed);

    /// <summary>Kicks the node's local position and springs it back; see <see cref="TweenRunner.PunchPosition"/>.</summary>
    public static Tween PunchPosition(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchPosition(node, punch, duration, vibrato);

    /// <summary>Grows the node's local scale by a fraction of itself and springs it back.</summary>
    public static Tween PunchScale(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchScale(node, punch, duration, vibrato);

    /// <summary>Kicks the node's local rotation, in Euler degrees, and springs it back.</summary>
    public static Tween PunchRotation(this Node node, Vector3 punch, float duration, int vibrato = 6) =>
        TweenRunner.Of(node).PunchRotation(node, punch, duration, vibrato);

    /// <summary>Squashes or stretches the node along an axis, keeping its volume; see <see cref="TweenRunner.SquashStretch"/>.</summary>
    public static Tween SquashStretch(this Node node, float amount, float duration, Vector3 axis, int wobbles = 4) =>
        TweenRunner.Of(node).SquashStretch(node, amount, duration, axis, wobbles);

    /// <summary>Kills every tween that moves the node or is linked to it.</summary>
    /// <returns>How many tweens were killed.</returns>
    public static int KillTweens(this Node node, bool complete = false) => TweenRunner.Of(node).KillTweensOf(node, complete);

    /// <summary>Whether any tween moves the node or is linked to it.</summary>
    public static bool IsTweening(this Node node) => TweenRunner.Of(node).IsTweening(node);
}
