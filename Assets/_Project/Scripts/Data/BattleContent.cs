using System;
using UnityEngine;
using Wassup.Data.Authoring;
using Wassup.Data.Season;

namespace Wassup.Data
{
    // battle-content-finish unit 0 — **판의 콘텐츠 묶음.** 모드와 무관하게 「이 전투에 존재하는 것」의 목록이다 —
    // 카탈로그 · 장판 · 길막 · 스택 · 부여 상한 · 이동 튜닝 · 시즌 · 보너스 웨이브 · 공용 액티브 풀.
    //
    // 모드(`MatchModeData`)에 넣지 않는 이유: 모드는 「닫힌 집합 + modeId 로 재현」이라 콘텐츠 목록을 품으면
    // 모드마다 같은 목록이 복제되고, 하나를 고칠 때 다른 모드가 조용히 옛 목록을 쓴다(D1). 씬 컴포넌트 필드에
    // 두지 않는 이유: 편성 하나 바꾸려고 씬을 열어야 하고, 씬 diff 에 저작과 배선이 섞인다.
    //
    // 읽는 자는 `BattleDriver` 뿐이고, 드라이버는 이것을 `MatchDefinitionBuilder` 의 입력으로 옮기기만 한다.
    [CreateAssetMenu(menuName = "Wassup/Battle Content", fileName = "BattleContent")]
    public sealed class BattleContent : ScriptableObject
    {
        [Header("카탈로그")]
        [Tooltip("입력(`MatchEntryInput.UnitIds`)의 유닛 id 를 푼다.")]
        public DefenderCatalog defenderCatalog;
        [Tooltip("입력의 드림스톤 id 를 푼다.")]
        public DreamstoneCatalog stoneCatalog;
        [Tooltip("입력(`MatchEntryInput.DeckCardIds`)의 덱을 푼다 — 없거나 검증에 실패하면 부착 덱은 비어 있다(기본 덱 폴백 없음).")]
        public DreamcatcherCardCatalog cardCatalog;

        [Header("판 위에 놓일 수 있는 것")]
        [Tooltip("존 장판 SO. 배열 순서 = 정의표 줄 번호. 비우면 장판 0.")]
        public HazardSO[] hazards = Array.Empty<HazardSO>();
        [Tooltip("탄이 참조하지 않는 길막 SO(디버그 · 생산자 전용). 탄이 참조하는 것은 탄 표에서 자동으로 모인다.")]
        public BlockingHazardSO[] extraBlockers = Array.Empty<BlockingHazardSO>();
        [Tooltip("스택 저작(불 · 얼음 · 출혈 · 피로도). 비우면 스택은 쌓이기만 하고 임계가 안 터진다.")]
        public StackModifierSO[] stackModifiers = Array.Empty<StackModifierSO>();
        [Tooltip("탄 부여 상한(한 발이 얼마까지 나르나). 줄이 없는 키의 부여는 관문이 거절한다.")]
        public ImbueCapConfig imbueCaps;

        [Header("규칙 튜닝 · 시즌")]
        [Tooltip("적이 어떻게 서고 어떻게 퍼지나. 비우면 코어 기본값.")]
        public MovementTuningConfig movementTuning;
        [Tooltip("시즌 등록부 — 활성 시즌의 맵 테마가 효과 타일 종류 · 개수를 준다. 비우면 효과 타일 0(조용히 지어내지 않는다).")]
        public SeasonRegistry seasonRegistry;
        [Tooltip("보너스 웨이브(당김) 저작. 포탈 뷰 타이밍도 여기서 읽는다.")]
        public BonusWaveData bonus;

        [Header("런타임 머티리얼")]
        [Tooltip("런타임이 복제해 쓰는 머티리얼 원본 묶음. 드라이버가 Awake 에서 `RuntimeMaterialFactory` 에 꽂는다.")]
        public RuntimeMaterialSet runtimeMaterials;

        [Header("공용 액티브 (판마다 굴림)")]
        [Tooltip("판마다 굴리는 공용 액티브의 스킬 풀. 굴림 시드 = 판 시드(재현).")]
        public SkillData[] activePool = Array.Empty<SkillData>();
        [Min(0)] public int activeCount = 2;
        [Tooltip("스킬을 감싸는 액티브 카드. 굴린 스킬을 감싸는 카드가 없으면 그 장만 빠진다.")]
        public DreamcatcherCard[] activeCards = Array.Empty<DreamcatcherCard>();

        /// <summary>활성 시즌의 맵 테마. 등록부 · 시즌 · 테마 중 하나라도 비면 null — 호출자가 loud 하게 말한다.</summary>
        public MapThemeData ActiveMapTheme
        {
            get
            {
                if (seasonRegistry == null) return null;
                var season = seasonRegistry.activeSeason;
                return season != null ? season.mapTheme : null;
            }
        }
    }
}
