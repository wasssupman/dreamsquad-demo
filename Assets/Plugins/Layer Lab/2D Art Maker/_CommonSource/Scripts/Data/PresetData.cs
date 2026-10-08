using System;
using System.Collections.Generic;
using UnityEngine;

namespace LayerLab.ArtMaker
{
    /// <summary>
    /// 캐릭터 프리셋 데이터를 저장하는 ScriptableObject
    /// ScriptableObject that stores character preset data
    /// </summary>
    [CreateAssetMenu(fileName = "PresetData", menuName = "Character/PresetData")]
    public class PresetData : ScriptableObject
    {
        public List<PresetItem> presetItems = new(); // 프리셋 항목 목록 / List of preset items

        /// <summary>
        /// 프리셋 저장
        /// Save preset
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        /// <param name="itemList">부품 인덱스 목록 / Parts index list</param>
        /// <param name="colorData">색상 데이터 / Color data</param>
        /// <param name="positionData">위치 데이터 / Position data</param>
        public void SavePreset(int index, Dictionary<PartsType, int> itemList, Dictionary<string, Color> colorData, Dictionary<PartsType, Vector2> positionData = null)
        {
            presetItems.RemoveAll(p => p.index == index);
            presetItems.Add(new PresetItem(index, itemList, colorData, positionData));
        }

        /// <summary>
        /// 프리셋의 부품 데이터 로드
        /// Load parts data from preset
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        /// <returns>부품 인덱스 딕셔너리 / Parts index dictionary</returns>
        public Dictionary<PartsType, int> LoadPreset(int index)
        {
            var preset = presetItems.Find(p => p.index == index);
            return preset != null ? new Dictionary<PartsType, int>(preset.itemList) : new Dictionary<PartsType, int>();
        }

        /// <summary>
        /// 프리셋의 색상 데이터 로드
        /// Load color data from preset
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        /// <returns>색상 딕셔너리 / Color dictionary</returns>
        public Dictionary<string, Color> LoadPresetColors(int index)
        {
            var preset = presetItems.Find(p => p.index == index);
            return preset != null ? new Dictionary<string, Color>(preset.colorData) : new Dictionary<string, Color>();
        }

        /// <summary>
        /// 프리셋의 위치 데이터 로드
        /// Load position data from preset
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        /// <returns>위치 딕셔너리 / Position dictionary</returns>
        public Dictionary<PartsType, Vector2> LoadPresetPositions(int index)
        {
            var preset = presetItems.Find(p => p.index == index);
            return preset != null ? new Dictionary<PartsType, Vector2>(preset.positionData) : new Dictionary<PartsType, Vector2>();
        }

        /// <summary>
        /// 프리셋 삭제
        /// Clear preset
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        public void ClearPreset(int index)
        {
            presetItems.RemoveAll(p => p.index == index);
        }
    }

    /// <summary>
    /// 개별 프리셋 항목 데이터
    /// Individual preset item data
    /// </summary>
    [Serializable]
    public class PresetItem
    {
        public int index; // 프리셋 인덱스 / Preset index
        public List<PartItem> parts = new(); // 부품 목록 / Parts list
        public List<ColorItem> colors = new(); // 색상 목록 / Color list
        public List<PositionItem> positions = new(); // 위치 목록 / Position list

        /// <summary>
        /// 부품 데이터를 딕셔너리로 변환
        /// Convert parts data to dictionary
        /// </summary>
        public Dictionary<PartsType, int> itemList
        {
            get
            {
                Dictionary<PartsType, int> dict = new();
                foreach (var part in parts)
                {
                    dict[part.partType] = part.value;
                }
                return dict;
            }
        }

        /// <summary>
        /// 색상 데이터를 딕셔너리로 변환
        /// Convert color data to dictionary
        /// </summary>
        public Dictionary<string, Color> colorData
        {
            get
            {
                Dictionary<string, Color> dict = new();
                foreach (var color in colors)
                {
                    dict[color.slotName] = color.color;
                }
                return dict;
            }
        }

        /// <summary>
        /// 위치 데이터를 딕셔너리로 변환
        /// Convert position data to dictionary
        /// </summary>
        public Dictionary<PartsType, Vector2> positionData
        {
            get
            {
                Dictionary<PartsType, Vector2> dict = new();
                foreach (var position in positions)
                {
                    dict[position.partType] = position.position;
                }
                return dict;
            }
        }

        /// <summary>
        /// 프리셋 항목 생성자
        /// Preset item constructor
        /// </summary>
        /// <param name="index">프리셋 인덱스 / Preset index</param>
        /// <param name="itemList">부품 인덱스 목록 / Parts index list</param>
        /// <param name="colorData">색상 데이터 / Color data</param>
        /// <param name="positionData">위치 데이터 / Position data</param>
        public PresetItem(int index, Dictionary<PartsType, int> itemList, Dictionary<string, Color> colorData, Dictionary<PartsType, Vector2> positionData = null)
        {
            this.index = index;
            foreach (var kvp in itemList)
            {
                parts.Add(new PartItem(kvp.Key, kvp.Value));
            }

            if (colorData != null)
            {
                foreach (var kvp in colorData)
                {
                    colors.Add(new ColorItem(kvp.Key, kvp.Value));
                }
            }

            if (positionData != null)
            {
                foreach (var kvp in positionData)
                {
                    positions.Add(new PositionItem(kvp.Key, kvp.Value));
                }
            }
        }
    }

    /// <summary>
    /// 부품 항목 직렬화 데이터
    /// Part item serialization data
    /// </summary>
    [Serializable]
    public class PartItem
    {
        public PartsType partType; // 부품 유형 / Part type
        public int value; // 부품 인덱스 / Part index

        public PartItem(PartsType partType, int value)
        {
            this.partType = partType;
            this.value = value;
        }
    }

    /// <summary>
    /// 색상 항목 직렬화 데이터
    /// Color item serialization data
    /// </summary>
    [Serializable]
    public class ColorItem
    {
        public string slotName; // 슬롯 이름 / Slot name
        public Color color; // 색상 값 / Color value

        public ColorItem(string slotName, Color color)
        {
            this.slotName = slotName;
            this.color = color;
        }
    }

    /// <summary>
    /// 위치 항목 직렬화 데이터
    /// Position item serialization data
    /// </summary>
    [Serializable]
    public class PositionItem
    {
        public PartsType partType; // 부품 유형 / Part type
        public Vector2 position; // 위치 값 / Position value

        public PositionItem(PartsType partType, Vector2 position)
        {
            this.partType = partType;
            this.position = position;
        }
    }
}
