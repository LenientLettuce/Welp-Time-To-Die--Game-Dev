# Isolated dash dummy

Open `DashDummy.unity` in this folder, enter Play Mode, and focus the Game view.

- **A / left arrow:** dash left.
- **D / right arrow:** dash right.
- **Up / Down:** increase/decrease strength by 0.1 (clamped to 0.1–1).
- **Space:** pause/resume, including partway through a dash. Strength can be
  changed while paused, but movement input waits until the demo is resumed.
- **R:** cancel/reset to the starting point (does not reset the combat clock).
- Select **Dash Test Dummy** and change **Dash Strength** in the Inspector (0.1–1).
  Keyboard/Inspector changes apply to the next dash; an active dash keeps its original strength.

The top-right overlay shows total completed combat frames, strength, and pause
status. Resetting position with R preserves the total frame counter and pause state.

Every dash lasts **6 combat frames = 0.25 simulated seconds** and then stops
without sliding. Physics freezes between moves. The demo paces those six calls
at **12 combat frames per real second** (half speed), so each dash takes **0.5
real seconds** to watch. Simulation still advances 1/24 second per combat frame.
Paused wall time never accumulates or causes a catch-up jump. All movement is performed through `CombatPhysicsController` and its
physics-step hooks, not Transform movement or Update-time physics.

The test map/ruler is **20 world units** wide. Distance is linear:
`distance = mapWidth * 0.5 * strength`.

| Strength | Distance | Fraction of map width |
| --- | --- | --- |
| 0.1 | 1 unit | 0.05 |
| 0.5 | 5 units | 0.25 |
| 1.0 | 10 units | 0.50 |

Distance refers to movement of the rectangle's center in unobstructed space.
The ruler edges are visual markers, not walls, so repeated dashes can leave
the visible map; press R to reset. The body has zero gravity/damping, frozen Y
and rotation, and no automatic Rigidbody interpolation. Colliders added later
can block a dash. Map widths requiring speeds above Unity's configured maximum
are rejected rather than silently traveling too short a distance.

This entire folder is a disposable test fixture with its own assembly and scene.
It does not replace a real player, implement combat decisions/prediction, modify
the original scene, or change build settings. `DashDummyDemo` handles keyboard
input/playback only; `DashTestDummy` can also be stepped manually via its bound
clock. Disabling the dummy cancels movement and unsubscribes its callbacks.

Unity API reference checked:
[Rigidbody2D.linearVelocity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody2D-linearVelocity.html).
