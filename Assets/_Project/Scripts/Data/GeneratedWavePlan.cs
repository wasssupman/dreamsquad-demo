using System.Collections.Generic;

namespace Wassup.Data
{
    public readonly struct GeneratedWavePlan
    {
        public readonly int seed;
        public readonly int generatorVersion;
        public readonly float timerDurationSec;
        public readonly float waveIntervalSec;
        public readonly float intraWaveSpacingSec;
        public readonly IReadOnlyList<GeneratedWave> waves;

        public GeneratedWavePlan(
            int seed,
            int generatorVersion,
            float timerDurationSec,
            float waveIntervalSec,
            float intraWaveSpacingSec,
            IReadOnlyList<GeneratedWave> waves)
        {
            this.seed = seed;
            this.generatorVersion = generatorVersion;
            this.timerDurationSec = timerDurationSec;
            this.waveIntervalSec = waveIntervalSec;
            this.intraWaveSpacingSec = intraWaveSpacingSec;
            this.waves = waves;
        }
    }

    // wave-authoring-test-mode unit 1 — 한 웨이브 안의 (적 타입, 수량) 한 묶음.
    public readonly struct WaveSpawnGroup
    {
        public readonly AttackUnitData unit;
        public readonly int count;

        public WaveSpawnGroup(AttackUnitData unit, int count)
        {
            this.unit = unit;
            this.count = count;
        }
    }

    // wave-authoring-test-mode unit 1 — 2타입 고정에서 N개 그룹으로 일반화.
    // seed 경로는 2-entry 편의 생성자로 만들어 기존 동작과 byte-identical.
    // 작성 플랜(WavePlanAsset)은 N-entry 로 같은 모델을 채운다.
    public readonly struct GeneratedWave
    {
        public readonly int waveIndex;
        public readonly float triggerTimeSec;
        public readonly IReadOnlyList<WaveSpawnGroup> groups;
        public readonly int totalCount;

        public GeneratedWave(int waveIndex, float triggerTimeSec, IReadOnlyList<WaveSpawnGroup> groups)
        {
            this.waveIndex = waveIndex;
            this.triggerTimeSec = triggerTimeSec;
            this.groups = groups;
            int total = 0;
            if (groups != null)
                for (int i = 0; i < groups.Count; i++) total += groups[i].count;
            totalCount = total;
        }

        // seed 경로 + 테스트용 2-entry 편의 생성자. 정확히 2개 그룹을 만든다.
        public GeneratedWave(
            int waveIndex,
            float triggerTimeSec,
            AttackUnitData unitA,
            int countA,
            AttackUnitData unitB,
            int countB)
            : this(waveIndex, triggerTimeSec, new[]
            {
                new WaveSpawnGroup(unitA, countA),
                new WaveSpawnGroup(unitB, countB),
            })
        {
        }
    }
}
