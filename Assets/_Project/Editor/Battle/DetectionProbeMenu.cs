using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;

namespace Wassup.EditorTools.Battle
{
    // enemy-detection-range unit 0 — **감지 반경을 넣기 전에 재는 계측기.**
    //
    // 왜 이게 먼저인가: `docs/spec/enemy-hunter-targeting/` 이 거의 같은 기능이었고
    // 2026-07-11 에 전량 폐기됐다. 사유는 「단일 레인 맵에선 그냥 마칭해도 방어유닛 옆을
    // 지나가 어차피 교전하므로 추격이 얻는 게 0」. 그 결론은 **맵 기하가 정한 것**이고,
    // 멀티골·분리복도 맵이 생긴 지금 다시 참인지는 아무도 모른다. 그래서 구현 전에 잰다.
    //
    // 이 파일은 sim 을 한 줄도 바꾸지 않는다 — 고정 스텝 하네스를 돌리며 매 틱 월드를
    // **읽기만** 한다(`SimHarnessRunner.Capture` 와 같은 근거로 Editor 전용 · 제약 1 예외).
    //
    // ⚠ 배치를 하네스의 `ApplyScheduledInput` 에 맡기지 않는다. 그쪽은 «골에 가장 가까운
    // 가능 칸» 을 고르는데(교전 보장이 목적), 그러면 방어유닛이 전부 골 앞에 뭉쳐
    // 「적이 레인을 끝까지 걷는 동안 근처에 아무도 없다」가 되어 **재려는 값 자체가 편향된다.**
    // 여기서는 골까지 남은 거리(흐름장 dist)를 기준으로 경로를 따라 퍼뜨린다.
    public static class DetectionProbeMenu
    {
        // 자동 생성물. 손으로 고치지 말 것 — unit 6 이 감지를 켠 뒤 같은 계측기를 다시 돌려
        // `0_measurement.md` 의 기준선 표와 대조한다.
        private const string ReportPath = "docs/spec/enemy-detection-range/measurement-report.md";

        private const int MatchTicks = 10800;      // 3분 판 전체(웨이브 회전·보스까지 본다)
        private const int SampleEvery = 3;         // 표본 주기. 3틱 = 0.05초 = 최대 0.1칸 이동이라 간격 해상도에 무해
        private const int PathSampleEvery = 30;    // 도달성 BFS 표본 주기(0.5초)
        private const float MaxProbeRadius = 6f;   // 이 밖은 감지 후보로 안 본다(반경 후보 1~4 를 덮는다)
        private const float DetectionRadius = 3f;  // 사용자 확정 반경 — C1 불일치 표본을 이 안에서만 잰다
        private const float BehindRadius = 1.5f;   // 비대칭 반경 후보 — 뒤쪽만 좁힐 때의 값
        private const int MatchSeed = 20260822;    // 아래 주석대로 이 값은 결과에 영향이 없다
        // 리뷰 M4(단일 realization) 에 대한 답 — ⚠ **시드 축은 여기서 무효다.**
        // 실측(2026-09-05): seed 20260822 / 777 / 31337 이 맵별로 **완전히 같은 판**을 냈다
        // (적 67/72/29/27 · 킬 50/51/19/18 전부 동일, configHash 만 다름). 이유는 계측기가
        // `DevMapOverride` 로 맵을 고정하기 때문이다 — 그러면 웨이브 시드가 매치 시드가 아니라
        // **인카운터 덱**에서 온다(`Battle started with generated deck … source=deck-fixed`).
        // 골든의 `seed_b`/`seed_c` 가 갈리는 것은 시드가 **맵+덱 페어를 바꾸기** 때문이고,
        // 맵을 못박은 이 계측기에는 그 경로가 없다.
        //
        // 그래서 두 번째 realization 을 **플레이어 쪽 축**에서 만든다 — 배치 밀도.
        // 감지 기회는 방어유닛 밀도에 정비례하므로 payload 가 밀도에 얼마나 기대는지가
        // 「이 수치를 얼마나 믿을 수 있나」의 답이다(리뷰 M5: 하네스 배치가 코스트를 몰라
        // 라이브보다 촘촘하다 → payload 상향 편향).
        private static readonly int[] Densities = { 12, 6 };

        // ⚠ **덱을 명시로 고정한다.** 시나리오가 덱을 안 지정하면 `SimHarnessRunner` 는 «씬 기본
        // 풀»(`BattleBridge` 의 serialized 필드)을 쓰는데, 그건 **공유 가변 상태**다 — 다른 세션의
        // 씬 편집이나 골든 시나리오의 덱 교체가 남긴 정적 메모 하나로 값이 갈린다.
        // 실측(2026-09-05): 같은 seed·같은 맵인데 두 시점의 결과가 달랐고(적 67 vs 57, 킬 51 vs 42)
        // 원인이 씬 풀 7종 → 8종 변화였다. 「조건이 바뀐 것을 코드 회귀로 읽는」 부류이며,
        // `DevMapOverride` 를 못박은 것과 같은 이유로 이쪽도 못박는다.
        //
        // 값은 **카탈로그의 기본 편성**(`DefenderCatalog.defaultSquadUnits`)이다 — 플레이어가 실제로
        // 시작하는 7종이라 대표성이 있고, 씬이 아니라 데이터가 소유해 git 에 추적된다.
        private static readonly string[] Deck =
            { "malphite", "shotgunner", "shield_shuttle", "busters", "cannon", "bastion", "anti_air" };
        private static readonly float[] Radii = { 1f, 2f, 3f, 4f };

        // 맵 하나가 10800틱이라 한 번에 다 돌리면 호출이 길다. 맵 단위로 나눠 돌리고
        // 여기 누적한 뒤 마지막에 보고서를 쓴다.
        private static readonly List<RunStats> _accum = new List<RunStats>();

        [MenuItem("Wassup/Battle/Sim Harness/Detection Probe (measure)")]
        public static void Run()
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge)) return;
            int mapCount = Mathf.Max(1, ResolveMapPoolCount(bridge));
            ResetAccum();
            for (int di = 0; di < Densities.Length; di++)
                for (int m = 0; m < mapCount; m++) RunMap(m, Densities[di]);
            Debug.Log(WriteReport());
        }

        public static void ResetAccum() => _accum.Clear();

        public static int MapCount()
            => SimHarnessGuards.TryGetBridge(out var bridge) ? Mathf.Max(1, ResolveMapPoolCount(bridge)) : 0;

        // 맵 하나를 재고 누적한다. 반환 = 한 줄 요약(호출부 로그용).
        public static string RunMap(int mapIndex, int maxPlacements = 12)
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge)) return "no bridge";
            int savedOverride = Wassup.Core.DevMapOverride.Index;
            try
            {
                // 맵을 **의도적으로** 고정한다. 골든 베이크가 이 값을 끄는 것과 반대 방향이지만
                // 목적이 다르다 — 골든은 조건을 하나로 못박고, 계측은 맵별 편차가 질문이다.
                Wassup.Core.DevMapOverride.Index = mapIndex;
                var sc = new SimHarnessRunner.Scenario
                {
                    name = $"map{mapIndex}d{maxPlacements}",
                    seed = MatchSeed,
                    ticks = MatchTicks,
                    placementTicks = new int[0],   // 배치는 계측기 훅이 직접 한다
                    defenderIds = Deck,            // 씬 풀에 기대지 않는다(위 주석)
                };
                var st = RunOne(bridge, sc, mapIndex, maxPlacements);
                st.maxPlacements = maxPlacements;
                _accum.Add(st);
                return $"map{mapIndex} d{maxPlacements} {st.gridSize.x}x{st.gridSize.y} enemies={st.enemies.Count} "
                     + $"engaged={Count(st.enemies, e => e.everEngaged)} leaked={Count(st.enemies, e => e.leaked)} "
                     + $"placed={st.defendersPlaced} kills={st.kills}/{st.leaks} hash={st.configHash}"
                     + (string.IsNullOrEmpty(st.setupError) ? "" : $" SETUP-ERR: {st.setupError}");
            }
            finally
            {
                Wassup.Core.DevMapOverride.Index = savedOverride;
                SimHarnessRunner.RestoreDefaultPool(bridge);
            }
        }

        public static string WriteReport()
        {
            System.IO.File.WriteAllText(ReportPath, BuildReport(_accum));
            return $"[DetectionProbe] {_accum.Count}개 맵 완료. 보고서: {ReportPath}";
        }

        // ── 한 판 ────────────────────────────────────────────────────────────────

        private static RunStats RunOne(BattleBridge bridge, in SimHarnessRunner.Scenario sc,
                                       int mapIndex, int maxPlacements)
        {
            var probe = new Probe(bridge, maxPlacements);
            var result = SimHarnessRunner.Run(bridge, sc, record: false, onTick: probe.OnTick);
            var stats = probe.Finish();
            stats.mapIndex = mapIndex;
            stats.configHash = result.configHash;
            stats.setupError = result.setupError;
            bridge.ReadFinalTally(out int kills, out int score, out int leaks);
            stats.kills = kills;
            stats.leaks = leaks;
            stats.wavesReached = bridge.NextWaveNumber;
            probe.Dispose();
            return stats;
        }

        // 적 1기의 생애 기록. 「감지가 있었다면 무엇이 달라졌나」를 사후에 계산할 수 있는
        // 최소 집합만 담는다(감지 로직 자체는 여기 없다 — 아직 존재하지 않으니까).
        private sealed class EnemyRec
        {
            public int simId;
            public int ticks;
            public bool hasWeapon;
            public float attackRangeTiles;
            public bool isHunter;
            // 적 엔티티엔 타입 id 가 없다(SO 참조를 안 굽는다). `(사거리, 최대체력)` 서명이
            // 오늘 24종을 사실상 유일하게 가른다 — unit 6 의 저작 선정을 근거 위에 올리기 위한
            // 인구조사용이며, 이 값으로 규칙을 만들지 않는다(서명이 겹치면 조용히 틀린다).
            public float maxHealth;

            public float minGapAny = float.MaxValue;     // 최근접 방어유닛까지 몸-가장자리 간격(칸)
            public float minGapLegal = float.MaxValue;   // 그중 **내가 때릴 수 있는** 놈까지

            public bool everEngaged;      // 한 번이라도 사격 대상을 잡았다(AiState.Engaging)
            public bool everAggroed;
            // `PastGoalTag` = 골 셀에 닿아 **공성으로 전환**했다. 게임의 tally `leaks` 와는
            // 다른 것을 센다(실측 51 vs 7) — 그쪽은 판정용 카운터고 이쪽은 「골에 닿았나」다.
            // 두 수를 같은 말로 부르지 말 것.
            public bool leaked;
            public int huntTicks;         // 사냥 필드가 실제로 도달 가능했던 틱(보스 레인 실측)

            public int engageEpisodes;    // 비교전 → 교전 전이 횟수 = 사용자가 말한 «사이클»
            public int aggroEpisodes;
            public int marchTicks, engageTicks, chaseTicks, standoffTicks;
            public bool prevEngaged, prevAggroed;

            // 도달성 표본 — 「감지했는데 갈 수 있나 / 몇 칸을 돌아야 하나」
            public int pathProbes, pathReachable;
            public float bestDetour = float.MaxValue;   // BFS 걸음수 / 직선 간격
            public float bestDetourGap;

            // ★ 이 기능의 실제 payload — **「감지 반경 안인데 그냥 걸어가는」 시간.**
            //   마칭 = `hasFireTarget == false` 이므로 마칭 중 gap <= R 은 정의상 「사거리 밖
            //   이지만 감지 반경 안」이다. 즉 감지를 켜면 이 틱들이 통째로 추격/교전으로 바뀐다.
            public readonly int[] marchWithinR = new int[4];
            public readonly int[] detectEpisodes = new int[4];   // 그 구간에 새로 들어간 횟수 = 사용자가 말한 «사이클»
            public readonly bool[] prevWithinR = new bool[4];
            // 전방향 원의 대가 — 감지된 그 방어유닛이 **이미 지나친 뒤**(골까지 남은 거리가
            // 나보다 멀다)면 적은 되돌아간다. 종심 방어가 무의미해지는지가 여기서 나온다.
            public readonly int[] marchWithinRBehind = new int[4];
            // 뒤쪽이면서 **가까운**(≤ BehindRadius) 것. 「앞 R / 뒤 1.5」 비대칭 반경의 payload 를
            // 사후 계산하기 위한 값 — 전방향과 앞쪽전용의 사이를 한 번의 실행으로 잰다.
            public readonly int[] marchWithinRBehindNear = new int[4];

            // C3(리뷰) — **군집 정체.** 「행진 중인데 자기주도 변위가 0」인 틱.
            // `continuous-agent-movement` unit 12 가 «여유 < 밀어냄 폭 + 앞에 마개» 로 6맵 중
            // 4맵 100초 교착을 실측했고, 감지는 그 조건(레인 옆 정지 유닛 + 뒤에서 밀려오는 무리)을
            // 대량 생산한다. 감지 전 기준선을 잡아 두지 않으면 켠 뒤에 비교할 대상이 없다.
            public int stallTicks;
            public int maxStallRun;
            public int curStallRun;
        }

        private sealed class RunStats
        {
            public int mapIndex;
            public int maxPlacements;
            public string configHash;
            public string setupError;
            public int kills, leaks;
            public int defendersPlaced;
            // 리뷰(designer C2 · player M1/M2) — 감지가 **점수를 깎을 수 있는 경로**를 보는 값들.
            // 웨이브 회전은 「필드에 적 0기」로 돌고(`BattleBridge:2146`), 당김 예산도 「비웠다」
            // 하나로만 회복된다(`:2180`). 감지가 만드는 낙오병은 그 둘을 동시에 늦춘다 →
            // 총 스폰이 줄고 → 1킬 1점 게임에서 **점수가 내려간다.** 안 재면 못 본다.
            public int wavesReached;
            public int pullsTried, pullsOk;
            public int defendersSeen, defenderDeaths;
            public int2 gridSize;
            public List<EnemyRec> enemies = new List<EnemyRec>();
            // (BFS 걸음수, 그때의 간격) 표본. 비율만 보면 간격이 작을 때 분모가 터져 부풀므로
            // **절대 걸음수를 같이** 남긴다 — 우회 예산은 걸음수로 정할 값이다.
            public List<(int steps, float gap)> pathSamples = new List<(int steps, float gap)>();
            // C1(리뷰) — **감지 대상 ≠ 이동 도착지.** 감지는 직선 최근접 legal 을 고르는데,
            // 배송될 이동(A안)은 **공용 다중소스 필드**라 «경로가 가장 가까운 아무 방어유닛» 쪽으로
            // 흐른다. 둘이 갈리면 unit 5 의 표식이 A 를 가리키고 몸은 B 로 가는 거짓말이 된다.
            // 기준선 측정은 이 값을 **한 번도 재지 않았다**(도달성 표본은 대상 전용 필드 = B안이다).
            public int targetSamples, targetMismatch, targetMismatchIllegal;
        }

        // ── 계측 본체 ────────────────────────────────────────────────────────────

        private sealed class Probe
        {
            private readonly BattleBridge _bridge;
            private readonly RunStats _stats = new RunStats();
            private readonly Dictionary<int, EnemyRec> _byId = new Dictionary<int, EnemyRec>();

            // BFS 스크래치 — 판마다 그리드가 다르므로 크기가 바뀌면 다시 잡는다.
            private NativeArray<byte> _walkMask;
            private NativeArray<float2> _tmpFlow;
            private NativeArray<int> _tmpDist;
            private int _scratchCells;
            private byte _walkMaskLayers;
            private readonly List<bool> _legal = new List<bool>(16);   // 적별 방어유닛 legality (틱마다 재사용)

            // 배치 스케줄 — 첫 배치는 판이 서고 난 뒤(150틱), 이후 5초 간격.
            private int _placed;
            private const int FirstPlaceTick = 150;
            private const int PlaceEvery = 300;


            // 당김(플레이어 동사)을 실제로 돌린다. 안 돌리면 감지↔당김예산 루프가 측정에서
            // 구조적으로 안 보인다(player M1). 규칙 층(상한·쿨다운)은 그대로 통과시키고
            // 성공/실패를 센다 — 거절당한 시도도 정보다.
            private const int FirstPullTick = 600;
            private const int PullEvery = 600;

            // 방어유닛 사망 추적 — 사라진 id 를 센다. 하네스는 재배치를 안 하므로
            // 「킬 하락이 감지 탓인가 하네스 탓인가」를 이 값 없이는 못 가른다(player M2).
            private readonly HashSet<int> _liveDefenders = new HashSet<int>();
            private readonly HashSet<int> _tickDefenders = new HashSet<int>();

            // 쿼리는 **한 번만** 만든다. 틱마다 `CreateEntityQuery` 하면 43,200틱에서 그 비용이
            // 측정 자체보다 커진다(그리고 쿼리 캐시를 계속 흔든다).
            private EntityQuery _defQ, _enQ, _fieldQ, _huntQ, _obsQ;
            private bool _queriesBuilt;

            private readonly int _maxPlacements;
            public Probe(BattleBridge bridge, int maxPlacements)
            { _bridge = bridge; _maxPlacements = maxPlacements; }

            private void BuildQueries(EntityManager em)
            {
                _defQ = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<FactionTag>(), ComponentType.ReadOnly<LocalTransform>(),
                                  ComponentType.ReadOnly<Health>() },
                    None = new[] { ComponentType.ReadOnly<PendingDeployment>(), ComponentType.ReadOnly<DeadTag>() },
                });
                _enQ = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<AttackUnitTag>(), ComponentType.ReadOnly<LocalTransform>(),
                                  ComponentType.ReadOnly<SimEntityId>(), ComponentType.ReadOnly<FactionTag>() },
                    None = new[] { ComponentType.ReadOnly<DeadTag>() },
                });
                _fieldQ = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
                _huntQ = em.CreateEntityQuery(ComponentType.ReadOnly<DefenderFieldSingleton>());
                _obsQ = em.CreateEntityQuery(ComponentType.ReadOnly<ObstacleSingleton>());
                _queriesBuilt = true;
            }

            public void OnTick(int tick)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) return;
                var em = world.EntityManager;
                if (!_queriesBuilt) BuildQueries(em);

                if (tick >= FirstPlaceTick && (tick - FirstPlaceTick) % PlaceEvery == 0 && _placed < _maxPlacements)
                    PlaceSpread(em);

                if (tick >= FirstPullTick && (tick - FirstPullTick) % PullEvery == 0)
                {
                    _stats.pullsTried++;
                    if (_bridge.TryPullNextWave()) _stats.pullsOk++;
                }

                if (tick % SampleEvery == 0) Sample(em, tick);
            }

            // ── 배치: 골까지 남은 거리로 경로를 따라 퍼뜨린다 ──────────────────
            //
            // 「가능한 칸」만으로는 부족하다는 하네스의 교훈(SimHarnessRunner:281)은 그대로 받되,
            // 해법을 뒤집는다: 하네스는 **골에 가장 가까운** 칸 하나로 수렴시켰고, 그건 교전을
            // 보장하는 대신 방어유닛을 전부 한 곳에 뭉쳐 놓는다. 여기서는 **골까지 남은 거리**
            // 순으로 정렬해 분위수 자리에 놓는다 — 실제 플레이의 «길을 따라 배치» 에 가깝다.
            private void PlaceSpread(EntityManager em)
            {
                var pool = _bridge.DefenderPool;
                if (pool == null || pool.Length == 0) return;
                if (!TryGetField(em, out var field)) return;

                var unit = pool[_placed % pool.Length];
                int goalSlot = field.SlotFor(FlowFieldSingleton.GoalSentinel, TraversalSlots.DefaultMask);
                var goalDist = field.DistSlot(goalSlot);

                // (골까지 남은 거리, y, x) — 결정론. 거리는 «인접한 걸을 수 있는 칸» 에서 읽는다:
                // 배치 칸 자신은 통상 벽이라 dist 가 무한이다.
                var cands = new List<(int progress, int x, int y)>(64);
                for (int y = 0; y < field.gridSize.y; y++)
                for (int x = 0; x < field.gridSize.x; x++)
                {
                    if (!_bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                    int best = int.MaxValue;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= field.gridSize.x || ny >= field.gridSize.y) continue;
                        int d = goalDist[ny * field.gridSize.x + nx];
                        if (d < best) best = d;
                    }
                    if (best == int.MaxValue) continue;   // 레인에 안 붙은 칸 — 놓아도 아무 일이 없다
                    cands.Add((best, x, y));
                }
                if (cands.Count == 0) return;
                cands.Sort((a, b) => a.progress != b.progress ? a.progress.CompareTo(b.progress)
                                   : (a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x)));

                // 분위수 순회 — 골 앞부터 스폰 쪽까지 고르게. 5주기로 돌아 겹치면 다음 칸으로 민다.
                float[] q = { 0f, 0.5f, 0.25f, 0.75f, 1f };
                int idx = Mathf.Clamp(Mathf.RoundToInt(q[_placed % q.Length] * (cands.Count - 1)), 0, cands.Count - 1);
                for (int probe = 0; probe < cands.Count; probe++)
                {
                    var c = cands[(idx + probe) % cands.Count];
                    if (_bridge.PlaceDefenderAs(c.x, c.y, unit)) { _placed++; _stats.defendersPlaced++; return; }
                }
            }

            // ── 표본 ─────────────────────────────────────────────────────────────
            private void Sample(EntityManager em, int tick)
            {
                if (!TryGetField(em, out var field)) return;
                _stats.gridSize = field.gridSize;

                // 방어유닛 스냅샷 — `DefenderFieldSystem`·`EnemyAiStateSystem` 후보 풀과 같은 조건.
                var defEnts = _defQ.ToEntityArray(Allocator.Temp);
                var defTf = _defQ.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                var defFac = _defQ.ToComponentDataArray<FactionTag>(Allocator.Temp);

                // 「골까지 남은 거리」 자 — 앞/뒤 판정에 쓴다. 방어유닛 칸 자체는 통상 벽이라
                // 인접 걸을 수 있는 칸에서 읽는다(배치 분위수 계산과 같은 규약).
                int goalSlot = field.SlotFor(FlowFieldSingleton.GoalSentinel, TraversalSlots.DefaultMask);
                var goalDist = field.DistSlot(goalSlot);

                var defs = new List<(Entity e, float3 pos, int faction, byte layers, int cls, float bodyR, int progress)>(defEnts.Length);
                for (int i = 0; i < defEnts.Length; i++)
                {
                    if (((int)defFac[i].value & (int)Faction.DefenderUnit) == 0) continue;
                    var e = defEnts[i];
                    int2 dc = GridMath.WorldToCell(defTf[i].Position, field.tileSize, field.gridSize, origin: field.origin);
                    int prog = NeighborMinDist(dc, goalDist, field.gridSize);
                    defs.Add((e, defTf[i].Position, (int)defFac[i].value,
                        em.HasComponent<PathFollowState>(e) ? em.GetComponentData<PathFollowState>(e).traversalLayers : (byte)0,
                        em.HasComponent<DefenderClassTag>(e) ? (int)em.GetComponentData<DefenderClassTag>(e).value : -1,
                        em.HasComponent<HitRadius>(e) ? em.GetComponentData<HitRadius>(e).value : 0f,
                        prog));
                }
                // 방어유닛 사망 추적 — 이번 틱에 사라진 id 를 센다(방어유닛은 유출하지 않으므로
                // 소멸 = 사망이다). 하네스가 재배치를 안 하므로 이 값이 있어야 킬 하락의 원인을
                // 「감지」와 「보드가 영구히 얇아짐」으로 가를 수 있다.
                _tickDefenders.Clear();
                for (int i = 0; i < defs.Count; i++)
                    if (em.HasComponent<SimEntityId>(defs[i].e))
                        _tickDefenders.Add(em.GetComponentData<SimEntityId>(defs[i].e).value);
                foreach (var id in _liveDefenders)
                    if (!_tickDefenders.Contains(id)) _stats.defenderDeaths++;
                foreach (var id in _tickDefenders)
                    if (_liveDefenders.Add(id)) _stats.defendersSeen++;
                _liveDefenders.IntersectWith(_tickDefenders);

                defEnts.Dispose(); defTf.Dispose(); defFac.Dispose();

                bool hasHunt = TryGetHuntField(em, out var huntField);

                var enEnts = _enQ.ToEntityArray(Allocator.Temp);
                var enTf = _enQ.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                var enId = _enQ.ToComponentDataArray<SimEntityId>(Allocator.Temp);
                var enFac = _enQ.ToComponentDataArray<FactionTag>(Allocator.Temp);

                float inv = field.tileSize > 1e-6f ? 1f / field.tileSize : 1f;

                for (int i = 0; i < enEnts.Length; i++)
                {
                    if (((int)enFac[i].value & (int)Faction.EnemyUnit) == 0) continue;
                    var e = enEnts[i];
                    int id = enId[i].value;
                    if (!_byId.TryGetValue(id, out var rec))
                    {
                        rec = new EnemyRec { simId = id };
                        _byId[id] = rec;
                        _stats.enemies.Add(rec);
                    }
                    rec.ticks++;

                    float3 pos = enTf[i].Position;
                    float selfR = em.HasComponent<HitRadius>(e) ? em.GetComponentData<HitRadius>(e).value : 0f;

                    bool hasAtk = em.HasComponent<AttackState>(e);
                    var atk = hasAtk ? em.GetComponentData<AttackState>(e) : default;
                    rec.hasWeapon = hasAtk;
                    if (hasAtk) rec.attackRangeTiles = atk.range;
                    if (rec.maxHealth <= 0f && em.HasComponent<Health>(e))
                        rec.maxHealth = em.GetComponentData<Health>(e).max;
                    rec.isHunter |= em.HasComponent<DefenderHunterTag>(e);

                    bool hasFilter = em.HasComponent<EnemyTargetFilter>(e);
                    int filterMask = hasFilter ? em.GetComponentData<EnemyTargetFilter>(e).classMask : -1;

                    // 상태·전이
                    var ai = em.HasComponent<EnemyAiState>(e) ? em.GetComponentData<EnemyAiState>(e).value : AiState.Marching;
                    bool aggroed = em.HasComponent<Aggroed>(e);
                    switch (ai)
                    {
                        case AiState.Marching: rec.marchTicks++; break;
                        case AiState.Engaging: rec.engageTicks++; break;
                        case AiState.Chasing: rec.chaseTicks++; break;
                        case AiState.Standoff: rec.standoffTicks++; break;
                    }
                    bool engagedNow = ai == AiState.Engaging;
                    if (engagedNow && !rec.prevEngaged) rec.engageEpisodes++;
                    if (aggroed && !rec.prevAggroed) rec.aggroEpisodes++;
                    rec.prevEngaged = engagedNow;
                    rec.prevAggroed = aggroed;
                    rec.everEngaged |= engagedNow;
                    rec.everAggroed |= aggroed;
                    rec.leaked |= em.HasComponent<PastGoalTag>(e);

                    int2 cell = GridMath.WorldToCell(pos, field.tileSize, field.gridSize, origin: field.origin);
                    if (hasHunt && huntField.IsCreated
                        && huntField.dist[GridMath.CellIndex(cell, field.gridSize)] != int.MaxValue
                        && em.HasComponent<DefenderHunterTag>(e))
                        rec.huntTicks++;

                    // 최근접 방어유닛(전체 / 때릴 수 있는 놈) — 간격은 **몸 가장자리 기준**이라
                    // `AttackReach` 의 자와 같은 단위다(gap <= range 가 곧 사거리 안).
                    float gapAny = float.MaxValue, gapLegal = float.MaxValue;
                    int legalIdx = -1;
                    _legal.Clear();
                    for (int d = 0; d < defs.Count; d++)
                    {
                        var df = defs[d];
                        float dx = (df.pos.x - pos.x) * inv, dz = (df.pos.z - pos.z) * inv;
                        float gap = math.sqrt(dx * dx + dz * dz) - selfR - df.bodyR;
                        if (gap < gapAny) gapAny = gap;
                        bool ok = hasAtk
                            && (df.faction & atk.targetMask) != 0
                            && Wassup.Data.PlacementLayers.CanTarget(atk.targetTraversalLayers, df.layers)
                            && !(hasFilter && df.cls >= 0 && (filterMask & (1 << df.cls)) == 0);
                        _legal.Add(ok);
                        if (!ok) continue;
                        if (gap < gapLegal) { gapLegal = gap; legalIdx = d; }
                    }

                    // C3(리뷰) — 행진 중인데 자기주도 변위가 0 = 정체. `holdingGround` 는
                    // `MovementSystem` unit 13 이 «실제로 움직였을 때만 0» 으로 쓰는 값이라
                    // 밀어냄·외력에 밀린 프레임을 «이동» 으로 세지 않는다.
                    //
                    // ⚠ **두 가지를 빼야 참말이 된다**(초판이 오염됐다 — 「최장 정체 103초」가
                    // 나왔는데 교착이 아니라 **골에 도달해 공성 중인 적**이었다):
                    //   · `PastGoalTag` — 이 유닛들은 `MovementSystem` 쿼리에서 제외돼
                    //     `holdingGround` 가 마지막 값 1 에 **영원히 고정**된다. 서 있는 게 정상이다.
                    //   · 행동정지 CC(잠·스턴) — 못 움직이는 게 규칙이지 막힌 게 아니다.
                    // 정체 = «가려는데 못 간다» 다.
                    bool stalled = ai == AiState.Marching
                        && !em.HasComponent<PastGoalTag>(e)
                        && em.HasComponent<PathFollowState>(e)
                        && em.GetComponentData<PathFollowState>(e).holdingGround != 0
                        && !(em.HasBuffer<CcEffect>(e) && CcActionLock.IsLocked(em.GetBuffer<CcEffect>(e)));
                    if (stalled)
                    {
                        rec.stallTicks++;
                        rec.curStallRun++;
                        if (rec.curStallRun > rec.maxStallRun) rec.maxStallRun = rec.curStallRun;
                    }
                    else rec.curStallRun = 0;
                    if (gapAny < rec.minGapAny) rec.minGapAny = gapAny;
                    if (gapLegal < rec.minGapLegal) rec.minGapLegal = gapLegal;

                    // 마칭 = 사격 대상 없음(EnemyAiStateSystem 의 hasFireTarget == false).
                    // 그러니 «마칭 중 gap <= R» 은 곧 «감지를 켰다면 추격했을 시간» 이다.
                    // 앞/뒤 — 나보다 골에서 **먼** 방어유닛이면 이미 지나친 놈이다.
                    //
                    // ⚠ **두 progress 를 같은 규약으로 읽는다**(리뷰 지적으로 고쳤다). 초판은
                    // 방어유닛만 3×3 이웃 최소를 쓰고 적은 자기 셀 값을 썼는데, 이웃 최소는
                    // 대각 1칸(비용 14 = 1.4칸)까지 작게 나오므로 판정이 「방어유닛이 나보다 앞」
                    // 쪽으로 기운다 = **뒤쪽 과소 집계**. 그 편향 폭이 R=3(18%)과 R=4(28%)를
                    // 가르는 크기와 같은 자리수라 「전방향 원의 대가」 추정치를 낙관적으로 만든다.
                    // 적도 같은 이웃 최소를 쓴다(방어유닛 칸은 통상 벽이라 이쪽을 자기 셀로
                    // 되돌릴 수는 없다 — 그러면 늘 MaxValue 다).
                    int myProgress = NeighborMinDist(cell, goalDist, field.gridSize);
                    bool behind = legalIdx >= 0 && myProgress != int.MaxValue
                                  && defs[legalIdx].progress != int.MaxValue
                                  && defs[legalIdx].progress > myProgress;
                    for (int r = 0; r < Radii.Length; r++)
                    {
                        bool within = ai == AiState.Marching && gapLegal <= Radii[r];
                        if (within)
                        {
                            rec.marchWithinR[r]++;
                            if (behind)
                            {
                                rec.marchWithinRBehind[r]++;
                                // 비대칭 반경(앞 R / 뒤 1.5칸) 평가용 — 뒤쪽이지만 가까운 것.
                                if (gapLegal <= BehindRadius) rec.marchWithinRBehindNear[r]++;
                            }
                            if (!rec.prevWithinR[r]) rec.detectEpisodes[r]++;
                        }
                        rec.prevWithinR[r] = within;
                    }

                    // 도달성 표본 — 「감지 반경 안인데 아직 못 쏘는」 상황에서만 의미가 있다.
                    if (tick % PathSampleEvery == 0 && legalIdx >= 0 && hasAtk
                        && gapLegal <= MaxProbeRadius && gapLegal > atk.range)
                    {
                        ProbePath(em, in field, e, cell, defs[legalIdx].pos, atk.range, gapLegal, rec);
                        // C1 — 감지 반경(결정값 3칸) 안에서만 묻는다. 그 밖은 감지가 안 켜지므로
                        // 「대상이 갈렸다」를 물을 대상이 아니다.
                        if (gapLegal <= DetectionRadius)
                            ProbeTargetMismatch(em, in field, e, cell, defs, _legal, legalIdx, atk.range);
                    }
                }

                enEnts.Dispose(); enTf.Dispose(); enId.Dispose(); enFac.Dispose();
            }

            // 「그 방어유닛을 쏠 수 있는 칸」까지 몇 걸음인가. 어그로 추격이 쓰는 것과 **같은
            // 필드 빌더**를 쓴다 — 다른 자로 재면 이 측정이 실제 이동과 무관해진다.
            // BFS 스크래치와 이 적의 벽 마스크를 준비한다. 두 표본(도달성 · 대상 불일치)이 공유한다.
            private bool EnsureScratch(EntityManager em, Entity enemy, in FlowFieldSingleton field)
            {
                int cells = field.gridSize.x * field.gridSize.y;
                if (cells <= 0) return false;
                if (_scratchCells != cells)
                {
                    DisposeScratch();
                    _walkMask = new NativeArray<byte>(cells, Allocator.Persistent);
                    _tmpFlow = new NativeArray<float2>(cells, Allocator.Persistent);
                    _tmpDist = new NativeArray<int>(cells, Allocator.Persistent);
                    _scratchCells = cells;
                    _walkMaskLayers = 0;
                }
                byte layers = em.HasComponent<PathFollowState>(enemy)
                    ? em.GetComponentData<PathFollowState>(enemy).traversalLayers : (byte)0;
                if (layers == 0) layers = TraversalSlots.DefaultMask;
                bool hasObs = TryGetObstacles(em, out var obstacles);
                if (layers != _walkMaskLayers)
                {
                    MovementCellTrim.FillWalkMask(in field, layers, hasObs, in obstacles, _walkMask);
                    _walkMaskLayers = layers;
                }
                return true;
            }

            // C1(리뷰) — **감지가 고른 대상**과 **공용 사냥판이 데려갈 곳**이 같은가.
            //
            // 공용 필드는 다중소스 BFS 라 그 값이 곧 「모든 방어유닛의 사격 칸 중 경로가 가장 가까운
            // 것」이다. 그래서 방어유닛별로 필드를 굽고 argmin 을 취하면 그 필드가 수렴하는 대상이
            // 나온다. legality 를 **안 보는 것이 핵심이다** — 소스 수집(`DefenderFieldSystem`)도
            // 안 보기 때문이고, 그게 「달려는 가는데 못 때리는」의 원천이다.
            private void ProbeTargetMismatch(
                EntityManager em, in FlowFieldSingleton field, Entity enemy, int2 enemyCell,
                List<(Entity e, float3 pos, int faction, byte layers, int cls, float bodyR, int progress)> defs,
                List<bool> legal, int detectedIdx, float atkRange)
            {
                if (!EnsureScratch(em, enemy, in field)) return;
                int tileRange = math.max(1, GridMath.RangeToTiles(atkRange));
                int best = -1, bestCost = int.MaxValue;
                for (int d = 0; d < defs.Count; d++)
                {
                    int2 dc = GridMath.WorldToCell(defs[d].pos, field.tileSize, field.gridSize, origin: field.origin);
                    if (AggroChaseMath.BuildChaseField(_walkMask, field.gridSize, dc, tileRange, _tmpFlow, _tmpDist) == 0)
                        continue;
                    int c = _tmpDist[GridMath.CellIndex(enemyCell, field.gridSize)];
                    if (c == int.MaxValue || c >= bestCost) continue;
                    bestCost = c; best = d;
                }
                _stats.targetSamples++;
                if (best < 0 || best == detectedIdx) return;
                _stats.targetMismatch++;
                if (!legal[best]) _stats.targetMismatchIllegal++;
            }

            private void ProbePath(EntityManager em, in FlowFieldSingleton field, Entity enemy,
                                   int2 enemyCell, float3 defPos, float atkRange, float gap, EnemyRec rec)
            {
                if (!EnsureScratch(em, enemy, in field)) return;
                int2 defCell = GridMath.WorldToCell(defPos, field.tileSize, field.gridSize, origin: field.origin);
                int tileRange = math.max(1, GridMath.RangeToTiles(atkRange));
                int src = AggroChaseMath.BuildChaseField(_walkMask, field.gridSize, defCell, tileRange, _tmpFlow, _tmpDist);
                rec.pathProbes++;
                if (src == 0) return;
                int steps = _tmpDist[GridMath.CellIndex(enemyCell, field.gridSize)];
                if (steps == int.MaxValue) return;
                rec.pathReachable++;
                _stats.pathSamples.Add((steps, gap));
                float detour = gap > 0.01f ? steps / gap : steps;
                if (detour < rec.bestDetour) { rec.bestDetour = detour; rec.bestDetourGap = gap; }
            }

            public RunStats Finish() => _stats;

            public void Dispose()
            {
                DisposeScratch();
                if (!_queriesBuilt) return;
                _defQ.Dispose(); _enQ.Dispose(); _fieldQ.Dispose(); _huntQ.Dispose(); _obsQ.Dispose();
                _queriesBuilt = false;
            }

            private void DisposeScratch()
            {
                if (_walkMask.IsCreated) _walkMask.Dispose();
                if (_tmpFlow.IsCreated) _tmpFlow.Dispose();
                if (_tmpDist.IsCreated) _tmpDist.Dispose();
                _scratchCells = 0;
            }

            // 셀의 3×3 이웃 중 최소 goalDist. 방어유닛 칸은 통상 벽이라 자기 셀이 MaxValue 이므로
            // 이 규약이 필요하고, **적도 같은 규약으로 읽어야** 앞/뒤 판정이 한쪽으로 안 기운다.
            private static int NeighborMinDist(int2 c, in NativeArray<int> goalDist, int2 gridSize)
            {
                int best = int.MaxValue;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = c.x + dx, ny = c.y + dy;
                    if (nx < 0 || ny < 0 || nx >= gridSize.x || ny >= gridSize.y) continue;
                    int d = goalDist[ny * gridSize.x + nx];
                    if (d < best) best = d;
                }
                return best;
            }

            private bool TryGetField(EntityManager em, out FlowFieldSingleton field)
            {
                field = default;
                if (!_queriesBuilt || _fieldQ.CalculateEntityCount() != 1) return false;
                field = _fieldQ.GetSingleton<FlowFieldSingleton>();
                return field.IsCreated;
            }

            private bool TryGetHuntField(EntityManager em, out DefenderFieldSingleton f)
            {
                f = default;
                if (!_queriesBuilt || _huntQ.CalculateEntityCount() != 1) return false;
                f = _huntQ.GetSingleton<DefenderFieldSingleton>();
                return f.IsCreated;
            }

            private bool TryGetObstacles(EntityManager em, out ObstacleSingleton o)
            {
                o = default;
                if (!_queriesBuilt || _obsQ.CalculateEntityCount() != 1) return false;
                o = _obsQ.GetSingleton<ObstacleSingleton>();
                return true;
            }
        }

        // ── 보고서 ───────────────────────────────────────────────────────────────

        private static string BuildReport(List<RunStats> runs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# detection-probe — 감지 반경 도입 전 실측");
            sb.AppendLine();
            sb.AppendLine($"- 조건: {runs.Count}판 × {MatchTicks}틱(3분) · 맵 풀 전체 × 배치밀도 {string.Join("/", Densities)}기(경로 분위수) · 당김 구동");
            sb.AppendLine("- 간격(gap) = 몸 가장자리 기준 칸 거리. `gap <= attackRange` 가 곧 사거리 안(AttackReach 와 같은 자).");
            sb.AppendLine("- **legal** = 그 적의 targetMask · 통행층 · classMask 를 전부 통과하는 방어유닛(AttackSystem 후보 루프 미러).");
            sb.AppendLine();

            var all = new List<EnemyRec>();
            foreach (var r in runs) all.AddRange(r.enemies);

            sb.AppendLine("## 1. 감지가 새로 만드는 교전 (핵심)");
            sb.AppendLine();
            sb.AppendLine("`감지됨` = 생애 중 한 번이라도 legal 방어유닛과의 간격이 R 이하였던 적.");
            sb.AppendLine("`신규 교전` = 그중 **사거리 안엔 한 번도 못 들어온** 적 = 감지가 없으면 그냥 지나쳤을 적.");
            sb.AppendLine();
            sb.AppendLine("| R | 감지됨 | 신규 교전 | 신규 교전 비율 | 최근접이 못 때리는 놈 |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            foreach (float R in Radii)
            {
                int seen = 0, fresh = 0, mismatch = 0;
                foreach (var e in all)
                {
                    if (e.minGapLegal <= R) { seen++; if (!e.everEngaged) fresh++; }
                    if (e.minGapAny <= R && e.minGapLegal > R) mismatch++;
                }
                sb.AppendLine($"| {R:F0} | {seen} | **{fresh}** | {Pct(fresh, all.Count)} | {mismatch} |");
            }
            sb.AppendLine();
            sb.AppendLine($"- 관측 적 총 **{all.Count}**기 · 그중 한 번이라도 교전한 적 **{Count(all, e => e.everEngaged)}**기"
                          + $" · 골 도달·공성전환 **{Count(all, e => e.leaked)}**기"
                          + $" · **골에 닿았는데 한 번도 교전 안 한 적 {Count(all, e => e.leaked && !e.everEngaged)}기**");
            sb.AppendLine();

            sb.AppendLine("## 2. 맵별");
            sb.AppendLine();
            sb.AppendLine("| 맵 | 배치 | 격자 | 적 | 교전 | 골도달 | 미교전 골도달 | R=2 감지 | R=2 신규 | R=3 신규 | 킬/유출(tally) | 배치 |");
            sb.AppendLine("|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---:|");
            foreach (var r in runs)
            {
                var e = r.enemies;
                sb.AppendLine($"| {r.mapIndex} | {r.maxPlacements}기 | {r.gridSize.x}×{r.gridSize.y} | {e.Count} | {Count(e, x => x.everEngaged)} "
                    + $"| {Count(e, x => x.leaked)} | {Count(e, x => x.leaked && !x.everEngaged)} "
                    + $"| {Count(e, x => x.minGapLegal <= 2f)} | **{Count(e, x => x.minGapLegal <= 2f && !x.everEngaged)}** "
                    + $"| **{Count(e, x => x.minGapLegal <= 3f && !x.everEngaged)}** "
                    + $"| {r.kills}/{r.leaks} | {r.defendersPlaced} |"
                    + (string.IsNullOrEmpty(r.setupError) ? "" : $"  ⚠ {r.setupError}"));
            }
            sb.AppendLine();

            sb.AppendLine("## 3. 감지가 실제로 지배할 시간 (payload)");
            sb.AppendLine();
            sb.AppendLine("마칭 = 사격 대상 없음. 그러니 **「마칭 중인데 legal 방어유닛과 간격 ≤ R」** 인 틱은");
            sb.AppendLine("정의상 「감지를 켰다면 그쪽으로 향했을 시간」이다. 이게 이 기능의 실제 payload다.");
            sb.AppendLine("(표본 주기 3틱 — 비율은 그대로, 절대 틱은 실제의 1/3)");
            sb.AppendLine();
            long marchAll = 0;
            foreach (var e in all) marchAll += e.marchTicks;
            sb.AppendLine("| R | 감지 지배 틱 | 전체 마칭 대비 | 감지 사이클(진입 횟수) | 사이클을 겪는 적 | 그중 **뒤쪽**(되돌아감) |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|");
            for (int r = 0; r < Radii.Length; r++)
            {
                long t = 0, back = 0; int epiR = 0, who = 0;
                foreach (var e in all)
                {
                    t += e.marchWithinR[r]; back += e.marchWithinRBehind[r]; epiR += e.detectEpisodes[r];
                    if (e.detectEpisodes[r] > 0) who++;
                }
                sb.AppendLine($"| {Radii[r]:F0} | {t} | **{Pct(t, marchAll)}** | {epiR} | {who} | {back} ({Pct(back, t)}) |");
            }
            sb.AppendLine();
            sb.AppendLine("`뒤쪽` = 감지된 그 방어유닛이 **골까지 남은 거리 기준 나보다 뒤**에 있다 = 이미 지나친 놈.");
            sb.AppendLine("전방향 원을 고르면 이 비율만큼 적이 **되돌아간다**. 앞쪽만 감지하면 그만큼이 사라진다.");
            sb.AppendLine("(두 progress 를 **같은 규약**(3×3 이웃 최소)으로 읽는다 — 초판은 적만 자기 셀을 써서");
            sb.AppendLine("뒤쪽이 과소 집계됐다. 리뷰 지적으로 고쳤으므로 초판 수치와 직접 비교하지 말 것.)");
            sb.AppendLine();

            sb.AppendLine("**비대칭 반경** — 「앞 R / 뒤 " + BehindRadius.ToString("F1") + "칸」을 쓰면 payload 가 얼마나 남나.");
            sb.AppendLine("전방향(전부)과 앞쪽전용(뒤쪽 0) 사이의 값이다. 저작 축이 하나 늘지만 이분법보다 튜닝된다.");
            sb.AppendLine();
            sb.AppendLine("| R | 전방향 | 앞쪽만 | **앞 R / 뒤 " + BehindRadius.ToString("F1") + "** | 되돌아감 감소 |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            for (int r = 0; r < Radii.Length; r++)
            {
                long t = 0, back = 0, backNear = 0;
                foreach (var e in all)
                { t += e.marchWithinR[r]; back += e.marchWithinRBehind[r]; backNear += e.marchWithinRBehindNear[r]; }
                long front = t - back, asym = front + backNear;
                sb.AppendLine($"| {Radii[r]:F0} | {t} | {front} | **{asym}** | {Pct(back - backNear, t)} 제거 |");
            }
            sb.AppendLine();

            sb.AppendLine("## 4. 도달성 / 우회 비용");
            sb.AppendLine();
            sb.AppendLine("감지 반경 안이지만 아직 못 쏘는 순간마다, 「그놈을 쏠 수 있는 칸」까지의 경로 길이를 잰다.");
            sb.AppendLine("**비율이 아니라 절대 길이가 결정에 쓰는 값이다** — 간격이 작으면 비율 분모가 터져 부푼다.");
            sb.AppendLine();
            sb.AppendLine("> ⚠ `FlowFieldBuilder` 의 `dist` 는 걸음수가 아니라 **가중 다익스트라 비용(직교 10 · 대각 14)** 이다.");
            sb.AppendLine("> 아래 표는 그 값을 10으로 나눈 **칸** 단위다. 직선 간격과 같은 자로 읽으면 된다.");
            sb.AppendLine();
            int probes = 0, reach = 0;
            var samples = new List<(int steps, float gap)>();
            foreach (var r0 in runs) samples.AddRange(r0.pathSamples);
            foreach (var e in all) { probes += e.pathProbes; reach += e.pathReachable; }
            sb.AppendLine($"- 표본 {probes}건 중 도달 가능 **{reach}**건 ({Pct(reach, probes)})");
            sb.AppendLine();
            sb.AppendLine("| 그때의 간격(칸) | 표본 | 경로 길이 중앙값(칸) | p90 | 최대 | 초과분 중앙값 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|");
            float[] gapBins = { 0f, 1f, 2f, 3f, 4f, MaxProbeRadius };
            for (int b = 1; b < gapBins.Length; b++)
            {
                var steps = new List<float>();
                var excess = new List<float>();
                foreach (var s in samples)
                    if (s.gap > gapBins[b - 1] && s.gap <= gapBins[b])
                    { steps.Add(s.steps / 10f); excess.Add(s.steps / 10f - s.gap); }
                if (steps.Count == 0) { sb.AppendLine($"| {gapBins[b - 1]:F0} ~ {gapBins[b]:F0} | 0 | — | — | — | — |"); continue; }
                steps.Sort(); excess.Sort();
                sb.AppendLine($"| {gapBins[b - 1]:F0} ~ {gapBins[b]:F0} | {steps.Count} | **{steps[steps.Count / 2]:F1}** "
                    + $"| {steps[Mathf.Min(steps.Count - 1, (int)(steps.Count * 0.9f))]:F1} | {steps[steps.Count - 1]:F1} "
                    + $"| {excess[excess.Count / 2]:F1} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 4b. 웨이브 케이던스 · 총 스폰 · 방어유닛 사망 (점수 방향)");
            sb.AppendLine();
            sb.AppendLine("감지는 **점수를 깎을 수도 있다.** 웨이브 회전은 「필드에 적 0기」로 돌고 당김 예산도");
            sb.AppendLine("「비웠다」 하나로만 회복되는데, 감지가 만드는 낙오병이 그 둘을 늦춘다 → 총 스폰↓ →");
            sb.AppendLine("**1킬 1점 게임에서 점수↓**. 방어유닛 사망은 하네스가 재배치를 안 하므로 킬 하락의");
            sb.AppendLine("원인을 「감지」와 「보드가 영구히 얇아짐」으로 가르는 데 쓴다.");
            sb.AppendLine();
            sb.AppendLine("| 맵 | 배치 | 총 스폰 | 도달 웨이브 | 당김 성공/시도 | 방어유닛 배치/사망 | 킬 | tally유출 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            int spawnAll = 0, waveAll = 0, pullOkAll = 0, pullTryAll = 0, dSeenAll = 0, dDeadAll = 0, killAll = 0, leakAll = 0;
            foreach (var r2 in runs)
            {
                sb.AppendLine($"| {r2.mapIndex} | {r2.maxPlacements}기 | {r2.enemies.Count} | {r2.wavesReached} | {r2.pullsOk}/{r2.pullsTried} "
                    + $"| {r2.defendersPlaced}/{r2.defenderDeaths} | {r2.kills} | {r2.leaks} |");
                spawnAll += r2.enemies.Count; waveAll += r2.wavesReached;
                pullOkAll += r2.pullsOk; pullTryAll += r2.pullsTried;
                dSeenAll += r2.defendersPlaced; dDeadAll += r2.defenderDeaths;
                killAll += r2.kills; leakAll += r2.leaks;
            }
            sb.AppendLine($"| **합** | — | **{spawnAll}** | **{waveAll}** | **{pullOkAll}/{pullTryAll}** "
                + $"| **{dSeenAll}/{dDeadAll}** | **{killAll}** | **{leakAll}** |");
            sb.AppendLine();

            sb.AppendLine("## 5. 감지 대상 ≠ 이동 도착지 (A안의 대가)");
            sb.AppendLine();
            sb.AppendLine("감지는 **직선 최근접 legal** 을 고르지만, 배송될 이동(A안)은 **공용 다중소스 필드**라");
            sb.AppendLine("«경로가 가장 가까운 아무 방어유닛» 쪽으로 흐른다. 둘이 갈리면 unit 5 의 표식이");
            sb.AppendLine("A 를 가리키고 몸은 B 로 간다. 반경 3칸 안에서 아직 못 쏘는 순간만 표본으로 잡는다.");
            sb.AppendLine();
            int tSamp = 0, tMis = 0, tMisIll = 0;
            foreach (var r0 in runs) { tSamp += r0.targetSamples; tMis += r0.targetMismatch; tMisIll += r0.targetMismatchIllegal; }
            sb.AppendLine($"- 표본 **{tSamp}**건 · 불일치 **{tMis}**건 (**{Pct(tMis, tSamp)}**)");
            sb.AppendLine($"- 그중 도착지가 **이 적이 못 때리는** 방어유닛: **{tMisIll}**건 ({Pct(tMisIll, tSamp)})");
            sb.AppendLine("  — 이게 「달려는 가는데 피해 0」의 원천이다. 0 이 아니면 표식은 대상을 가리키면 안 된다.");
            sb.AppendLine();

            sb.AppendLine("## 6. 정체 (군집 교착 기준선)");
            sb.AppendLine();
            sb.AppendLine("「행진 중인데 자기주도 변위가 0」인 틱. `continuous-agent-movement` unit 12 가 «여유 <");
            sb.AppendLine("밀어냄 폭 + 앞에 마개» 로 6맵 중 4맵 100초 교착을 실측했고, 감지는 그 조건(레인 옆 정지");
            sb.AppendLine("유닛 + 뒤에서 밀려오는 무리)을 대량 생산한다. **감지를 켠 뒤 이 값과 대조한다.**");
            sb.AppendLine();
            long stall = 0; int worst = 0; int stalledEnemies = 0;
            foreach (var e in all)
            {
                stall += e.stallTicks;
                if (e.stallTicks > 0) stalledEnemies++;
                if (e.maxStallRun > worst) worst = e.maxStallRun;
            }
            sb.AppendLine($"- 정체 틱 **{stall}** / 전체 마칭 {marchAll} (**{Pct(stall, marchAll)}**) · 겪은 적 {stalledEnemies}/{all.Count}");
            sb.AppendLine($"- **최장 연속 정체 {worst} 표본틱** (= 실제 {worst * SampleEvery}프레임 ≈ {(worst * SampleEvery / 60f):F1}초)");
            sb.AppendLine("  — unit 12 의 교착 판정선은 6000프레임(100초)이다. 이 값이 그쪽으로 자라면 교착이다.");
            sb.AppendLine();

            sb.AppendLine("## 7. 사이클 · 상태 분포");
            sb.AppendLine();
            long march = 0, engage = 0, chase = 0, standoff = 0;
            int epi = 0, aggroEpi = 0, hunters = 0, huntTicks = 0;
            foreach (var e in all)
            {
                march += e.marchTicks; engage += e.engageTicks; chase += e.chaseTicks; standoff += e.standoffTicks;
                epi += e.engageEpisodes; aggroEpi += e.aggroEpisodes;
                if (e.isHunter) { hunters++; huntTicks += e.huntTicks; }
            }
            long total = march + engage + chase + standoff;
            sb.AppendLine($"- 상태 점유: 마칭 {Pct(march, total)} · 교전 {Pct(engage, total)} · 추격 {Pct(chase, total)} · 대치 {Pct(standoff, total)}");
            sb.AppendLine($"- 교전 사이클(비교전→교전 전이) 총 **{epi}**회 · 적 1기당 평균 {(all.Count > 0 ? epi / (float)all.Count : 0):F2}회");
            sb.AppendLine($"- 어그로 획득 총 {aggroEpi}회 · 어그로를 겪은 적 {Count(all, e => e.everAggroed)}기");
            sb.AppendLine($"- 사냥꾼(보스·huntsDefenders) **{hunters}**기 · 사냥 필드가 실제로 도달 가능했던 틱 합 {huntTicks}");
            sb.AppendLine();

            sb.AppendLine("## 8. 적 종별 인구조사 — 감지를 어디에 켤 것인가");
            sb.AppendLine();
            sb.AppendLine("적 엔티티엔 타입 id 가 없어 `(사거리, 최대체력)` 서명으로 묶었다. `payload` 는");
            sb.AppendLine("**그 종이 R=3 감지 반경 안에서 그냥 걷는 틱** — 감지를 켰을 때 실제로 달라질 시간이다.");
            sb.AppendLine("이 표가 unit 6 의 저작 선정 근거다. 인원이 많아도 payload 가 0 이면 켜도 아무 일이 없다.");
            sb.AppendLine();
            var census = new Dictionary<(int, int), (int n, long pay, int engaged, int leaked)>();
            foreach (var e in all)
            {
                var key = ((int)Mathf.Round(e.attackRangeTiles), (int)Mathf.Round(e.maxHealth));
                census.TryGetValue(key, out var v);
                census[key] = (v.n + 1, v.pay + e.marchWithinR[2], v.engaged + (e.everEngaged ? 1 : 0),
                               v.leaked + (e.leaked ? 1 : 0));
            }
            var rows = new List<((int r, int hp) key, (int n, long pay, int engaged, int leaked) v)>();
            foreach (var kv in census) rows.Add((kv.Key, kv.Value));
            rows.Sort((a, b) => b.v.pay.CompareTo(a.v.pay));
            sb.AppendLine("| 사거리 | 최대체력 | 마리 | **R=3 payload 틱** | 1기당 | 교전 | 골도달 |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var r1 in rows)
                sb.AppendLine($"| {r1.key.r} | {r1.key.hp} | {r1.v.n} | **{r1.v.pay}** "
                    + $"| {(r1.v.n > 0 ? r1.v.pay / (float)r1.v.n : 0):F0} | {r1.v.engaged} | {r1.v.leaked} |");
            sb.AppendLine();

            sb.AppendLine("## 9. 간격 분포 (legal 최근접, 생애 최소)");
            sb.AppendLine();
            sb.AppendLine("| 생애 최소 간격 | 적 수 | 그중 미교전 |");
            sb.AppendLine("|---|---:|---:|");
            {
                int c0 = 0, ne0 = 0;
                foreach (var e in all)
                    if (e.minGapLegal <= 0f) { c0++; if (!e.everEngaged) ne0++; }
                sb.AppendLine($"| ≤ 0 (몸이 닿음) | {c0} | {ne0} |");
            }
            float[] bins = { 0f, 1f, 2f, 3f, 4f, 6f, 10f, float.MaxValue };
            for (int b = 1; b < bins.Length; b++)
            {
                float lo = bins[b - 1], hi = bins[b];
                int c = 0, ne = 0;
                foreach (var e in all)
                    if (e.minGapLegal > lo && e.minGapLegal <= hi) { c++; if (!e.everEngaged) ne++; }
                string label = hi == float.MaxValue ? $"> {lo:F0} (또는 legal 대상 없음)" : $"{lo:F0} ~ {hi:F0}";
                sb.AppendLine($"| {label} | {c} | {ne} |");
            }
            return sb.ToString();
        }

        private static int Count(List<EnemyRec> src, System.Func<EnemyRec, bool> p)
        {
            int n = 0;
            foreach (var e in src) if (p(e)) n++;
            return n;
        }

        private static string Pct(long n, long d) => d <= 0 ? "—" : $"{(100.0 * n / d):F1}%";

        // mapPool 은 private SerializeField 다. Editor 계측기라 리플렉션이 허용 범위 —
        // 접근자를 새로 뚫으면 그게 곧 런타임 API 가 되고, 이 파일은 곧 사라질 수도 있다.
        private static int ResolveMapPoolCount(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("mapPool",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var pool = f?.GetValue(bridge);
            if (pool == null) return 0;
            var countProp = pool.GetType().GetProperty("Count");
            return countProp != null ? (int)countProp.GetValue(pool) : 0;
        }
    }
}
