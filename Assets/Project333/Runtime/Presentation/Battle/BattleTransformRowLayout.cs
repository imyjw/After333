using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleTransformRowLayout : MonoBehaviour
    {
        [SerializeField] private float _spacing = 220f;
        [SerializeField] private Vector2 _originOffset = Vector2.zero;
        [SerializeField] private bool _centerRow = true;
        [SerializeField] private bool _applyOnAwake = true;

        private void Awake()
        {
            if (_applyOnAwake)
            {
                ApplyLayout();
            }
        }

        private void OnValidate()
        {
            ApplyLayout();
        }

        [ContextMenu("Apply Layout")]
        public void ApplyLayout()
        {
            var childCount = transform.childCount;
            if (childCount == 0)
            {
                return;
            }

            var totalWidth = (childCount - 1) * _spacing;
            var startOffset = new Vector3(_originOffset.x, _originOffset.y, 0f);

            if (_centerRow)
            {
                startOffset += new Vector3(-totalWidth * 0.5f, 0f, 0f);
            }

            for (var i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                child.localPosition = startOffset + new Vector3(i * _spacing, 0f, 0f);
            }
        }
    }
}
