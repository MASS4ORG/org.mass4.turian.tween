global using System.Numerics;
global using Microsoft.Extensions.DependencyInjection;
global using Turian.Engine.Core;
global using Turian.Tweening;
global using Xunit;

namespace Turian.Tests;

/// <summary>A running game with the tween brick: composed services, lifetimes with a session, and a scene ticker.</summary>
sealed class TweenGame : IDisposable
{
    public TweenGame()
    {
        Services = new ServiceCollection()
            .AddSingleton(Clock)
            .AddSingleton<GameLoopScheduler>()
            .AddSingleton<ISessionLifetime>(_ => Lifetimes!)
            .AddEngineModules(typeof(TweenModule).Assembly)
            .BuildServiceProvider();
        var host = new LifetimeHost(new SubsystemRegistry(Services.GetServices<SubsystemDescriptor>()), Services);
        Lifetimes = new GameLifetimes(host, Scenes, static _ => Task.CompletedTask);
        SceneServices = new SubsystemServiceProvider(Services, host);
        Ticker = new SceneTicker(Scenes, Clock)
        {
            Async = Services.GetRequiredService<GameLoopScheduler>(),
            Lifetimes = Lifetimes,
        };
        Lifetimes.Start();
        Scenes.BindServices(SceneServices);
    }

    public SimulationClock Clock { get; } = new();
    public ServiceProvider Services { get; }
    public SceneManager Scenes { get; } = new(new AssetDatabase());
    public GameLifetimes Lifetimes { get; }
    public IServiceProvider SceneServices { get; }
    public SceneTicker Ticker { get; }
    public TweenRunner Runner => Lifetimes.Session!.Get<TweenRunner>();

    /// <summary>A node awake in the running game, so its tweens find the runner.</summary>
    public Node Spawn(Vector3 position = default)
    {
        var node = new Node { Position = position };
        node.Awake(null, SceneServices);
        return node;
    }

    /// <summary>Advances the game by whole frames of <paramref name="seconds"/> each.</summary>
    public void Frames(int count, double seconds = 0.25)
    {
        for (var i = 0; i < count; i++)
        {
            Ticker.Tick(seconds);
        }
    }

    public void Dispose()
    {
        Lifetimes.Dispose();
        Services.Dispose();
    }
}
