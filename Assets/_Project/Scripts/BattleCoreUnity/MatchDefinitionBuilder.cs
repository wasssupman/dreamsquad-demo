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
                                            System.Collections.Generic.IReadOnlyList<StructureEntry> structures = null,
                                            MatchViewAssets viewAssets = null,
                                            MovementTuningConfig movement = null,
                                            StackModifierSO[] stackModifiers = null,
                                            ImbueCapConfig imbueCaps = null,
                                            BoardEffectAuthoring board = default)
        {
            // 모드가 고른 저작이 호출자(드라이버)의 것을 이긴다 — 모드는 「어느 자산을 쓸지」를 고른다.
            deck = ResolveDeck(mode, deck);
            plan = ResolvePlan(mode, plan);
            var enemies = CollectEnemies(deck, plan, bonus);
            var def = Build(defenders, enemies, seed, ToModeDef(mode), in map, tileSize, structures,
                            viewAssets, movement, stackModifiers, imbueCaps, board);

            def.CostRateMultiplier = Mathf.Max(0f, costRateMultiplier);
            def.WaveDeck = ToDeckDef(deck, enemies);
            def.WavePlan = ToPlanDef(plan, enemies);
            def.Bonus = ToBonusDef(bonus, enemies);
            def.Heart = ToHeartConfig(deck);
            def.Gimmicks = ToGimmickDefs(mode);
            def.Roster = RosterOf(defenders);

            // ⚠ 정의표가 다 찬 **뒤에** 굽는다. 먼저 구우면 「덱을 바꿨는데 해시가 그대로」가 된다.
            def.ConfigHash = def.ComputeConfigHash();

            // 모드 × 저작의 유효성을 **판 밖에서 한 번** 묻는다. 안 물으면 「12웨이브를 막으라는데
            // 플랜이 비었다」가 판 중간에야 드러난다(그 판은 영영 안 끝난다). 판은 짓되 문제를
            // 전부 한 번에 loud 하게 적는다 — 저작을 고치는 사람이 한 번에 봐야 한다.
            var problems = new System.Collections.Generic.List<string>();
            if (!ModeValidation.Validate(def, problems))
                foreach (var why in problems)
                    Debug.LogError($"[MatchDefinitionBuilder] 모드 검증 실패('{def.Mode.ModeId}'): {why}", mode);
            return def;
        }

        /// <summary>
        /// 이 판의 덱. **모드가 덱을 골랐으면 그것**, 비었으면 호출자의 덱이다. 툴팁의 「비우면 맵
        /// 풀이 짝지은 덱」 중 맵 풀 짝은 아직 배선 전이라 그 자리를 호출자 덱이 채운다 — 맵 풀
        /// 로테이션(`mapPool`·`fixedMapSeed`)의 귀속은 `docs/spec/battle-core-rebuild/` 가 정한다.
        /// ⚠ 드라이버도 적 목록을 모을 때 **같은 함수**를 지나야 한다(적 인덱스가 갈린다).
        /// </summary>
        public static AttackDeck ResolveDeck(MatchModeData mode, AttackDeck fallback)
            => mode != null && mode.deck != null ? mode.deck : fallback;

        /// <summary>
        /// 이 판의 저작 플랜. 모드가 **저작 플랜 모드**이고 플랜을 골랐으면 그것, 아니면 호출자의 것.
        /// 덱 생성 모드는 모드의 플랜을 읽지 않는다(툴팁 계약).
        /// </summary>
        public static WavePlanAsset ResolvePlan(MatchModeData mode, WavePlanAsset fallback)
            => mode != null && mode.waveSourceKind == WaveSourceKind.AuthoredPlan && mode.plan != null
                ? mode.plan
                : fallback;

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
                                            System.Collections.Generic.IReadOnlyList<StructureEntry> structures = null,
                                            MatchViewAssets viewAssets = null,
                                            MovementTuningConfig movement = null,
                                            StackModifierSO[] stackModifiers = null,
                                            ImbueCapConfig imbueCaps = null,
                                            BoardEffectAuthoring board = default)
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
            // unit 6b — 탄 SO 목록을 **번호를 매긴 그 순회에서** 받는다(길막 역참조가 그 번호를 쓴다).
            // 뷰가 없는 판(테스트·헤드리스)에서도 필요하므로 없으면 로컬 한 벌을 만든다.
            var assets = viewAssets ?? new MatchViewAssets();
            CombatDefinitionBuilder.Fill(def, defenders, enemies, structures, assets);
            // unit 6b — 판 위에 깔리는 것(존 장판 · 길막 · 효과 타일). **해시를 굽기 전**이다.
            BoardEffectDefinitionBuilder.Fill(def, assets, in board);
            // ⚠ **해시를 굽기 전**이어야 한다 — 뒤에 두면 「분산 폭을 바꿨는데 해시가 그대로」가 된다.
            // 저작이 없으면 코어 기본값(= 옛 씬 값)을 그대로 둔다. 0 으로 덮지 않는다 —
            // 그러면 몸 반지름 0(충돌 소멸)과 레인 1(분산 없음)이 조용히 성립한다.
            if (movement != null) def.Movement = ToMovementDef(movement);
            // unit 6a — 스택 저작. **해시를 굽기 전**이어야 한다(뒤에 두면 「임계를 바꿨는데
            // 해시가 그대로」가 된다). 안 넘기면 빈 표이고, 그러면 스택은 폴백 상한 5 로
            // 쌓이기만 하고 **임계가 하나도 안 터진다** — 그 상태를 조용히 두지 않으려고
            // `ToStackRuleDefs` 가 잘못된 저작을 loud 하게 거절한다.
            def.StackRules = ToStackRuleDefs(stackModifiers);
            // unit 6a2 — 탄 부여 상한. 같은 이유로 **해시를 굽기 전**이다. 안 넘기면 빈 표이고,
            // 그러면 부여가 **하나도 안 걸린다**(관문이 상한 없는 키를 거절한다) — 「저작이
            // 없다」가 「상한이 없다」로 읽히지 않게 하는 것이 그 거절의 뜻이다.
            def.ImbueCaps = ToImbueCapDefs(imbueCaps);
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
                var tile = ToCoreTile(map.tiles[i]);
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
        ///
        /// ⚠ **정의표에 칸이 늘면 이 함수도 같이 는다.** unit 4 가 배치 저작 일곱 칸(코스트 ·
        /// 연사 게이트 · 사망/퇴근 대기 · 판 위 상한 · 배치 모션 · 각성 보상)을 `UnitDef` 에
        /// 더했는데 이 변환은 unit 1 의 모양 그대로였다 — 그래서 **판 전체가 공짜였고**(트레이
        /// 코스트 칩 0), 상한은 1 로 접히고, 배치 모션 0 이라 배치 페이즈 자체가 없었다.
        /// 「기본값이 그럴듯해서 안 보이는 미싱」이라 콘솔 에러도 테스트 빨강도 안 났다.
        ///
        /// 실어 보내는 것은 **날 저작값**이고 「0 이면 무슨 뜻인가」의 해석은 코어의
        /// `Effective*` 가 한다 — 두 곳에서 접으면 언젠가 두 답이 갈린다.
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
                // ⚠ **`targetAllies` 가 저작 마스크를 이긴다** — 힐러는 어떤 마스크가 저작돼 있어도
                // 아군(방어유닛) 단독이다(옛 `DefenderTargetDefaults.Resolve`). 라이브 힐러가
                // `targetFactions: 98`(적 전부)을 들고 있어 raw 로 실으면 **적을 회복시킨다**
                // (2026-09-24 드리프트 감사 H4). 거점까지 넓히지 않는다 — 마음이 회복을 받는다.
                TargetFactions = d.targetAllies
                    ? (int)Wassup.Battle.Units.Faction.DefenderUnit
                    : (int)d.targetFactions,
                MoveSpeed = d.moveSpeed,

                // ── unit 4 배치 저작 ──────────────────────────────────────────
                Cost = d.cost,
                PlacementCooldown = d.placementCooldown,
                DeathCooldown = d.deathCooldown,
                RetireCooldownRatio = d.retireCooldownRatio,
                MaxOnBoard = d.maxOnBoard,
                // 저작 초가 아니라 **모션에서 파생된** 값이고 그 파생은 소유자(SO)가 한다.
                DeployMotionSeconds = d.DeployMotionSeconds,
                AwakeningReward = d.awakeningReward,
            };
        }

        // ── unit 4: 모드·덱·플랜·기믹 → plain ────────────────────────────────

        public static MovementTuningDef ToMovementDef(MovementTuningConfig c) => new MovementTuningDef
        {
            AgentRadiusTiles = c.AgentRadiusTiles,
            SpawnSubLaneCount = c.SpawnSubLaneCount,
            SpawnSpreadFraction = c.SpawnSpreadFraction,
            SpawnSpreadTopScale = c.SpawnSpreadTopScale,
        };

        public static ModeDef ToModeDef(MatchModeData m)
        {
            if (m == null) return ModeDef.Default();
            // 배치 자원 저작이 없으면 **값을 지어내지 않고** 크게 알린다. 옛 배치 창 폴백은 30초
            // (`PlacementPhaseView`)였는데 SO 폴백은 0초라 조용히 뒤집혀 있었다 — 숫자를 하나 더
            // 두는 대신 「저작이 없다」를 오류로 만든다(코스트 시작·상한·재생 폴백도 같은 SO 몫이다).
            if (m.costConfig == null)
                Debug.LogError($"[MatchDefinitionBuilder] 모드 '{m.modeId}' 에 costConfig(배치 자원 저작)가 없다 — "
                               + "배치 창·코스트가 코드 폴백으로 돈다. 저작을 연결한다.", m);
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

        /// <summary>
        /// 저작 칸 종류 → 코어 칸 종류. **이름으로 옮긴다**(`CombatDefinitionBuilder` 의 매핑들과
        /// 같은 이유 — 두 어휘는 다른 어셈블리라 한쪽이 값을 끼우면 컴파일러가 못 잡는다).
        /// 모르는 값은 **장식(못 걷는 칸)**으로 접고 loud 하다 — 통로로 접으면 벽이 길이 된다.
        /// </summary>
        public static MapTile ToCoreTile(MapTileType authored)
        {
            switch (authored)
            {
                case MapTileType.Walk: return MapTile.Walk;
                case MapTileType.Place: return MapTile.Place;
                case MapTileType.Env: return MapTile.Env;
                case MapTileType.Deco: return MapTile.Deco;
                default:
                    Debug.LogError($"[MatchDefinitionBuilder] 모르는 칸 종류({authored}) — 장식으로 접는다.");
                    return MapTile.Deco;
            }
        }

        /// <summary>저작 교전 이동 → 코어 어휘. **이름으로 옮긴다.** 모르는 값은 멈춤(Halt)으로 접고 loud 하다.</summary>
        public static Wassup.BattleCore.EngageMovement ToCoreEngage(Wassup.Data.EngageMovement authored)
        {
            switch (authored)
            {
                case Wassup.Data.EngageMovement.Halt: return Wassup.BattleCore.EngageMovement.Halt;
                case Wassup.Data.EngageMovement.Advance: return Wassup.BattleCore.EngageMovement.Advance;
                case Wassup.Data.EngageMovement.Pulse: return Wassup.BattleCore.EngageMovement.Pulse;
                default:
                    Debug.LogError($"[MatchDefinitionBuilder] 모르는 교전 이동({authored}) — 멈춤으로 접는다.");
                    return Wassup.BattleCore.EngageMovement.Halt;
            }
        }

        /// <summary>
        /// 저작 임계 모드 → 코어 어휘. **이름으로 옮긴다**(`CombatDefinitionBuilder` 의 매핑 넷과 같은 이유).
        /// </summary>
        public static StackThresholdMode ToCoreThresholdMode(ThresholdMode authored)
        {
            switch (authored)
            {
                case ThresholdMode.Edge: return StackThresholdMode.Edge;
                case ThresholdMode.Consume: return StackThresholdMode.Consume;
                default:
                    Debug.LogError($"[MatchDefinitionBuilder] 모르는 임계 모드({authored}) — Edge 로 접는다.");
                    return StackThresholdMode.Edge;
            }
        }

        /// <summary>저작 파생 효과 → 코어 어휘. **이름으로 옮긴다.**</summary>
        public static StackDerivedKind ToCoreDerivedKind(DerivedEffectKind authored)
        {
            switch (authored)
            {
                case DerivedEffectKind.ApplyDot: return StackDerivedKind.ApplyDot;
                case DerivedEffectKind.ApplyStun: return StackDerivedKind.ApplyStun;
                case DerivedEffectKind.ApplyStat: return StackDerivedKind.ApplyStat;
                default:
                    Debug.LogError($"[MatchDefinitionBuilder] 모르는 파생 효과({authored}) — 지속 피해로 접는다.");
                    return StackDerivedKind.ApplyDot;
            }
        }

        /// <summary>
        /// 스택 저작 SO → 정의표. **자산당 한 줄**이다(F31) — 옛 전투는 `StackKind` 당 전역
        /// 한 벌이라 드래곤과 킨들러가 불 스택 규칙을 물리적으로 공유했고, 한쪽을 올리면
        /// 다른 쪽이 같이 올라갔다. 줄이 갈리면 그 결합이 사라진다.
        ///
        /// ⚠ **임계 배열의 비내림차순은 여기서 fail-closed 로 검증한다**(F13). 옛 전투는
        /// 「저작자 책임」으로 두고 검증하지 않았고, 어긋난 저작은 **조용히** 임계 일부를
        /// 건너뛴다. 같은 임계를 여러 줄 쓰는 것은 정상이다(라이브 피로도가 5·5·5 로 세
        /// 스탯을 한꺼번에 건다) — 그래서 「엄격 증가」가 아니라 「비내림차순」이다.
        /// </summary>
        public static StackRuleDef[] ToStackRuleDefs(StackModifierSO[] src)
        {
            if (src == null || src.Length == 0) return System.Array.Empty<StackRuleDef>();

            var list = new System.Collections.Generic.List<StackRuleDef>(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                var so = src[i];
                if (so == null) continue;

                var authored = so.thresholds;
                int n = authored != null ? authored.Length : 0;
                var rows = n > 0 ? new StackThresholdDef[n] : System.Array.Empty<StackThresholdDef>();
                for (int t = 0; t < n; t++)
                {
                    var a = authored[t];
                    rows[t] = new StackThresholdDef
                    {
                        AtStack = a.atStack,
                        // ⚠ 번호 캐스트 금지 — 이름으로 옮긴다. 앞에 값이 끼면 `Consume` 이
                        // `Edge` 로 읽혀 소비형 임계가 스택을 안 깎고 **무한 발화**한다.
                        Mode = ToCoreThresholdMode(a.mode),
                        Derived = ToCoreDerivedKind(a.derivedKind),
                        Magnitude = a.magnitude,
                        Duration = a.duration,
                        Stat = (int)CombatDefinitionBuilder.ToCoreStat(a.stat),
                        Op = (int)CombatDefinitionBuilder.ToCoreOp(a.op),
                        TickInterval = a.tickInterval,
                    };
                }

                if (!Wassup.BattleCore.Effects.StackRules.IsAscending(rows))
                {
                    Debug.LogError(
                        $"[MatchDefinitionBuilder] 스택 저작 '{so.name}' 의 임계가 비내림차순이 아니다 — "
                        + "이 줄을 버린다(어긋난 저작은 임계 일부를 조용히 건너뛴다).", so);
                    continue;
                }

                list.Add(new StackRuleDef
                {
                    Id = so.name,
                    // ⚠ 번호 캐스트 금지 — 산출물 스택 종류와 **같은 매핑**을 쓴다.
                    Kind = (int)CombatDefinitionBuilder.ToCoreStackKind(so.kind),
                    MaxStack = so.maxStack,
                    PerAppDuration = so.perAppDuration,
                    Thresholds = rows,
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// 부여 상한 SO → 정의표. **키 하나에 한 줄**이다.
        ///
        /// ⚠ 거절 둘 다 loud 하다(제약 6): ⑴ 상한이 **0 이하**인 줄 — 「값을 안 적었다」가
        /// 「상한이 없다」로 읽히면 근거 없는 무한 부여가 조용히 성립한다. ⑵ **같은 키가 두 줄** —
        /// 먼저 찾은 줄이 이기므로 저작자가 고친 줄이 안 먹는 침묵이 난다.
        /// </summary>
        public static ImbueCapDef[] ToImbueCapDefs(ImbueCapConfig src)
        {
            var rows = src != null ? src.Rows : System.Array.Empty<ImbueCapConfig.Row>();
            if (rows.Length == 0) return System.Array.Empty<ImbueCapDef>();

            var list = new System.Collections.Generic.List<ImbueCapDef>(rows.Length);
            var seen = new System.Collections.Generic.List<Wassup.BattleCore.Effects.ImbueKey>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                var key = rows[i].Key();
                if (key.IsNone)
                {
                    Debug.LogError($"[MatchDefinitionBuilder] 부여 상한 저작 '{src.name}' 의 {i}번 줄이 "
                                   + "종류를 안 골랐다 — 이 줄을 버린다.", src);
                    continue;
                }
                if (rows[i].cap <= 0f)
                {
                    Debug.LogError($"[MatchDefinitionBuilder] 부여 상한 저작 '{src.name}' 의 {i}번 줄에 "
                                   + "상한 값이 없다 — 「상한 없음」이 아니라 오류다(이 줄을 버린다).", src);
                    continue;
                }
                if (seen.Contains(key))
                {
                    Debug.LogError($"[MatchDefinitionBuilder] 부여 상한 저작 '{src.name}' 의 {i}번 줄이 "
                                   + "앞줄과 같은 키다 — 뒤 줄은 영영 안 읽힌다(이 줄을 버린다).", src);
                    continue;
                }
                seen.Add(key);
                list.Add(new ImbueCapDef
                {
                    Kind = (int)key.Kind,
                    Target = key.Target,
                    Op = key.Op,
                    Cap = rows[i].cap,
                });
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
                    EngageMovement = (int)ToCoreEngage(e.engageMovement),
                    TargetFactions = (int)e.targetFactions,
                    WaypointPathIndex = e.waypointPathIndex,
                };
            }
            return outp;
        }
    }
}
