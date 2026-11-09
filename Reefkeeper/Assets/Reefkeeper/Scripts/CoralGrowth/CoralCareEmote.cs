using UnityEngine;

namespace Reefkeeper.Corals
{
    [DisallowMultipleComponent]
    public sealed class CoralCareEmote : MonoBehaviour
    {
        const float CryAfterSeconds = 8f;
        const float SmileSeconds = 0.65f;
        const float HeartSeconds = 1.4f;
        const float BubbleSize = 0.17f;

        enum Mood { None, Blocked, Crying, Fertilize, Smile, Heart }

        static Sprite[] icons;
        CoralNurture nurture;
        SpriteRenderer bubble;
        Mood shown;
        float blockedTime, lastCleaned, smileLeft, heartLeft, opacity, popAge;
        bool wasBlocked;
        Transform stage;
        float stageTopOffset;

        public void Bind(CoralNurture care)
        {
            nurture = care;
            icons ??= new[]
            {
                null, Load("Blocked"), Load("Crying"), Load("Fertilize"), Load("Smile"), Load("Heart"),
            };
            var visual = new GameObject("Coral care emote");
            visual.transform.SetParent(transform, false);
            bubble = visual.AddComponent<SpriteRenderer>();
            bubble.enabled = false;
        }

        static Sprite Load(string name) => Resources.Load<Sprite>("CoralEmotes/" + name);

        void LateUpdate()
        {
            if (!nurture || !bubble) return;
            float dt = Time.deltaTime;
            var patch = nurture.socket ? nurture.socket.moss : null;
            bool blocked = nurture.Paused && !nurture.FullyGrown;
            float cleaned = patch ? patch.Cleaned : 0f;

            smileLeft -= dt;
            heartLeft -= dt;
            blockedTime = blocked ? blockedTime + dt : 0f;
            if (patch && cleaned > lastCleaned && (blocked || wasBlocked))
            {
                if (blocked) smileLeft = SmileSeconds;
                else if (patch.Complete) heartLeft = HeartSeconds;
            }
            lastCleaned = cleaned;
            wasBlocked = blocked;

            Mood mood = Mood.None;
            if (blocked)
            {
                if (smileLeft > 0f) mood = Mood.Smile;
                else mood = blockedTime >= CryAfterSeconds ? Mood.Crying : Mood.Blocked;
            }
            else if (heartLeft > 0f) mood = Mood.Heart;
            else if (nurture.Ready) mood = Mood.Fertilize;

            if (mood != Mood.None && mood != shown)
            {
                shown = mood;
                bubble.sprite = icons[(int)mood];
                popAge = 0f;
            }
            float targetOpacity = mood == Mood.Heart ? Mathf.Clamp01(heartLeft / 0.45f)
                : mood == Mood.None ? 0f : 1f;
            opacity = Mathf.MoveTowards(opacity, targetOpacity, dt * 7f);
            if (opacity <= 0f) shown = Mood.None;
            var camera = Camera.main;
            bubble.enabled = opacity > 0f && bubble.sprite && camera;
            if (!bubble.enabled) return;

            popAge += dt;
            PlaceBubble(camera.transform);
            bubble.color = new Color(1f, 1f, 1f, opacity);
        }

        void PlaceBubble(Transform viewer)
        {
            var current = nurture.coral.Current;
            if (stage != current)
            {
                stage = current;
                stageTopOffset = 0f;
                if (stage)
                {
                    foreach (var mesh in stage.GetComponentsInChildren<MeshRenderer>())
                        stageTopOffset = Mathf.Max(stageTopOffset, mesh.bounds.max.y - transform.position.y);
                }
            }

            float phase = Time.time * 2.2f + transform.position.x;
            float drift = Mathf.Sin(phase) * 0.014f;
            float rise = shown == Mood.Heart ? Mathf.Clamp01(popAge / HeartSeconds) * 0.09f : 0f;
            var visual = bubble.transform;
            visual.position = transform.position + Vector3.up * (stageTopOffset + 0.13f + drift + rise);
            var forward = visual.position - viewer.position;
            if (forward.sqrMagnitude > 0.0001f)
                visual.rotation = Quaternion.LookRotation(forward, viewer.up)
                    * Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * 4f);

            float t = Mathf.Clamp01(popAge / 0.28f);
            float pop = Mathf.Lerp(0.55f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.16f;
            float size = BubbleSize * pop * (1f + Mathf.Sin(phase) * 0.045f) / bubble.sprite.bounds.size.x;
            Vector3 parentScale = transform.lossyScale;
            visual.localScale = new Vector3(size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
        }
    }
}
