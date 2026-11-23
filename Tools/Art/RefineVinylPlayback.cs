using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Reefkeeper.Corals;
public static class RefineVinylPlayback
{
    static void Thicken(GameObject root)
    {
        foreach(var d in root.GetComponentsInChildren<VinylDisplay>(true))
        {
            var t=d.record.transform;float old=d.record.sharedMesh.bounds.size.z*Mathf.Abs(t.lossyScale.z);
            if(old>.00001f){var scale=t.localScale;scale.z*=.007f/old;t.localScale=scale;}
        }
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Edit mode required");
        const string path="Assets/Reefkeeper/Resources/Shop/StandRack.prefab";
        var prefab=PrefabUtility.LoadPrefabContents(path);
        try{Thicken(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var p=UnityEngine.Object.FindFirstObjectByType<VinylPlayer>(FindObjectsInactive.Include);
        Thicken(p.recordRack.gameObject);
        foreach(var d in p.recordRack.GetComponentsInChildren<VinylDisplay>(true))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(d.record.transform);
            EditorUtility.SetDirty(d.record.transform);
        }
        p.musicFadeIn=1.2f;p.musicFadeOut=.8f;
        p.tonearm=VinylTonearm.Install(p.turntableModel.gameObject,p);p.tonearm.name="Tonearm - automatic";
        p.tonearm.animationSeconds=.9f;p.tonearm.playAngle=35;
        EditorUtility.SetDirty(p.tonearm);EditorUtility.SetDirty(p);
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(p.gameObject.scene);EditorSceneManager.SaveScene(p.gameObject.scene);
        return "Records thickened to 7 mm in the scene; automatic tonearm has no grab collider; fade in/out set to 1.2/0.8 seconds. Existing transforms and rack layout preserved.";
    }
}
