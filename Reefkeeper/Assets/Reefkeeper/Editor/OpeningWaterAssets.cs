using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reefkeeper.Editor
{
    public static partial class OpeningWaterAssets
    {
        const string Folder = "Assets/Reefkeeper/Generated/SurfaceIntro";
        const string Source = "Assets/ThirdParty/UberStylizedWater/Textures/";
        public static Material WaterMaterial()
        {
            const string path = Folder + "/SurfaceWater_Foam.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = Shader.Find("Reefkeeper/Uber Surface");
            if (!shader) throw new System.InvalidOperationException("Import Uber surface shader first.");
            material = new Material(shader) { name = "SurfaceWater_Foam", renderQueue = 2000 };
            material.SetColor("_Color_Shallow", new Color(.12f,.46f,.49f,1));
            material.SetColor("_Color_Deep", new Color(.12f,.46f,.49f,1));
            material.SetFloat("_ShoreFade",0);
            material.SetFloat("_Enable_Intersection",0);
            material.SetFloat("_Enable_SurfaceFoam",1);
            material.SetTexture("_SurfFoam_Map", Texture("Foam 1.png",false,512));
            material.SetTexture("_SurfaceDistortion_Map", Texture("Noise 2.png",false,512));
            material.SetTexture("_Normal_Map", Texture("Normal 2.png",true,1024));
            material.SetColor("_SurfFoam_Color",new Color(.94f,.94f,.81f,.52f));
            material.SetFloat("_SurfFoam_Scale",6f);
            material.SetVector("_SurfFoam_Tile",new Vector4(1,.45f,0,0));
            material.SetVector("_SurfFoam_Pan",new Vector4(.018f,.009f,0,0));
            material.SetFloat("_SurfFoam_Edge",.94f);
            material.SetFloat("_SurfFoam_EdgeSmooth",.035f);
            material.SetFloat("_SurfFoam_AlphaBlend",0);
            material.SetFloat("_SurfaceDistortion_Scale",.37f);
            material.SetFloat("_SurfaceDistortion_Strength",.14f);
            material.SetVector("_SurfaceDistortion_Pan",new Vector4(.013f,-.009f,0,0));
            material.SetFloat("_Normal_Strength",1f);
            material.SetFloat("_Normal_Scale",6f);
            material.SetFloat("_Normal_Pan",.035f);
            material.SetFloat("_Normal_DistanceStrength",.08f);
            material.SetFloat("_DistanceMask_Start",12);
            material.SetFloat("_DistanceMask_Fade",45);
            material.SetColor("_Specular_Color",new Color(5,3.9f,2.5f,1));
            material.SetFloat("_Specular_Spread",.25f);
            material.SetFloat("_Specular_Hardness",.65f);
            material.SetFloat("_Specular_Size",.5f);
            material.SetColor("_ShadowColor",new Color(.12f,.3f,.32f,0));
            material.SetFloat("_1st_Wave_Length",7);
            material.SetFloat("_1st_Wave_Height",.035f);
            material.SetFloat("_1st_Wave_Speed",.35f);
            material.SetFloat("_1st_Wave_Sharpness",.08f);
            material.SetVector("_1st_Wave_Direction",new Vector4(.8f,0,.6f,0));
            material.SetFloat("_2nd_Wave_Length",4.3f);
            material.SetFloat("_2nd_Wave_Height",.022f);
            material.SetFloat("_2nd_Wave_Speed",.27f);
            material.SetFloat("_2nd_Wave_Sharpness",.06f);
            material.SetVector("_2nd_Wave_Direction",new Vector4(-.4f,0,.9f,0));
            material.SetColor("_Wave_Top_Color",new Color(.17f,.5f,.52f,.18f));
            foreach (var keyword in new[]{"_ENABLESHORELINE","_ENABLEREFRACTION","_ENABLECAUSTICS","_ENABLE_UNDERWATERLAYER","_ENABLEPLANERREFLECTION"})
            { material.DisableKeyword(keyword);material.SetFloat(keyword,0); }
            foreach (var keyword in new[]{"_ENABLENORMAL","_ENABLEWAVE"})
            { material.EnableKeyword(keyword);material.SetFloat(keyword,1); }
            material.SetFloat("_Reflection_Strength",0);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }
        static Texture2D Texture(string name,bool normal,int size)
        {
            string path=Source+name;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=false;importer.isReadable=false;importer.mipmapEnabled=true;
            importer.maxTextureSize=size;importer.wrapMode=TextureWrapMode.Repeat;
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name="Android",overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_6x6 });
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public static Mesh WaterMesh()
        {
            const string path=Folder+"/SurfaceWaterGrid.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)return mesh;
            var axis=new List<float>{-2000,-500,-160,-80};
            for(int i=0;i<=64;i++)axis.Add(-48+i*1.5f);
            axis.AddRange(new float[]{80,160,500,2000});
            int n=axis.Count;
            var vertices=new Vector3[n*n];var normals=new Vector3[n*n];var uv=new Vector2[n*n];var tangents=new Vector4[n*n];
            var triangles=new int[(n-1)*(n-1)*6];int index=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int v=z*n+x;vertices[v]=new Vector3(axis[x],0,axis[z]);normals[v]=Vector3.up;
                uv[v]=new Vector2(axis[x],axis[z]);tangents[v]=new Vector4(1,0,0,-1);
                if(x==n-1||z==n-1)continue;
                triangles[index++]=v;triangles[index++]=v+n;triangles[index++]=v+n+1;
                triangles[index++]=v;triangles[index++]=v+n+1;triangles[index++]=v+1;
            }
            mesh=new Mesh { name="Opening ocean - dense near seat, sparse horizon",vertices=vertices,normals=normals,uv=uv,tangents=tangents,triangles=triangles };
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(4000,1,4000));
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
