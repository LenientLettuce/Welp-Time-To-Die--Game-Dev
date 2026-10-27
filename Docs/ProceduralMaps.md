# Procedural map prototype

Open `Assets/Scenes/Main.unity` and press Play. The map starts with a fresh seed each run.

The current player moveset is documented in [PlayerActions.md](PlayerActions.md). Main now uses click/button planning, jumping, and physics-based airborne movement. The player is one block wide and two blocks tall.

## Controls

- Hold the right mouse button and drag to browse the map. Release to keep browsing from that location.
- Scroll to zoom. The initial orthographic size is 6.75, matching the previous view (about 24 blocks across at 16:9).
- Click **Recenter on player** to resume following. Recentring retains the selected zoom.
- Enter any text or number in **Seed**, then click **Generate from seed** to reproduce a map. **Randomize** creates a fresh seed.
- The current seed is displayed and placed in the editable seed field so it can be copied.
- Player actions, direction, strength, playback, and reset use on-screen buttons. Left clicking the world sets aim. Seed-field and UI clicks do not aim at the world.

## Inspector settings

Select **Level**, then expand **Procedural Terrain Map → Settings**:

- **Width In Blocks / Height In Blocks**: defaults 128 × 32. Height includes ground and air. Supported ranges are 4–2048 wide and 4–512 high.
- **Base Ground Height**: average filled depth measured from the bottom.
- **Height Variation**: hill amplitude in blocks. Adjacent columns differ by at most one block.
- **Hill Width In Blocks**: horizontal spacing of hill samples.
- **Randomize Seed**: fresh seed each Play. Disable to use **Seed Input** on startup.

After changing settings outside Play Mode, use the component menu's **Generate Using Seed Input** or **Generate Random Map** to update the Scene view preview, then save the scene. Entering Play regenerates automatically. Changes made during Play follow Unity's usual temporary Inspector behavior.

Each tile is 16 × 16 source pixels at 16 pixels per unit: one block equals one Unity unit. The default map is 2048 × 512 source pixels and 128 × 32 world units. Scaling the Game view or zooming does not change the block grid.

Select **Main Camera → Map Camera Controller** to adjust initial/minimum/maximum zoom or follow offset. The previous full-map framing component is disabled. Visible side lines and solid colliders cover the complete map height; invisible top/bottom colliders close the outer limits.

## Responsibilities

| Class | Responsibility |
| --- | --- |
| `TerrainSettings` | Serialized block dimensions and generation parameters |
| `TerrainHeightGenerator` | Deterministic seeded hill generation |
| `TerrainHeightMap` | Immutable column data and solidity queries |
| `ProceduralTerrainMap` | Coordinates generation, layout, rendering, and boundaries |
| `TerrainTilemapRenderer` | Paints solid columns into the existing Tilemap and updates its collider |
| `MapBoundaryController` | Positions four collision limits and two side lines |
| `PlayerTerrainFollower` | Adapts the current horizontal dash prototype to the stepped floor |
| `MapCameraController` | Follow/browse state, zoom, recentering, and camera limits |
| `MapCameraInput` | Mouse input and exclusion of HUD interactions |
| `MapHudPresenter` | Seed controls and recenter button binding |

`PlayerTerrainFollower` is retained for the earlier dash-only prototype. Main uses `PlayerMovementMotor` for gravity, airborne movement, and terrain collision on the combat clock. Dash remains six frames and travels five blocks at 50% strength. Terrain generation reserves at least three empty rows above its tallest ground column for the two-block-tall player.

The generator fills each column from the bottom; it creates no gaps, caves, floating platforms, or additional content. The runtime systems expect the scene's existing axis-aligned, unit-cell Grid.

## Verification and scene setup

`ProceduralMapTests` covers dimensions, solid columns, height steps, seed repeatability, camera bounds, browse state, recentering, and zoom limits. Existing gameplay/combat tests remain applicable.

`AgentScripts/ConfigureProceduralMap.cs` is a repeatable Editor scene setup entry point, invoked using Unity Pipeline `run_script`. `AgentScripts/VerifyProceduralMap.cs` verifies tile occupancy, physical walls, seed controls, and recentering. `AgentScripts/VerifyPlayerActions.cs` verifies the current moveset and traversal. These scripts are outside Assets and do not ship in the player.
