using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Reefkeeper.Editor
{
    public static class CoralStickerCapture
    {
        const string Output="Assets/Reefkeeper/Resources/DiveCards/CoralStickers";
        [MenuItem("Reefkeeper/V3/Capture coral stage stickers")]
        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            var cameraObject=new GameObject("Temporary coral capture camera");
            cameraObject.hideFlags=HideFlags.HideAndDontSave;
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            var manifest=new List<string>{"Actual game prefab captures, original meshes/materials. Transparent PNG, 512 x 512.","White cutout border is derived from captured alpha, not generated artwork.","Each stage is individually framed for legibility; sticker sizes do not indicate relative physical size.","Rebuild: Reefkeeper > V3 > Capture coral stage stickers."};
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Reefkeeper/Prefabs/CoralGrowth"}).OrderBy(g=>AssetDatabase.GUIDToAssetPath(g)))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var growth=prefab.GetComponent<CoralGrowth>();
                    if(!growth)continue;
                    for(int stage=0;stage<growth.stages.Length;stage++)
                    {
                        var model=Object.Instantiate(growth.stages[stage].gameObject);
                        model.hideFlags=HideFlags.HideAndDontSave;
                        Texture2D photo=null;
                        try
                        {
                            model.SetActive(true);model.transform.SetPositionAndRotation(new Vector3(0,1000,0),Quaternion.identity);
                            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                            camera.transform.position=bounds.center+new Vector3(.65f,.40f,-1f).normalized*(bounds.size.magnitude*2+1);
                            camera.transform.LookAt(bounds.center);
                            float extent=0;
                            for(int corner=0;corner<8;corner++)
                            {
                                var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                                var local=camera.transform.InverseTransformPoint(p);extent=Mathf.Max(extent,Mathf.Abs(local.x),Mathf.Abs(local.y));
                            }
                            camera.orthographicSize=extent*1.14f;camera.targetTexture=target;camera.Render();
                            RenderTexture.active=target;photo=new Texture2D(512,512,TextureFormat.RGBA32,false);
                            photo.ReadPixels(new Rect(0,0,512,512),0,0);photo.Apply();WhiteBorder(photo);
                            string name=prefab.name+"_S"+(stage+1)+".png";
                            File.WriteAllBytes(Output+"/"+name,photo.EncodeToPNG());manifest.Add(name+" <- "+path+" / "+growth.stages[stage].name);
                        }
                        finally{if(photo)Object.DestroyImmediate(photo);Object.DestroyImmediate(model);}
                    }
                }
                File.WriteAllLines(Output+"/README.txt",manifest);
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(cameraObject);}
            AssetDatabase.Refresh();
            foreach(string file in Directory.GetFiles(Output,"*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.SaveAndReimport();
            }
            Debug.Log("Coral sticker capture complete: "+(manifest.Count-4)+" growth stages.");
        }

        // Two-pass chamfer distance produces a soft, rounded white border around the captured silhouette.
        static void WhiteBorder(Texture2D image)
        {
            int n=image.width;var pixels=image.GetPixels();var distance=new float[pixels.Length];
            for(int i=0;i<distance.Length;i++)distance[i]=pixels[i].a>.1f?0:10000;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                int i=y*n+x;
                if(x>0)distance[i]=Mathf.Min(distance[i],distance[i-1]+1);
                if(y>0){distance[i]=Mathf.Min(distance[i],distance[i-n]+1);if(x>0)distance[i]=Mathf.Min(distance[i],distance[i-n-1]+1.4142f);if(x<n-1)distance[i]=Mathf.Min(distance[i],distance[i-n+1]+1.4142f);}
            }
            for(int y=n-1;y>=0;y--)for(int x=n-1;x>=0;x--)
            {
                int i=y*n+x;
                if(x<n-1)distance[i]=Mathf.Min(distance[i],distance[i+1]+1);
                if(y<n-1){distance[i]=Mathf.Min(distance[i],distance[i+n]+1);if(x>0)distance[i]=Mathf.Min(distance[i],distance[i+n-1]+1.4142f);if(x<n-1)distance[i]=Mathf.Min(distance[i],distance[i+n+1]+1.4142f);}
            }
            for(int i=0;i<pixels.Length;i++)
            {
                var source=pixels[i];float border=Mathf.Clamp01(10-distance[i]);float alpha=source.a+border*(1-source.a);
                pixels[i]=alpha>0?new Color((source.r*source.a+border*(1-source.a))/alpha,(source.g*source.a+border*(1-source.a))/alpha,(source.b*source.a+border*(1-source.a))/alpha,alpha):Color.clear;
            }
            image.SetPixels(pixels);image.Apply();
        }
    }
}
