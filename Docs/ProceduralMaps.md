# Procedural maps in Main

Main retains seeded generation, tile rendering, solid boundaries, contour
background, concealed tunnels and collapsing pits. Each source tile is 16 by 16
pixels at 16 pixels per world unit. Dimensions and feature settings remain on
Level's ProceduralTerrainMap component.

Each Play generates a fresh seed when Randomize Seed is enabled. Expand
**Map & advanced controls** to copy the current seed, Generate from seed, or
Randomize. Generation starts a fresh encounter with both fighters on the new
surface. Reusing a seed reproduces hills and feature geometry. Reset restores
that seed's original terrain and encounter. Generation controls are unavailable
while preview/action playback is active.

Right-drag browses, scroll zooms, and Recenter on player resumes following.
Camera movement does not advance combat. Ground movement steps up one block;
taller obstacles require Jump from the advanced panel. Both fighters can reveal
tunnels and collapse pit floors. AI retains the arena's attack choices and can
jump toward a higher opponent or past a tall obstacle.

Generation and tilemap ownership remain in `Assets/Scripts/Gameplay/Level`.
The immutable height map and copied TerrainFeatureState feed RectangleTerrain
in `Assets/Scripts/Arena`. Each combat snapshot owns its feature state. Preview
draws a temporary future tilemap; commit applies that state to the live tilemap
and collider. Cancelling or completing preview restores committed terrain.

Collision uses the existing axis-aligned rectangular grid and the arena's
0.9 by 1.8 unit bodies. Small swept slices and contact refinement stop fast
movement at solid cells. Rendering interpolates between combat snapshots.

The Editor preview updater refreshes changed maps and places actor roots at
their spawn surfaces. It never writes animation poses into imported bind bones.
Generator/camera/feature tests remain in `Assets/Tests/Gameplay/PlayMode`;
combat/terrain integration tests are in `Assets/Tests/Arena/EditMode`.
