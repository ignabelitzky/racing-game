# Underbody collision and ledge recovery

Available on `main`. This update follows the six-wheel `IsGrounded()` correction in `e86319b`.

This document records the underbody update in `bb52d19`. The subsequent [steering correction](steering-and-acceleration.md) changes speed-dependent steering without changing this collision hull, its material or mass properties.

The newer [vehicle upgrade](tumbler-upgrade.md) also retains this hull and mass properties while adding the seven-speed drivetrain, digital instruments and outer/front visual detailing.

## Contact with edges

The original Sandbox Tumbler used a wide rectangular lower BoxCollider. On a sharp platform edge, that box could support most of the vehicle's weight while all four rear wheels still reported contact. The resulting low tire load and friction between the body and platform could prevent movement in either direction, including AWD and RWD.

Sandbox now uses one small **convex MeshCollider** for the lower body. Its 16 vertices and 28 triangles form a closed solid with two bevel stages around the bottom perimeter. The bevel rises 0.24 m, with the lowest perimeter inset by 0.22 m on each side and 0.55 m at each end. This removes the square lower corners that could catch on an edge.

The hull retains the previous box's exact outer bounds: size `(3.4234967, 0.73455465, 5.6127586)` and center `(0.010071993, -0.36884636, 0.14504433)` in the Body object's local space. Its lowest point and the vehicle's ground clearance are unchanged. The upper BoxCollider remains unchanged.

The collision mesh is a separate asset, `Assets/_Project/Physics/TumblerLowerChassis.asset`. It has no renderer. The original FBX, visible body and wheel meshes, proportions, materials and approved mechanical underbody details are unchanged.

## Lower-body friction

Only the lower hull uses `Assets/_Project/Physics/TumblerUnderbodyGlide.physicMaterial`:

| Setting | Value |
| --- | --- |
| Static friction | 0.04 |
| Dynamic friction | 0.025 |
| Friction combine | Minimum |
| Bounciness | 0 |

Minimum preserves low friction against the existing Average road materials, instead of averaging a low body coefficient with a higher road coefficient. A future road material using Multiply or Maximum has higher combine priority and must be evaluated separately. See [Unity's combine rules](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-surfaces/combine).

The cabin keeps its original LowFriction material. WheelCollider tire friction is calculated separately from standard collider materials, so the existing asphalt, gravel and wet-lane grip profiles retain their settings. See [Unity WheelCollider](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/wheelcollider).

## Preserving the driving setup

Changing collider volume also changes automatic Rigidbody mass properties. The original box-derived center of mass, inertia tensor and inertia rotation were measured in Unity and explicitly retained on the 3000 kg vehicle. Suspension, wheel colliders, torque, steering, braking, drivetrain modes, boost, inputs and recovery controls retain their existing settings and code.

If the mass distribution is intentionally redesigned later, review these explicit Rigidbody values rather than assuming Unity will recalculate them automatically.

These changes reduce underbody drag and corner snagging. They do not guarantee recovery from every obstacle: a fully suspended vehicle can still lack tire traction. There is no additional automatic lift, motor power or recovery impulse.

## Validation

The [validation report](underbody-collision-validation.json) records 39 native asset checks, 24 runtime checks, the 288 existing driving-ground/contact checks, scene preservation and measured comparison results in Unity 6000.6.7f1.

Run with the matching Unity Editor while the project is closed:

```sh
python3 tools/verify-chassis.py --unity /path/to/Unity
python3 tools/verify-driving-ground.py --unity /path/to/Unity
```

The chassis helper checks native hull geometry, materials and original mass properties, then compares the actual Sandbox vehicle with a temporary recreation of its original lower box. Tests use deterministic 0.02 s physics steps and temporary fixtures above the playground:

- A narrow 7-degree rail supports the underbody with all tires airborne, isolating friction under gravity.
- A 6 cm step on a narrow rail compares the original square box and the beveled hull using the same low friction and a 9000 N horizontal test force. This force exists only in the verifier.
- A 1.5 m platform edge compares the original and updated configurations, with wheel contact forces recorded before applying the existing controller's drive torque.
- An isolated lower-body impact onto a 10 cm platform checks the existing collision detection mode at a 70 m/s downward speed.

The helper resets body and wheel velocities between trials. Fore/aft trials constrain sideways motion, roll and yaw; rail/step trials additionally hold body rotation to isolate shape and friction. These are controlled regression fixtures, not a replacement for free driving on the user's PC. Results go to ignored `TestResults/Chassis/`; no scene or test fixture is saved. The helper preserves the original dynamic font asset if TMP updates it.

The existing driving-ground helper checks scene contacts, grip profiles, handbrake behavior and six-wheel ground detection. Manual acceptance should include slow approaches to the original orange ramps, braking while hung on a lip, reversing, oblique contacts, jumping, landing and reset with keyboard/Xbox controls. Player builds, physical controller driving and exhaustive obstacle coverage remain manual checks.

In the representative platform fixture, the original configuration remained stationary under throttle in AWD, RWD and FWD while the four rear wheels touched the platform. Their combined contact force was about 661 N. With the beveled hull and glide material, the four rear wheels supported about 26.6 kN before throttle, and the vehicle left the edge forward in all three modes. The original configuration also stayed still in reverse; the updated configuration moved about 1.7 m back toward the upper platform but did not fully return to it. Forward escape and reverse progress are distinct outcomes; full reverse recovery is not claimed.
