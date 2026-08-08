using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackDeck", menuName = "Wassup/AttackDeck", order = 11)]
    public class AttackDeck : ScriptableObject
    {
        public string deckId = "WaveA";

        // endless-mode unit 0 — 배틀 모드. Main=기존 토너먼트, Endless=스코어어택 무한 모드.
        // BattleBridge 만 읽어 분기(간격/누수/시간축/리포트). 기본 Main 이라 기존 덱 동작 불변.
        [Header("Battle Mode")]
        public BattleMode battleMode = BattleMode.Main;

        [Header("Generated Waves")]
        public bool useGeneratedWaves = true;
        // wave-pattern unit 6(2026-07-20): 비0 = 라이브 고정 오버라이드 — 매판 동일 공격 패턴
        // (테스트 버전). 브리핑 스트립(Generate(deck))과 런타임이 같은 플랜을 공유하게 된다.
        // 0 = GameManager.matchSeed 파생(MatchSeed.DeriveWaveSeed, 매판 랜덤).
        // ResolveWaveSeed 의 0→1 폴백은 레거시 Generate(deck) 오버로드(프리뷰/테스트) 전용 —
        // 라이브 분기는 0 판별이 필요하므로 waveSeed 필드를 직접 본다(BattleBridge).
        public int waveSeed = 0;
        public int waveGeneratorVersion = 1;
        public AttackUnitData[] attackUnitPool;
        public int minWaveCount = 10;
        public int maxWaveCount = 15;
        public int minUnitsPerWave = 10;
        public int maxUnitsPerWave = 15;
        // wave-pattern unit 7 — 수량 램프: minUnitsPerWave(첫 웨이브)→maxUnitsPerWave(마지막)
        // 선형 증가값에 ±waveCountJitter 정수 지터를 더한 뒤 [min,max] 클램프. 0 = 순수 선형.
        [Tooltip("웨이브 진행 수량 램프의 지터 폭(±정수). 0 = 순수 선형. min→max 선형값을 이만큼 흔든 뒤 [min,max] 클램프.")]
        public int waveCountJitter = 1;
        public float intraWaveSpacingSec = 0.35f;

        // three-minute-survival unit 2 — 웨이브 간 **상한** 간격(초). 이전 웨이브를 전멸시키면
        // 즉시 다음 웨이브가 오고, 못 잡으면 이 시간에 자동으로 넘어간다(구 fixedWaveIntervalSec
        // 을 이 의미로 대체 — 고정 간격 스케줄은 은퇴했다). 0 = 제한시간/웨이브수 파생(레거시
        // 폴백, 직접 호출자 전용).
        //
        // ⚠ 스폰 창 불변식: `waveSpawnLeadInSec + (maxUnitsPerWave−1) × intraWaveSpacingSec`
        // 이 이 값보다 **작아야** 한다. 크면 _pending 이 영구히 비지 않아 "전멸 즉시 진행"이
        // 구조적으로 죽고 상한 케이던스만 남는다. 생성기가 위반 시 경고한다.
        [Tooltip("웨이브 간 상한 간격(초). 전멸하면 즉시 진행, 못 잡으면 이 시간에 자동 진행.")]
        public float maxWaveIntervalSec = 20f;

        // three-minute-survival unit 2 — 웨이브당 수량의 지수 성장률(웨이브마다 ×). 1 = 성장 없음.
        // total_i = clamp(round(minUnitsPerWave × growth^i) + jitter, minUnitsPerWave, maxUnitsPerWave).
        [Tooltip("웨이브당 수량 지수 성장률. 1=성장 없음. 초반은 완만하고 중반부터 붙는다.")]
        [Min(1f)] public float unitGrowthPerWave = 1.12f;

        // wave-pattern unit 11 — 웨이브 트리거와 첫 적 등장 사이의 균일 리드인(초).
        // 트리거 그리드(i*interval)·리스케줄(_waveTimeShift)·플랜 시각은 불변이고 QueueWave 의
        // 스폰 base 만 밀린다. 0 = 기존 동작(트리거 = 첫 스폰).
        // 작성 플랜(PerGroupTimeline)에는 적용되지 않는다 — 그룹 상대 시각으로 같은 표현이
        // 가능해 겹쳐 주면 이중 가산이 된다(그래서 값은 플랜에 실어 FromPlanAsset 이 0).
        [Tooltip("웨이브 시작과 첫 적 등장 사이의 리드인(초). 0=즉시 스폰(기존). 작성 플랜에는 미적용.")]
        public float waveSpawnLeadInSec = 2f;

        [Header("Boss Waves")]
        [Tooltip("보스 웨이브에 스폰할 보스 유닛. null이면 보스 웨이브 없음. attackUnitPool에 넣지 말 것 — 생성기가 방어적으로 제외한다.")]
        public AttackUnitData bossUnit;
        // boss-jjangssen unit 0 — 보스 2종+ 로테이션. bossUnit 을 **rename 하지 않고** 병행 유지하는 것이
        // 계약이다: 라이브 덱 asset 들이 bossUnit guid 를 직렬화하고 있어서 키를 바꾸면 YAML orphan 이
        // 되고, 생성기가 "보스 없음" 을 graceful no-op 으로 처리하므로 **에러도 경고도 없이** 전 맵에서
        // 보스만 사라진다. 비어 있으면 생성기가 bossUnit 단일로 폴백한다(폴백 소유자는 생성기 —
        // 덱을 안 거치는 직접 호출자도 같은 규칙을 받아야 하므로 한 곳에만 둔다).
        [Tooltip("보스 로테이션 풀. 비우면 bossUnit 단일로 폴백. attackUnitPool에 넣지 말 것 — 생성기가 방어적으로 제외한다.")]
        public AttackUnitData[] bossPool = Array.Empty<AttackUnitData>();
        [Tooltip("매 N번째 웨이브가 보스 웨이브(보스 1기 + 잡몹 호위). <=0 이면 보스 웨이브 없음.")]
        public int bossWaveInterval = 5;
        [Tooltip("보스 웨이브의 잡몹 호위 최소 수")]
        public int bossEscortMin = 3;
        [Tooltip("보스 웨이브의 잡몹 호위 최대 수")]
        public int bossEscortMax = 4;

        [Header("Legacy Spawns")]
        public List<SpawnEntry> spawns = new();
        public int defeatGoalReachedCount = 5;
        public float timerDurationSec = 180f;

        // three-minute-survival unit 0 — 골 안정도 최대치. 유출한 적의 stabilityDamage 합이
        // 이 값에 닿으면 패배다. defeatGoalReachedCount(스트레스 한계)는 더 이상 패배를
        // 만들지 않지만 몽마의 계약이 그 값을 지불 대상으로 쓰므로 **은퇴시키지 않는다**.
        // Appended last (직렬화 back-compat).
        [Header("Goal Stability")]
        [Tooltip("골 안정도 최대치. 유출한 적의 stabilityDamage 합이 이 값에 닿으면 패배.")]
        // 2026-08-08 — 20 → 1000. 공성(goal-tower-siege)이 붙으면서 20 은 적 2~3대에 녹아
        // "3분 생존" 전제가 성립하지 않았다(검증에서 첫 접촉 몇 초 뒤 패배). 1000 은 사용자 결정.
        [Min(1)] public int goalStabilityMax = 1000;

        public int ResolveWaveSeed()
        {
            return waveSeed != 0 ? waveSeed : 1;
        }

        public AttackUnitData[] ResolveAttackUnitPool()
        {
            if (attackUnitPool != null && attackUnitPool.Length >= 2)
                return attackUnitPool;

            if (spawns == null || spawns.Count == 0)
                return Array.Empty<AttackUnitData>();

            var units = new List<AttackUnitData>();
            for (int i = 0; i < spawns.Count; i++)
            {
                var unit = spawns[i].unitType;
                if (unit == null || units.Contains(unit)) continue;
                units.Add(unit);
            }
            return units.ToArray();
        }
    }

    [Serializable]
    public class SpawnEntry
    {
        public float triggerTimeSec;
        public AttackUnitData unitType;
        [Tooltip("GeneratedMap.spawns array index. Out-of-range values fall back to index 0.")]
        public int spawnIndex;
    }
}
