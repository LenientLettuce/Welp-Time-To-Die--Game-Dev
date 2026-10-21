# Combat physics clock

Add one `CombatPhysicsController` to an enabled GameObject in the combat scene.
While enabled in Play Mode it owns **the default 2D physics world**, including
objects in other scenes sharing that world. It sets `Physics2D.simulationMode`
to `Script`, then restores the previous setting when disabled/destroyed. No
scene or project settings need to be edited. No frames run until requested.
Keep it enabled during player decisions: disabling it relinquishes the freeze.

```csharp
using WelpTimeToDie.Combat;

// combatPhysics is a reference to the scene's CombatPhysicsController.
int actualFrames = combatPhysics.AdvanceFrames(
    framesUntilNextPlannedDecision,
    completedFrame => EitherFighterCanAct());
```

The names in this example belong to your future combat system. They are not
implemented here. The predicate runs after every completed frame, including
collision callbacks and `AfterFrame`, and can stop before the requested limit.
Check initial readiness before calling if no time should pass. Collision or
other callbacks may also call `RequestStop()` to stop after the current frame.

- One combat frame is 1/24 second. `CurrentFrame` counts completed frames from
  zero; hook arguments identify the frame being processed, starting at one.
- The default is two fixed physics steps per combat frame (1/48 second each).
  Set Physics Steps Per Frame in the Inspector before combat starts. More steps
  may help joints/fast bodies, but change numerical results and CPU cost.
- Order: `BeforeFrame` -> (`BeforePhysicsStep` -> sync transforms -> simulate)
  repeated for every physics step -> increment clock -> `AfterFrame` -> stop check.
- Use `BeforeFrame` for gameplay/rig targets and `BeforePhysicsStep` for forces
  and future rig evaluation. Unity clears applied forces after each simulation
  step, so continuous forces should be applied every step. Units remain Unity
  units/second and units/second squared. Use the supplied duration, not deltaTime.
- Calls are synchronous and must run on Unity's main thread. A caller can pace
  visual playback by requesting one frame at a time. Neither rendering FPS nor
  `Time.timeScale` affects the amount of simulation performed per call.
- `FramesToSeconds` derives elapsed time from the integer clock rather than
  accumulating floats. `SecondsToFrames` rounds up (durations are minimums).
- Recursive advances and multiple active controllers are rejected. A callback
  exception faults the controller rather than resuming a possibly partial frame.
  `CurrentFrame` still records fully simulated frames. No state rollback occurs;
  rebuild/reload combat state after a fault. Unity logs exceptions from its own
  collision callbacks separately; those do not necessarily propagate to this class.

## Integration boundaries

This controls Rigidbody2D/Collider2D physics only. `Update`, `FixedUpdate`, Animator,
coroutines, and 2D Animation IK's usual `LateUpdate` continue on Unity's clocks.
Future gameplay/rig code must use these hooks and explicitly evaluate animation
and IK at combat time rather than independently changing combat bodies while
the controller is paused. No Animator, IK, hit, landing, or interruption logic
is implemented here. `Time.timeScale` and `Time.fixedDeltaTime` are not changed.
For frozen visuals, use no Rigidbody2D interpolation or supply combat-time visual
interpolation later; Unity's automatic interpolation uses its own timing.

Prediction is intentionally unimplemented. Do not call `AdvanceFrames(24)` on
the live controller to preview: it commits real physics and gameplay callbacks.
A future predictor should own an isolated local 2D physics scene and copied
gameplay/rig state. This controller does not create, clone, or step that world.
Fixed stepping does not promise bit-identical physics across platforms or full
rollback determinism.

## Verification

Play Mode tests are in `Assets/Tests/PlayMode/CombatPhysicsControllerTests.cs`.
They create and clean up temporary bodies, exercise real Unity physics, and
restore global settings. Run the `WelpTimeToDie.Combat.Tests` assembly in the
Unity Test Runner. No dummy gameplay components are included in player builds.

API references checked for Unity 6000.3:
[PhysicsScene2D.Simulate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene2D.Simulate.html),
[Physics2D.simulationMode](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics2D-simulationMode.html),
[Physics2D.SyncTransforms](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics2D.SyncTransforms.html).
