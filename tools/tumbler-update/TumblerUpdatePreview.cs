using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using TMPro;

public static class TumblerUpdatePreview
{
    static string Folder => Environment.GetEnvironmentVariable("TUMBLER_PREVIEWS") ?? "TestResults/TumblerUpdate/Previews";
    static Camera camera;
    static Canvas canvas;
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            var vehicle = GameObject.Find("Tumbler");
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            camera = new GameObject("Preview camera only").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.05f;camera.farClipPlane=1500;camera.fieldOfView=58;
            var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
            var inspection=GameObject.Find("Sandbox Exposure").GetComponent<Volume>().profile;
            if(!inspection.TryGet(out MotionBlur blur))blur=inspection.Add<MotionBlur>(false);blur.intensity.Override(0);
            canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.2f;
            camera.transform.position=vehicle.transform.position+new Vector3(-4,2.6f,-7.6f);camera.transform.LookAt(vehicle.transform.position+new Vector3(0,.4f,.3f));
            var hud=UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>();
            hud.Refresh(0,900,7200,1,false,TumblerController.driveType.AllWheelDrive);Capture("hud-idle-1080",1920,1080);
            hud.Refresh(128,4650,7200,4,false,TumblerController.driveType.RearWheelDrive);Capture("hud-shift-1080",1920,1080);
            hud.Refresh(260,7200,7200,7,false,TumblerController.driveType.FrontWheelDrive);Capture("hud-redline-1080",1920,1080);
            hud.Refresh(128,4650,7200,4,false,TumblerController.driveType.AllWheelDrive);Capture("hud-720",1280,720);
            canvas.gameObject.SetActive(false);
            foreach(var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))terrain.enabled=false;
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(!renderer.transform.IsChildOf(vehicle.transform)||renderer is ParticleSystemRenderer)renderer.enabled=false;
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.enabled=false;
            vehicle.transform.position=Vector3.zero;
            var studio=new GameObject("Preview studio only").AddComponent<Volume>();studio.isGlobal=true;studio.priority=999;studio.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=studio.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(11.5f);
            var environment=studio.sharedProfile.Add<VisualEnvironment>(true);environment.skyType.Override((int)SkyType.Gradient);
            var sky=studio.sharedProfile.Add<GradientSky>(true);sky.top.Override(new Color(.12f,.17f,.24f));sky.middle.Override(new Color(.25f,.29f,.34f));sky.bottom.Override(new Color(.10f,.12f,.16f));sky.exposure.Override(12);
            Light("Underbody key",40000,Quaternion.Euler(-45,25,0),new Color(1,.95f,.87f));
            Light("Underbody fill",18000,Quaternion.Euler(-28,-105,0),new Color(.70f,.82f,1));
            hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.018f,.025f,.035f,1);
            var systems=GameObject.Find("Underbody Outer Panels and Front Subframe (Visual Only)");
            camera.orthographic=true;camera.orthographicSize=3.65f;camera.transform.position=new Vector3(0,-10,0);camera.transform.LookAt(new Vector3(0,-.5f,0),Vector3.forward);
            systems.SetActive(false);Capture("underbody-plan-before",1600,1100);
            systems.SetActive(true);Capture("underbody-plan-after",1600,1100);
            camera.orthographic=false;camera.fieldOfView=52;camera.transform.position=new Vector3(-4,-3.8f,-5.1f);camera.transform.LookAt(new Vector3(0,-.55f,0));Capture("underbody-rear-after",1600,1000);
            camera.transform.position=new Vector3(2.8f,-3.2f,4.8f);camera.transform.LookAt(new Vector3(0,-.6f,1.0f));Capture("underbody-front-after",1600,1000);
            Debug.Log("TUMBLER UPDATE GPU PREVIEWS COMPLETE");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Light(string name,float intensity,Quaternion rotation,Color color)
    {
        var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;
        light.transform.rotation=rotation;light.gameObject.AddComponent<HDAdditionalLightData>();
    }
    static void Capture(string name,int width,int height)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        if(canvas.gameObject.activeInHierarchy)canvas.GetComponent<CanvasScaler>().SendMessage("Update",SendMessageOptions.DontRequireReceiver);
        Canvas.ForceUpdateCanvases();foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate();
        for(int i=0;i<8;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
