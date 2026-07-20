using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackDeck", menuName = "Wassup/AttackDeck", order = 11)]
    public class AttackDeck : ScriptableObject
    {
        public string deckId = "WaveA";
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

        [Header("Boss Waves")]
        [Tooltip("보스 웨이브에 스폰할 보스 유닛. null이면 보스 웨이브 없음. attackUnitPool에 넣지 말 것 — 생성기가 방어적으로 제외한다.")]
        public AttackUnitData bossUnit;
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
