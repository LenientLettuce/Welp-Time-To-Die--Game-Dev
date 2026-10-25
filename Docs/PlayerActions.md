# Click-based player actions

Open Main and press Play. The player is **16 × 32 pixels**: one tile wide and two tiles tall. The blocks retain their 16 × 16 pixel grid.

## Planning and playback

1. Click a move: **Move**, **Dash**, **Jump**, **Attack**, **Block**, or **Wait**.
2. Click the world to aim, or use the direction buttons. The yellow arrow shows aim.
3. Set strength using the slider or minus/plus buttons. It scales Move distance, Dash distance, and Jump height/speed. The current action snapshots direction and strength when committed.
4. Inspect the translucent endpoint and movement path. Prediction uses a separate 2D physics world and does not advance the live combat clock.
5. Click **Commit** to play the action at 12 combat frames per wall-clock second. The simulation still uses 24 frames per simulated second.
6. Use **Pause / Play** or **Next frame** to inspect individual frames. When ready, Next frame starts an available selected action and advances one frame. When the selected action is unavailable, it advances an idle frame so gravity and cooldowns can progress.

Time stops when the player can choose again, including in mid-air. Falling and cooldowns advance only with committed combat frames. Use another action or Wait to continue descending. Reset player returns to the terrain spawn and clears the action/cooldown state; it retains the elapsed combat clock, as before.

Movement commands do not use WASD or other gameplay keys. Left click aims; hold right mouse to browse the camera; scroll to zoom; Recenter on player restores following. UI clicks do not aim at the world.

## Frame data

| Move | Duration | Strike/startup | Recharge from action start |
| --- | --- | --- | --- |
| Move | 8f | Immediate | None |
| Dash | 6f | Immediate | 12f |
| Jump | 12f | Immediate | 16f |
| Attack | 9f | Strike on frame 4 | 12f |
| Block | 8f | Immediate | 4f |
| Wait | 4f | Immediate | None |

These preserve the earlier rectangle prototype's basic move timings. The terrain scene retains the previous dash tuning: maximum distance 10 blocks, or 5 at 50% strength. Aim also permits diagonal/air dashes. Jump always launches upward, with left/right drift from aim. It requires ground contact and cannot be started again in mid-air. Grounded movement can step up one-block terrain edges; larger height changes require a jump. Airborne motion uses gravity and collision rather than snapping to terrain height.

Attack performs a single oriented overlap query on frame four, deals 2 damage through `ICombatDamageReceiver`, and deduplicates targets with multiple colliders. Its gold rectangle shows the actual active hitbox. Block shows a directional guard; `ResolveIncomingDamage` reduces frontal damage by 3 and leaves rear damage unchanged. The Main scene contains no enemy or damage target.

## Code responsibilities

- `PlayerMoveCatalog`: move durations and recharge data.
- `PlayerActionState`: copyable action, aim, strength, and cooldown state.
- `PlayerMovementMotor`: collision, gravity, jump, dash, and one-block ground steps.
- `PlayerActionController`: connects action state and movement to the combat frame clock.
- `PlayerCombatController`: attack queries and directional guard rules.
- `PlayerActionSelection`: uncommitted move/direction/strength.
- `PlayerClickInput`: world-click aiming and playback pacing.
- `PlayerActionPreview`: isolated physics rehearsal and endpoint/path rendering.
- `PlayerActionVisuals`: aim arrow, active attack hitbox, and guard.
- `PlayerActionHudPresenter`: action buttons, direction, strength, and playback controls.

The old dash-only scripts remain available for their isolated tests and older prototypes. Main uses the new action controller/motor, with its keyboard input and ground-snapping adapter removed.

## Verification

`PlayerActionControllerTests` covers exact action boundaries, attack timing/deduplication, directional guard, mid-air decision freezes, landing, cooldowns, immutable planning snapshots, and single-frame playback. `AgentScripts/VerifyPlayerActions.cs` checks the integrated scene through its UI, compares jump/dash predictions against live outcomes, and tests actual mouse input for world aiming. `AgentScripts/ConfigurePlayerActions.cs` saves the player/controller wiring through the Editor.

Interaction reference: [Your Only Move Is HUSTLE, by ivysly](https://ivysly.itch.io/your-only-move-is-hustle). This prototype uses the plan/inspect/commit pattern with its own terrain and combat clock.
