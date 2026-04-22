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

    public readonly struct GeneratedWave
    {
        public readonly int waveIndex;
        public readonly float triggerTimeSec;
        public readonly AttackUnitData unitA;
        public readonly int countA;
        public readonly AttackUnitData unitB;
        public readonly int countB;
        public readonly int totalCount;

        public GeneratedWave(
            int waveIndex,
            float triggerTimeSec,
            AttackUnitData unitA,
            int countA,
            AttackUnitData unitB,
            int countB)
        {
            this.waveIndex = waveIndex;
            this.triggerTimeSec = triggerTimeSec;
            this.unitA = unitA;
            this.countA = countA;
            this.unitB = unitB;
            this.countB = countB;
            totalCount = countA + countB;
        }
    }
}
