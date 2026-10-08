using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LayerLab.ArtMaker
{

   /// <summary>
   /// 데모 전체 흐름을 제어하는 컨트롤러
   /// Controller that manages the overall demo flow
   /// </summary>
   public class DemoControl : MonoBehaviour
   {
       #region Fields and Properties
       
       public static DemoControl Instance { get; private set; }

       [field: SerializeField] public PanelParts PanelParts { get; set; } // 부품 패널 참조 / Parts panel reference
       [field: SerializeField] public PanelPreset PanelPreset { get; set; } // 프리셋 패널 참조 / Preset panel reference
       [field: SerializeField] public PresetData PresetData { get; set; } // 프리셋 데이터 ScriptableObject / Preset data ScriptableObject

       [SerializeField] private Sprite[] sprites; // 스프라이트 배열 / Sprite array
       [SerializeField] private Button buttonRandomParts; // UI 버튼 / UI buttons
       public string pathAsset; // 에셋 경로 / Asset path

       // 스프라이트 이름 기반 캐시 - 반복 검색 최적화 / Sprite name-based cache for optimized repeated lookups
       private Dictionary<string, Sprite> _spriteCache;
       
       #endregion

       #region Unity Lifecycle
       
       /// <summary>
       /// 인스턴스 설정
       /// Set instance
       /// </summary>
       private void Awake()
       {
           Instance = this;
       }

       /// <summary>
       /// 시작 시 초기화
       /// Initialize on start
       /// </summary>
       private void Start()
       {
           Init();
       }
       
       #endregion

       #region Initialization
       
       /// <summary>
       /// 초기화
       /// Initialize
       /// </summary>
       public void Init()
       {
           InitializeManagers();
           OnClick_RandomParts();
       }

       /// <summary>
       /// 매니저들 초기화
       /// Initialize managers
       /// </summary>
       private void InitializeManagers()
       {
           Player.Instance.PartsManager.Init();
           PanelParts.Init();
           PanelPreset.Init();
           AnimationController.Instance.Init();
           
       }
       
       #endregion

       #region Static Methods
       
       /// <summary>
       /// 부품 유형별 색상 변경 가능 여부 확인
       /// Check if color can be changed for parts type
       /// </summary>
       /// <param name="partsType">부품 유형 / Parts type</param>
       /// <returns>색상 변경 가능 여부 / Can change color</returns>
       public static bool CanChangeColor(PartsType partsType) => 
           partsType is PartsType.Hair_Short or PartsType.Brow or PartsType.Beard or PartsType.Skin;
       
       #endregion


       #region Utility Methods
       
       /// <summary>
       /// 스프라이트 가져오기
       /// Get sprite
       /// </summary>
       /// <param name="name">스프라이트 이름 / Sprite name</param>
       /// <returns>스프라이트 / Sprite</returns>
       public Sprite GetSprite(string name)
       {
           // 스프라이트 캐시 초기화 / Initialize sprite cache
           if (_spriteCache == null)
           {
               _spriteCache = new Dictionary<string, Sprite>();
               foreach (var s in sprites)
                   if (s != null) _spriteCache[s.name] = s;
           }

           // "/" 이후 이름으로 검색 / Search by name after "/"
           int slashIndex = name.IndexOf('/');
           string key = slashIndex >= 0 ? name.Substring(slashIndex + 1) : name;
           return _spriteCache.TryGetValue(key, out var sprite) ? sprite : null;
       }
       
       #endregion

       #region Button Events
       
       /// <summary>
       /// 랜덤 부품 버튼 클릭
       /// Click random parts button
       /// </summary>
       public void OnClick_RandomParts()
       {
           AudioManager.Instance.PlaySound(SoundList.ButtonRandom, 0.7f);
           PanelParts.PanelPartsList.OnClick_Close(false);
    
           // 부품 랜덤 적용 / Apply random parts
           Player.Instance.PartsManager.RandomParts();

           // 색상 랜덤 적용 / Apply random colors
           ColorPresetManager.Instance.SetRandomAllColor();

           // Hex 표시 업데이트 / Update hex display
           StartCoroutine(UpdateHexAfterRandomColors());
       }

       /// <summary>
       /// 랜덤 색상 적용 후 Hex 업데이트
       /// Update hex after applying random colors
       /// </summary>
       private System.Collections.IEnumerator UpdateHexAfterRandomColors()
       {
           yield return new WaitForEndOfFrame();
           yield return new WaitForEndOfFrame(); // 색상 적용 완료 대기 / Wait for color application to complete

           // 현재 선택된 부품의 색상으로 Hex 표시 업데이트 / Update hex display with current selected part's color
           if (ColorPicker.Instance != null)
           {
               var currentPartsType = ColorPicker.Instance.CurrentPartsType;
               if (currentPartsType != PartsType.None)
               {
                   Color currentColor = ColorPresetManager.Instance.GetColorByType(currentPartsType);
                   ColorFavoriteManager.Instance?.UpdateHexDisplay(currentColor);
               }
           }
       }
       #endregion

       #region SNS Button Events
       
       /// <summary>
       /// 디스코드 버튼 클릭
       /// Click Discord button
       /// </summary>
       public void OnClick_Discord()
       {
           
       }

       /// <summary>
       /// 페이스북 버튼 클릭
       /// Click Facebook button
       /// </summary>
       public void OnClick_Facebook()
       {
           
       }


       /// <summary>
       /// 에셋 스토어 버튼 클릭
       /// Click Asset Store button
       /// </summary>
       public void OnClick_AssetStore()
       {
           
       }

       /// <summary>
       /// 에셋 버튼 클릭
       /// Click Asset button
       /// </summary>
       public void OnClick_Asset()
       {
           
       }
       
       #endregion
   }
}