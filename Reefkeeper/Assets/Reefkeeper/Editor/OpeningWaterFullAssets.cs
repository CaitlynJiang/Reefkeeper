using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reefkeeper.Editor
{
    public static partial class OpeningWaterAssets
    {
        public static Material FullWaterMaterial()
        {
            const string path = Folder + "/SurfaceWater_Full.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(WaterMaterial()) { name = "SurfaceWater_Full", shader = Shader.Find("Reefkeeper/Uber Surface Full"), renderQueue = 3000 };
            foreach (var keyword in new[]{"_ENABLENORMAL","_ENABLEWAVE","_ENABLEPLANERREFLECTION","_ENABLEREFRACTION","_ENABLECAUSTICS"})
            { m.EnableKeyword(keyword); m.SetFloat(keyword,1); }
            // Keep the surface clear until foam is driven by wave generation/breaking.
            m.SetFloat("_Enable_SurfaceFoam",0);
            foreach (var keyword in new[]{"_ENABLESHORELINE","_ENABLE_UNDERWATERLAYER"})
            { m.DisableKeyword(keyword); m.SetFloat(keyword,0); }
            m.SetColor("_Color_Shallow",new Color(.13f,.53f,.55f,.32f));
            m.SetColor("_Color_Deep",new Color(.035f,.22f,.28f,1));
            m.SetFloat("_Water_Depth",.24f);
            m.SetFloat("_WorldSpaceDepth",1);
            m.SetFloat("_ShoreFade",1);m.SetFloat("_ShoreFade_Smoothness",.12f);
            m.SetFloat("_1st_Wave_Length",8);m.SetFloat("_1st_Wave_Height",.32f);
            m.SetFloat("_1st_Wave_Speed",.65f);m.SetFloat("_1st_Wave_Sharpness",.25f);
            m.SetFloat("_2nd_Wave_Length",4.5f);m.SetFloat("_2nd_Wave_Height",.16f);
            m.SetFloat("_2nd_Wave_Speed",.48f);m.SetFloat("_2nd_Wave_Sharpness",.18f);
            m.SetColor("_Wave_Top_Color",new Color(.23f,.62f,.60f,.3f));
            m.SetFloat("_Reflection_Strength",.28f);m.SetFloat("_Reflection_Fresnel",2.5f);
            m.SetFloat("_Reflection_Distortion",.35f);
            m.SetColor("_Specular_Color",new Color(8,6.2f,4f,1));
            m.SetFloat("_Normal_Strength",.65f);m.SetFloat("_Specular_Spread",.32f);
            m.SetFloat("_Refraction_Strength",.2f);m.SetFloat("_Refraction_Distance_Strength",.06f);
            m.SetFloat("_Refraction_Distance_Fade",.4f);
            m.SetFloat("_Enable_Intersection",0);m.SetFloat("_InterSec_Width",.35f);
            m.SetTexture("_InterSec_Foam_Mask",Texture("Noise 2.png",false,512));
            m.SetColor("_InterSec_Color",new Color(.94f,.97f,.85f,.7f));
            m.SetFloat("_InterSec_Dissolve",.65f);m.SetFloat("_InterSec_Foam_Scale",3);
            m.SetFloat("_InterSec_Foam_Smooth",.1f);
            m.SetTexture("_SL_Dissolve_Mask",Texture("Noise 2.png",false,512));
            m.SetColor("_SL_Color",new Color(.86f,.94f,.85f,.45f));
            m.SetFloat("_SL_Thickness",.12f);m.SetFloat("_SL_Dissolve",.6f);
            m.SetTexture("_Caustics_Map",Texture("Caustic 1.png",false,1024));
            m.SetTexture("_Caustics_Distortion_Map",Texture("Noise 2.png",false,512));
            m.SetFloat("_Caustics_Strength",.3f);m.SetFloat("_Caustics_Scale",2);
            m.SetFloat("_Caustics_Depth",-3);m.SetFloat("_Caustics_Start",8);m.SetFloat("_Caustics_Fade",20);
            m.SetFloat("_ConformToGeometry",0);m.SetFloat("_UnderWater_Depth",-.35f);
            m.SetColor("_Underwater_Color",new Color(.04f,.22f,.25f,.15f));
            m.SetFloat("_UnderWater_Start",5);m.SetFloat("_Underwater_Fade",12);
            AssetDatabase.CreateAsset(m,path);
            return m;
        }

        public static Mesh FullWaterMesh()
        {
            const string path = Folder + "/SurfaceWaterFullGrid.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)return mesh;
            var axis=new List<float>{-2000,-500,-160,-80};
            for(int i=0;i<=192;i++)axis.Add(-48+i*.5f);
            axis.AddRange(new float[]{80,160,500,2000});
            int n=axis.Count,index=0;
            var positions=new Vector3[n*n];var normals=new Vector3[n*n];
            var uv=new Vector2[n*n];var tangents=new Vector4[n*n];
            var tris=new int[(n-1)*(n-1)*6];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int v=z*n+x;positions[v]=new Vector3(axis[x],0,axis[z]);normals[v]=Vector3.up;
                uv[v]=new Vector2(axis[x],axis[z]);tangents[v]=new Vector4(1,0,0,-1);
                if(x==n-1||z==n-1)continue;
                tris[index++]=v;tris[index++]=v+n;tris[index++]=v+n+1;
                tris[index++]=v;tris[index++]=v+n+1;tris[index++]=v+1;
            }
            mesh=new Mesh {name="Full surface waves - half metre near grid",vertices=positions,normals=normals,uv=uv,tangents=tangents,triangles=tris};
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(4002,4,4002));
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        public static void ConfigureFullSurface(Renderer water)
        {
            water.sharedMaterial=FullWaterMaterial();
            water.GetComponent<MeshFilter>().sharedMesh=FullWaterMesh();
            water.gameObject.layer=4; // Exclude water itself from the reflected camera.
            water.gameObject.name="Surface ocean - full water trial";
            var manager=water.GetComponent<PlanarReflectionManager>();
            if(!manager)manager=water.gameObject.AddComponent<PlanarReflectionManager>();
            manager.runOnEditMode=false;
            var volume=water.GetComponent<PlanarReflectionVolume>();
            if(!volume)volume=water.gameObject.AddComponent<PlanarReflectionVolume>();
            volume.isGlobal=true;volume.reflectSkybox=true;volume.renderScale=1;
            volume.reflectionLayer=~(1<<4);volume.hideReflectionCamera=true;
            volume.reflectionTargets=new List<GameObject>{water.gameObject};
            volume.UpdateTargetMaterials();
            EditorUtility.SetDirty(water);EditorUtility.SetDirty(volume);EditorUtility.SetDirty(manager);
        }
    }
}
