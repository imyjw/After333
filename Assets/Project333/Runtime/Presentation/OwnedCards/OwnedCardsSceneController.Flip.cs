using System;
using System.Collections;
using System.Globalization;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Runtime.Presentation.OwnedCards
{
    public sealed partial class OwnedCardsSceneController
    {
        private const float DetailArtworkFlipDuration = .36f;
        private const int UpgradeTableRowCount = 14;
        private UnityEngine.UI.Button _detailArtworkFlipButton;
        private RectTransform _detailUpgradeTableRoot;
        private UnityEngine.UI.Text _detailUpgradeTableTitle;
        private UnityEngine.UI.Text _detailUpgradeTableEmpty;
        private UnityEngine.UI.Text[,] _detailUpgradeTableCells;
        private Coroutine _detailArtworkFlipRoutine;
        private int _detailArtworkFlipGeneration;
        private bool _detailArtworkBackVisible;
        private bool _detailFrontShowsStats;
        private bool _detailFrontShowsHp;
        private string _detailArtworkFlipCardId;

        private void EnsureDetailArtworkFlipUi()
        {
            if (_detailArtworkImage == null) return;
            if (_detailArtworkFlipButton != null && _detailUpgradeTableRoot != null && _detailUpgradeTableCells != null)
            {
                ApplyDetailArtworkFace();
                return;
            }
            var artwork = _detailArtworkImage.rectTransform;
            _detailArtworkImage.raycastTarget = false;
            _detailArtworkFlipButton = GalleryComponent<UnityEngine.UI.Button>(artwork);
            _detailArtworkFlipButton.transition = UnityEngine.UI.Selectable.Transition.None;
            _detailArtworkFlipButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            _detailArtworkFlipButton.onClick.RemoveListener(ToggleDetailArtworkFromUi);
            _detailArtworkFlipButton.onClick.AddListener(ToggleDetailArtworkFromUi);

            _detailUpgradeTableRoot = GalleryRect(artwork, "UpgradeTableBack");
            StretchFull(_detailUpgradeTableRoot);
            PaintCollectionPanel(_detailUpgradeTableRoot, Color.black, false);
            _detailUpgradeTableTitle = UpgradeTableLabel(_detailUpgradeTableRoot, "Title", "레벨별 강화 증가량", 24);
            Region(_detailUpgradeTableTitle.rectTransform, .025f, .915f, .975f, .995f);
            var grid = GalleryRect(_detailUpgradeTableRoot, "Grid");
            Region(grid, .025f, .025f, .975f, .91f);
            _detailUpgradeTableCells = new UnityEngine.UI.Text[UpgradeTableRowCount, 4];
            // Four equal columns keep long effect names legible after wrapping to two lines.
            for (var row = 0; row < UpgradeTableRowCount; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    var cell = UpgradeTableLabel(grid, "Cell" + row + "_" + col, string.Empty, row == 0 ? 22 : 24);
                    Region(cell.rectTransform, col * .25f, UpgradeTableBoundary(row + 1),
                        (col + 1f) * .25f, UpgradeTableBoundary(row), 2f);
                    _detailUpgradeTableCells[row, col] = cell;
                }
            }
            for (var row = 0; row <= UpgradeTableRowCount; row++)
            {
                var line = GalleryRect(grid, "Horizontal" + row);
                var y = UpgradeTableBoundary(row);
                line.anchorMin = new Vector2(0f, y);
                line.anchorMax = new Vector2(1f, y);
                line.pivot = new Vector2(.5f, .5f);
                line.anchoredPosition = Vector2.zero;
                line.sizeDelta = new Vector2(0f, 1f);
                PaintCollectionPanel(line, Color.white, false);
            }
            for (var col = 0; col <= 4; col++)
            {
                var line = GalleryRect(grid, "Vertical" + col);
                var x = col * .25f;
                line.anchorMin = new Vector2(x, 0f);
                line.anchorMax = new Vector2(x, 1f);
                line.pivot = new Vector2(.5f, .5f);
                line.anchoredPosition = Vector2.zero;
                line.sizeDelta = new Vector2(1f, 0f);
                PaintCollectionPanel(line, Color.white, false);
            }
            _detailUpgradeTableEmpty = UpgradeTableLabel(_detailUpgradeTableRoot, "EmptyMessage", string.Empty, 24);
            Region(_detailUpgradeTableEmpty.rectTransform, .06f, .15f, .94f, .85f);

            // The original artwork and all labels remain non-raycast graphics. This always-active surface
            // receives clicks even when the selected card has no artwork, and bubbles them to its Button.
            var hitArea = GalleryRect(artwork, "ArtworkFlipHitArea");
            StretchFull(hitArea);
            PaintCollectionPanel(hitArea, Color.clear, true);
            hitArea.SetAsLastSibling();
            _detailArtworkFlipButton.targetGraphic = hitArea.GetComponent<UnityEngine.UI.Image>();
            _detailArtworkFlipButton.interactable = _detailArtworkFlipRoutine == null;
            ApplyDetailArtworkFace();
        }

        private static float UpgradeTableBoundary(int row)
        {
            return row == 0 ? 1f : .88f * (UpgradeTableRowCount - row) / (UpgradeTableRowCount - 1f);
        }

        private UnityEngine.UI.Text UpgradeTableLabel(RectTransform parent, string name, string value, int size)
        {
            var text = GalleryLabel(parent, name, value, size, Color.white, TextAnchor.MiddleCenter);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            text.horizontalOverflow = UnityEngine.HorizontalWrapMode.Wrap;
            text.verticalOverflow = UnityEngine.VerticalWrapMode.Truncate;
            return text;
        }

        private void RefreshDetailUpgradeTable(CardDefinitionAsset cardAsset)
        {
            EnsureDetailArtworkFlipUi();
            if (_detailUpgradeTableRoot == null) return;
            if (!string.Equals(_detailArtworkFlipCardId, _selectedCardId, StringComparison.Ordinal))
            {
                ResetDetailArtworkFlipToFront();
                _detailArtworkFlipCardId = _selectedCardId;
            }
            var table = OwnedCardsUpgradeTableData.Build(cardAsset != null ? ResolveCurrentDefinition(cardAsset) : null);
            _detailUpgradeTableTitle.text = table.Title;
            var hasRows = table.Rows.Count != 0;
            _detailUpgradeTableRoot.Find("Grid").gameObject.SetActive(hasRows);
            _detailUpgradeTableEmpty.gameObject.SetActive(!hasRows);
            _detailUpgradeTableEmpty.text = table.EmptyMessage;
            for (var col = 0; col < 4; col++)
            {
                // A single wrapped header identifies the spell effect; all 13 row values stay compact.
                _detailUpgradeTableCells[0, col].text = col == 3 ? WrapUpgradeEffectHeader(table.ColumnHeaders[col]) : table.ColumnHeaders[col];
                for (var row = 1; row < UpgradeTableRowCount; row++)
                {
                    var value = row <= table.Rows.Count ? table.Rows[row - 1] : null;
                    _detailUpgradeTableCells[row, col].text = value == null ? string.Empty : col == 0
                        ? "Lv." + value.Level.ToString(CultureInfo.InvariantCulture)
                        : col == 1 ? value.HpText : col == 2 ? value.AttackText : value.EffectText;
                }
            }
            ApplyDetailArtworkFace();
        }

        private static string WrapUpgradeEffectHeader(string header)
        {
            var separator = header.IndexOf(' ');
            return separator > 0 ? header.Substring(0, separator) + "\n" + header.Substring(separator + 1) : header;
        }

        private void ToggleDetailArtworkFromUi()
        {
            if (!_isCardDetailOpen || _detailArtworkImage == null || _detailArtworkFlipRoutine != null) return;
            var showBack = !_detailArtworkBackVisible;
            if (IsTutorialCollectionMode && !TutorialCollectionActionAllowed(showBack ? TutorialAction.ShowUpgradeTable : TutorialAction.RestoreCardArtwork)) return;
            if (!UnityEngine.Application.isPlaying)
            {
                _detailArtworkBackVisible = showBack;
                ApplyDetailArtworkFace();
                NotifyTutorialFlipCompleted(showBack);
                return;
            }
            _detailArtworkFlipButton.interactable = false;
            var generation = ++_detailArtworkFlipGeneration;
            _detailArtworkFlipRoutine = StartCoroutine(AnimateDetailArtworkFlip(showBack, generation, _selectedCardId));
        }

        private IEnumerator AnimateDetailArtworkFlip(bool showBack, int generation, string cardId)
        {
            var started = Time.unscaledTime;
            while (true)
            {
                if (generation != _detailArtworkFlipGeneration || !_isCardDetailOpen ||
                    !string.Equals(cardId, _selectedCardId, StringComparison.Ordinal) ||
                    _detailArtworkImage == null || !_detailArtworkImage.gameObject.activeInHierarchy) yield break;
                var progress = Mathf.Clamp01((Time.unscaledTime - started) / DetailArtworkFlipDuration);
                SetDetailArtworkFlipProgress(progress, showBack);
                if (progress >= 1f) break;
                yield return null;
            }
            _detailArtworkImage.rectTransform.localScale = Vector3.one;
            _detailArtworkFlipRoutine = null;
            _detailArtworkFlipButton.interactable = true;
            NotifyTutorialFlipCompleted(showBack);
        }

        private void SetDetailArtworkFlipProgress(float progress, bool showBack)
        {
            var secondHalf = progress >= .5f;
            _detailArtworkBackVisible = secondHalf ? showBack : !showBack;
            ApplyDetailArtworkFace();
            var halfProgress = secondHalf ? (progress - .5f) * 2f : progress * 2f;
            var width = secondHalf ? Mathf.SmoothStep(0f, 1f, halfProgress) : Mathf.SmoothStep(1f, 0f, halfProgress);
            // Positive scales on both faces prevent reversed text and restore exact dimensions at completion.
            _detailArtworkImage.rectTransform.localScale = new Vector3(width, 1f, 1f);
        }

        private void ApplyDetailArtworkFace()
        {
            if (_detailArtworkImage != null)
                _detailArtworkImage.enabled = !_detailArtworkBackVisible && _detailArtworkImage.sprite != null;
            if (_detailUpgradeTableRoot != null) _detailUpgradeTableRoot.gameObject.SetActive(_detailArtworkBackVisible);
            if (_detailAttackValueText != null) _detailAttackValueText.enabled = !_detailArtworkBackVisible && _detailFrontShowsStats;
            if (_detailHpValueText != null) _detailHpValueText.enabled = !_detailArtworkBackVisible && _detailFrontShowsStats && _detailFrontShowsHp;
        }

        private void ResetDetailArtworkFlipToFront()
        {
            ++_detailArtworkFlipGeneration;
            if (_detailArtworkFlipRoutine != null) StopCoroutine(_detailArtworkFlipRoutine);
            _detailArtworkFlipRoutine = null;
            _detailArtworkBackVisible = false;
            if (_detailArtworkImage != null) _detailArtworkImage.rectTransform.localScale = Vector3.one;
            if (_detailArtworkFlipButton != null) _detailArtworkFlipButton.interactable = true;
            ApplyDetailArtworkFace();
        }
    }
}
