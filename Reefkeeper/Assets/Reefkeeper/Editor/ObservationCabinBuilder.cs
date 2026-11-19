using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    /// <summary>
    /// Static, metre-scale cabin blockout. Glass is intentionally absent: only opaque
    /// edge glints and independent collision panes. No cameras, lights or runtime scripts.
    /// </summary>
    public static class ObservationCabinBuilder
    {
        const string Folder = "Assets/Reefkeeper/Generated/ObservationCabin";
        public const string PrefabPath = Folder + "/ObservationCabin.prefab";
        public const string InstanceName = "V4 Observation Cabin";
        static readonly Vector3 Placement = new Vector3(3.8f, 2.45f, 10.2f);
        static readonly Quaternion Orientation = Quaternion.Euler(0,90,0);
        static readonly string[] Names = { "Cream frame", "Lagoon shell", "Honey cushions", "Deep teal fittings", "Ivory glints", "Warm lamp lenses" };
        static readonly Color[] Colors = {
            new Color(.91f,.86f,.67f), new Color(.20f,.49f,.49f),
            new Color(.95f,.64f,.20f), new Color(.12f,.27f,.29f),
            new Color(.91f,.97f,.85f), new Color(1f,.85f,.49f)
        };
        static Geometry[] geometry;
        static GameObject root;

        [MenuItem("Reefkeeper/V4/Build observation cabin model")]
        public static void BuildAndPlace()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before building the cabin.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != V4HabitatBuilder.Scene) throw new InvalidOperationException("Open ShallowReef_V4 first.");
            if (scene.isDirty) throw new InvalidOperationException("Save current scene edits before building the cabin.");
            var prefab = BuildPrefab();
            var previous = GameObject.Find(InstanceName);
            if (previous) Undo.DestroyObjectImmediate(previous);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.name = InstanceName;
            placed.transform.position = Placement;
            placed.transform.rotation = Orientation;
            Undo.RegisterCreatedObjectUndo(placed, "Add observation cabin");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = placed;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Placement + Vector3.up, new Vector3(7,6,7)), false);
        }

        public static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            geometry = Names.Select(_ => new Geometry()).ToArray();
            root = new GameObject("Observation Cabin");
            try
            {
                // Floor origin is the walkable surface, y=0. Roof underside is y=2.6.
                // Front is -Z; entry is on +X, from z=-0.25 to z=1.05.
                var outline = RoundedRectangle(4.2f,3.8f,.42f,3);
                var inner = RoundedRectangle(3.92f,3.52f,.30f,3);
                Ring(1, outline, inner, -.24f, -.04f);
                Ring(0, outline, inner, -.04f, .04f);
                // A continuous ring avoids gaps where a rectangular slab meets rounded corners.
                var viewport=RoundedRectangle(2.6f,1.05f,.015f,3).Select(v=>v+new Vector2(0,-1.035f)).ToArray();
                Ring(0,inner,viewport,-.16f,0);
                // Low rim and dark gasket communicate a sealed pane without blending pixels.
                RectRim(3,2.68f,1.13f,.045f,new Vector3(0,.012f,-1.035f));
                RectRim(0,2.78f,1.23f,.045f,new Vector3(0,.025f,-1.035f));
                Beam(4,new Vector3(-1.19f,.033f,-1.49f),new Vector3(-.62f,.033f,-1.49f),.018f,.018f);
                Beam(4,new Vector3(-1.19f,.033f,-1.49f),new Vector3(-1.19f,.033f,-1.23f),.018f,.018f);
                // Collision across the viewport is independent from its deliberately empty mesh.
                Collider("Solid floor including observation pane",new Vector3(0,-.10f,0),new Vector3(3.92f,.20f,3.52f));

                // Slight bevels make the roof read as moulded PVC rather than a cube.
                Solid(1,RoundedRectangle(4.18f,3.78f,.46f,3),2.59f,2.69f);
                BevelSlab(0,new Vector3(0,2.76f,0),4.38f,3.98f,.20f,.50f,.075f);
                Collider("Ceiling",new Vector3(0,2.72f,0),new Vector3(4.2f,.24f,3.8f));

                for(int i=0;i<outline.Length;i++)
                {
                    Vector2 a=outline[i], b=outline[(i+1)%outline.Length];
                    // The straight starboard segment is split around the open entry.
                    if(Mathf.Abs(a.x-2.1f)<.001f && Mathf.Abs(b.x-2.1f)<.001f)
                    {
                        Wall(a,new Vector2(2.1f,-.25f));
                        Wall(new Vector2(2.1f,1.05f),b);
                        continue;
                    }
                    Wall(a,b);
                }
                // Main window mullions. Side corner segments provide softly faceted glazing.
                foreach(float x in new[]{-1.68f,1.68f})
                    Box(0,new Vector3(x,1.47f,-1.9f),new Vector3(.11f,2.16f,.16f));
                Box(0,new Vector3(-2.1f,1.47f,0),new Vector3(.16f,2.16f,.11f));
                foreach(float x in new[]{-1.68f,1.68f})
                    Box(0,new Vector3(x,1.47f,1.9f),new Vector3(.11f,2.16f,.16f));
                // Entry: clear 1.3m width, 2.22m height. No invisible pane across it.
                foreach(float z in new[]{-.32f,1.12f})
                {
                    Box(0,new Vector3(2.1f,1.16f,z),new Vector3(.24f,2.32f,.14f));
                    Collider("Door jamb",new Vector3(2.1f,1.16f,z),new Vector3(.24f,2.32f,.14f));
                }
                Box(0,new Vector3(2.1f,2.40f,.40f),new Vector3(.24f,.36f,1.58f));
                Collider("Door lintel",new Vector3(2.1f,2.40f,.4f),new Vector3(.24f,.36f,1.58f));
                Box(2,new Vector3(2.12f,.055f,.4f),new Vector3(.32f,.035f,1.30f));
                // Parked open panel: static visual only, does not imply a working button.
                Box(1,new Vector3(2.20f,1.15f,-1.02f),new Vector3(.08f,2.12f,1.27f));
                Box(2,new Vector3(2.27f,1.15f,-.49f),new Vector3(.08f,.34f,.065f));
                Box(3,new Vector3(2.24f,2.27f,-.34f),new Vector3(.08f,.045f,2.74f));

                // Back bench leaves the entrance and a >1.5m turning area unobstructed.
                BevelSlab(1,new Vector3(0,.25f,1.35f),2.45f,.70f,.46f,.16f,.055f);
                BevelSlab(2,new Vector3(0,.51f,1.31f),2.38f,.70f,.16f,.18f,.035f);
                Box(3,new Vector3(0,.60f,1.65f),new Vector3(2.38f,.05f,.08f));
                // Cushioned back: rounded slab rotated to face the room.
                SlabRotated(2,new Vector3(0,.84f,1.64f),2.38f,.65f,.13f,.16f,Quaternion.Euler(90,0,0));
                Collider("Bench seat",new Vector3(0,.30f,1.34f),new Vector3(2.45f,.60f,.75f));
                Collider("Bench back",new Vector3(0,.86f,1.64f),new Vector3(2.4f,.65f,.18f));

                // Four opaque lenses, no realtime lights. Tiny warm pieces suggest cabin lighting.
                foreach(float x in new[]{-1.30f,1.30f}) foreach(float z in new[]{-.95f,.95f})
                {
                    Solid(3,Circle(.14f,10,new Vector2(x,z)),2.46f,2.59f);
                    Solid(5,Circle(.115f,10,new Vector2(x,z)),2.43f,2.48f);
                }
                // Subtle edge glints stay next to frames, leaving all central views clear.
                Beam(4,new Vector3(-1.5f,2.46f,-1.905f),new Vector3(-.92f,2.46f,-1.905f),.016f,.016f);
                Beam(4,new Vector3(-1.54f,2.40f,-1.905f),new Vector3(-1.54f,2.13f,-1.905f),.016f,.016f);
                // Four fixed moorings to the deck; the cabin never bobs or changes locomotion.
                foreach(float x in new[]{-1.55f,1.55f})foreach(float z in new[]{-1.24f,1.24f})
                {
                    // In this V4 placement the moorings splay back onto actual ship surfaces.
                    var worldAnchor=new Vector3(z<0?2.45f:3.5f,0,x<0?11.3f:9.4f);
                    var ship=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name.StartsWith("Shipwreck"));
                    var deckHits=ship.GetComponentsInChildren<Collider>().Select(c=>{
                        RaycastHit hit;bool found=c.Raycast(new Ray(worldAnchor+Vector3.up*14,Vector3.down),out hit,30);
                        return new {found,hit};
                    }).Where(h=>h.found && !h.hit.collider.name.StartsWith("Habitat")).OrderBy(h=>h.hit.distance).ToArray();
                    if(deckHits.Length==0)throw new InvalidOperationException("No ship surface beneath mooring anchor.");
                    worldAnchor.y=deckHits[0].hit.point.y+.05f;
                    var end=Quaternion.Inverse(Orientation)*(worldAnchor-Placement);
                    Beam(3,new Vector3(x,-.20f,z),end,.035f,.035f);
                    BevelSlab(2,end,new Vector3(.30f,.10f,.25f));
                    BevelSlab(2,new Vector3(x,-.24f,z),new Vector3(.24f,.10f,.20f));
                }
                // Landmark transforms make camera captures and future interaction setup repeatable.
                Landmark("Interior eye 1.65m",new Vector3(0,1.65f,.45f));
                Landmark("Entrance center",new Vector3(2.1f,1.11f,.4f));
                Landmark("Floor observation center",new Vector3(0,.04f,-1.035f));
                Landmark("Seated eye 1.15m",new Vector3(0,1.15f,1.15f));

                int triangles=0;
                for(int i=0;i<geometry.Length;i++)
                {
                    var mesh=geometry[i].Mesh(Names[i]);triangles+=mesh.triangles.Length/3;
                    string path=Folder+"/"+Names[i]+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
                    else AssetDatabase.CreateAsset(mesh,path);
                    var part=new GameObject(Names[i]);part.transform.SetParent(root.transform,false);
                    part.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=Material(i);
                    renderer.shadowCastingMode=i>=4?ShadowCastingMode.Off:ShadowCastingMode.On;
                    renderer.receiveShadows=i<4;
                }
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                File.WriteAllText(Folder+"/ModelNotes.txt",
                    "Observation cabin low-poly prototype\nUnits: metres. Floor y=0, roof underside y=2.6.\n"+
                    "Outer footprint 4.2 x 3.8m; interior roughly 3.9 x 3.5m before bench.\n"+
                    "Entry +X: 1.3m clear width, 2.22m height. Door parked OPEN; no door controls yet.\n"+
                    "Floor viewing opening: 2.6 x 1.05m, towards -Z. Independent solid floor collider.\n"+
                    "Six opaque material groups, "+triangles+" triangles. No transparent panes, reflection/refraction, extra cameras or realtime lights.\n"+
                    "Fixed placement above forward shipwreck deck. Swimming remains unchanged; no gravity/airlock logic.\n"+
                    "VR comfort and headset timing still require device testing. Desktop screenshots are actual model captures, not generated concept art.\n");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();return prefab;
            }
            finally { Object.DestroyImmediate(root);root=null;geometry=null; }
        }

        static Material Material(int i)
        {
            string path=Folder+"/"+Names[i]+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",Colors[i]);mat.SetFloat("_Smoothness",.18f);mat.SetFloat("_Metallic",0);
            if(i==5){mat.SetColor("_EmissionColor",Colors[i]*.6f);mat.EnableKeyword("_EMISSION");}
            return mat;
        }
        static void Wall(Vector2 a,Vector2 b)
        {
            if(Vector2.Distance(a,b)<.01f)return;
            var p=new Vector3(a.x,0,a.y);var q=new Vector3(b.x,0,b.y);
            Beam(1,p+Vector3.up*.20f,q+Vector3.up*.20f,.34f,.12f);
            Beam(0,p+Vector3.up*.42f,q+Vector3.up*.42f,.10f,.18f);
            Beam(0,p+Vector3.up*2.53f,q+Vector3.up*2.53f,.14f,.18f);
            Beam(3,p+Vector3.up*.49f,q+Vector3.up*.49f,.023f,.07f);
            var wall=new GameObject("Window collision (no glass renderer)");wall.transform.SetParent(root.transform,false);
            wall.transform.localPosition=(p+q)*.5f+Vector3.up*1.3f;
            wall.transform.localRotation=Quaternion.LookRotation(q-p);
            wall.AddComponent<BoxCollider>().size=new Vector3(.12f,2.6f,Vector3.Distance(p,q));
        }
        static void Collider(string name,Vector3 center,Vector3 size){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.localPosition=center;g.AddComponent<BoxCollider>().size=size;}
        static void Landmark(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.localPosition=p;}
        static void Box(int mat,Vector3 p,Vector3 size){geometry[mat].Box(Matrix4x4.TRS(p,Quaternion.identity,size));}
        static void Beam(int mat,Vector3 a,Vector3 b,float height,float width){geometry[mat].Box(Matrix4x4.TRS((a+b)*.5f,Quaternion.LookRotation(b-a),new Vector3(width,height,(b-a).magnitude)));}
        static void RectRim(int mat,float w,float d,float t,Vector3 p){Box(mat,p+Vector3.right*(w-t)*.5f,new Vector3(t,.025f,d));Box(mat,p-Vector3.right*(w-t)*.5f,new Vector3(t,.025f,d));Box(mat,p+Vector3.forward*(d-t)*.5f,new Vector3(w,.025f,t));Box(mat,p-Vector3.forward*(d-t)*.5f,new Vector3(w,.025f,t));}
        static void BevelSlab(int mat,Vector3 p,Vector3 size)=>BevelSlab(mat,p,size.x,size.z,size.y,.08f,.025f);
        static void BevelSlab(int mat,Vector3 p,float w,float d,float h,float r,float bevel)
        {
            var outer=RoundedRectangle(w,d,r,3);var inset=RoundedRectangle(w-bevel*2,d-bevel*2,Mathf.Max(.02f,r-bevel),3);
            var g=geometry[mat];g.Profile(new[]{inset,outer,outer,inset},new[]{-h/2,-h/2+bevel,h/2-bevel,h/2},Matrix4x4.Translate(p));
        }
        static void SlabRotated(int mat,Vector3 p,float w,float d,float h,float r,Quaternion rotation){var shape=RoundedRectangle(w,d,r,3);geometry[mat].Profile(new[]{shape,shape},new[]{-h/2,h/2},Matrix4x4.TRS(p,rotation,Vector3.one));}
        static void Solid(int mat,Vector2[] shape,float lo,float hi)=>geometry[mat].Profile(new[]{shape,shape},new[]{lo,hi},Matrix4x4.identity);
        static void Ring(int mat,Vector2[] outer,Vector2[] inner,float lo,float hi)
        {
            var g=geometry[mat];for(int i=0;i<outer.Length;i++){int j=(i+1)%outer.Length;var a=P(outer[i],lo);var b=P(outer[j],lo);var c=P(inner[i],lo);var d=P(inner[j],lo);var up=Vector3.up*(hi-lo);g.Quad(a,a+up,b+up,b);g.Quad(d,d+up,c+up,c);g.Quad(a+up,c+up,d+up,b+up);g.Quad(a,b,d,c);}
        }
        static Vector3 P(Vector2 v,float y)=>new Vector3(v.x,y,v.y);
        static Vector2[] Circle(float radius,int n,Vector2 center){var p=new Vector2[n];for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;p[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}return p;}
        static Vector2[] RoundedRectangle(float w,float d,float r,int segments)
        {
            var pts=new List<Vector2>();for(int c=0;c<4;c++)for(int j=0;j<=segments;j++)
            {float a=(c*90+j*90f/segments)*Mathf.Deg2Rad;float cx=(c==0||c==3?1:-1)*(w/2-r);float cz=(c<2?1:-1)*(d/2-r);pts.Add(new Vector2(cx+r*Mathf.Cos(a),cz+r*Mathf.Sin(a)));}return pts.ToArray();
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
            void Tri(Vector3 a,Vector3 b,Vector3 c){int i=vertices.Count;vertices.AddRange(new[]{a,b,c});indices.AddRange(new[]{i,i+1,i+2});}
            public void Box(Matrix4x4 m)
            {
                var p=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)}.Select(m.MultiplyPoint3x4).ToArray();
                Quad(p[0],p[3],p[2],p[1]);Quad(p[4],p[5],p[6],p[7]);Quad(p[0],p[4],p[7],p[3]);Quad(p[1],p[2],p[6],p[5]);Quad(p[3],p[7],p[6],p[2]);Quad(p[0],p[1],p[5],p[4]);
            }
            public void Profile(Vector2[][] shapes,float[] ys,Matrix4x4 m)
            {
                int n=shapes[0].Length;for(int l=0;l<ys.Length-1;l++)for(int i=0;i<n;i++){int j=(i+1)%n;Quad(m.MultiplyPoint3x4(P(shapes[l][i],ys[l])),m.MultiplyPoint3x4(P(shapes[l+1][i],ys[l+1])),m.MultiplyPoint3x4(P(shapes[l+1][j],ys[l+1])),m.MultiplyPoint3x4(P(shapes[l][j],ys[l])));}
                var bottom=shapes[0].Aggregate(Vector2.zero,(sum,p)=>sum+p)/n;var top=shapes[shapes.Length-1].Aggregate(Vector2.zero,(sum,p)=>sum+p)/n;int last=ys.Length-1;
                for(int i=0;i<n;i++){int j=(i+1)%n;Tri(m.MultiplyPoint3x4(P(bottom,ys[0])),m.MultiplyPoint3x4(P(shapes[0][i],ys[0])),m.MultiplyPoint3x4(P(shapes[0][j],ys[0])));Tri(m.MultiplyPoint3x4(P(top,ys[last])),m.MultiplyPoint3x4(P(shapes[last][j],ys[last])),m.MultiplyPoint3x4(P(shapes[last][i],ys[last])));}
            }
            public Mesh Mesh(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetTriangles(indices,0);m.SetUVs(0,vertices.Select(v=>new Vector2(v.x,v.z)).ToList());m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
    }
}
