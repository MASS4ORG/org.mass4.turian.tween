namespace Turian.Tweening;

public static partial class TweenExtensions
{
    /// <summary>Fades the light's intensity to <paramref name="to"/>; the tween ends with the light's node.</summary>
    public static Tween IntensityTo(this LightComponent light, float to, float duration)
    {
        var node = NodeOf(light);
        return TweenRunner.Of(node).To(() => light.Intensity, value => light.Intensity = value, to, duration).SetLink(node);
    }

    /// <summary>Blends the light's color to <paramref name="to"/>; the tween ends with the light's node.</summary>
    public static Tween ColorTo(this LightComponent light, Vector4 to, float duration)
    {
        var node = NodeOf(light);
        return TweenRunner.Of(node).To(() => light.Color, value => light.Color = value, to, duration).SetLink(node);
    }

    /// <summary>Zooms the camera's vertical field of view to <paramref name="degrees"/>; the tween ends with the camera's node.</summary>
    public static Tween FieldOfViewTo(this CameraComponent camera, float degrees, float duration)
    {
        var node = NodeOf(camera);
        return TweenRunner.Of(node)
            .To(() => camera.FieldOfViewDegrees, value => camera.FieldOfViewDegrees = value, degrees, duration)
            .SetLink(node);
    }

    static Node NodeOf(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return component.Node ?? throw new InvalidOperationException("The component is not attached to a node.");
    }
}
