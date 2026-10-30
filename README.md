# Tumbler Driving Sandbox — Unity

A personal Unity sandbox for learning and experimenting with vehicle gameplay. Drive a Tumbler inspired by the Batmobile from *Batman Begins*, explore a physics playground, and experiment with different drivetrain modes.

This repository (`racing-game`) is a learning project, not a finished commercial game. The features below are verified in source and serialized scenes; runtime verification of the repository reorganization is still pending.

## Features

- Rigidbody and WheelCollider vehicle physics, with wheel mesh updates, speed-dependent steering and downforce.
- Xbox/XInput controller bindings for acceleration, reverse, steering, braking and handbrake.
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

Requirements: Git, Unity Hub, Unity Editor **6000.0.0f1**, an HDRP-capable graphics environment, and an Xbox controller recognized by Unity as an XInput device. Other controllers and operating systems have not been verified.

1. Clone the repository:

   ```sh
   git clone https://github.com/ignabelitzky/racing-game.git
   cd racing-game
   ```

2. Install Unity **6000.0.0f1** through Unity Hub. If it is not listed, use Hub's link to the Unity download archive.
3. In Hub, choose **Add project from disk**, select this repository's root, and open it with that exact Editor version.
4. Allow Package Manager resolution and the first asset import to finish. The initial download/import includes large binary source assets.
5. Open `Assets/_Project/Scenes/Main Menu.unity` and press **Play**, then use the menu to load Sandbox. For direct driving, open `Assets/_Project/Scenes/Sandbox.unity` and press **Play**.
6. Connect an Xbox controller and use the mapping below.

Keep `Assets` and its `.meta` files, both package manifests, and `ProjectSettings` together. Avoid opening with a newer Editor unless you intend to perform a separate, reviewed upgrade. A player build has not been validated; see [validation and existing issues](docs/repository-audit.md).

## Xbox controller mapping

Bindings are read from [TumblerInput.inputactions](Assets/_Project/Input/TumblerInput.inputactions). Gamepad paths explicitly target `<XInputController>`.

| Xbox input | Action | Input System action |
| --- | --- | --- |
| Left Stick, horizontal axis | Steering | `Steer` |
| RT | Acceleration | Positive side of `Throttle` |
| LT | Reverse | Negative side of `Throttle` |
| A | Boost | `Boost` |
| B | Brake | `Brake` |
| X | Handbrake | `Handbrake` |
| Y | Upright recovery, below 1 m/s | `FlipCar` |
| RB | Cycle AWD → FWD → RWD → AWD | `SwitchDriveMode` |
| Left Stick Click | Reset position, rotation and velocity | `ResetCar` |
| Escape, keyboard | Quit / stop Editor Play mode | `QuitGame` |

The keyboard binding is `<Keyboard>/{Cancel}` (Escape on a standard keyboard). The two triggers form a single signed throttle axis. No keyboard driving bindings or additional controller compatibility are claimed. Historical action labels in the scene differ in places; event action IDs and the input asset are preserved.

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
| Input System | `TumblerInput.inputactions` supplies bindings; `Input.cs` is the generated `TumblerInput` class. Vehicle/boost poll wrappers, and scene PlayerInput events dispatch callbacks. |
| `UIController` | Finds HUD text objects and displays the current gear and drivetrain; scene Unity events connect it to vehicle state changes. |
| `MenuController` | Loads a scene by name and handles the menu exit action. Scene names remain unchanged. |

Scene objects, Inspector references and Unity events provide the wiring. There is no dependency injection or architectural framework implied. Named object lookups such as `Tumbler`, `SpeedometerNeedle`, `DriveModeText` and `GearNumText` remain significant.

The Input Actions importer now generates to `Assets/_Project/Input/Input.cs`. Its generated source was moved without edits, so its header still records the original generation path and generator version 1.8.1; the installed package version is 1.8.2. Regenerate through Unity's importer only when needed.

## Screenshots

Authentic screenshots have not been captured in this environment. This section is reserved for future Unity captures of the Tumbler, the Sandbox playground and the driving HUD.

## Current status and validation

The repository implements the sandbox systems described above. It does not claim a complete racing game, a verified release build, or cross-platform controller support. No future roadmap is committed here.

The organization and cleanup preserve retained asset GUIDs, gameplay C# source bytes, input bindings, build-scene contents, active HDRP settings and physics values. Removed files are explicitly inventoried in [cleanup-manifest.json](docs/cleanup-manifest.json); no retained package was upgraded. Available static checks pass; this does **not** establish runtime or player-build correctness. Unity 6000.0.0f1 was unavailable in the authoring environment, so import, compilation, Play mode, rendering, controller testing and build testing were **not run**.

Run the repeatable static comparison with Python 3 and Git:

```sh
python3 tools/validate-organization.py
```

See the [audit and manual checklist](docs/repository-audit.md), [machine-readable results](docs/validation-results.json), [move manifest](docs/asset-moves.json), and [scene dependency map](docs/scene-dependencies.json).

## Credits and licensing

The existing [GNU GPLv3 license](LICENSE) is retained unchanged. External assets can carry different licenses; the repository license does not establish ownership or redistribution rights for those resources.

- **Ciathyza — Gridbox Prototype Materials:** imported under `Assets/Thirdparty/Ciathyza`; Sandbox materials reference its textures. No separate license text was found in that imported folder.
- **Unity Technologies — Particle Pack:** imported under `Assets/UnityTechnologies/ParticlePack`; Sandbox references particle materials and textures, which are retained; the unused first-person demo and its legacy postprocessing dependency were removed. Its bundled `Readme.asset` identifies the pack but is not a license grant.
- **TextMeshPro / Liberation Sans:** bundled TMP resources; Liberation Sans credits Red Hat, Inc. and includes the [SIL Open Font License 1.1](Assets/TextMeshPro/Fonts/LiberationSans%20-%20OFL.txt).
- **Unity packages:** retain their respective package license terms.
- **Other models, audio, images, materials and adapted scripts:** authorship and original license terms are not fully established by repository metadata. Confirm their sources and permissions before redistributing them; see the audit inventory.

This is an unofficial personal project, with no affiliation with or endorsement by Warner Bros. or DC. Batman, the Tumbler design and related intellectual property belong to their respective rights holders. No ownership of those properties or external resources is claimed.

## Author

**Developer: Ignacio Belitzky**

[Portfolio](https://ignaciobelitzky.dev) · [GitHub](https://github.com/ignabelitzky)
