using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif
namespace Reefkeeper.Hands
{
    /// <summary>Left thumb/middle pinch toggles the existing hand journal.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class ReefPalmMenu : MonoBehaviour
    {
        public static ReefPalmMenu Instance { get; private set; }
        public bool Visible => DiveCardMock.Instance && DiveCardMock.Instance.IsOpen;
        float holdTime, releaseTime;
        bool openedByPalm, armed;
#if UNITY_EDITOR
        public bool EditorPreview => DiveCardMock.Instance && DiveCardMock.Instance.EditorHandPreview && Visible;
        public void SetEditorPreview(bool value)
        {
            var card=DiveCardMock.Instance;
            if(!card||UnityEngine.XR.XRSettings.isDeviceActive)return;
            card.EditorHandPreview=value;card.SetOpen(value);
        }
#endif
        public static bool EditorPreviewVisible
        {
            get
            {
#if UNITY_EDITOR
                return Instance && Instance.EditorPreview;
#else
                return false;
#endif
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var workshop=FindFirstObjectByType<ReefWorkshop>();
            if(workshop&&!workshop.GetComponent<ReefPalmMenu>())workshop.gameObject.AddComponent<ReefPalmMenu>();
        }
        void Awake(){Instance=this;}
        void LateUpdate()
        {
            var card=DiveCardMock.Instance;
            if(!card)return;
#if UNITY_EDITOR
            if(!UnityEngine.XR.XRSettings.isDeviceActive&&Keyboard.current?.f6Key.wasPressedThisFrame==true)SetEditorPreview(!EditorPreview);
            if(EditorPreview)return;
#endif
            var view=Camera.main;
            if(!view||!ReefInput.HandsMode)
            {
                if(openedByPalm)card.SetOpen(false);
                openedByPalm=false;holdTime=releaseTime=0;armed=false;return;
            }
            var left=ReefHands.Get(0);
            if(!left.Tracked)
            {
                holdTime=releaseTime=0;armed=false;return;
            }
            bool gesture=left.MiddlePinch&&!ReefInput.HandBusy(0);
            if(!gesture)
            {
                holdTime=0;
                releaseTime+=Time.unscaledDeltaTime;
                if(releaseTime>=.15f)armed=true;
                return;
            }
            releaseTime=0;
            if(!armed||!card.Available)return;
            holdTime+=Time.unscaledDeltaTime;
            if(holdTime>=.5f)
            {
                card.SetOpen(!card.IsOpen);
                openedByPalm=card.IsOpen;
                armed=false;holdTime=0;
            }
            // Keep the page open on release; require a fresh gesture before toggling again.
        }
        void OnDisable(){if(openedByPalm&&DiveCardMock.Instance)DiveCardMock.Instance.SetOpen(false);openedByPalm=false;}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
