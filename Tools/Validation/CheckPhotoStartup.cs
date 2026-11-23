using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Reefkeeper;
using Reefkeeper.Corals;
using Reefkeeper.Photos;

public static class CheckPhotoStartup
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type T=typeof(ReefPhotoMode);
    static object Get(ReefPhotoMode p,string name)=>T.GetField(name,F).GetValue(p);
    static void Set(ReefPhotoMode p,string name,object value)=>T.GetField(name,F).SetValue(p,value);
    static void Call(ReefPhotoMode p,string name,params object[] args)=>T.GetMethod(name,F).Invoke(p,args);
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static bool Hidden(ReefPhotoMode p,string name)
    {
        var root=(Transform)Get(p,name);
        var group=(CanvasGroup)Get(p,name+"Group");
        return !root || !root.gameObject.activeSelf && group.alpha==0;
    }
    static void SeedVisible(ReefPhotoMode p)
    {
        Set(p,"<Active>k__BackingField",true);Set(p,"owning",true);
        Set(p,"flash",1f);Set(p,"toastUntil",Time.unscaledTime+10f);
        foreach(var name in new[]{"finder","hint"})
        {
            ((Transform)Get(p,name)).gameObject.SetActive(true);
            ((CanvasGroup)Get(p,name+"Group")).alpha=1;
        }
    }
    public static string Main()
    {
        var mode=ReefPhotoMode.Instance;
        var opening=UnityEngine.Object.FindFirstObjectByType<OpeningDiveSequence>();
        var main=Camera.main;
        Need(mode&&opening&&main,"Run in the immersive-opening scene in Play Mode");
        var stateField=typeof(OpeningDiveSequence).GetField("<State>k__BackingField",F);
        var oldState=opening.State;bool modeEnabled=mode.enabled,openingEnabled=opening.enabled,unlocked=ReefPhotoMode.Unlocked;
        var position=main.transform.position;var rotation=main.transform.rotation;
        var cameras=new List<GameObject>();
        Dictionary<string,Texture2D> album=null;Texture2D catPhoto=null;bool removedPhoto=false;
        mode.enabled=false;opening.enabled=false;
        try
        {
            Call(mode,"HideImmediately");
            Transform previous=null;
            // First construction and camera/rig replacement must both start invisible.
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Photo startup validation camera "+i);cameras.Add(go);
                var camera=go.AddComponent<Camera>();camera.enabled=false;
                Set(mode,"view",camera);Call(mode,"BuildUI");
                Need(Hidden(mode,"finder")&&Hidden(mode,"hint"),"New photo UI starts visible");
                Need(!previous||!previous.gameObject.activeSelf,"Old camera UI remains visible until deferred Destroy");
                Call(mode,"UpdateUI",camera.transform,1f/60f,false);
                Need(Hidden(mode,"finder")&&Hidden(mode,"hint"),"Closed viewfinder flashes on its first UI update");
                previous=(Transform)Get(mode,"finder");previous.gameObject.SetActive(true);
            }
            ReefPhotoMode.Unlocked=true;
            foreach(var phase in new[]{OpeningDiveSequence.Phase.Preparing,OpeningDiveSequence.Phase.Seated,
                OpeningDiveSequence.Phase.FadingOut,OpeningDiveSequence.Phase.FadingIn})
            {
                stateField.SetValue(opening,phase);SeedVisible(mode);Call(mode,"Update");
                Need(Hidden(mode,"finder")&&Hidden(mode,"hint")&&!mode.Active&&!ReefPhotoMode.OwnsHands&&mode.NearbySpecies==null,
                    "Photo state escaped opening gate in "+phase);
            }
            stateField.SetValue(opening,OpeningDiveSequence.Phase.Swimming);
            ReefPhotoMode.Unlocked=false;SeedVisible(mode);Call(mode,"Update");
            Need(Hidden(mode,"finder")&&Hidden(mode,"hint")&&!mode.Active&&!ReefPhotoMode.OwnsHands,"Locked camera displays stale photo UI");

            // Temporarily make the real cat a new subject in memory only. No photo is stored or deleted on disk.
            ReefPhotoAlbum.Has(LuckyCatProgress.CatSpecies);
            album=(Dictionary<string,Texture2D>)typeof(ReefPhotoAlbum).GetField("photos",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            removedPhoto=album.TryGetValue(LuckyCatProgress.CatSpecies,out catPhoto);
            if(removedPhoto)album.Remove(LuckyCatProgress.CatSpecies);
            var cat=UnityEngine.Object.FindFirstObjectByType<LuckyCat>();Need(cat,"Missing real cat target");
            var renderer=cat.GetComponentInChildren<Renderer>();Need(renderer,"Missing cat renderer");
            var center=renderer.bounds.center;
            main.transform.position=center+Vector3.back*.65f;main.transform.LookAt(center);
            ReefPhotoMode.Unlocked=true;Call(mode,"Update");
            Need(mode.NearbySpecies!=null,"Unlocked underwater camera lost species detection");
            Need(((Transform)Get(mode,"hint")).gameObject.activeSelf&&((CanvasGroup)Get(mode,"hintGroup")).alpha>0,
                "Unlocked species hint did not appear");
            Call(mode,"Open");Call(mode,"UpdateUI",main.transform,1f/60f,false);
            Need(mode.Active&&((Transform)Get(mode,"finder")).gameObject.activeSelf&&((CanvasGroup)Get(mode,"finderGroup")).alpha>0,
                "Requested viewfinder no longer appears");
            Call(mode,"Close","validation");
            for(int frame=0;frame<30;frame++)Call(mode,"UpdateUI",main.transform,1f/60f,false);
            Need(Hidden(mode,"finder"),"Normal viewfinder close no longer finishes");
            // Legacy reef scenes without an opening sequence retain photography.
            Set(mode,"opening",null);Call(mode,"Update");Need(mode.NearbySpecies!=null,"No-opening scene lost photography");
            return "PASS: initial and replacement-camera UI are invisible; all four opening phases suppress stale UI/flash/input; locked camera stays hidden; unlocked underwater subject hint and viewfinder work; normal close fades out; scenes without an opening still work. No save files changed.";
        }
        finally
        {
            if(removedPhoto&&album!=null)album[LuckyCatProgress.CatSpecies]=catPhoto;
            Call(mode,"HideImmediately");Set(mode,"opening",opening);
            ReefPhotoMode.Unlocked=unlocked;stateField.SetValue(opening,oldState);
            main.transform.SetPositionAndRotation(position,rotation);
            foreach(var go in cameras)UnityEngine.Object.Destroy(go);
            opening.enabled=openingEnabled;mode.enabled=modeEnabled;
        }
    }
}
