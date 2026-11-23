// One-time scene migration; subsequent placement is authored using normal Transforms.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Reefkeeper.Corals;

public static class InstallEditableTurntable
{
    const string Assets="Assets/Reefkeeper/Resources/VinylShop/";
    static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
    static Quaternion Q(JToken p)=>new Quaternion((float)p[0],(float)p[1],(float)p[2],(float)p[3]);
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Edit mode required");
        var player=UnityEngine.Object.FindFirstObjectByType<VinylPlayer>(FindObjectsInactive.Include);
        if(player.turntableModel)throw new Exception("Already authored; edit the scene objects instead of rebuilding them");
        var saved=JObject.Parse(File.ReadAllText("../artifacts/turntable/previous-runtime-layout.json"))["model"];
        var group=new GameObject("Turntable - editable");
        Undo.RegisterCreatedObjectUndo(group,"Author editable turntable");
        group.transform.SetParent(player.transform.parent,true);
        group.transform.SetPositionAndRotation(V(saved["position"]),Q(saved["rotation"]));
        var model=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Shop/RecordPlayer"));
        PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        model.name="Turntable (music corner)";
        model.transform.SetParent(group.transform,false);model.transform.localScale=V(saved["scale"]);
        foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var t in model.GetComponentsInChildren<Transform>(true))if(t.name=="Record_Default")t.gameObject.SetActive(false);
        var platter=model.GetComponentsInChildren<MeshFilter>().First(t=>t.name=="Platter");
        // Measure in the model's plane, not the tilted world AABB: the latter floats above the platter.
        var b=platter.sharedMesh.bounds;var localBounds=new Bounds();bool first=true;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
        {
            var corner=b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
            var point=model.transform.InverseTransformPoint(platter.transform.TransformPoint(corner));
            if(first){localBounds=new Bounds(point,Vector3.zero);first=false;}else localBounds.Encapsulate(point);
        }
        var seat=new Vector3(localBounds.center.x,localBounds.max.y+.006f,localBounds.center.z);
        Undo.RecordObject(player.transform,"Author record placement target");
        player.transform.SetParent(group.transform,true);player.name="Record placement target";
        player.transform.localScale=Vector3.one;
        player.transform.SetPositionAndRotation(model.transform.TransformPoint(seat),model.transform.rotation);
        var disc=player.recordRack.GetComponentInChildren<VinylDisplay>(true).record;
        float diameter=disc.sharedMesh.bounds.size.x*Mathf.Abs(disc.transform.lossyScale.x);
        var slot=player.GetComponent<BoxCollider>();slot.isTrigger=true;slot.center=Vector3.zero;
        slot.size=new Vector3(diameter*.88f,.1f,diameter*.88f);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Assets+"PlacementRingMesh.asset");
        if(!mesh){mesh=Arcs();AssetDatabase.CreateAsset(mesh,Assets+"PlacementRingMesh.asset");}
        var material=AssetDatabase.LoadAssetAtPath<Material>(Assets+"PlacementRing.mat");
        if(!material)
        {
            material=new Material(Shader.Find("Reefkeeper/Vinyl Placement Arc")){name="PlacementRing"};
            material.SetColor("_BaseColor",new Color(.3f,.85f,1f,.7f));material.enableInstancing=true;
            AssetDatabase.CreateAsset(material,Assets+"PlacementRing.mat");
        }
        var ring=new GameObject("Placement light arcs");ring.transform.SetParent(player.transform,false);
        ring.transform.localScale=Vector3.one*diameter;ring.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=ring.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        player.placementRing=renderer;player.turntableModel=model.transform;
        player.tonearm=VinylTonearm.Install(model,player);
        EditorUtility.SetDirty(player);EditorUtility.SetDirty(slot);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(player.gameObject.scene);
        Selection.activeGameObject=player.gameObject;
        return "Authored turntable, automatic tonearm and three soft light arcs (192 triangles). Move Record placement target to move the ring and actual landing point together; parent Turntable - editable moves everything. Existing rack and records preserved.";
    }
    static Mesh Arcs()
    {
        const int arcs=3,segments=32;
        var vertices=new Vector3[arcs*(segments+1)*2];var uv=new Vector2[vertices.Length];
        var triangles=new int[arcs*segments*6];int index=0;
        for(int a=0;a<arcs;a++)for(int s=0;s<=segments;s++)
        {
            float t=(float)s/segments;float angle=(a*120+8+t*88)*Mathf.Deg2Rad;
            var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            int v=(a*(segments+1)+s)*2;
            vertices[v]=direction*.47f;vertices[v+1]=direction*.51f;
            uv[v]=new Vector2(t,0);uv[v+1]=new Vector2(t,1);
            if(s==segments)continue;
            triangles[index++]=v;triangles[index++]=v+1;triangles[index++]=v+2;
            triangles[index++]=v+2;triangles[index++]=v+1;triangles[index++]=v+3;
        }
        var mesh=new Mesh{name="Three soft placement arcs",vertices=vertices,uv=uv,triangles=triangles};
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
