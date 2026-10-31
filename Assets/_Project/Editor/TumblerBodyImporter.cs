using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>Builds the body without the replaced tank assemblies; keeps the full-resolution FBX as its source.</summary>
[ScriptedImporter(1, "tumblerbody")]
public sealed class TumblerBodyImporter : ScriptedImporter
{
    [Serializable]
    public sealed class Definition
    {
        public string sourceModelGuid, sourceMeshName, sourceFileSha256;
        public int materialSlot, sourceTriangleCount;
    }
    public static string HashFile(string path)
    {
        using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    public override void OnImportAsset(AssetImportContext context)
    {
        var definition=JsonUtility.FromJson<Definition>(File.ReadAllText(context.assetPath));
        string sourcePath=AssetDatabase.GUIDToAssetPath(definition.sourceModelGuid);
        context.DependsOnSourceAsset(sourcePath);
        context.DependsOnArtifact(sourcePath);
        if(HashFile(sourcePath)!=definition.sourceFileSha256)
            throw new InvalidDataException("The source Tumbler FBX changed. Re-author the tank replacement before importing this body.");
        var source=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>().Single(m=>m.name==definition.sourceMeshName);
        var indices=source.GetTriangles(definition.materialSlot);
        if(indices.Length/3!=definition.sourceTriangleCount)throw new InvalidDataException("Unexpected source body topology");
        if(definition.sourceTriangleCount<=0)throw new InvalidDataException("Empty source tank selection");
        var result=UnityEngine.Object.Instantiate(source);result.name="Tumbler_Body_Equipment_Replaced";
        result.SetTriangles(Array.Empty<int>(),definition.materialSlot,false);
        context.AddObjectToAsset("body",result);context.SetMainObject(result);
    }
}
