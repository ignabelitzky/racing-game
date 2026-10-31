using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NightInspect
{
    [Serializable] public class Part { public int material, id, triangles; public Vector3 center,size,normal; [NonSerialized] public int[] indices; }
    [Serializable] public class Report { public string[] materials; public Part[] parts; public Vector3[] wheelCenters; }
    public static List<Part> Parts(Mesh mesh, Transform meshTransform, Transform root)
    {
        var v=mesh.vertices; var result=new List<Part>();
        for(int sm=0;sm<mesh.subMeshCount;sm++)
        {
            var tris=mesh.GetTriangles(sm);var parent=new int[v.Length];for(int i=0;i<parent.Length;i++)parent[i]=i;
            var positions=new Dictionary<Vector3Int,int>();
            Func<int,int> find=null;find=i=>{while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;};
            Action<int,int> join=(a,b)=>{a=find(a);b=find(b);if(a!=b)parent[b]=a;};
            foreach(int i in tris)
            {
                var p=v[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
                if(positions.TryGetValue(key,out int previous))join(i,previous);else positions[key]=i;
            }
            for(int i=0;i<tris.Length;i+=3){join(tris[i],tris[i+1]);join(tris[i],tris[i+2]);}
            var groups=new Dictionary<int,List<int>>();
            for(int i=0;i<tris.Length;i+=3)
            {int k=find(tris[i]);if(!groups.TryGetValue(k,out var list))groups[k]=list=new List<int>();list.AddRange(new[]{tris[i],tris[i+1],tris[i+2]});}
            foreach(var item in groups)
            {
                var b=new Bounds(root.InverseTransformPoint(meshTransform.TransformPoint(v[item.Value[0]])),Vector3.zero);
                var normal=Vector3.zero;
                foreach(int i in item.Value)b.Encapsulate(root.InverseTransformPoint(meshTransform.TransformPoint(v[i])));
                for(int i=0;i<item.Value.Count;i+=3)
                {var a=v[item.Value[i]];var c=v[item.Value[i+1]];var d=v[item.Value[i+2]];normal+=Vector3.Cross(c-a,d-a);}
                result.Add(new Part{material=sm,id=item.Key,triangles=item.Value.Count/3,center=b.center,size=b.size,normal=root.InverseTransformDirection(meshTransform.TransformDirection(normal.normalized)),indices=item.Value.ToArray()});
            }
        }
        return result;
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            var root=GameObject.Find("Tumbler").transform;var body=root.Find("Body").GetComponent<MeshFilter>();
            var r=new Report {materials=body.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name).ToArray(),
                parts=Parts(body.sharedMesh,body.transform,root).Where(p=>p.triangles>5).ToArray(),
                wheelCenters=root.GetComponentsInChildren<WheelCollider>().Select(w=>root.InverseTransformPoint(w.transform.position)).ToArray()};
            Directory.CreateDirectory("TestResults/NightSystems");File.WriteAllText("TestResults/NightSystems/parts.json",JsonUtility.ToJson(r,true));
            Debug.Log("NIGHT INSPECTION COMPLETE: "+r.parts.Length+" components");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
