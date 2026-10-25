# Basic rectangle prototype

## Open it

Stop Play mode and reopen `RectangleArena.unity` to load the updated saved objects.
The floor, blue player, and red enemy are now real SpriteRenderers in the scene.
They are visible before pressing Play. Select `Player - Blue` in the Hierarchy
and press F to frame it in Scene view; use Unity's 2D Scene view mode.

The previous version built the whole arena as runtime screen-space UI, which is
why it appeared only in the Game view during Play. Only the small HUD is UI now.

## Play

- Blue is you. Red is the enemy. The bars above them show health.
- Choose **Move**, **Punch**, **Uppercut**, **Kick**, **Dash kick**, **Block**, or **Wait**.
- Click left or right of your character to set direction. A small cyan mark shows
  the chosen direction. Then click **Go** (or press Enter).
- Time freezes when YOUR move finishes and you can choose again. Enemy AI runs
  automatically instead of forcing a Continue click whenever the enemy is ready.
- **Watch preview** animates exactly the next Go action on copied state. The
  header says PREVIEW, and the result explains hits, misses, or incoming damage.
  After a brief hold, the real position and health return unchanged. **Go** commits
  the same frame sequence. **Stop preview** returns to live positions immediately.
- An unavailable attack stays selected and explains its recharge. It never
  silently turns into Wait. Choose Move, Block, or Wait while it recharges.
- A 24-frame safety checkpoint can still show Continue for unusually long
  actions/stuns, but ordinary basic moves finish in one Go action.
- **Reset** or **R** starts again after death or victory.

The starting enemy winds up a slow strike. Try Punch first to counter it.
Hover over move buttons for details. The top bar's tooltip contains enemy intent.

The default encounter has ONE enemy and no hazards. Class menus, frame readouts,
strength sliders, detailed prediction tables, and trails are removed
from the visible interface. The arena uses most of the Game view; controls wrap
on narrower views. The camera automatically frames both fighters.

## Scene editing

`Player - Blue` and `Enemy - Red` are saved scene objects you can select and move
before Play. Their starting positions are read into the combat model on startup.
Reset returns to those captured positions. Sprite scaling is controlled by the
prototype during Play. The floor stays at the fixed combat ground height (1.4).

`RectangleStage` has explicit scene references to the camera and two fighters.
It updates their sprites, health bars, and the animated rehearsal. No overlapping
future outlines are drawn. Preview and real playback use the same camera framing
while a move is playing.
`RectangleArena` handles only the small UI, mouse direction, and paced playback.
`RectangleCombat` handles fixed-frame rules; `RectangleFighter` stores fighter data.
`RectanglePlan` copies the live model and resolves just the next human decision
interval. Both the Watch preview and Go controls use that exact list of snapshots;
Go commits independent copies so the cached rehearsal cannot be mutated.

The model still contains the more advanced class/hazard rules for later work,
but `CreateBasic` disables hazards and removes the golem. Those features do not
run in the default scene. Combat still advances in 1/24-second simulation frames,
shown at half speed, with no changes to Unity's global time or physics settings.

## Isolation and validation

Everything is in `Assets/RectanglePrototype`. Art and material assets are owned
by this prototype; the unlit shader comes from Unity's installed URP package.
No references to the main characters, dash demo, or existing combat controller
are needed. Open the prototype scene alone, not additively with other scenes.
No project settings or build settings were changed.

The scripts compile against Unity 6000.3.13f1. The model's NUnit tests cover
basic encounter isolation as well as prediction, damage, counters, blocking,
projectiles, cooldowns, jumps, hazards in the advanced model, and death. Rehearsal
tests also check next-decision timing, frame-for-frame playback agreement, safe
copying, rejection of recharging moves, and warnings when a block ends too early.

## Attack choices and feedback

Punch is the fast 4-frame interrupt. Uppercut starts in 6 frames and launches for
an aerial follow-up. Kick starts in 7 frames with longer reach and pushback.
Dash kick advances during its 8-frame startup, stops at an opponent, and has a
longer punishable recovery. Kick and dash kick stay active for 3 and 4 frames;
each swing damages each opponent at most once. All attacks respect facing,
frontal guard, hitstun, cooldowns, and the same deterministic rehearsal rules.

Enemy timing appears in the header. Amber action bars and a compressed stance
show startup, white indicates active frames, and gray indicates recovery. Strike
shapes distinguish launchers from horizontal hits. Damage adds a short playback
impact pause to both Watch and Go without changing simulation time or prediction.
