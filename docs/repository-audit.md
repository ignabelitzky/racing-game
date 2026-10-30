# Repository organization and cleanup audit — historical snapshot

> This audit records the organization/cleanup at `b6245330e0f9ec22140bed35da20393735393000`. The subsequent keyboard/Xbox update is described in the current [README](../README.md), [input simulation results](input-validation-results.json), [static results](input-static-results.json) and [additional demo cleanup](input-cleanup.json). The former migration-only validator was retired in favor of `tools/validate-project.py`; the historical counts and unchanged-scene statements below describe that earlier snapshot.

## Scope and baselines

Original main: `0cf4539c1a5ea3ff6ea07b313cefeca4a6d3b532`. Organization-only head: `19946456cbdca706a807f1bf959aa07231779709`. Inspected on 2026-10-30 through GitHub and a full local clone. All assets, scripts and scenes named in the original request existed. The user subsequently authorized removal of unnecessary files, a single rendering pipeline, merging to main and deletion of obsolete branches.

The final project retains **Unity 6000.0.0f1 and HDRP 17.0.3**. HDRP was already active; this is removal of unused pipelines, not a visual conversion or an Editor upgrade. No retained package version changes.

## Organization and runtime dependencies

```text
Before                              After
Assets/Scripts/GameManager.cs        Assets/_Project/Scripts/Core/GameManager.cs
Assets/Scripts/Tumbler/              Assets/_Project/Scripts/Vehicle/Tumbler/
Assets/Scripts/UI/                   Assets/_Project/Scripts/UI/
Assets/Scripts/Input/                Assets/_Project/Input/
Assets/Scenes/                      Assets/_Project/Scenes/
```

All moves used `git mv` with original metas. Three new folder metas were added for `_Project`, Core and Vehicle. The [move manifest](asset-moves.json) records the original 31 moves, including ObjectSpawner which was subsequently removed as unused. Retained GUIDs are never regenerated.

Main Menu uses MenuController and UI/TMP resources; its button loads `Sandbox` by unchanged scene name. Sandbox contains the Tumbler, six WheelColliders, ten Rigidbodies across the scene, terrain, vehicle/audio/effects scripts, HUD and PlayerInput callbacks. The [scene dependency map](scene-dependencies.json) records its unchanged transitive local dependencies: Main Menu has 7 local dependencies and Sandbox 51, from 16/51 direct GUIDs respectively.

The active HDRP pipeline GUID is `dc3b6d5c06e42df499acdc8306dc06ad` at `Assets/RenderingAssets/HDRP Render/HDRenderPipelineAsset.asset`. Six quality levels have no override. Both the HDRP asset and all quality settings are byte-identical to baseline. GraphicsSettings retains the HDRP global settings and removes only the unused URP global map entry. Its built-in shader entries remain because UI and imported effects may require them; these entries are not an additional installed render pipeline.

Sandbox's enabled camera brain GUID matches the [official CinemachineBrain meta](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Behaviours/CinemachineBrain.cs.meta). The project manifest pins Cinemachine 3.1.0; upstream main is used only for GUID identification.

Special Resources/Editor folders remain intact except for a pre-existing unused orphan Editor.meta. Scene names, object names, event IDs, callbacks, physics values, colliders and component settings are unchanged. Input remains XInput-specific; `activeInputHandler: 2` remains Both. The generated wrapper was moved without manual edits; its historical header retains the old path/version.

## Necessary configuration edits

Six original project files change:

1. EditorBuildSettings: both scene paths now use `_Project/Scenes`; order, enabled flags and GUIDs unchanged.
2. TumblerInput importer meta: wrapperCodePath now points to `_Project/Input/Input.cs`.
3. GraphicsSettings: remove only the unused URP global settings map entry.
4. SceneTemplateSettings: remove the two legacy PostProcessProfile/PostProcessResources type entries.
5. Packages/manifest.json: remove Universal RP and Postprocessing.
6. Packages/packages-lock.json: remove Universal RP, Universal RP config and Postprocessing; all other entries are preserved.

HDRP, Core, ShaderGraph, HDRP config, VFX Graph, mathematics, Burst and collections remain. Shared dependencies were not pruned just because URP used them.

## Files removed and evidence

The [cleanup manifest](cleanup-manifest.json) lists every removed path, byte size and SHA256, plus the exact legacy package GUID index. In total **68 tracked files / 189,045,976 bytes (180.29 MiB)** were removed from the current tree. Historical Git objects remain; no history was rewritten.

| Group | Removal and evidence |
| --- | --- |
| Unused source content | Ford GT40 model, engine-rev audio, duplicate cyan material and ObjectSpawner helper: no external GUID consumers or code/name/path loads anywhere in the repository. Each asset and its meta removed. |
| Old sky | skybox_1 material has no consumers; pure_sky EXR was consumed only by that material. Both removed with metas, plus the empty Textures folder meta. Active HDRP sky settings remain unchanged. |
| Orphan metas | Assets/Editor.meta and Ciathyza Materials/Standard.meta (plus the now-empty Materials folder meta): no payload and no GUID consumers. |
| URP | Universal RP asset/renderer/folder meta, root URP global settings, root empty DefaultVolumeProfile (used only by URP), and two unused TMP URP shadergraphs with metas: 13 files. Their consumers are exclusively within this set except for the explicitly removed GraphicsSettings map entry. |
| Legacy demo | Entire Particle Pack Scenes subtree and its meta, including exclusive baked lighting/reflections/profile, plus Shared/Prefabs/Player.prefab and meta: 39 files. No GUID consumer exists outside the deleted group. Main Menu and Sandbox do not reference this demo. |
| Legacy package | Postprocessing 3.4.0 was consumed only by the demo profile and Player prefab; reverse dependency closure adds only the demo scene. Removing these permits removal of the package without changing Sandbox's HDRP volumes/effects. |

The exact Postprocessing 3.4.0 package was inspected from the official Unity registry; its 287 meta GUIDs were cross-referenced against every tracked file, including binaries. Its tarball SHA256 is `277198219c82e40a84fc5899c1c82ed274a3f6fea0c2a3d0c57101f4c29b7ee2`. Source URL and GUID inventory are in the cleanup manifest.

Deletion audits searched textual GUIDs, UTF-16 forms, canonical binary GUIDs and common little-endian UUID/32-bit-word forms. Source inspection found no dynamic Resources/Addressables/AssetBundle or name/path loading of the removed candidates. This is stronger than merely checking two scene graphs, but it remains static evidence rather than an Editor import result.

Remaining Thirdparty/Ciathyza, UnityTechnologies Particle Pack effect resources, TextMeshPro fonts/shaders, Resources HUD art and FBX models used by Sandbox stay in their original locations. No license or credit notices are removed. The build-scene dependency map is unchanged, including imported particle materials/textures.

## Validation

The historical migration-only validator compared original files against baseline, applies only the six specified configuration changes, and allows only the inventoried deletions whose original hashes match. Missing files outside the manifest fail validation. All retained original files are compared byte-for-byte.

Additional checks cover retained GUID preservation/uniqueness, asset/meta pairs, build scene paths/GUIDs, generated wrapper target, serialized reference equality after expected configuration edits, absence of retained references to deleted GUIDs and the 287 removed package GUIDs (including common binary representations), JSON validity, package lock closure and absence of tracked generated project folders. [validation-results.json](validation-results.json) contains the historical migration results. Existing external/unresolved GUIDs are reported separately; they must not be interpreted as verified package resolution.

README local links and `git diff <original-main> --check` are also checked. .gitignore excludes Unity generated folders, IDE temporary output, OS metadata and crash reports; shared .vscode settings remain. .gitattributes retains text normalization and marks known binary sources without enabling LFS. Generic .asset files are not forced to text because Unity assets can be binary.

## Unity execution — NOT RUN

Exact Unity 6000.0.0f1 is unavailable. Installed newer Editors were not used to upgrade the project. Import, C# compilation, runtime/controller/audio/HUD/rendering and player builds have **not** been executed. The user authorized merging with these documented limitations.

Manual acceptance checklist:

- [ ] Restore packages and import with Unity 6000.0.0f1; confirm no compile/import/missing-reference errors, especially after removing URP and Postprocessing.
- [ ] Open Main Menu and load Sandbox through its button.
- [ ] Confirm Tumbler meshes/components/colliders, terrain, textures, materials and HDRP sky/rendering.
- [ ] Exercise steering, RT acceleration, LT reverse, B brake, X handbrake and A boost.
- [ ] Confirm particles, jet/engine/tire audio and tire trails/smoke.
- [ ] Cycle RB AWD/FWD/RWD; check gears, HUD, speedometer and camera.
- [ ] Test Left Stick Click reset, Y recovery below 1 m/s, Escape and menu exit.
- [ ] Build and smoke-test intended player target if baseline build issues permit.

## Existing issues and attribution

`EasySuspenssion.cs` declares `EasySuspension`; Sandbox serializes this GUID, including a disabled instance. No rename or functional fix is included. MenuController uses UnityEditor.EditorApplication behind a runtime condition rather than a compile-time guard, a potential player-build problem. Named-object lookups can dereference before null checks. Historical PlayerInput action-name strings differ from current map names, but IDs/callbacks are preserved. ProjectSettings still points templateDefaultScene to absent SampleScene.unity, unrelated to enabled build scenes. Investigate these separately; this cleanup does not claim to resolve them.

The largest retained source model is tumbler_v7.fbx (87.38 MiB); alfa_romeo.fbx (11.68 MiB) and playground models remain because they are referenced. Consider LFS separately if these sources change regularly; no filters or migration were applied.

GPLv3 and bundled credits remain unchanged. Ciathyza Gridbox textures and Unity Particle Pack effects have identifiable origins but no standalone license grants were located in their imported trees. Liberation Sans retains Red Hat copyright and SIL OFL 1.1. Model/audio/art/suspension/skidmark provenance is not fully established by repository metadata; location does not imply original authorship. No ownership of Batman/Tumbler or external resources is claimed.
