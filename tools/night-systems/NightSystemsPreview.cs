using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using TMPro;

// Temporary render-only staging. Never saves the scene or its lighting assets.
public static class NightSystemsPreview
{
    const string Folder="TestResults/NightSystems/Previews";
    static Camera camera;
    static Canvas canvas;
    static Transform vehicle;
    static TumblerSystems systems;
    static DigitalVehicleHUD hud;
    static Volume studio;
    static FieldInfo F(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
    static T Get<T>(object o,string n)=>(T)F(o,n).GetValue(o);
    static void Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,null);
    static void Pose(Vector3 p,Vector3 target){camera.transform.position=p;camera.transform.LookAt(target);}
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            vehicle=GameObject.Find("Tumbler").transform;systems=vehicle.GetComponent<TumblerSystems>();hud=UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>();
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            camera=new GameObject("Temporary detail camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.04f;camera.farClipPlane=1000;camera.fieldOfView=48;
            var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.016f,.021f,.029f,1);
            canvas=hud.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
            foreach(var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))terrain.enabled=false;
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!r.transform.IsChildOf(vehicle)||r is ParticleSystemRenderer)r.enabled=false;
            foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))l.enabled=false;
            vehicle.position=new Vector3(0,1,0);
            studio=new GameObject("Temporary inspection lighting").AddComponent<Volume>();studio.isGlobal=true;studio.priority=999;studio.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exp=studio.sharedProfile.Add<Exposure>(true);exp.mode.Override(ExposureMode.Fixed);exp.fixedExposure.Override(11);
            var env=studio.sharedProfile.Add<VisualEnvironment>(true);env.skyType.Override((int)SkyType.Gradient);
            var sky=studio.sharedProfile.Add<GradientSky>(true);sky.top.Override(new Color(.09f,.12f,.17f));sky.middle.Override(new Color(.25f,.29f,.34f));sky.bottom.Override(new Color(.08f,.10f,.12f));sky.exposure.Override(12);
            var blur=studio.sharedProfile.Add<MotionBlur>(true);blur.intensity.Override(0);
            var bloom=studio.sharedProfile.Add<Bloom>(true);bloom.intensity.Override(.12f);
            var key=Directional("Inspection key",42000,new Vector3(36,-32,0),new Color(1,.94f,.83f));
            var fill=Directional("Inspection fill",18000,new Vector3(12,120,0),new Color(.65f,.79f,1));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Temporary inspection floor";floor.transform.localScale=Vector3.one*25;
            var mat=new Material(Shader.Find("HDRP/Lit"));mat.SetColor("_BaseColor",new Color(.075f,.09f,.105f));mat.SetFloat("_Smoothness",.25f);floor.GetComponent<Renderer>().sharedMaterial=mat;
            systems.SetLightMode(TumblerSystems.LightMode.Off);
            Pose(new Vector3(5,3.2f,7.3f),vehicle.position+new Vector3(0,.05f,.35f));
            hud.Refresh(128,4650,7200,4,false,TumblerController.driveType.AllWheelDrive);hud.RefreshSystems(.73f,false,true,false,TumblerSystems.LightMode.Off,false,false);Capture("vehicle-and-hud",1600,1000);
            hud.Refresh(260,7200,7200,7,false,TumblerController.driveType.RearWheelDrive);hud.RefreshSystems(.24f,true,false,false,TumblerSystems.LightMode.High,true,false);Capture("hud-1080",1920,1080);Capture("hud-720",1280,720);Capture("hud-ultrawide",2560,1080);
            canvas.gameObject.SetActive(false);floor.SetActive(false);vehicle.position=Vector3.zero;
            camera.orthographic=true;camera.orthographicSize=3.6f;Pose(new Vector3(0,-11,0),new Vector3(0,-.6f,0));camera.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
            key.transform.rotation=Quaternion.Euler(-40,25,0);fill.transform.rotation=Quaternion.Euler(-25,-100,0);
            Capture("underbody-front-clearance",1600,1100);
            var wheelVisuals=Get<Transform[]>(vehicle.GetComponent<BrakeDiscThermal>(),"wheelVisuals");
            var rotations=wheelVisuals.Take(2).Select(w=>w.localRotation).ToArray();
            for(int i=0;i<2;i++)wheelVisuals[i].localRotation=Quaternion.Euler(0,40,0)*rotations[i];
            Capture("underbody-steering-lock",1600,1100);for(int i=0;i<2;i++)wheelVisuals[i].localRotation=rotations[i];
            key.transform.rotation=Quaternion.Euler(36,-32,0);fill.transform.rotation=Quaternion.Euler(12,120,0);camera.orthographic=false;
            Pose(new Vector3(2.15f,.45f,4.35f),new Vector3(.35f,-.1f,1.6f));Capture("headlight-optics-off",1600,1000);
            systems.SetLightMode(TumblerSystems.LightMode.High);Capture("headlight-optics-high",1600,1000);systems.SetLightMode(TumblerSystems.LightMode.Off);
            var brakes=vehicle.GetComponent<BrakeDiscThermal>();var rotors=Get<Renderer[]>(brakes,"rotors");
            key.transform.rotation=Quaternion.Euler(-35,110,0);fill.transform.rotation=Quaternion.Euler(-10,-75,0);
            Pose(new Vector3(0,-1.15f,3.2f),new Vector3(.67f,-.35f,2.375f));Capture("front-brake-cold",1400,1000);
            Pose(new Vector3(0,-1f,-3.5f),new Vector3(.61f,-.15f,-2.138f));Capture("rear-brake-inboard-cold",1400,1000);
            var block=new MaterialPropertyBlock();foreach(var r in rotors){block.SetColor("_BaseColor",new Color(.75f,.045f,.008f));block.SetColor("_EmissiveColor",new Color(1,.07f,.002f)*35);r.SetPropertyBlock(block,0);}
            Pose(new Vector3(0,-1.15f,3.2f),new Vector3(.67f,-.35f,2.375f));
            Capture("front-brake-hot",1400,1000);
            Pose(new Vector3(0,-1f,-3.5f),new Vector3(.61f,-.15f,-2.138f));Capture("rear-brakes-hot",1600,1000);
            foreach(var r in rotors)r.SetPropertyBlock(null,0);
            key.transform.rotation=Quaternion.Euler(36,-32,0);fill.transform.rotation=Quaternion.Euler(12,120,0);
            Pose(new Vector3(.46f,.46f,-1.98f),new Vector3(.25f,-.10f,-1.52f));Capture("turbo-tanks-installed",1600,1100);
            var bodyFilter=vehicle.Find("Body").GetComponent<MeshFilter>();var bodyMesh=bodyFilter.sharedMesh;
            var cutaway=UnityEngine.Object.Instantiate(bodyMesh);cutaway.SetTriangles(Array.Empty<int>(),2,false);bodyFilter.sharedMesh=cutaway;
            Pose(new Vector3(1.1f,.65f,-2.65f),new Vector3(0,-.07f,-1.6f));Capture("turbo-tanks-cutaway-inspection",1600,1100);
            bodyFilter.sharedMesh=bodyMesh;UnityEngine.Object.DestroyImmediate(cutaway);
            Pose(new Vector3(.6f,1f,-3.2f),new Vector3(0,.4f,-2.05f));Capture("bronze-fuel-caps",1600,1100);
            // A dusk staging makes the true HDRP beams readable; the authored daytime exposure stays untouched.
            floor.SetActive(true);vehicle.position=new Vector3(0,1,0);key.enabled=false;fill.enabled=false;exp.fixedExposure.Override(8);
            sky.exposure.Override(7);var fog=studio.sharedProfile.Add<Fog>(true);fog.enabled.Override(true);fog.enableVolumetricFog.Override(true);fog.meanFreePath.Override(1000000);fog.depthExtent.Override(100);
            Pose(new Vector3(6,3.2f,12),vehicle.position+new Vector3(0,.1f,8));canvas.gameObject.SetActive(true);
            systems.SetLightMode(TumblerSystems.LightMode.Low);hud.Refresh(38,2500,7200,2,false,TumblerController.driveType.AllWheelDrive);hud.RefreshSystems(.85f,false,false,false,systems.EffectiveLights,false,false);Capture("lights-low",1600,1000);
            systems.SetLightMode(TumblerSystems.LightMode.High);hud.RefreshSystems(.85f,false,false,false,systems.EffectiveLights,false,false);Capture("lights-high",1600,1000);
            // SetStealth requires its runtime caches, so simulate the visible off state for the edit-mode preview only.
            systems.SetLightMode(TumblerSystems.LightMode.Off);hud.RefreshSystems(.85f,false,false,false,TumblerSystems.LightMode.Off,false,true);Capture("stealth-hud",1600,1000);
            Debug.Log("NIGHT SYSTEMS GPU PREVIEWS COMPLETE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Light Directional(string name,float intensity,Vector3 rotation,Color color)
    {var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.intensity=intensity;l.color=color;l.transform.rotation=Quaternion.Euler(rotation);l.gameObject.AddComponent<HDAdditionalLightData>();return l;}
    static void Capture(string name,int width,int height)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        if(canvas.gameObject.activeInHierarchy)canvas.GetComponent<CanvasScaler>().SendMessage("Update",SendMessageOptions.DontRequireReceiver);
        Canvas.ForceUpdateCanvases();foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate();
        foreach(var light in UnityEngine.Object.FindObjectsByType<HDAdditionalLightData>(FindObjectsSortMode.None))
        {light.UpdateAllLightValues();Call(light,"UpdateRenderEntity");}
        for(int i=0;i<12;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var getter=typeof(HDCamera).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="GetOrCreate"&&m.GetParameters().Length==3);
        var rendered=(HDCamera)getter.Invoke(null,new object[]{camera,0,Enum.ToObject(getter.GetParameters()[2].ParameterType,0)});
        bool volumetric=rendered.frameSettings.IsEnabled(FrameSettingsField.Volumetrics);
        Debug.Log("PREVIEW "+name+" Volumetrics="+volumetric+" volume="+rendered.volumeStack.GetComponent<Fog>().enableVolumetricFog.value);
        if(name=="lights-high"&&!volumetric)throw new Exception("HDRP volumetric frame setting is disabled");
        var old=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
