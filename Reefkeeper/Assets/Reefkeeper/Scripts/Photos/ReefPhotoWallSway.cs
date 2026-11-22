using System;
using UnityEngine;
using Reefkeeper.Hands;

namespace Reefkeeper.Photos
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-30)]
    public sealed class ReefPhotoWallSway : MonoBehaviour
    {
        [Serializable] public sealed class Vine
        {
            public Transform pivot;
            public Vector3 localTip;
            [NonSerialized] public Vector2 angle, velocity;
            [NonSerialized] public Quaternion rest;
            [NonSerialized] public bool moving;
        }
        public Vine[] vines = Array.Empty<Vine>();
        [Range(1,15)] public float maximumBend=8f;
        [Min(1)] public float spring=25f,damping=3.8f;
        [Min(0)] public float touchStrength=65f;
        bool focused=true,initialized;
        readonly Vector3[] previousPalm=new Vector3[2],previousTip=new Vector3[2];
        readonly bool[] tracked=new bool[2],armed=new bool[2];
        public bool Moving {get {foreach(var v in vines)if(v.moving)return true;return false;}}
        void Awake(){foreach(var v in vines)if(v.pivot)v.rest=v.pivot.localRotation;initialized=true;}
        void OnApplicationFocus(bool focus){focused=focus;ResetHands();}
        void ResetHands(){for(int i=0;i<2;i++){tracked[i]=false;armed[i]=false;}}
        void OnDisable()
        {
            ResetHands();if(!initialized)return;
            foreach(var v in vines){v.angle=v.velocity=Vector2.zero;v.moving=false;if(v.pivot)v.pivot.localRotation=v.rest;}
        }
        void Update(){SampleHands();StepMotion(Mathf.Min(Time.deltaTime,.05f));}
        void SampleHands()
        {
            if(!focused || !ReefInput.HandsMode || ReefToolMenu.BlocksWorldInput || (ReefPhotoMode.Instance && ReefPhotoMode.Instance.Active))
            {ResetHands();return;}
            var space=ReefInput.HandTrackingSpace;
            if(!space){ResetHands();return;}
            for(int i=0;i<2;i++)
            {
                var hand=ReefHands.Get(i);
                // A lightly curled empty hand can still brush seaweed. Reserve pinches
                // for grabbing/anchoring and the journal, rather than requiring a flat palm.
                bool free=!ReefInput.HandBusy(i)&&!hand.Pinch&&!hand.MiddlePinch&&
                    !(ReefSurfaceHold.Instance&&ReefSurfaceHold.Instance.OwnsHand(i));
                SampleHandContact(i,hand.Tracked,free,space.TransformPoint(hand.Palm.position),
                    space.TransformPoint(hand.IndexTip.position),Time.deltaTime);
            }
        }
        // A fresh open-hand contact supplies one impulse, rather than a force every frame.
        public void SampleHand(int hand,bool valid,bool free,Vector3 palm,float dt)
            => SampleHandContact(hand,valid,free,palm,palm,dt);

        public void SampleHandContact(int hand,bool valid,bool free,Vector3 palm,Vector3 tip,float dt)
        {
            if(hand<0||hand>1)return;
            if(!valid||!free||dt<=0||dt>.12f){tracked[hand]=false;armed[hand]=false;return;}
            float distance=Mathf.Min(DistanceToFrame(palm),DistanceToFrame(tip));
            if(!tracked[hand])
            {previousPalm[hand]=palm;previousTip[hand]=tip;tracked[hand]=true;armed[hand]=distance>.12f;return;}
            Vector3 oldPalm=previousPalm[hand],oldTip=previousTip[hand];
            Vector3 palmMotion=(palm-oldPalm)/dt,tipMotion=(tip-oldTip)/dt;
            previousPalm[hand]=palm;previousTip[hand]=tip;
            if(palmMotion.sqrMagnitude>36f||tipMotion.sqrMagnitude>36f)
            {armed[hand]=false;return;} // tracking discontinuity, not a poke
            if(!armed[hand]){if(distance>.12f)armed[hand]=true;return;}
            // Sweep both probes so a quick pass through a narrow vine cannot skip contact.
            Vector3 motion=tipMotion;
            bool contact=ContactAlong(oldTip,tip,.035f,out var at);
            if(!contact){motion=palmMotion;contact=ContactAlong(oldPalm,palm,.045f,out at);}
            if(!contact)return;
            armed[hand]=false;
            if(motion.sqrMagnitude>.0064f)Push(at,Vector3.ClampMagnitude(motion,1.5f));
        }
        bool ContactAlong(Vector3 from,Vector3 to,float margin,out Vector3 point)
        {
            point=to;
            if(DistanceToFrame(to)<=margin)return true;
            float travel=Vector3.Distance(from,to);
            if(DistanceToFrame(from)>travel+margin)return false;
            int steps=Mathf.Clamp(Mathf.CeilToInt(travel/.03f),1,24);
            for(int i=0;i<steps;i++)
            {
                point=Vector3.Lerp(from,to,(float)i/steps);
                if(DistanceToFrame(point)<=margin)return true;
            }
            return false;
        }
        // The fixed wooden frame is deliberately excluded from contact detection.
        int Nearest(Vector3 point,out float distance)
        {
            int found=-1;distance=float.PositiveInfinity;
            for(int i=0;i<vines.Length;i++)
            {
                var v=vines[i];if(!v.pivot)continue;
                var a=v.pivot.position;var b=v.pivot.TransformPoint(v.localTip);var ab=b-a;
                var closest=a+ab*Mathf.Clamp01(Vector3.Dot(point-a,ab)/Mathf.Max(ab.sqrMagnitude,.00001f));
                float d=Vector3.Distance(point,closest)-.025f*Mathf.Abs(v.pivot.lossyScale.x);
                if(d<distance){distance=d;found=i;}
            }
            return found;
        }
        public float DistanceToFrame(Vector3 point){Nearest(point,out float d);return d;}
        public void Push(Vector3 point,Vector3 worldVelocity)
        {
            if(!isActiveAndEnabled)return;
            int i=Nearest(point,out float distance);if(i<0||distance>.06f)return;
            var v=vines[i];var local=transform.InverseTransformDirection(worldVelocity);
            float lever=Mathf.Clamp01(Vector3.Distance(point,v.pivot.position)/Mathf.Max(.1f,v.localTip.magnitude));
            v.velocity+=new Vector2(-local.z,local.x)*touchStrength*Mathf.Lerp(.35f,1,lever);
            v.velocity=Vector2.ClampMagnitude(v.velocity,90);v.moving=true;
        }
        public void StepMotion(float dt)
        {
            if(dt<=0)return;dt=Mathf.Min(dt,.1f);
            int steps=Mathf.CeilToInt(dt/.008f);float step=dt/steps;
            foreach(var v in vines)
            {
                if(!v.pivot||!v.moving)continue;
                for(int k=0;k<steps;k++)
                {
                    v.velocity+=(-spring*v.angle-damping*v.velocity)*step;v.angle+=v.velocity*step;
                    if(v.angle.magnitude>maximumBend){v.angle=Vector2.ClampMagnitude(v.angle,maximumBend);v.velocity*=.6f;}
                }
                if(v.angle.sqrMagnitude<.0004f&&v.velocity.sqrMagnitude<.0025f){v.angle=v.velocity=Vector2.zero;v.moving=false;}
                v.pivot.localRotation=v.rest*Quaternion.Euler(v.angle.x,0,v.angle.y);
            }
        }
    }
}
