using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Reefkeeper.Hands
{
    // PICO's XR eye-gaze feature names, read without a hard platform assembly dependency.
    // No head-forward substitute: callers can fall back to their existing hand pointer.
    public static class ReefEyeGaze
    {
        static readonly List<InputDevice> devices=new List<InputDevice>();
        static readonly InputFeatureUsage<bool> valid=new InputFeatureUsage<bool>("isEyeGazeTracked");
        static readonly InputFeatureUsage<Vector3> position=new InputFeatureUsage<Vector3>("eyeGazePosition");
        static readonly InputFeatureUsage<Quaternion> rotation=new InputFeatureUsage<Quaternion>("eyeGazeRotation");
        static InputDevice device;
        static float searchAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){device=default;searchAt=0;devices.Clear();}
        public static bool TryGetRay(Transform trackingSpace,out Ray ray)
        {
            ray=default;
            if(!trackingSpace || !Application.isFocused)return false;
            if(!device.isValid)
            {
                if(Time.unscaledTime<searchAt)return false;
                searchAt=Time.unscaledTime+.5f;
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking|InputDeviceCharacteristics.HeadMounted,devices);
                if(devices.Count==0)return false;
                device=devices[0];
            }
            if(!device.TryGetFeatureValue(valid,out bool tracked)||!tracked ||
                !device.TryGetFeatureValue(position,out var at)||!device.TryGetFeatureValue(rotation,out var facing))return false;
            return TryMakeRay(trackingSpace,at,facing,out ray);
        }
        public static bool TryMakeRay(Transform space,Vector3 at,Quaternion facing,out Ray ray)
        {
            ray=default;
            if(!space || !Finite(at.x)||!Finite(at.y)||!Finite(at.z)||!Finite(facing.x)||!Finite(facing.y)||!Finite(facing.z)||!Finite(facing.w))return false;
            float length=facing.x*facing.x+facing.y*facing.y+facing.z*facing.z+facing.w*facing.w;
            if(length<.5f||length>1.5f)return false;
            ray=new Ray(space.TransformPoint(at),space.TransformDirection(facing.normalized*Vector3.forward).normalized);
            return true;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    }
}
