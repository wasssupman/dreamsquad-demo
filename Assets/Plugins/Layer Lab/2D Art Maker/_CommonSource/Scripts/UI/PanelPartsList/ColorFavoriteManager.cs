using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Serialization;

namespace LayerLab.ArtMaker
{
    public class ColorFavoriteManager : MonoBehaviour
    {
        [Header("Favorite Color Settings")]
        [SerializeField] private Transform favoriteSlotParent;
        [SerializeField] private GameObject favoriteSlotPrefab;
        [SerializeField] private int maxFavoriteSlots = 20;
        
        [Header("Hex Input Settings")]
        [SerializeField] private TMP_InputField hexInputField;
        [SerializeField] private Button copyHexButton;
        [SerializeField] private TMP_Text hexDisplayText;
        
        [Header("Favorite Control Buttons")]
        [SerializeField] private Button addCurrentColorButton;
        [SerializeField] private Button deleteSelectedSlotButton;
        
        private List<ColorFavoriteSlot> favoriteSlots = new ();
        private List<Color> favoriteColors = new ();
        private ColorPicker colorPicker;
        private int selectedSlotIndex = -1; // 현재 선택된 슬롯 인덱스 / Currently selected slot index
        
        public static ColorFavoriteManager Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
        }
        
        /// <summary>
        /// 즐겨찾기 매니저 초기화
        /// Initialize favorite manager
        /// </summary>
        public void Init(ColorPicker picker)
        {
            colorPicker = picker;
            InitializeFavoriteSlots();
            SetupButtons();
            LoadFavoriteColors();
            
            // 컬러피커의 색상 변경 이벤트에 연결 / Connect to color picker's color changed event
            if (colorPicker != null)
            {
                // 기존 구독 해제 후 다시 구독 (중복 방지) / Unsubscribe then resubscribe (prevent duplicates)
                colorPicker.OnColorChanged -= UpdateHexDisplay;
                colorPicker.OnColorChanged += UpdateHexDisplay;

                // 초기 Hex 값 설정 / Set initial hex value
                UpdateHexDisplay(colorPicker.GetCurrentColor());
                
                Debug.Log("ColorFavoriteManager initialized and connected to ColorPicker events");
            }
        }
        
        /// <summary>
        /// 즐겨찾기 슬롯들 초기화
        /// Initialize favorite slots
        /// </summary>
        private void InitializeFavoriteSlots()
        {
            if (favoriteSlotPrefab == null || favoriteSlotParent == null)
            {
                Debug.LogError("Favorite slot prefab or parent is not assigned!");
                return;
            }
            
            for (int i = 0; i < maxFavoriteSlots; i++)
            {
                GameObject slotObj = Instantiate(favoriteSlotPrefab, favoriteSlotParent);
                ColorFavoriteSlot slot = slotObj.GetComponent<ColorFavoriteSlot>();
                
                if (slot == null)
                {
                    slot = slotObj.AddComponent<ColorFavoriteSlot>();
                }
                
                slot.Init(i, this);
                favoriteSlots.Add(slot);
            }
        }
        
        /// <summary>
        /// 버튼 이벤트 설정
        /// Setup button events
        /// </summary>
        private void SetupButtons()
        {            
            if (copyHexButton != null)
                copyHexButton.onClick.AddListener(CopyHexToClipboard);
            
            if (hexInputField != null)
            {
                hexInputField.onEndEdit.AddListener(OnHexInputChanged);
                hexInputField.characterLimit = 7; // #FFFFFF 형태 / #FFFFFF format
            }
            
            // 새로운 버튼들 설정 / Setup new buttons
            if (addCurrentColorButton != null)
                addCurrentColorButton.onClick.AddListener(AddCurrentColorToSelectedSlot);
            
            if (deleteSelectedSlotButton != null)
                deleteSelectedSlotButton.onClick.AddListener(DeleteSelectedSlot);
            
            // 초기 버튼 상태 설정 / Set initial button state
            UpdateControlButtonsState();
        }
        
        /// <summary>
        /// 슬롯 선택
        /// Select slot
        /// </summary>
        /// <param name="slotIndex">선택할 슬롯 인덱스 / Slot index to select</param>
        public void SelectSlot(int slotIndex)
        {
            // 이전 선택 해제 / Deselect previous
            if (selectedSlotIndex >= 0 && selectedSlotIndex < favoriteSlots.Count)
            {
                favoriteSlots[selectedSlotIndex].SetSelected(false);
            }

            // 새로운 슬롯 선택 / Select new slot
            selectedSlotIndex = slotIndex;
            
            if (selectedSlotIndex >= 0 && selectedSlotIndex < favoriteSlots.Count)
            {
                favoriteSlots[selectedSlotIndex].SetSelected(true);
            }
            
            UpdateControlButtonsState();
            AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
            
            Debug.Log($"Selected favorite slot: {selectedSlotIndex}");
        }
        
        /// <summary>
        /// 슬롯 선택 해제
        /// Deselect slot
        /// </summary>
        public void DeselectSlot()
        {
            if (selectedSlotIndex >= 0 && selectedSlotIndex < favoriteSlots.Count)
            {
                favoriteSlots[selectedSlotIndex].SetSelected(false);
            }
            
            selectedSlotIndex = -1;
            UpdateControlButtonsState();
        }
        
        /// <summary>
        /// 현재 색상을 선택된 슬롯에 추가
        /// Add current color to selected slot
        /// </summary>
        private void AddCurrentColorToSelectedSlot()
        {
            if (selectedSlotIndex < 0 || colorPicker == null)
            {
                Debug.LogWarning("No slot selected or color picker is null");
                return;
            }
            
            Color currentColor = colorPicker.GetCurrentColor();
            SetFavoriteColor(selectedSlotIndex, currentColor);
            SaveFavoriteColors();
            AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
            
            Debug.Log($"Added current color to slot {selectedSlotIndex}: {ColorUtility.ToHtmlStringRGBA(currentColor)}");
        }
        
        /// <summary>
        /// 선택된 슬롯 삭제
        /// Delete selected slot
        /// </summary>
        private void DeleteSelectedSlot()
        {
            if (selectedSlotIndex < 0)
            {
                Debug.LogWarning("No slot selected");
                return;
            }
            
            RemoveFavoriteColor(selectedSlotIndex);
            Debug.Log($"Deleted favorite slot: {selectedSlotIndex}");
        }
        
        /// <summary>
        /// 컨트롤 버튼들 상태 업데이트
        /// Update control buttons state
        /// </summary>
        private void UpdateControlButtonsState()
        {
            bool hasSelection = selectedSlotIndex >= 0;
            bool hasColorInSelectedSlot = hasSelection && selectedSlotIndex < favoriteColors.Count && 
                                        favoriteColors[selectedSlotIndex] != Color.clear;
            
            // 현재 색상 추가 버튼: 슬롯이 선택되어 있을 때 활성화 / Add button: enabled when a slot is selected
            if (addCurrentColorButton != null)
                addCurrentColorButton.interactable = hasSelection;

            // 삭제 버튼: 선택된 슬롯에 색상이 있을 때만 활성화 / Delete button: enabled only when selected slot has a color
            if (deleteSelectedSlotButton != null)
                deleteSelectedSlotButton.interactable = hasColorInSelectedSlot;
        }
        
        /// <summary>
        /// 특정 슬롯에 현재 색상을 즐겨찾기로 추가
        /// Add current color to specific favorite slot
        /// </summary>
        public void AddCurrentColorToFavorites(int slotIndex = -1)
        {
            if (colorPicker == null) return;
            
            Color currentColor = colorPicker.GetCurrentColor();
            
            if (slotIndex >= 0)
            {
                // 특정 슬롯에 저장 / Save to specific slot
                SetFavoriteColor(slotIndex, currentColor);
                SaveFavoriteColors();
                AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
                Debug.Log($"Color added to favorite slot {slotIndex}: {ColorUtility.ToHtmlStringRGBA(currentColor)}");
            }
            else
            {
                // 빈 슬롯 찾아서 저장 / Find empty slot and save
                int emptySlotIndex = FindEmptySlot();
                if (emptySlotIndex >= 0)
                {
                    SetFavoriteColor(emptySlotIndex, currentColor);
                    SaveFavoriteColors();
                    AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
                    Debug.Log($"Color added to favorite slot {emptySlotIndex}: {ColorUtility.ToHtmlStringRGBA(currentColor)}");
                }
                else
                {
                    Debug.LogWarning("No empty favorite slots available!");
                }
            }
            
            UpdateControlButtonsState();
        }
        
        /// <summary>
        /// 빈 슬롯 찾기
        /// Find empty slot
        /// </summary>
        private int FindEmptySlot()
        {
            for (int i = 0; i < favoriteColors.Count; i++)
            {
                if (favoriteColors[i] == Color.clear)
                {
                    return i;
                }
            }
            
            if (favoriteColors.Count < maxFavoriteSlots)
            {
                return favoriteColors.Count;
            }
            
            return -1;
        }
        
        /// <summary>
        /// 즐겨찾기 색상 설정
        /// Set favorite color
        /// </summary>
        public void SetFavoriteColor(int slotIndex, Color color)
        {
            if (slotIndex < 0 || slotIndex >= maxFavoriteSlots) return;
            
            // 리스트 크기 조정 / Adjust list size
            while (favoriteColors.Count <= slotIndex)
            {
                favoriteColors.Add(Color.clear);
            }
            
            favoriteColors[slotIndex] = color;
            
            if (slotIndex < favoriteSlots.Count)
            {
                favoriteSlots[slotIndex].SetColor(color);
            }
        }
        
        /// <summary>
        /// 즐겨찾기 색상 삭제
        /// Remove favorite color
        /// </summary>
        public void RemoveFavoriteColor(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= favoriteColors.Count) return;
            
            favoriteColors[slotIndex] = Color.clear;
            
            if (slotIndex < favoriteSlots.Count)
            {
                favoriteSlots[slotIndex].SetColor(Color.clear);
            }
            
            SaveFavoriteColors();
            UpdateControlButtonsState();
            AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
            Debug.Log($"Favorite color removed from slot {slotIndex}");
        }
        
        /// <summary>
        /// 즐겨찾기 색상 적용
        /// Apply favorite color
        /// </summary>
        public void ApplyFavoriteColor(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= favoriteColors.Count) return;
            if (favoriteColors[slotIndex] == Color.clear) return;
            if (colorPicker == null) return;
            
            Color selectedColor = favoriteColors[slotIndex];
            
            // 컬러피커에 색상 적용 / Apply color to color picker
            ApplyColorToColorPicker(selectedColor);
            
            AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
            Debug.Log($"Applied favorite color from slot {slotIndex}: {ColorUtility.ToHtmlStringRGBA(selectedColor)}");
        }
        
        /// <summary>
        /// 컬러피커에 색상 적용
        /// Apply color to color picker
        /// </summary>
        private void ApplyColorToColorPicker(Color color)
        {
            if (colorPicker == null) return;

            // public API를 사용하여 색상 설정 / Set color using public API
            var currentPartsType = colorPicker.CurrentPartsType;
            colorPicker.SetColorDirect(currentPartsType, color);

            // 캐릭터에 색상 적용 / Apply color to character
            if (Player.Instance?.PartsManager != null)
            {
                Player.Instance.PartsManager.ApplyColorByType(currentPartsType, color);
            }

            // ColorPresetManager에도 색상 저장 / Save color to ColorPresetManager
            if (ColorPresetManager.Instance != null)
            {
                ColorPresetManager.Instance.SetSelectByColor(currentPartsType, color);
            }

            // 색상 변경 이벤트 호출 - Hex 업데이트를 트리거 / Invoke color changed event - triggers Hex update
            colorPicker.OnColorChanged?.Invoke(color);
        }
        
        /// <summary>
        /// Hex 코드 적용
        /// Apply hex color
        /// </summary>
        private void ApplyHexColor()
        {
            if (hexInputField == null || colorPicker == null) return;
            
            string hexCode = hexInputField.text.Trim();
            
            // # 없으면 추가 / Add # if missing
            if (!hexCode.StartsWith("#"))
            {
                hexCode = "#" + hexCode;
            }
            
            // Hex 코드를 Color로 변환 / Convert hex code to Color
            if (ColorUtility.TryParseHtmlString(hexCode, out Color color))
            {
                // 컬러피커와 캐릭터에 모두 적용 / Apply to both color picker and character
                ApplyColorToColorPicker(color);
                AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
                Debug.Log($"Applied hex color: {hexCode}");
            }
            else
            {
                Debug.LogWarning($"Invalid hex color code: {hexCode}");
                // 잘못된 입력시 현재 색상으로 되돌리기 / Revert to current color on invalid input
                UpdateHexDisplay(colorPicker.GetCurrentColor());
            }
        }
        
        /// <summary>
        /// Hex 입력 필드 변경 이벤트
        /// Hex input field change event
        /// </summary>
        private void OnHexInputChanged(string value)
        {
            ApplyHexColor();
        }
        
        /// <summary>
        /// Hex 코드를 클립보드에 복사
        /// Copy hex code to clipboard
        /// </summary>
        private void CopyHexToClipboard()
        {
            if (colorPicker == null) return;
            
            string hexCode = "#" + ColorUtility.ToHtmlStringRGB(colorPicker.GetCurrentColor());
            GUIUtility.systemCopyBuffer = hexCode;
            
            AudioManager.Instance?.PlaySound(SoundList.ButtonDefault);
            Debug.Log($"Copied hex code to clipboard: {hexCode}");
        }
        
        /// <summary>
        /// Hex 디스플레이 업데이트 / Update hex display
        /// </summary>
        public void UpdateHexDisplay(Color color)
        {
            string hexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
            
            if (hexInputField != null)
            {
                hexInputField.SetTextWithoutNotify(hexCode);
            }
            
            if (hexDisplayText != null)
            {
                hexDisplayText.text = hexCode;
            }
            
            Debug.Log($"Hex display updated: {hexCode}");
        }
        
        /// <summary>
        /// 현재 컬러피커 색상으로 Hex 업데이트 / Update hex with current color picker color
        /// </summary>
        public void UpdateHexWithCurrentColor()
        {
            if (colorPicker != null)
            {
                UpdateHexDisplay(colorPicker.GetCurrentColor());
            }
        }
        
        /// <summary>
        /// 즐겨찾기 색상들 저장
        /// Save favorite colors
        /// </summary>
        private void SaveFavoriteColors()
        {
            for (int i = 0; i < favoriteColors.Count; i++)
            {
                Color color = favoriteColors[i];
                string colorString = $"{color.r},{color.g},{color.b},{color.a}";
                PlayerPrefs.SetString($"FavoriteColor_{i}", colorString);
            }
            PlayerPrefs.SetInt("FavoriteColorCount", favoriteColors.Count);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// 즐겨찾기 색상들 로드
        /// Load favorite colors
        /// </summary>
        private void LoadFavoriteColors()
        {
            int count = PlayerPrefs.GetInt("FavoriteColorCount", 0);
            favoriteColors.Clear();
            
            for (int i = 0; i < maxFavoriteSlots; i++)
            {
                if (i < count)
                {
                    string colorString = PlayerPrefs.GetString($"FavoriteColor_{i}", "0,0,0,0");
                    string[] values = colorString.Split(',');
                    
                    if (values.Length == 4 && 
                        float.TryParse(values[0], out float r) &&
                        float.TryParse(values[1], out float g) &&
                        float.TryParse(values[2], out float b) &&
                        float.TryParse(values[3], out float a))
                    {
                        Color color = new Color(r, g, b, a);
                        favoriteColors.Add(color);
                        
                        if (i < favoriteSlots.Count)
                        {
                            favoriteSlots[i].SetColor(color);
                        }
                    }
                    else
                    {
                        favoriteColors.Add(Color.clear);
                    }
                }
                else
                {
                    favoriteColors.Add(Color.clear);
                }
            }
        }
        
        /// <summary>
        /// 컴포넌트 파괴시 정리
        /// Cleanup on destroy
        /// </summary>
        private void OnDestroy()
        {
            if (colorPicker != null)
            {
                colorPicker.OnColorChanged -= UpdateHexDisplay;
            }
        }
    }
}