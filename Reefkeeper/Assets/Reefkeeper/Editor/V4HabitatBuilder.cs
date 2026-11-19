using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Reefkeeper.Editor
{
    public static class V4HabitatBuilder
    {
        public const string Scene="Assets/Reefkeeper/Scenes/ShallowReef_V4.unity";
        [MenuItem("Reefkeeper/V4/Open habitat prototype")]
        public static void Open(){if(!Application.isPlaying)EditorSceneManager.OpenScene(Scene);}
        [MenuItem("Reefkeeper/V4/Rebuild prototype from V3")]
        public static void Build()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            var scene=EditorSceneManager.OpenScene("Assets/Reefkeeper/Scenes/ShallowReef_V3.unity");
            var ship=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name.StartsWith("Shipwreck"));
            var rackSlots=ship.GetComponentsInChildren<RackSocket>().OrderBy(s=>s.name).Take(5).ToArray();
            if(rackSlots.Length!=5)throw new System.InvalidOperationException("The shipwreck must have five authored sockets.");
            var root=new GameObject("V4 Shipwreck Habitat");var habitat=root.AddComponent<ShipwreckHabitat>();
            habitat.species=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Reefkeeper/Prefabs/CoralGrowth"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<CoralGrowth>()).Where(c=>c).OrderBy(c=>c.name).ToArray();
            habitat.slots=rackSlots.Select(s=>s.transform).ToArray();habitat.visitors=new[]{"Fish1","Fish3"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reefkeeper/Generated/AnimatedFish/"+n+".prefab")).ToArray();
            Directory.CreateDirectory("Assets/Reefkeeper/Generated/V4");
            const string matPath="Assets/Reefkeeper/Generated/V4/HabitatSpot.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.SetColor("_BaseColor",new Color(.37f,.88f,.75f));mat.SetColor("_EmissionColor",new Color(.08f,.25f,.19f));mat.EnableKeyword("_EMISSION");
            for(int i=0;i<rackSlots.Length;i++)
            {
                var socket=rackSlots[i];if(socket.moss)Object.DestroyImmediate(socket.moss.gameObject);
                var spot=socket.gameObject.AddComponent<HabitatSpot>();spot.index=i;
                var surface=GameObject.CreatePrimitive(PrimitiveType.Cylinder);surface.name="Habitat pad "+(i+1);surface.transform.SetParent(socket.transform,false);
                surface.transform.localPosition=new Vector3(0,.025f,0);
                var scale=socket.transform.lossyScale;surface.transform.localScale=new Vector3(.85f/scale.x,.025f/scale.y,.85f/scale.z);
                surface.GetComponent<Renderer>().sharedMaterial=mat;
                // An invisible selection volume remains reachable after placing the displayed coral.
                var box=socket.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.center=new Vector3(0,.3f/scale.y,0);box.size=new Vector3(.9f/scale.x,.7f/scale.y,.9f/scale.z);
                Object.DestroyImmediate(socket);
            }
            var center=new GameObject("Habitat fish orbit").transform;center.SetParent(root.transform);center.position=(habitat.slots[0].position+habitat.slots[1].position+habitat.slots[2].position)/3+Vector3.up;
            habitat.visitorCenter=center;
            var workshop=Object.FindFirstObjectByType<ReefWorkshop>();
            workshop.desktopPlayer.transform.position=new Vector3(2.3f,1.4f,6.4f);workshop.desktopPlayer.transform.rotation=Quaternion.identity;
            var cam=workshop.desktopPlayer.GetComponentInChildren<Camera>(true);cam.transform.localRotation=Quaternion.Euler(16,0,0);
            EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();CaptureVisitors(habitat.visitors);
        }
        static void CaptureVisitors(GameObject[] prefabs)
        {
            const string folder="Assets/Reefkeeper/Resources/DiveCards/Marine";Directory.CreateDirectory(folder);
            var cameraObject=new GameObject("Marine photo camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var lightObject=new GameObject("Marine photo key light");var key=lightObject.AddComponent<Light>();key.type=LightType.Directional;key.color=Color.white;key.intensity=2;key.cullingMask=1<<31;
            lightObject.transform.rotation=Quaternion.Euler(35,110,0);var oldSun=RenderSettings.sun;RenderSettings.sun=key;
            try
            {
                for(int i=0;i<prefabs.Length;i++)
                {
                    var fish=Object.Instantiate(prefabs[i]);fish.transform.position=new Vector3(0,1000,0);
                    try
                    {
                        foreach(var t in fish.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                        foreach(var a in fish.GetComponentsInChildren<Animator>())a.enabled=false;
                        var renderers=fish.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                        camera.transform.position=bounds.center+new Vector3(-2,.35f,-.35f);camera.transform.LookAt(bounds.center);camera.orthographicSize=bounds.size.magnitude*.60f;
                        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                        var photo=new Texture2D(512,512,TextureFormat.RGBA32,false);photo.ReadPixels(new Rect(0,0,512,512),0,0);photo.Apply();File.WriteAllBytes(folder+"/Visitor"+i+".png",photo.EncodeToPNG());Object.DestroyImmediate(photo);
                    }
                    finally{Object.DestroyImmediate(fish);}
                }
                File.WriteAllText(folder+"/README.txt","Visitor0: Generated/AnimatedFish/Fish1.prefab\nVisitor1: Generated/AnimatedFish/Fish3.prefab\nUnity captures of existing models and materials; no generated species art.\nVisitor names are descriptive prototype labels. Recipes are fictional game rules.\n");
            }
            finally{RenderSettings.sun=oldSun;Object.DestroyImmediate(lightObject);camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(cameraObject);}
            AssetDatabase.Refresh();
        }
    }
}
