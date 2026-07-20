using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // loadout-preset-page unit 3 — 프리셋 하나를 표현하는 목록 아이템:
    // 이름 + 유닛 셀 7 + 드림캐쳐 아트 10 + 적용 버튼. 읽기전용 표시 + 적용 버튼 하나만 상호작용.
    public class PresetListItemView : MonoBehaviour
    {
        public event Action ApplyClicked;

        private static readonly Color ItemBg = new Color(0.10f, 0.11f, 0.15f, 1f);
        private static readonly Color ApplyOn = new Color(0.16f, 0.5f, 0.28f, 1f); // MenuPopup 재개 버튼 톤

        private static readonly Vector2 UnitCellSize = new Vector2(84, 84);
        private static readonly Vector2 ArtSize = new Vector2(58, 82);
        private const float HeaderHeight = 60f;
        private const float ItemHeight = 270f; // 패딩24 + 헤더60 + 셀84 + 아트82 + 간격16

        private TMP_FontAsset _font;

        public void Build(SquadPreset preset, TMP_FontAsset font)
        {
            _font = font;

            var selfImg = gameObject.GetComponent<Image>();
            if (selfImg == null) selfImg = gameObject.AddComponent<Image>();
            selfImg.color = ItemBg;

            var vlg = gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var itemLe = gameObject.GetComponent<LayoutElement>();
            if (itemLe == null) itemLe = gameObject.AddComponent<LayoutElement>();
            itemLe.minHeight = ItemHeight; itemLe.preferredHeight = ItemHeight;

            BuildHeader(preset);
            BuildUnitRow(preset);
            BuildArtRow(preset);
        }

        private void BuildHeader(SquadPreset preset)
        {
            var header = MakeRow("Header", HeaderHeight, TextAnchor.MiddleLeft, 8);

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(header, false);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            string nm = (preset != null && !string.IsNullOrEmpty(preset.presetName)) ? preset.presetName : "프리셋";
            nameText.text = nm;
            nameText.fontSize = 34; nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft; nameText.enableWordWrapping = false;
            if (_font != null) nameText.font = _font;
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f; nameLe.minHeight = HeaderHeight;

            var applyGo = new GameObject("Apply", typeof(RectTransform), typeof(Image), typeof(Button));
            applyGo.transform.SetParent(header, false);
            applyGo.GetComponent<Image>().color = ApplyOn;
            applyGo.GetComponent<Button>().onClick.AddListener(() => ApplyClicked?.Invoke());
            var applyLe = applyGo.AddComponent<LayoutElement>();
            applyLe.minWidth = 160; applyLe.preferredWidth = 160; applyLe.minHeight = HeaderHeight;
            var applyLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            applyLabel.transform.SetParent(applyGo.transform, false);
            var alrt = (RectTransform)applyLabel.transform;
            alrt.anchorMin = Vector2.zero; alrt.anchorMax = Vector2.one; alrt.offsetMin = Vector2.zero; alrt.offsetMax = Vector2.zero;
            var alt = applyLabel.GetComponent<TextMeshProUGUI>();
            alt.text = "적용"; alt.fontSize = 30; alt.color = Color.white;
            alt.alignment = TextAlignmentOptions.Center; alt.raycastTarget = false;
            if (_font != null) alt.font = _font;
        }

        private void BuildUnitRow(SquadPreset preset)
        {
            var row = MakeRow("Units", UnitCellSize.y, TextAnchor.MiddleLeft, 8);
            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                var cell = PresetUnitCell.Create(row, UnitCellSize);
                var unit = (preset != null && preset.units != null && i < preset.units.Length) ? preset.units[i] : null;
                cell.Set(unit);
                var le = cell.gameObject.AddComponent<LayoutElement>();
                le.minWidth = UnitCellSize.x; le.preferredWidth = UnitCellSize.x;
                le.minHeight = UnitCellSize.y; le.preferredHeight = UnitCellSize.y;
            }
        }

        private void BuildArtRow(SquadPreset preset)
        {
            var row = MakeRow("Cards", ArtSize.y, TextAnchor.MiddleLeft, 6);
            if (preset == null || preset.cards == null) return;
            for (int i = 0; i < preset.cards.Length; i++)
            {
                var card = preset.cards[i];
                if (card == null) continue;
                var go = new GameObject("Art", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(row, false);
                var img = go.GetComponent<Image>();
                img.preserveAspect = true; img.raycastTarget = false;
                if (card.art != null) { img.sprite = card.art; img.color = Color.white; }
                else { img.sprite = null; img.color = CardCategoryStyle.ArtFallback(card); }
                var le = go.AddComponent<LayoutElement>();
                le.minWidth = ArtSize.x; le.preferredWidth = ArtSize.x;
                le.minHeight = ArtSize.y; le.preferredHeight = ArtSize.y;
            }
        }

        private RectTransform MakeRow(string name, float height, TextAnchor align, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(transform, false);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = align; hlg.spacing = spacing;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            return (RectTransform)go.transform;
        }
    }
}
