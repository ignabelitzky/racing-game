using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DrivingGroundVerify
{
 const string Flag="DrivingGroundVerification";
 static string ReportPath(string name) { var folder=Environment.GetEnvironmentVariable("DRIVING_GROUND_RESULTS") ?? "TestResults/DrivingGround";Directory.CreateDirectory(folder);return Path.Combine(folder,name); }
 static readonly List<string> Results=new List<string>();
 static int step;static double next;static Rigidbody body;static WheelCollider[] wheels;static float[] f,s;static WheelSurfaceGrip service;
 static DrivingGroundVerify(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Results.Add("PASS "+message);}
 public static void Run()
 {
  try
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
   var root=GameObject.Find("Driving Test Ground");Check(root!=null,"Sandbox loads the test-ground prefab");
   Check(root.GetComponentsInChildren<Transform>().Count(t=>t.parent==root.transform)==7,"Six zones plus access group");
   Check(root.GetComponentsInChildren<Rigidbody>().Length==6,"Six movable slalom markers");
   Check(root.GetComponentsInChildren<MeshCollider>().All(c=>c.sharedMesh!=null&&!c.isTrigger),"Driveable mesh colliders present");
   Check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts in new prefab");
   Physics.SyncTransforms();
   for(int i=0;i<7;i++) { var bump=root.GetComponentsInChildren<Collider>().Single(c=>c.name=="Progressive speed bump "+(i+1)); Check(bump.bounds.size.y<.4f && bump.bounds.size.y>.05f && bump.bounds.size.x>11,"Speed bump orientation and height "+(i+1)); Ray(82,191+i*8,"Progressive speed bump "+(i+1),.14f+i*.03f); }
   Ray(-130,207,"Launch_1",.64f);Ray(-105,209,"Launch_2",1.34f);Ray(-80,211,"Launch_3",2.14f);
   Ray(-130,213,"Asphalt access",.04f);Ray(-105,219,"Landing_2",1.47f);Ray(-80,225,"Landing_3",2.23f);
   Ray(-130,333,"Asphalt grip lane",.06f);Ray(-80,333,"Wet low-grip lane",.06f);Ray(-105,333,"Gravel grip lane",null);
   Ray(20,331,"Hill climb and descent",5.24f);Ray(-20,331,"Cross slope 12 degrees",7*Mathf.Tan(12*Mathf.Deg2Rad)+.04f);Ray(137,210,"Narrow balance bridge",1.44f);
   var colliders=root.GetComponentsInChildren<Collider>();
   Check(!colliders.Any(c=>c.bounds.Intersects(new Bounds(new Vector3(0,1,40),new Vector3(4,2,7)))),"Spawn remains clear of new colliders");
   Check(!colliders.Any(c=>c.bounds.Intersects(new Bounds(new Vector3(0,1,0),new Vector3(4,2,7)))),"Reset position remains clear");
   File.WriteAllLines(ReportPath("edit-validation.txt"),Results);Results.Clear();SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
  }catch(Exception ex){Fail(ex);}
 }
 static void Ray(float x,float z,string expected,float? height)
 {
  Check(Physics.Raycast(new Vector3(x,30,z),Vector3.down,out RaycastHit hit,35),"Surface contact at "+x+","+z);
  Check(hit.collider.name==expected,"Contact collider "+expected);
  Check(hit.normal.y>.9f,"Upward normal on "+expected);
  if(height.HasValue)Check(Mathf.Abs(hit.point.y-height.Value)<.04f,"Expected surface height on "+expected);
 }
 static void State(PlayModeStateChange state)
 {
  if(!SessionState.GetBool(Flag,false))return;
  if(state==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+.8;}
 }
 static void Move(float x,float z)
 {
  body.position=new Vector3(x,1.2f,z);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();next=EditorApplication.timeSinceStartup+1.5;
 }
 static void Grip(float forward,float side,string label,bool handbrake=false)
 {
  Check(wheels.All(w=>w.GetGroundHit(out var hit)&&hit.collider!=null),label+": all six wheels contact ground");
  for(int i=0;i<wheels.Length;i++)
  {
   Check(Mathf.Abs(wheels[i].forwardFriction.stiffness-f[i]*forward)<.001f,label+": forward stiffness "+i);
   Check(Mathf.Abs(wheels[i].sidewaysFriction.stiffness-s[i]*side)<.001f,label+": sideways stiffness "+i);
  }
  if(!handbrake)Check(wheels.All(w=>Mathf.Abs(w.sidewaysFriction.extremumSlip-.2f)<.001f),label+": original slip logic retained");
 }
 static void Tick()
 {
  if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
  try
  {
   switch(step++)
   {
    case 0:
     body=GameObject.Find("Tumbler").GetComponent<Rigidbody>();wheels=body.GetComponentsInChildren<WheelCollider>();service=GameObject.Find("Driving Test Ground").GetComponent<WheelSurfaceGrip>();
     Check(wheels.Length==6,"Original six vehicle wheels present");f=wheels.Select(w=>w.forwardFriction.stiffness).ToArray();s=wheels.Select(w=>w.sidewaysFriction.stiffness).ToArray();Check(f.All(v=>v>0)&&s.All(v=>v>0),"Original tire stiffness positive");
     body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;Move(-80,333);break;
    case 1:Grip(.55f,.35f,"Wet lane");Move(-130,333);break;
    case 2:Grip(1,1,"Asphalt lane");Move(-105,333);break;
    case 3:Grip(.72f,.58f,"Gravel lane");Move(-190,333);break;
    case 4:Grip(1,1,"Unmarked terrain restores baseline");Move(-80,333);break;
    case 5:Grip(.55f,.35f,"Wet reentry");service.enabled=false;next=EditorApplication.timeSinceStartup+.3;break;
    case 6:Grip(1,1,"Disable restores baseline");service.enabled=true;next=EditorApplication.timeSinceStartup+.3;break;
    case 7:Grip(.55f,.35f,"Reenable reapplies profile");var controller=body.GetComponent<TumblerController>();controller.enabled=false;controller.SendMessage("ApplyHandbrake");service.SendMessage("FixedUpdate");next=EditorApplication.timeSinceStartup+.2;break;
    case 8:
     Grip(.55f,.35f,"Handbrake on wet",true);Check(wheels.Count(w=>Mathf.Abs(w.sidewaysFriction.extremumSlip-.5f)<.001f)==4,"Handbrake retains rear-wheel slip change");body.GetComponent<TumblerController>().enabled=true;next=EditorApplication.timeSinceStartup+.3;break;
    case 9:
     Grip(.55f,.35f,"Handbrake release");File.WriteAllLines(ReportPath("play-validation.txt"),Results);SessionState.SetBool(Flag,false);Debug.Log("DRIVING GROUND NATIVE VALIDATION PASS "+Results.Count+" runtime checks");EditorApplication.Exit(0);break;
   }
  }catch(Exception ex){Fail(ex);}
 }
 static void Fail(Exception ex){SessionState.SetBool(Flag,false);File.WriteAllLines(ReportPath("failed.txt"),Results.Concat(new[]{ex.ToString()}));Debug.LogException(ex);EditorApplication.Exit(1);}
}
