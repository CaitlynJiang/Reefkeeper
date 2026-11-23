using System.Linq;
using UnityEngine;

namespace Reefkeeper.Corals
{
    // Automatic cueing after a record seats; the headshell is no longer a grab target.
    [DisallowMultipleComponent]
    public sealed class VinylTonearm : MonoBehaviour
    {
        public VinylPlayer player;
        public Transform stylus,handle;
        [Range(10,60)] public float playAngle=35f;
        [Min(.1f)] public float animationSeconds=.9f;
        [Min(0)] public float liftHeight=.035f;
        enum Phase { Parked, RaiseToPlay, SwingToPlay, LowerToPlay, Playing, RaiseToPark, SwingToPark, LowerToPark }
        Phase phase;
        Quaternion rest;
        Vector3 restNeedle,flatNeedle,pitchAxis;
        float yaw,cueAngle,fromYaw,fromCue,elapsed;
        bool initialized;
        ShopRecord queuedRecord;
        public Vector3 StylusPosition=>stylus?stylus.position:transform.position;
        public float CurrentAngle=>yaw;
        public bool IsCueing=>phase==Phase.RaiseToPlay||phase==Phase.SwingToPlay||phase==Phase.LowerToPlay;

        bool EnsurePose()
        {
            if(initialized)return true;
            if(!stylus||!player)return false;
            rest=transform.localRotation;restNeedle=transform.InverseTransformPoint(stylus.position);
            flatNeedle=Vector3.ProjectOnPlane(restNeedle,Vector3.up);
            pitchAxis=Vector3.Cross(Vector3.up,flatNeedle).normalized;
            initialized=flatNeedle.sqrMagnitude>.0001f;return initialized;
        }
        Vector3 InRestFrame(Vector3 point)
        {
            var local=transform.parent?transform.parent.InverseTransformPoint(point):point;
            local=Quaternion.Inverse(rest)*(local-transform.localPosition);
            return new Vector3(local.x/transform.localScale.x,local.y/transform.localScale.y,local.z/transform.localScale.z);
        }
        float CuePitch(float lift)
        {
            float targetY=InRestFrame(player.RecordSurfacePosition).y+lift/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.y));
            float angle=Mathf.Atan2(restNeedle.y,flatNeedle.magnitude)-Mathf.Asin(Mathf.Clamp(targetY/restNeedle.magnitude,-1,1));
            return Mathf.Clamp(angle*Mathf.Rad2Deg,-18,18);
        }
        void ApplyPose()=>transform.localRotation=rest*Quaternion.AngleAxis(yaw,Vector3.up)*Quaternion.AngleAxis(cueAngle,pitchAxis);
        void Enter(Phase next){phase=next;elapsed=0;fromYaw=yaw;fromCue=cueAngle;}
        public void BeginPlayback()
        {
            if(!EnsurePose()||!player.Seated)return;
            queuedRecord=player.Seated;Enter(Phase.RaiseToPlay);
        }
        public void CancelPlayback()
        {
            queuedRecord=null;
            if(initialized&&phase!=Phase.Parked)Enter(Phase.RaiseToPark);
        }
        void Update()=>StepMotion(Time.deltaTime);
        public void StepMotion(float dt)
        {
            if(!EnsurePose()||dt<=0)return;
            if((IsCueing||phase==Phase.Playing)&&(!queuedRecord||player.Seated!=queuedRecord||queuedRecord.isSelected))CancelPlayback();
            if(phase==Phase.Parked||phase==Phase.Playing)return;
            bool swinging=phase==Phase.SwingToPlay||phase==Phase.SwingToPark;
            float duration=Mathf.Max(.03f,animationSeconds*(swinging?.55f:.225f));
            elapsed+=dt;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
            float toYaw=phase==Phase.SwingToPlay?playAngle:phase==Phase.SwingToPark?0:fromYaw;
            float toCue=phase==Phase.LowerToPlay?CuePitch(.001f):phase==Phase.LowerToPark?0:CuePitch(liftHeight);
            yaw=Mathf.Lerp(fromYaw,toYaw,t);cueAngle=Mathf.Lerp(fromCue,toCue,t);ApplyPose();
            if(elapsed<duration)return;
            switch(phase)
            {
                case Phase.RaiseToPlay:Enter(Phase.SwingToPlay);break;
                case Phase.SwingToPlay:Enter(Phase.LowerToPlay);break;
                case Phase.LowerToPlay:phase=Phase.Playing;player.TryPlayFromTonearm();break;
                case Phase.RaiseToPark:Enter(Phase.SwingToPark);break;
                case Phase.SwingToPark:Enter(Phase.LowerToPark);break;
                case Phase.LowerToPark:phase=Phase.Parked;break;
            }
        }
        void OnDisable()
        {
            queuedRecord=null;phase=Phase.Parked;yaw=cueAngle=0;
            if(initialized)transform.localRotation=rest;
            if(player)player.LiftTonearm();
        }
        public static VinylTonearm Install(GameObject model,VinylPlayer owner)
        {
            var existing=model.GetComponentInChildren<VinylTonearm>(true);
            if(existing)
            {
                existing.player=owner;
                foreach(var c in existing.GetComponentsInChildren<Collider>(true))
                    if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);
                return existing;
            }
            var parts=model.GetComponentsInChildren<Transform>(true);
            var arm=parts.FirstOrDefault(t=>t.name=="Tonearm_Pivot");
            var needle=parts.FirstOrDefault(t=>t.name=="Tonearm_Stylus");
            var head=parts.FirstOrDefault(t=>t.name=="Tonearm_Headshell");
            if(!arm||!needle||!head)return null;
            var root=new GameObject("Tonearm - automatic");root.transform.SetParent(model.transform,false);root.transform.position=arm.position;
            arm.SetParent(root.transform,true);
            var control=root.AddComponent<VinylTonearm>();control.player=owner;control.stylus=needle;control.handle=head;
            return control;
        }
    }
}
