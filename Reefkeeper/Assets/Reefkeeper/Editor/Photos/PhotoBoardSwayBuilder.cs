using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Reefkeeper.Photos;
namespace Reefkeeper.PhotoTools
{
    public static class PhotoBoardSwayBuilder
    {
        const string PrefabPath="Assets/Reefkeeper/Resources/Photos/PhotoBoard.prefab";
        public static string Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var sway=root.GetComponent<ReefPhotoWallSway>()??root.AddComponent<ReefPhotoWallSway>();
                if(sway.vines.Length==6)return "Six independent vines already installed.";
                foreach(var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                    if(t && (t.name=="Flexible seaweed skin"||t.name=="Seaweed bend 0"||t.name=="Flexible frame collider"))UnityEngine.Object.DestroyImmediate(t.gameObject);
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.name.StartsWith("Pb_"))r.enabled=true;
                foreach(var c in root.GetComponentsInChildren<Collider>(true))
                    if(c.name.StartsWith("Pb_Post")||c.name=="Pb_Beam"||c.name=="Pb_Crossbar")c.enabled=true;
                var all=root.GetComponentsInChildren<Transform>(true);
                var wall=root.GetComponent<ReefPhotoWall>();
                sway.vines=new ReefPhotoWallSway.Vine[6];
                sway.maximumBend=8;sway.spring=25;sway.damping=3.8f;sway.touchStrength=65;
                for(int i=0;i<6;i++)
                {
                    var stem=all.First(t=>t.name=="Pb_Vine"+i);
                    var bounds=stem.GetComponent<Renderer>().bounds;
                    var pivot=new GameObject("Independent vine "+(i+1)).transform;
                    pivot.SetParent(root.transform,false);pivot.position=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                    var tip=pivot.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
                    sway.vines[i]=new ReefPhotoWallSway.Vine{pivot=pivot,localTip=tip};
                    stem.SetParent(pivot,true);
                    all.First(t=>t.name=="Pb_VineLeaves"+i).SetParent(pivot,true);
                    string suffix="S"+(i+1)+"_";
                    foreach(var card in wall.cards)if(card&&card.name.StartsWith("Photo_"+suffix))card.SetParent(pivot,true);
                    foreach(var clip in all.Where(t=>t.name.StartsWith("Pb_Clip_"+suffix)))clip.SetParent(pivot,true);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                return "Six independent hanging vines, six pivots; fixed frame; matching photos and clips follow. No skinning or physics added.";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
