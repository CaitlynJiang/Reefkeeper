using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif
namespace Reefkeeper.Hands
{
    /// <summary>Left palm gesture opens the full overlay journal, not a second menu.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class ReefPalmMenu : MonoBehaviour
    {
        public static ReefPalmMenu Instance { get; private set; }
        public bool Visible => DiveCardMock.Instance && DiveCardMock.Instance.IsOpen;
        float upTime,downTime;
        bool openedByPalm,armed=true;
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
                openedByPalm=false;upTime=downTime=0;return;
            }
            var left=ReefHands.Get(0);
            Vector3 normal=view.transform.parent.TransformDirection(left.Palm.rotation*Vector3.down);
            bool up=left.Tracked&&!left.Grab&&!ReefInput.HandBusy(0)&&Vector3.Dot(normal,Vector3.up)>(openedByPalm?.5f:.75f);
            upTime=up?upTime+Time.unscaledDeltaTime:0;
            downTime=up?0:downTime+Time.unscaledDeltaTime;
            if(downTime>=.3f)
            {
                if(openedByPalm)card.SetOpen(false);
                openedByPalm=false;armed=true;
            }
            if(armed&&!Visible&&card.Available&&upTime>=.3f)
            {card.SetOpen(true);openedByPalm=card.IsOpen;armed=false;}
            // Closing with X/Esc must not reopen until the player lowers and raises the palm again.
        }
        void OnDisable(){if(openedByPalm&&DiveCardMock.Instance)DiveCardMock.Instance.SetOpen(false);openedByPalm=false;}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
