namespace Wassup.BattleCore.Wave
{
    // battle-core-rebuild unit 4 — 생성 결과. 「이 판에 무엇이 · 언제 · 어디서 나오나」.
    //
    // 옛 `GeneratedWavePlan`/`GeneratedWave`/`WaveSpawnGroup` 의 후계다. 이름을 그대로
    // 쓰지 않는 이유는 Unity 층(`MatchDefinitionBuilder`)이 **두 어휘를 동시에** 본다는 것뿐이다 —
    // 같은 단순명이면 거기서 모호성이 난다.
    //
    // SO 참조가 없다: 적은 `MatchDefinition.Enemies` 의 **인덱스**다.

    /// <summary>
    /// 웨이브 안의 펼침 방식.
    ///   · `RoundRobin` — 시드 경로. 그룹을 A,B,A,B… 인터리브, 간격은 `IntraWaveSpacingSec`.
    ///   · `Timeline` — 저작 경로. 그룹마다 상대 시각에서 `SpawnIntervalSec` 간격으로.
    /// </summary>
    public enum WaveLayout : byte { RoundRobin = 0, Timeline = 1 }

    /// <summary>한 웨이브 안의 (적, 수량) 한 묶음.</summary>
    public struct PlannedGroup
    {
        public int EnemyIndex;
        public int Count;

        /// <summary>`Timeline` 에서만 쓰는 웨이브 상대 시각.</summary>
        public float TriggerOffsetSec;

        /// <summary>컨셉이 지정한 입구. -1 = 무지정(펼침 순번 라운드로빈).</summary>
        public int LaneIndex;

        /// <summary>컨셉 슬롯이 지정한 경로. -1 = 무지정.</summary>
        public int PathIndex;

        public PlannedGroup(int enemyIndex, int count, float triggerOffsetSec = 0f,
                            int laneIndex = -1, int pathIndex = -1)
        {
            EnemyIndex = enemyIndex;
            Count = count;
            TriggerOffsetSec = triggerOffsetSec;
            LaneIndex = laneIndex;
            PathIndex = pathIndex;
        }
    }

    public struct PlannedWave
    {
        public int WaveIndex;

        /// <summary>명목 트리거 시각(그리드). 런타임 케이던스는 **전멸 OR 상한 경과**라 이 값을 읽지 않는다.</summary>
        public float TriggerTimeSec;

        public PlannedGroup[] Groups;
        public float SpawnIntervalSec;
        public WaveLayout Layout;

        /// <summary>컨셉 표시 이름. plain string — 뷰가 저작 데이터를 참조하지 않게.</summary>
        public string ConceptLabel;

        /// <summary>
        /// 보스 웨이브인가. **판별은 생성기 한 곳**이고 경보도 그 값을 읽는다(X13) —
        /// 스폰 시점에 재판정하면 이중 발화한다.
        /// </summary>
        public bool IsBoss;

        public int TotalCount;

        public PlannedWave(int waveIndex, float triggerTimeSec, PlannedGroup[] groups,
                           float spawnIntervalSec = 0f, WaveLayout layout = WaveLayout.RoundRobin,
                           string conceptLabel = "", bool isBoss = false)
        {
            WaveIndex = waveIndex;
            TriggerTimeSec = triggerTimeSec;
            Groups = groups ?? System.Array.Empty<PlannedGroup>();
            SpawnIntervalSec = spawnIntervalSec;
            Layout = layout;
            ConceptLabel = conceptLabel ?? "";
            IsBoss = isBoss;
            int total = 0;
            if (groups != null)
                for (int i = 0; i < groups.Length; i++) total += groups[i].Count;
            TotalCount = total;
        }
    }

    /// <summary>펼쳐진 스폰 한 건. 「몇 초에 · 어느 적이 · 어느 입구로 · 어느 경로로」.</summary>
    public struct PlannedSpawn
    {
        public float TriggerTimeSec;
        public int EnemyIndex;

        /// <summary>저작 입구 번호(그룹 무지정이면 펼침 순번 라운드로빈).</summary>
        public int SpawnIndex;

        /// <summary>실제 입구. 3레인 이상에서 `SpawnIndex` 와 갈릴 수 있다.</summary>
        public int LaneIndex;

        public int SwarmIndex;
        public int PathIndex;
    }

    public sealed class WavePlan
    {
        public int Seed;
        public int GeneratorVersion;
        public float TimerDurationSec;

        /// <summary>명목 그리드 간격. 런타임 케이던스의 **상한 간격**이기도 하다.</summary>
        public float WaveIntervalSec;

        public float IntraWaveSpacingSec;

        /// <summary>트리거 ~ 첫 적 등장. **스폰 기준시각에만** 더한다(X10).</summary>
        public float SpawnLeadInSec;

        public PlannedWave[] Waves = System.Array.Empty<PlannedWave>();

        public int WaveCount => Waves != null ? Waves.Length : 0;

        public static WavePlan Empty() => new WavePlan();
    }
}
