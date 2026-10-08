using System;
using UnityEngine;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Data
{
    // battle-core-rebuild unit 4 — **매치 모드.**
    //
    // 모드가 소유하는 것은 **지금 단일 소유자가 없는 값**뿐이다: 목표 · 종료 정책 ·
    // 점수 정책 · 시계 정책 · 수량 축. 웨이브 램프·당김 상한·보스 케이던스는 덱이 소유한
    // 채로 남는다 — 모드는 **값을 덮어쓰지 않고 «어느 저작 자산을 쓸지» 고른다**(원칙 3).
    // 다르게 하고 싶으면 다른 덱을 만든다.
    //
    // ⚠ **필드는 append-only** 다(사용자 판정 4). 제출·리플레이·리더보드가 `modeId` 를
    // 저장하므로 그 키는 **리네임 금지**이고, 중간에 필드를 끼우면 기존 에셋의 직렬화가 밀린다.
    //
    // 코어는 이 타입을 모른다. `MatchDefinitionBuilder` 가 여기서 plain `ModeDef` 로 굽고,
    // **그 빌더가 모드를 읽는 유일한 지점**이다.
    [CreateAssetMenu(fileName = "MatchMode", menuName = "Somnia/Battle/Match/Mode", order = 1)]
    public sealed class MatchModeData : ScriptableObject
    {
        [Header("정체성")]
        [Tooltip("안정 키. 제출·리플레이·리더보드가 저장한다 — 리네임 금지.")]
        public string modeId = "kill_score_timed";
        public string displayName = "";

        [Header("목표")]
        public GoalKind goalKind = GoalKind.KillScoreTimed;
        [Tooltip("WaveClear·TimeAttack 의 목표 웨이브 수. KillScoreTimed 는 안 읽는다.")]
        [Min(0)] public int targetWaves;

        [Header("시계")]
        public ClockKind clockKind = ClockKind.FixedLimit;
        [Tooltip("제한 시간(초). CountUp 모드에서는 안 읽는다.")]
        [Min(0f)] public float durationSec = 180f;
        [Min(0f)] public float submitUnlockSec = 60f;
        [Tooltip("제출 어휘를 여는 모드인가. false 면 제출 커맨드가 항상 거절된다.")]
        public bool allowSubmit = true;

        [Header("웨이브 원천")]
        public WaveSourceKind waveSourceKind = WaveSourceKind.GeneratedFromDeck;
        [Tooltip("비우면 맵 풀이 짝지은 덱을 쓴다.")]
        public AttackDeck deck;
        [Tooltip("저작 플랜. waveSourceKind = AuthoredPlan 일 때만 읽는다.")]
        public WavePlanAsset plan;

        [Header("맵")]
        [Tooltip("비우면 기본 풀. 선택은 seed % Count 그대로.")]
        // ⚠ 새 전투 코어의 빌더는 이 칸을 **아직 읽지 않는다.** 맵 풀 로테이션의 소비자 귀속은
        // `docs/spec/battle-core-rebuild/` 가 정한다(`deck` 의 「맵 풀이 짝지은 덱」도 같은 자리).
        public MapStagePool mapPool;

        [Header("기믹")]
        public bool gimmickEnabled;
        public GimmickData[] gimmickPool = Array.Empty<GimmickData>();

        [Header("배치 — 「배치 수량」 축")]
        // ⚠ **값을 복제하지 않고 에셋을 가리킨다**(원칙 3). 코스트 경제는 이미 단일 소유자
        // (`CostConfig`)가 있으므로 모드는 「어느 것을 쓸지」만 고른다 — 여기 숫자를 다시 두면
        // 같은 값이 두 곳에 살고, 언젠가 한쪽만 튜닝된다.
        [Tooltip("배치 자원 저작. 비우면 코드 기본값(시작 10 · 상한 15 · 초당 1).")]
        public CostConfig costConfig;
        [Tooltip("배치 페이즈에 입력을 받나. false 여도 페이즈 자체는 돈다 — 건너뛰면 트레이가 빈다.")]
        public bool placementPhaseEnabled = true;
        [Tooltip("placementPhaseEnabled = false 일 때 자동 시작까지의 카운트다운(초).")]
        [Min(0f)] public float autoStartCountdownSec = 3f;
        [Min(0)] public int squadSlots = 7;
        [Tooltip("판 위 방어유닛 총 상한. 0 = 모드 미지정(유닛 저작만 본다).")]
        [Min(0)] public int boardCap;
        public bool retireEnabled = true;

        [Header("드림캐쳐 — 「드림캐쳐 수량」 축")]
        [Tooltip("덱 규칙(덱 10 · Squad ≤2). 비우면 코드 기본값.")]
        public DeckRuleConfig deckRuleConfig;
        [Tooltip("각성 게이지·손패·부착 상한 저작. 비우면 코드 기본값.")]
        public AwakeningConfig awakeningConfig;
        [Min(0)] public int publicActiveCount = 2;

        [Header("토너먼트")]
        [Tooltip("이 판을 서버에 올리나. v1 은 KillScoreTimed 만.")]
        public bool submitsReport = true;
        [Tooltip("리더보드 키. 비우면 modeId 가 그 자리다.")]
        public string leaderboardId = "";

        /// <summary>
        /// 배치 창의 길이(초). 입력이 꺼진 모드는 **카운트다운**이 그 길이이고, 켜진 모드는
        /// 저작된 창 길이(`CostConfig.placementPhaseDuration`)다 — 0 이면 플레이어가 닫는다.
        /// 어느 쪽이든 **길이가 0 이어도 열림 신호는 난다**(census 계약 3).
        /// ⚠ `costConfig` 가 없을 때의 0 은 저작이 아니라 **누락**이다 — 옛 폴백(30초)과 다르므로
        /// 빌더(`MatchDefinitionBuilder.ToModeDef`)가 loud 오류로 알린다.
        /// </summary>
        public float PlacementSeconds
            => placementPhaseEnabled
                ? (costConfig != null ? Mathf.Max(0f, costConfig.placementPhaseDuration) : 0f)
                : Mathf.Max(0f, autoStartCountdownSec);

        public int DeckSize => deckRuleConfig != null ? deckRuleConfig.deckSize : 10;
        public int HandSize => awakeningConfig != null ? awakeningConfig.handSize : 5;
        public int AttachCap => awakeningConfig != null ? awakeningConfig.maxAttachPerUnit : 3;
        public float AwakeningStart => awakeningConfig != null ? awakeningConfig.gaugeStart : 20f;
        public float AwakeningMax => awakeningConfig != null ? awakeningConfig.gaugeMax : 100f;
        public float CostStart => costConfig != null ? costConfig.startingCost : 10f;
        public float CostMax => costConfig != null ? costConfig.maxCost : 15f;
        public float CostRegenPerSec => costConfig != null ? costConfig.regenPerSec : 1f;
    }
}
