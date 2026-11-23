using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Reefkeeper;
using Reefkeeper.Corals;
using Reefkeeper.Photos;

public static class CheckVinylTonearm
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static string Folder=>Path.GetFullPath("../artifacts/turntable");
    static string Pose(Transform t)=>t.localPosition.ToString("F4")+"|"+t.localRotation.ToString("F4")+"|"+t.localScale.ToString("F4");
    public static async Task<string> Main()
    {
        var p=UnityEngine.Object.FindFirstObjectByType<VinylPlayer>(FindObjectsInactive.Include);
        Need(p&&p.turntableModel&&p.tonearm&&p.placementRing,"Missing authored turntable");
        Directory.CreateDirectory(Folder);
        if(!Application.isPlaying)
        {
            File.WriteAllLines(Path.Combine(Folder,"authored-poses.txt"),new[]{Pose(p.transform),Pose(p.turntableModel)});
            Capture(p,"Turntable-empty.png");return "Saved authored poses and preview.";
        }
        var expected=File.ReadAllLines(Path.Combine(Folder,"authored-poses.txt"));
        Need(expected[0]==Pose(p.transform)&&expected[1]==Pose(p.turntableModel),"Runtime overwrote artist placement");
        var arm=p.tonearm;
        Need(arm.GetComponentsInChildren<Collider>(true).Length==0,"Former headshell grab collider remains");
        Need(arm.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>()==null,"Tonearm still competes for grabs");
        foreach(var d in p.recordRack.GetComponentsInChildren<VinylDisplay>(true))
            Need(Mathf.Abs(d.record.sharedMesh.bounds.size.z*Mathf.Abs(d.record.transform.lossyScale.z)-.007f)<.0001f,"Incorrect disc thickness");
        var item=p.recordRack.GetComponentInChildren<ShopRecord>(true);
        var home=item.transform.position;var rotation=item.transform.rotation;var oldClip=item.music;
        bool owned=(bool)typeof(ShopRecord).GetField("owned",F).GetValue(item);
        var sound=UnityEngine.Object.FindFirstObjectByType<ReefSoundscape>();var source=sound.MusicSource;
        var initialClip=source.clip;float originalVolume=source.volume;bool originalMute=source.mute;
        var track=AudioClip.Create("Temporary silent fade test",44100,1,44100,false);
        var second=AudioClip.Create("Temporary second track",44100,1,44100,false);
        ReefPrintedPhoto photo=null;
        Action place=()=>{item.transform.position=p.SeatPosition;typeof(VinylPlayer).GetMethod("Release",F).Invoke(p,new object[]{item});};
        Action lift=()=>typeof(VinylPlayer).GetMethod("LiftRecord",F).Invoke(p,new object[]{item});
        try
        {
            typeof(ShopRecord).GetField("owned",F).SetValue(item,true);item.music=track;
            place();Need(p.Seated==item&&!p.Playing&&arm.IsCueing,"Record must wait for automatic cueing");
            for(int i=0;i<15;i++)arm.StepMotion(1f/60);
            Need(!p.Playing,"Music started before needle lowered");
            lift();for(int i=0;i<90;i++)arm.StepMotion(1f/60);
            Need(!p.Playing&&!p.Seated&&p.placementRing.enabled&&Mathf.Abs(arm.CurrentAngle)<.01f,"Interrupted cue did not return safely");
            place();for(int i=0;i<75;i++)arm.StepMotion(1f/60);
            Need(p.Playing==item&&p.NeedleOverRecord(arm.StylusPosition),"Automatic cue failed to start playback");
            await Task.Delay(100);float initial=source.volume;
            await Task.Delay(400);float middle=source.volume;
            Need(source.clip==track&&initial<middle&&middle<sound.musicVolume,"Fade-in skipped or wrong album clip: start="+initial+" mid="+middle+" target="+sound.musicVolume+" clip="+source.clip+" playing="+p.Playing+" seated="+p.Seated+" delta="+Time.unscaledDeltaTime);
            await Task.Delay(1000);
            Need(p.Playing==item&&Mathf.Abs(source.volume-sound.musicVolume)<.002f,"Playback or fade-in did not settle across real frames");
            Capture(p,"Turntable-playing.png");
            lift();Need(!p.Playing&&!source.mute&&source.volume>0,"Removal cut music instead of fading");
            await Task.Delay(300);float leaving=source.volume;
            Need(leaving>0&&leaving<sound.musicVolume,"Fade-out not gradual");
            await Task.Delay(700);Need(source.mute&&!source.isPlaying&&source.volume<.001f,"Fade-out did not finish");
            item.transform.SetPositionAndRotation(home,rotation);
            // Switching a track during an unfinished fade must cancel the stale transition.
            sound.PlayMusic(track,.2f);await Task.Delay(100);
            sound.StopMusic(.5f);await Task.Delay(80);
            sound.PlayMusic(second,.15f);await Task.Delay(500);
            Need(source.clip==second&&!source.mute&&source.isPlaying,"Rapid track switch was stopped by a stale fade");
            sound.StopMusic(0);await Task.Delay(50);
            var texture=new Texture2D(8,8);texture.Apply();
            photo=ReefPrintedPhoto.Spawn(texture,Camera.main.transform,"placeholder text","ValidationOnly");
            await Task.Delay(650);
            var stock=photo.transform.Find("Visual/Paper");
            Need(Mathf.Abs(stock.GetComponent<MeshFilter>().sharedMesh.bounds.size.z*stock.lossyScale.z-.003f)<.00001f,"Photo lacks solid 3 mm thickness");
            Need(photo.transform.Find("Visual/Picture").localPosition.z<-.0015f&&photo.transform.Find("Visual/Caption").localPosition.z<-.0015f,"Image/caption is inside paper");
            var grab=photo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            Need(grab.colliders.Count==1&&grab.colliders[0]==photo.GetComponent<BoxCollider>(),"Photo grab volume includes visual colliders");
            return "PASS: 7 mm records, solid 3 mm photos, no tonearm grab target; seat -> automatic raise/swing/lower -> playback; interruption returns arm; real-frame fade in/out and rapid track-switch cancellation pass; per-album AudioClip used; authored placement preserved. No saves changed.";
        }
        finally
        {
            lift();item.music=oldClip;typeof(ShopRecord).GetField("owned",F).SetValue(item,owned);
            item.transform.SetPositionAndRotation(home,rotation);
            sound.StopMusic(0);await Task.Delay(50);source.clip=initialClip;source.volume=originalVolume;source.mute=originalMute;
            if(photo)UnityEngine.Object.DestroyImmediate(photo.gameObject);
            UnityEngine.Object.DestroyImmediate(track);UnityEngine.Object.DestroyImmediate(second);
        }
    }
    static void Capture(VinylPlayer p,string file)
    {
        var go=new GameObject("Temporary turntable preview camera");var camera=go.AddComponent<Camera>();
        if(Camera.main)camera.CopyFrom(Camera.main);camera.enabled=false;
        camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        var target=p.SeatPosition;var model=p.turntableModel;
        camera.transform.position=target+model.up*.7f+model.forward*.85f+model.right*.27f;
        camera.transform.LookAt(target);camera.fieldOfView=48;camera.nearClipPlane=.03f;
        var rt=RenderTexture.GetTemporary(1100,800,24);var old=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(1100,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,800),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Folder,file),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
