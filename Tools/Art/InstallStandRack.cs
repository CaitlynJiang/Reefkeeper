// Run once in Edit Mode with Pipeline: script run --file ../Tools/Art/InstallStandRack.cs
// The resulting prefab and scene Transforms are the authoring interface; running the game never rebuilds them.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Reefkeeper.Corals;

public static class InstallStandRack
{
    const string Base="Assets/Reefkeeper/";
    const string MatDir=Base+"Materials/MusicCorner/StandRack";
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Install in Edit Mode");
        var shop=UnityEngine.Object.FindFirstObjectByType<CoralShop>();
        var player=shop.GetComponentInChildren<VinylPlayer>(true);
        if(player.recordRack)throw new Exception("An authored rack already exists; edit its Transform instead of rebuilding it");
        Directory.CreateDirectory(MatDir);AssetDatabase.Refresh();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Meshes/MusicCorner/MC_5_StandRack.fbx");
        if(!model)throw new Exception("Import the stand-rack FBX first");
        var rack=new GameObject("MC_5_StandRack");Undo.RegisterCreatedObjectUndo(rack,"Install editable record rack");
        var body=(GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(body,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        body.name="Rack model";body.transform.SetParent(rack.transform,false);
        foreach(var renderer in body.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(MaterialFor).ToArray();
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        var collider=rack.AddComponent<BoxCollider>();collider.center=new Vector3(0,.62f,.06f);collider.size=new Vector3(.39f,1.24f,.045f);
        var records=new GameObject("Records - edit individual Transforms");records.transform.SetParent(rack.transform,false);
        var variants=new[]{"Yellow","Orange","Teal"};var slots=new[]{"Slot_Bottom","Slot_Middle","Slot_Top"};
        var displays=shop.GetComponentsInChildren<VinylDisplay>(true);
        for(int i=0;i<variants.Length;i++)
        {
            var display=displays.First(d=>d.variant==variants[i]);
            var slot=body.GetComponentsInChildren<Transform>().First(t=>t.name==slots[i]);
            Undo.SetTransformParent(display.transform,records.transform,"Move record to stand rack");
            display.transform.localPosition=rack.transform.InverseTransformPoint(slot.position)+new Vector3(0,.045f,-.045f);
            display.transform.localRotation=Quaternion.Euler(-28f,0,0);
            display.transform.localScale=Vector3.one*1.62f;
            display.sleeve.transform.localScale=new Vector3(1,1,3);
            display.record.transform.localPosition=new Vector3(0,.060f,.012f);
            display.record.transform.localRotation=Quaternion.Euler(-8f,0,0);
            AddCovers(display);
        }
        string prefab=Base+"Resources/Shop/StandRack.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(rack,prefab,InteractionMode.AutomatedAction);
        var front=Vector3.ProjectOnPlane(shop.transform.forward,Vector3.up).normalized;
        var right=Vector3.Cross(Vector3.up,front);
        var position=shop.counterSpot.position+right*.95f+front*.55f;
        // Ground placement is only an initial authoring convenience; runtime never repeats this raycast.
        var hits=Physics.RaycastAll(position+Vector3.up*3,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
        var ground=hits.Where(h=>!h.collider.transform.IsChildOf(shop.transform)&&!h.collider.transform.IsChildOf(rack.transform)
            &&!h.collider.GetComponentInParent<CharacterController>()&&h.point.y<=shop.counterSpot.position.y-.25f)
            .OrderByDescending(h=>h.point.y).ToArray();
        position.y=ground.Length>0?ground[0].point.y:shop.transform.position.y;
        rack.transform.SetParent(shop.transform,true);
        rack.transform.SetPositionAndRotation(position,Quaternion.LookRotation(-front));
        rack.transform.localScale=Vector3.one*1.25f;
        Undo.RecordObject(player,"Assign editable record rack");player.recordRack=rack.transform;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rack.transform);
        EditorUtility.SetDirty(player);EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(shop.gameObject.scene);
        Selection.activeGameObject=rack;if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.FrameSelected();
        Capture(rack);
        return "Installed editable three-tier stand rack with Yellow/Orange/Teal playable albums and existing cover art. Prefab: "+prefab;
    }
    static Material MaterialFor(Material source)
    {
        if(!source)return source;
        if(source.name=="Outline")return AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/CatShop/Toon_Outline.mat");
        string path=MatDir+"/"+source.name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
        material=new Material(Shader.Find("Reefkeeper/UnderWater/Toon")){name=source.name};
        var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.color:Color.white;
        var texturePath=Base+"Textures/MusicCorner/StandRack/"+source.name+".png";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.maxTextureSize=1024;importer.isReadable=false;importer.mipmapEnabled=true;
            var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=1024;android.format=TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            material.SetTexture("_BaseMap",texture);color=Color.white;
        }
        material.SetColor("_BaseColor",color);if(material.HasProperty("_SwayAmount"))material.SetFloat("_SwayAmount",0);
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static void AddCovers(VinylDisplay display)
    {
        var texture=Resources.Load<Texture2D>("VinylShop/Covers/"+display.variant);
        if(!texture)throw new Exception("Missing existing album cover: "+display.variant);
        var path=MatDir+"/Album_"+display.variant+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)
        {
            material=new Material(Shader.Find("Reefkeeper/UnderWater/Toon")){name="Album "+display.variant};
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_Emission"))material.SetFloat("_Emission",.35f);
            AssetDatabase.CreateAsset(material,path);
        }
        var b=display.sleeve.sharedMesh.bounds;
        for(int side=0;side<2;side++)
        {
            string name=side==0?"Cover (front)":"Cover (back)";if(display.sleeve.transform.Find(name))continue;
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.name=name;
            go.transform.SetParent(display.sleeve.transform,false);go.transform.localPosition=new Vector3(b.center.x,b.center.y,side==0?b.min.z-.0006f:b.max.z+.0006f);
            go.transform.localRotation=side==0?Quaternion.identity:Quaternion.Euler(0,180,0);
            go.transform.localScale=new Vector3(b.size.x*.995f,b.size.y*.995f,1);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    static void Capture(GameObject rack)
    {
        var renderers=rack.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
        var go=new GameObject("Stand rack review camera");var camera=go.AddComponent<Camera>();
        if(Camera.main)camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=38;
        go.AddComponent<UniversalAdditionalCameraData>();
        camera.transform.position=b.center-rack.transform.forward*2.5f+rack.transform.right*.5f+Vector3.up*.12f;
        camera.transform.LookAt(b.center);camera.nearClipPlane=.03f;
        var rt=RenderTexture.GetTemporary(900,1000,24);var old=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(900,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,1000),0,0);image.Apply();
            File.WriteAllBytes(Path.GetFullPath("../artifacts/stand-rack/StandRack-in-editor.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
