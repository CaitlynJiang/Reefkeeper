using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper
{
    // A small, resolution-independent soft shape; the object icons remain photographed assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReefPebbleGraphic : MaskableGraphic
    {
        public float seed;
        public bool softShadow, highlight;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            if (softShadow) Silhouette(mesh, rect, new Vector2(1,-4), 1.02f, new Color(.08f,.19f,.18f,.19f), 5);
            // A transparent fringe softens the silhouette without adding a colored outline.
            Silhouette(mesh, rect, Vector2.zero, 1, color, .8f);
            if (highlight) Highlight(mesh, rect);
        }

        Vector2 Edge(Rect rect, float angle)
        {
            float wave=.93f+.035f*Mathf.Sin(angle*3+seed)+.025f*Mathf.Cos(angle*2-seed);
            return new Vector2(Mathf.Cos(angle)*rect.width*.5f,Mathf.Sin(angle)*rect.height*.5f)*wave;
        }

        void Silhouette(VertexHelper mesh, Rect rect, Vector2 offset, float scale, Color fill, float feather)
        {
            const int segments = 64;
            int center=mesh.currentVertCount;
            mesh.AddVert(rect.center+offset,fill,Vector2.zero);
            var transparent=fill; transparent.a=0;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var edge=Edge(rect,angle)*scale;
                mesh.AddVert(rect.center+offset+edge,fill,Vector2.zero);
                mesh.AddVert(rect.center+offset+edge+edge.normalized*feather,transparent,Vector2.zero);
                if (i == 0) continue;
                int a = center + 1 + (i - 1) * 2, b = a + 2;
                mesh.AddTriangle(center, a, b);
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        void Highlight(VertexHelper mesh, Rect rect)
        {
            int start=mesh.currentVertCount;
            var light=new Color(.99f,.97f,.84f,.94f); var transparent=light;transparent.a=0;
            const int segments=18;
            for(int i=0;i<=segments;i++)
            {
                float t=(float)i/segments, angle=Mathf.Lerp(151,116,t)*Mathf.Deg2Rad;
                var point=rect.center+Edge(rect,angle)*.82f;
                var tangent=(Edge(rect,angle+.01f)-Edge(rect,angle-.01f)).normalized;
                var normal=new Vector2(-tangent.y,tangent.x);
                float width=1.15f*Mathf.Sqrt(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)));
                mesh.AddVert(point-normal*(width+.7f),transparent,Vector2.zero);
                mesh.AddVert(point-normal*width,light,Vector2.zero);
                mesh.AddVert(point+normal*width,light,Vector2.zero);
                mesh.AddVert(point+normal*(width+.7f),transparent,Vector2.zero);
                if(i==0) continue;
                int a=start+(i-1)*4,b=a+4;
                for(int j=0;j<3;j++) { mesh.AddTriangle(a+j,a+j+1,b+j+1);mesh.AddTriangle(a+j,b+j+1,b+j); }
            }
        }

        public void Tint(Color fill)
        {
            if (color == fill) return;
            color = fill;
        }
    }
}
