using UnityEngine;
using UnityEngine.Rendering;

namespace Reefkeeper
{
    // A single reusable mesh for all world-space shell billboards and their trails.
    // No colliders, rigidbodies, per-shell GameObjects, or per-frame managed arrays.
    internal sealed class ScallopWorldSprites : System.IDisposable
    {
        readonly GameObject drawing;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Material material;
        readonly Vector3[] vertices;
        readonly Vector2[] uv;
        readonly Color32[] colors;
        readonly Texture2D atlas;
        Matrix4x4 worldToLocal;
        int count;

        public ScallopWorldSprites(Transform parent, Texture2D texture, int capacity)
        {
            atlas=texture;
            drawing=new GameObject("World scallop rewards",typeof(MeshFilter),typeof(MeshRenderer));
            drawing.transform.SetParent(parent,false);
            mesh=new Mesh{name="Pooled scallop billboards"};mesh.MarkDynamic();
            vertices=new Vector3[capacity*4];uv=new Vector2[capacity*4];colors=new Color32[capacity*4];
            var triangles=new int[capacity*6];
            for(int i=0;i<capacity;i++)
            {
                int v=i*4,t=i*6;
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors32=colors;mesh.triangles=triangles;
            drawing.GetComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Resources.Load<Shader>("ScallopReward/WorldSprite")){name="World scallop atlas",mainTexture=atlas};
            renderer=drawing.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.enabled=false;
        }
        public void Begin()
        {
            count=0;worldToLocal=drawing.transform.worldToLocalMatrix;
        }
        public void Draw(Sprite sprite,Vector3 position,Quaternion facing,float size,float alpha)
        {
            int v=count*4;if(v+3>=vertices.Length)return;
            var right=facing*Vector3.right*(size*.5f);var up=facing*Vector3.up*(size*.5f);
            vertices[v]=worldToLocal.MultiplyPoint3x4(position-right-up);
            vertices[v+1]=worldToLocal.MultiplyPoint3x4(position-right+up);
            vertices[v+2]=worldToLocal.MultiplyPoint3x4(position+right+up);
            vertices[v+3]=worldToLocal.MultiplyPoint3x4(position+right-up);
            var rect=sprite.rect;
            float x0=rect.xMin/atlas.width,x1=rect.xMax/atlas.width,y0=rect.yMin/atlas.height,y1=rect.yMax/atlas.height;
            uv[v]=new Vector2(x0,y0);uv[v+1]=new Vector2(x0,y1);uv[v+2]=new Vector2(x1,y1);uv[v+3]=new Vector2(x1,y0);
            var color=new Color32(255,255,255,(byte)(255*Mathf.Clamp01(alpha)));
            for(int i=0;i<4;i++)colors[v+i]=color;
            count++;
        }
        public void End()
        {
            renderer.enabled=count>0;if(count==0)return;
            // Unused slots are degenerate transparent quads, retaining fixed buffer capacity.
            for(int i=count*4;i<vertices.Length;i++){vertices[i]=vertices[0];colors[i]=new Color32(0,0,0,0);}
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors32=colors;mesh.RecalculateBounds();
        }
        public void Hide()=>renderer.enabled=false;
        public void Dispose()
        {
            Object.Destroy(drawing);Object.Destroy(mesh);Object.Destroy(material);
        }
    }
}
