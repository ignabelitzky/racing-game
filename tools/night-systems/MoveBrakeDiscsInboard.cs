using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Moves existing brake assemblies onto the wheel faces that point toward the chassis.</summary>
public static class MoveBrakeDiscsInboard
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            var vehicle=GameObject.Find("Tumbler").transform;
            var thermal=new SerializedObject(vehicle.GetComponent<BrakeDiscThermal>());
            var wheels=thermal.FindProperty("wheelVisuals");var rotors=thermal.FindProperty("rotors");
            var calipers=thermal.FindProperty("calipers");var offsets=thermal.FindProperty("caliperOffsets");
            for(int i=0;i<wheels.arraySize;i++)
            {
                var wheel=(Transform)wheels.GetArrayElementAtIndex(i).objectReferenceValue;
                var rotor=((Renderer)rotors.GetArrayElementAtIndex(i).objectReferenceValue).transform;
                var caliper=(Transform)calipers.GetArrayElementAtIndex(i).objectReferenceValue;
                var mesh=wheel.GetComponent<MeshFilter>().sharedMesh;var center=mesh.bounds.center;
                var materials=wheel.GetComponent<Renderer>().sharedMaterials;
                int rim=Array.FindIndex(materials,m=>m.name=="Forged_Gunmetal");
                if(rim<0)throw new Exception("Missing rim on "+wheel.name);
                float side=Mathf.Sign(vehicle.InverseTransformPoint(wheel.TransformPoint(center)).x);
                var vertices=mesh.vertices;var axial=mesh.GetTriangles(rim).Select(j=>(vertices[j].x-center.x)*side).ToArray();
                float mount=-Mathf.Min(axial.Max(),mesh.bounds.extents.x)+.055f;
                rotor.localPosition=center+Vector3.right*(side*mount);
                var offset=offsets.GetArrayElementAtIndex(i).vector3Value;offset.x=side*(mount-.01f);
                offsets.GetArrayElementAtIndex(i).vector3Value=offset;
                caliper.localPosition=vehicle.InverseTransformPoint(wheel.TransformPoint(center))+offset;
                caliper.localScale=new Vector3(-side,1,1);
                EditorUtility.SetDirty(rotor);EditorUtility.SetDirty(caliper);
                Debug.Log("INBOARD BRAKE "+i+" wheel="+wheel.name+" mount="+mount+" rimInner="+axial.Min()+" rimOuter="+axial.Max());
            }
            thermal.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);EditorSceneManager.SaveScene(vehicle.gameObject.scene);
            Debug.Log("BRAKE ASSEMBLIES MOVED INBOARD");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
