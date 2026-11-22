using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Reefkeeper.Editor
{
    // Build the runtime parts from the artists' OBJ groups once, during import.
    [ScriptedImporter(1, "reefvinyl")]
    public sealed class VinylImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string color = File.ReadAllText(ctx.assetPath).Trim();
            string source = "Assets/Reefkeeper/Meshes/Vinyl/" + color;
            string shaderPath = AssetDatabase.GUIDToAssetPath("70b22a61296d81e4eaa6bf44d743d143");
            ctx.DependsOnSourceAsset(shaderPath);
            ctx.DependsOnSourceAsset(source + ".obj");
            ctx.DependsOnSourceAsset(source + ".mtl");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var palette = new Dictionary<string, Material>();
            string key = null;
            foreach (var line in File.ReadLines(source + ".mtl"))
            {
                if (line.StartsWith("newmtl ")) key = line.Substring(7);
                if (!line.StartsWith("Kd ")) continue;
                var rgb = ParseVector(line);
                var material = new Material(shader) { name = key };
                material.SetColor("_BaseColor", new Color(rgb.x,rgb.y,rgb.z));
                material.SetFloat("_SwayAmount", 0);
                palette.Add(key, material);
                ctx.AddObjectToAsset(key, material);
            }
            var root = new GameObject(color);
            ctx.AddObjectToAsset("root",root);
            ctx.SetMainObject(root);
            var vertices = new List<Vector3>();
            var faces = new List<List<int>>();
            var materials = new List<Material>();
            string group = null;
            int offset = 0;
            void Finish()
            {
                if (group == null) return;
                var mesh = new Mesh { name = group };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = faces.Count;
                for (int i=0;i<faces.Count;i++) mesh.SetTriangles(faces[i],i);
                mesh.RecalculateNormals();
                ctx.AddObjectToAsset(group,mesh);
                var part = new GameObject(group, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root.transform,false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                offset += vertices.Count;
                vertices.Clear(); faces.Clear(); materials.Clear();
            }
            foreach (string line in File.ReadLines(source + ".obj"))
            {
                if (line.StartsWith("g ")) { Finish(); group=line.Substring(2); }
                else if (line.StartsWith("v ")) { var v=ParseVector(line); vertices.Add(new Vector3(v.x,v.y,-v.z)); }
                else if (line.StartsWith("usemtl ")) { materials.Add(palette[line.Substring(7)]); faces.Add(new List<int>()); }
                else if (line.StartsWith("f "))
                {
                    var tri=line.Split(' ');
                    // OBJ is right-handed; reverse winding when reflecting its front to -Z.
                    for(int i=3;i>=1;i--) faces[faces.Count-1].Add(int.Parse(tri[i])-1-offset);
                }
            }
            Finish();
        }

        static Vector3 ParseVector(string line)
        {
            var parts=line.Split(' ');
            return new Vector3(float.Parse(parts[1],CultureInfo.InvariantCulture),
                float.Parse(parts[2],CultureInfo.InvariantCulture),float.Parse(parts[3],CultureInfo.InvariantCulture));
        }
    }
}

