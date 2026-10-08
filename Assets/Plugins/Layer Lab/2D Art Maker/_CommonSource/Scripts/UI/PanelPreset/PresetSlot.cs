using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LayerLab.ArtMaker
{
    public class PresetSlot : MonoBehaviour, IPointerClickHandler
    {
        [field: SerializeField] private PartsManager PartsManager { get; set; }
        [SerializeField] private bool useSave;
        private int _slotIndex;

        // 기본 색상 값 / Default color values
        private readonly Color _defaultHairColor = new (0.5f, 0.5f, 0.5f);
        private readonly Color _defaultBeardColor = new (0.5f, 0.5f, 0.5f);
        private readonly Color _defaultBrowColor = new (0.5f, 0.5f, 0.5f);
        private readonly Color _defaultSkinColor = new (1f, 0.8f, 0.7f); // 기본 피부색 / Default skin color

        /// <summary>
        /// 초기화
        /// Initialize
        /// </summary>
        /// <param name="index">인덱스 / Index</param>
        public void Init(int index)
        {
            _slotIndex = index;
            PartsManager = transform.GetComponentInChildren<PartsManager>();
            PartsManager.Init();
            LoadData();
        }

        /// <summary>
        /// 프리셋 데이터 로드
        /// Load preset data
        /// </summary>
        private void LoadData()
        {
            // 프리셋에서 부품 인덱스 일괄 로드 / Batch load part indices from preset
            var dic = DemoControl.Instance.PresetData.LoadPreset(_slotIndex);
            if (dic != null && dic.Count > 0)
            {
                PartsManager.SetSkinActiveIndex(dic);
            }

            var colorData = DemoControl.Instance.PresetData.LoadPresetColors(_slotIndex);
            var positionData = DemoControl.Instance.PresetData.LoadPresetPositions(_slotIndex);

            // 색상 데이터 적용 (ApplyColorByType 통합 메서드 사용)
            // Apply color data (using unified ApplyColorByType method)
            if (colorData != null && colorData.Count > 0)
            {
                PartsManager.ApplyColorByType(PartsType.Hair_Short,
                    colorData.TryGetValue("hair", out Color hairColor) ? hairColor : _defaultHairColor);
                PartsManager.ApplyColorByType(PartsType.Beard,
                    colorData.TryGetValue("beard", out Color beardColor) ? beardColor : _defaultBeardColor);
                PartsManager.ApplyColorByType(PartsType.Brow,
                    colorData.TryGetValue("brow", out Color browColor) ? browColor : _defaultBrowColor);
                PartsManager.ApplyColorByType(PartsType.Skin,
                    colorData.TryGetValue("body", out Color skinColor) ? skinColor : _defaultSkinColor);
            }
            else
            {
                // 색상 데이터 없으면 기본값 적용 / Apply defaults if no color data
                PartsManager.ApplyColorByType(PartsType.Hair_Short, _defaultHairColor);
                PartsManager.ApplyColorByType(PartsType.Beard, _defaultBeardColor);
                PartsManager.ApplyColorByType(PartsType.Brow, _defaultBrowColor);
                PartsManager.ApplyColorByType(PartsType.Skin, _defaultSkinColor);
            }

            // 위치 데이터 적용 / Apply position data
            if (positionData != null && positionData.Count > 0 && ColorPicker.Instance != null)
            {
                foreach (var kvp in positionData)
                {
                    ColorPicker.Instance.SetPartPosition(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// 프리셋 슬롯 클릭 처리 (좌클릭: 프리셋 적용, 우클릭: 프리셋 저장)
        /// Handle preset slot click (Left: apply preset, Right: save preset)
        /// </summary>
        /// <param name="eventData">이벤트 데이터 / Event data</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    DemoControl.Instance.PanelParts.PanelPartsList.OnClick_Close();

                    // 프리셋의 모든 부품을 캐릭터에 일괄 적용 / Batch apply all parts from preset to character
                    ApplyAllPartsToCharacter();

                    var colorData = LoadColorDataFromPreset();
                    var positionData = LoadPositionDataFromPreset();

                    // 색상 적용: ApplyColorByType이 내부에서 OnColorChange?.Invoke도 호출
                    // Apply colors: ApplyColorByType internally calls OnColorChange?.Invoke too
                    if (colorData != null && colorData.Count > 0)
                    {
                        if (colorData.TryGetValue("hair", out Color hairColor))
                            Player.Instance.PartsManager.ApplyColorByType(PartsType.Hair_Short, hairColor);

                        if (colorData.TryGetValue("beard", out Color beardColor))
                            Player.Instance.PartsManager.ApplyColorByType(PartsType.Beard, beardColor);

                        if (colorData.TryGetValue("brow", out Color browColor))
                            Player.Instance.PartsManager.ApplyColorByType(PartsType.Brow, browColor);

                        if (colorData.TryGetValue("body", out Color skinColor))
                            Player.Instance.PartsManager.ApplyColorByType(PartsType.Skin, skinColor);
                    }
                    else
                    {
                        // 색상 데이터 없으면 슬롯의 현재 색상 사용 / Use slot's current color if no color data
                        Player.Instance.PartsManager.ApplyColorByType(PartsType.Hair_Short, PartsManager.GetColorBySlotType("hair"));
                        Player.Instance.PartsManager.ApplyColorByType(PartsType.Beard, PartsManager.GetColorBySlotType("beard"));
                        Player.Instance.PartsManager.ApplyColorByType(PartsType.Brow, PartsManager.GetColorBySlotType("brow"));
                        Player.Instance.PartsManager.ApplyColorByType(PartsType.Skin, PartsManager.GetColorBySlotType("body"));
                    }

                    // 위치 데이터 적용 / Apply position data
                    if (positionData != null && positionData.Count > 0 && ColorPicker.Instance != null)
                    {
                        foreach (var kvp in positionData)
                        {
                            ColorPicker.Instance.SetPartPosition(kvp.Key, kvp.Value);
                        }
                    }

                    break;

                case PointerEventData.InputButton.Right:
                    if (!useSave) return;

                    // 캐릭터의 부품을 프리셋 슬롯에 저장 / Save character parts to preset slot
                    ApplyAllPartsFromCharacterToPreset();

                    // 캐릭터 색상을 프리셋 슬롯에 반영 / Reflect character colors to preset slot
                    PartsManager.ApplyColorByType(PartsType.Hair_Short, Player.Instance.PartsManager.GetColorBySlotType("hair"));
                    PartsManager.ApplyColorByType(PartsType.Beard, Player.Instance.PartsManager.GetColorBySlotType("beard"));
                    PartsManager.ApplyColorByType(PartsType.Brow, Player.Instance.PartsManager.GetColorBySlotType("brow"));
                    PartsManager.ApplyColorByType(PartsType.Skin, Player.Instance.PartsManager.GetColorBySlotType("body"));

                    SavePresetData();
                    break;
            }
        }

        /// <summary>
        /// 프리셋에서 색상 데이터 로드
        /// Load color data from preset
        /// </summary>
        /// <returns>색상 데이터 / Color data</returns>
        private Dictionary<string, Color> LoadColorDataFromPreset()
        {
            return DemoControl.Instance.PresetData.LoadPresetColors(_slotIndex);
        }

        /// <summary>
        /// 프리셋에서 위치 데이터 로드
        /// Load position data from preset
        /// </summary>
        /// <returns>위치 데이터 / Position data</returns>
        private Dictionary<PartsType, Vector2> LoadPositionDataFromPreset()
        {
            return DemoControl.Instance.PresetData.LoadPresetPositions(_slotIndex);
        }

        /// <summary>
        /// 프리셋에서 캐릭터로 모든 부품 일괄 적용 (15번 개별 호출 대신 배치 처리)
        /// Batch apply all parts from preset to character (instead of 15 individual calls)
        /// </summary>
        private void ApplyAllPartsToCharacter()
        {
            var pm = Player.Instance.PartsManager;
            var indices = new Dictionary<PartsType, int>();

            // 프리셋 데이터에서 인덱스 일괄 수집 (미착용 파츠 포함) / Batch collect indices from preset data (including unequipped parts)
            foreach (var partsType in PartsManager.AllPartsTypes)
            {
                if (partsType == PartsType.None) continue;
                // index < 0 (미착용)도 포함해야 ActiveIndices 키 누락 방지 / Include index < 0 (unequipped) to prevent missing ActiveIndices keys
                indices[partsType] = PartsManager.GetCurrentPartIndex(partsType);
            }

            // 한 번에 적용 (15번 개별 호출 대신) / Apply at once (instead of 15 individual calls)
            pm.SetSkinActiveIndex(indices);
            pm.FinalizeCharacter();
        }

        /// <summary>
        /// 캐릭터에서 프리셋으로 모든 부품 일괄 적용 (15번 개별 호출 대신 배치 처리)
        /// Batch apply all parts from character to preset (instead of 15 individual calls)
        /// </summary>
        private void ApplyAllPartsFromCharacterToPreset()
        {
            var playerPm = Player.Instance.PartsManager;
            var indices = new Dictionary<PartsType, int>();

            // 플레이어 캐릭터에서 인덱스 일괄 수집 (미착용 파츠 포함) / Batch collect indices from player character (including unequipped parts)
            foreach (var partsType in PartsManager.AllPartsTypes)
            {
                if (partsType == PartsType.None) continue;
                // index < 0 (미착용)도 포함해야 ActiveIndices 키 누락 방지 / Include index < 0 (unequipped) to prevent missing ActiveIndices keys
                indices[partsType] = playerPm.GetCurrentPartIndex(partsType);
            }

            // 프리셋 슬롯에 한 번에 적용 / Apply to preset slot at once
            PartsManager.SetSkinActiveIndex(indices);
        }

        /// <summary>
        /// 프리셋 저장
        /// Save preset data
        /// </summary>
        private void SavePresetData()
        {
            // 색상 데이터 수집 / Collect color data
            var colorData = new Dictionary<string, Color>
            {
                { "hair", Player.Instance.PartsManager.GetColorBySlotType("hair") },
                { "beard", Player.Instance.PartsManager.GetColorBySlotType("beard") },
                { "brow", Player.Instance.PartsManager.GetColorBySlotType("brow") },
                { "body", Player.Instance.PartsManager.GetColorBySlotType("body") }
            };

            // 위치 데이터 수집 / Collect position data
            var positionData = new Dictionary<PartsType, Vector2>();
            if (ColorPicker.Instance != null)
            {
                var hairPos = ColorPicker.Instance.GetPartPosition(PartsType.Hair_Short);
                var beardPos = ColorPicker.Instance.GetPartPosition(PartsType.Beard);
                var browPos = ColorPicker.Instance.GetPartPosition(PartsType.Brow);
                var skinPos = ColorPicker.Instance.GetPartPosition(PartsType.Skin);

                if (hairPos.x >= 0) positionData.Add(PartsType.Hair_Short, hairPos);
                if (beardPos.x >= 0) positionData.Add(PartsType.Beard, beardPos);
                if (browPos.x >= 0) positionData.Add(PartsType.Brow, browPos);
                if (skinPos.x >= 0) positionData.Add(PartsType.Skin, skinPos);
            }

            DemoControl.Instance.PresetData.SavePreset(_slotIndex, Player.Instance.PartsManager.ActiveIndices, colorData, positionData);
        }
    }
}
