# Tweening

Enable `org.mass4.turian.tween` to use `Turian.Tweening`: tweens, sequences, easing and springs for nodes and any property, game-feel effects (shake, punch, squash and stretch, hit-stop, trauma shake) and a data-driven `FeedbackPlayer` that composes them without code. A running tween allocates nothing per frame.

## Tweens

Start a tween from a node in a running game; setters chain:

```csharp
Node.MoveTo(new Vector3(0, 2, 0), 0.4f).SetEase(Ease.OutBack).SetDelay(0.1f).OnComplete(Land);
await Node.ScaleTo(Vector3.One * 1.2f, 0.2f).SetLoops(2, LoopType.Yoyo);
```

- `MoveTo`, `MoveToWorld`, `RotateTo` and `ScaleTo` tween a node's transform. `RotateBy` turns by Euler degrees, however far. `JumpTo` hops in arcs. `MovePath` follows waypoints in lines or a Catmull-Rom curve, optionally closed.
- `MoveBlend`, `ScaleBlend` and `RotateBlend` add to every other change instead of overwriting it, so overlapping blendable tweens and plain movement combine; what they moved stays. Blend, punch and shake scale effects multiply the current scale, so `0.5` grows a node by half at any size.
- `TweenRunner.To` tweens any float, vector or color through a getter and a setter, and `TweenRunner.Value(from, to, duration, setter)` hands each value to a setter; pass static or cached delegates to keep creation allocation-free. `light.IntensityTo`, `light.ColorTo` and `camera.FieldOfViewTo` cover the engine components.
- `SetEase` takes an `Ease`, a `CubicBezier`, your own `Func<float, float>` or a name: CSS keywords (`ease-in-out`), easings.net names (`ease-out-back`) or `cubic-bezier(x1, y1, x2, y2)`, the names the in-game UI's style sheets use. `SetEaseOvershoot` and `SetEasePeriod` shape back and elastic curves.
- `SetLoops(n, LoopType.Restart | Yoyo | Incremental)`, `-1` loops forever. `SetRelative`, `From`, `SetDelay`, `SetSpeed` and `Pause` do what they say.
- `SetUnscaledTime` keeps a tween playing while game time is paused or frozen.
- `OnStart`, `OnUpdate`, `OnStepComplete` (each loop), `OnComplete` and `OnKill` are the callbacks.

## Control

- A tween dies with its node, with `SetLink(node)` for tweens that do not target one, with `WithCancellation(token)`, or with `Kill(complete)`. Awaiting a tween completes when it finishes and is cancelled when it is killed.
- `SetId(id)` tags tweens; `TweenRunner.Kill(id)`, `node.KillTweens()` and `TweenRunner.KillAll()` end many at once, and `IsTweening` asks. `PauseAll` and `PlayAll` hold and resume everything.
- `SetAutoKill(false)` keeps a finished tween so it can `Restart`, `Rewind`, `GoTo(seconds)`, `PlayBackwards`, `PlayForward`, `Flip` or `Complete`; kill it when done with it. `Elapsed`, `Duration`, `FullDuration`, `Progress`, `IsComplete` and `IsBackwards` report its state.

## Sequences

`TweenRunner.Sequence()` builds a timeline: `Append` plays after the previous tween, `Join` with it, `Insert` at a time, plus `AppendInterval`, `AppendCallback` and `InsertCallback`. `AsTween()` loops, delays, awaits or kills the whole timeline, and controls on any tween in a sequence act on the whole sequence. Set a tween's delay and loops before adding it, and finish a nested sequence before adding it to another.

## Springs

`Spring.Step(ref value, ref velocity, target, frequency, damping, deltaTime)` moves a float or a `Vector3` toward a target that may change every frame, with exact, frame-rate independent damped-spring motion. Damping 1 settles fastest without overshoot, lower wobbles, higher creeps.

## Game feel

- `Shake`, `ShakeRotation` and `ShakeScale` offset a node with seeded smooth noise that fades out; the same seed replays the same shake.
- `PunchPosition`, `PunchScale` and `PunchRotation` kick a node and spring it back.
- `SquashStretch` deforms a node along an axis and wobbles it back, keeping its volume.
- `TweenRunner.HitStop(seconds, timeScale, recoverSeconds)` freezes or slows game time for real seconds, optionally eases it back, then restores the time scale in force before.
- `TraumaShakeComponent` shakes a camera or any node by accumulated trauma that decays; `AddTrauma(0.3f)` for a bump, `1` for a blast. It keeps shaking during a hit-stop.

Shakes, punches and squashes add to whatever else moves the node and leave nothing behind when they end or are killed.

## Feedback player

Add a `FeedbackPlayer` to a node and list `Feedback` effects on it, saved with the scene or prefab, then call `Play()` from gameplay or set `PlayOnStart`. Each feedback has a `Delay`, can start after the previous one, can be skipped by `Chance`, and can act on a named descendant with `TargetName`.

- Built in: `PunchFeedback`, `ShakeFeedback`, `SquashStretchFeedback`, `TransformFeedback`, `CameraShakeFeedback`, `HitStopFeedback`, `LightFlashFeedback`, `FieldOfViewFeedback`, `PauseFeedback` and `EventFeedback`, which raises `EventRaised` so other code, such as sound, hooks into a moment.
- `Intensity` and `Play(intensity)` scale every amplitude. `Cooldown` and `CanPlayWhilePlaying` refuse repeats. `Play()` returns an awaitable tween, `Stop()` ends the play and removes every offset, and `Completed` fires at the end. Players run in real time by default so they keep going through a hit-stop.
- Bricks add their own effects by deriving from `Feedback`, giving the class a `[TypeId]` and implementing `Play(FeedbackContext)`; start tweens through `context.Own(tween)` so stopping the player ends them.

## Lifetime

`TweenRunner` is a session subsystem: components receive it with `[InjectService]`, nodes reach it through `TweenRunner.Of(node)`, and a session restart kills every tween and releases a hit-stop.

## Install

Install with `turian-cli brick add org.mass4.turian.tween` or the Bricks panel. Local checkouts can be installed with `turian-cli brick add org.mass4.turian.tween file:/absolute/path/to/org.mass4.turian.tween`.

## Development

Build against a Turian checkout at `../../Turian` relative to the brick root, or pass `-p:TurianDir=/absolute/path/to/Turian/`. From the Turian checkout, run `dotnet test --project ../Bricks/org.mass4.turian.tween/Brick~/Tests/Turian.Tweening.Tests/Turian.Tweening.Tests.csproj -p:NoLocalPackages=true`; its `global.json` selects the test runner.
