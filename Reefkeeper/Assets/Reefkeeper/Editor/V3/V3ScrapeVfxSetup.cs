using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    public static class V3ScrapeVfxSetup
    {
        const string Folder = "Assets/Reefkeeper/Generated/V3/";

        [MenuItem("Reefkeeper/V3/Set up scraping debris VFX")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != V3CleaningSetup.ScenePath) throw new InvalidOperationException("Open ShallowReef_V3 first.");
            var workshop = Object.FindFirstObjectByType<ReefWorkshop>();
            if (!workshop || !workshop.scraper) throw new InvalidOperationException("V3 scraper is missing.");
            var effect = workshop.scraper.scrapeDebris;
            if (!effect)
            {
                var root = new GameObject("Scraping coral debris VFX");
                root.transform.SetParent(workshop.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "Add scraping debris");
                effect = root.AddComponent<ReefScrapeDebris>();
            }
            var particles = effect.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 96; main.startLifetime = 1; main.startSpeed = 0;
            main.startSize3D = true; main.startSizeX = main.startSizeY = main.startSizeZ = 0.012f;
            main.startRotation3D = true; main.gravityModifier = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0.45f, 0.65f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0.35f));
            var spin = particles.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-3, 3); spin.y = new ParticleSystem.MinMaxCurve(-4, 4); spin.z = new ParticleSystem.MinMaxCurve(-5, 5);
            var drift = particles.limitVelocityOverLifetime; drift.enabled = true;
            drift.limit = 0.16f; drift.dampen = 0.15f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = ChipMesh();
            renderer.sharedMaterial = ChipMaterial(); renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            workshop.scraper.scrapeDebris = effect;
            EditorUtility.SetDirty(workshop.scraper); EditorUtility.SetDirty(effect); EditorUtility.SetDirty(particles);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("V3_SCRAPE_VFX_READY: contact-driven world-space coral chips, lifetime fade, maximum 96 particles.");
        }

        static Material ChipMaterial()
        {
            string path = Folder + "ScrapeDebris.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Reefkeeper/UnderWater/Coral Debris");
            if (!shader) throw new InvalidOperationException("Coral debris shader has not imported yet.");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetFloat("_Brightness", 1.3f); EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh ChipMesh()
        {
            string path = Folder + "ScrapeDebrisChip.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh { name = "Irregular coral chip" }; AssetDatabase.CreateAsset(mesh, path); }
            // A chipped triangular prism, split per face for small faceted highlights.
            Vector3[] corners = {
                new Vector3(-0.48f, -0.16f, -0.32f), new Vector3(0.43f, -0.12f, -0.20f), new Vector3(0.07f, -0.10f, 0.54f),
                new Vector3(-0.35f, 0.17f, -0.24f), new Vector3(0.36f, 0.13f, -0.13f), new Vector3(0.03f, 0.11f, 0.42f) };
            int[] faces = { 0, 1, 2, 3, 5, 4, 0, 3, 1, 1, 3, 4, 1, 4, 2, 2, 4, 5, 2, 5, 0, 0, 5, 3 };
            var vertices = new Vector3[faces.Length]; var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]]; indices[i] = i; }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
    }
}
