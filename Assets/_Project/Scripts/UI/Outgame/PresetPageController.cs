using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // loadout-preset-page unit 4 — 오케스트레이터. collection 의 각 프리셋으로 목록 아이템을 빌드하고,
    // 적용 버튼 → 확인 팝업 → PresetApply.WriteToProfile → ProfileStore.Save 로 배선한다.
    // 프리셋 집합은 authoring 정적이라 1회 빌드 후 재사용(패널 재활성에도 중복 생성 없음).
    public class PresetPageController : MonoBehaviour
    {
        [SerializeField] private SquadPresetCollection collection;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private RectTransform content;      // scroll content
        [SerializeField] private PresetConfirmPopup confirmPopup;

        private bool _built;

        private void OnEnable()
        {
            if (_built) return;
            if (collection == null) { Debug.LogWarning("[PresetPageController] collection 미할당 — 목록 비움.", this); return; }
            _built = true;
            if (confirmPopup != null) confirmPopup.Init(font);
            BuildItems();
        }

        private void BuildItems()
        {
            if (content == null || collection.presets == null) return;
            for (int i = 0; i < collection.presets.Count; i++)
            {
                var preset = collection.presets[i];
                if (preset == null) continue;
                var itemGo = new GameObject("PresetItem", typeof(RectTransform));
                itemGo.transform.SetParent(content, false);
                var view = itemGo.AddComponent<PresetListItemView>();
                view.Build(preset, font);
                var captured = preset;
                view.ApplyClicked += () => OnApplyRequested(captured);
            }
        }

        private void OnApplyRequested(SquadPreset preset)
        {
            string msg = $"'{Name(preset)}' 프리셋을 적용합니다.\n현재 스쿼드·드림캐쳐 덱이 교체됩니다.";
            if (confirmPopup != null) confirmPopup.Show(msg, () => Apply(preset));
            else Apply(preset); // 팝업 미주입 시 안전 폴백
        }

        private void Apply(SquadPreset preset)
        {
            if (profileSO == null || profileSO.profile == null || preset == null) return;

            // SO → id 매핑(호출처 책임). 유닛은 슬롯 정렬 유지 위해 null→"", 카드는 null 스킵.
            var unitIds = new List<string>();
            if (preset.units != null)
                for (int i = 0; i < preset.units.Length; i++)
                    unitIds.Add(preset.units[i] != null ? preset.units[i].id : "");

            var cardIds = new List<string>();
            if (preset.cards != null)
                for (int i = 0; i < preset.cards.Length; i++)
                    if (preset.cards[i] != null) cardIds.Add(preset.cards[i].id);

            if (PresetApply.WriteToProfile(profileSO.profile, unitIds, cardIds))
                ProfileStore.Save(profileSO.profile);
        }

        private static string Name(SquadPreset p) =>
            (p != null && !string.IsNullOrEmpty(p.presetName)) ? p.presetName : "프리셋";
    }
}
