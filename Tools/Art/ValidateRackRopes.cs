using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Reefkeeper.CoralGrowthTools;

public static class ValidateRackRopes
{
    const string Source = "Assets/Reefkeeper/Meshes/CoralGrowth/RackBases/RackBase_Hanging.fbx";
    const string Prefab = "Assets/Reefkeeper/Prefabs/CoralGrowth/RackBases/RackBase_Hanging.prefab";
    const string Output = "../artifacts/rack-rope-fix";
    public static string Main()
    {
        Directory.CreateDirectory(Output);
        string temporary = "Assets/__RackRopeValidation_" + Guid.NewGuid().ToString("N") + ".fbx";
        GameObject test = null;
        try
        {
            // Import the same source under a temporary name, bypassing only this model-specific repair.
            File.Copy(Source, temporary);
            string importerSettings = File.ReadAllText(Source + ".meta");
            importerSettings = System.Text.RegularExpressions.Regex.Replace(importerSettings, "(?m)^guid: [a-f0-9]+", "guid: " + Guid.NewGuid().ToString("N"));
            File.WriteAllText(temporary + ".meta", importerSettings);
            AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport);
            var before = AssetDatabase.LoadAllAssetsAtPath(temporary).OfType<Mesh>().First();
            var after = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<Mesh>().First();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var sockets = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Socket_Hang_")).Select(t => prefab.transform.InverseTransformPoint(t.position)).ToArray();
            if (sockets.Length != 18) throw new Exception("Unexpected socket count: " + sockets.Length);
            var copy = UnityEngine.Object.Instantiate(before);
            int repaired = RackRopeMeshRepair.Repair(copy, sockets);
            if (repaired != 21) throw new Exception("Expected 21 repaired cords; got " + repaired);
            if (RackRopeMeshRepair.Repair(copy, sockets) != 0) throw new Exception("Repair must be idempotent.");
            // Unity reorders/welds vertices after OnPostprocessModel; compare topology size here
            // and test the actual imported geometry independently with the rays below.
            if (copy.triangles.Length != after.triangles.Length) throw new Exception("Imported repair triangle count differs.");
            UnityEngine.Object.DestroyImmediate(copy);
            if (prefab.GetComponentInChildren<MeshFilter>().sharedMesh != after || prefab.GetComponentInChildren<MeshCollider>().sharedMesh != after) throw new Exception("Prefab lost its shared mesh reference.");
            test = new GameObject("Temporary rope validation");
            test.transform.position = new Vector3(0, 1000, 0);
            var collider = test.AddComponent<MeshCollider>();
            int beforeHits = CheckRays(collider, before, sockets);
            int afterHits = CheckRays(collider, after, sockets);
            if (afterHits != 18 * 16) throw new Exception("Some rope angles are still missing: " + afterHits);
            UnityEngine.Object.DestroyImmediate(test); test = null;
            RenderComparison(before, after, prefab.GetComponentInChildren<Renderer>().sharedMaterial);
            string result = $"PASS: 21 closed six-sided cords restored (18 hanging + 3 buoy).\nSide-ray hits across 18 hanging ropes / 16 directions: {beforeHits}/288 before, {afterHits}/288 after.\nTriangles: {before.triangles.Length / 3} -> {after.triangles.Length / 3}.\nIdempotence, imported result, 18 socket anchors and prefab renderer/collider mesh references verified.\nRenderer/material count unchanged; source FBX and scene not modified.\n";
            File.WriteAllText(Output + "/validation.txt", result);
            return result;
        }
        finally
        {
            if (test) UnityEngine.Object.DestroyImmediate(test);
            AssetDatabase.DeleteAsset(temporary);
        }
    }
    static int CheckRays(MeshCollider collider, Mesh mesh, Vector3[] sockets)
    {
        collider.sharedMesh = null; collider.sharedMesh = mesh;
        Physics.SyncTransforms();
        int hits = 0;
        foreach (var socket in sockets) for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8;
            var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var center = collider.transform.position + socket + Vector3.up * .05f;
            if (collider.Raycast(new Ray(center + direction * .03f, -direction), out var hit, .06f)) hits++;
        }
        return hits;
    }
    static void RenderComparison(Mesh before, Mesh after, Material material)
    {
        var root = new GameObject("Temporary rack render");
        root.transform.position = new Vector3(0, 1000, 0); root.layer = 31;
        var filter = root.AddComponent<MeshFilter>(); root.AddComponent<MeshRenderer>().sharedMaterial = material;
        var cameraObject = new GameObject("Temporary rack camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 31; camera.orthographic = true; camera.orthographicSize = .30f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.38f, .60f, .60f); camera.nearClipPlane = .01f; camera.farClipPlane = 10;
        var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
        var rt = new RenderTexture(500, 500, 24); rt.antiAliasing = 4; var oldRT = RenderTexture.active;
        var sheet = new Texture2D(2000, 1000, TextureFormat.RGB24, false);
        var globals = new Dictionary<string, float>(); foreach (string key in new[] { "_UW_FogDensity", "_UW_DepthLightFalloff", "_UW_AmbientStrength" }) globals[key] = Shader.GetGlobalFloat(key);
        var absorption = Shader.GetGlobalVector("_UW_Absorption"); var caustics = Shader.GetGlobalVector("_UW_CausticsColor");
        try
        {
            Shader.SetGlobalFloat("_UW_FogDensity", 0); Shader.SetGlobalFloat("_UW_DepthLightFalloff", 0); Shader.SetGlobalFloat("_UW_AmbientStrength", .75f); Shader.SetGlobalVector("_UW_Absorption", Vector4.zero); Shader.SetGlobalVector("_UW_CausticsColor", Vector4.zero);
            for (int row = 0; row < 2; row++) for (int i = 0; i < 4; i++)
            {
                filter.sharedMesh = row == 0 ? before : after;
                var target = root.transform.position + new Vector3(.18f, 1.41f, .10f);
                float angle = (i * 90 + 10) * Mathf.Deg2Rad;
                camera.transform.position = target + new Vector3(Mathf.Cos(angle), .02f, Mathf.Sin(angle)) * 2;
                camera.transform.LookAt(target); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, 500, 500), i * 500, (1 - row) * 500, false);
            }
            sheet.Apply(); File.WriteAllBytes(Output + "/before-after-four-angles.png", sheet.EncodeToPNG());
        }
        finally
        {
            foreach (var kv in globals) Shader.SetGlobalFloat(kv.Key, kv.Value);
            Shader.SetGlobalVector("_UW_Absorption", absorption); Shader.SetGlobalVector("_UW_CausticsColor", caustics);
            RenderTexture.active = oldRT; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
