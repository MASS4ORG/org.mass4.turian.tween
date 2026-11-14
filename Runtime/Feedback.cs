namespace Turian.Tweening;

/// <summary>The transform property a feedback acts on.</summary>
public enum FeedbackProperty
{
    /// <summary>The node's local position.</summary>
    Position,

    /// <summary>The node's local rotation, in Euler degrees.</summary>
    Rotation,

    /// <summary>The node's local scale.</summary>
    Scale,
}

/// <summary>
/// One effect of a <see cref="FeedbackPlayer"/>: a punch, a shake, a hit-stop or anything a brick adds by deriving from
/// this class and giving the type a <c>[TypeId]</c>. Feedbacks are data, so designers compose them in the inspector.
/// </summary>
[JsonConverter(typeof(ObjectJsonSerializer<Feedback>))]
public abstract class Feedback : IdObject
{
    /// <summary>A name that tells the feedbacks of a player apart.</summary>
    public string Label { get; set; } = "";

    /// <summary>Whether the feedback plays; a disabled one is skipped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds to wait before the feedback starts.</summary>
    public float Delay { get; set; }

    /// <summary>Starts when the previous feedback ends instead of when the player starts.</summary>
    public bool StartsAfterPrevious { get; set; }

    /// <summary>The probability, from 0 to 1, that the feedback plays each time.</summary>
    public float Chance { get; set; } = 1f;

    /// <summary>The name of the node to act on, searched among the player's node and its descendants; empty means the player's node.</summary>
    public string TargetName { get; set; } = "";

    /// <summary>Seconds the feedback keeps going once started, which later feedbacks that follow it wait for.</summary>
    [JsonIgnore, Hide]
    public virtual float Length => 0f;

    /// <summary>Starts the feedback; tweens it starts belong to the player, so stopping the player ends them.</summary>
    /// <param name="context">The player, the tween runner and the intensity of this play.</param>
    public abstract void Play(FeedbackContext context);
}

/// <summary>What a <see cref="Feedback"/> needs to play: its player, the tween runner and how hard to hit.</summary>
public sealed class FeedbackContext
{
    internal FeedbackContext(FeedbackPlayer player, TweenRunner tweens, float intensity)
    {
        Player = player;
        Tweens = tweens;
        Intensity = intensity;
    }

    /// <summary>The player running the feedback.</summary>
    public FeedbackPlayer Player { get; }

    /// <summary>The session's tween runner.</summary>
    public TweenRunner Tweens { get; }

    /// <summary>The multiplier for amplitudes and strengths, from the player and from this play.</summary>
    public float Intensity { get; }

    /// <summary>The node a feedback acts on: the player's node, or its descendant called <paramref name="targetName"/>.</summary>
    /// <exception cref="InvalidOperationException">The player has no node, or no such descendant exists.</exception>
    public Node Resolve(string? targetName)
    {
        var node = Player.Node ?? throw new InvalidOperationException("The feedback player is not attached to a node.");
        if (string.IsNullOrEmpty(targetName) || node.Name == targetName)
        {
            return node;
        }

        return FindByName(node, targetName)
               ?? throw new InvalidOperationException($"'{Player.Node.Name}' has no node called '{targetName}'.");
    }

    /// <summary>
    /// Finds a component for a scene-wide effect such as a camera shake: on the target node, below it, or anywhere in
    /// the tree the player belongs to.
    /// </summary>
    /// <returns>The component, or null when the scene has none.</returns>
    public T? Find<T>(string? targetName)
        where T : Component
    {
        var node = Resolve(targetName);
        if (node.GetComponent<T>() is { } own)
        {
            return own;
        }

        var root = node;
        while (root.Parent is { } parent)
        {
            root = parent;
        }

        return root.GetComponentsInChildren<T>().FirstOrDefault();
    }

    /// <summary>Hands a tween to the player: it plays in the player's time and ends when the player stops.</summary>
    public Tween Own(Tween tween) => tween.SetUnscaledTime(Player.UnscaledTime).SetId(Player);

    static Node? FindByName(Node node, string name)
    {
        foreach (var child in node.Children)
        {
            if (child.Name == name)
            {
                return child;
            }

            if (FindByName(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
