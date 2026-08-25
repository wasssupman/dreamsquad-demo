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
        // wave-pattern unit 11 — 웨이브 트리거와 첫 적 등장 사이의 리드인(초). 스폰 base 에만
        // 더해지고 triggerTimeSec 그리드는 불변. 작성 플랜(FromPlanAsset)은 0 — 그룹 상대
        // 시각으로 같은 표현이 가능하므로 겹쳐 주면 이중 가산이 된다.
        public readonly float spawnLeadInSec;

        public GeneratedWavePlan(
            int seed,
            int generatorVersion,
            float timerDurationSec,
            float waveIntervalSec,
            float intraWaveSpacingSec,
            IReadOnlyList<GeneratedWave> waves,
            float spawnLeadInSec = 0f)
        {
            this.seed = seed;
            this.generatorVersion = generatorVersion;
            this.timerDurationSec = timerDurationSec;
            this.waveIntervalSec = waveIntervalSec;
            this.intraWaveSpacingSec = intraWaveSpacingSec;
            this.waves = waves;
            this.spawnLeadInSec = spawnLeadInSec;
        }
    }

    // wave-authoring-test-mode unit 6 — 웨이브 펼침 방식.
    //  RoundRobin: seed 경로. 그룹들을 A,B,A,B... 인터리브, intraWaveSpacing 간격(byte-identical).
    //  PerGroupTimeline: 작성 경로. 그룹마다 triggerOffsetSec(웨이브 상대) 에서 count 를
    //                    spawnIntervalSec 간격으로 펼침.
    public enum WaveExpandMode { RoundRobin, PerGroupTimeline }

    // wave-authoring-test-mode unit 1/6 — 한 웨이브 안의 (적 타입, 수량) 한 묶음.
    // triggerOffsetSec 은 PerGroupTimeline 에서만 사용(웨이브 시작 기준 상대 시각).
    public readonly struct WaveSpawnGroup
    {
        public readonly AttackUnitData unit;
        public readonly int count;
        public readonly float triggerOffsetSec;
        // wave-concept-blocks unit 2 — 컨셉이 지정한 스폰 지점. -1 = 무지정(기존
        // EffectiveSpawnIndex 경로). ExpandWave 가 이 값을 존중해야 하는 이유는
        // EffectiveSpawnIndex 가 3레인 이상 맵에서 authored 값을 무시하고 deckIndex 라운드로빈으로
        // 돌리기 때문이다 — 우회하지 않으면 저작한 lane 이 조용히 지워진다.
        public readonly int laneIndex;
        // duel-route-tours unit 1 — 컨셉 슬롯이 지정한 맵 경로. -1 = 무지정.
        // laneIndex 와 같은 여정을 탄다(그룹 → 펼침 → 브리지 → 엔티티). 보스·호위·레거시 덱
        // 스폰은 기본값 -1 이라 이 축이 생겨도 거동이 같다.
        public readonly int pathIndex;

        public WaveSpawnGroup(
            AttackUnitData unit, int count, float triggerOffsetSec = 0f, int laneIndex = -1,
            int pathIndex = -1)
        {
            this.unit = unit;
            this.count = count;
            this.triggerOffsetSec = triggerOffsetSec;
            this.laneIndex = laneIndex;
            this.pathIndex = pathIndex;
        }
    }

    // waypoint-routing unit 7 — 실제 스폰 펼침 결과가 어느 WaveSpawnGroup(현재의 스웜
    // 단위)에서 왔는지 보존한다. SpawnEntry 만 만들고 나중에 그룹을 역추적하면
    // RoundRobin/PerGroupTimeline 규칙을 예고 쪽에서 다시 구현하게 되므로, 펼칠 때 함께 싣는다.
    public readonly struct ExpandedWaveSpawn
    {
        public readonly SpawnEntry entry;
        public readonly int swarmIndex;
        public readonly int laneIndex;
        // duel-route-tours unit 1 — 이 스폰을 만든 그룹의 경로 지정. -1 = 무지정.
        // 스폰(BattleBridge)과 예고(BuildSpawnGuideForecasts)가 **같은 값**을 읽어야 하므로
        // 여기까지 실어 나른다 — 예고 쪽에서 그룹을 역추적하면 규칙이 두 벌이 된다.
        public readonly int pathIndex;

        public ExpandedWaveSpawn(SpawnEntry entry, int swarmIndex, int laneIndex, int pathIndex = -1)
        {
            this.entry = entry;
            this.swarmIndex = swarmIndex;
            this.laneIndex = laneIndex;
            this.pathIndex = pathIndex;
        }
    }

    // waypoint-routing unit 7 — Presentation 이 경로 가이드 하나를 그리는 데 필요한 plain 값.
    // SO 참조나 미래 스웜 ID를 넘기지 않는다. 현재 swarmIndex 는 GeneratedWave.groups 인덱스다.
    public readonly struct SpawnGuideForecast
    {
        public readonly int swarmIndex;
        public readonly int laneIndex;
        public readonly float firstSpawnSec;
        public readonly int waypointPathIndex;
        public readonly byte traversalLayers;

        public SpawnGuideForecast(
            int swarmIndex,
            int laneIndex,
            float firstSpawnSec,
            int waypointPathIndex,
            byte traversalLayers)
        {
            this.swarmIndex = swarmIndex;
            this.laneIndex = laneIndex;
            this.firstSpawnSec = firstSpawnSec;
            this.waypointPathIndex = waypointPathIndex;
            this.traversalLayers = traversalLayers;
        }
    }

    // wave-authoring-test-mode unit 1/6 — 2타입 고정에서 N개 그룹으로 일반화.
    // seed 경로는 2-entry 편의 생성자(RoundRobin) 로 만들어 기존 동작과 byte-identical.
    // 작성 플랜(WavePlanAsset)은 PerGroupTimeline 으로 같은 모델을 채운다.
    public readonly struct GeneratedWave
    {
        public readonly int waveIndex;
        public readonly float triggerTimeSec;        // 웨이브 절대 시작 시각
        public readonly IReadOnlyList<WaveSpawnGroup> groups;
        public readonly int totalCount;
        public readonly float spawnIntervalSec;       // PerGroupTimeline: count 펼침 간격
        public readonly WaveExpandMode expandMode;
        // wave-concept-blocks unit 2 — 이 웨이브를 만든 컨셉의 표시 이름. plain string 이다:
        // 뷰가 WaveConceptData 를 참조하면 프레젠테이션이 저작 데이터에 묶인다(SpawnGuideForecast
        // 가 plain 값만 넘긴 것과 같은 판단). "" = 컨셉 없음(레거시/폴백 경로).
        public readonly string conceptLabel;

        public GeneratedWave(
            int waveIndex,
            float triggerTimeSec,
            IReadOnlyList<WaveSpawnGroup> groups,
            float spawnIntervalSec = 0f,
            WaveExpandMode expandMode = WaveExpandMode.RoundRobin,
            string conceptLabel = "")
        {
            this.waveIndex = waveIndex;
            this.triggerTimeSec = triggerTimeSec;
            this.groups = groups;
            this.spawnIntervalSec = spawnIntervalSec;
            this.expandMode = expandMode;
            this.conceptLabel = conceptLabel ?? "";
            int total = 0;
            if (groups != null)
                for (int i = 0; i < groups.Count; i++) total += groups[i].count;
            totalCount = total;
        }

        // seed 경로 + 테스트용 2-entry 편의 생성자. 정확히 2개 그룹(RoundRobin).
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
            }, 0f, WaveExpandMode.RoundRobin)
        {
        }
    }
}
