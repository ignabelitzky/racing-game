using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.HighDefinition;
using TMPro;

[InitializeOnLoad]
public static class NightSystemsVerify
{
    const string Flag="NightSystemsVerify";
    const string Folder="TestResults/NightSystems";
    static readonly List<string> results=new List<string>();
    static int tick;
    static TumblerController car;
    static TumblerSystems systems;
    static Boost boost;
    static BrakeDiscThermal brakes;
    static Rigidbody body;
    static DigitalVehicleHUD hud;
    static FieldInfo Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    static T Get<T>(object o,string n)=>(T)Field(o,n).GetValue(o);
    static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static void Check(bool valid,string label){if(!valid)throw new Exception(label);results.Add("PASS "+label);}
    static NightSystemsVerify(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode){tick=0;Time.captureDeltaTime=.02f;}};EditorApplication.update+=Tick;}
    static void Find()
    {
        car=GameObject.Find("Tumbler").GetComponent<TumblerController>();body=car.GetComponent<Rigidbody>();systems=car.GetComponent<TumblerSystems>();boost=car.GetComponent<Boost>();brakes=car.GetComponent<BrakeDiscThermal>();hud=UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>();
    }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/failed.txt");EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");Find();
            var equipment=car.transform.Find("Lighting and Auxiliary Equipment");
            Check(equipment!=null&&equipment.GetComponentsInChildren<Light>(true).Length==8,"Eight real HDRP headlights on the original optical locations");
            Check(equipment.GetComponentsInChildren<Light>(true).All(l=>l.type==LightType.Spot&&l.GetComponent<HDAdditionalLightData>().volumetricDimmer>0),"All eight spot beams have volumetric scattering");
            Check(equipment.GetComponentsInChildren<Renderer>().Count(r=>r.name=="Bronze Fuel Filler Cap")==2,"Both original top fuel caps have bronze overlays");
            Check(equipment.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Interior Boost Canister"))==2,"Two interior turbo tanks detailed separately from the shocks");
            var wheels=Get<Transform[]>(brakes,"wheelVisuals");
            Check(wheels.Length==6&&wheels.All(w=>w.Find("Ventilated Brake Disc")!=null),"Six ventilated rotors parented to the six wheel visuals");
            Check(Get<Transform[]>(brakes,"calipers").Length==6,"Six calipers follow suspension and steering without spinning");
            Check(equipment.GetComponentsInChildren<Collider>().Length==0,"Added equipment introduces no vehicle collision geometry");
            var original=car.GetComponentsInChildren<MeshFilter>().Where(m=>AssetDatabase.GetAssetPath(m.sharedMesh)=="Assets/Prefabs/tumbler_v7.fbx").ToArray();
            Check(original.Length==6,"All six original wheel geometries retained");
            EquipmentGeometry();
            foreach(var w in wheels)
                Check(w.GetComponent<Renderer>().sharedMaterials.Any(m=>m.name=="Molded_Tire_Rubber")&&w.GetComponent<Renderer>().sharedMaterials.Any(m=>m.name=="Forged_Gunmetal"),"Textured tire and forged rim: "+w.name);
            Clearance();
            Check(hud.GetComponent<RectTransform>().sizeDelta==new Vector2(300,240),"Compact extended HUD remains bottom-right at 300 by 240 reference pixels");
            foreach(var mode in new[]{TumblerSystems.LightMode.Off,TumblerSystems.LightMode.Low,TumblerSystems.LightMode.High})
            {
                hud.Refresh(260,7200,7200,7,false,TumblerController.driveType.AllWheelDrive);hud.RefreshSystems(.58f,false,true,true,mode,true,true);
                foreach(var t in hud.GetComponentsInChildren<TextMeshProUGUI>()){t.ForceMeshUpdate();Check(t.preferredWidth<=t.rectTransform.rect.width+1,"HUD text fits "+mode+": "+t.name);}
            }
            File.WriteAllLines(Folder+"/edit.txt",results);results.Clear();SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void Clearance()
    {
        var detail=car.GetComponentsInChildren<MeshFilter>().Single(m=>m.sharedMesh.name=="07_Front_Subframe_Rack_And_Sump");
        var wheels=Get<Transform[]>(brakes,"wheelVisuals");int collisions=0;float clearance=float.PositiveInfinity;
        for(int i=0;i<2;i++)
        {
            var mesh=wheels[i].GetComponent<MeshFilter>().sharedMesh;var bounds=mesh.bounds;var center=car.transform.InverseTransformPoint(wheels[i].TransformPoint(bounds.center));
            float radius=Mathf.Max(bounds.extents.y,bounds.extents.z),halfWidth=bounds.extents.x;
            for(float angle=-40;angle<=40;angle+=5)for(float travel=-.15f;travel<=.15f;travel+=.05f)
                foreach(var v in detail.sharedMesh.vertices)
                {
                    var p=Quaternion.Euler(0,-angle,0)*(car.transform.InverseTransformPoint(detail.transform.TransformPoint(v))-center-Vector3.up*travel);
                    float axial=Mathf.Abs(p.x)-halfWidth,radial=new Vector2(p.y,p.z).magnitude-radius;
                    clearance=Mathf.Min(clearance,Mathf.Max(axial,radial));if(axial<.035f&&radial<.035f)collisions++;
                }
        }
        Check(collisions==0,"Front detail clears both tire envelopes at +/-40 degrees steering and +/-15 cm suspension travel; min separation="+clearance.ToString("F3")+" m");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||tick++<4)return;
        try
        {
            Find();Time.captureDeltaTime=.02f;Physics.simulationMode=SimulationMode.Script;
            // Stop normal callbacks; the checks feed the same production callbacks explicitly.
            foreach(var script in car.GetComponents<MonoBehaviour>())script.enabled=false;
            car.tumblerInput.Enable();boost.tumblerInput.Enable();Get<TumblerInput>(systems,"input").Enable();
            Controls();Reserve();Thermal();
            File.WriteAllLines(Folder+"/play.txt",results);SessionState.SetBool(Flag,false);Time.captureDeltaTime=0;Physics.simulationMode=SimulationMode.FixedUpdate;
            Debug.Log("NIGHT SYSTEMS VALIDATION PASS "+results.Count+" runtime checks");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    static void Controls()
    {
        var previous=InputSystem.settings;var restored=UnityEngine.Object.Instantiate(previous);var settings=UnityEngine.Object.Instantiate(previous);InputSystem.settings=settings;
        settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>();var pad=(Gamepad)InputSystem.AddDevice("XInputController");
        foreach(var input in new[]{car.tumblerInput,boost.tumblerInput,Get<TumblerInput>(systems,"input")})input.asset.devices=new InputDevice[]{keyboard,pad};
        Action read=()=>{Call(systems,"Update");Call(car,"Update");Call(boost,"Update");};InputSystem.onAfterUpdate+=read;
        Action<Key[]> keys=state=>{InputSystem.QueueStateEvent(keyboard,new KeyboardState(state));InputSystem.Update();};
        Action<GamepadState> gamepad=state=>{InputSystem.QueueStateEvent(pad,state);InputSystem.Update();};
        try
        {
            keys(new Key[0]);gamepad(new GamepadState());
            for(int device=0;device<2;device++)
            {
                systems.SetLightMode(TumblerSystems.LightMode.Off);
                for(int step=1;step<=3;step++)
                {
                    if(device==0)keys(new[]{Key.L});else gamepad(new GamepadState().WithButton(GamepadButton.DpadUp));
                    Check((int)systems.EffectiveLights==step%3,(device==0?"Keyboard L":"Xbox D-pad Up")+" cycles light state "+step);
                    var mode=systems.EffectiveLights;InputSystem.Update();Check(systems.EffectiveLights==mode,"Held light button does not repeat");
                    Check(Get<Light[]>(systems,"headlights").Count(l=>l.enabled)==(step==1?4:step==2?8:0),"Actual enabled lamps match "+mode);
                    if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
                }
                systems.SetLightMode(TumblerSystems.LightMode.High);
                if(device==0)keys(new[]{Key.V});else gamepad(new GamepadState().WithButton(GamepadButton.RightStick));
                Check(systems.IsStealth&&systems.EffectiveLights==TumblerSystems.LightMode.Off,"Stealth toggle from "+(device==0?"keyboard V":"Xbox R3"));
                Check(car.GetComponentsInChildren<Light>(true).All(l=>!l.enabled),"Stealth disables every vehicle light");
                Check(Get<ParticleSystem[]>(systems,"turbineParticles").All(p=>!p.isPlaying&&p.particleCount==0),"Stealth clears turbine flame and embers immediately");
                var engine=car.GetComponent<EngineAudio>();
                Check(new[]{"startingSound","runningSound","idleSound"}.All(n=>Get<AudioSource>(engine,n).mute),"Stealth silences all engine layers");
                if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
                if(device==0)keys(new[]{Key.L});else gamepad(new GamepadState().WithButton(GamepadButton.DpadUp));
                Check(systems.SelectedLights==TumblerSystems.LightMode.High,"Stealth preserves previous headlight preset");
                if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
                Set(boost,"reserve",0f);Set(boost,"releaseRequired",true);
                if(device==0)keys(new[]{Key.LeftShift});else gamepad(new GamepadState().WithButton(GamepadButton.South));
                Check(!systems.IsStealth&&systems.EffectiveLights==TumblerSystems.LightMode.High,"Fresh boost press exits stealth even with empty reserve");
                Check(!boost.IsBoostActive&&!boost.Particles.isPlaying,"Empty-reserve stealth exit restores ordinary effects without starting boost flame");
                Check(!Get<AudioSource>(engine,"runningSound").mute,"Engine audio restored after stealth");
                if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
                systems.SetStealth(true);
                if(device==0)keys(new[]{Key.V});else gamepad(new GamepadState().WithButton(GamepadButton.RightStick));
                Check(!systems.IsStealth,"Second stealth click restores lights and engine");
                if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
                if(device==0)keys(new[]{Key.LeftCtrl});else gamepad(new GamepadState().WithButton(GamepadButton.East));
                Call(car,"ApplyBrakesAndHandbrake");Check(Get<WheelCollider[]>(brakes,"wheels").All(w=>w.brakeTorque==1000),"Service brake input reaches all six wheels: "+device);
                if(device==0)keys(new[]{Key.Space});else gamepad(new GamepadState().WithButton(GamepadButton.West));
                Call(car,"ApplyBrakesAndHandbrake");var ws=Get<WheelCollider[]>(brakes,"wheels");
                Check(ws.Take(2).All(w=>w.brakeTorque==0)&&ws.Skip(2).All(w=>w.brakeTorque==4000),"Handbrake input remains rear-only: "+device);
                if(device==0)keys(new Key[0]);else gamepad(new GamepadState());
            }
        }
        finally
        {
            InputSystem.onAfterUpdate-=read;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);
            InputSystem.settings=previous!=null?previous:restored;
            if(InputSystem.settings!=restored)UnityEngine.Object.DestroyImmediate(restored);
            if(settings!=null)UnityEngine.Object.DestroyImmediate(settings);
        }
    }
    static void Reserve()
    {
        Action reset=()=>{Set(boost,"reserve",1f);Set(boost,"delayRemaining",0f);Set(boost,"releaseRequired",false);};
        reset();for(int i=0;i<60;i++)Call(boost,"AdvanceReserve",.1f,true,false);
        Check(boost.Reserve01<.00001f&&!boost.IsBoostActive,"Six seconds of turbo exhausts the reserve and stops thrust");
        Call(boost,"AdvanceReserve",2.5f,false,false);Check(boost.Reserve01<.00001f,"Recharge waits the full 2.5 second cooldown");
        Call(boost,"AdvanceReserve",6f,false,false);Check(Mathf.Abs(boost.Reserve01-.5f)<.0001f,"Ground recharge takes twelve seconds from empty to full");
        Set(boost,"reserve",0f);Call(boost,"AdvanceReserve",2.4f,false,true);Check(Mathf.Abs(boost.Reserve01-.5f)<.0001f,"Airborne recharge is 2.5 times faster");
        Call(boost,"AdvanceReserve",100f,false,true);Check(boost.Reserve01==1,"Reserve clamps at full");
        reset();Call(boost,"AdvanceReserve",6.1f,true,false);Call(boost,"AdvanceReserve",20f,true,false);
        Check(!boost.IsBoostActive&&boost.Reserve01==1,"Holding an exhausted boost cannot cause thrust/recharge chatter");
        Call(boost,"AdvanceReserve",.02f,false,false);Call(boost,"AdvanceReserve",.02f,true,false);Check(boost.IsBoostActive,"Releasing and pressing boost re-arms it after exhaustion");
        reset();Call(boost,"AdvanceReserve",3f,true,false);Call(boost,"AdvanceReserve",3.5f,false,false);float coarse=boost.Reserve01;
        reset();for(int i=0;i<300;i++)Call(boost,"AdvanceReserve",.01f,true,false);for(int i=0;i<350;i++)Call(boost,"AdvanceReserve",.01f,false,false);
        Check(Mathf.Abs(boost.Reserve01-coarse)<.0001f,"Turbo drain and delayed refill are independent of frame subdivision");Call(boost,"AdvanceReserve",.02f,false,false);
    }
    static void Thermal()
    {
        var wheels=Get<WheelCollider[]>(brakes,"wheels");var rotors=Get<Renderer[]>(brakes,"rotors");
        Action clear=()=>Set(brakes,"heat",new float[6]);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary brake contact fixture";floor.transform.position=new Vector3(0,99,0);floor.transform.localScale=new Vector3(100,2,100);
        body.position=new Vector3(0,101.2f,0);body.rotation=Quaternion.identity;body.linearVelocity=body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
        Set(car,"currentBrakeForce",0f);Set(car,"isHandbrakeActive",false);Call(car,"ApplyBrakesAndHandbrake");
        for(int n=0;n<100;n++)Physics.Simulate(.02f);
        Check(wheels.All(w=>w.isGrounded),"Native brake fixture supports all six actual WheelColliders");
        clear();body.linearVelocity=Vector3.forward*20;Set(car,"currentBrakeForce",1000f);Call(car,"ApplyBrakesAndHandbrake");
        for(int n=0;n<400;n++)Call(brakes,"Update");
        Check(Enumerable.Range(0,6).All(i=>brakes.GetHeat(i)>.9f),"Production thermal Update reads movement, wheel contact and applied service torque for all six rotors (dt="+Time.deltaTime+", heat="+brakes.GetHeat(0)+")");
        body.linearVelocity=Vector3.zero;clear();for(int n=0;n<150;n++)Call(brakes,"Update");
        Check(Enumerable.Range(0,6).All(i=>brakes.GetHeat(i)==0),"Production thermal Update keeps stationary braked discs cold");
        Action<int> heatWithAppliedTorque=steps=>{for(int n=0;n<steps;n++)for(int i=0;i<6;i++)Call(brakes,"AdvanceHeat",i,.02f,Mathf.Clamp(wheels[i].brakeTorque/1000f,0,1.5f),20f);Call(brakes,"ApplyAppearance");};
        clear();Set(car,"currentBrakeForce",1000f);Set(car,"isHandbrakeActive",false);Call(car,"ApplyBrakesAndHandbrake");heatWithAppliedTorque(250);
        Check(Enumerable.Range(0,6).All(i=>brakes.GetHeat(i)>.9f),"Actual service-brake torque heats all six discs, including the front pair");
        var block=new MaterialPropertyBlock();rotors[0].GetPropertyBlock(block,0);Check(block.GetColor("_EmissiveColor").maxColorComponent>20,"Front discs visibly incandesce under prolonged service braking");
        systems.SetStealth(true);Call(brakes,"ApplyAppearance");rotors[0].GetPropertyBlock(block,0);
        Check(block.GetColor("_EmissiveColor").maxColorComponent==0&&brakes.GetHeat(0)>.9f,"Stealth conceals incandescence without erasing retained heat");systems.SetStealth(false);Call(brakes,"ApplyAppearance");
        clear();Set(car,"currentBrakeForce",0f);Set(car,"isHandbrakeActive",true);Call(car,"ApplyBrakesAndHandbrake");heatWithAppliedTorque(250);
        Check(brakes.GetHeat(0)==0&&brakes.GetHeat(1)==0&&Enumerable.Range(2,4).All(i=>brakes.GetHeat(i)>.9f),"Handbrake heats only the four rear discs");
        float before=brakes.GetHeat(2);for(int n=0;n<100;n++)Call(brakes,"AdvanceHeat",2,.02f,0f,0f);Check(brakes.GetHeat(2)<before,"Released discs cool gradually");
        clear();for(int n=0;n<100;n++)Call(brakes,"AdvanceHeat",0,.02f,1f,0f);Check(brakes.GetHeat(0)==0,"Stationary braking does not generate heat");
        var visuals=Get<Transform[]>(brakes,"wheelVisuals");var calipers=Get<Transform[]>(brakes,"calipers");wheels[0].steerAngle=40;Call(brakes,"LateUpdate");var angle=calipers[0].rotation;
        visuals[0].Rotate(Vector3.right,150,Space.Self);Call(brakes,"LateUpdate");
        Check(Quaternion.Angle(angle,calipers[0].rotation)<.01f&&Quaternion.Angle(angle,car.transform.rotation*Quaternion.Euler(0,40,0))<.01f,"Caliper follows 40 degree steering and remains fixed through wheel rotation");
        hud.RefreshSystems(.5f,false,true,true,TumblerSystems.LightMode.High,true,true);
        Check(Get<TextMeshProUGUI>(hud,"lightStateText").text=="HIGH"&&Get<TextMeshProUGUI>(hud,"handbrakeStateText").text=="ON"&&Get<TextMeshProUGUI>(hud,"stealthStateText").text=="ON"&&Get<RectTransform>(hud,"boostFill").anchorMax.x==.5f,"HUD binds reserve, high beams, handbrake and stealth states");
    }
    static void EquipmentGeometry()
    {
        var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Prefabs/tumbler_v7.fbx").OfType<Mesh>().OrderByDescending(m=>m.vertexCount).First();
        var filter=car.transform.Find("Body").GetComponent<MeshFilter>();var replacement=filter.sharedMesh;
        Check(source.vertices.SequenceEqual(replacement.vertices)&&source.normals.SequenceEqual(replacement.normals)&&source.uv.SequenceEqual(replacement.uv),"Body vertex positions, normals and UVs unchanged");
        for(int sm=0;sm<5;sm++)Check(source.GetTriangles(sm).SequenceEqual(replacement.GetTriangles(sm)),"Original body submesh "+sm+" triangles unchanged, including turbine and shock geometry");
        Check(source.GetTriangles(5).Length-replacement.GetTriangles(5).Length==5316*3,"Exactly the two old tank assemblies and their valves/collars removed from the active body mesh");
        var parts=NightInspect.Parts(replacement,filter.transform,car.transform);
        Check(!parts.Any(p=>p.material==5&&p.triangles>=890&&p.triangles<=910&&p.center.z>-1.6f&&p.center.z<-1.3f&&p.size.y>.65f),"No old tank shell remains beneath the new equipment");
        var tanks=car.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Interior Boost Canister")).OrderBy(t=>t.localPosition.x).ToArray();
        Check(tanks.Length==2&&Mathf.Abs(tanks[0].localPosition.x+.302f)<.002f&&Mathf.Abs(tanks[1].localPosition.x-.320f)<.002f&&tanks.All(t=>Mathf.Abs(t.localPosition.z+1.552f)<.002f),"New tanks occupy the original interior bays with a 9.5 cm rearward / 3.5 cm lower clearance adjustment");
        var obstacle=new GameObject("Temporary equipment-clearance mesh");var surface=obstacle.AddComponent<MeshCollider>();surface.sharedMesh=replacement;
        obstacle.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);obstacle.transform.localScale=filter.transform.lossyScale;
        foreach(var t in tanks)
        {
            var shell=t.GetComponentInChildren<MeshFilter>();var g=new NightGeometry(1);
            g.LatheY(Vector3.zero,new[]{new Vector2(-.355f,.01f),new Vector2(-.340f,.08f),new Vector2(-.310f,.16f),new Vector2(-.270f,.187f),new Vector2(.220f,.187f),new Vector2(.270f,.16f),new Vector2(.310f,.08f),new Vector2(.355f,.065f)},0,24);
            var proxy=g.Mesh("Conservative tank clearance hull");var collider=shell.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=proxy;collider.convex=true;
            bool hit=Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,surface,surface.transform.position,surface.transform.rotation,out var direction,out float depth);
            Check(!hit||depth<.002f,"Tank shell clears the remaining body/turbine/shock mesh: "+t.name+" penetration="+(hit?depth:0).ToString("F4"));
            var wheel=Get<Transform[]>(brakes,"wheelVisuals")[t.localPosition.x<0?5:3].GetComponent<Renderer>().bounds;
            var envelope=shell.GetComponent<Renderer>().bounds;float gap=t.localPosition.x<0?envelope.min.x-wheel.max.x:wheel.min.x-envelope.max.x;
            Check(gap>.005f,"Tank shell has at least 5 mm separation from the inner rear tire: "+t.name+" gap="+gap.ToString("F4"));
            UnityEngine.Object.DestroyImmediate(collider);UnityEngine.Object.DestroyImmediate(proxy);
            var hose=t.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Protected pressure line");
            var line=hose.gameObject.AddComponent<MeshCollider>();line.sharedMesh=hose.sharedMesh;line.convex=true;
            hit=Physics.ComputePenetration(line,line.transform.position,line.transform.rotation,surface,surface.transform.position,surface.transform.rotation,out direction,out depth);
            Check(!hit||depth<.002f,"Pressure line clears the remaining body/turbine/shock mesh: "+t.name+" penetration="+(hit?depth:0).ToString("F4"));
            UnityEngine.Object.DestroyImmediate(line);
        }
        var visuals=Get<Transform[]>(brakes,"wheelVisuals");var rotors=Get<Renderer[]>(brakes,"rotors");
        for(int i=0;i<6;i++)
        {
            var wheel=visuals[i].GetComponent<MeshFilter>().sharedMesh;var mats=visuals[i].GetComponent<Renderer>().sharedMaterials;int slot=Array.FindIndex(mats,m=>m.name=="Forged_Gunmetal");
            var vertices=wheel.vertices;float rimRadius=wheel.GetTriangles(slot).Select(j=>new Vector2(vertices[j].y,vertices[j].z).magnitude).Max();
            var rotor=rotors[i].GetComponent<MeshFilter>().sharedMesh;float radius=rotor.vertices.Max(p=>new Vector2(p.y,p.z).magnitude);
            Check(radius<rimRadius*.71f,"Sport rotor "+i+" fits within 71 percent of the original rim radius");
            int intersections=0;
            foreach(float angle in i<2?new[]{-40f,0f,40f}:new[]{0f})foreach(float travel in new[]{-.15f,0f,.15f})
            {
                var rotation=car.transform.rotation*Quaternion.Euler(0,angle,0)*Quaternion.Inverse(car.transform.rotation)*visuals[i].rotation;
                var center=visuals[i].position+car.transform.up*travel+rotation*rotors[i].transform.localPosition;
                var axle=rotation*Vector3.right;
                foreach(float band in new[]{.45f,.70f,.93f})for(int j=0;j<32;j++)
                {
                    float a=j*Mathf.PI*2/32;var p=center+rotation*new Vector3(0,Mathf.Cos(a)*radius*band,Mathf.Sin(a)*radius*band);
                    if(surface.Raycast(new Ray(p-axle*.014f,axle),out var hit,.028f)||surface.Raycast(new Ray(p+axle*.014f,-axle),out hit,.028f))intersections++;
                }
            }
            Check(intersections==0,"Sport rotor "+i+" friction ring clears body surfaces across tested steering and suspension poses; hits="+intersections);
        }
        UnityEngine.Object.DestroyImmediate(obstacle);
    }
    static void Fail(Exception e)
    {SessionState.SetBool(Flag,false);Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"/failed.txt",results.Concat(new[]{e.ToString()}));Debug.LogException(e);EditorApplication.Exit(1);}
}
