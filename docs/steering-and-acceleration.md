# Full-lock steering and acceleration

Available on `main`. Baseline: `bb52d19`.

## Reproduced behavior and cause

Holding full throttle and full steering could produce a low-speed plateau in FWD and RWD, with another plateau at higher speed in AWD. No per-mode cutoff at 40 or 60 km/h exists in the controller. The existing motor torque cutoff is 175 km/h.

The original steering envelope decreased linearly from 40 degrees at rest to 15 degrees at 175 km/h. It still requested about 34 degrees at 40 km/h and 31 degrees at 60 km/h. Those angles demand very tight curves at speeds that the tires cannot sustain efficiently. Cornering, tire slip and lateral scrub consume driving force until it balances the available propulsion.

Native comparisons reproduce the symptom with active motor torque, all six wheels on a flat surface and the underbody clear of the ground. Speeds differ from the reported 40/60 km/h because these are controlled fixtures with fixed inputs. They are measured outcomes, not hard-coded speed caps. Unity describes the tire model in [Wheel collider friction](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/friction).

## Correction

`TumblerController` now limits steering demand using the vehicle's actual wheelbase, measured from the mean front/rear WheelCollider positions in local space. The Sandbox wheelbase is approximately 4.513 m.

The steering limit uses the bicycle-model relationship:

```text
lateral acceleration = speed² × tan(steering angle) / wheelbase
steering limit = atan(wheelbase × allowed acceleration / speed²)
```

Sandbox configures **Max Steering Lateral Acceleration = 9 m/s²**. The controller takes the smaller of this limit and its original steering envelope. This bounds the requested curve; it does not add grip, force the vehicle onto that curve or guarantee a measured lateral acceleration. Horizontal speed is used, so falling velocity does not reduce steering.

| Horizontal speed | Original full-input limit | Updated full-input limit |
| --- | --- | --- |
| 0 km/h | 40.0° | 40.0° |
| 20 km/h | 37.1° | 37.1° |
| 40 km/h | 34.3° | 18.2° |
| 60 km/h | 31.4° | 8.3° |
| 100 km/h | 25.7° | 3.0° |
| 175 km/h | 15.0° | 1.0° |

Full steering therefore requests a progressively wider curve as speed increases. Parking lock, steering polarity, analog input proportions and recentering remain available. The original response setting still smooths steering, using the fixed physics timestep; an outer clamp prevents the previous frame's larger angle from exceeding the current speed-dependent limit.

Motor power, drive-mode torque distribution, gear ratios, maximum-speed cutoff, brake forces, handbrake slip settings, boost, input bindings, surface grip profiles, vehicle geometry, collision hull and Rigidbody mass properties are unchanged. Straight-line acceleration is compared directly against the original steering. Modes can still accelerate differently because the existing controller applies torque per driven wheel: FWD drives two wheels, RWD four and AWD six.

Grip, propulsion and terrain still impose physical limits. The change does not promise identical speeds across drivetrains or the ability to take a fixed tight corner at 175 km/h. Wet/gravel profiles and handbrake-induced sliding retain their existing friction behavior.

For tuning, select **Tumbler → Tumbler Controller → Steering Assist**. Higher allowed lateral acceleration permits sharper curves and more tire scrub; lower values request wider curves at speed. Restart Play Mode after changing wheel positions so the wheelbase cache is refreshed.

## Validation

The [validation report](steering-validation.json) records 75 native handling/input checks and 351 driving-ground/chassis regression checks in Unity 6000.6.7f1. Mean speeds below average both steering directions over seconds 30–35 of full throttle and held steering; they are not universal maximum speeds.

| Drive mode | Original steering | Updated steering |
| --- | --- | --- |
| FWD | 30 km/h | 97 km/h |
| RWD | 34 km/h | 155 km/h |
| AWD | 108 km/h | 175 km/h |

FWD was still accelerating at the end of the held-input phase, reaching about 103 km/h by second 35. Straight-line comparison speeds remained unchanged within 0.02 km/h across modes. The per-direction measurements and minimum body clearances are in the report.

Close the project in Unity, then run with the matching installed Editor:

```sh
python3 tools/verify-handling.py --unity /path/to/Unity
python3 tools/verify-driving-ground.py --unity /path/to/Unity
python3 tools/verify-chassis.py --unity /path/to/Unity
```

The handling helper runs the actual Sandbox vehicle on a temporary 6 km flat fixture above the playground, without motion constraints or boost. It resets body/wheel velocities between trials and uses captured 0.02 s frame and physics steps. Each trial applies full throttle for 35 s with straight, full-left or full-right steering, then straightens for 8 s. The comparison replays only the old steering formula after the current controller's physics update, leaving all other physics and drivetrain code identical. Speeds are averaged over the last five seconds of held input; raw half-second samples go to ignored `TestResults/Handling/trials.json`.

The helper checks improvement in both directions for all three drivetrains, unchanged straight-line acceleration, continued tire contact, body clearance and left/right consistency. It also checks parking lock, proportional input, recentering, reverse/vertical-speed steering behavior, service brakes and the original handbrake torque/slip settings. Virtual keyboard/Xbox devices exercise the current generated input wrapper with Input System 1.20.1. Headless input settings are temporary in-memory copies and do not modify project settings. The scene and dynamic font asset are preserved.

The driving-ground and chassis helpers cover the prior surface grip, six-wheel contact and underbody changes. Physical controller driving, player builds, performance profiling and free driving on every test surface remain manual checks. Personal acceptance should include both steering directions, all drive modes, partial stick input, braking/handbrake, reversing, jumping and releasing steering after a slide.
