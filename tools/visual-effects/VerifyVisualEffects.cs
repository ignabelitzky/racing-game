using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class VerifyVisualEffects
{
    const string Flag = "VerifyTumblerVisualEffects";
    static string Folder => Environment.GetEnvironmentVariable("VISUAL_EFFECTS_RESULTS") ?? "TestResults/VisualEffects";
    static readonly List<string> Results = new List<string>();
    static int step, beforeCount;
    static double next;
    static Rigidbody body;
    static WheelCollider[] wheels;
    static TumblerEffects effects;
    static TumblerController controller;
    static SurfaceSkidmarks marks;
    static Mesh mesh;
    static Vector3[] airborneVertices;
    static Collider asphalt, slope;
    static Camera camera;

    static VerifyVisualEffects()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Tick;
    }
    static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
        Results.Add("PASS " + message);
    }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (string file in new[] { "edit.txt", "play.txt", "failed.txt" }) File.Delete(Path.Combine(Folder, file));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
            var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            var enter = texts.Single(t => t.text == "ENTER SANDBOX");
            var controls = texts.Single(t => t.text == "CONTROLS");
            var exit = texts.Single(t => t.text == "EXIT");
            Check(enter.color == controls.color && enter.color == exit.color, "All menu labels have equal color");
            var eb = enter.GetComponentInParent<Button>();
            var cb = controls.GetComponentInParent<Button>();
            Check(eb.image.color == cb.image.color && eb.colors.Equals(cb.colors), "Menu button normal, hover, pressed and selected styles match");
            Check(eb.onClick.GetPersistentEventCount() > 0, "Enter Sandbox callback retained");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            Check(UnityEngine.Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Legacy camera-facing trail renderers removed");
            var volume = GameObject.Find("Sandbox Exposure").GetComponent<Volume>();
            Check(volume.isGlobal && volume.priority > 0 && volume.sharedProfile.TryGet(out Exposure exposure), "Sandbox has a local global exposure profile");
            volume.sharedProfile.TryGet(out exposure);
            Check(exposure.mode.overrideState && exposure.mode.value == ExposureMode.Fixed && exposure.fixedExposure.overrideState,
                "Fixed exposure overrides the inherited automatic exposure");
            File.WriteAllLines(Path.Combine(Folder, "edit.txt"), Results);
            Results.Clear();
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void State(PlayModeStateChange state)
    {
        if (SessionState.GetBool(Flag, false) && state == PlayModeStateChange.EnteredPlayMode)
        {
            step = 0;
            next = EditorApplication.timeSinceStartup + 0.6;
        }
    }
    static void Move(Vector3 position, Quaternion rotation)
    {
        body.transform.SetPositionAndRotation(position, rotation);
        body.position = position;
        body.rotation = rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        foreach (var wheel in wheels) { wheel.brakeTorque = 0; wheel.motorTorque = 0; }
        Physics.SyncTransforms();
        next = EditorApplication.timeSinceStartup + 0.7;
    }
    static int MarkedVertices() => mesh.uv2.Count(p => p.y > 0.00001f);
    static void CheckContactGeometry(Collider surface, Func<Vector3, bool> filter)
    {
        var points = mesh.vertices;
        var strength = mesh.uv2;
        int count = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (strength[i].y <= 0.00001f || !filter(points[i])) continue;
            if (!surface.Raycast(new Ray(points[i] + Vector3.up * 0.2f, Vector3.down), out var hit, 0.4f) ||
                Mathf.Abs(Vector3.Dot(points[i] - hit.point, hit.normal) - 0.008f) >= 0.003f)
                throw new Exception("A skidmark vertex left its contacted surface: " + points[i]);
            count++;
        }
        Check(count >= 8, "Every deposited vertex follows the contact surface and its normal (" + count + " vertices checked)");
    }
    static void Capture(string name, Vector3 position, Vector3 target)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        if (camera == null)
        {
            camera = new GameObject("Verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1200;
            camera.fieldOfView = 58;
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
        }
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        // Disable blur only in inspection captures, keeping the gameplay profile unchanged.
        var inspection = GameObject.Find("Sandbox Exposure").GetComponent<Volume>().profile;
        if (!inspection.TryGet(out MotionBlur blur)) blur = inspection.Add<MotionBlur>(false);
        blur.intensity.Override(0);
        camera.transform.position = position;
        camera.transform.LookAt(target);
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        rt.Create(); camera.targetTexture = rt;
        for (int i = 0; i < 5; i++) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
        var old = RenderTexture.active; RenderTexture.active = rt;
        var png = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), png.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(png); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        try
        {
            switch (step++)
            {
                case 0:
                    body = GameObject.Find("Tumbler").GetComponent<Rigidbody>();
                    wheels = body.GetComponentsInChildren<WheelCollider>();
                    body.interpolation = RigidbodyInterpolation.None;
                    controller = body.GetComponent<TumblerController>(); controller.enabled = false;
                    effects = body.GetComponent<TumblerEffects>();
                    marks = UnityEngine.Object.FindFirstObjectByType<SurfaceSkidmarks>();
                    Check(marks != null && wheels.Length == 6, "Contact-mark renderer initialized for the original six wheels");
                    mesh = marks.GetComponent<MeshFilter>().sharedMesh;
                    asphalt = GameObject.Find("Asphalt grip lane").GetComponent<Collider>();
                    slope = GameObject.Find("Cross slope 12 degrees").GetComponent<Collider>();
                    var method = typeof(TumblerEffects).GetMethod("ContactIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
                    Func<float, float, float, float, float> intensity = (side, forward, brake, speed) => (float)method.Invoke(effects, new object[] { side, forward, brake, speed });
                    Check(intensity(0, 0, 0, 20) == 0, "Straight coasting leaves no marks");
                    Check(intensity(1, 1, 4000, 0.1f) == 0, "Stationary/very slow wheels leave no marks");
                    Check(intensity(.5f, 0, 0, 20) > 0 && intensity(-.5f, 0, 0, 20) > 0, "Drift detected in both lateral directions without braking");
                    Check(intensity(0, .5f, 4000, 20) > 0, "Ordinary hard braking can leave marks");
                    Check(intensity(0, -.5f, 0, 20) == 0, "Acceleration slip alone is not treated as braking");
                    Move(new Vector3(-130, 1.2f, 311), Quaternion.identity);
                    break;
                case 1:
                    Check(wheels.All(w => w.GetGroundHit(out var hit) && hit.collider == asphalt), "All wheels contact asphalt before the braking trial");
                    Check(Mathf.Abs(body.position.x + 130) < .1f, "Native trial is located on the intended asphalt lane");
                    Check(MarkedVertices() == 0, "Parked vehicle has not deposited marks");
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    body.linearVelocity = new Vector3(0, 0, 22);
                    foreach (var wheel in wheels) wheel.brakeTorque = 4000;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 2:
                    File.WriteAllLines(Path.Combine(Folder, "braking-diagnostics.txt"), wheels.Select(w => { w.GetGroundHit(out var h);return w.name+" speed="+body.linearVelocity+" slip="+h.forwardSlip+","+h.sidewaysSlip+" ground="+w.isGrounded+" point="+h.point+" forward="+h.forwardDir+" normal="+h.normal+" collider="+h.collider+" brake="+w.brakeTorque; }));
                    Check(MarkedVertices() > 8, "Native braking contacts generated visible mark geometry");
                    Check(!controller.IsHandbraking(), "Braking marks do not require handbrake input");
                    Check(mesh.uv2.All(p => p.y <= .2401f), "Mark opacity remains within the subtle configured limit");
                    CheckContactGeometry(asphalt, p => p.x < -120);
                    Capture("braking-contact-marks", new Vector3(-135, 7, 307), new Vector3(-130, 0, 322));
                    Move(new Vector3(-130, 1.2f, 338), Quaternion.identity);
                    break;
                case 3:
                    beforeCount = MarkedVertices();
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    body.linearVelocity = new Vector3(11, 0, 11);
                    next = EditorApplication.timeSinceStartup + .35;
                    break;
                case 4:
                    Check(MarkedVertices() > beforeCount && wheels.All(w => w.brakeTorque == 0) && !controller.IsHandbraking(),
                        "Native lateral sliding deposits marks with both brake and handbrake released");
                    Capture("subtle-drift-marks", new Vector3(-135, 7, 327), new Vector3(-127, 0, 342));
                    airborneVertices = mesh.vertices;
                    beforeCount = MarkedVertices();
                    Move(new Vector3(-130, 10, 348), Quaternion.identity);
                    body.constraints |= RigidbodyConstraints.FreezePositionY;
                    next = EditorApplication.timeSinceStartup + .4;
                    break;
                case 5:
                    Check(wheels.All(w => !w.isGrounded), "Airborne trial has no wheel-ground contacts");
                    Check(MarkedVertices() == beforeCount && mesh.vertices.SequenceEqual(airborneVertices), "Airborne/reset motion creates no marks and deposited marks remain fixed in world space");
                    Move(new Vector3(-20, 2.8f, 328), Quaternion.Euler(0, 0, 12));
                    break;
                case 6:
                    Check(wheels.Any(w => w.GetGroundHit(out var hit) && hit.collider == slope), "Vehicle contacts the real cambered test mesh");
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    body.linearVelocity = new Vector3(0, 0, 15);
                    foreach (var wheel in wheels) wheel.brakeTorque = 3000;
                    next = EditorApplication.timeSinceStartup + .45;
                    break;
                case 7:
                    CheckContactGeometry(slope, p => p.x > -30 && p.x < -10);
                    Capture("marks-on-camber", new Vector3(-29, 8, 319), new Vector3(-20, 1.5f, 332));
                    var positions = mesh.vertices;
                    Check(Enumerable.Range(0, positions.Length / 4).All(i => Vector3.Distance(positions[i * 4], positions[i * 4 + 2]) <= 3.1f), "No segment bridges a reset or missing contact");
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                    {
                        var shader = marks.GetComponent<MeshRenderer>().sharedMaterial.shader;
                        Check(shader.isSupported && !ShaderUtil.ShaderHasError(shader), "HDRP skidmark shader compiles and is supported");
                    }
                    var profile = GameObject.Find("Sandbox Exposure").GetComponent<Volume>().sharedProfile;
                    profile.TryGet(out Exposure exposure);
                    Check(exposure.mode.value == ExposureMode.Fixed && exposure.fixedExposure.value == 12.5f, "Exposure remains fixed at EV 12.5 after vehicle movement and camera changes");
                    if (camera != null && VolumeManager.instance.isInitialized)
                    {
                        var stack = VolumeManager.instance.CreateStack();
                        foreach (var direction in new[] { Vector3.forward, Vector3.down, Vector3.back })
                        {
                            camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.right);
                            VolumeManager.instance.Update(stack, camera.transform, 1);
                            var resolved = stack.GetComponent<Exposure>();
                            Check(resolved.mode.value == ExposureMode.Fixed && resolved.fixedExposure.value == 12.5f,
                                "Resolved HDRP volume stack uses fixed exposure at a different camera angle");
                        }
                        VolumeManager.instance.DestroyStack(stack);
                    }
                    body.constraints = RigidbodyConstraints.FreezeAll;
                    var renderer = marks.GetComponent<MeshRenderer>();
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                    properties.SetFloat("_Now", Time.time + 30); renderer.SetPropertyBlock(properties);
                    Capture("expired-marks", new Vector3(-29, 8, 319), new Vector3(-20, 1.5f, 332));
                    File.WriteAllLines(Path.Combine(Folder, "play.txt"), Results);
                    SessionState.SetBool(Flag, false);
                    Debug.Log("VISUAL EFFECTS VALIDATION PASS " + Results.Count + " runtime assertions");
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void Fail(Exception ex)
    {
        SessionState.SetBool(Flag, false);
        Directory.CreateDirectory(Folder);
        File.WriteAllLines(Path.Combine(Folder, "failed.txt"), Results.Concat(new[] { ex.ToString() }));
        Debug.LogException(ex); EditorApplication.Exit(1);
    }
}
