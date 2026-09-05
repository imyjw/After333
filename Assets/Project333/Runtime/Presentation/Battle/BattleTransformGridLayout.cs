using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleTransformGridLayout : MonoBehaviour
    {
        [Min(1)]
        [SerializeField] private int _columns = 5;
        [SerializeField] private Vector2 _cellStep = new Vector2(220f, 120f);
        [SerializeField] private Vector2 _originOffset = Vector2.zero;
        [SerializeField] private bool _centerGrid = true;
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
            if (_columns <= 0)
            {
                _columns = 1;
            }

            var childCount = transform.childCount;
            if (childCount == 0)
            {
                return;
            }

            var rows = Mathf.CeilToInt(childCount / (float)_columns);
            var gridWidth = (_columns - 1) * _cellStep.x;
            var gridHeight = (rows - 1) * _cellStep.y;
            var baseOffset = new Vector3(_originOffset.x, _originOffset.y, 0f);

            if (_centerGrid)
            {
                baseOffset += new Vector3(-gridWidth * 0.5f, gridHeight * 0.5f, 0f);
            }

            for (var i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                var column = i % _columns;
                var row = i / _columns;
                child.localPosition = baseOffset + new Vector3(column * _cellStep.x, -row * _cellStep.y, 0f);
            }
        }
    }
}
