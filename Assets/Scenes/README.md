# Main gameplay scene

Open `Assets/Scenes/Main.unity` and press Play. Main combines procedural terrain,
the arena combat moves and AI, the rectangle player, and Abbas's rigged golem.
It remains the first enabled build scene and retains its original asset GUID.

Choose Move, Punch, Uppercut, Kick, Dash kick, Block, or Wait. Click the world to
face left/right, Watch preview to rehearse, and Go to commit the same snapshots.
The preview returns to unchanged live combat, terrain, and bone state. Combat and
the golem's spring physics freeze immediately at the next player decision.

Expand **Map & advanced controls** for seeds, regeneration, camera recentering,
Dash, Jump, dash strength, Pause / Resume, and Next frame. Right-drag browses the
map; scroll zooms. Enter commits and R resets outside text entry.
Reset restores both fighters and the initial terrain state for the current seed.

See `Docs/PlayerActions.md` and `Docs/ProceduralMaps.md`. The retired scenes,
controllers, UI, and tests are preserved outside Assets in
`Archives/PreMerge-2026-11-05/OriginalScenesAndSystems.unitypackage`.
