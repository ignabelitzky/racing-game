# Repository organization audit

## Baseline and scope

Inspected `ignabelitzky/racing-game` through the connected GitHub integration, then cloned `main` with its history. Baseline SHA: `0cf4539c1a5ea3ff6ea07b313cefeca4a6d3b532` (project inspection performed on 2026-10-30).

All files and scenes named in the supplied inspection list existed at this SHA. Unity version is 6000.0.0f1. The dependency graph was recorded before moving anything. The change is limited to navigation, documentation, ignore/attribute rules and two required path edits.

## Asset ownership and dependency map

Project-specific scene wiring and gameplay components live in `_Project`. This identifies their role, not verified original authorship. The repository does not contain a complete provenance ledger for models, sounds, textures, suspension code or the skidmark shader.

| Area | Evidence / dependency | Decision |
| --- | --- | --- |
| Main Menu | MenuController, TMP/UI assets; button calls `LoadScene` with `Sandbox` | Move scene and controller; preserve scene name and serialized bytes |
| Sandbox | Vehicle and HUD scripts, input asset, six WheelColliders, ten Rigidbodies across the scene, models, terrain, audio, prototype materials, TMP and Particle Pack resources | Move scene; preserve every component, fileID, event and reference |
| Gameplay scripts | GameManager, TumblerController, Boost, EngineAudio, TumblerEffects, UIController are referenced by Sandbox | Group under Core, Vehicle/Tumbler and UI; preserve class names and content |
| ObjectSpawner | Present in source, absent from the two build scenes' GUID graphs | Retain under Core; do not remove an apparently unused helper |
| Suspension | `EasySuspenssion.cs` declares `EasySuspension`; its GUID occurs in Sandbox | Move with meta; preserve mismatch rather than changing component identity |
| Input | `Gameplay` action map; explicit XInput bindings; no control schemes; PlayerInput event wiring in Sandbox; polling wrappers in controller and boost | Move asset and generated wrapper together, update importer output path only |
| Rendering | GraphicsSettings selects GUID `dc3b6d5c06e42df499acdc8306dc06ad`, resolving to `RenderingAssets/HDRP Render/HDRenderPipelineAsset.asset`; six quality levels have null overrides | HDRP 17.0.3 is active; leave rendering resources and URP installed assets intact |
| Camera | Sandbox Main Camera has enabled script GUID `72ece51f2901e7445ab60da3685d6b5f` | Identified as CinemachineBrain by the official upstream meta; package manifest pins Cinemachine 3.1.0 |
| Thirdparty/Ciathyza | Gridbox Prototype Materials; project materials reference imported textures | Leave complete imported tree in place |
| UnityTechnologies | Particle Pack, with its own Editor scripts and Readme; Sandbox references particle materials/textures | Leave complete imported tree, including special Editor folders, in place |
| TextMeshPro | Fonts, shaders, Resources and OFL text | Leave imported resources and notices in place |
| Resources | Fonts and HUD dial/needle images; serialized scene dependencies | Preserve special path and all contents |
| Prefabs | Actually includes FBX source models, including Tumbler and playground geometry; imported subobjects have serialized fileIDs | Leave names, import settings, externalObjects mappings and binary contents unchanged |
| Audios, Materials, PhysicsMaterials, Textures | Referenced runtime source content, partly uncertain provenance; imported shader and material dependencies | Leave in place to avoid claiming ownership or restructuring imported bundles |
| Root terrain and render assets | Includes `New Terrain 1.asset`, DefaultVolumeProfile and URP global settings | Retain; no visual or pipeline change |

The pre-move textual GUID traversal found 16 distinct direct GUIDs / 7 transitive local assets for Main Menu and 51 direct GUIDs / 51 transitive local assets for Sandbox. [scene-dependencies.json](scene-dependencies.json) lists the local transitive dependencies at their final paths. This scan follows serialized textual GUIDs and importer references; it cannot infer hidden references inside binaries or resolve package-internal resources before Unity package restoration. Identical binary hashes and unchanged import metadata provide the conservative safeguard for those files.

Cinemachine identification source: [official CinemachineBrain.cs.meta](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Behaviours/CinemachineBrain.cs.meta). This confirms the component GUID; the installed version comes from this project's manifest, not upstream main.

## Before and after

```text
Before                                    After
Assets/Scripts/GameManager.cs              Assets/_Project/Scripts/Core/GameManager.cs
Assets/Scripts/ObjectSpawner.cs            Assets/_Project/Scripts/Core/ObjectSpawner.cs
Assets/Scripts/Tumbler/*                   Assets/_Project/Scripts/Vehicle/Tumbler/*
Assets/Scripts/UI/*                        Assets/_Project/Scripts/UI/*
Assets/Scripts/Input/*                     Assets/_Project/Input/*
Assets/Scenes/*                            Assets/_Project/Scenes/*
```

Each move used `git mv` and included the original `.meta`. The Scripts, Tumbler, UI, Input and Scenes folder metas also moved with their original GUIDs. Three new folder metas are required for `_Project`, `Core` and `Vehicle`; only these receive new GUIDs. [asset-moves.json](asset-moves.json) records all 31 tracked path moves, including folder metas.

Only two existing project file contents change:

1. `ProjectSettings/EditorBuildSettings.asset`: scene paths now point into `_Project/Scenes`; order, enabled flags and scene GUIDs remain unchanged (Main Menu first, Sandbox second).
2. `Assets/_Project/Input/TumblerInput.inputactions.meta`: `wrapperCodePath` now targets `Assets/_Project/Input/Input.cs`; all other importer settings and GUIDs remain unchanged.

The wrapper is byte-identical generated code. Its header's old generation path is historical metadata, not a runtime lookup. Scene loading uses the unchanged `Sandbox` name, and there are no runtime Resources.Load or hardcoded moved asset paths in the project scripts. `ProjectSettings.asset` already contains a templateDefaultScene path to absent `Assets/Scenes/SampleScene.unity`; it is unrelated to either enabled build scene and is left unchanged.

## Static validation results

Run `python3 tools/validate-organization.py` from the repository root. The script compares against the full recorded baseline SHA; an optional argument selects another baseline for investigation. It is a migration-specific integrity check, not a general Unity test suite.

[validation-results.json](validation-results.json) records the actual run:

- 1,124 original files under Assets, Packages and ProjectSettings checked; none removed.
- 1,122 files byte-identical; two match exact, allowlisted path substitutions.
- All 594 original meta GUIDs resolve at their expected final paths; 597 current GUIDs are unique.
- All 6,890 serialized GUID reference occurrences match before/after, accounting for moved source paths.
- Every original scene, prefab, shader, material, model, audio file, generated wrapper and other C# source is byte-identical. Their local fileIDs, component references and importer dependencies therefore remain unchanged.
- Both build scene paths exist and match their meta GUIDs; generated-wrapper output target exists.
- Package manifests/lock and Input Actions parse as JSON and are unchanged.
- Graphics, quality, input handler and physics settings are byte-identical. `activeInputHandler: 2` (Both) is retained.
- No Library, Temp, Obj, Build, Builds, Logs or UserSettings directories are tracked.

There are **536 unchanged external or unresolved GUIDs** in baseline text, including package references. Their preservation establishes that this change introduces no new GUID target losses; it does not establish that all baseline references resolve in Unity. Package restoration and Editor inspection are still necessary. Existing orphan folder metas are `Assets/Editor.meta` and `Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/Standard.meta`; neither has a tracked folder payload. They remain unchanged and are reported separately, not silently removed.

## Manual Unity acceptance checklist — NOT RUN

Unity Editor **6000.0.0f1** was not installed in the available environment. Installed 6000.3.0f1 and 6000.6.7f1 were deliberately not used to open or upgrade this project. No Editor import, compilation, runtime, screenshot or build result is claimed.

Before merging, use the exact version and an Xbox/XInput controller:

- [ ] Restore packages, complete import and confirm C# compilation without errors.
- [ ] Open Main Menu, check Console, and load Sandbox through its button.
- [ ] Confirm Tumbler meshes, components, colliders, terrain, materials, textures and HDRP rendering.
- [ ] Exercise steering, RT acceleration, LT reverse and B braking.
- [ ] Exercise A boost, particles/jet audio and X handbrake with tire trails/smoke/screech.
- [ ] Cycle RB through AWD, FWD and RWD; check gear changes and both HUD labels.
- [ ] Check Left Stick Click reset and Y upright recovery below 1 m/s.
- [ ] Check startup/idle/running audio, camera and speedometer needle.
- [ ] Verify Escape quit/stop and menu exit behavior; inspect Console for missing scripts/references.
- [ ] Build for the intended supported target and smoke-test the player, if baseline build issues permit it.

## Existing issues outside scope

- `EasySuspenssion.cs` and `EasySuspension` do not match. Sandbox serializes this script GUID (including a disabled instance). Unity component recognition needs manual inspection; renaming was not assumed safe.
- `MenuController.ExitGame` uses `UnityEditor.EditorApplication` behind a runtime `Application.isEditor` condition rather than a compile-time editor guard. This is a potential player-build compilation problem; no code fix is included.
- GameManager and UIController rely on exact object names and some dereferences precede null checks. Scene bytes and names remain unchanged; no general logic refactoring is included.
- Sandbox contains historical PlayerInput action-name strings inconsistent with current map labels, while action IDs and callbacks remain intact. No rebinding or scene serialization changes were made.
- Two orphan metas, unresolved GUIDs and the absent template scene path predate this change. Investigate independently in the exact Editor; do not delete assets on the basis of the static scan.
- Materials such as `prototype_512x512_cyan 1.mat`, extra car models and `engine-rev.ogg` may look redundant, but no deletion is justified merely by names or absence from the two scene graphs. All retained.

## Git hygiene and large assets

The existing Unity ignore rules already covered generated project folders and IDE build files. Additional exclusions cover local IDE/OS state, swap files, `.slnx` and crash diagnostics. Shared `.vscode` configuration is retained. Attributes retain automatic text handling, add explicit source/Unity text types, and mark known binary source formats to prevent text conversion. Generic `.asset` files are intentionally not forced to text because Unity assets may be binary.

Largest source assets in the baseline (MiB, rounded):

| File | MiB |
| --- | ---: |
| `Assets/Prefabs/ford_gt40.fbx` | 99.59 |
| `Assets/Prefabs/tumbler_v7.fbx` | 87.38 |
| `Assets/Textures/pure_sky.exr` | 70.52 |
| `Assets/Prefabs/alfa_romeo.fbx` | 11.68 |
| `Assets/Prefabs/Playground_prefabs/complete_grill_fence.fbx` | 8.97 |
| `Assets/Prefabs/Playground_prefabs/half_grill_fence.fbx` | 8.96 |

Git LFS merits a separate decision for these FBX/EXR sources and large Particle Pack texture sources if they will change regularly. Plan client setup, hosting quotas and collaborator access first. No LFS filters, migrations, history rewrites, model removal or binary changes are included. GPLv3 and all bundled notices are byte-identical.

## Attribution follow-up

The known external asset trees stay separated. Ciathyza Gridbox Prototype Materials and Unity Particle Pack are identifiable from imported folder names and bundled Particle Pack Readme, but no standalone license grant was located in those trees. Liberation Sans includes Red Hat copyright and SIL OFL 1.1 text in `Assets/TextMeshPro/Fonts/LiberationSans - OFL.txt`.

Model filenames, audio filenames, HUD art, sky source, skidmark shader, friction assets and suspension code do not establish their authorship. Obtain and record original source links/license evidence before redistributing or presenting them as original work. This audit documents repository evidence and does not replace the existing license or certify third-party permissions.
