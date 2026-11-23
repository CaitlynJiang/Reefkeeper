using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Reefkeeper.Corals;

public static class CheckStandRack
{
    [Serializable] public class Pose { public string name;public Vector3 position,scale;public Quaternion rotation; }
    [Serializable] public class Snapshot { public Pose[] poses; }
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static string PathOf(string file)=>Path.GetFullPath("../artifacts/stand-rack/"+file);
    static Pose Read(Transform t,string name)=>new Pose{name=name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale};
    static string Line(Pose p)=>p.name+"|"+p.position.ToString("F4")+"|"+p.rotation.ToString("F4")+"|"+p.scale.ToString("F4");
    static Pose[] ReadAll(VinylPlayer player)
    {
        var list=new System.Collections.Generic.List<Pose>{Read(player.recordRack,"rack")};
        foreach(var d in player.recordRack.GetComponentsInChildren<VinylDisplay>(true))
        {list.Add(Read(d.transform,d.variant));list.Add(Read(d.sleeve.transform,d.variant+" sleeve"));list.Add(Read(d.record.transform,d.variant+" disc"));}
        return list.ToArray();
    }
    public static async Task<string> Main()
    {
        var player=UnityEngine.Object.FindFirstObjectByType<VinylPlayer>(FindObjectsInactive.Include);
        Need(player&&player.recordRack,"Missing authored rack");
        if(!Application.isPlaying)
        {
            File.WriteAllLines(PathOf("authored-poses.txt"),ReadAll(player).Select(Line));
            Capture(player.recordRack);return "Saved editor-authored pose baseline and in-engine preview.";
        }
        var shop=UnityEngine.Object.FindFirstObjectByType<CoralShop>();Need(shop.Progress,"Shop progression missing");
        Need(shop.Progress.VinylShopOpen,"Vinyl shop must be open immediately");
        Need(!shop.Progress.CameraOwned&&!Reefkeeper.Photos.ReefPhotoMode.Unlocked,"Use a fresh session before taking the camera");
        Need(player.recordRack.gameObject.activeInHierarchy&&player.gameObject.activeInHierarchy,"Rack/player hidden at startup");
        var turntables=player.transform.parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Turntable (music corner)").ToArray();
        Need(turntables.Length==1&&turntables[0].gameObject.activeInHierarchy,"Expected one visible turntable at startup");
        var displays=player.recordRack.GetComponentsInChildren<VinylDisplay>(true);Need(displays.Length==3,"Expected three playable record displays");
        Need(!player.recordRack.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Sleeve_Rack")),"Original placeholder sleeves remain");
        Need(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.name=="RecordCrate"),"Old runtime crate was still spawned");
        await Task.Yield();
        var expected=File.ReadAllLines(PathOf("authored-poses.txt")).ToDictionary(line=>line.Split('|')[0]);
        foreach(var pose in ReadAll(player))
        {
            Need(expected[pose.name]==Line(pose),
                "Runtime overrode authored Transform: "+pose.name);
        }
        foreach(var d in displays)
        {
            Need(d.gameObject.activeInHierarchy,"Record hidden at startup: "+d.variant);
            Need(d.sleeve.transform.childCount==2,"Cover art duplicated or missing");
            var item=d.record.GetComponent<ShopRecord>();Need(item&&item.GetComponent<BoxCollider>()&&item.GetComponent<Rigidbody>(),"Record lost purchase/grab components");
            var key=(string)typeof(ShopRecord).GetField("unlockKey",F).GetValue(item);Need(key.EndsWith("Reefkeeper.Vinyl."+d.variant),"Purchase identity changed");
            var home=(Vector3)typeof(ShopRecord).GetField("home",F).GetValue(item);
            var rotation=(Quaternion)typeof(ShopRecord).GetField("homeRotation",F).GetValue(item);
            bool owned=(bool)typeof(ShopRecord).GetField("owned",F).GetValue(item);
            Need(owned==(PlayerPrefs.GetInt(key)==1),"Startup granted a record instead of preserving purchase ownership");
            Need(ShopRecord.Price==50,"Record price changed");
            try
            {
                typeof(ShopRecord).GetField("owned",F).SetValue(item,true);
                item.transform.position=player.transform.TransformPoint(player.Slot.center);
                typeof(VinylPlayer).GetMethod("Release",F).Invoke(player,new object[]{item});
                Need(player.Seated==item&&!player.Playing,"Record must seat without automatically playing");
                item.transform.position=home+Vector3.up*5;
                typeof(VinylPlayer).GetMethod("Release",F).Invoke(player,new object[]{item});
                Need(Vector3.Distance(item.transform.position,home)<.0001f&&Quaternion.Angle(item.transform.rotation,rotation)<.01f,"Record did not return to authored outward-tilted pose");
            }
            finally{typeof(ShopRecord).GetField("owned",F).SetValue(item,owned);item.transform.SetPositionAndRotation(home,rotation);}
        }
        return "PASS: rack, three albums and turntable visible before receiving camera; no old crate/duplicate turntable; authored transforms preserved; purchase ownership and 50-scallop price unchanged; all records seat silently and return to their edited poses. No purchase/save data changed.";
    }
    static void Capture(Transform rack)
    {
        var renderers=rack.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
        var go=new GameObject("Rack review camera");var camera=go.AddComponent<Camera>();if(Camera.main)camera.CopyFrom(Camera.main);
        camera.enabled=false;camera.fieldOfView=38;go.AddComponent<UniversalAdditionalCameraData>();
        camera.transform.position=b.center-rack.forward*2.7f+rack.right*.48f+Vector3.up*.12f;camera.transform.LookAt(b.center);camera.nearClipPlane=.03f;
        var rt=RenderTexture.GetTemporary(900,1000,24);var old=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(900,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,1000),0,0);image.Apply();
            File.WriteAllBytes(PathOf("StandRack-in-editor.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
