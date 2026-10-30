# Tumbler Driving Sandbox — Unity

A personal Unity sandbox for learning and experimenting with vehicle gameplay. Drive a Tumbler inspired by the Batmobile from *Batman Begins*, explore a physics playground, and experiment with different drivetrain modes.

This repository (`racing-game`) is a learning project, not a finished commercial game. The features below are verified in source and serialized scenes; isolated input simulations pass, while full-game runtime verification is still pending.

## Features

- Rigidbody and WheelCollider vehicle physics, with wheel mesh updates, speed-dependent steering and downforce.
- Xbox controller and keyboard bindings for every driving action, including WASD and arrow-key driving.
- Boost with forward impulses, particles and jet audio.
- Switchable AWD, FWD and RWD; automatic five-gear shifting.
- Vehicle reset and low-speed upright recovery.
- Engine startup, idle and speed-dependent running audio; handbrake tire sounds, trails and smoke.
- Speedometer needle, current gear and drivetrain HUD.
- Main menu and Sandbox scenes, with a Cinemachine camera setup.

## Tech stack

| Technology | Repository version / use |
| --- | --- |
| Unity 6 | **6000.0.0f1**, revision `4ff56b3ea44c` |
| C# | MonoBehaviour components and Unity events |
| Input System | **1.8.2**, `Gameplay` action map and generated `TumblerInput` wrapper |
| HDRP | **17.0.3**, active render pipeline |
| Cinemachine | **3.1.0**, camera components serialized in Sandbox |
| Unity UI / TextMeshPro | uGUI **2.0.0**, menu and driving HUD |

Versions come from `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json`. HDRP is the only installed render pipeline; Graphics Settings selects its existing asset and all quality levels inherit that selection. Unused URP and legacy Postprocessing packages, configurations and exclusive demo resources were removed after reference auditing. Other installed packages are not evidence of implemented gameplay features. No Unity or retained-package versions were upgraded.

## Getting started

Requirements: Git, Unity Hub, Unity Editor **6000.0.0f1**, an HDRP-capable graphics environment, and a keyboard or an Xbox controller recognized by Unity as a Gamepad. Physical hardware and operating-system compatibility have not been verified in this environment.

1. Clone the repository:

   ```sh
   git clone https://github.com/ignabelitzky/racing-game.git
   cd racing-game
   ```

2. Install Unity **6000.0.0f1** through Unity Hub. If it is not listed, use Hub's link to the Unity download archive.
3. In Hub, choose **Add project from disk**, select this repository's root, and open it with that exact Editor version.
4. Allow Package Manager resolution and the first asset import to finish. The initial download/import includes large binary source assets.
5. Open `Assets/_Project/Scenes/Main Menu.unity` and press **Play**, then use the menu to load Sandbox. For direct driving, open `Assets/_Project/Scenes/Sandbox.unity` and press **Play**.
6. Use the keyboard immediately, or connect an Xbox controller and use the mapping below. No input-mode selection is required.

Keep `Assets` and its `.meta` files, both package manifests, and `ProjectSettings` together. Avoid opening with a newer Editor unless you intend to perform a separate, reviewed upgrade. A player build has not been validated; see [validation and existing issues](docs/repository-audit.md).

## Controls — Xbox and keyboard

Bindings come from [TumblerInput.inputactions](Assets/_Project/Input/TumblerInput.inputactions). Xbox uses Unity's `<Gamepad>` layout, including XInput-derived Xbox devices. This avoids limiting the bindings to the Windows-specific XInput family.

| Action | Xbox controller | Keyboard |
| --- | --- | --- |
| Steering | Left Stick, horizontal axis | A / D or Left / Right Arrow |
| Acceleration | RT | W or Up Arrow |
| Reverse | LT | S or Down Arrow |
| Boost | A | Left Shift |
| Brake | B | Left Ctrl |
| Handbrake | X | Space |
| Upright recovery, below 1 m/s | Y | F |
| Cycle AWD → FWD → RWD → AWD | RB | E |
| Reset position, rotation and velocity | Left Stick Click | R |
| Quit / stop Editor Play mode | Start / Menu | Escape |

The Xbox triggers remain a signed analog throttle axis, and the stick retains analog steering. Keyboard throttle/steering are digital; opposing keyboard directions cancel. Reverse and brake remain separate controls. You can combine acceleration, steering, boost and handbrake, subject to the existing vehicle behavior. No physics values or drivetrain/gearbox logic changed.

Both device types remain available together without exclusive control schemes. One scene PlayerInput dispatches recovery, reset, drivetrain and quit events; the vehicle and boost components continue polling their generated input wrappers. Other controllers may use the same Gamepad layout, but no physical compatibility claim is made for them.

## Project structure

```text
racing-game/
├── Assets/
│   ├── _Project/
│   │   ├── Input/                 # Input actions and generated C# wrapper
│   │   ├── Scenes/                # Main Menu and Sandbox
│   │   └── Scripts/
│   │       ├── Core/             # GameManager
│   │       ├── Vehicle/Tumbler/  # Driving, boost, audio, effects, suspension
│   │       └── UI/               # Menu and HUD controllers
│   ├── Audios/                   # Existing audio sources; provenance unconfirmed
│   ├── Materials/                # Existing materials and skidmark shader
│   ├── PhysicsMaterials/         # Existing friction assets
│   ├── Prefabs/                  # Existing FBX models and playground geometry
│   ├── Resources/                # Fonts and HUD images; special Unity folder
│   ├── RenderingAssets/          # Active HDRP asset
│   ├── HDRPDefaultResources/
│   ├── TextMeshPro/              # Imported TMP resources and font license
│   ├── Thirdparty/               # Ciathyza Gridbox Prototype Materials
│   └── UnityTechnologies/        # Particle Pack
├── Packages/                     # Manifest and dependency lock
├── ProjectSettings/              # Editor, build, input, graphics and physics settings
├── docs/                         # Dependency audit, moves and validation results
└── tools/                        # Read-only static reorganization validator
```

The root-level terrain asset remains in `Assets`. Retained imported content, binary sources and special folders keep their existing locations. Verified unused sources, URP resources and the legacy Particle Pack demo were removed; the particle resources used by Sandbox remain. Project-specific organization does not imply ownership of every script or asset.

## Architecture

| Component | Responsibility |
| --- | --- |
| `TumblerController` | Reads steering/throttle/brake/handbrake, applies wheel torque and downforce, updates wheels, shifts gears, switches drive modes, and exposes speed/gear state. |
| `GameManager` | Finds the named vehicle and speedometer needle, updates the needle, handles reset, low-speed upright recovery and quit callbacks. |
| `Boost` | Reads boost input and applies forward impulses while controlling assigned particles and jet audio. |
| `EngineAudio` | Coordinates startup/idle/running sources, adjusts pitch with speed, and handles handbrake tire sounds. |
| `TumblerEffects` | Controls tire trails and smoke using handbrake, speed and grounding state. |
| `EasySuspension` | Calculates child WheelCollider spring/damper settings and suspension distances, including in Edit mode. The existing filename `EasySuspenssion.cs` is preserved; see the audit for the naming mismatch. |
| Input System | `TumblerInput.inputactions` supplies bindings; `Input.cs` is the generated `TumblerInput` class. Vehicle/boost poll wrappers, and a single scene PlayerInput dispatches recovery, reset, drivetrain and quit callbacks to their original components. |
| `UIController` | Finds HUD text objects and displays the current gear and drivetrain; scene Unity events connect it to vehicle state changes. |
| `MenuController` | Loads a scene by name and handles the menu exit action. Scene names remain unchanged. |

Scene objects, Inspector references and Unity events provide the wiring. There is no dependency injection or architectural framework implied. Named object lookups such as `Tumbler`, `SpeedometerNeedle`, `DriveModeText` and `GearNumText` remain significant.

The Input Actions importer generates `Assets/_Project/Input/Input.cs` using Input System **1.8.2**. The wrapper has been regenerated with the official generator, keeping the original action IDs and meta GUIDs. Edit the `.inputactions` source and regenerate through Unity; do not edit generated C# manually.

## Screenshots

Authentic screenshots have not been captured in this environment. This section is reserved for future Unity captures of the Tumbler, the Sandbox playground and the driving HUD.

## Current status and validation

The repository implements the sandbox systems described above. It does not claim a complete racing game, a verified release build, or cross-platform controller support. No future roadmap is committed here.

Vehicle physics, gearbox, boost force, audio/effects scripts, active HDRP configuration and package versions remain unchanged. Input bindings and the scene's input dispatch were updated together; the redundant second PlayerInput was removed. Verified unused content is inventoried in the [earlier cleanup](docs/cleanup-manifest.json) and [unused materials demo removal](docs/input-cleanup.json).

Validation performed:

- **53 assertions passed** with virtual keyboard/Xbox devices in an isolated Unity **6000.3.0f1** Play Mode harness using the project's exact **Input System 1.8.2**. Tests cover axes, keyboard combinations, all action callbacks, analog input, disconnect/reconnect and disable/reenable behavior. The imported/generated wrapper compiled and matched its source asset.
- Static comparisons check that vehicle code, physics components, colliders and unrelated scene objects/settings remain unchanged; retained GUIDs and dependencies are preserved. See [input simulation results](docs/input-validation-results.json) and [static integrity results](docs/input-static-results.json).
- **Not run:** the full game in Unity **6000.0.0f1**, physical Xbox/keyboard driving, graphics/audio/HUD checks or player builds. The newer Editor was used only for a disposable input harness, not to open or upgrade this repository.

Run static validation with Python 3 and Git:

```sh
python3 tools/validate-project.py
```

Run the isolated input simulations with an installed Unity Editor (the helper creates a temporary project and resolves Input System 1.8.2):

```sh
python3 tools/verify-inputs.py --unity /path/to/Unity
```

After intentionally changing bindings, pass `--regenerate` to copy back the official importer-generated wrapper only after all checks pass. `--input-package /path/to/extracted/package` can use a local official 1.8.2 package. `--report /path/to/results.json` saves a report; temporary files are cleaned on success and logs are kept on failure. Tests never import the full game. See the [historical organization audit and manual checklist](docs/repository-audit.md) for outstanding full-game checks.

## Credits and licensing

The existing [GNU GPLv3 license](LICENSE) is retained unchanged. External assets can carry different licenses; the repository license does not establish ownership or redistribution rights for those resources.

- **Ciathyza — Gridbox Prototype Materials:** imported under `Assets/Thirdparty/Ciathyza`; Sandbox materials reference its retained textures. The unreferenced demonstration scene and exclusive demo materials were removed. No separate license text was found in that imported folder.
- **Unity Technologies — Particle Pack:** imported under `Assets/UnityTechnologies/ParticlePack`; Sandbox references particle materials and textures, which are retained; the unused first-person demo and its legacy postprocessing dependency were removed. Its bundled `Readme.asset` identifies the pack but is not a license grant.
- **TextMeshPro / Liberation Sans:** bundled TMP resources; Liberation Sans credits Red Hat, Inc. and includes the [SIL Open Font License 1.1](Assets/TextMeshPro/Fonts/LiberationSans%20-%20OFL.txt).
- **Unity packages:** retain their respective package license terms.
- **Other models, audio, images, materials and adapted scripts:** authorship and original license terms are not fully established by repository metadata. Confirm their sources and permissions before redistributing them; see the audit inventory.

This is an unofficial personal project, with no affiliation with or endorsement by Warner Bros. or DC. Batman, the Tumbler design and related intellectual property belong to their respective rights holders. No ownership of those properties or external resources is claimed.

## Author

**Developer: Ignacio Belitzky**

[Portfolio](https://ignaciobelitzky.dev) · [GitHub](https://github.com/ignabelitzky)
