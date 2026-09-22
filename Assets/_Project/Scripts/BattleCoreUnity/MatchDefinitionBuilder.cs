using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Wave;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 1 — SO → 정의표. 계약 6 의 «판 밖에서 안으로» 의 문이다.
    //
    // 여기가 Unity 층인 이유: `ScriptableObject` 를 아는 마지막 자리이기 때문이다.
    // 이 함수를 지나면 값은 plain 이고, 코어는 시트도 SO 도 아트도 모른다.
    //
    // **읽는 것은 plain 수치·열거형뿐**이다. Mesh·Material·Prefab·AudioClip·
    // SkeletonDataAsset 같은 아트 참조는 읽지 않는다 — 옛 `MatchConfigSnapshot` 은
    // 리플렉션으로 SO 를 통째로 접느라 「아트 타입 제외 목록」을 손으로 유지해야 했고,
    // 목록이 낡으면 스킨 교체가 「조건이 바뀌었다」로 읽혔다. 명시 필드에는 그 함정이
    // 원리적으로 없다(정의표 타입에 아트 필드가 아예 없다).
    //
    // ⚠ 정의표에 필드를 추가하면 `MatchDefinition.Canonicalize` 도 같이 고친다.
    // 안 고치면 「스탯을 바꿨는데 해시가 그대로」라는 조용한 실패가 된다.
    public static class MatchDefinitionBuilder
    {
        /// <summary>
        /// **모드를 읽는 유일한 지점.** 판 밖의 저작(모드 SO · 덱 · 플랜 · 기믹 풀)을 한 번에
        /// plain 정의표로 굽는다.
        ///
        /// 적 목록을 **여기서 모으는** 것이 핵심이다: 코어의 웨이브 저작은 SO 참조가 아니라
        /// `MatchDefinition.Enemies` 의 **인덱스**라서, 표를 만드는 쪽과 인덱스를 매기는 쪽이
        /// 갈리면 웨이브가 엉뚱한 적을 부른다.
        /// </summary>
        public static MatchDefinition Build(MatchModeData mode,
                                            DefenderUnitData[] defenders,
                                            AttackDeck deck,
                                            WavePlanAsset plan,
                                            BonusWaveData bonus,
                                            int seed,
                                            float costRateMultiplier = 1f,
                                            in GeneratedMap map = default,
                                            float tileSize = 1f,
                                            System.Collections.Generic.IReadOnlyList<StructureEntry> structures = null)
        {
            var enemies = CollectEnemies(deck, plan, bonus);
            var def = Build(defenders, enemies, seed, ToModeDef(mode), in map, tileSize, structures);

            def.CostRateMultiplier = Mathf.Max(0f, costRateMultiplier);
            def.WaveDeck = ToDeckDef(deck, enemies);
            def.WavePlan = ToPlanDef(plan, enemies);
            def.Bonus = ToBonusDef(bonus, enemies);
            def.Heart = ToHeartConfig(deck);
            def.Gimmicks = ToGimmickDefs(mode);
            def.Roster = RosterOf(defenders);

            // ⚠ 정의표가 다 찬 **뒤에** 굽는다. 먼저 구우면 「덱을 바꿨는데 해시가 그대로」가 된다.
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// 모드 선택 3단: **테스트 모드 강제 &gt; 로비/서버 지정 &gt; 기본 모드**.
        /// 한 줄짜리 규칙이지만 호출처가 셋(스쿼드·테스트·토너먼트)이라 여기 한 곳에 둔다 —
        /// 세 곳에 두면 언젠가 하나가 다른 순서를 쓴다.
        /// </summary>
        public static MatchModeData ResolveMode(MatchModeData testOverride,
                                                MatchModeData lobbyOrServer,
                                                MatchModeData fallback)
            => testOverride != null ? testOverride
             : lobbyOrServer != null ? lobbyOrServer
             : fallback;

        /// <summary>
        /// ⚠ `structures` 는 **스테이지 저작 목록**(`StructureMarker` 산출)이다. 격자 투영
        /// (`GeneratedMap.structures`)에는 셀과 진영밖에 없어 스탯이 없다 — 둘을 칸으로
        /// 맞추는 것이 `CombatDefinitionBuilder.FillStructures` 의 일이고, 안 넘기면
        /// 저작 거점은 **한 기도 안 선다**(조용히 기본 스탯으로 세우지 않는다).
        /// </summary>
        public static MatchDefinition Build(DefenderUnitData[] defenders,
                                            AttackUnitData[] enemies,
                                            int seed,
                                            ModeDef mode,
                                            in GeneratedMap map = default,
                                            float tileSize = 1f,
                                            System.Collections.Generic.IReadOnlyList<StructureEntry> structures = null)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Mode = mode,
                Units = BuildUnits(defenders),
                Enemies = BuildEnemies(enemies),
                Map = BuildMap(in map, tileSize),
            };
            // unit 3 — 전투 저작(공격·탄·발사 명세)을 같은 줄에 채워 넣는다. **해시를 굽기 전**
            // 이어야 한다 — 뒤에 두면 「스탯을 바꿨는데 해시가 그대로」가 된다.
            CombatDefinitionBuilder.Fill(def, defenders, enemies, structures);
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// 스캐너 산출(`GeneratedMap`)을 plain 스냅샷으로 접는다. **스캐너는 무변**이다 —
        /// 그쪽은 씬 계층을 훑어 칸 격자를 파는 저작 파이프라인이고, 여기는 그 결과를 읽기만 한다.
        ///
        /// ⚠ **통행 층은 `tiles` 에서만 파생한다**(M1). `placeMask` 는 저작 그대로 싣되 통행에
        /// 쓰지 않는다 — 저작 의미가 «어느 유닛이 여기 설 수 있나» 라서, 통행으로 읽으면
        /// 「배치 금지」로 칠한 통로가 라우팅에서 사라진다(실측 사고).
        /// </summary>
        public static MapSnapshot BuildMap(in GeneratedMap map, float tileSize)
        {
            var snap = MapSnapshot.Empty();
            snap.TileSize = tileSize > 0f ? tileSize : 1f;
            if (!map.IsCreated) return snap;

            snap.Width = map.gridSize.x;
            snap.Height = map.gridSize.y;
            int n = snap.Width * snap.Height;

            snap.Tiles = new MapTile[n];
            snap.PlaceMask = new byte[n];
            snap.CellLayers = new byte[n];
            for (int i = 0; i < n; i++)
            {
                var tile = (MapTile)(byte)map.tiles[i];
                snap.Tiles[i] = tile;
                snap.PlaceMask[i] = map.placeMask.IsCreated
                    ? PlacementLayers.Sanitize(map.placeMask[i])
                    : LayerBits.Derive(tile);
                snap.CellLayers[i] = LayerBits.Derive(tile);
            }

            snap.Spawns = Copy(map.spawns);
            snap.Goals = map.goals.IsCreated && map.goals.Length > 0
                ? Copy(map.goals)
                : new[] { new int2(map.goal.x, map.goal.y) };
            snap.WaypointCells = Copy(map.waypointCells);
            snap.WaypointRanges = Copy(map.waypointRanges);

            if (map.spawnRoutes.IsCreated)
            {
                snap.SpawnRoutes = new int[map.spawnRoutes.Length];
                for (int i = 0; i < snap.SpawnRoutes.Length; i++) snap.SpawnRoutes[i] = map.spawnRoutes[i];
            }

            if (map.structures.IsCreated)
            {
                snap.Structures = new StructureSpot[map.structures.Length];
                for (int i = 0; i < snap.Structures.Length; i++)
                {
                    var st = map.structures[i];
                    snap.Structures[i] = new StructureSpot
                    {
                        Cell = st.cell,
                        Faction = (int)st.faction,
                        // 크기는 **종류에서 파생한다**. 상수를 박으면 1×1 마음이 3×3 을
                        // 차지한다고 거짓말한다.
                        Footprint = StructurePlacements.FootprintOf(st.faction),
                        // 스탯 줄은 스테이지 저작과 칸을 맞춰야 정해진다 — 그 일은
                        // `CombatDefinitionBuilder.FillStructures` 가 한다.
                        DefIndex = -1,
                    };
                }
            }

            snap.BonusSpawns = Copy(map.bonusSpawns);

            // 스폰·골·거점 자리는 배치를 받지 않는다(옛 `BattleBridge.CloseCellLayers`).
            // **저작을 읽은 뒤 마지막에 덮는다** — 규칙이 저작본을 오염시키지 않게 하는 순서다.
            snap.CloseReservedPlacement();
            return snap;
        }

        private static int2[] Copy(Unity.Collections.NativeArray<int2> src)
        {
            if (!src.IsCreated || src.Length == 0) return System.Array.Empty<int2>();
            var outp = new int2[src.Length];
            for (int i = 0; i < outp.Length; i++) outp[i] = src[i];
            return outp;
        }

        private static UnitDef[] BuildUnits(DefenderUnitData[] src)
        {
            if (src == null) return System.Array.Empty<UnitDef>();
            var outp = new UnitDef[src.Length];
            for (int i = 0; i < src.Length; i++) outp[i] = ToUnitDef(src[i]);
            return outp;
        }

        /// <summary>
        /// 방어유닛 SO 한 장 → 정의표 한 줄. `public` 인 이유: 순찰 소환물처럼 **카탈로그에
        /// 없는** 에셋을 unit 3 의 전투 빌더가 표에 편입해야 한다(그쪽이 같은 변환을 복제하면
        /// 두 벌이 갈린다).
        /// </summary>
        public static UnitDef ToUnitDef(DefenderUnitData d)
        {
            if (d == null) return default;
            var fp = d.Footprint;
            return new UnitDef
            {
                Id = d.id,
                Health = d.health,
                AttackRange = d.attackRange,
                AttackCooldown = d.attackCooldown,
                HitDelaySeconds = d.hitDelaySec,
                AttackTargetCount = d.attackTargetCount,
                BodyRadiusTiles = d.BodyRadiusTiles,
                FootprintWidth = fp.x,
                FootprintHeight = fp.y,
                PlacementLayers = (int)d.EffectivePlacementLayers,
                TraversalLayers = (int)d.EffectiveTraversalLayers,
                Role = (int)d.role,
                AggroCapacity = d.aggroCapacity,
                TargetFactions = (int)d.targetFactions,
                MoveSpeed = d.moveSpeed,
            };
        }

        // ── unit 4: 모드·덱·플랜·기믹 → plain ────────────────────────────────

        public static ModeDef ToModeDef(MatchModeData m)
        {
            if (m == null) return ModeDef.Default();
            return new ModeDef
            {
                ModeId = m.modeId,
                Goal = m.goalKind,
                TargetWaves = m.targetWaves,
                Clock = m.clockKind,
                MatchSeconds = m.durationSec,
                SubmitUnlockSeconds = m.submitUnlockSec,
                AllowSubmit = m.allowSubmit,
                WaveSource = m.waveSourceKind,
                GimmickEnabled = m.gimmickEnabled,
                PlacementInputEnabled = m.placementPhaseEnabled,
                PlacementSeconds = m.PlacementSeconds,
                SquadSlots = m.squadSlots,
                BoardCap = m.boardCap,
                RetireEnabled = m.retireEnabled,
                Cost = new CostDef
                {
                    Start = m.CostStart,
                    Max = m.CostMax,
                    RegenPerSec = m.CostRegenPerSec,
                },
                DeckSize = m.DeckSize,
                PublicActiveCount = m.publicActiveCount,
                HandSize = m.HandSize,
                AttachCap = m.AttachCap,
                Awakening = new AwakeningDef { Start = m.AwakeningStart, Max = m.AwakeningMax },
                SubmitsReport = m.submitsReport,
                LeaderboardId = m.leaderboardId,
            };
        }

        /// <summary>
        /// 그 판에 나올 수 있는 적 **전부**를 순서대로 모은다(중복 제거). 이 순서가 곧
        /// 정의표 인덱스이고, 웨이브·보스·보너스가 그 번호로 서로를 가리킨다.
        /// </summary>
        public static AttackUnitData[] CollectEnemies(AttackDeck deck, WavePlanAsset plan, BonusWaveData bonus)
        {
            var list = new System.Collections.Generic.List<AttackUnitData>(24);
            if (deck != null)
            {
                Add(list, deck.ResolveAttackUnitPool());
                Add(list, deck.bossPool);
                Add(list, deck.bossUnit);
            }
            if (plan != null && plan.waves != null)
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if (w == null || w.groups == null) continue;
                    for (int g = 0; g < w.groups.Count; g++)
                        Add(list, w.groups[g] != null ? w.groups[g].unit : null);
                }
            if (bonus != null) Add(list, bonus.enemyUnit);
            return list.ToArray();

            void Add(System.Collections.Generic.List<AttackUnitData> into, params AttackUnitData[] units)
            {
                if (units == null) return;
                for (int i = 0; i < units.Length; i++)
                {
                    var u = units[i];
                    if (u == null || into.Contains(u)) continue;
                    into.Add(u);
                }
            }
        }

        private static int IndexOf(AttackUnitData[] enemies, AttackUnitData unit)
        {
            if (unit == null || enemies == null) return -1;
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] == unit) return i;
            return -1;
        }

        private static int[] IndicesOf(AttackUnitData[] enemies, System.Collections.Generic.IReadOnlyList<AttackUnitData> units)
        {
            if (units == null) return System.Array.Empty<int>();
            var list = new System.Collections.Generic.List<int>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                int at = IndexOf(enemies, units[i]);
                if (at >= 0 && !list.Contains(at)) list.Add(at);
            }
            return list.ToArray();
        }

        public static WaveDeckDef ToDeckDef(AttackDeck deck, AttackUnitData[] enemies)
        {
            if (deck == null) return WaveDeckDef.Empty();

            // 보스 폴백(풀이 비면 `bossUnit` 단일)은 **여기서** 접는다. 옛 구현은 생성기가
            // 그 폴백을 소유했는데, 코어의 덱 정의표에는 단일 보스 칸이 아예 없다 —
            // 「두 표현 중 하나」가 사라졌으므로 접는 자리도 SO 를 아는 쪽으로 옮겼다.
            var bossList = new System.Collections.Generic.List<AttackUnitData>(4);
            if (deck.bossPool != null)
                for (int i = 0; i < deck.bossPool.Length; i++)
                    if (deck.bossPool[i] != null && !bossList.Contains(deck.bossPool[i]))
                        bossList.Add(deck.bossPool[i]);
            if (bossList.Count == 0 && deck.bossUnit != null) bossList.Add(deck.bossUnit);

            return new WaveDeckDef
            {
                GeneratorVersion = deck.waveGeneratorVersion,
                WaveSeed = deck.waveSeed,
                TimerDurationSec = deck.timerDurationSec,
                MinWaveCount = deck.minWaveCount,
                MaxWaveCount = deck.maxWaveCount,
                MinUnitsPerWave = deck.minUnitsPerWave,
                MaxUnitsPerWave = deck.maxUnitsPerWave,
                WaveCountJitter = deck.waveCountJitter,
                IntraWaveSpacingSec = deck.intraWaveSpacingSec,
                MaxWaveIntervalSec = deck.maxWaveIntervalSec,
                SpawnLeadInSec = deck.waveSpawnLeadInSec,
                UnitGrowthPerWave = deck.unitGrowthPerWave,
                MaxPullsPerClear = deck.maxPullsPerClear,
                BossWaveInterval = deck.bossWaveInterval,
                BossEscortMin = deck.bossEscortMin,
                BossEscortMax = deck.bossEscortMax,
                EnemyPool = IndicesOf(enemies, deck.ResolveAttackUnitPool()),
                BossPool = IndicesOf(enemies, bossList),
                Concepts = ToConceptDefs(deck.waveConceptPool),
                ConceptHoldWaves = deck.conceptHoldWaves,
                RampBreakWave = deck.waveRampBreakWave,
                RampBreakUnits = deck.waveRampBreakUnits,
            };
        }

        private static WaveConceptDef[] ToConceptDefs(WaveConceptData[] pool)
        {
            if (pool == null) return System.Array.Empty<WaveConceptDef>();
            var list = new System.Collections.Generic.List<WaveConceptDef>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                var c = pool[i];
                if (c == null) continue;
                list.Add(new WaveConceptDef
                {
                    Id = c.id,
                    DisplayName = c.displayName,
                    Weight = c.weight,
                    MinWaveNumber = c.minWaveNumber,
                    CountMul = c.countMul,
                    Slots = ToSlotDefs(c.slots),
                    VariantSlots = ToSlotDefs(c.variantSlots),
                });
            }
            return list.ToArray();
        }

        private static WaveSlotDef[] ToSlotDefs(WaveConceptSlot[] slots)
        {
            if (slots == null) return System.Array.Empty<WaveSlotDef>();
            var list = new System.Collections.Generic.List<WaveSlotDef>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                list.Add(new WaveSlotDef
                {
                    ClassFilter = (int)s.classFilter,
                    Altitude = s.altitude == Wassup.Data.SlotAltitude.Air
                        ? Wassup.BattleCore.Wave.SlotAltitude.Air
                        : Wassup.BattleCore.Wave.SlotAltitude.Ground,
                    LaneGroup = s.laneGroup,
                    PathIndex = s.pathIndex,
                });
            }
            return list.ToArray();
        }

        public static WavePlanDef ToPlanDef(WavePlanAsset plan, AttackUnitData[] enemies)
        {
            if (plan == null || plan.waves == null) return default;
            var waves = new AuthoredWaveDef[plan.waves.Count];
            for (int i = 0; i < waves.Length; i++)
            {
                var w = plan.waves[i];
                var groups = System.Array.Empty<AuthoredGroupDef>();
                if (w != null && w.groups != null)
                {
                    var list = new System.Collections.Generic.List<AuthoredGroupDef>(w.groups.Count);
                    for (int g = 0; g < w.groups.Count; g++)
                    {
                        var grp = w.groups[g];
                        if (grp == null || grp.unit == null || grp.count <= 0) continue;
                        list.Add(new AuthoredGroupDef
                        {
                            TriggerTimeSec = grp.triggerTimeSec,
                            EnemyIndex = IndexOf(enemies, grp.unit),
                            Count = grp.count,
                            LaneIndex = grp.laneIndex,
                            PathIndex = -1,
                        });
                    }
                    groups = list.ToArray();
                }
                waves[i] = new AuthoredWaveDef
                {
                    DurationSec = w != null ? w.durationSec : 0f,
                    IntervalSec = w != null ? w.intervalSec : 0f,
                    Groups = groups,
                };
            }
            return new WavePlanDef
            {
                DisplayName = plan.displayName,
                TimerDurationSec = plan.timerDurationSec,
                Waves = waves,
            };
        }

        public static BonusWaveDef ToBonusDef(BonusWaveData bonus, AttackUnitData[] enemies)
        {
            if (bonus == null || bonus.enemyUnit == null) return BonusWaveDef.None();
            return new BonusWaveDef
            {
                EnemyIndex = IndexOf(enemies, bonus.enemyUnit),
                EnemyCount = bonus.enemyCount,
                PortalAppearDelaySec = bonus.portalAppearDelaySec,
                FirstSpawnDelaySec = bonus.firstSpawnDelaySec,
                SpawnIntervalSec = bonus.spawnIntervalSec,
                KillThreshold = bonus.killThreshold,
                MaxStressToOffer = bonus.maxStressToOffer,
            };
        }

        /// <summary>
        /// 마음의 저작. 덱 SO 에 살지만 주인은 `HeartMeter` 라 **담당자 이름으로** 넘긴다 —
        /// 웨이브 정의표 안에 두면 「웨이브 저작」을 읽으러 온 사람이 거기서 마음 체력을 만난다.
        /// </summary>
        public static HeartDef ToHeartConfig(AttackDeck deck)
            => deck == null
                ? HeartDef.Default()
                : new HeartDef
                {
                    MaxHealth = deck.goalStabilityMax,
                    KillHealPerAwakening = deck.killHealPerAwakening,
                };

        private static GimmickDef[] ToGimmickDefs(MatchModeData mode)
        {
            if (mode == null || !mode.gimmickEnabled || mode.gimmickPool == null)
                return System.Array.Empty<GimmickDef>();
            var list = new System.Collections.Generic.List<GimmickDef>(mode.gimmickPool.Length);
            for (int i = 0; i < mode.gimmickPool.Length; i++)
            {
                var g = mode.gimmickPool[i];
                if (g == null) continue;
                list.Add(new GimmickDef { Id = g.gimmickId });
            }
            return list.ToArray();
        }

        // 놓을 수 있는 유닛 = 반입한 스쿼드 **그대로**다. 전투 빌더가 나중에 카탈로그 밖
        // 에셋(순찰 소환물)을 표에 편입하므로, 그 전에 찍어 둔 이 번호들이 로스터가 된다.
        private static int[] RosterOf(DefenderUnitData[] defenders)
        {
            if (defenders == null) return System.Array.Empty<int>();
            var list = new System.Collections.Generic.List<int>(defenders.Length);
            for (int i = 0; i < defenders.Length; i++)
                if (defenders[i] != null) list.Add(i);
            return list.ToArray();
        }

        private static EnemyDef[] BuildEnemies(AttackUnitData[] src)
        {
            if (src == null) return System.Array.Empty<EnemyDef>();
            var outp = new EnemyDef[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var e = src[i];
                if (e == null) continue;
                outp[i] = new EnemyDef
                {
                    Id = e.id,
                    Health = e.health,
                    MoveSpeed = e.moveSpeed,
                    AttackRange = e.attackRange,
                    AttackCooldown = e.attackCooldown,
                    HitDelaySeconds = e.hitDelaySec,
                    AttackTargetCount = e.attackTargetCount,
                    // ⚠ **`bodyRadius` 가 아니라 `BodyRadiusTiles` 다.** 앞의 날 필드는
                    // `bodySize == Boss` 일 때만 읽히는 저작 칸이고, 나머지 크기는 파생이다
                    // (Small 0.25 · Medium 0.5 · Large 1.0). 날 필드를 실으면 중형·대형 적의
                    // **몸이 통째로 0.25 로 줄어** 제약 13 의 「대상의 몸」 항이 틀린다.
                    // 방어유닛 줄은 처음부터 파생값(`d.BodyRadiusTiles`)을 쓰고 있었다 — 비대칭이었다.
                    BodyRadius = e.BodyRadiusTiles,
                    TraversalLayers = (int)e.EffectiveTraversalLayers,
                    EnemyClass = (int)e.enemyClass,
                    Tier = (int)e.tier,
                    MinWaveNumber = e.minWaveNumber,
                    MaxPerWave = e.maxPerWave,
                    StabilityDamage = e.stabilityDamage,
                    DetectionRange = e.detectionRange,
                    AwakeningReward = e.awakeningReward,
                    EngageMovement = (int)e.engageMovement,
                    TargetFactions = (int)e.targetFactions,
                    WaypointPathIndex = e.waypointPathIndex,
                };
            }
            return outp;
        }
    }
}
