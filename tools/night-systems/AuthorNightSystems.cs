using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using TMPro;

public static class AuthorNightSystems
{
    const string Art="Assets/_Project/Art/VehicleSystems/";
    const string Prefab="Assets/_Project/Prefabs/TumblerLightingAndEquipment.prefab";
    static Transform vehicle;
    static Material steel, dark, bronze, optical, rubber, tank;
    static TMP_FontAsset font;
    static Mesh OriginalBody => AssetDatabase.LoadAllAssetsAtPath("Assets/Prefabs/tumbler_v7.fbx").OfType<Mesh>().OrderByDescending(m=>m.vertexCount).First();
    static NightInspect.Part[] OldTanks(IEnumerable<NightInspect.Part> parts) => parts.Where(p=>p.material==5&&p.triangles>=890&&p.triangles<=910&&Mathf.Abs(p.center.x)<.4f&&p.center.z>-1.6f&&p.center.z<-1.3f&&p.size.y>.65f).OrderBy(p=>p.center.x).ToArray();
    static void RemoveOriginalTanks(MeshFilter filter,Mesh source,IEnumerable<NightInspect.Part> parts)
    {
        var old=parts.Where(p=>p.material==5&&Mathf.Abs(p.center.x)<.7f&&p.center.z>-1.72f&&p.center.z<-1.20f).ToArray();
        if(old.Length!=8)throw new Exception("Expected two original tank assemblies, including valves and collars");
        var remove=new HashSet<int>(old.SelectMany(p=>p.indices));var triangles=source.GetTriangles(5);var removed=new List<int>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            if(remove.Contains(triangles[i])&&remove.Contains(triangles[i+1])&&remove.Contains(triangles[i+2]))removed.Add(i/3);
        }
        if(removed.Count!=old.Sum(p=>p.triangles)||removed.Count!=triangles.Length/3)throw new Exception("Tank material contains unexpected geometry");
        string model=AssetDatabase.GetAssetPath(source),path=Art+"Meshes/Tumbler_Body_Equipment_Replaced.tumblerbody";
        var definition=new TumblerBodyImporter.Definition{sourceModelGuid=AssetDatabase.AssetPathToGUID(model),sourceMeshName=source.name,sourceFileSha256=TumblerBodyImporter.HashFile(model),materialSlot=5,sourceTriangleCount=triangles.Length/3};
        File.WriteAllText(path,JsonUtility.ToJson(definition,true));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(filter.sharedMesh==null)throw new Exception("Replacement body mesh did not import");
        Debug.Log("Removed exactly "+removed.Count+" original tank-assembly triangles; every other body buffer/triangle retained");
    }
    static T Store<T>(T value,string path) where T:UnityEngine.Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(value);return old;
    }
    static GameObject Child(string name,Transform parent)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Renderer Model(string name,Transform parent,Mesh mesh,params Material[] materials)
    {
        var go=Child(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=materials;return r;
    }
    static Material Surface(string name,Color color,float metal,float smooth,string texture,bool triplanar=true)
    {
        var m=new Material(Shader.Find("HDRP/Lit")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        m.SetFloat("_SmoothnessRemapMin",smooth*.7f);m.SetFloat("_SmoothnessRemapMax",smooth);
        m.SetFloat("_MetallicRemapMin",metal*.8f);m.SetFloat("_MetallicRemapMax",metal);
        m.SetFloat("_DoubleSidedEnable",1);m.SetFloat("_UVBase",triplanar?5:0);m.SetFloat("_ObjectSpaceUVMapping",1);
        m.SetFloat("_TexWorldScale",1.5f);
        foreach(string suffix in new[]{"Base","Normal","Mask"})
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+texture+"_"+suffix+".png");
            if(tex!=null)m.SetTexture(suffix=="Base"?"_BaseColorMap":suffix=="Normal"?"_NormalMap":"_MaskMap",tex);
        }
        m.SetFloat("_NormalScale",texture == "OpticalLens" ? .22f : .45f);
        m.SetColor("_EmissiveColor",Color.black);m.SetFloat("_EmissiveExposureWeight",1);
        HDShaderUtils.ResetMaterialKeywords(m);return Store(m,Art+"Materials/"+name+".mat");
    }
    static void TextureImport()
    {
        AssetDatabase.Refresh();
        foreach(string f in Directory.GetFiles(Art+"Textures","*.png"))
        {
            var p=f.Replace('\\','/');var i=(TextureImporter)AssetImporter.GetAtPath(p);
            bool normal=p.Contains("_Normal"),mask=p.Contains("_Mask");
            i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            i.sRGBTexture=!normal&&!mask;i.mipmapEnabled=true;i.maxTextureSize=512;
            i.wrapMode=p.Contains("OpticalLens")||p.Contains("Cookie")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            i.textureCompression=TextureImporterCompression.CompressedHQ;i.SaveAndReimport();
        }
    }
    static Mesh Extract(Mesh source,NightInspect.Part part,Transform body,string name,bool top)
    {
        var vs=source.vertices;var ns=source.normals;var map=new Dictionary<int,int>();var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        foreach(int old in part.indices)
        {
            if(!map.TryGetValue(old,out int id))
            {
                id=v.Count;map[old]=id;var normal=vehicle.InverseTransformDirection(body.TransformDirection(ns[old])).normalized;
                var p=vehicle.InverseTransformPoint(body.TransformPoint(vs[old]));v.Add(p+normal*.00065f);n.Add(normal);
                uv.Add(top?new Vector2(.5f+(p.x-part.center.x)/part.size.x,.5f+(p.z-part.center.z)/part.size.z):
                    new Vector2(.5f+(p.x-part.center.x)/part.size.x,.5f+(p.y-part.center.y)/part.size.y));
            }
            tri.Add(id);
        }
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
        return Store(mesh,Art+"Meshes/"+name+".asset");
    }
    static void FrontFloor()
    {
        var b=new NightGeometry();
        b.Trapezoid(1.10f,1.49f,.84f,.29f,-.731f,.031f,0);
        b.Trapezoid(1.49f,2.58f,.29f,.145f,-.731f,.031f,0);
        foreach(float side in new[]{-1f,1f})
        {
            b.Beam(new Vector3(side*.81f,-.781f,1.10f),new Vector3(side*.27f,-.781f,1.48f),.065f,.055f,0);
            b.Beam(new Vector3(side*.245f,-.785f,1.49f),new Vector3(side*.125f,-.785f,2.55f),.043f,.045f,0);
            for(int i=0;i<7;i++)
            {
                float z=1.51f+i*.135f;float w=Mathf.Lerp(.19f,.11f,(z-1.49f)/1.09f);
                b.Box(new Vector3(side*w*.58f,-.774f,z),new Vector3(w*.78f,.021f,.037f),1);
            }
            b.Cylinder(new Vector3(side*.17f,-.792f,2.18f),new Vector3(side*.25f,-.792f,2.18f),.039f,3,16);
            foreach(float z in new[]{1.53f,1.86f,2.30f,2.51f})
            {float x=side*Mathf.Lerp(.22f,.12f,(z-1.49f)/1.09f);b.Cylinder(new Vector3(x,-.76f,z),new Vector3(x,-.798f,z),.017f,2,6);}
        }
        b.Cylinder(new Vector3(-.15f,-.792f,2.18f),new Vector3(.15f,-.792f,2.18f),.032f,1,20);
        b.Box(new Vector3(0,-.785f,1.34f),new Vector3(.48f,.085f,.26f),1);
        for(int i=0;i<6;i++)b.Box(new Vector3(-.19f+i*.076f,-.833f,1.34f),new Vector3(.022f,.023f,.22f),1);
        Store(b.Mesh("07_Front_Subframe_Rack_And_Sump"),"Assets/_Project/Art/TumblerVisuals/Meshes/07_Front_Subframe_Rack_And_Sump.asset");
        foreach(string name in new[]{"Chassis_Graphite","Cast_Alloy","Zinc_Hardware"})
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/TumblerVisuals/Materials/"+name+".mat");
            m.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/MachinedMetal_Base.png"));
            m.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/MachinedMetal_Normal.png"));
            m.SetFloat("_UVBase",5);m.SetFloat("_ObjectSpaceUVMapping",1);m.SetFloat("_NormalScale",.20f);
            HDShaderUtils.ResetMaterialKeywords(m);EditorUtility.SetDirty(m);
        }
    }
    static void Tanks(Transform parent,IEnumerable<NightInspect.Part> parts)
    {
        var candidates=OldTanks(parts);
        if(candidates.Length!=2)throw new Exception("Expected two interior turbo canisters");
        for(int i=0;i<2;i++)
        {
            var root=Child("Interior Boost Canister "+(i+1),parent).transform;root.localPosition=candidates[i].center+new Vector3(0,-.035f,-.095f);
            var g=new NightGeometry(4);
            g.LatheY(Vector3.zero,new[]{new Vector2(-.350f,.001f),new Vector2(-.340f,.070f),new Vector2(-.310f,.150f),
                new Vector2(-.270f,.163f),new Vector2(.220f,.163f),new Vector2(.270f,.150f),new Vector2(.300f,.070f),new Vector2(.310f,.001f)},0);
            foreach(float y in new[]{-.19f,.15f})
            {
                g.LatheY(new Vector3(0,y,0),new[]{new Vector2(-.015f,.163f),new Vector2(-.012f,.169f),new Vector2(.012f,.169f),new Vector2(.015f,.163f)},1);
                g.Box(new Vector3(0,y,.150f),new Vector3(.13f,.044f,.035f),2);
                foreach(float x in new[]{-.05f,.05f})g.Cylinder(new Vector3(x,y,.165f),new Vector3(x,y,.176f),.008f,1,6);
            }
            g.Cylinder(new Vector3(0,.295f,0),new Vector3(0,.337f,0),.042f,1);
            g.Cylinder(new Vector3(0,.337f,0),new Vector3(0,.350f,0),.029f,3,24);
            g.Cylinder(new Vector3(-.05f,.333f,0),new Vector3(.05f,.333f,0),.009f,1,16);
            var mesh=Store(g.Mesh("Interior_Boost_Canister_"+i),Art+"Meshes/Interior_Boost_Canister_"+i+".asset");
            Model("Canister shell, straps and valve",root,mesh,tank,steel,dark,bronze);
            var hose=new NightGeometry(1);Vector3 a=new Vector3(0,.333f,-.02f),b=new Vector3(-Mathf.Sign(root.localPosition.x)*.12f,.31f,-.15f),c=new Vector3(-Mathf.Sign(root.localPosition.x)*.26f,.25f,-.25f);
            hose.Cylinder(a,b,.012f,0,12);hose.Cylinder(b,c,.012f,0,12);
            Model("Protected pressure line",root,Store(hose.Mesh("Pressure_Line_"+i),Art+"Meshes/Pressure_Line_"+i+".asset"),rubber);
        }
    }
    static void Brakes(Transform parent,TumblerController controller,BrakeDiscThermal thermal)
    {
        var so=new SerializedObject(controller);var wc=new List<WheelCollider>();var visuals=new List<Transform>();
        foreach(string field in new[]{"frontWheelsColliders","rearWheelsColliders"})
        {var a=so.FindProperty(field);for(int i=0;i<a.arraySize;i++)wc.Add((WheelCollider)a.GetArrayElementAtIndex(i).objectReferenceValue);}
        var aVis=so.FindProperty("wheelsTransforms");for(int i=0;i<aVis.arraySize;i++)visuals.Add((Transform)aVis.GetArrayElementAtIndex(i).objectReferenceValue);
        var rotors=new List<Renderer>();var calipers=new List<Transform>();var centers=new List<Vector3>();var offsets=new List<Vector3>();
        for(int i=0;i<visuals.Count;i++)
        {
            var w=visuals[i];var old=w.Find("Ventilated Brake Disc");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var mf=w.GetComponent<MeshFilter>();var r=w.GetComponent<Renderer>();var mats=r.sharedMaterials;var bounds=mf.sharedMesh.bounds;
            int ri=Array.FindIndex(mats,m=>m.name.Contains("rim_black")||m.name=="Forged_Gunmetal");float radius=0,outside=0;
            if(ri<0)throw new Exception("Wheel rim material not found: "+w.name);
            float side=Mathf.Sign(vehicle.InverseTransformPoint(w.position).x);var vertices=mf.sharedMesh.vertices;
            foreach(int j in mf.sharedMesh.GetTriangles(Mathf.Max(0,ri)))
            {var p=vertices[j]-bounds.center;radius=Mathf.Max(radius,new Vector2(p.y,p.z).magnitude);outside=Mathf.Max(outside,p.x*side);}
            radius*=.69f;float inside=-Mathf.Min(outside,bounds.extents.x)+.055f;
            var g=new NightGeometry(3);g.Rotor(radius,.024f);
            var mesh=Store(g.Mesh("Brake_Disc_"+i),Art+"Meshes/Brake_Disc_"+i+".asset");
            var disc=Model("Ventilated Brake Disc",w,mesh,steel,dark,bronze);disc.transform.localPosition=bounds.center+Vector3.right*(side*inside);
            rotors.Add(disc);centers.Add(bounds.center);
            var housing=new NightGeometry(3);housing.RoundedCaliper();
            var caliper=Model("Fixed Brake Caliper "+i,parent,Store(housing.Mesh("Brake_Caliper_"+i),Art+"Meshes/Brake_Caliper_"+i+".asset"),dark,rubber,steel);
            var offset=new Vector3(side*(inside-.01f),radius*.65f,radius*.57f);offsets.Add(offset);
            caliper.transform.localPosition=vehicle.InverseTransformPoint(w.TransformPoint(bounds.center))+offset;
            caliper.transform.localScale=new Vector3(-side,1,1);
            calipers.Add(caliper.transform);
            for(int j=0;j<mats.Length;j++)
            {
                if(mats[j].name.Contains("tire_rubber"))mats[j]=rubber;
                else if(mats[j].name.Contains("rim_black"))mats[j]=dark;
                else if(mats[j].name.Contains("silver_nut"))mats[j]=steel;
            }
            r.sharedMaterials=mats;
            Debug.Log("BRAKE FIT "+i+" radius="+radius+" inboardAxleOffset="+inside+" wheelMeshCenter="+bounds.center);
        }
        var s=new SerializedObject(thermal);
        SetObjects(s,"wheels",wc.Cast<UnityEngine.Object>().ToArray());SetObjects(s,"wheelVisuals",visuals.Cast<UnityEngine.Object>().ToArray());
        SetObjects(s,"rotors",rotors.Cast<UnityEngine.Object>().ToArray());SetObjects(s,"calipers",calipers.Cast<UnityEngine.Object>().ToArray());
        SetVectors(s,"wheelCenters",centers);SetVectors(s,"caliperOffsets",offsets);s.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetObjects(SerializedObject so,string field,UnityEngine.Object[] values)
    {var p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    static void SetVectors(SerializedObject so,string field,List<Vector3> values)
    {var p=so.FindProperty(field);p.arraySize=values.Count;for(int i=0;i<values.Count;i++)p.GetArrayElementAtIndex(i).vector3Value=values[i];}
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Art+"Materials");Directory.CreateDirectory(Art+"Meshes");TextureImport();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");vehicle=GameObject.Find("Tumbler").transform;
            var previous=vehicle.Find("Lighting and Auxiliary Equipment");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            steel=Surface("Brushed_Brake_Steel",new Color(.48f,.51f,.53f),.92f,.60f,"MachinedMetal",false);
            dark=Surface("Forged_Gunmetal",new Color(.22f,.25f,.27f),.83f,.48f,"MachinedMetal");
            bronze=Surface("Aged_Bronze_Fuel_Caps",new Color(.49f,.27f,.10f),.86f,.57f,"MachinedMetal",false);
            rubber=Surface("Molded_Tire_Rubber",new Color(.17f,.18f,.18f),0,.21f,"Rubber");
            tank=Surface("Pressure_Canister_Composite",Color.white,.73f,.45f,"BoostTank",false);
            optical=Surface("Prismatic_Headlamp_Optics",new Color(.77f,.83f,.87f),.65f,.95f,"OpticalLens",false);
            optical.SetTexture("_EmissiveColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/OpticalLens_Emission.png"));
            HDShaderUtils.ResetMaterialKeywords(optical);EditorUtility.SetDirty(optical);
            FrontFloor();
            var root=Child("Lighting and Auxiliary Equipment",vehicle);
            var mf=vehicle.Find("Body").GetComponent<MeshFilter>();var source=OriginalBody;var parts=NightInspect.Parts(source,mf.transform,vehicle);
            RemoveOriginalTanks(mf,source,parts);
            var lamps=parts.Where(p=>p.material==3&&p.triangles>=27000&&p.size.x<.09f&&p.size.y<.09f&&p.center.z>.6f).OrderByDescending(p=>p.center.y).ThenBy(p=>p.center.x).ToArray();
            if(lamps.Length!=8)throw new Exception("Expected original eight optical meshes, found "+lamps.Length);
            var lights=new List<Light>();var lenses=new List<Renderer>();
            for(int i=0;i<lamps.Length;i++)
            {
                var part=lamps[i];var lens=Model("Textured Headlamp "+(i+1),root.transform,Extract(source,part,mf.transform,"Headlamp_Lens_"+i,false),optical);
                lens.shadowCastingMode=ShadowCastingMode.Off;lenses.Add(lens);
                var go=Child("Headlight Beam "+(i+1),root.transform);go.transform.localPosition=part.center+Vector3.forward*(part.size.z*.5f+.004f);
                var l=go.AddComponent<Light>();l.type=LightType.Spot;l.lightUnit=LightUnit.Candela;l.color=new Color(1,.91f,.75f);l.intensity=8000;l.range=38;l.spotAngle=56;l.innerSpotAngle=38;l.shadows=LightShadows.Soft;
                var hd=go.AddComponent<HDAdditionalLightData>();hd.volumetricDimmer=1;hd.SetShadowResolution(256);l.enabled=false;lights.Add(l);
            }
            foreach(var p in parts.Where(p=>p.material==3&&p.triangles==2048&&p.center.y>.6f&&p.center.z<-1.5f&&p.size.x>.10f&&p.size.x<.14f))
                Model("Bronze Fuel Filler Cap",root.transform,Extract(source,p,mf.transform,"Fuel_Cap_"+(p.center.x<0?"L":"R"),true),bronze);
            Tanks(root.transform,parts);
            var haze=Child("Headlight Volumetric Haze",root.transform);haze.transform.localPosition=new Vector3(0,1,30);
            var fog=haze.AddComponent<LocalVolumetricFog>();var fp=fog.parameters;fp.size=new Vector3(16,5,66);fp.meanFreePath=95;fp.albedo=new Color(.7f,.73f,.76f);fp.positiveFade=fp.negativeFade=Vector3.one*.28f;fp.distanceFadeStart=65;fp.distanceFadeEnd=100;fog.parameters=fp;fog.enabled=false;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefab);UnityEngine.Object.DestroyImmediate(root);
            root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,vehicle);root.name="Lighting and Auxiliary Equipment";
            lights=root.GetComponentsInChildren<Light>(true).ToList();lenses=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Textured Headlamp")).ToList();
            var sys=vehicle.GetComponent<TumblerSystems>();if(sys==null)sys=vehicle.gameObject.AddComponent<TumblerSystems>();
            var s=new SerializedObject(sys);SetObjects(s,"headlights",lights.Cast<UnityEngine.Object>().ToArray());SetObjects(s,"headlampLenses",lenses.Cast<UnityEngine.Object>().ToArray());
            SetObjects(s,"lowBeamLamps",lights.Take(4).Cast<UnityEngine.Object>().ToArray());
            var effects=new SerializedObject(vehicle.GetComponent<TumblerEffects>());var smoke=effects.FindProperty("tireSmokes");var excluded=new HashSet<ParticleSystem>();
            for(int i=0;i<smoke.arraySize;i++)excluded.Add((ParticleSystem)smoke.GetArrayElementAtIndex(i).objectReferenceValue);
            SetObjects(s,"turbineParticles",vehicle.GetComponentsInChildren<ParticleSystem>(true).Where(p=>!excluded.Contains(p)).Cast<UnityEngine.Object>().ToArray());
            s.FindProperty("lowBeamIntensity").floatValue=30000;s.FindProperty("highBeamIntensity").floatValue=90000;
            s.FindProperty("volumetricHaze").objectReferenceValue=root.GetComponentInChildren<LocalVolumetricFog>(true);s.ApplyModifiedPropertiesWithoutUndo();sys.SetLightMode(TumblerSystems.LightMode.Off);
            var thermal=vehicle.GetComponent<BrakeDiscThermal>();if(thermal==null)thermal=vehicle.gameObject.AddComponent<BrakeDiscThermal>();
            Brakes(root.transform,vehicle.GetComponent<TumblerController>(),thermal);
            var bodyMaterials=mf.GetComponent<Renderer>().sharedMaterials;bodyMaterials[3]=steel;bodyMaterials[4]=steel;mf.GetComponent<Renderer>().sharedMaterials=bodyMaterials;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Rendering/SandboxExposure.asset");
            if(!profile.TryGet(out Fog globalFog)){globalFog=profile.Add<Fog>(true);AssetDatabase.AddObjectToAsset(globalFog,profile);}
            globalFog.enabled.Override(true);globalFog.enableVolumetricFog.Override(true);globalFog.meanFreePath.Override(1000000);globalFog.depthExtent.Override(100);globalFog.globalLightProbeDimmer.Override(.15f);globalFog.anisotropy.Override(.1f);
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(globalFog);
            AuthorHUD();
            var hud=UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>().GetComponent<RectTransform>();hud.sizeDelta=new Vector2(300,240);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);EditorSceneManager.SaveScene(vehicle.gameObject.scene);
            Debug.Log("NIGHT SYSTEMS AUTHORING COMPLETE");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }

    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static Image Image(string name,Transform parent,float x,float y,float w,float h,Color color)
    {var r=Rect(name,parent,x,y,w,h);r.gameObject.AddComponent<CanvasRenderer>();var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
    static TextMeshProUGUI Text(string name,string value,Transform parent,float x,float y,float w,float h,float size,Color color)
    {
        var r=Rect(name,parent,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.fontStyle=FontStyles.Bold;t.color=color;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;return t;
    }
    static VehicleStatusIcon Icon(string name,Transform parent,float x,float y,VehicleStatusIcon.Symbol symbol)
    {var r=Rect(name,parent,x,y,21,21);r.gameObject.AddComponent<CanvasRenderer>();var i=r.gameObject.AddComponent<VehicleStatusIcon>();i.SetSymbol(symbol);i.raycastTarget=false;i.color=new Color(.4f,.45f,.46f);return i;}
    static void AuthorHUD()
    {
        const string path="Assets/_Project/Prefabs/DigitalVehicleHUD.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);var r=root.GetComponent<RectTransform>();
        var old=root.transform.Find("Auxiliary Systems");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        if(r.sizeDelta.y<200)foreach(RectTransform child in root.transform)child.anchoredPosition+=Vector2.up*64;
        r.sizeDelta=new Vector2(300,240);
        var footer=Rect("Auxiliary Systems",root.transform,0,0,300,64);
        Image("Footer Shadow",footer,-3,-3,306,67,new Color(0,0,0,.25f));
        Image("Footer Panel",footer,0,0,300,64,new Color(.027f,.036f,.041f,.98f));
        Image("Systems Divider",footer,18,62,264,1,new Color(.15f,.20f,.21f));
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var dim=new Color(.51f,.58f,.60f);
        var label=Text("Boost State","BOOST",footer,18,38,60,14,9,dim);
        var back=Image("Boost Track",footer,80,44,162,6,new Color(.12f,.17f,.18f));
        var fill=Image("Boost Reserve",back.transform,0,0,162,6,new Color(.69f,.57f,.37f));
        var fr=fill.rectTransform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
        var percent=Text("Boost Percent","100%",footer,248,38,34,15,10,dim);percent.alignment=TextAlignmentOptions.Right;
        var li=Icon("Light Status",footer,18,11,VehicleStatusIcon.Symbol.LightsOff);var ls=Text("Light State","OFF",footer,45,17,47,14,11,dim);Text("Lights Caption","LIGHTS",footer,45,6,47,10,8.5f,dim);
        var bi=Icon("Handbrake Status",footer,110,11,VehicleStatusIcon.Symbol.Handbrake);var bs=Text("Handbrake State","OFF",footer,137,17,48,14,11,dim);Text("Brake Caption","H-BRAKE",footer,137,6,48,10,8.5f,dim);
        var si=Icon("Bat Status",footer,203,11,VehicleStatusIcon.Symbol.Bat);var ss=Text("Stealth State","OFF",footer,231,17,51,14,11,dim);Text("Stealth Caption","STEALTH",footer,231,6,51,10,8.5f,dim);
        var so=new SerializedObject(root.GetComponent<DigitalVehicleHUD>());
        foreach(var entry in new Dictionary<string,UnityEngine.Object>{{"boostFill",fr},{"boostFillImage",fill},{"boostPercentText",percent},{"boostStateText",label},
            {"lightIcon",li},{"handbrakeIcon",bi},{"stealthIcon",si},{"lightStateText",ls},{"handbrakeStateText",bs},{"stealthStateText",ss}})
            so.FindProperty(entry.Key).objectReferenceValue=entry.Value;
        so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
    }
}
