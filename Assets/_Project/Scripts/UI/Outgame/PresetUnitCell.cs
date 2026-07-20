using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // loadout-preset-page unit 2 — 읽기전용 유닛 셀. 스쿼드 로스터 셀(SquadRosterBrowser.AddCell)의
    // 비주얼(희귀도 프레임 + inner bg + preserveAspect 포트레이트)만 값 복사로 맞춘 최소 셀.
    // 탭/아웃라인/라벨/뱃지 없음. 모든 그래픽 raycastTarget=false (스크롤 드래그 방해 금지).
    public class PresetUnitCell : MonoBehaviour
    {
        // 값 복사(원본은 private static): SquadRosterBrowser.CellBg / DreamcatcherDeckStrip.EmptySlot 톤.
        private static readonly Color CellBg = new Color(0.12f, 0.13f, 0.17f, 1f);
        private static readonly Color EmptySlot = new Color(0.16f, 0.18f, 0.24f, 1f);

        private Image _frame;    // 외곽(희귀도) 프레임 = 셀 배경
        private Image _portrait;

        public static PresetUnitCell Create(RectTransform parent, Vector2 size)
        {
            var root = new GameObject("PresetUnitCell", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = size;
            var cell = root.AddComponent<PresetUnitCell>();

            var frame = root.GetComponent<Image>();
            frame.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(root.transform, false);
            var irt = (RectTransform)innerGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(4, 4); irt.offsetMax = new Vector2(-4, -4);
            var inner = innerGo.GetComponent<Image>();
            inner.color = CellBg; inner.raycastTarget = false;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(innerGo.transform, false);
            var prt = (RectTransform)portraitGo.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(4, 4); prt.offsetMax = new Vector2(-4, -4);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true; portrait.raycastTarget = false; portrait.enabled = false;

            cell._frame = frame;
            cell._portrait = portrait;
            return cell;
        }

        // 유닛 표시. null 이면 빈 슬롯(포트레이트 숨김 + EmptySlot 색).
        public void Set(DefenderUnitData unit)
        {
            if (_frame != null) _frame.color = unit != null ? UnitRarityStyle.Frame(unit.rarity) : EmptySlot;
            if (_portrait != null)
            {
                var sprite = unit != null ? unit.portrait : null;
                _portrait.sprite = sprite;
                _portrait.enabled = sprite != null;
            }
        }
    }
}
