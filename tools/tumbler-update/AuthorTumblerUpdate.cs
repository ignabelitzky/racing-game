using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AuthorTumblerUpdate
{
    const string Art = "Assets/_Project/Art/TumblerVisuals/";
    static Material[] finishes;
    static TMP_FontAsset font;
    static Color white = new Color(.95f, .96f, .95f);
    static Color muted = new Color(.55f, .61f, .62f);
    static Color amber = new Color(.88f, .69f, .36f);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var rubber = new Material(Shader.Find("HDRP/Lit")) { name = "Underbody Rubber" };
            rubber.SetColor("_BaseColor", new Color(.028f, .033f, .036f)); rubber.SetFloat("_Smoothness", .22f);
            rubber = Store(rubber, Art + "Materials/Underbody_Rubber.mat");
            finishes = new[] {
                AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Chassis_Graphite.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Cast_Alloy.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Zinc_Hardware.mat"), rubber };
            var vehicle = GameObject.Find("Tumbler");
            var previousSystems = vehicle.transform.Find("Underbody Outer Panels and Front Subframe (Visual Only)");
            if (previousSystems != null) UnityEngine.Object.DestroyImmediate(previousSystems.gameObject);
            var previousHUD = UnityEngine.Object.FindFirstObjectByType<DigitalVehicleHUD>();
            if (previousHUD != null) UnityEngine.Object.DestroyImmediate(previousHUD.gameObject);
            var systems = Underbody();
            var systemAsset = PrefabUtility.SaveAsPrefabAsset(systems, "Assets/_Project/Prefabs/TumblerUnderbodySystems.prefab");
            UnityEngine.Object.DestroyImmediate(systems);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(systemAsset, vehicle.transform);
            instance.name = "Underbody Outer Panels and Front Subframe (Visual Only)";
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            foreach (string name in new[] { "Speedometer", "DriveModeText", "GearNumText" })
            { var old = GameObject.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old); }
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var hud = HUD();
            var hudAsset = PrefabUtility.SaveAsPrefabAsset(hud, "Assets/_Project/Prefabs/DigitalVehicleHUD.prefab");
            UnityEngine.Object.DestroyImmediate(hud);
            var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudAsset, canvas.transform);
            hudInstance.name = "Digital Vehicle HUD";
            var ui = UnityEngine.Object.FindFirstObjectByType<UIController>();
            var serializedUI = new SerializedObject(ui);
            serializedUI.FindProperty("tumblerController").objectReferenceValue = vehicle.GetComponent<TumblerController>();
            serializedUI.FindProperty("hud").objectReferenceValue = hudInstance.GetComponent<DigitalVehicleHUD>();
            serializedUI.ApplyModifiedPropertiesWithoutUndo();
            var car = new SerializedObject(vehicle.GetComponent<TumblerController>());
            car.FindProperty("forwardPower").floatValue = 1400;
            car.FindProperty("maxSpeedKPH").floatValue = 260;
            car.FindProperty("numberOfGears").intValue = 7;
            car.FindProperty("highSpeedSteeringAllowance").floatValue = 4;
            car.ApplyModifiedPropertiesWithoutUndo();
            var audio = new SerializedObject(vehicle.GetComponent<EngineAudio>());
            audio.FindProperty("runningPitch").floatValue = .65f; audio.FindProperty("runningMaxPitch").floatValue = 1.8f;
            audio.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(vehicle.scene);
            EditorSceneManager.SaveScene(vehicle.scene);
            Directory.CreateDirectory("TestResults/TumblerUpdate");
            File.WriteAllText("TestResults/TumblerUpdate/authoring.txt", "Native HUD prefab: 300 x 176; 20 RPM segments. Original vehicle mesh assets and physics colliders retained.\n" +
                string.Join("\n", systemAsset.GetComponentsInChildren<MeshFilter>().Select(m => m.name + ": " + m.sharedMesh.vertexCount + " vertices / " + m.sharedMesh.triangles.Length / 3 + " triangles")));
            Debug.Log("TUMBLER UPDATE AUTHORING COMPLETE"); EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static GameObject HUD()
    {
        var root = new GameObject("Digital Vehicle HUD", typeof(RectTransform), typeof(DigitalVehicleHUD)); root.layer = 5;
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0); rect.sizeDelta = new Vector2(300, 176); rect.anchoredPosition = new Vector2(-24, 24);
        Image(root.transform, "Panel Shadow", -3, -3, 306, 182, new Color(0, 0, 0, .25f));
        Image(root.transform, "Panel", 0, 0, 300, 176, new Color(.027f, .036f, .041f, .98f));
        Image(root.transform, "Upper Rule", 0, 175, 300, 1, new Color(.4f, .46f, .46f, .7f));
        Image(root.transform, "Accent", 18, 152, 3, 9, amber);
        Text(root.transform, "Identity", "TUMBLER", 27, 148, 145, 18, 11, muted, FontStyles.Bold, TextAlignmentOptions.Left, 3);
        Image(root.transform, "Drive Chip", 231, 146, 51, 20, new Color(.24f, .21f, .15f, .8f));
        var drive = Text(root.transform, "DriveModeText", "AWD", 231, 146, 51, 20, 12, amber, FontStyles.Bold, TextAlignmentOptions.Center, 1);
        var speed = Text(root.transform, "SpeedValue", "000", 15, 64, 188, 84, 76, white, FontStyles.Bold, TextAlignmentOptions.Left, -2);
        Text(root.transform, "Speed Units", "KM/H", 197, 76, 46, 18, 10, muted, FontStyles.Bold, TextAlignmentOptions.Left, 1);
        Image(root.transform, "Gear Divider", 244, 78, 1, 50, new Color(.22f, .27f, .28f));
        Text(root.transform, "Gear Label", "GEAR", 251, 118, 34, 14, 10, muted, FontStyles.Bold, TextAlignmentOptions.Center, 1);
        var gear = Text(root.transform, "GearNumText", "1", 250, 75, 36, 45, 34, white, FontStyles.Bold, TextAlignmentOptions.Center);
        Image(root.transform, "Lower Divider", 18, 63, 264, 1, new Color(.15f, .20f, .21f));
        Text(root.transform, "Engine Label", "ENGINE", 18, 35, 90, 20, 10, muted, FontStyles.Bold, TextAlignmentOptions.Left, 2);
        var rpm = Text(root.transform, "RPMValue", "900", 160, 32, 87, 24, 19, white, FontStyles.Bold, TextAlignmentOptions.Right);
        Text(root.transform, "RPM Units", "RPM", 253, 34, 30, 20, 9, muted, FontStyles.Bold, TextAlignmentOptions.Right);
        var segments = new Image[20];
        for (int i = 0; i < segments.Length; i++) segments[i] = Image(root.transform, "RPM Segment " + (i + 1), 18 + 13.3f * i, 18, 10.8f, 8, new Color(.16f, .19f, .20f));
        var so = new SerializedObject(root.GetComponent<DigitalVehicleHUD>());
        so.FindProperty("speedText").objectReferenceValue = speed; so.FindProperty("rpmText").objectReferenceValue = rpm;
        so.FindProperty("gearText").objectReferenceValue = gear; so.FindProperty("driveText").objectReferenceValue = drive;
        var list = so.FindProperty("rpmSegments"); list.arraySize = segments.Length;
        for (int i = 0; i < segments.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    static Image Image(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.layer = 5;
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float width, float height,
        float size, Color color, FontStyles style, TextAlignmentOptions alignment, float spacing = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.layer = 5;
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height);
        var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.text = value; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = alignment; t.characterSpacing = spacing; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static GameObject Underbody()
    {
        var root = new GameObject("Tumbler Underbody Systems");
        var pans = new Parts();
        foreach (float side in new[] { -1f, 1f })
        {
            pans.Box(new Vector3(side * 1.27f, -.729f, -.15f), new Vector3(.60f, .028f, 2.62f), 0);
            pans.Box(new Vector3(side * 1.55f, -.765f, -.15f), new Vector3(.065f, .07f, 2.67f), 0);
            for (int bay = 0; bay < 3; bay++)
            {
                float z = -1.05f + bay * .88f;
                pans.Box(new Vector3(side * 1.26f, -.75f, z), new Vector3(.45f, .026f, .65f), 1);
                for (int bead = 0; bead < 4; bead++) pans.Box(new Vector3(side * (1.11f + bead * .10f), -.776f, z), new Vector3(.024f, .028f, .49f), 0);
                foreach (float dz in new[] { -.31f, .31f }) foreach (float dx in new[] { -.22f, .22f })
                    pans.Cylinder(new Vector3(side * 1.26f + dx, -.76f, z + dz), new Vector3(side * 1.26f + dx, -.79f, z + dz), .025f, 2, 6);
            }
            for (int i = 0; i < 8; i++) pans.Cylinder(new Vector3(side * 1.55f, -.80f, -1.35f + i * .34f), new Vector3(side * 1.55f, -.815f, -1.35f + i * .34f), .023f, 2, 6);
        }
        pans.Save(root.transform, "06_Outer_Stampings_And_Sill_Rails");
        var front = new Parts();
        front.Box(new Vector3(0, -.73f, 1.85f), new Vector3(1.23f, .03f, 1.39f), 0);
        foreach (float side in new[] { -1f, 1f })
        {
            front.Beam(new Vector3(side * 1.47f, -.79f, 1.02f), new Vector3(side * .48f, -.79f, 1.77f), .10f, .07f, 0);
            front.Beam(new Vector3(side * .51f, -.78f, 1.23f), new Vector3(side * .51f, -.78f, 2.51f), .07f, .07f, 0);
            for (int i = 0; i < 5; i++) front.Box(new Vector3(side * .32f, -.762f, 1.36f + i * .23f), new Vector3(.43f, .022f, .07f), 3);
            for (int i = 0; i < 6; i++) front.Cylinder(new Vector3(side * (.36f + i * .038f), -.81f, 2.26f),
                new Vector3(side * (.38f + i * .038f), -.81f, 2.26f), .07f, 3, 12);
            front.Cylinder(new Vector3(side * .6f, -.81f, 2.26f), new Vector3(side * .77f, -.78f, 2.15f), .018f, 2, 10);
        }
        front.Cylinder(new Vector3(-.35f, -.81f, 2.26f), new Vector3(.35f, -.81f, 2.26f), .055f, 1, 16);
        front.Box(new Vector3(0, -.745f, 2.70f), new Vector3(.91f, .028f, .15f), 0);
        foreach (float side in new[] { -1f, 1f })
        {
            front.Beam(new Vector3(side*.43f,-.77f,2.55f),new Vector3(0,-.77f,2.75f),.035f,.025f,1);
            front.Cylinder(new Vector3(side*.34f,-.76f,2.70f),new Vector3(side*.34f,-.787f,2.70f),.025f,2,6);
        }
        front.Box(new Vector3(0, -.79f, 1.42f), new Vector3(.53f, .10f, .52f), 1);
        for (int i = 0; i < 7; i++) front.Box(new Vector3(-.23f + i * .077f, -.852f, 1.42f), new Vector3(.025f, .025f, .43f), 1);
        foreach (float x in new[] { -.53f, .53f }) foreach (float z in new[] { 1.22f, 1.8f, 2.46f })
            front.Cylinder(new Vector3(x, -.77f, z), new Vector3(x, -.795f, z), .026f, 2, 6);
        front.Save(root.transform, "07_Front_Subframe_Rack_And_Sump");
        var lines = new Parts();
        foreach (float side in new[] { -1f, 1f })
        {
            for (int p = 0; p < 3; p++) lines.Cylinder(new Vector3(side * (1.0f + p * .045f), -.80f, -1.39f),
                new Vector3(side * (1.0f + p * .045f), -.80f, 1.05f), p == 0 ? .018f : .011f, p == 0 ? 3 : 2, 8);
            foreach (float z in new[] { -1.22f, -.55f, .12f, .80f })
            {
                lines.Box(new Vector3(side * 1.045f, -.819f, z), new Vector3(.17f, .035f, .046f), 0);
                lines.Cylinder(new Vector3(side * 1.14f, -.77f, z), new Vector3(side * 1.14f, -.815f, z), .02f, 2, 6);
            }
            lines.Cylinder(new Vector3(side * 1.33f, -.76f, -.4f), new Vector3(side * 1.33f, -.82f, -.4f), .095f, 3, 16);
            lines.Cylinder(new Vector3(side * 1.33f, -.82f, -.4f), new Vector3(side * 1.33f, -.83f, -.4f), .056f, 1, 12);
        }
        lines.Save(root.transform, "08_Brake_Lines_Clips_And_Body_Mounts");
        return root;
    }

    sealed class Parts
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        public void Box(Vector3 center, Vector3 size, int material, Quaternion? rotation = null)
        {
            Quaternion q = rotation ?? Quaternion.identity; Vector3 h = size * .5f;
            Vector3[] v = { new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z) };
            int[][] faces = { new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{0,1,5,4},new[]{3,7,6,2} };
            foreach (var face in faces)
            {
                int n = vertices.Count; foreach (int i in face) vertices.Add(center + q * v[i]);
                indices[material].AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
            }
        }
        public void Beam(Vector3 a, Vector3 b, float width, float depth, int material)
            => Box((a+b)*.5f,new Vector3(width,depth,(b-a).magnitude),material,Quaternion.LookRotation(b-a,Vector3.up));
        public void Cylinder(Vector3 a, Vector3 b, float radius, int material, int sides)
        {
            var rotation = Quaternion.FromToRotation(Vector3.up, (b-a).normalized);
            for (int i=0;i<sides;i++)
            {
                float t=(float)i/sides*Mathf.PI*2, u=(float)(i+1)/sides*Mathf.PI*2;
                Vector3 r=rotation*new Vector3(Mathf.Cos(t)*radius,0,Mathf.Sin(t)*radius),s=rotation*new Vector3(Mathf.Cos(u)*radius,0,Mathf.Sin(u)*radius);
                int n=vertices.Count; vertices.AddRange(new[]{a+r,b+r,b+s,a+s,a,a+s,a+r,b,b+r,b+s});
                indices[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+4,n+6,n+5,n+7,n+9,n+8});
            }
        }
        public void Save(Transform parent,string name)
        {
            var mesh = new Mesh { name=name }; mesh.SetVertices(vertices); mesh.subMeshCount=4;
            for(int i=0;i<4;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh=Store(mesh,Art+"Meshes/"+name+".asset");
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=finishes;
        }
    }
    static T Store<T>(T value, string path) where T : UnityEngine.Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(value); return existing;
    }
}
