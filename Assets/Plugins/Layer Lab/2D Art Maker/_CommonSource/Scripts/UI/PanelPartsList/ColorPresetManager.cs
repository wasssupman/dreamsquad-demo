using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace LayerLab.ArtMaker
{
    public class ColorPresetManager : MonoBehaviour
    {
        public static ColorPresetManager Instance { get; set; }
        
        private PartsType _currentPart;
        private Dictionary<PartsType, Color> _customColors = new ();
        
        private void Awake()
        {
            Instance = this;
        }
        
        /// <summary>
        /// 타입 값으로 색상 가져오기
        /// Get color by part type
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        /// <returns>색상 / Color</returns>
        public Color GetColorByType(PartsType partsType)
        {
            if (_customColors.ContainsKey(partsType))
            {
                return _customColors[partsType];
            }

            // Player.Instance에서 실제 색상 가져오기 / Get actual color from Player.Instance
            if (Player.Instance?.PartsManager != null)
            {
                return GetActualColorFromPartsManager(partsType);
            }

            // 기본 색상 반환 / Return default color
            return GetDefaultColor(partsType);
        }
        
        /// <summary>
        /// PartsManager에서 실제 색상 가져오기
        /// Get actual color from PartsManager
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        /// <returns>실제 색상 / Actual color</returns>
        private Color GetActualColorFromPartsManager(PartsType partsType)
        {
            try
            {
                return partsType switch
                {
                    PartsType.Hair_Short => Player.Instance.PartsManager.GetColorBySlotType("hair"),
                    PartsType.Beard => Player.Instance.PartsManager.GetColorBySlotType("beard"),
                    PartsType.Brow => Player.Instance.PartsManager.GetColorBySlotType("brow"),
                    PartsType.Skin => Player.Instance.PartsManager.GetColorBySlotType("body"),
                    _ => GetDefaultColor(partsType)
                };
            }
            catch
            {
                return GetDefaultColor(partsType);
            }
        }
        
        /// <summary>
        /// 기본 색상 가져오기
        /// Get default color
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        /// <returns>기본 색상 / Default color</returns>
        private Color GetDefaultColor(PartsType partsType)
        {
            return partsType switch
            {
                PartsType.Hair_Short => new Color(0.5f, 0.3f, 0.1f), // 갈색 머리 / Brown hair
                PartsType.Beard => new Color(0.5f, 0.3f, 0.1f), // 갈색 수염 / Brown beard
                PartsType.Brow => new Color(0.4f, 0.2f, 0.1f), // 어두운 갈색 눈썹 / Dark brown brow
                PartsType.Skin => new Color(1f, 0.8f, 0.7f), // 기본 피부색 / Default skin color
                _ => Color.white
            };
        }
    
        /// <summary>
        /// 타입별 프리셋 색상 설정
        /// Set preset color by part type
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        public void SetPresetColor(PartsType partsType)
        {
            _currentPart = partsType;
            
            // 현재 파츠의 실제 색상 가져오기 / Get actual color of current part
            var currentColor = GetColorByType(partsType);
            _customColors[partsType] = currentColor;
            
            // 컬러피커에도 현재 파츠 타입 설정 / Set current parts type on color picker
            if (ColorPicker.Instance != null)
            {
                ColorPicker.Instance.SetCurrentPartsType(partsType);

                // 컬러피커에서 해당 색상의 위치를 찾아서 설정 / Find and set color position in color picker
                SetColorPickerPositionByColor(partsType, currentColor);

                // ColorFavoriteManager의 Hex 업데이트도 트리거 / Also trigger Hex update for ColorFavoriteManager
                ColorFavoriteManager.Instance?.UpdateHexDisplay(currentColor);
            }
            
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 색상에 따른 컬러피커 위치 설정
        /// Set color picker position by color
        /// </summary>
        /// <param name="partsType">파츠 타입 / Parts type</param>
        /// <param name="color">색상 / Color</param>
        private void SetColorPickerPositionByColor(PartsType partsType, Color color)
        {
            if (ColorPicker.Instance == null) return;

            // public API를 사용하여 색상 설정 / Set color using public API
            ColorPicker.Instance.SetColorDirect(partsType, color);
        }

        /// <summary>
        /// 색상별 선택 설정
        /// Set selection by color
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        /// <param name="color">색상 / Color</param>
        public void SetSelectByColor(PartsType partsType, Color color)
        {
            _customColors[partsType] = color;
            // 여기서는 캐릭터 적용하지 않음 (다른 곳에서 이미 적용됨) / Don't apply to character here (already applied elsewhere)
        }

        /// <summary>
        /// 모든 부품에 랜덤 색상 설정
        /// Set random colors for all parts
        /// </summary>
        public void SetRandomAllColor()
        {
            if (ColorPicker.Instance != null)
            {
                SetRandomColorFromPicker(PartsType.Hair_Short);
                SetRandomColorFromPicker(PartsType.Brow);
                SetRandomColorFromPicker(PartsType.Beard);
                SetRandomColorFromPicker(PartsType.Skin);
            }
            else
            {
                ApplyRandomColor(PartsType.Hair_Short);
                ApplyRandomColor(PartsType.Brow);
                ApplyRandomColor(PartsType.Beard);
                ApplyRandomColor(PartsType.Skin);
            }
        }
        
        /// <summary>
        /// 랜덤 색상 적용
        /// Apply random color
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        private void ApplyRandomColor(PartsType partsType)
        {
            Color randomColor = new Color(Random.value, Random.value, Random.value, 1f);
            _customColors[partsType] = randomColor;
            
            switch (partsType)
            {
                case PartsType.Hair_Short:
                    Player.Instance.PartsManager.ChangeHairColor(randomColor);
                    Player.Instance.PartsManager.OnColorChange?.Invoke(PartsType.Hair_Short, randomColor);
                    break;
            
                case PartsType.Beard:
                    Player.Instance.PartsManager.ChangeBeardColor(randomColor);
                    Player.Instance.PartsManager.OnColorChange?.Invoke(PartsType.Beard, randomColor);
                    break;
            
                case PartsType.Brow:
                    Player.Instance.PartsManager.ChangeBrowColor(randomColor);
                    Player.Instance.PartsManager.OnColorChange?.Invoke(PartsType.Brow, randomColor);
                    break;
            
                case PartsType.Skin:
                    Player.Instance.PartsManager.ChangeSkinColor(randomColor);
                    Player.Instance.PartsManager.OnColorChange?.Invoke(PartsType.Skin, randomColor);
                    break;
            }
        }
        
        /// <summary>
        /// 컬러피커에서 랜덤 위치 색상 설정
        /// Set random position color from color picker
        /// </summary>
        /// <param name="partsType">부품 유형 / Parts type</param>
        private void SetRandomColorFromPicker(PartsType partsType)
        {
            Color randomColor = ColorPicker.Instance.ApplyRandomPositionToPart(partsType);
            _customColors[partsType] = randomColor;
            
            Debug.Log($"Applied random color to {partsType}: {ColorUtility.ToHtmlStringRGBA(randomColor)}");
        }
    }
}