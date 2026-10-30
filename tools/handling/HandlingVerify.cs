using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class HandlingVerify
{
    const string Flag = "HandlingVerify";
    static string Folder => Environment.GetEnvironmentVariable("HANDLING_RESULTS") ?? "TestResults/Handling";
    static Rigidbody body;
    static TumblerController controller;
    static WheelCollider[] wheels, fronts;
    static MeshCollider lower;
    static int tick;
    static float legacyAngle;
    static readonly List<string> Results = new List<string>();
    static readonly List<Trial> Trials = new List<Trial>();
    static FieldInfo Field(string name) => typeof(TumblerController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    [Serializable] public class Sample
    {
        public float time, speed, frontSteer, rearSteer, torque, frontSideSlip, rearSideSlip, frontForwardSlip, rearForwardSlip, frontLoad, rearLoad, bodyClearance, upright;
        public int gear, grounded;
    }
    [Serializable] public class Trial
    {
        public string mode;
        public bool legacySteering;
        public float steer, heldMeanSpeed, heldFinalSpeed, releasedFinalSpeed;
        public Sample[] samples;
    }
    [Serializable] public class Report { public float frameDelta; public Trial[] trials; }
    static HandlingVerify() { EditorApplication.playModeStateChanged += State; EditorApplication.update += Tick; }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Results.Add("PASS " + message);
    }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var name in new[] { "trials.json", "play.txt", "failed.txt" }) File.Delete(Path.Combine(Folder, name));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void State(PlayModeStateChange state)
    {
        if (SessionState.GetBool(Flag, false) && state == PlayModeStateChange.EnteredPlayMode)
        { tick = 0; Time.captureDeltaTime = .02f; }
    }
    static void Pose(TumblerController.driveType drive)
    {
        body.constraints = RigidbodyConstraints.None; body.interpolation = RigidbodyInterpolation.None;
        body.transform.SetPositionAndRotation(new Vector3(0, 101.2f, 0), Quaternion.identity);
        body.position = body.transform.position; body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        Field("currentPower").SetValue(controller, 0f); Field("currentBrakeForce").SetValue(controller, 0f);
        Field("steerInput").SetValue(controller, 0f); Field("isHandbrakeActive").SetValue(controller, false);
        Field("currentTurnAngle").SetValue(controller, 0f); Field("<currentGear>k__BackingField").SetValue(controller, 1);
        Field("<currentDriveMode>k__BackingField").SetValue(controller, drive);
        legacyAngle = 0;
        foreach (var wheel in wheels) { wheel.motorTorque = 0; wheel.brakeTorque = 0; wheel.rotationSpeed = 0; wheel.steerAngle = 0; }
        Physics.SyncTransforms(); body.WakeUp();
        for (int i = 0; i < 100; i++) Physics.Simulate(.02f);
    }
    static Sample Measure(float time)
    {
        var sample = new Sample { time = time, speed = controller.GetSpeed(), gear = controller.currentGear, upright = body.transform.up.y,
            frontSteer = fronts.Average(w => Mathf.Abs(w.steerAngle)), rearSteer = wheels.Except(fronts).Average(w => Mathf.Abs(w.steerAngle)),
            torque = wheels.Sum(w => Mathf.Abs(w.motorTorque)), bodyClearance = lower.sharedMesh.vertices.Min(v => lower.transform.TransformPoint(v).y) - 100 };
        foreach (var wheel in wheels)
        {
            if (!wheel.GetGroundHit(out var hit)) continue;
            sample.grounded++;
            if (fronts.Contains(wheel))
            { sample.frontSideSlip += Mathf.Abs(hit.sidewaysSlip) / 2; sample.frontForwardSlip += Mathf.Abs(hit.forwardSlip) / 2; sample.frontLoad += hit.force; }
            else
            { sample.rearSideSlip += Mathf.Abs(hit.sidewaysSlip) / 4; sample.rearForwardSlip += Mathf.Abs(hit.forwardSlip) / 4; sample.rearLoad += hit.force; }
        }
        return sample;
    }
    static void DriveStep(float steer, bool legacy)
    {
        Field("steerInput").SetValue(controller, steer); Field("currentPower").SetValue(controller, 1200f);
        controller.SendMessage("FixedUpdate");
        if (legacy)
        {
            // Reproduce bb52d19's original steering only; all other game physics and
            // controller methods are identical in the paired trials.
            float speedFactor = Mathf.Clamp01(body.linearVelocity.magnitude / (controller.GetMaxSpeed() / 3.6f));
            float limit = Mathf.Lerp((float)Field("maxTurnAngle").GetValue(controller), (float)Field("maxTurnAngleAtMaxSpeed").GetValue(controller), speedFactor);
            legacyAngle = Mathf.Lerp(legacyAngle, steer * limit, Time.fixedDeltaTime * (float)Field("steeringResponse").GetValue(controller));
            foreach (var wheel in fronts) wheel.steerAngle = legacyAngle;
        }
        Physics.Simulate(.02f);
    }
    static Trial TrialRun(TumblerController.driveType drive, float steer, bool legacy)
    {
        Pose(drive);
        var samples = new List<Sample>();
        for (int i = 0; i < 1750; i++)
        {
            DriveStep(steer, legacy);
            if (i % 25 == 24) samples.Add(Measure((i + 1) * .02f));
        }
        var trial = new Trial { mode = drive.ToString(), steer = steer, legacySteering = legacy,
            heldMeanSpeed = samples.Where(s => s.time >= 30).Average(s => s.speed), heldFinalSpeed = controller.GetSpeed() };
        for (int i = 0; i < 400; i++)
        {
            DriveStep(0, legacy);
            if (i % 25 == 24) samples.Add(Measure(35 + (i + 1) * .02f));
        }
        trial.releasedFinalSpeed = controller.GetSpeed(); trial.samples = samples.ToArray(); Trials.Add(trial);
        File.WriteAllText(Path.Combine(Folder, "trials.json"), JsonUtility.ToJson(new Report { frameDelta = Time.deltaTime, trials = Trials.ToArray() }, true));
        Check(samples.All(s => s.grounded == 6 && s.upright > .95f), trial.mode + " steer=" + steer + " legacy=" + legacy + ": all six wheels remain on the flat fixture, upright");
        Check(samples.All(s => s.bodyClearance > .05f), trial.mode + " steer=" + steer + " legacy=" + legacy + ": no underbody ground contact");
        Debug.Log("HANDLING " + trial.mode + " steer=" + steer + " legacy=" + legacy + " held=" + trial.heldMeanSpeed + " release=" + trial.releasedFinalSpeed);
        return trial;
    }
    static void Controls()
    {
        var originalSettings = InputSystem.settings;
        var restoredSettings = UnityEngine.Object.Instantiate(originalSettings);
        var testSettings = UnityEngine.Object.Instantiate(originalSettings);
        InputSystem.settings = testSettings;
        testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var pad = (Gamepad)InputSystem.AddDevice("XInputController");
        var input = controller.tumblerInput;
        input.asset.devices = new InputDevice[] { keyboard, pad }; input.Enable();
        Action readInput = () => controller.SendMessage("Update");
        InputSystem.onAfterUpdate += readInput;
        try
        {
            foreach (var key in new[] { Key.A, Key.D, Key.LeftArrow, Key.RightArrow })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, key)); InputSystem.Update();
                Check((float)Field("currentPower").GetValue(controller) == 1200f &&
                    Mathf.Abs((float)Field("steerInput").GetValue(controller)) == 1f, "Keyboard throttle and full steering remain independent: " + key +
                    " (power=" + Field("currentPower").GetValue(controller) + ", steer=" + Field("steerInput").GetValue(controller) + ", key=" + keyboard.wKey.isPressed + ")");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = .6f, leftStick = new Vector2(.5f, 0) });
            InputSystem.Update();
            float steer = (float)Field("steerInput").GetValue(controller);
            Check(Mathf.Abs((float)Field("currentPower").GetValue(controller) - 720) < .01f && steer > 0 && steer < 1,
                "Xbox throttle and stick remain analog while used together");
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1, leftStick = Vector2.left });
            InputSystem.Update();
            Check((float)Field("currentPower").GetValue(controller) == 1200 && (float)Field("steerInput").GetValue(controller) == -1,
                "Xbox full trigger and full left stick remain independent");
        }
        finally
        {
            InputSystem.onAfterUpdate -= readInput;
            input.Disable(); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad);
            InputSystem.settings = originalSettings != null ? originalSettings : restoredSettings;
            if (InputSystem.settings != restoredSettings) UnityEngine.Object.DestroyImmediate(restoredSettings);
            if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        }

        Pose(TumblerController.driveType.AllWheelDrive);
        var steering = typeof(TumblerController).GetMethod("HandleSteering", BindingFlags.Instance | BindingFlags.NonPublic);
        Field("steerInput").SetValue(controller, 1f);
        for (int i = 0; i < 60; i++) steering.Invoke(controller, null);
        Check(fronts.All(w => Mathf.Abs(w.steerAngle - 40) < .1f), "Original 40 degree parking lock retained");
        body.linearVelocity = new Vector3(0, 0, 80 / 3.6f);
        Field("steerInput").SetValue(controller, .5f);
        for (int i = 0; i < 100; i++) steering.Invoke(controller, null);
        float partialAngle = fronts[0].steerAngle;
        Field("steerInput").SetValue(controller, 1f);
        for (int i = 0; i < 100; i++) steering.Invoke(controller, null);
        Check(Mathf.Abs(fronts[0].steerAngle - 2 * partialAngle) < .01f, "Partial steering input retains proportional response at speed");
        float forwardAngle = fronts[0].steerAngle;
        body.linearVelocity = -body.linearVelocity;
        steering.Invoke(controller, null);
        Check(Mathf.Abs(fronts[0].steerAngle - forwardAngle) < .01f, "Reverse retains the same steering range at equal speed");
        body.linearVelocity = new Vector3(0, 100, 80 / 3.6f);
        steering.Invoke(controller, null);
        Check(Mathf.Abs(fronts[0].steerAngle - forwardAngle) < .01f, "Vertical velocity does not restrict steering");
        Field("steerInput").SetValue(controller, 0f);
        for (int i = 0; i < 80; i++) steering.Invoke(controller, null);
        Check(fronts.All(w => Mathf.Abs(w.steerAngle) < .01f), "Released steering recenters");

        body.linearVelocity = Vector3.zero;
        Field("currentBrakeForce").SetValue(controller, 1000f); controller.SendMessage("FixedUpdate");
        Check(wheels.All(w => w.brakeTorque == 1000), "Service brake still acts on all six wheels");
        Field("currentBrakeForce").SetValue(controller, 0f); Field("isHandbrakeActive").SetValue(controller, true); controller.SendMessage("FixedUpdate");
        Check(wheels.Except(fronts).All(w => w.brakeTorque == 4000 && Mathf.Abs(w.sidewaysFriction.extremumSlip - .5f) < .001f),
            "Handbrake retains rear brake torque and drift slip settings");
        Field("isHandbrakeActive").SetValue(controller, false); controller.SendMessage("FixedUpdate");
        Check(wheels.All(w => w.brakeTorque == 0 && Mathf.Abs(w.sidewaysFriction.extremumSlip - .2f) < .001f), "Handbrake release restores the original curves");
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (tick++ < 3) return;
        try
        {
            body = GameObject.Find("Tumbler").GetComponent<Rigidbody>(); controller = body.GetComponent<TumblerController>();
            wheels = body.GetComponentsInChildren<WheelCollider>(); lower = body.transform.Find("Body").GetComponent<MeshCollider>();
            fronts = wheels.Where(w => body.transform.InverseTransformPoint(w.transform.position).z > 0).ToArray();
            foreach (var script in body.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
            GameObject.Find("Driving Test Ground").GetComponent<WheelSurfaceGrip>().enabled = false;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary handling floor";
            floor.transform.position = new Vector3(0, 99, 0); floor.transform.localScale = new Vector3(6000, 2, 6000);
            Physics.simulationMode = SimulationMode.Script;
            Check(Mathf.Abs(Time.deltaTime - .02f) < .0001f, "Fixed 0.02 s captured frame and physics steps");
            Check(Mathf.Abs((float)Field("wheelbase").GetValue(controller) - 4.513f) < .001f, "Steering uses actual 4.513 m wheelbase");
            Check((float)Field("maxSteeringLateralAcceleration").GetValue(controller) == 9, "Sandbox explicitly configures the 9 m/s² steering demand");
            Controls();
            foreach (var drive in new[] { TumblerController.driveType.FrontWheelDrive, TumblerController.driveType.RearWheelDrive, TumblerController.driveType.AllWheelDrive })
            {
                var originalStraight = TrialRun(drive, 0, true); var straight = TrialRun(drive, 0, false);
                Check(Mathf.Abs(originalStraight.heldMeanSpeed - straight.heldMeanSpeed) < .5f, drive + ": straight-line acceleration unchanged");
                var corrected = new List<Trial>();
                foreach (float steer in new[] { 1f, -1f })
                {
                    var original = TrialRun(drive, steer, true); var current = TrialRun(drive, steer, false); corrected.Add(current);
                    float factor = drive == TumblerController.driveType.AllWheelDrive ? 1.35f : 2f;
                    Check(current.heldMeanSpeed > original.heldMeanSpeed * factor, drive + " steer=" + steer + ": sustained full-lock acceleration improves materially");
                    Check(original.samples.Where(s => s.time >= 30 && s.time <= 35).All(s => s.torque > 0), drive + " steer=" + steer + ": original plateau occurs with motor torque still active");
                    if (drive != TumblerController.driveType.AllWheelDrive)
                        Check(original.releasedFinalSpeed > original.heldFinalSpeed + 10,
                            drive + " steer=" + steer + ": straightening the original steering releases the cornering plateau");
                }
                Check(Mathf.Abs(corrected[0].heldMeanSpeed - corrected[1].heldMeanSpeed) < corrected[0].heldMeanSpeed * .1f,
                    drive + ": left and right full-lock response stays within 10 percent");
            }
            SessionState.SetBool(Flag, false); Physics.simulationMode = SimulationMode.FixedUpdate;
            File.WriteAllLines(Path.Combine(Folder, "play.txt"), Results);
            Time.captureDeltaTime = 0; Debug.Log("HANDLING NATIVE VALIDATION PASS " + Results.Count + " checks"); EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void Fail(Exception ex)
    {
        SessionState.SetBool(Flag, false); Physics.simulationMode = SimulationMode.FixedUpdate;
        File.WriteAllLines(Path.Combine(Folder, "failed.txt"), Results.Concat(new[] { ex.ToString() }));
        Debug.LogException(ex); EditorApplication.Exit(1);
    }
}
