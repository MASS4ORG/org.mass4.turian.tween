# Tweening

Enable `org.mass4.turian.tween` to use `Turian.Tweening`: tweens, sequences and easing for nodes and any property, and game-feel effects (shake, punch, squash and stretch, hit-stop, trauma shake). A running tween allocates nothing per frame.

## Tweens

Start a tween from a node in a running game; setters chain:

```csharp
Node.MoveTo(new Vector3(0, 2, 0), 0.4f).SetEase(Ease.OutBack).SetDelay(0.1f).OnComplete(Land);
await Node.ScaleTo(Vector3.One * 1.2f, 0.2f).SetLoops(2, LoopType.Yoyo);
```

- `MoveTo`, `MoveToWorld`, `RotateTo` and `ScaleTo` tween a node's transform. `TweenRunner.To` tweens any float, vector or color through a getter and a setter; pass static or cached delegates to keep creation allocation-free.
- `SetEase` takes an `Ease`, a `CubicBezier` or a name: CSS keywords (`ease-in-out`), easings.net names (`ease-out-back`) or `cubic-bezier(x1, y1, x2, y2)`, the names the in-game UI's style sheets use.
- `SetLoops(n, LoopType.Restart | Yoyo | Incremental)`, `-1` loops forever. `SetRelative`, `From`, `SetDelay`, `Pause` and `Play` do what they say.
- `SetUnscaledTime` keeps a tween playing while game time is paused or frozen.
- A tween dies with its node, with `WithCancellation(token)`, or with `Kill(complete)`. Awaiting a tween completes when it finishes and is cancelled when it is killed.

## Sequences

`TweenRunner.Sequence()` builds a timeline: `Append` plays after the previous tween, `Join` with it, `Insert` at a time, plus `AppendInterval`, `AppendCallback` and `InsertCallback`. `AsTween()` loops, delays, awaits or kills the whole timeline. Set a tween's delay and loops before adding it.

## Game feel

- `Shake` offsets a node with seeded smooth noise that fades out; the same seed replays the same shake.
- `PunchPosition`, `PunchScale` and `PunchRotation` kick a node and spring it back.
- `SquashStretch` deforms a node along an axis and wobbles it back, keeping its volume.
- `TweenRunner.HitStop(seconds)` freezes game time for real seconds, then restores the time scale.
- `TraumaShakeComponent` shakes a camera or any node by accumulated trauma that decays; `AddTrauma(0.3f)` for a bump, `1` for a blast. It keeps shaking during a hit-stop.

Shakes, punches and squashes add to whatever else moves the node and leave nothing behind when they end or are killed.

## Lifetime

`TweenRunner` is a session subsystem: components receive it with `[InjectService]`, nodes reach it through `TweenRunner.Of(node)`, and a session restart kills every tween and releases a hit-stop.

## Install

Install with `turian-cli brick add org.mass4.turian.tween` or the Bricks panel. Local checkouts can be installed with `turian-cli brick add org.mass4.turian.tween file:/absolute/path/to/org.mass4.turian.tween`.

## Development

Build against a Turian checkout at `../../Turian` relative to the brick root, or pass `-p:TurianDir=/absolute/path/to/Turian/`. From the Turian checkout, run `dotnet test --project ../Bricks/org.mass4.turian.tween/Brick~/Tests/Turian.Tweening.Tests/Turian.Tweening.Tests.csproj -p:NoLocalPackages=true`; its `global.json` selects the test runner.
