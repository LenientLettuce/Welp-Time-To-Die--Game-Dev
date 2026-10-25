# Main gameplay scene

Open `Assets/Scenes/Main.unity` and enter Play mode. Main is the first enabled
scene in Build Settings. The former DashDummy scene was moved with its original
GUID; the rectangles, ruler, HUD and six-frame dash are preserved.

## Controls

- A / Left: dash left. D / Right: dash right.
- Up / Down: adjust the next dash's strength by 0.1, from 0.1 to 1.
- Space: pause or resume, including during a dash.
- R: return to the initial player position and cancel the current dash.
  The combat frame counter and pause state are preserved.

A dash lasts six combat frames (0.25 simulated seconds), shown at 12 combat
frames per real second (0.5 seconds of playback). Physics freezes between moves.
Distance is half the level's world width multiplied by dash strength. Changes
to level size or strength apply to the next dash; active moves retain their speed.
The prototype's ruler and edges are visual markers, so repeated dashes can leave
the visible area. Colliders can block movement without changing the combat clock.

## Responsibilities

| Component | Responsibility |
| --- | --- |
| `CombatPhysicsController` | Owns manual 2D physics and the 24 Hz combat clock. |
| `PlayerDashController` | Rigidbody movement, dash duration, strength and spawn reset. |
| `CombatPlaybackController` | Wall-time pacing, pause state and gameplay commands. |
| `PlayerKeyboardInput` | Samples keyboard controls and feeds playback each render frame. |
| `LevelLayout` | Authored cell bounds, cell/world conversion and world bounds. |
| `LevelCameraFraming` | Fits the orthographic camera to the level's bounds. |
| `CombatHudPresenter` | Reads playback state into the saved UI Toolkit HUD. |

Runtime scripts live in `Assets/Scripts/Gameplay`, grouped by feature, and use
the `WelpTimeToDie.Gameplay` assembly. The physics clock stays in its independent
Combat assembly. Regression tests live in `Assets/Tests/Gameplay/PlayMode`.
HUD assets are in `Assets/UI/Combat`; placeholder art is in `Assets/Art/Prototype`.

## Preparing a tiled level

`Level` owns a rectangular Unity Grid and `LevelLayout`. Its default playable
cells start at (-10, -1, 0) and span (20, 4, 1), with one world unit per cell.
Select Level to inspect its bounds gizmo. The camera and dash use these authored
bounds rather than a renderer's size or the occupied tile area.

The empty child Tilemaps are `Background`, `Terrain`, and `Decoration`, with
sorting orders -10, 0 and 5. They are ready for future tile painting; no tiles
are painted or collision rules introduced by this refactor. Existing imported
tile art remains in `Assets/MapTiles`.

When replacing the rectangles, paint the Tilemaps, set the playable cell bounds
for the new level, and disable or remove `Prototype Geometry (visual only)`.
That child contains the original fixed-size ruler; resizing the authored bounds
does not stretch its debug markings. Add terrain collision when the tile shapes
and traversal rules are decided. Keep actors and systems outside Level so changes
to the visual map hierarchy do not rescale the player or combat clock.

For actors in isolated tests without a LevelLayout, `FallbackLevelWidth` supplies
the reference width. In Main, the player's explicit Level reference takes priority.
