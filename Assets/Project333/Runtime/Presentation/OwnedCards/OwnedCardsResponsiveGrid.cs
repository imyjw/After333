using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.OwnedCards
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(GridLayoutGroup))]
    public sealed class OwnedCardsResponsiveGrid : MonoBehaviour
    {
        [SerializeField] private Vector2 _maximumCellSize = new Vector2(240f, 355f);
        [SerializeField] private Vector2 _spacing = new Vector2(16f, 24f);

        private void LateUpdate()
        {
            var grid = GetComponent<GridLayoutGroup>();
            var rect = ((RectTransform)transform).rect;
            var width = Mathf.Max(1f, rect.width - grid.padding.horizontal);
            var height = Mathf.Max(1f, rect.height - grid.padding.vertical);
            var maximum = Vector2.Max(Vector2.one, _maximumCellSize);
            var gap = Vector2.Max(Vector2.zero, _spacing);
            var scale = Mathf.Min(1f, width / (6f * maximum.x + 5f * gap.x),
                height / (2f * maximum.y + gap.y));
            var size = maximum * Mathf.Max(0.001f, scale);
            var spacing = gap * Mathf.Max(0.001f, scale);
            if ((grid.cellSize - size).sqrMagnitude < 0.01f && (grid.spacing - spacing).sqrMagnitude < 0.01f)
                return;
            grid.cellSize = size;
            grid.spacing = spacing;
        }
    }
}
