using UnityEditor;
using UnityEngine;

namespace Reefkeeper.Editor
{
    // PICO SDK 6.2 leaves its resource initializer on every Editor update,
    // calling SaveAssets repeatedly. Water preview dirties QualitySettings,
    // so this becomes a continuous save/import cycle and flashes the busy cursor.
    // Let the SDK initialize normally, then detach only that initializer.
    [InitializeOnLoad]
    internal static class PicoDebuggerInitializationGuard
    {
        static readonly string[] Resources =
        {
            "Assets/Resources/PXR_PICODebugger.prefab",
            "Assets/Resources/PXR_DebuggerPanel.prefab",
            "Assets/Resources/PXR_PICODebuggerActions.inputactions",
            "Assets/Resources/PXR_PicoDebuggerSO.asset"
        };

        static PicoDebuggerInitializationGuard()
        {
            EditorApplication.delayCall += FinishInitialization;
        }

        static void FinishInitialization()
        {
            var callbacks = EditorApplication.update;
            if (callbacks == null) return;
            foreach (var callback in callbacks.GetInvocationList())
            {
                if (callback.Method.DeclaringType?.FullName != "ByteDance.PICO.Debugger.PXR_PicoDebuggerSetup"
                    || callback.Method.Name != "Init_PXR_PicoDebuggerSetup") continue;

                var initialize = (EditorApplication.CallbackFunction)callback;
                // Also supports startup ordering where the SDK's first update has not run.
                initialize();
                foreach (var path in Resources)
                    if (!AssetDatabase.LoadAssetAtPath<Object>(path)) return;
                EditorApplication.update -= initialize;
            }
        }
    }
}
