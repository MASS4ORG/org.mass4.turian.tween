namespace Turian.Tests;

/// <summary>The feedback player: timelines, intensity, lookups, refusals, stopping, every built-in feedback and saving.</summary>
[Collection(SerialTests.Name)]
public sealed class FeedbackTests : IDisposable
{
    readonly TweenGame game = new();

    /// <inheritdoc />
    public void Dispose() => game.Dispose();

    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}.");

    (Node Root, Node Actor, FeedbackPlayer Player) Build(params Feedback[] feedbacks) => Build(null, feedbacks);

    (Node Root, Node Actor, FeedbackPlayer Player) Build(Action<Node>? addToScene, params Feedback[] feedbacks)
    {
        var root = new Node { Name = "Level" };
        var actor = new Node { Name = "Actor" };
        var player = new FeedbackPlayer { Feedbacks = [.. feedbacks] };
        actor.AddComponent(player);
        actor.Children.Add(new Node { Name = "Body" });
        root.Children.Add(actor);
        addToScene?.Invoke(root);
        game.Scenes.AdoptScene(Guid.NewGuid(), root);
        root.Awake(null, game.SceneServices);
        return (root, actor, player);
    }

    /// <summary>Feedbacks start at their delays, after-previous ones wait for the one before, and the player completes at the end.</summary>
    [Fact]
    public void FeedbacksPlayOnTheirTimeline()
    {
        var log = new List<string>();
        var (_, actor, player) = Build(
            new EventFeedback { EventName = "start" },
            new PauseFeedback { Duration = 1f },
            new EventFeedback { EventName = "after-pause", StartsAfterPrevious = true },
            new TransformFeedback { To = new Vector3(4, 0, 0), Duration = 2f, Delay = 0.5f, EaseName = "linear" });
        player.EventRaised += log.Add;
        player.Completed += () => log.Add("done");

        var awaited = player.Play().AsTask();
        Assert.True(player.IsPlaying);
        game.Frames(1);
        Assert.Equal(["start"], log);

        game.Frames(2);
        Assert.Equal(["start"], log);
        game.Frames(2);
        Assert.Equal(["start", "after-pause"], log);
        Assert.InRange(actor.Position.X, 0.5f, 3.5f);

        game.Frames(6);
        Assert.Equal(["start", "after-pause", "done"], log);
        Assert.Equal(new Vector3(4, 0, 0), actor.Position);
        Assert.True(awaited.IsCompletedSuccessfully);
        Assert.False(player.IsPlaying);
    }

    /// <summary>Intensity multiplies amplitudes, from the player and from the play; a camera shake finds its component in the scene.</summary>
    [Fact]
    public void IntensityScalesAndCameraShakeFindsItsComponent()
    {
        var shake = new TraumaShakeComponent { Recovery = 0f };
        var (_, _, player) = Build(
            root =>
            {
                var camera = new Node { Name = "Camera" };
                camera.AddComponent(shake);
                root.Children.Add(camera);
            },
            new CameraShakeFeedback { Trauma = 0.8f });
        player.Intensity = 0.5f;

        player.Play(0.5f);
        game.Frames(1);

        Assert.Equal(0.2f, shake.Trauma, 3);
    }

    /// <summary>A play is refused by cooldown, by a running play when overlap is off, by chance, and without a runner.</summary>
    [Fact]
    public void PlaysAreRefusedWhenTheyShouldBe()
    {
        var (_, _, player) = Build(new PauseFeedback { Duration = 0.5f }, new PauseFeedback { Enabled = false, Chance = 0f });
        player.Cooldown = 1f;
        player.CanPlayWhilePlaying = false;

        Assert.True(player.Play().IsActive);
        Assert.False(player.Play().IsActive);
        game.Frames(2);
        Assert.False(player.IsPlaying);
        Assert.False(player.Play().IsActive);
        game.Frames(2);
        Assert.True(player.Play().IsActive);

        var heard = new List<string>();
        var (_, _, never) = Build(new EventFeedback { EventName = "x", Chance = 0f });
        never.EventRaised += heard.Add;
        never.Play();
        game.Frames(2);
        Assert.Empty(heard);

        Assert.False(new FeedbackPlayer().Play().IsActive);
        new FeedbackPlayer().Stop();
    }

    /// <summary>Stopping, or disabling the player, ends the tweens its feedbacks started and removes their offsets.</summary>
    [Fact]
    public void StoppingRemovesEveryOffset()
    {
        var (_, actor, player) = Build(
            new PunchFeedback { Property = FeedbackProperty.Position, Amount = new Vector3(0, 1, 0), Duration = 2f },
            new ShakeFeedback { Property = FeedbackProperty.Scale, Strength = new Vector3(0.5f), Duration = 2f });
        var awaited = player.Play().AsTask();
        game.Frames(2);
        Assert.NotEqual(Vector3.Zero, actor.Position);

        player.Stop();

        AssertNear(Vector3.Zero, actor.Position);
        AssertNear(Vector3.One, actor.Scale);
        Assert.True(awaited.IsCanceled);
        Assert.Equal(0, game.Runner.ActiveCount);

        player.Play();
        game.Frames(1);
        player.IsActive = false;
        AssertNear(Vector3.Zero, actor.Position);
        Assert.False(player.IsPlaying);
    }

    /// <summary>A player set to play on start does so once the game starts.</summary>
    [Fact]
    public void PlayOnStartPlaysWhenTheGameStarts()
    {
        var heard = new List<string>();
        var (root, actor) = (new Node { Name = "Level" }, new Node { Name = "Actor" });
        var player = new FeedbackPlayer { PlayOnStart = true, Feedbacks = [new EventFeedback { EventName = "go" }] };
        actor.Components.Add(player);
        root.Children.Add(actor);
        player.EventRaised += heard.Add;

        root.Awake(null, game.SceneServices);
        root.Start();
        game.Frames(2);

        Assert.Equal(["go"], heard);
    }

    /// <summary>Feedbacks act on the player's node or a named descendant; an unknown name is reported.</summary>
    [Fact]
    public void TargetsAreResolvedByName()
    {
        var (_, actor, player) = Build(new TransformFeedback
        {
            Property = FeedbackProperty.Scale, To = new Vector3(3), Duration = 0.5f, TargetName = "Body", EaseName = "linear",
        });

        player.Play();
        game.Frames(4);

        Assert.Equal(new Vector3(3), actor.Children[0].Scale);
        Assert.Equal(Vector3.One, actor.Scale);
        var context = new FeedbackContext(player, game.Runner, 1f);
        Assert.Same(actor, context.Resolve("Actor"));
        Assert.Throws<InvalidOperationException>(() => context.Resolve("Nowhere"));
        Assert.Throws<InvalidOperationException>(
            () => new FeedbackContext(new FeedbackPlayer(), game.Runner, 1f).Resolve(""));
        Assert.Null(context.Find<LightComponent>(null));
    }

    /// <summary>Transform feedbacks move, turn or scale absolutely or relatively and ignore an unknown ease.</summary>
    [Fact]
    public void TransformFeedbacksChangeTheNode()
    {
        var (_, actor, player) = Build();
        actor.Position = new Vector3(1, 0, 0);
        player.Feedbacks.AddRange([
            new TransformFeedback { To = new Vector3(2, 0, 0), Relative = true, EaseName = "nonsense", Duration = 1f },
            new TransformFeedback { Property = FeedbackProperty.Rotation, To = new Vector3(0, 90, 0), EaseName = "cubic-bezier(0,0,1,1)", Duration = 1f },
            new TransformFeedback { Property = FeedbackProperty.Scale, To = Vector3.One, Relative = true, Duration = 1f },
        ]);

        player.Play();
        game.Frames(6);

        AssertNear(new Vector3(3, 0, 0), actor.Position);
        AssertNear(new Vector3(2), actor.Scale);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0, 0), actor.Orientation)) > 0.9999f);

        player.Feedbacks.Clear();
        player.Feedbacks.Add(new TransformFeedback { Property = FeedbackProperty.Rotation, To = new Vector3(0, 90, 0), Relative = true, Duration = 1f });
        player.Play();
        game.Frames(6);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.CreateFromYawPitchRoll(MathF.PI, 0, 0), actor.Orientation)) > 0.9999f);
    }

    /// <summary>Punch, shake and squash feedbacks deform the node while they play and leave it as it was.</summary>
    [Fact]
    public void DeformingFeedbacksLeaveTheNodeAsItWas()
    {
        foreach (var property in Enum.GetValues<FeedbackProperty>())
        {
            var (_, actor, player) = Build(
                new PunchFeedback { Property = property, Amount = new Vector3(0.5f), Duration = 1f },
                new ShakeFeedback { Property = property, Strength = new Vector3(0.5f), Duration = 1f, Seed = 4 },
                new SquashStretchFeedback { Amount = 0.5f, Duration = 1f });
            player.Play();
            game.Frames(2, 0.1);
            Assert.True(
                actor.Position != Vector3.Zero || actor.Scale != Vector3.One || actor.Orientation != Quaternion.Identity,
                $"{property} left the node untouched.");

            game.Frames(6);
            AssertNear(Vector3.Zero, actor.Position);
            AssertNear(Vector3.One, actor.Scale);
            Assert.True(Quaternion.Dot(Quaternion.Identity, actor.Orientation) > 0.9999f);
        }
    }

    /// <summary>Hit-stop, light flash and field-of-view feedbacks drive the scene and put it back.</summary>
    [Fact]
    public void SceneFeedbacksDriveTheSceneAndRestoreIt()
    {
        var (_, actor, player) = Build(
            new HitStopFeedback { Seconds = 0.5f },
            new LightFlashFeedback { PeakIntensity = 5f, FlashColor = true, PeakColor = new Vector4(1, 0, 0, 1), Duration = 1f },
            new FieldOfViewFeedback { Degrees = -10f, Duration = 1f });
        var (light, camera) = (new LightComponent(), new CameraComponent { FieldOfViewDegrees = 60f });
        actor.AddComponent(light);
        actor.AddComponent(camera);

        player.Play();
        game.Frames(1);
        Assert.Equal(0d, game.Clock.TimeScale);
        Assert.Equal(1f, light.Intensity);

        game.Frames(2);
        Assert.Equal(5f, light.Intensity, 3);
        Assert.Equal(new Vector4(1, 0, 0, 1), light.Color);
        Assert.Equal(50f, camera.FieldOfViewDegrees, 3);

        game.Frames(3);
        Assert.Equal(1d, game.Clock.TimeScale);
        Assert.Equal((1f, new Vector4(1), 60f), (light.Intensity, light.Color, camera.FieldOfViewDegrees));

        var (_, bare, other) = Build(new LightFlashFeedback(), new FieldOfViewFeedback());
        other.Play();
        game.Frames(1);
        Assert.Equal(Vector3.Zero, bare.Position);
    }

    /// <summary>A saved player loads back with the same feedbacks and settings.</summary>
    [Fact]
    public void PlayersSurviveSavingAndLoading()
    {
        var (root, _, player) = Build(
            new PunchFeedback { Label = "hit", Amount = new Vector3(1, 2, 3), Vibrato = 9, Delay = 0.1f },
            new HitStopFeedback { Seconds = 0.3f, StartsAfterPrevious = true },
            new EventFeedback { EventName = "boom", Chance = 0.5f });
        (player.Cooldown, player.Intensity, player.PlayOnStart) = (2f, 1.5f, true);

        var json = Serializer.Serialize(root);
        var loaded = Serializer.LoadData<Node>(json)!;
        var copy = loaded.Children[0].GetComponent<FeedbackPlayer>()!;

        Assert.Equal((2f, 1.5f, true), (copy.Cooldown, copy.Intensity, copy.PlayOnStart));
        Assert.Collection(
            copy.Feedbacks,
            static first =>
            {
                var punch = Assert.IsType<PunchFeedback>(first);
                Assert.Equal(("hit", new Vector3(1, 2, 3), 9, 0.1f), (punch.Label, punch.Amount, punch.Vibrato, punch.Delay));
            },
            static second => Assert.True(Assert.IsType<HitStopFeedback>(second).StartsAfterPrevious),
            static third => Assert.Equal("boom", Assert.IsType<EventFeedback>(third).EventName));
    }
}
