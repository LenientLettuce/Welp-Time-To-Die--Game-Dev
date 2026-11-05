# Terrain-aware arena combat

Main uses the arena's 24 Hz copied-state combat model, displayed at 12 frames per
second. Primary moves, cooldowns, knockback, counters, frontal blocking, launching,
and automatic enemy decisions retain the arena's rules. One golem starts near
the blue rectangle player. Victory or defeat stops combat until Reset.

| Move | Startup | Duration | Recharge |
| --- | --- | --- | --- |
| Move | Immediate | 8f | None |
| Punch | 4f | 9f | 12f |
| Uppercut | 6f | 15f | 15f |
| Kick | 7f | 16f | 16f |
| Dash kick | 8f | 21f | 24f |
| Block | Immediate | 8f | 4f |
| Wait | Immediate | 4f | None |
| Dash (advanced) | Immediate | 6f | 12f |
| Jump (advanced) | Immediate | 12f | 16f |

Select a move and click left/right in the world to choose facing. Watch preview
rehearses the next player decision interval without committing it. Go plays the
same cached snapshots; copies protect the plan from later mutations. Unavailable
moves stay selected and explain the cooldown/ground requirement. Next frame
advances one action frame, or one idle combat frame when selection is unavailable.
Pause preserves the current action and bone pose. R/Reset restores the encounter
and the seed's original unbroken terrain. Enter commits outside text entry.

The advanced panel exposes Dash/Jump and dash strength. Dash uses the arena's
five-unit maximum distance scaled by strength. Jump requires ground contact and
uses the arena's fixed launch speed and facing-based drift. Ground movement and
dash kicks step up one block. Higher walls require jumping; knockback does not
step up cliffs. Bodies stop at terrain, tunnel ceilings, each other, and map
boundaries. Solid rock blocks melee. Vertically clear airborne bodies can pass.

The integrated code is in `Assets/Scripts/Arena`; its existing namespace and
assembly names (`WelpTimeToDie.RectanglePrototype`) remain stable:

- RectangleCombat / RectangleFighter: combat, AI, cooldowns, copied state.
- RectangleTerrain: swept block collision and copied tunnel/pit state.
- RectanglePlan: next-decision snapshots shared by preview and Go.
- RectangleArena: input, playback, pause/step, seed reset, and HUD binding.
- RectangleStage: rectangle visuals, hit feedback, and golem presentation.
- GolemPhysicsAnimator: Abbas's spring/torque solver, advanced only during
  playback or explicit frame steps. Preview saves/restores the solver state.

The golem's imported Animator is disabled. Its visible body is calibrated to
approximately 1.8 world units, with its lower edge at the logical feet.
MainCharacter, Mimic, and Stick_Figure art remains under `Assets/Sprites`.

`Assets/UI/Combat/ArenaHud.uxml` and `.uss` define the UI. MapHudPresenter binds
seeds/recenter; MapCameraInput excludes HUD interactions from map navigation.
HUD clicks do not change combat facing.

`Assets/Tests/Arena/EditMode` covers original combat rules, stepped terrain,
tall walls, fast knockback, tunnel ceilings, solid-rock occlusion, pit-preview
isolation, and seeded rehearsal/commit agreement. `AgentScripts/VerifyMergedMain.cs`
exercises real UI submission, bone freeze/restoration, Go agreement, frame stepping,
seed controls, golem alignment, and camera controls in the live Editor.
