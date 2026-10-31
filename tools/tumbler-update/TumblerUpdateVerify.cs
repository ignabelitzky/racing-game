using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class TumblerUpdateVerify
{
    const string Flag = "TumblerUpdateVerify";
    static string Folder => Environment.GetEnvironmentVariable("TUMBLER_UPDATE_RESULTS") ?? "TestResults/TumblerUpdate";
    static readonly List<string> Results = new List<string>();
    static readonly List<RunData> Runs = new List<RunData>();
    static readonly List<TurnData> Turns = new List<TurnData>();
    static readonly List<CornerData> Corners = new List<CornerData>();
    static Rigidbody body;
    static TumblerController controller;
    static WheelCollider[] wheels, fronts;
    static EngineAudio audio;
    static AudioSource running;
    static DigitalVehicleHUD hud;
    static int tick;
    static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    static FieldInfo Field(string name) => Field(typeof(TumblerController), name);
    static void Set(string name, object value) => Field(typeof(TumblerController), name).SetValue(controller, value);
    static object Get(string name) => Field(typeof(TumblerController), name).GetValue(controller);
    static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    static void Check(bool valid, string label) { if (!valid) throw new Exception(label); Results.Add("PASS " + label); }
    [Serializable] public class Shift { public float time, speed, rpmBefore, rpmAfter, pitchBefore, pitchAfter; public int from, to; }
    [Serializable] public class RunData { public string mode; public float topSpeed, finalSpeed, finalRPM; public int finalGear; public Shift[] shifts; }
    [Serializable] public class TurnData { public float speed, allowance, headingChange, finalSpeed, frontAngle, sidewaysSlip; }
    [Serializable] public class CornerData { public string mode; public float input, meanFinalSpeed, finalSpeed, minimumUpright; }
    [Serializable] public class Report { public RunData[] runs; public TurnData[] turns; public CornerData[] corners; }
    static TumblerUpdateVerify() { EditorApplication.playModeStateChanged += State; EditorApplication.update += Tick; }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var name in new[] { "edit.txt", "play.txt", "failed.txt", "performance.json" }) File.Delete(Path.Combine(Folder, name));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            Find();
            Check(GameObject.Find("Speedometer") == null && GameObject.Find("SpeedometerNeedle") == null, "Legacy analog gauge and needle removed");
            Check(hud != null && UnityEngine.Object.FindObjectsByType<DigitalVehicleHUD>(FindObjectsSortMode.None).Length == 1, "One digital HUD instance");
            var rect = hud.GetComponent<RectTransform>();
            Check(rect.anchorMin == new Vector2(1,0) && rect.anchorMax == new Vector2(1,0) && rect.sizeDelta == new Vector2(300,240), "Compact 300 x 240 HUD anchored bottom-right, including auxiliary systems");
            var canvas = hud.GetComponentInParent<Canvas>(); var scaler = canvas.GetComponent<CanvasScaler>();
            Check(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1920,1080), "HUD scales with screen resolution");
            Check(hud.GetComponentsInChildren<Image>().All(i => !i.raycastTarget), "HUD does not consume pointer input");
            Check(controller.GetMaxSpeed() == 260 && (int)Get("numberOfGears") == 7 && ((float[])Get("gearRatios")).Length == 7, "260 km/h configuration and seven valid gear ratios");
            Check(controller.GetIdleRPM() == 900 && controller.GetRedlineRPM() == 7200, "Consistent 900–7200 engine RPM range");
            foreach (float rpm in new[] { 900f, 3600f, 5600f, 7200f })
            {
                hud.Refresh(260, rpm, 7200, 7, false, TumblerController.driveType.AllWheelDrive);
                foreach (var text in hud.GetComponentsInChildren<TextMeshProUGUI>())
                { text.ForceMeshUpdate(); Check(text.preferredWidth <= text.rectTransform.rect.width + 1, "HUD text fits at " + rpm + " RPM: " + text.name); }
            }
            var green = hud.RPMColor(900 / 7200f); var red = hud.RPMColor(1);
            Check(green.g > green.r && red.r > red.g * 2, "RPM colors progress from green at idle to red at the limit");
            var systems = GameObject.Find("Underbody Outer Panels and Front Subframe (Visual Only)");
            Check(systems != null && systems.GetComponentsInChildren<Renderer>().Length == 3, "Three new underbody systems groups");
            Check(systems.GetComponentsInChildren<Collider>().Length == 0 && systems.GetComponentsInChildren<Rigidbody>().Length == 0, "Underbody expansion is visual only");
            var original = body.GetComponentsInChildren<MeshFilter>().Where(m => AssetDatabase.GetAssetPath(m.sharedMesh) == "Assets/Prefabs/tumbler_v7.fbx" || m.transform.name == "Body").ToArray();
            Check(original.Length == 7, "Six original wheels and the body with only the old tank shells removed retained");
            var bounds = original[0].GetComponent<Renderer>().bounds;
            foreach (var mesh in original.Skip(1)) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            bounds.Expand(.02f);
            foreach (var renderer in systems.GetComponentsInChildren<Renderer>())
                Check(bounds.Contains(renderer.bounds.min) && bounds.Contains(renderer.bounds.max), "New detail remains in original render envelope: " + renderer.name);
            File.WriteAllLines(Path.Combine(Folder, "edit.txt"), Results); Results.Clear();
            SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void Find()
    {
        controller = GameObject.Find("Tumbler").GetComponent<TumblerController>(); body = controller.GetComponent<Rigidbody>();
        wheels = body.GetComponentsInChildren<WheelCollider>(); fronts = wheels.Where(w => body.transform.InverseTransformPoint(w.transform.position).z > 0).ToArray();
        audio = body.GetComponent<EngineAudio>(); running = (AudioSource)Field(typeof(EngineAudio), "runningSound").GetValue(audio);
        hud = UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>();
    }
    static void State(PlayModeStateChange state)
    { if (SessionState.GetBool(Flag, false) && state == PlayModeStateChange.EnteredPlayMode) { tick = 0; Time.captureDeltaTime = .02f; } }
    static void Pose(TumblerController.driveType drive)
    {
        body.constraints = RigidbodyConstraints.None; body.interpolation = RigidbodyInterpolation.None;
        body.transform.SetPositionAndRotation(new Vector3(0,101.2f,0),Quaternion.identity);
        body.position = body.transform.position; body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        Set("currentPower",0f); Set("currentBrakeForce",0f); Set("isHandbrakeActive",false); Set("steerInput",0f); Set("currentTurnAngle",0f);
        Set("<currentDriveMode>k__BackingField",drive); Set("<currentGear>k__BackingField",1); Set("engineRPM",900f); Set("shiftCooldown",0f); Set("shiftTimeRemaining",0f);
        foreach(var wheel in wheels) { wheel.motorTorque=0;wheel.brakeTorque=0;wheel.rotationSpeed=0;wheel.steerAngle=0; }
        Physics.SyncTransforms();body.WakeUp();for(int i=0;i<100;i++)Physics.Simulate(.02f);
    }
    static void Step(float power, float steer)
    {
        Set("currentPower",power);Set("steerInput",steer);controller.SendMessage("FixedUpdate");
        Method(typeof(EngineAudio),"UpdateEngineSound").Invoke(audio,null);
        Physics.Simulate(.02f);
    }
    static void Write() => File.WriteAllText(Path.Combine(Folder,"performance.json"),JsonUtility.ToJson(new Report {runs=Runs.ToArray(),turns=Turns.ToArray(),corners=Corners.ToArray()},true));
    static void SteadyCorner(TumblerController.driveType mode,float input)
    {
        Pose(mode);Set("highSpeedSteeringAllowance",4f);
        float sum=0,upright=1;int samples=0;
        for(int i=0;i<1750;i++)
        {
            Step(1400,input);upright=Mathf.Min(upright,body.transform.up.y);
            if(i>=1500){sum+=controller.GetSpeed();samples++;}
        }
        var corner=new CornerData{mode=mode.ToString(),input=input,meanFinalSpeed=sum/samples,finalSpeed=controller.GetSpeed(),minimumUpright=upright};
        Corners.Add(corner);Write();
        Check(corner.meanFinalSpeed>60,mode+" input="+input+": sustained full steering avoids the former low-speed plateau");
        Check(corner.minimumUpright>.9f,mode+" input="+input+": sustained full steering remains upright");
    }
    static void Straight(TumblerController.driveType mode)
    {
        Pose(mode);Set("highSpeedSteeringAllowance",4f);
        var shifts=new List<Shift>();float peak=0;
        for(int i=0;i<6000;i++)
        {
            int gear=controller.currentGear;float rpm=controller.GetEngineRPM(),pitch=running.pitch;
            Step(1400,0);peak=Mathf.Max(peak,controller.GetSpeed());
            if(controller.currentGear!=gear)shifts.Add(new Shift{time=i*.02f,speed=controller.GetSpeed(),from=gear,to=controller.currentGear,rpmBefore=rpm,rpmAfter=controller.GetEngineRPM(),pitchBefore=pitch,pitchAfter=running.pitch});
        }
        var run=new RunData{mode=mode.ToString(),topSpeed=peak,finalSpeed=controller.GetSpeed(),finalGear=controller.currentGear,finalRPM=controller.GetEngineRPM(),shifts=shifts.ToArray()};Runs.Add(run);Write();
        Check(run.finalSpeed>258 && run.topSpeed<261,mode+": reaches the 260 km/h governor without boost");
        Check(run.finalGear==7 && Enumerable.Range(2,6).All(g=>shifts.Any(s=>s.to==g)),mode+": all seven gears reached sequentially");
        Check(shifts.Where(s=>s.to>s.from).All(s=>s.rpmAfter<s.rpmBefore && s.pitchAfter<s.pitchBefore),mode+": upshifts lower both RPM and engine pitch");
        Check(shifts.Zip(shifts.Skip(1),(a,b)=>b.time-a.time).All(t=>t>=.44f),mode+": shift cooldown prevents gear hunting");
        Check(wheels.All(w=>w.isGrounded) && body.transform.up.y>.98f,mode+": all wheels remain grounded at top speed");
        Check(controller.GetEngineRPM()>=900 && controller.GetEngineRPM()<=7200,mode+": RPM stays within the instrument range");
    }
    static TurnData Turn(float speed,float allowance)
    {
        Pose(TumblerController.driveType.AllWheelDrive);Set("highSpeedSteeringAllowance",allowance);
        body.linearVelocity=Vector3.forward*speed/3.6f;
        float maxSlip=0;
        for(int i=0;i<100;i++)
        {
            Step(1400,1);
            foreach(var w in fronts)if(w.GetGroundHit(out var h))maxSlip=Mathf.Max(maxSlip,Mathf.Abs(h.sidewaysSlip));
        }
        var result=new TurnData{speed=speed,allowance=allowance,headingChange=Mathf.Abs(Mathf.DeltaAngle(0,body.rotation.eulerAngles.y)),finalSpeed=controller.GetSpeed(),frontAngle=fronts[0].steerAngle,sidewaysSlip=maxSlip};
        Turns.Add(result);Write();return result;
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
                Check((float)Field("currentPower").GetValue(controller) == (float)Get("forwardPower") &&
                    Mathf.Abs((float)Field("steerInput").GetValue(controller)) == 1f, "Keyboard throttle and full steering remain independent: " + key +
                    " (power=" + Field("currentPower").GetValue(controller) + ", steer=" + Field("steerInput").GetValue(controller) + ", key=" + keyboard.wKey.isPressed + ")");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = .6f, leftStick = new Vector2(.5f, 0) });
            InputSystem.Update();
            float steer = (float)Field("steerInput").GetValue(controller);
            Check(Mathf.Abs((float)Field("currentPower").GetValue(controller) - (float)Get("forwardPower") * .6f) < .01f && steer > 0 && steer < 1,
                "Xbox throttle and stick remain analog while used together");
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1, leftStick = Vector2.left });
            InputSystem.Update();
            Check((float)Field("currentPower").GetValue(controller) == (float)Get("forwardPower") && (float)Field("steerInput").GetValue(controller) == -1,
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
    static void Instruments()
    {
        foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})
        {
            hud.Refresh(260,7200,7200,7,false,mode);
            Check(hud.transform.Find("SpeedValue").GetComponent<TextMeshProUGUI>().text=="260" && hud.transform.Find("GearNumText").GetComponent<TextMeshProUGUI>().text=="7",mode+": speed and seventh gear displayed");
            string text=hud.transform.Find("DriveModeText").GetComponent<TextMeshProUGUI>().text;
            Check(text==(mode==TumblerController.driveType.AllWheelDrive?"AWD":mode==TumblerController.driveType.FrontWheelDrive?"FWD":"RWD"),mode+": current drivetrain displayed");
        }
        hud.Refresh(12,1900,7200,1,true,TumblerController.driveType.RearWheelDrive);
        Check(hud.transform.Find("GearNumText").GetComponent<TextMeshProUGUI>().text=="R","Reverse displayed independently from the forward gear");
        Set("engineRPM",4200f);Method(typeof(EngineAudio),"UpdateEngineSound").Invoke(audio,null);
        float expected=Mathf.Lerp(.65f,1.8f,controller.GetNormalizedEngineRPM());
        Check(Mathf.Abs(running.pitch-expected)<.001f,"Engine sound uses the same normalized RPM as the HUD");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(tick++<3)return;
        try
        {
            Find();foreach(var script in body.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
            GameObject.Find("Driving Test Ground").GetComponent<WheelSurfaceGrip>().enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary performance floor";floor.transform.position=new Vector3(0,99,0);floor.transform.localScale=new Vector3(30000,2,30000);
            Physics.simulationMode=SimulationMode.Script;
            Instruments(); Controls();
            foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})Straight(mode);
            Set("currentBrakeForce",1000f);
            for(int i=0;i<900;i++)Step(0,0);
            Check(controller.currentGear==1 && controller.GetSpeed()<1 && controller.GetEngineRPM()<1100,"Automatic gearbox returns to first and idle after braking to a stop");
            Set("currentBrakeForce",0f);
            foreach(float speed in new[]{60f,120f,180f,240f})
            {
                var previous=Turn(speed,0);var balanced=Turn(speed,4);
                Check(balanced.headingChange>previous.headingChange*1.1f,speed+" km/h: full steering produces a stronger directional response");
                Check(body.transform.up.y>.9f,speed+" km/h: vehicle remains upright during the steering check");
            }
            foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})
                foreach(float input in new[]{-1f,1f})SteadyCorner(mode,input);
            Set("highSpeedSteeringAllowance",4f);
            Pose(TumblerController.driveType.AllWheelDrive);
            body.linearVelocity=Vector3.forward*260/3.6f;
            var boost=body.GetComponent<Boost>();Field(typeof(Boost),"isBoostActive").SetValue(boost,true);
            float boostedPeak=0;
            for(int i=0;i<150;i++){Step(1400,0);boostedPeak=Mathf.Max(boostedPeak,controller.GetSpeed());}
            Field(typeof(Boost),"isBoostActive").SetValue(boost,false);
            Check(boostedPeak<261,"Boost respects the forward speed governor");
            body.linearVelocity=new Vector3(0,10,400/3.6f);Step(1400,0);
            Check(controller.GetSpeed()<261 && body.linearVelocity.y>9,"Speed governor and speedometer exclude falling/jumping velocity without cancelling vertical motion");
            body.linearVelocity=Vector3.zero;controller.ResetTransmission();
            Check(controller.currentGear==1 && controller.GetEngineRPM()==900 && wheels.All(w=>Mathf.Abs(w.rotationSpeed)<.001f),"Vehicle reset returns the gearbox, RPM and wheel spin to their initial state");
            File.WriteAllLines(Path.Combine(Folder,"play.txt"),Results);SessionState.SetBool(Flag,false);Physics.simulationMode=SimulationMode.FixedUpdate;Time.captureDeltaTime=0;
            Debug.Log("TUMBLER UPDATE NATIVE VALIDATION PASS "+Results.Count+" runtime checks");EditorApplication.Exit(0);
        }
        catch(Exception ex){Fail(ex);}
    }
    static void Fail(Exception ex)
    {SessionState.SetBool(Flag,false);Physics.simulationMode=SimulationMode.FixedUpdate;File.WriteAllLines(Path.Combine(Folder,"failed.txt"),Results.Concat(new[]{ex.ToString()}));Debug.LogException(ex);EditorApplication.Exit(1);}
}
