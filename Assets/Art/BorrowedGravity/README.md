# Borrowed Gravity – Art Kit

Low-poly, flat-shaded environment kit in the sunset/vector style. There are 67 models and 3 skyboxes.
Every model uses **one material** (`BG_Palette`): each face is UV-mapped onto a colour swatch of `Textures/BG_Palette_Albedo.png`, and the glow strips come from `BG_Palette_Emission.png`.

## Setup (once)
1. Open the project in Unity and let it import.
2. Menu **Borrowed Gravity → Art Kit → 1. Build Materials + Prefabs**.
   This creates `Materials/BG_Palette.mat`, the three `Sky_*.mat` and `Prefabs/<Shared|Stage1|Stage2|Stage3>/*.prefab` (with colliders).
3. In each stage scene, run **Borrowed Gravity → Art Kit → 2. Apply Skybox: Stage N**.
4. Menu **Borrowed Gravity → Art Kit → 3. Build Stage Environments**. This creates `Assets/Scenes/Env_Stage1/2/3.unity` (sky, sun, fog and bloom already set) plus `Prefabs/Environments/Env_StageN.prefab`. Each environment is grouped by PRD set piece (5.1 to 7.8, plus Backdrop). Drag the prefab into a gameplay scene, or build gameplay into the Env scene. The layouts are in `Layouts/*.json` (from `ArtTools/gen_layouts.py`). The route runs along +X, and the camera sits at −Z.
5. For the glow to pop, add **Bloom** to the Volume profile (threshold ~0.9, intensity ~0.6). Set the Directional Light to a warm `#FFC8A0`.

## Conventions
- 1 unit = 1 m, Y up, front faces −Z.
- Walkable pieces (islands, platforms, bridges, floors, lift, docking platform): pivot = **centre of the top surface** (y = 0).
- Props, walls, towers: pivot = bottom centre. Cable and counterweight: pivot = top hook.
- `Airlock_Door_L/R` sit in the closed position when placed at the frame's origin. Slide them along local X to open.
- `Engine_Core` sits on `Engine_Carrier` at y = 0.75.
- Collider types: physics props get a **convex** MeshCollider (add a Rigidbody / `Anchorable`). The mesas get no collider.

## HUD icons (`UI/`, 36 sprites)
These import automatically as Sprites (no mipmaps, Banner_* get 40 px 9-slice borders, Tilt_Needle pivot at its hub).
- **Power cooldown**: stack `Ring_Track`, then `Ring_Cooldown`, then the `Power_A_*` / `Power_B_*` badge. Set the ring Image to *Filled → Radial 360* and drive `fillAmount`. Tint it orange (A) or cyan (B). Swap in `Ring_Active` while the power is running.
- **Gravity direction**: `Gravity_Down/Up/Left/Right/Off/Zero`. `Gravity_Arrow_Small` is the bare arrow for world-space markers.
- **Hazards**: the `Warn_*` triangles, `Countdown_Ring` (draw the number with text on top) and `Danger_Pulse` (flash it behind an icon).
- **Engine (Stage 3)**: `Meter_Engine_Frame` with `Meter_Fill` inside the well (*Filled → Horizontal*). Alternatively use `Tilt_Gauge` with `Tilt_Needle` (rotate ±75°).
- **Checkpoint / objectives**: `Checkpoint_Badge`, `Objective_Station/Engine/Ship`, and the `Banner_Checkpoint/Objective/Danger` frames.
- **Players**: `Tag_Player_A/B` go above heads. `Offscreen_Arrow` points to the other player (tint it).
- The current HUD (`GameUI.cs`) is IMGUI. It can draw these with `GUI.DrawTexture(rect, sprite.texture)`.

## Editing / regenerating
Source lives in `ArtTools/` (outside Assets):
- `gen_models.py`: all models (Python + numpy + scipy). Run: `python gen_models.py ../Assets/Art/BorrowedGravity preview`
- `gen_skies.py` + `render_skies.sh`: the three 4096×2048 panoramic skies (needs Google Chrome).
- `render.sh`: renders `preview/preview.html` contact sheets.
- `gen_hud.py` + `render_hud.sh`: the HUD icons (SVG source in `ArtTools/hud/`).
