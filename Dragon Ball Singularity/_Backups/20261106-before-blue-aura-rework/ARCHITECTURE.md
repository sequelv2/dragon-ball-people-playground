# Dragon Ball Singularity — source map

The game loads `EntryDll.cs` (compiled by People Playground), which loads the prebuilt
`dragon ball.dll`. Build it with `build.ps1` (or `_build_update.cmd`, which writes `_build.log`).
`DBPU.csproj` compiles every `.cs` under `Assets/scripts` (wildcard), plus `assetloader.cs`.

## Folders (`Assets/scripts`)

| Folder | Contents |
| --- | --- |
| `Core/DBPU` | The `DBPU` partial class: `DBPU.cs` (settings fields, assets, `OnLoad`), `DBPU.Spawnables.cs` (`Main`: every spawn-menu registration), `DBPU.CharacterSetup.cs` (character spawn helpers, `ModAPIPlus`), `DBPU.SkinLoader.cs` |
| `Core`, `Core/Config`, `Core/Input`, `Core/Patches`, `Core/Fx` | Utilities, Mod Configurations menu, key bindings, Harmony patches, shared FX kit (`DbpuFx`, particles, captions, dismemberment) |
| `Ki` | `KiSystem`, charging, overdrive, deflection, power multipliers, power level |
| `Powers` | Character powers menu, power catalog, Power Giver, per-character power settings |
| `Attacks`, `Attacks/CellMax` | Blasts, beams (incl. beam clash), balls, specials |
| `Combat/*` | Melee powers, Ki Fist, Ki Shield, hand techniques (Kienzan, Hakai, Hakai Ball) |
| `Abilities` | Flight, Kaioken, Instant Transmission |
| `Transformations`, `Auras` | Forms, transformation menu, hair/eye glow, Oozaru, aura systems |
| `Fusion`, `Skins`, `Items` | Potara fusion, skins, Senzu Beans, Earth Dragon Balls |
| `UI`, `Characters`, `SpiritBomb`, `RealityRift`, `Tools`, `QA`, `ThirdParty` | As named |
| `Singularity/Core` | Module system: `SingularityModule`, `Singularity` (registry, toggles in `Mods/DBPU_Singularity.cfg`), `SingularityRunner` (fighter discovery), limb impact events, hitstop and world-freeze services, shared FX |
| `Singularity/Features` | Wave 1 modules: Shenron, Zen-O Button, Hit's Time Skip/Prison, Autonomous UI, Ultra Ego, Zenkai, Impact Frames, Tournament of Power |

`Singularity/Core/HitLens.cs` draws the gravitational lens on heavy hits. Its shader lives in
`_Pipeline/unity` and `_Pipeline/build_hitlens.ps1` rebuilds `Assets/VFX/HitLens/hitlens` (needs Unity 2020.3.1f1).

Files at the old flat paths that start with `// SINGULARITY-MOVED` are empty placeholders;
`_cleanup_moved_files.cmd` deletes them.

`Auras/ReduxAuraRig.cs` draws Super Saiyan 1/2/3 with the aura shaders and silhouettes from Dragon Ball Redux
(`RefinedAuras.Create` tries it first; the older effects remain the fallback). `_Pipeline/build_reduxaura.py` rebuilds
`Assets/VFX/ReduxAura/redux_aura` from Redux's bundle, at double resolution and under its own internal name.
`QA/ReduxAuraQa.cs` checks the three forms in a disposable game process: run the game with `-dbpu-redux-aura-qa <absolute folder>`.

## Adding a feature

1. Create `Assets/scripts/Singularity/Features/MyFeature.cs` with a class deriving from `SingularityModule`
   (`Id`, `Title`, `Summary`; override `Boot`, `RegisterSpawnables`, `PersonReady`, `Tick` as needed).
2. Add it in `Singularity.RegisterBuiltIns()` (`Singularity/Core/SingularityCore.cs`).
3. Load sprites and sounds in `Boot()` (ModAPI only accepts mod-relative paths during `OnLoad`/`Main`);
   register spawnables in `RegisterSpawnables()` with `DBPU.ModAPIPlus.CreateObject`.
4. Every module gets a toggle under Mod Configurations > Singularity Features automatically, and any
   exception it throws is logged instead of breaking the mod.

## Power scale

Every Saiyan's menus count from Base x1, followed by SSJ1 x50, SSJ2 x100, SSJ3 x400, God x20000, Blue x1000000 and Blue Evolution x20000000 (`CharacterPowerCatalog.SaiyanFormPowers`). Goku, Gohan and Vegeta (BM and DBSSH) also have innate strength of x35, x11 and x55, and Frieza of x100000000 counted from his Final Form (`CharacterPowerCatalog.InnatePowers`): it is part of the real power from spawn, so Goku's SSJ1 shows x50 and hits at x1750. `GetEffectivePower` is the real value for combat and `GetDisplayPower` is the value for menus. Stock values live in `DBPU.Spawnables.cs`, but a form saved in `Mods/DBPU_CustomTransformations.json` pins its own multiplier over the stock one. `PowerScaleVersion` in both save files marks the one-time migration to this scale.
`QA/PowerScaleQa.cs` checks the real fighters in a disposable game process: run the game with `-dbpu-power-scale-qa <absolute folder>`.

## Menus

Every menu is built on the kit in `Assets/scripts/UI/Kit`, which ships no UI art:

| File | Contents |
| --- | --- |
| `DbTheme` | Palette, form accent colours, shared font |
| `DbAtlas` | All shapes and icons, generated once onto one texture so windows batch together |
| `DbMotion` | The single tween runner; sleeps when nothing moves, uses unscaled time |
| `DbUi` | Factory and absolute-layout helpers (`Pin`, `Stretch`, `Label`, `Button`, ...), `DbArt` crops the frames baked into icons and illustrations |
| `DbControls` | `DbButton`, `DbSwitch`, `DbSlider`, `DbField`, `DbScroll`, `DbRecycler` (reuses cells in long lists), `DbChips` |
| `DbWindow` | Window frame (drag, resize, remembered placement, open/close animation) and `DbUiRoot`, the child canvas that holds all windows |
| `DbStage` | Spotlight with a sliding crossfade for character art |
| `DbSkin` | Restyles the editor tools (Transformation Maker, Skin Maker, Sprite Editor, Asset Capture) at runtime without changing their screen code |

Screens: `ConfigsWindow` (data rows are declared in `Configs`), `DbpuConfigMenuUI` (quick prompts), `PowerMenuWindow`, `TransformationMenuWindow`, `SkinMenuWindow`, `PowerGiverUI`, `CharacterPowerInputUI`, and `CombatWheelView` for both combat wheels.

Do not add a `CanvasScaler` to a menu: the game finds its own with `FindObjectOfType`. Layout is authored at 1920x1080 and `DbWindow` scales it to fit.

`QA/MenuQa.cs` opens every menu in a disposable game process and saves screenshots: run the game with `-dbpu-menu-qa <absolute folder>`.

## Dynamic beams

`Assets/scripts/Attacks/BeamPhysics` holds the "Dynamic Beams" Singularity module: spline beams (`BeamBody`), catching and swatting (`KiGuard`, shared with spheres through `GuardTarget`), head-on struggles (`BeamDuel`), the pose rig (`BeamRig`) and ground damage (`BeamGround`). Its shaders live in `_Pipeline/unity/Assets/BeamFx`; `_Pipeline/build_beamfx.ps1` rebuilds them into `Assets/VFX/BeamFx/beamfx`.

## Dynamic spheres

`Assets/scripts/Attacks/SpherePhysics` holds the "Dynamic Ki Spheres" module. `KiOrb` is the physical side of a flying sphere, with one subclass per family: `BallOrb` (Death Ball, Supernova, Beerus Ball, Gigantic Crusher), `BlastOrb` (anything fired as a `BlastComponent`), `SpiritOrb` and `HakaiOrb`. `SpherePhysics` decides who reacts and runs clashes (`OrbDuel`). `OrbFx` draws them: `OrbStyle` holds each technique's colours and light, `OrbView` one big sphere, `OrbShots` all small blasts in one mesh, `OrbBursts` the explosions. Stardust Breaker and Hakai Ball deliberately keep their own original drawing, with only the physical side added.

The look is soft cinematic light: energy added up and tone-mapped towards white, wide halos, no hard edges and no fine surface detail. Shaders live in `_Pipeline/unity/Assets/OrbFx`, with `burst.png` the explosion flipbook baked in Cycles and blurred; `_Pipeline/build_orbfx.ps1` rebuilds them into `Assets/VFX/OrbFx/orbfx`.

`QA/SpherePhysicsQa.cs` runs with `-dbpu-sphere-qa` alongside `-dbpu-beam-qa`, and `-dbpu-sphere-qa-only <names>` picks tests.

## Majin regeneration

Every Buu reassembles its own native limbs; nothing is cloned or spawned. `Characters/BuuRegeneration` is one component split by responsibility:

| File | Contents |
| --- | --- |
| `BuuRegeneration.cs` | Lifecycle, public API (`Reassemble`, `Erase`, `Stage`), save/copy hooks, native disintegration events |
| `BuuRegeneration.Body.cs` | The blueprint of the healthy body, rebuilding joints, nodes and circulation from it, healing |
| `BuuRegeneration.Matter.cs` | Surviving limbs and the crumbs native Crush registers with them: anchor, scatter, quiver, steering, merging |
| `BuuRegeneration.Recovery.cs` | The stage machine Rest, Quiver, Gather, Grow, then the closing rubber shiver |
| `BuuRecoveryTimeline.cs`, `BuuPalette.cs` | Every timing; every pink/grey colour and the Evil Buu check |

Presentation only follows the core: `BuuAssemblyView` builds each missing part from opaque pieces of its own sprite (rubber stretch in flight, seams that zip shut), `BuuFleshAssets` draws the Blender flesh over each crumb (squash on landing, swell on merging), `BuuRegenerationFx` draws droplets, steam and the head-hole vent, and plays the procedural sounds. The look is matte rubber, never ki glow.
`QA/BuuRegenerationQa.cs` runs with `-dbpu-buu-qa <absolute folder>`; add `-dbpu-buu-movement-qa`, `-dbpu-buu-copy-qa`, `-dbpu-buu-bomb-qa` for one fixture, or `-dbpu-buu-preview` to record 288 frames.
