using UnityEngine;

namespace Reefkeeper.Corals
{
    [ExecuteAlways]
    public sealed class LuckyCatEyeExpressions : MonoBehaviour
    {
        public enum Expression { Sparkle, Happy, Blink, Sad }
        public Expression expression;
        public Texture2D[] frames;
        public Renderer[] eyes;

        MaterialPropertyBlock block;
        Expression? applied;
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        void OnEnable() => applied = null;
        void OnValidate() => applied = null;
        void LateUpdate() { if (applied != expression) Apply(); }

        public void SetExpression(Expression value)
        {
            expression = value;
            Apply();
        }

        void Apply()
        {
            if (eyes == null || frames == null || (uint)expression >= (uint)frames.Length) return;
            block ??= new MaterialPropertyBlock();
            foreach (var eye in eyes)
            {
                if (!eye || !eye.sharedMaterial) continue;
                // Each material holds its own painted open eye. Other frames are shared.
                var texture = expression == Expression.Sparkle
                    ? eye.sharedMaterial.GetTexture(BaseMap)
                    : frames[(int)expression];
                if (!texture) continue;
                eye.GetPropertyBlock(block);
                block.SetTexture(BaseMap, texture);
                eye.SetPropertyBlock(block);
            }
            applied = expression;
        }
    }
}
