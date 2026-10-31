# Complete Tumbler model export

The approved equipment branch was fast-forwarded into `main`. The model snapshot uses game revision `257f247`, including the inboard placement of all six brake assemblies. It exports the assembled `Tumbler` hierarchy from `Assets/_Project/Scenes/Sandbox.unity`, rather than the original FBX alone.

## Delivered model

| Item | Export |
| --- | --- |
| Blender | `Tumbler_Complete.blend`, Blender 4.5.3 LTS, with packed textures and a separate inspection studio |
| Maya / general interchange | `Tumbler_Complete.fbx`, binary FBX 7.4, with embedded textures |
| Additional PBR interchange | `Tumbler_Complete.glb` |
| Texture editing | `Textures/`, original Unity texture references and a portable material manifest |
| Optional Maya shading | `Apply_Maya_PBR.py`, a helper to connect native `standardSurface` shaders after importing the FBX |

The delivery is generated outside the repository. Large generated model files are not added to Git. The package includes the repository license and source revision.

The model has **41 core mesh objects**, **3,427,917 triangles**, six wheels, six inboard discs, six fixed calipers, eight underbody groups, eight textured headlamp overlays, two detailed pressure tanks and hoses, and two bronze filler caps. The original body includes its existing suspension, turbine and exterior details. The replaced legacy tank submesh is absent, exactly as in the active game body. Geometry, proportions, hierarchy and local assembly transforms come from the scene. No remeshing or decimation is applied.

The model uses meters. Its game-space bounds measure approximately **3.65851 m wide × 5.72998 m long × 1.88717 m high**. The export places the vehicle pivot at the origin and converts Unity's Y-up frame to Blender's Z-up frame. FBX uses its unit/axis conversion for interchange. Bounds include the entire rendered vehicle, not only the body.

## Opening the files

In Blender, open the `.blend` file. Its textures are packed. The vehicle and inspection studio are separate collections; the studio floor, camera and area lights are excluded from FBX and GLB exports. The vehicle retains its source pivot, so the studio floor sits at the wheel contact height below that pivot.

In Maya, import `Tumbler_Complete.fbx`. The file embeds its textures; the extracted `Textures/` folder is also provided for direct editing or relinking. Maya's FBX importer may translate metallic/roughness shading into legacy material attributes. For native PBR nodes, run the contents of `Apply_Maya_PBR.py` in Maya's Python Script Editor, then call `apply_tumbler_pbr()` and select the extracted package folder. This optional helper replaces shading connections without editing geometry. It has been syntax-checked, but has not been executed in Autodesk Maya in this environment.

## Rendering and runtime behavior

HDRP shaders are translated to portable metallic/roughness materials. Non-HDR Unity base colors are converted from sRGB to linear factors, color textures retain sRGB encoding, and mask/normal maps remain non-color data. This follows Unity's [linear color workflow](https://docs.unity.cn/6000.0/Documentation/Manual/linear-color-space.html). Unity triplanar materials are baked into UV atlases with padded islands; their normal-map projection is an approximation for portable renderers. Original texture files and material references are included for further editing. Lighting, tone mapping and renderer differences can change the final appearance.

The eight headlights are present, with their original Unity settings recorded as metadata and lights off by default. Four optional turbine particle meshes are **static reference snapshots**, hidden by default. FBX/Blender do not run the Unity particle simulation, volumetric fog, brake-heating logic, stealth controls, turbo recharge, physics, engine audio or HUD. These systems remain in the Unity project on `main`. The model is delivered parked, with cold discs and lights off.

## Reproducing the export

Use an isolated clean checkout. The Editor extraction does not save the game scene. It temporarily requires the two files under `tools/model-export` to be copied into Unity's asset tree; remove that temporary asset folder after exporting. Use the project Editor version, **6000.6.7f1**, with graphics available, because baking requires the GPU. `-nographics` cannot produce these texture atlases.

Example from the repository root on Linux, using absolute output paths outside the project:

```sh
mkdir -p Assets/_ModelExportTools/Editor
cp tools/model-export/ExportTumblerSnapshot.cs Assets/_ModelExportTools/Editor/
cp tools/model-export/ExportBake.shader Assets/_ModelExportTools/

export TUMBLER_EXPORT_COMMIT="$(git rev-parse HEAD)"
export TUMBLER_EXPORT_DIR=/absolute/output/snapshot
"/absolute/path/to/Unity" -batchmode -force-vulkan \
  -projectPath "$PWD" -executeMethod ExportTumblerSnapshot.Run \
  -logFile /absolute/output/unity-export.log

python tools/model-export/prepare_textures.py \
  /absolute/output/snapshot /absolute/output/Tumbler_Complete

"/absolute/path/to/blender" --background --factory-startup --python-exit-code 1 \
  --python tools/model-export/build_tumbler.py -- \
  /absolute/output/snapshot /absolute/output/Tumbler_Complete --render
```

Texture preparation requires Python with NumPy, Pillow and SciPy. The delivered build used NumPy 2.4.6, Pillow 12.3.0 and SciPy 1.17.1. Blender supplies its own Python/NumPy for the model build. The source commit variable is required to avoid mislabeling an export.

After Unity exits, remove the temporary `Assets/_ModelExportTools` folder and its `.meta` file. Check the working tree for incidental Editor-generated changes before committing anything. Keep the snapshot and delivery outside `Assets`.

## Validation

The build checks source triangle counts across UV generation and asserts six rotors, six calipers and two canister shells. A fresh Blender process reimports the FBX from an isolated folder without its external texture directory. Validation compares all 41 core mesh objects, all 3,427,917 triangles and vehicle bounds within 0.2 mm, and checks that every texture resolves. The `.blend` is reopened separately and all 182 portable images are verified packed.

```sh
"/absolute/path/to/blender" --background --factory-startup --python-exit-code 1 \
  --python tools/model-export/validate_export.py -- \
  /isolated/folder/Tumbler_Complete.fbx \
  /absolute/output/Tumbler_Complete/source_reference/blender_build_validation.json \
  /absolute/output/Tumbler_Complete/source_reference/fbx_validation.json
```

The same validator accepts `.blend` or `.glb`. All three formats passed; see the [validation report](model-export-validation.json). Validation JSON files are also included in the delivered package. The delivery reused the already generated UV atlases and corrected their base-color tint in linear space for each material slot; the current Unity helper applies that conversion before baking in future exports. Autodesk Maya is not installed here, so a native Maya import is not claimed. The game changes already passed the [equipment and input checks](tumbler-night-systems/validation.json); export tools do not modify gameplay assets.
