using System.Globalization;
using System.Text;

namespace Wassup.BattleCore.Wave
{
    // battle-core-rebuild unit 4 — 웨이브 저작의 **plain 투영**.
    //
    // 옛 생성기는 `AttackDeck`·`WaveConceptData`·`AttackUnitData`(전부 SO)를 직접 읽었다.
    // 코어는 엔진을 모르므로(제약 2) 그 저작을 **값**으로 받는다. 풀의 원소가 SO 참조가
    // 아니라 **`MatchDefinition.Enemies` 의 인덱스**인 것이 그 투영의 핵심이다 —
    // 「어느 적인가」의 답이 한 판 안에서 정수 하나가 되고, 생성 결과를 그대로 스폰에 넘길 수 있다.
    //
    // ⚠ **RNG 소비 순서가 계약이다**(cP5-16 · `WaveGenerator` 머리말). 이 구조체에 필드를
    // 더해도 생성기가 그것을 **읽는 순서**를 바꾸면 기존 맵의 편성이 통째로 밀린다.

    /// <summary>슬롯이 요구하는 고도. `Air` = 통행층에 Air 비트가 있다, `Ground` = 없다.</summary>
    public enum SlotAltitude : byte { Ground = 0, Air = 1 }

    /// <summary>컨셉 슬롯 하나. 「어떤 성질의 적이 · 어느 입구로 · 어느 경로를」.</summary>
    public struct WaveSlotDef
    {
        /// <summary>`EnemyDef.EnemyClass` 와 같은 축. 0 = 무필터.</summary>
        public int ClassFilter;

        public SlotAltitude Altitude;

        /// <summary>위상. 같은 값 = 같은 입구, 다른 값 = 다른 입구. -1 = 무지정.</summary>
        public int LaneGroup;

        /// <summary>저작 경로. -1 = 무지정.</summary>
        public int PathIndex;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv, string prefix)
        {
            MatchDefinition.Put(sb, prefix + "class", ClassFilter, inv);
            MatchDefinition.Put(sb, prefix + "alt", (int)Altitude, inv);
            MatchDefinition.Put(sb, prefix + "laneGroup", LaneGroup, inv);
            MatchDefinition.Put(sb, prefix + "path", PathIndex, inv);
        }
    }

    /// <summary>
    /// 컨셉 하나. **웨이브가 아니라 블록의 속성**이다 — 블록 경계에서 하나 뽑고
    /// `ConceptHoldWaves` 웨이브 동안 컨셉과 입구 배정을 유지한다.
    /// </summary>
    public struct WaveConceptDef
    {
        public string Id;
        public string DisplayName;
        public float Weight;
        public int MinWaveNumber;

        /// <summary>수량 배율. 「중장」 0.4 처럼 그 컨셉의 밀도를 정한다.</summary>
        public float CountMul;

        public WaveSlotDef[] Slots;

        /// <summary>변주 슬롯 — 블록 가운데 웨이브(클라이맥스에선 매 웨이브)에 **끼어든다**.</summary>
        public WaveSlotDef[] VariantSlots;

        public int EffectiveSlotCount => Slots != null ? Slots.Length : 0;

        /// <summary>
        /// 이 컨셉이 요구하는 입구 수 = 슬롯의 distinct `LaneGroup`(음수 제외) 수.
        /// **저작 필드로 두지 않는다** — 두면 파생값과 갈려 「저작은 2를 요구하는데 슬롯은
        /// 3 입구를 쓰는」 컨셉이 게이트를 통과한다.
        /// </summary>
        public int RequiredLaneCount
        {
            get
            {
                if (Slots == null) return 0;
                int n = 0;
                for (int i = 0; i < Slots.Length; i++)
                {
                    int g = Slots[i].LaneGroup;
                    if (g < 0) continue;
                    bool seen = false;
                    for (int j = 0; j < i && !seen; j++) seen = Slots[j].LaneGroup == g;
                    if (!seen) n++;
                }
                return n;
            }
        }

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            // `DisplayName` 은 **의도적으로 뺀다** — 표시 전용 문자열이라 규칙 입력이 아니다(이름만 바꾼
            // 같은 컨셉은 같은 판이어야 한다). 2026-09-24 드리프트 감사의 「누락」 지적은 오탐으로 종결.
            MatchDefinition.Put(sb, "weight", Weight, inv);
            MatchDefinition.Put(sb, "minWaveNumber", MinWaveNumber, inv);
            MatchDefinition.Put(sb, "countMul", CountMul, inv);
            if (Slots != null)
                for (int i = 0; i < Slots.Length; i++)
                    Slots[i].Canonicalize(sb, inv, "slot" + i.ToString(inv) + ".");
            if (VariantSlots != null)
                for (int i = 0; i < VariantSlots.Length; i++)
                    VariantSlots[i].Canonicalize(sb, inv, "var" + i.ToString(inv) + ".");
        }
    }

    /// <summary>
    /// 시드 생성 덱. `AttackDeck` 의 **웨이브 저작 절**과 1:1 이다.
    ///
    /// ⚠ 마음 체력(`goalStabilityMax`)·회복 배율(`killHealPerAwakening`)은 여기 **없다** —
    /// 같은 SO 에 살지만 주인이 `HeartMeter` 라 `HeartDef` 로 간다. 「웨이브 저작」을
    /// 읽으러 온 사람이 여기서 마음 체력을 발견하면 그 다음 사람이 그것을 웨이브 값으로 읽는다.
    /// </summary>
    public struct WaveDeckDef
    {
        public int GeneratorVersion;

        /// <summary>고정 오버라이드. **비0 = 같은 맵 같은 웨이브**, 0 = 판 시드에서 파생.</summary>
        public int WaveSeed;

        public float TimerDurationSec;
        public int MinWaveCount;
        public int MaxWaveCount;
        public int MinUnitsPerWave;
        public int MaxUnitsPerWave;
        public int WaveCountJitter;
        public float IntraWaveSpacingSec;

        /// <summary>웨이브 간 **상한** 간격. 전멸하면 즉시 다음, 못 잡으면 이 시각에 자동 진행.</summary>
        public float MaxWaveIntervalSec;

        /// <summary>트리거 ~ 첫 적 등장. **스폰 기준시각에만** 더한다(X10).</summary>
        public float SpawnLeadInSec;

        public float UnitGrowthPerWave;

        /// <summary>필드를 비운 뒤 당길 수 있는 횟수. **전멸로만 회복**한다. 0 이하 = 폴백 3.</summary>
        public int MaxPullsPerClear;

        public int BossWaveInterval;
        public int BossEscortMin;
        public int BossEscortMax;

        /// <summary>잡몹 풀 — `MatchDefinition.Enemies` 인덱스. 중복·음수는 생성기가 접는다.</summary>
        public int[] EnemyPool;

        /// <summary>보스 로테이션 풀. 비면 보스 웨이브 없음.</summary>
        public int[] BossPool;

        public WaveConceptDef[] Concepts;
        public int ConceptHoldWaves;

        public int RampBreakWave;
        public int RampBreakUnits;

        public const int PullCapFallback = 3;

        public int EffectiveMaxPullsPerClear
            => MaxPullsPerClear > 0 ? MaxPullsPerClear : PullCapFallback;

        public static WaveDeckDef Empty() => new WaveDeckDef
        {
            GeneratorVersion = 1,
            EnemyPool = System.Array.Empty<int>(),
            BossPool = System.Array.Empty<int>(),
            Concepts = System.Array.Empty<WaveConceptDef>(),
            ConceptHoldWaves = 3,
            UnitGrowthPerWave = 1f,
        };

        public bool HasPool => EnemyPool != null && EnemyPool.Length >= 2;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "generatorVersion", GeneratorVersion, inv);
            MatchDefinition.Put(sb, "waveSeed", WaveSeed, inv);
            MatchDefinition.Put(sb, "timerDurationSec", TimerDurationSec, inv);
            MatchDefinition.Put(sb, "minWaveCount", MinWaveCount, inv);
            MatchDefinition.Put(sb, "maxWaveCount", MaxWaveCount, inv);
            MatchDefinition.Put(sb, "minUnitsPerWave", MinUnitsPerWave, inv);
            MatchDefinition.Put(sb, "maxUnitsPerWave", MaxUnitsPerWave, inv);
            MatchDefinition.Put(sb, "waveCountJitter", WaveCountJitter, inv);
            MatchDefinition.Put(sb, "intraWaveSpacingSec", IntraWaveSpacingSec, inv);
            MatchDefinition.Put(sb, "maxWaveIntervalSec", MaxWaveIntervalSec, inv);
            MatchDefinition.Put(sb, "spawnLeadInSec", SpawnLeadInSec, inv);
            MatchDefinition.Put(sb, "unitGrowthPerWave", UnitGrowthPerWave, inv);
            MatchDefinition.Put(sb, "maxPullsPerClear", MaxPullsPerClear, inv);
            MatchDefinition.Put(sb, "bossWaveInterval", BossWaveInterval, inv);
            MatchDefinition.Put(sb, "bossEscortMin", BossEscortMin, inv);
            MatchDefinition.Put(sb, "bossEscortMax", BossEscortMax, inv);
            MatchDefinition.Put(sb, "conceptHoldWaves", ConceptHoldWaves, inv);
            MatchDefinition.Put(sb, "rampBreakWave", RampBreakWave, inv);
            MatchDefinition.Put(sb, "rampBreakUnits", RampBreakUnits, inv);
            Put(sb, inv, "pool", EnemyPool);
            Put(sb, inv, "bossPool", BossPool);
            if (Concepts != null)
                for (int i = 0; i < Concepts.Length; i++)
                {
                    sb.Append("[concept").Append(i.ToString(inv)).Append("]\n");
                    Concepts[i].Canonicalize(sb, inv);
                }
        }

        private static void Put(StringBuilder sb, CultureInfo inv, string key, int[] values)
        {
            if (values == null || values.Length == 0) { MatchDefinition.Put(sb, key, "~"); return; }
            var text = new StringBuilder(values.Length * 3);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(values[i].ToString(inv));
            }
            MatchDefinition.Put(sb, key, text.ToString());
        }
    }

    /// <summary>저작 웨이브 한 덩어리(그룹). 시각은 **웨이브 시작 기준 상대**다.</summary>
    public struct AuthoredGroupDef
    {
        public float TriggerTimeSec;
        public int EnemyIndex;
        public int Count;
        public int LaneIndex;
        public int PathIndex;
    }

    /// <summary>저작 웨이브 하나. 웨이브 i 의 절대 시작 = 앞 웨이브 `DurationSec` 의 합.</summary>
    public struct AuthoredWaveDef
    {
        public float DurationSec;
        public float IntervalSec;
        public AuthoredGroupDef[] Groups;
    }

    /// <summary>
    /// 저작 플랜. **케이던스가 타임라인**이라 전멸·당김 상한이 적용되지 않는다 —
    /// 저작자가 시각을 직접 적었다는 것이 그 면제의 근거다.
    /// </summary>
    public struct WavePlanDef
    {
        public string DisplayName;

        /// <summary>0 = 시간제한 없음(전 웨이브 dispatch + 전멸로 끝난다).</summary>
        public float TimerDurationSec;

        public AuthoredWaveDef[] Waves;

        public bool HasWaves => Waves != null && Waves.Length > 0;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "displayName", DisplayName);
            MatchDefinition.Put(sb, "timerDurationSec", TimerDurationSec, inv);
            if (Waves == null) return;
            for (int i = 0; i < Waves.Length; i++)
            {
                var w = Waves[i];
                MatchDefinition.Put(sb, "wave" + i.ToString(inv) + ".duration", w.DurationSec, inv);
                MatchDefinition.Put(sb, "wave" + i.ToString(inv) + ".interval", w.IntervalSec, inv);
                if (w.Groups == null) continue;
                for (int g = 0; g < w.Groups.Length; g++)
                {
                    var grp = w.Groups[g];
                    MatchDefinition.Put(sb,
                        "wave" + i.ToString(inv) + ".g" + g.ToString(inv),
                        grp.EnemyIndex.ToString(inv) + "," + grp.Count.ToString(inv) + ","
                        + grp.TriggerTimeSec.ToString("R", inv) + "," + grp.LaneIndex.ToString(inv)
                        + "," + grp.PathIndex.ToString(inv));
                }
            }
        }
    }

    /// <summary>
    /// 보너스 당김의 저작. **본류와 코드 경로를 공유하지 않는다** — 그쪽은 덱·시드·컨셉·레인을
    /// 다루고 이쪽은 「N기를 P개 포탈에 순서대로」가 전부다.
    /// </summary>
    public struct BonusWaveDef
    {
        /// <summary>보너스 적. -1 = 보너스 기능 없음.</summary>
        public int EnemyIndex;

        public int EnemyCount;
        public float PortalAppearDelaySec;
        public float FirstSpawnDelaySec;
        public float SpawnIntervalSec;

        /// <summary>이만큼의 **일반 적**을 처치할 때마다 크레딧이 한 회분 쌓인다.</summary>
        public int KillThreshold;

        /// <summary>이 스트레스 **이하**일 때만 제안된다. 등장 조건이지 유지 조건이 아니다.</summary>
        public float MaxStressToOffer;

        public bool Enabled => EnemyIndex >= 0 && EnemyCount > 0 && KillThreshold > 0;

        public float FirstSpawnAtSec => PortalAppearDelaySec + FirstSpawnDelaySec;

        public static BonusWaveDef None() => new BonusWaveDef { EnemyIndex = -1 };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "bonusEnemy", EnemyIndex, inv);
            MatchDefinition.Put(sb, "bonusCount", EnemyCount, inv);
            MatchDefinition.Put(sb, "bonusPortalDelay", PortalAppearDelaySec, inv);
            MatchDefinition.Put(sb, "bonusFirstDelay", FirstSpawnDelaySec, inv);
            MatchDefinition.Put(sb, "bonusInterval", SpawnIntervalSec, inv);
            MatchDefinition.Put(sb, "bonusKillThreshold", KillThreshold, inv);
            MatchDefinition.Put(sb, "bonusMaxStress", MaxStressToOffer, inv);
        }
    }
}
