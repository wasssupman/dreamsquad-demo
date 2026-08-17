using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackDeck", menuName = "Wassup/AttackDeck", order = 11)]
    public class AttackDeck : ScriptableObject
    {
        public string deckId = "WaveA";

        // endless-mode-removal unit 0 — `battleMode` 필드는 제거했다. 실제로 그 축이 하던 일은
        // 「토너먼트 리포트 스킵」 하나였고, 그건 모드가 아니라 플래그로 충분했다. 모드 축이
        // 다시 필요해지면 «그 모드가 무엇을 다르게 하는가» 를 한 줄로 답할 수 있을 때 만든다.

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

        // wave-pull-revival unit 0 — 당김 상한. 「필드를 비운 뒤로 몇 번 더 얹을 수 있나」.
        //
        // 「기본 스케줄 대비 선행 N웨이브」로 재지 않는 이유: 빨리 밀면 자동 진행도 함께
        // 빨라져 «선행» 상태가 되는데, 그 계산식은 **잘 막은 플레이어의 당김을 막는다**.
        // 전멸로 리셋되는 카운터가 「빠른 안정화 → 더 일찍 투입할 선택권」을 그대로 구현하고,
        // 막으려던 것(무한 스태킹)도 같은 값으로 잡힌다.
        //
        // 0 이하는 «당김 금지»가 아니라 폴백이다(BattleBridge.MaxPullsPerClear) — 저작 누락이
        // 조용히 기능을 끄면 «버튼이 안 먹는다»의 원인을 찾을 수 없다.
        [Tooltip("필드를 비운 뒤 당길 수 있는 최대 횟수. 전멸하면 0으로 리셋. 0 이하=폴백 3.")]
        public int maxPullsPerClear = 3;

        // wave-pull-revival unit 3 — 「진출 예상선」 par 비율.
        // par(t) = 기본 진행으로 t 까지 나왔을 적의 점수 합 × 이 값.
        //
        // **이 기준선은 가짜다** — 진짜는 같은 시드를 돈 10인 분포에서 나와야 하는데(PRD §7.1)
        // 경기 중 서버 조회 경로가 없다. 감각을 먼저 확인하기 위한 임시 par 이며, 서버가
        // 생기면 PaceBaseline 안만 바뀐다.
        //
        // 값 감각: 1.0 = 나온 적을 전부 잡는 페이스(당김 없이는 거의 불가능). 「조금 부족」이
        // 대부분의 시간 동안 떠 있어야 압박이 된다 — 항상 앞서면 장식이고 항상 크게 뒤지면
        // 포기하게 된다. **0 이하 = 표시 안 함**(0 을 par 로 읽으면 «항상 앞섬» 거짓말이 된다).
        [Tooltip("진출 예상선 par 비율. 기본 진행 적 점수 × 이 값. 0 이하=표시 안 함. 현재 값은 서버 없는 임시 par.")]
        public float paceParFraction = 0.92f;

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
        // wave-concept-blocks unit 3 — 5 → 9 (사용자 결정 2026-08-13: 5마다는 너무 잦다).
        // 9 는 conceptHoldWaves=3 의 **블록 마지막** 웨이브라 「컨셉을 두 웨이브 배우고 세 번째에
        // 보스가 그 컨셉을 입고 온다」가 성립한다. 10 은 블록의 첫 웨이브라 «새 컨셉 + 보스»가
        // 겹치고, 최악 케이스 도달(floor(180/20)+1 = 10웨이브)에서 보스를 못 보는 판이 생긴다.
        [Tooltip("매 N번째 웨이브가 보스 웨이브(보스 1기 + 호위). <=0 이면 보스 웨이브 없음.")]
        public int bossWaveInterval = 9;
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

        // wave-concept-blocks unit 0 — 웨이브 컨셉 블록. Appended last (직렬화 back-compat).
        //
        // 컨셉은 웨이브가 아니라 **블록**의 속성이다: 블록 경계에서 하나 뽑고 conceptHoldWaves
        // 웨이브 동안 컨셉과 lane 배정을 유지한다. 풀이 비면 생성기가 구조적 폴백(슬롯 2개·
        // lane 무지정·countMul 1 = 현행 동작의 모양)으로 떨어지므로 무회귀 경로가 데이터로
        // 확보된다.
        [Header("Wave Concepts")]
        [Tooltip("웨이브 컨셉 풀. 비우면 현행 동작(랜덤 2종·전 lane 분산)으로 폴백.")]
        public WaveConceptData[] waveConceptPool = Array.Empty<WaveConceptData>();

        [Tooltip("한 컨셉이 유지되는 웨이브 수(블록 길이). 웨이브당 12~18초라 1이면 반응할 창이 없다.")]
        [Min(1)] public int conceptHoldWaves = 3;

        // wave-ramp-two-phase unit 0 — 두 단계 수량 곡선(옵트인). Appended last (직렬화 back-compat).
        //
        // breakWave 미저작(0) = 기존 지수 그대로 → 라이브 덱 무회귀가 데이터로 성립한다.
        // 저작 시: 본편(w1~break)은 min → breakUnits 평탄 상승, break 웨이브부터 breakUnits 를
        // 기점으로 unitGrowthPerWave 지수 — «난이도 낮은 다양한 본편 + 클라이맥스».
        // 이 필드는 unit 1 의 클라이맥스 변주 격상(상시 변주·신규 레인 개방)의 게이트도 겸한다 —
        // 목표 문장(«그 이후부터 지수 상승과 타입을 섞는다»)이 knob 하나로 표현된다.
        [Header("Wave Ramp (wave-ramp-two-phase)")]
        [Tooltip("두 단계 곡선의 경계 웨이브(그 웨이브부터 지수·변주 상시). 0 = 끔(기존 지수).")]
        [Min(0)] public int waveRampBreakWave;
        [Tooltip("경계 웨이브에서의 수량 — 평탄 구간의 종점이자 지수 구간의 기점. breakWave > 0 일 때만 의미.")]
        [Min(0)] public int waveRampBreakUnits;

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
