# Density 3

A Unity 6 (6000.0.75f1) first-person hand cannon shooter inspired by Destiny 2's
gunfeel. All geometry, weapons, and UI are original (primitives + procedural
generation); the gunshot recording is a royalty-free Pixabay sample.

## How to run

1. Open the project in Unity Hub (add `D:\Projects\fableTest`).
2. Open `Assets/Scenes/Title.unity` (the start screen — press Enter to play),
   or jump straight into `Assets/Scenes/TestRange.unity`.
3. Press Play.

If the scene is ever missing or broken, regenerate it from the menu:
**Density3 → Rebuild Test Range Scene**.

## Controls

| Input | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Left click | Fire (semi-auto) |
| Right click (hold) | Aim down sights |
| R | Reload |
| Shift | Sprint |
| C | Crouch (hold) |
| C while sprinting | Crouch-slide |
| Space | Jump, then strafe jump (one long lunge) or triple jump (two low hops) |
| J | Toggle jump type (strafe / triple) |
| 1 / 2 / 3 | Swap hand cannon frame |
| Esc | Free the cursor (click to re-lock) |
| Esc (title) / hold Esc (in game) | Exit to desktop |
| Enter (title) | Start the game |

## What's in the slice

- **Three hand cannon frames** (ScriptableObjects in `Assets/Weapons/`), each
  with its **own first-person model** (original primitive builds, design language
  inspired by classic D2 hand cannons — vented top rib, blade sight, exposed
  hammer; editable inside the Player prefab under Viewmodel):
  - 140 RPM Adaptive — "Last Riposte": black frame, ivory grip with spade medallions, brass trim
  - 120 RPM Aggressive — "Iron Remit": fat barrel, brass muzzle brake, walnut grip
  - 180 RPM Precision — "Dusty Vow": slim long barrel, floating rib, fiber-optic sight
- **Destiny-style combat feel**: precision (head) crits with yellow damage
  numbers, white body numbers, damage falloff past each frame's range,
  ADS zoom with reduced spread, camera + viewmodel recoil, muzzle flash, tracers.
- **Bullet magnetism** (`Assets/Scripts/Weapons/BulletMagnetism.cs`): shots bend
  toward enemies inside an aim-assist cone driven by per-frame Aim Assist /
  Range / Stability stats (0–100), with per-shot bloom, recovery, and ADS
  tightening. Select the Player in the Scene view to see the cone gizmo;
  enemies live on the "Enemy" layer (6).
- **Guardian-style movement**: sprint, momentum-boosting strafe jumps with a
  double/triple-jump toggle (J), regenerating shields (recharge after 4 s out
  of combat), death + respawn.
- **Audio** (`Assets/Scripts/Core/SFX.cs`): gunshots use a royalty-free
  recording pitched per frame (swappable on the GameManager); reload/dry-fire
  clicks, the ether-gas scream, enemy bolts, deaths, and the strafe-jump boost
  are synthesized at runtime.
- **Test range**: dummies at 10/20/35/50 m with range markers (watch falloff),
  one moving target, and three Fallen Dreg enemies that chase, strafe, and shoot back.
- **Rigged, animated enemies**: each Dreg is built on a real bone skeleton
  (`Assets/Scripts/Core/DregAnimator.cs`) and animated procedurally — idle
  breathing, a speed-driven walk cycle, weapon recoil, and eye flicker. Bones use
  humanoid naming, so a sculpted skinned mesh can bind to the same rig and reuse
  the animator unchanged.
- **Physics ragdoll + precision kills** (`Assets/Scripts/Core/DregDeath.cs`): on
  death the rig switches to a joint-driven ragdoll and is knocked back from the
  shot. A **precision (headshot) kill** bursts the Eliksni head into a blast of
  arc-blue ether (`FX.SpawnEtherBurst`), Destiny-style. The corpse lingers, then
  resets to bind pose for respawn.

## Project structure (everything is editable in the editor)

- `Assets/Prefabs/` — **Player**, **DregEnemy**, **TargetDummy**, **HUD** prefabs.
  Edit a prefab and every scene instance updates. All internal wiring (camera,
  viewmodel, bones, ragdoll bodies, UI elements) is serialized in the prefab.
- `Assets/Materials/` — all materials as `.mat` assets (Dreg armor/cloth/eyes,
  arena, gun, crit zones).
- `Assets/Weapons/` — the three hand cannon `WeaponData` assets; all balance
  (damage, RPM, falloff, magnetism stats) is Inspector-editable.
- `Assets/Scenes/TestRange.unity` — arena geometry as plain scene objects plus
  prefab instances for player/enemies/dummies/HUD.
- `Assets/Scripts/` — Core (health/damage, death/ragdoll, FX, SFX),
  Player, Weapons (HandCannon, BulletMagnetism), Enemies, UI.
- `Assets/Editor/ProjectBootstrap.cs` — **Density3 → Rebuild Test Range Scene**
  regenerates anything *missing* (deleted assets) and rewrites the scene.
  Existing prefabs/materials/weapons are never overwritten, so your edits are safe.

Cross-prefab references (enemy → player, HUD → player) resolve automatically at
runtime, so prefabs stay self-contained and you can drop more Dregs or dummies
into the scene by dragging the prefab in.

## Audio credits

- Title music: "Frozen Star" — Kevin MacLeod (incompetech.com)
- Battle music: "Space Fighter Loop" — Kevin MacLeod (incompetech.com)
- Both licensed under Creative Commons: By Attribution 4.0
  (https://creativecommons.org/licenses/by/4.0/)
- Gunshot recording: freesound_community via Pixabay (Pixabay Content License)
