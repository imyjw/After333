using System;
using TMPro;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.HUD
{
    public sealed class ResourceBarTextView : MonoBehaviour
    {
        private const int CurrentResourceLayoutVersion = 3;
        private const string ManaIconResourcePath = "Project333/BattleUI/ResourceIcons/Mana";
        private const string QiIconResourcePath = "Project333/BattleUI/ResourceIcons/Qi";
        private const string PowerIconResourcePath = "Project333/BattleUI/ResourceIcons/Power";
        private const string GoldIconResourcePath = "Project333/BattleUI/ResourceIcons/Gold";

        private static readonly ResourceSlotDefinition[] ResourceSlots =
        {
            new ResourceSlotDefinition("ManaResource", ManaIconResourcePath, ResourceKind.Mana),
            new ResourceSlotDefinition("QiResource", QiIconResourcePath, ResourceKind.Qi),
            new ResourceSlotDefinition("PowerResource", PowerIconResourcePath, ResourceKind.Power),
            new ResourceSlotDefinition("GoldResource", GoldIconResourcePath, ResourceKind.Gold),
        };

        [SerializeField] private TMP_Text _turnText;
        [SerializeField] private TMP_Text _playerResourceText;
        [SerializeField] private TMP_Text _aiResourceText;

        [Header("Resource Icon Layout Defaults")]
        [Tooltip("새 자원 UI를 생성할 때 첫 아이콘 묶음이 시작되는 X 위치입니다.")]
        [SerializeField] private float _firstResourceOffsetX;
        [Tooltip("새 자원 UI를 생성할 때 각 자원 아이콘 묶음 사이의 간격입니다.")]
        [SerializeField] private float _resourceSpacing = 95f;
        [Tooltip("새 자원 UI를 생성할 때 사용하는 아이콘 크기입니다.")]
        [SerializeField] private Vector2 _resourceIconSize = new Vector2(38f, 38f);
        [Tooltip("새 자원 UI를 생성할 때 사용하는 숫자 영역 크기입니다.")]
        [SerializeField] private Vector2 _resourceValueSize = new Vector2(52f, 48f);
        [Tooltip("타일, 유닛, 카드 연출보다 자원 UI가 앞에 표시되도록 하는 Canvas 순위입니다.")]
        [SerializeField] private int _resourceCanvasSortingOrder = 25000;
        [SerializeField, HideInInspector] private int _resourceLayoutVersion;
#if UNITY_EDITOR
        private bool _editorMaterializationQueued;
#endif

        private enum ResourceKind
        {
            Mana,
            Qi,
            Power,
            Gold,
        }

        private readonly struct ResourceSlotDefinition
        {
            public ResourceSlotDefinition(string objectName, string resourcePath, ResourceKind kind)
            {
                ObjectName = objectName;
                ResourcePath = resourcePath;
                Kind = kind;
            }

            public string ObjectName { get; }

            public string ResourcePath { get; }

            public ResourceKind Kind { get; }
        }

        private readonly struct ResourceValues
        {
            public ResourceValues(int mana, int qi, int power, int gold)
            {
                Mana = mana;
                Qi = qi;
                Power = power;
                Gold = gold;
            }

            public int Mana { get; }

            public int Qi { get; }

            public int Power { get; }

            public int Gold { get; }

            public int Get(ResourceKind kind)
            {
                return kind switch
                {
                    ResourceKind.Mana => Mana,
                    ResourceKind.Qi => Qi,
                    ResourceKind.Power => Power,
                    ResourceKind.Gold => Gold,
                    _ => 0,
                };
            }
        }

        private void Awake()
        {
            EnsureResourceIconRows(allowCreationInEditMode: false);
        }

        private void OnEnable()
        {
            EnsureResourceIconRows(allowCreationInEditMode: false);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorMaterialization();
#endif
        }

        public void SetResourceSummary(string turnSummary, string playerResourceSummary, string aiResourceSummary)
        {
            if (_turnText != null)
            {
                _turnText.text = BattleRichTextStyler.StyleTurnSummary(turnSummary);
            }

            if (_playerResourceText != null)
            {
                _playerResourceText.text = string.Empty;
                PresentResourceValues(_playerResourceText, ParseResourceSummary(playerResourceSummary));
            }

            if (_aiResourceText != null)
            {
                _aiResourceText.text = string.Empty;
                PresentResourceValues(_aiResourceText, ParseResourceSummary(aiResourceSummary));
            }
        }

        [ContextMenu("Ensure Editable Resource Icon Rows")]
        public void EnsureEditableResourceIconRows()
        {
            EnsureResourceIconRows(allowCreationInEditMode: true);

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                PresentResourceValues(_playerResourceText, new ResourceValues(0, 0, 0, 0));
                PresentResourceValues(_aiResourceText, new ResourceValues(0, 0, 0, 0));
                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        private static void ClearOwnerLabel(TMP_Text ownerText)
        {
            if (ownerText != null)
            {
                // Keep this object active: the resource icons and values are its children.
                ownerText.text = string.Empty;
            }
        }

        private void EnsureResourceIconRows(bool allowCreationInEditMode)
        {
            if (!UnityEngine.Application.isPlaying && !allowCreationInEditMode)
            {
                return;
            }

            EnsureResourceIconRow(_playerResourceText);
            EnsureResourceIconRow(_aiResourceText);
            ClearOwnerLabel(_playerResourceText);
            ClearOwnerLabel(_aiResourceText);

            if (_resourceLayoutVersion < 2)
            {
                ApplyCornerResourceLayout();
            }
            if (_resourceLayoutVersion < CurrentResourceLayoutVersion)
            {
                ApplyValueHorizontalPosition(_playerResourceText);
                ApplyValueHorizontalPosition(_aiResourceText);
                _resourceLayoutVersion = CurrentResourceLayoutVersion;
            }
        }

        private static void ApplyValueHorizontalPosition(TMP_Text ownerText)
        {
            if (ownerText == null) return;
            foreach (var slot in ResourceSlots)
            {
                if (ownerText.transform.Find($"{slot.ObjectName}/Value") is not RectTransform valueRect) continue;
                var position = valueRect.anchoredPosition;
                position.x = 43f;
                valueRect.anchoredPosition = position;
            }
        }

        private void ApplyCornerResourceLayout()
        {
            ApplyCornerRow(_playerResourceText, false);
            ApplyCornerRow(_aiResourceText, true);
            _firstResourceOffsetX = 0f;
        }

        private static void ApplyCornerRow(TMP_Text ownerText, bool top)
        {
            if (ownerText == null) return;

            var canvas = ownerText.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas != null && canvas.transform != ownerText.transform)
            {
                var safeRoot = canvas.transform.Find("BattleResourcesSafeArea") as RectTransform;
                if (safeRoot == null)
                {
                    var safeObject = new GameObject("BattleResourcesSafeArea", typeof(RectTransform));
                    safeObject.layer = ownerText.gameObject.layer;
                    safeRoot = safeObject.GetComponent<RectTransform>();
                    safeRoot.SetParent(canvas.transform, false);
                    safeObject.AddComponent<SafeAreaFitter>().Configure();
#if UNITY_EDITOR
                    if (!UnityEngine.Application.isPlaying)
                        Undo.RegisterCreatedObjectUndo(safeObject, "Place Resource UI In Safe Area");
#endif
                }
                ownerText.rectTransform.SetParent(safeRoot, false);
            }

            // Migrate once; subsequent refreshes preserve Inspector positioning edits.
            var rect = ownerText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, top ? 1f : 0f);
            rect.anchoredPosition = new Vector2(24f, top ? -24f : 24f);
            rect.localScale = Vector3.one;
            var first = ownerText.transform.Find(ResourceSlots[0].ObjectName) as RectTransform;
            var oldOffsetX = first != null ? first.anchoredPosition.x : 0f;
            var width = 0f;
            var height = 0f;
            foreach (var slot in ResourceSlots)
            {
                if (ownerText.transform.Find(slot.ObjectName) is not RectTransform group) continue;
                group.anchoredPosition = new Vector2(group.anchoredPosition.x - oldOffsetX, 0f);
                width = Mathf.Max(width, group.anchoredPosition.x + group.rect.width);
                height = Mathf.Max(height, group.rect.height);
            }
            rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        private void EnsureResourceIconRow(TMP_Text ownerText)
        {
            if (ownerText == null)
            {
                return;
            }

            EnsureResourceCanvas(ownerText);

            for (var slotIndex = 0; slotIndex < ResourceSlots.Length; slotIndex++)
            {
                var slot = ResourceSlots[slotIndex];
                var group = ownerText.transform.Find(slot.ObjectName) as RectTransform;
                if (group == null)
                {
                    group = CreateResourceGroup(ownerText, slot, slotIndex);
                }

                EnsureIconTexture(group, slot.ResourcePath);
            }
        }

        private void EnsureResourceCanvas(TMP_Text ownerText)
        {
            var canvas = ownerText.GetComponent<Canvas>();
            if (canvas == null)
            {
#if UNITY_EDITOR
                canvas = !UnityEngine.Application.isPlaying
                    ? Undo.AddComponent<Canvas>(ownerText.gameObject)
                    : ownerText.gameObject.AddComponent<Canvas>();
#else
                canvas = ownerText.gameObject.AddComponent<Canvas>();
#endif
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = _resourceCanvasSortingOrder;
        }

        private RectTransform CreateResourceGroup(
            TMP_Text ownerText,
            ResourceSlotDefinition slot,
            int slotIndex)
        {
            var groupObject = new GameObject(slot.ObjectName, typeof(RectTransform));
            groupObject.layer = ownerText.gameObject.layer;
            groupObject.transform.SetParent(ownerText.transform, false);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(groupObject, "Create Resource Icon UI");
            }
#endif

            var groupRect = groupObject.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0f, 0.5f);
            groupRect.anchorMax = new Vector2(0f, 0.5f);
            groupRect.pivot = new Vector2(0f, 0.5f);
            groupRect.anchoredPosition = new Vector2(
                _firstResourceOffsetX + (_resourceSpacing * slotIndex),
                0f);
            groupRect.sizeDelta = new Vector2(
                _resourceIconSize.x + _resourceValueSize.x,
                Mathf.Max(_resourceIconSize.y, _resourceValueSize.y));

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            iconObject.layer = ownerText.gameObject.layer;
            iconObject.transform.SetParent(groupRect, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = _resourceIconSize;
            var icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            icon.color = Color.white;

            var valueObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            valueObject.layer = ownerText.gameObject.layer;
            valueObject.transform.SetParent(groupRect, false);
            var valueRect = valueObject.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0f, 0.5f);
            valueRect.anchorMax = new Vector2(0f, 0.5f);
            valueRect.pivot = new Vector2(0f, 0.5f);
            valueRect.anchoredPosition = new Vector2(43f, 0f);
            valueRect.sizeDelta = _resourceValueSize;

            var valueText = valueObject.GetComponent<TextMeshProUGUI>();
            CopyTextStyle(ownerText, valueText);
            valueText.text = "0";
            valueText.alignment = TextAlignmentOptions.MidlineLeft;
            valueText.raycastTarget = false;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;

            return groupRect;
        }

        private static void CopyTextStyle(TMP_Text source, TMP_Text destination)
        {
            destination.font = source.font;
            destination.fontSharedMaterial = source.fontSharedMaterial;
            destination.fontSize = source.fontSize;
            destination.fontStyle = source.fontStyle;
            destination.color = source.color;
            destination.richText = true;
        }

        private static void EnsureIconTexture(RectTransform group, string resourcePath)
        {
            if (group == null)
            {
                return;
            }

            var icon = group.Find("Icon")?.GetComponent<RawImage>();
            if (icon == null || icon.texture != null)
            {
                return;
            }

            icon.texture = Resources.Load<Texture2D>(resourcePath);
        }

        private static void PresentResourceValues(TMP_Text ownerText, ResourceValues values)
        {
            if (ownerText == null)
            {
                return;
            }

            for (var slotIndex = 0; slotIndex < ResourceSlots.Length; slotIndex++)
            {
                var slot = ResourceSlots[slotIndex];
                var valueText = ownerText.transform
                    .Find($"{slot.ObjectName}/Value")
                    ?.GetComponent<TMP_Text>();
                if (valueText != null)
                {
                    valueText.text = values.Get(slot.Kind).ToString();
                }
            }
        }

        private static ResourceValues ParseResourceSummary(string summary)
        {
            var mana = 0;
            var qi = 0;
            var power = 0;
            var gold = 0;

            if (!string.IsNullOrWhiteSpace(summary))
            {
                var tokens = summary.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    var separatorIndex = token.IndexOf(':');
                    if (separatorIndex <= 0 ||
                        !int.TryParse(token.Substring(separatorIndex + 1), out var value))
                    {
                        continue;
                    }

                    switch (char.ToUpperInvariant(token[0]))
                    {
                        case 'M':
                            mana = value;
                            break;
                        case 'Q':
                            qi = value;
                            break;
                        case 'P':
                            power = value;
                            break;
                        case 'G':
                            gold = value;
                            break;
                    }
                }
            }

            return new ResourceValues(mana, qi, power, gold);
        }

#if UNITY_EDITOR
        private void QueueEditorMaterialization()
        {
            if (_editorMaterializationQueued || UnityEngine.Application.isPlaying)
            {
                return;
            }

            _editorMaterializationQueued = true;
            EditorApplication.delayCall += MaterializeResourceIconRowsInEditor;
        }

        private void MaterializeResourceIconRowsInEditor()
        {
            _editorMaterializationQueued = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureEditableResourceIconRows();
        }
#endif
    }
}
