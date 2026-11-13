namespace Turian.Tweening;

/// <summary>Registers the <see cref="TweenRunner"/> as a session subsystem, so a restarted session starts with no tweens.</summary>
public sealed class TweenModule : IEngineServiceModule
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSubsystem(LifetimeScope.Session, static scope => new TweenRunner(
            scope.Services?.GetService<GameLoopScheduler>(), scope.Services?.GetService<SimulationClock>()));
    }
}
