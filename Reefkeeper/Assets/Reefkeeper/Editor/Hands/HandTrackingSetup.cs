using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace Reefkeeper.Hands
{
    /// <summary>
    /// Adds tracked hands to the V3 rig next to the controllers. XRInputModalityManager then shows
    /// the hands, with their own poke and near-far interactors, whenever the controllers are put
    /// down, and the controllers again when they are picked up. Hands come from the XRI Hands
    /// Interaction Demo sample; their meshes from the XR Hands Hand Visualizer sample.
    /// </summary>
    public static class HandTrackingSetup
    {
        public const string ScenePath = "Assets/Reefkeeper/Scenes/ShallowReef_V3.unity";
        const string HandsRig = "Assets/Samples/XR Interaction Toolkit/3.3.2/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        [MenuItem("Reefkeeper/Hands/1. Add tracked hands to V3 rig - 添加手势追踪")]
        public static void AddHands()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var origin = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).First();
            var offset = origin.CameraFloorOffsetObject.transform;
            foreach (var name in new[] { "Left Hand", "Right Hand" })
            {
                var old = offset.Find(name);
                if (old) Object.DestroyImmediate(old.gameObject);
            }
            var oldFilter = origin.transform.Find("Hands Smoothing Post Processor");
            if (oldFilter) Object.DestroyImmediate(oldFilter.gameObject);

            // Instantiate the sample rig, then move its hand objects into ours.
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HandsRig), scene);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var tempOffset = temp.GetComponent<XROrigin>().CameraFloorOffsetObject.transform;
            var moved = new List<Transform>
            {
                tempOffset.Find("Left Hand"), tempOffset.Find("Right Hand"), temp.transform.Find("Hands Smoothing Post Processor"),
            };
            // Keep one mesh per hand: the generic (Quest) mesh also suits PICO.
            foreach (var hand in moved.Take(2))
                foreach (Transform child in hand.Cast<Transform>().ToArray())
                {
                    if (child.name.Contains("AndroidXRVisual")) Object.DestroyImmediate(child.gameObject);
                    else if (child.name.Contains("QuestVisual")) child.gameObject.SetActive(true);   // off in the sample
                }

            moved[0].SetParent(offset, false);
            moved[1].SetParent(offset, false);
            moved[2].SetParent(origin.transform, false);
            RetargetReferences(moved, temp.transform, origin.transform);
            Object.DestroyImmediate(temp);

            var modality = origin.GetComponent<XRInputModalityManager>();
            modality.leftHand = moved[0].gameObject;
            modality.rightHand = moved[1].gameObject;
            // Hands start hidden; the modality manager enables them once hand tracking starts.
            moved[0].gameObject.SetActive(false);
            moved[1].gameObject.SetActive(false);
            EditorUtility.SetDirty(modality);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Hands] Tracked hands added to " + ScenePath);
        }

        /// <summary>
        /// Moved objects may still point at parts of the sample rig (its camera, origin, interaction
        /// manager). Point those at the object with the same path in our rig instead.
        /// </summary>
        static void RetargetReferences(List<Transform> moved, Transform sampleRoot, Transform ourRoot)
        {
            bool InMoved(Transform t) => moved.Any(m => t == m || t.IsChildOf(m));
            foreach (var component in moved.SelectMany(m => m.GetComponentsInChildren<Component>(true)))
            {
                if (!component) continue;
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                    var target = it.objectReferenceValue;
                    var t = target is Component c ? c.transform : target is GameObject g ? g.transform : null;
                    if (!t || !t.IsChildOf(sampleRoot) || InMoved(t)) continue;
                    var path = AnimationUtility.CalculateTransformPath(t, sampleRoot);
                    var ours = path.Length == 0 ? ourRoot : ourRoot.Find(path);
                    Object replacement = null;
                    if (ours) replacement = target is GameObject ? ours.gameObject : (Object)ours.GetComponent(target.GetType());
                    it.objectReferenceValue = replacement;
                    changed = true;
                    if (!replacement) Debug.LogWarning($"[Hands] {component.name}.{it.propertyPath} lost its reference to {path}");
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
