using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExportTumblerSnapshot
{
    [Serializable] public class Snapshot {public string schema="tumbler-complete-model-1",sourceScene="Assets/_Project/Scenes/Sandbox.unity",colorSpace="linear",sourceCommit,unityVersion;public float[] boundsMin,boundsMax;public Node[] nodes;public MaterialData[] materials;public MeshData[] meshes;public int coreRendererCount,particleSnapshotCount;}
    [Serializable] public class Node {public int id,parent=-1,mesh=-1;public string name,path,kind="helper",metadata;public bool active,visible;public float[] matrix;public int[] materials;public LightData light;}
    [Serializable] public class LightData {public string type,unit;public bool enabled;public float intensity,range,angle,innerAngle;public float[] color;}
    [Serializable] public class MaterialData {public string name,shader,source,baseMap,normalMap,maskMap,emissionMap,metallicMap,roughnessMap;public float[] color,emission,tiling,offset;public float metallic,smoothness,metalMin,metalMax,smoothMin,smoothMax,normalStrength,worldScale;public int uvMode;public bool transparent,doubleSided,particle,baked;}
    [Serializable] public class MeshData {public string name,file,source;public int vertexCount;public long positionOffset,normalOffset,uvOffset,colorOffset=-1;public Primitive[] primitives;public bool bakedUV;}
    [Serializable] public class Primitive {public long offset;public int count;}
    static readonly List<MaterialData> materials=new List<MaterialData>();
    static readonly List<MeshData> meshes=new List<MeshData>();
    static readonly Dictionary<(Material,bool,bool),int> materialIds=new Dictionary<(Material,bool,bool),int>();
    static readonly Dictionary<Texture2D,string> textures=new Dictionary<Texture2D,string>();
    static string folder;
    static Transform root;
    static Material baker;
    static float F(Material m,string key,float fallback)=>m.HasProperty(key)?m.GetFloat(key):fallback;
    static Color ColorOf(Material m,string key,Color fallback)=>m.HasProperty(key)?m.GetColor(key):fallback;
    static float[] C(Color c)=>new[]{c.r,c.g,c.b,c.a};
    static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    static string Safe(string s)=>string.Concat(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_'));
    static string TextureFile(Material m,string property)
    {
        if(!m.HasProperty(property)||!(m.GetTexture(property) is Texture2D texture))return null;
        if(textures.TryGetValue(texture,out string saved))return saved;
        var source=AssetDatabase.GetAssetPath(texture);var extension=Path.GetExtension(source).ToLowerInvariant();
        if(string.IsNullOrEmpty(source)||source.Contains("unity_builtin"))return null;
        string relative="SourceTextures/"+textures.Count.ToString("D3")+"_"+Safe(texture.name);
        if(new[]{".png",".jpg",".jpeg",".tga",".exr",".hdr",".tif",".tiff",".bmp"}.Contains(extension)&&File.Exists(source))
        {relative+=extension;File.Copy(source,Path.Combine(folder,relative),true);}
        else
        {
            relative+=".png";var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();File.WriteAllBytes(Path.Combine(folder,relative),copy.EncodeToPNG());
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(copy);RenderTexture.ReleaseTemporary(rt);
        }
        textures[texture]=relative;return relative;
    }
    static int MaterialId(Material m,bool rotor,bool particle)
    {
        var key=(m,rotor,particle);if(materialIds.TryGetValue(key,out int existing))return existing;
        string baseProperty=m.HasProperty("_BaseColorMap")?"_BaseColorMap":"_MainTex";
        var color=ColorOf(m,"_BaseColor",ColorOf(m,"_Color",ColorOf(m,"_TintColor",Color.white)));
        if(rotor)color=new Color(.4f,.42f,.44f,1);
        // Unity's non-HDR Color properties cross from sRGB to linear on the GPU.
        // Portable PBR factors and the bake shader's untyped uniform expect linear RGB.
        if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
        bool transparent=F(m,"_SurfaceType",0)>.5f||particle;if(!transparent)color.a=1;
        var scale=m.HasProperty(baseProperty)?m.GetTextureScale(baseProperty):Vector2.one;
        var offset=m.HasProperty(baseProperty)?m.GetTextureOffset(baseProperty):Vector2.zero;
        var data=new MaterialData{name=m.name+(rotor?"_ColdRotor":""),shader=m.shader.name,source=AssetDatabase.GetAssetPath(m),color=C(color),
            emission=C(ColorOf(m,"_EmissiveColor",ColorOf(m,"_EmissionColor",Color.black))),metallic=F(m,"_Metallic",0),smoothness=F(m,"_Smoothness",F(m,"_Glossiness",.35f)),
            metalMin=F(m,"_MetallicRemapMin",0),metalMax=F(m,"_MetallicRemapMax",1),smoothMin=F(m,"_SmoothnessRemapMin",0),smoothMax=F(m,"_SmoothnessRemapMax",1),
            normalStrength=F(m,"_NormalScale",F(m,"_BumpScale",1)),worldScale=F(m,"_TexWorldScale",1),uvMode=(int)F(m,"_UVBase",0),
            doubleSided=true,transparent=transparent,particle=particle,tiling=new[]{scale.x,scale.y},offset=new[]{offset.x,offset.y},
            baseMap=TextureFile(m,baseProperty),normalMap=TextureFile(m,m.HasProperty("_NormalMap")?"_NormalMap":"_BumpMap"),maskMap=TextureFile(m,"_MaskMap"),
            emissionMap=TextureFile(m,m.HasProperty("_EmissiveColorMap")?"_EmissiveColorMap":"_EmissionMap")};
        int id=materials.Count;materials.Add(data);materialIds[key]=id;return id;
    }
    static void SetBakeMaterial(Material source,MaterialData info,int mode)
    {
        Texture Get(string p)=>source.HasProperty(p)?source.GetTexture(p):null;
        baker.SetTexture("_BaseMap",Get(source.HasProperty("_BaseColorMap")?"_BaseColorMap":"_MainTex")??Texture2D.whiteTexture);
        baker.SetTexture("_MaskMap",Get("_MaskMap")??Texture2D.whiteTexture);baker.SetTexture("_NormalMap",Get("_NormalMap")??Texture2D.normalTexture);
        baker.SetVector("_Tint",new Vector4(info.color[0],info.color[1],info.color[2],info.color[3]));baker.SetVector("_ST",new Vector4(info.tiling[0],info.tiling[1],info.offset[0],info.offset[1]));
        baker.SetFloat("_Mode",mode);baker.SetFloat("_UVMode",info.uvMode);baker.SetFloat("_WorldScale",info.worldScale);baker.SetFloat("_HasMask",Get("_MaskMap")!=null?1:0);
        baker.SetFloat("_HasNormal",Get("_NormalMap")!=null?1:0);baker.SetFloat("_NormalStrength",info.normalStrength);baker.SetFloat("_Metallic",info.metallic);baker.SetFloat("_Smoothness",info.smoothness);
        baker.SetVector("_Remap",new Vector4(info.metalMin,info.metalMax,info.smoothMin,info.smoothMax));
    }
    static void Bake(Mesh mesh,Material[] source,int[] ids,int nodeId,out int[] output)
    {
        int resolution=mesh.bounds.size.magnitude>2.5f?2048:mesh.bounds.size.magnitude>.5f?1024:512;
        UnwrapParam.SetDefaults(out var param);param.packMargin=6f/resolution;
        Unwrapping.GenerateSecondaryUVSet(mesh,param);
        if(mesh.uv2.Length!=mesh.vertexCount)throw new Exception("UV unwrap failed: "+mesh.name);
        string[] maps=new string[4];string[] suffix={"BaseColor","Metallic","Roughness","Normal"};
        for(int mode=0;mode<4;mode++)
        {
            var rt=RenderTexture.GetTemporary(resolution,resolution,0,RenderTextureFormat.ARGB32,mode==0?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;RenderTexture.active=rt;GL.Clear(true,true,Color.clear);GL.PushMatrix();GL.LoadOrtho();GL.Viewport(new Rect(0,0,resolution,resolution));
            bool srgb=GL.sRGBWrite;GL.sRGBWrite=mode==0;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                if(mesh.GetIndexCount(sub)==0)continue;int slot=Mathf.Min(sub,source.Length-1);SetBakeMaterial(source[slot],materials[ids[slot]],mode);
                if(!baker.SetPass(0))throw new Exception("PBR export shader failed");Graphics.DrawMeshNow(mesh,Matrix4x4.identity,sub);
            }
            GL.sRGBWrite=srgb;GL.PopMatrix();var texture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,mode!=0);
            texture.ReadPixels(new Rect(0,0,resolution,resolution),0,0);texture.Apply();
            maps[mode]="BakedTextures/"+nodeId.ToString("D3")+"_"+Safe(mesh.name)+"_"+suffix[mode]+".png";
            File.WriteAllBytes(Path.Combine(folder,maps[mode]),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        }
        output=new int[ids.Length];
        for(int i=0;i<ids.Length;i++)
        {
            var original=materials[ids[i]];var copy=JsonUtility.FromJson<MaterialData>(JsonUtility.ToJson(original));
            copy.name=original.name+"_UV_"+nodeId;copy.baked=true;copy.uvMode=0;copy.color=new[]{1f,1f,1f,1f};copy.emission=new[]{0f,0f,0f,1f};
            copy.tiling=new[]{1f,1f};copy.offset=new[]{0f,0f};copy.baseMap=maps[0];copy.metallicMap=maps[1];copy.roughnessMap=maps[2];copy.normalMap=maps[3];copy.maskMap=null;copy.emissionMap=null;copy.normalStrength=1;
            output[i]=materials.Count;materials.Add(copy);
        }
        Debug.Log("BAKED PBR "+nodeId+" "+mesh.name+" "+resolution);
    }
    static int SaveMesh(Mesh mesh,bool bake,string source)
    {
        var m=new MeshData{name=mesh.name,source=source,file="Meshes/"+meshes.Count.ToString("D3")+".bin",vertexCount=mesh.vertexCount,bakedUV=bake};
        using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,m.file))))
        {
            m.positionOffset=writer.BaseStream.Position;foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            var normals=mesh.normals;if(normals.Length!=mesh.vertexCount){mesh.RecalculateNormals();normals=mesh.normals;}
            m.normalOffset=writer.BaseStream.Position;foreach(var n in normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}
            var uv=bake?mesh.uv2:mesh.uv;m.uvOffset=writer.BaseStream.Position;
            for(int i=0;i<mesh.vertexCount;i++){var u=i<uv.Length?uv[i]:Vector2.zero;writer.Write(u.x);writer.Write(u.y);}
            var colors=mesh.colors;if(colors.Length==mesh.vertexCount){m.colorOffset=writer.BaseStream.Position;foreach(var c in colors){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}}
            m.primitives=new Primitive[mesh.subMeshCount];for(int s=0;s<mesh.subMeshCount;s++)
            {var indices=mesh.GetTriangles(s);m.primitives[s]=new Primitive{offset=writer.BaseStream.Position,count=indices.Length};foreach(int i in indices)writer.Write(i);}
        }
        int id=meshes.Count;meshes.Add(m);return id;
    }
    static string Metadata(Transform t)
    {
        var parts=new List<string>();foreach(var component in t.GetComponents<Component>())
        {
            if(component==null||component is Transform||component is MeshFilter||component is Renderer)continue;
            parts.Add(component.GetType().Name+": "+EditorJsonUtility.ToJson(component));
        }
        return string.Join("\n",parts);
    }
    public static void Run()
    {
        try
        {
            folder=Environment.GetEnvironmentVariable("TUMBLER_EXPORT_DIR")??Path.GetFullPath("../tumbler-export/snapshot");
            foreach(var directory in new[]{folder,folder+"/Meshes",folder+"/BakedTextures",folder+"/SourceTextures"})Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");root=GameObject.Find("Tumbler").transform;
            var shader=Shader.Find("Hidden/Tumbler/ExportBake");if(shader==null||!shader.isSupported)throw new Exception("Export bake shader unavailable");baker=new Material(shader);
            var transforms=root.GetComponentsInChildren<Transform>(true);var ids=transforms.Select((t,i)=>(t,i)).ToDictionary(p=>p.t,p=>p.i);
            var nodes=new List<Node>();var thermal=new SerializedObject(root.GetComponent<BrakeDiscThermal>());var rotorIds=new HashSet<Renderer>();
            var prop=thermal.FindProperty("rotors");for(int i=0;i<prop.arraySize;i++)rotorIds.Add((Renderer)prop.GetArrayElementAtIndex(i).objectReferenceValue);
            var system=new SerializedObject(root.GetComponent<TumblerSystems>());var particles=system.FindProperty("turbineParticles");var fx=new HashSet<ParticleSystem>();
            for(int i=0;i<particles.arraySize;i++)fx.Add((ParticleSystem)particles.GetArrayElementAtIndex(i).objectReferenceValue);
            var camera=new GameObject("Temporary particle snapshot camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=root.TransformPoint(new Vector3(0,1.6f,-7));camera.transform.LookAt(root.position+root.up*.2f);
            Bounds bounds=default;bool hasBounds=false;int core=0,particleCount=0;
            foreach(var t in transforms)
            {
                var n=new Node{id=ids[t],parent=t==root?-1:ids[t.parent],name=t.name,path=AnimationUtility.CalculateTransformPath(t,root),active=t.gameObject.activeInHierarchy,metadata=Metadata(t)};
                var matrix=t==root?Matrix4x4.Scale(root.lossyScale):Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);
                n.matrix=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)n.matrix[r*4+c]=matrix[r,c];
                var renderer=t.GetComponent<MeshRenderer>();var filter=t.GetComponent<MeshFilter>();
                if(renderer!=null&&filter!=null&&filter.sharedMesh!=null&&renderer.enabled&&n.active)
                {
                    var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var source=renderer.sharedMaterials;
                    n.materials=source.Select(m=>MaterialId(m,rotorIds.Contains(renderer),false)).ToArray();
                    bool bake=n.materials.Any(i=>materials[i].uvMode==5);
                    int triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s));
                    if(bake){Bake(mesh,source,n.materials,n.id,out var baked);n.materials=baked;}
                    if(triangles!=Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s)))throw new Exception("Baking changed triangle count");
                    n.mesh=SaveMesh(mesh,bake,AssetDatabase.GetAssetPath(filter.sharedMesh));n.kind="mesh";n.visible=true;core++;
                    if(!hasBounds){bounds=renderer.bounds;hasBounds=true;}else bounds.Encapsulate(renderer.bounds);
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                var light=t.GetComponent<Light>();if(light!=null)n.light=new LightData{type=light.type.ToString(),unit=light.lightUnit.ToString(),enabled=light.enabled&&n.active,intensity=light.intensity,range=light.range,angle=light.spotAngle,innerAngle=light.innerSpotAngle,color=C(light.color.linear)};
                var ps=t.GetComponent<ParticleSystem>();var pr=t.GetComponent<ParticleSystemRenderer>();
                if(ps!=null&&pr!=null&&fx.Contains(ps))
                {
                    ps.useAutoRandomSeed=false;ps.randomSeed=73;ps.Simulate(1.25f,false,true,true);var mesh=new Mesh{name=ps.name+"_StaticFXReference"};pr.BakeMesh(mesh,camera,false);
                    if(mesh.vertexCount>0)
                    {
                        n.mesh=SaveMesh(mesh,false,"Unity ParticleSystem snapshot");n.materials=pr.sharedMaterials.Select(m=>MaterialId(m,false,true)).ToArray();n.kind="particle_snapshot";n.visible=false;particleCount++;
                    }
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);UnityEngine.Object.DestroyImmediate(mesh);
                }
                nodes.Add(n);
            }
            string commit=Environment.GetEnvironmentVariable("TUMBLER_EXPORT_COMMIT");
            if(string.IsNullOrWhiteSpace(commit))throw new Exception("Set TUMBLER_EXPORT_COMMIT to the source Git revision before exporting");
            var manifest=new Snapshot{sourceCommit=commit,unityVersion=Application.unityVersion,nodes=nodes.ToArray(),materials=materials.ToArray(),meshes=meshes.ToArray(),coreRendererCount=core,particleSnapshotCount=particleCount,
                boundsMin=V(root.InverseTransformPoint(bounds.min)),boundsMax=V(root.InverseTransformPoint(bounds.max))};
            if(core<35||!nodes.Any(n=>n.name=="Body"&&n.mesh>=0&&meshes[n.mesh].source.EndsWith(".tumblerbody")))throw new Exception("Incomplete assembled vehicle snapshot");
            File.WriteAllText(Path.Combine(folder,"snapshot.json"),JsonUtility.ToJson(manifest,true));
            Debug.Log("COMPLETE TUMBLER SNAPSHOT "+core+" mesh renderers, "+particleCount+" optional particle snapshots, "+materials.Count+" material variants");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
