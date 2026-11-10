using UnityEditor;
using UnityEngine;
namespace Reefkeeper.Editor
{
    [InitializeOnLoad]
    public static class DiveCardMockPreview
    {
        const string Pending="Reefkeeper.DiveCardMockPreview";
        static DiveCardMockPreview(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);EditorApplication.delayCall+=Show;}};}
        [MenuItem("Reefkeeper/V3/Preview 3D dive cards")]
        public static void Preview(){if(EditorApplication.isPlaying)Show();else{SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}}
        static void Show()
        {
            var existing=Object.FindFirstObjectByType<DiveCardMock>();
            if(!existing)existing=new GameObject("Dive card visual mock (Play Mode only)").AddComponent<DiveCardMock>();
            existing.openOnStart=true;existing.SetOpen(true);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
    }
}
