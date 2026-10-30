using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ChassisGlideVerify
{
    const string Flag = "VerifyChassisGlide";
    const string PhysicsFolder = "Assets/_Project/Physics/";
    static readonly Vector3 BoxSize = new Vector3(3.4234967f, .73455465f, 5.6127586f);
    static readonly Vector3 BoxCenter = new Vector3(.010071993f, -.36884636f, .14504433f);
    static readonly Vector3 Origin = new Vector3(240, 100, 440);
    static readonly List<string> Results = new List<string>();
    static readonly List<Trial> Trials = new List<Trial>();
    static readonly List<GameObject> Fixtures = new List<GameObject>();
    static Rigidbody body;
    static TumblerController controller;
    static WheelCollider[] wheels;
    static MeshCollider lower;
    static BoxCollider legacy;
    static int step;
    static string Folder => Environment.GetEnvironmentVariable("CHASSIS_RESULTS") ?? "TestResults/Chassis";
    static FieldInfo Power => typeof(TumblerController).GetField("currentPower", BindingFlags.Instance | BindingFlags.NonPublic);

    [Serializable]
    public class Trial
    {
        public string scenario, shape, drive;
        public Vector3 start, end;
        public int rearContacts, frontContacts;
        public float rearContactForce, forwardTravel;
    }
    [Serializable] public class TrialReport { public Trial[] trials; }

    static ChassisGlideVerify()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Tick;
    }
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
            foreach (var name in new[] { "edit.txt", "play.txt", "failed.txt", "trials.json" })
                File.Delete(Path.Combine(Folder, name));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            FindVehicle();
            Check(lower != null && lower.convex && lower.enabled && !lower.isTrigger, "Lower body has one solid convex collision hull");
            Check(lower.transform.GetComponents<BoxCollider>().Length == 1, "Original upper body box remains; lower box removed");
            var mesh = lower.sharedMesh;
            Check(mesh != null && mesh.vertexCount == 16 && mesh.triangles.Length / 3 == 28, "Small closed collision mesh: 16 vertices / 28 triangles");
            Check(Vector3.Distance(mesh.bounds.size, BoxSize) < .0001f && Vector3.Distance(mesh.bounds.center, BoxCenter) < .0001f,
                "Original lower collision envelope and clearance retained");
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var normal = Vector3.Cross(vertices[indices[i + 1]] - a, vertices[indices[i + 2]] - a).normalized;
                Check(vertices.All(v => Vector3.Dot(v - a, normal) < .0001f), "Outward convex face " + i / 3);
            }
            var material = lower.sharedMaterial;
            Check(material != null && Mathf.Abs(material.staticFriction - .04f) < .0001f && Mathf.Abs(material.dynamicFriction - .025f) < .0001f,
                "Dedicated underbody friction: static .04 / dynamic .025");
            Check(material.frictionCombine == PhysicsMaterialCombine.Minimum && material.bounciness == 0, "Minimum friction combine and zero bounce");
            Check(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Count(c => c.sharedMaterial == material) == 1,
                "Glide material is exclusive to the lower collision hull");
            Check(AssetDatabase.GetAssetPath(mesh) == PhysicsFolder + "TumblerLowerChassis.asset", "Hull is separate from all rendered vehicle meshes");
            var com = body.centerOfMass; var inertia = body.inertiaTensor; var inertiaRotation = body.inertiaTensorRotation;
            lower.enabled = false; legacy = AddLegacyBox();
            Physics.SyncTransforms(); body.ResetCenterOfMass(); body.ResetInertiaTensor();
            Check(Vector3.Distance(body.centerOfMass, com) < .0001f, "Original box-derived center of mass retained");
            Check(Vector3.Distance(body.inertiaTensor, inertia) < .01f && Quaternion.Angle(body.inertiaTensorRotation, inertiaRotation) < .01f,
                "Original box-derived inertia retained");
            UnityEngine.Object.DestroyImmediate(legacy); lower.enabled = true;
            body.centerOfMass = com; body.inertiaTensor = inertia; body.inertiaTensorRotation = inertiaRotation;
            Check(wheels.Length == 6, "All six original WheelColliders present");
            File.WriteAllLines(Path.Combine(Folder, "edit.txt"), Results); Results.Clear();
            SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void FindVehicle()
    {
        body = GameObject.Find("Tumbler").GetComponent<Rigidbody>();
        controller = body.GetComponent<TumblerController>();
        lower = body.transform.Find("Body").GetComponent<MeshCollider>();
        wheels = body.GetComponentsInChildren<WheelCollider>();
    }
    static BoxCollider AddLegacyBox()
    {
        var box = lower.gameObject.AddComponent<BoxCollider>();
        box.size = BoxSize; box.center = BoxCenter;
        box.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PhysicsMaterials/Friction/LowFriction.physicMaterial");
        return box;
    }
    static void State(PlayModeStateChange state)
    {
        if (SessionState.GetBool(Flag, false) && state == PlayModeStateChange.EnteredPlayMode) step = 0;
    }
    static void Shape(bool old, bool glide = false)
    {
        lower.enabled = !old; legacy.enabled = old;
        legacy.sharedMaterial = glide ? lower.sharedMaterial : AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PhysicsMaterials/Friction/LowFriction.physicMaterial");
    }
    static void Box(string name, Vector3 center, Vector3 size, Quaternion rotation)
    {
        var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fixture.name = "Temporary chassis fixture: " + name;
        fixture.transform.SetPositionAndRotation(Origin + center, rotation);
        fixture.transform.localScale = size;
        Fixtures.Add(fixture);
    }
    static void ClearFixtures()
    {
        foreach (var fixture in Fixtures) UnityEngine.Object.DestroyImmediate(fixture);
        Fixtures.Clear();
    }
    static void Pose(Vector3 position, Quaternion rotation, RigidbodyConstraints constraints)
    {
        body.interpolation = RigidbodyInterpolation.None; body.constraints = constraints;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        body.transform.SetPositionAndRotation(Origin + position, rotation);
        body.position = body.transform.position; body.rotation = rotation;
        foreach (var wheel in wheels) { wheel.motorTorque = 0; wheel.brakeTorque = 0; wheel.rotationSpeed = 0; }
        Power.SetValue(controller, 0f);
        Physics.SyncTransforms(); body.WakeUp();
    }
    static void Simulate(int count, float power = 0, float force = 0)
    {
        Power.SetValue(controller, power);
        for (int i = 0; i < count; i++)
        {
            if (power != 0) controller.SendMessage("FixedUpdate");
            if (force != 0) body.AddForce(Vector3.forward * force);
            Physics.Simulate(.02f);
        }
    }
    static Trial Record(string scenario, string shape, string drive)
    {
        var trial = new Trial { scenario = scenario, shape = shape, drive = drive, start = body.position - Origin };
        foreach (var wheel in wheels)
        {
            if (!wheel.GetGroundHit(out var hit)) continue;
            bool rear = body.transform.InverseTransformPoint(wheel.transform.position).z < 0;
            if (rear) { trial.rearContacts++; trial.rearContactForce += hit.force; }
            else trial.frontContacts++;
        }
        return trial;
    }
    static Trial Finish(Trial trial)
    {
        trial.end = body.position - Origin; trial.forwardTravel = trial.end.z - trial.start.z;
        Trials.Add(trial);
        File.WriteAllText(Path.Combine(Folder, "trials.json"), JsonUtility.ToJson(new TrialReport { trials = Trials.ToArray() }, true));
        return trial;
    }
    static Trial Incline(bool old)
    {
        ClearFixtures(); Shape(old);
        var rotation = Quaternion.Euler(7, 0, 0);
        Box("narrow 7 degree rail", rotation * new Vector3(0, -.5f, 0), new Vector3(.65f, 1, 40), rotation);
        Pose(rotation * new Vector3(0, .74f, -5), rotation, RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation);
        Simulate(70);
        var trial = Record("gravity slide on 7 degree center rail", old ? "original box and original friction" : "beveled hull and glide friction", "none");
        Check(trial.frontContacts + trial.rearContacts == 0, trial.shape + ": only the underbody rests on the rail");
        Simulate(150); return Finish(trial);
    }
    static Trial Curb(bool old)
    {
        ClearFixtures(); Shape(old, true);
        Box("center rail", new Vector3(0, -.5f, 0), new Vector3(.65f, 1, 40), Quaternion.identity);
        Box("6 cm step", new Vector3(0, .03f, 13.4f), new Vector3(.65f, .06f, 20), Quaternion.identity);
        Pose(new Vector3(0, .74f, 0), Quaternion.identity, RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation);
        Simulate(70);
        var trial = Record("6 cm curb with equal low friction and 9000 N push", old ? "original box" : "beveled hull", "none");
        Check(trial.frontContacts + trial.rearContacts == 0, trial.shape + ": curb comparison excludes wheel traction");
        Simulate(150, force: 9000); return Finish(trial);
    }
    static Trial Ledge(bool old, TumblerController.driveType drive, bool reverse = false)
    {
        ClearFixtures(); Shape(old);
        Box("upper platform", new Vector3(0, -1, -60), new Vector3(20, 2, 120), Quaternion.identity);
        Box("lower floor", new Vector3(0, -2.5f, 60), new Vector3(20, 2, 120), Quaternion.identity);
        Pose(new Vector3(0, 1.1f, .9f), Quaternion.Euler(12, 0, 0), RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ);
        typeof(TumblerController).GetField("<currentDriveMode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, drive);
        Simulate(100);
        var trial = Record("1.5 m platform edge", old ? "original box and original friction" : "beveled hull and glide friction", drive + (reverse ? " reverse" : " forward"));
        Simulate(250, power: reverse ? -1000 : 1200); return Finish(trial);
    }
    static void FastDrop(bool old)
    {
        ClearFixtures(); Shape(old);
        var upper = lower.GetComponents<BoxCollider>().Single(c => c != legacy);
        upper.enabled = false; // Isolate the lower hull, which must catch this fall itself.
        Box("thin impact platform", new Vector3(0, -.05f, 0), new Vector3(20, .1f, 20), Quaternion.identity);
        Pose(new Vector3(0, 1.2f, 0), Quaternion.identity, RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation);
        foreach (var wheel in wheels) wheel.enabled = false;
        body.linearVelocity = Vector3.down * 70;
        Simulate(20);
        Check(body.position.y > Origin.y + .6f, (old ? "Original box" : "New hull") + ": existing continuous detection catches a 70 m/s fall onto a 10 cm platform");
        upper.enabled = true;
        foreach (var wheel in wheels) wheel.enabled = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (step++ == 0) return; // Allow the scene's normal Start/Update initialization.
            FindVehicle();
            foreach (var script in body.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
            GameObject.Find("Driving Test Ground").GetComponent<WheelSurfaceGrip>().enabled = false;
            legacy = AddLegacyBox(); legacy.enabled = false;
            Physics.simulationMode = SimulationMode.Script;
            var oldSlide = Incline(true); var newSlide = Incline(false);
            Check(Mathf.Abs(oldSlide.forwardTravel) < .15f, "Original underbody friction holds on a 7 degree rail");
            Check(newSlide.forwardTravel > 1, "New underbody slides more than 1 m under gravity on the same rail");
            var oldCurb = Curb(true); var newCurb = Curb(false);
            Check(oldCurb.forwardTravel < 1.2f, "Original square lower edge stops at the 6 cm curb despite low friction");
            Check(newCurb.forwardTravel > 2.5f && newCurb.end.y > oldCurb.end.y + .03f, "Beveled lower edge climbs the same curb with equal friction and force");
            foreach (var drive in new[] { TumblerController.driveType.AllWheelDrive, TumblerController.driveType.RearWheelDrive, TumblerController.driveType.FrontWheelDrive })
            {
                var oldLedge = Ledge(true, drive); var newLedge = Ledge(false, drive);
                Check(oldLedge.rearContacts == 4 && oldLedge.frontContacts == 0, drive + ": original vehicle is hung with all four rear wheels touching");
                Check(Mathf.Abs(oldLedge.forwardTravel) < .1f, drive + ": original configuration remains stuck under throttle");
                Check(newLedge.rearContacts == 4 && newLedge.rearContactForce > oldLedge.rearContactForce,
                    drive + ": new hull restores useful load on the four rear wheels");
                Check(newLedge.end.z > 3.5f && newLedge.end.y > -1, drive + ": actual vehicle leaves the edge and stays above the lower floor");
            }
            var oldReverse = Ledge(true, TumblerController.driveType.RearWheelDrive, true);
            var newReverse = Ledge(false, TumblerController.driveType.RearWheelDrive, true);
            Check(Mathf.Abs(oldReverse.forwardTravel) < .1f, "Original configuration also stays stuck in reverse");
            Check(newReverse.forwardTravel < -1 && newReverse.end.y > .5f, "New hull allows more than 1 m of reverse movement toward the upper platform");
            FastDrop(true); FastDrop(false);
            Physics.simulationMode = SimulationMode.FixedUpdate;
            File.WriteAllLines(Path.Combine(Folder, "play.txt"), Results);
            SessionState.SetBool(Flag, false); Debug.Log("CHASSIS NATIVE VALIDATION PASS " + Results.Count + " runtime checks");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void Fail(Exception ex)
    {
        SessionState.SetBool(Flag, false);
        Physics.simulationMode = SimulationMode.FixedUpdate;
        File.WriteAllLines(Path.Combine(Folder, "failed.txt"), Results.Concat(new[] { ex.ToString() }));
        Debug.LogException(ex); EditorApplication.Exit(1);
    }
}
