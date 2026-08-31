using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core.TimeControl;
using Wassup.Core.Trace;

namespace Wassup.EditorTools.Battle
{
    // battle-sim-extraction M0 unit 4 — 하네스 실행의 공용 몸통.
    //
    // 스텝 자체는 런타임(`BattleBridge.StepOneTick`)이 소유한다. 여기 있는 것은 **시나리오**
    // (seed·길이·입력 스케줄)와 **관측**(틱 다이제스트·trace)뿐이다. 시나리오를 런타임에 두지
    // 않는 이유: 커맨드 어휘의 정본은 M1(세션 파사드)이 정하고, 그 앞에 자리를 잡아 두면 두 번
    // 만들게 된다.
    public static class SimHarnessRunner
    {
        public const float StepDt = 1f / 60f;
        private const int ScanExtent = 32;   // 배치 후보 셀 스캔 범위(고정 = 결정론)

        public struct Scenario
        {
            public string name;
            public int seed;
            public int ticks;
            public int[] placementTicks;
            // 웨이브 당김(플레이어 경로 `TryPullNextWave`) — 웨이브 회전이 시계가 아니라
            // 입력으로 앞당겨지는 축. 없으면 골든이 「가만히 두면 이렇게 된다」만 증언한다.
            public int[] pullWaveTicks;
            // 판 중간 재시작 — 매치 경계 리셋(시계·SimEntityId·코스트)이 두 판을 갈라놓지
            // 않는지 보는 시나리오. 0 = 없음.
            public int restartAtTick;
            // 이 시나리오만 쓰는 덱(카탈로그 id). null = 씬 기본 풀.
            //
            // ⚠ **왜 필요했나**: 씬 기본 풀에 소환사가 없어 **코퍼스에 순찰병이 구조적으로
            // 못 들어왔다.** 순찰병은 `PathFollowState` 를 갖는 **연속 이동 아군**이라,
            // 「적(연속) × 순찰병(연속)」이 `AttackReach` 의 2차 게이트가 실제로 걸리는
            // 거의 유일한 조합이다 — 그 축이 코퍼스에 0 이면 사거리 술어를 바꿔도 골든이
            // 아무 말을 못 한다(distance-based-range unit 1 리뷰 C1).
            public string[] defenderIds;
        }

        // 골든 코퍼스. 스펙이 요구한 시나리오 축을 **지금 결정론적으로 만들 수 있는 형태**로만
        // 담는다 — 못 담은 축과 그 이유는 spec 4 문서에 적었다(추측으로 채우면 골든이 거짓이 된다).
        public static readonly Scenario[] Corpus =
        {
            new Scenario { name = "basic",      seed = 20260822, ticks = 900,
                           placementTicks = new[] { 150, 330, 510, 690 } },
            // 보스는 5웨이브마다 온다 — 60초는 그 회전을 최소 한 번 지난다.
            new Scenario { name = "long_boss",  seed = 20260822, ticks = 3600,
                           placementTicks = new[] { 150, 330, 510, 690, 1200, 1800, 2400 } },
            // seed 를 바꾸면 맵·덱 페어가 바뀐다(멀티골 맵·다른 컨셉이 여기서 들어온다).
            new Scenario { name = "seed_b",     seed = 777,      ticks = 1800,
                           placementTicks = new[] { 180, 420, 660, 900, 1200 } },
            new Scenario { name = "seed_c",     seed = 31337,    ticks = 1800,
                           placementTicks = new[] { 180, 420, 660, 900, 1200 } },
            // 배치를 안 하는 판 — 적이 그대로 골까지 간다(유출·공성·골 붕괴 경로).
            new Scenario { name = "no_defense", seed = 20260822, ticks = 1800,
                           placementTicks = new int[0] },
            // 연속 이동 아군(순찰병)이 판에 서는 유일한 시나리오. 위 defenderIds 주석 참조 —
            // 이게 없으면 `AttackReach` 2차 게이트의 실효 경로가 코퍼스에 0 이다.
            new Scenario { name = "summoner",   seed = 20260822, ticks = 1800,
                           placementTicks = new[] { 150, 330, 510, 690, 900 },
                           defenderIds = new[] { "summoner", "archer", "cannon" } },
            // 판 중간 재시작. 리셋이 새는 순간 뒤쪽 절반이 통째로 갈린다.
            // ⚠ 각 반쪽이 **최소 20초**여야 한다. 처음엔 10초씩(1200틱/재시작 600)으로 잡았더니
            // 두 반쪽 다 교전 전에 끝나 **이벤트 0개짜리 골든**이 됐다 — 통과하지만 아무것도
            // 증언하지 않는 골든이라 있으나 마나다.
            new Scenario { name = "restart",    seed = 4242,     ticks = 2400,
                           placementTicks = new[] { 200, 500, 900, 1400, 1700, 2000 },
                           restartAtTick = 1200 },
            // 웨이브를 입력으로 당기는 판.
            new Scenario { name = "force_wave", seed = 20260822, ticks = 1800,
                           placementTicks = new[] { 150, 330, 510, 690 },
                           pullWaveTicks = new[] { 300, 600, 900, 1200 } },
        };

        public struct Digest
        {
            public float clock;
            public int entities;
            public ulong fingerprint;

            public bool Equals(Digest o) => clock == o.clock && entities == o.entities && fingerprint == o.fingerprint;
            public override string ToString() => $"{clock:F4} / {entities} / {fingerprint:X16}";
        }

        public struct RunResult
        {
            public string configHash;
            public Digest[] digests;
            public LegacyTraceV0 trace;   // record: false 면 null
        }

        public static RunResult Run(BattleBridge bridge, in Scenario sc, bool record)
        {
            StartMatch(bridge, sc);
            if (record)
                LegacyTraceRecorder.Begin(sc.name, bridge.MatchConfigHash, sc.seed, StepDt);

            var digests = new Digest[sc.ticks];
            SimHarnessClock.Begin(StepDt);
            try
            {
                for (int t = 0; t < sc.ticks; t++)
                {
                    if (sc.restartAtTick > 0 && t == sc.restartAtTick)
                    {
                        // ⚠ 하네스 시계를 끄지 않고 재시작한다. StopBattle→StartBattle 은
                        // MonoBehaviour 경로가 아니라 직접 호출이라 프레임을 쓰지 않는다.
                        StartMatch(bridge, sc);
                    }
                    LegacyTraceRecorder.SetTick(t);
                    ApplyScheduledInput(bridge, sc, t);
                    if (sc.pullWaveTicks != null && System.Array.IndexOf(sc.pullWaveTicks, t) >= 0)
                        bridge.TryPullNextWave();   // 규칙 층(상한·쿨다운)은 그대로 통과시킨다
                    bridge.StepOneTick();
                    digests[t] = Capture(bridge);
                }
            }
            catch
            {
                // 기록기가 켜진 채로 빠져나가면 **라이브 세션이 계속 기록**한다(누수 + 오염).
                // 시계와 달리 기록기는 End 가 성공 경로에만 있어 여기서 따로 닫는다.
                LegacyTraceRecorder.Abort();
                throw;
            }
            finally
            {
                SimHarnessClock.End();
            }

            var result = new RunResult { configHash = bridge.MatchConfigHash, digests = digests };
            if (record)
            {
                bridge.ReadFinalTally(out int kills, out int score, out int leaks);
                result.trace = LegacyTraceRecorder.End(
                    sc.ticks, kills, score, leaks,
                    digests.Length > 0 ? digests[digests.Length - 1].fingerprint : 0UL);
            }
            return result;
        }

        private static void StartMatch(BattleBridge bridge, in Scenario sc)
        {
            bridge.StopBattle();
            bridge.SetMatchSeed(sc.seed);
            bridge.PrepareDraftMap();
            // ⚠ 덱 교체는 **`BeginPlacement` 앞**이어야 한다 — 그 호출이 판을 짓고 코스트·상한을
            // 덱 기준으로 세운다. 뒤에 바꾸면 배치가 옛 덱 기준으로 거부된다.
            ApplyScenarioPool(bridge, sc);
            bridge.BeginPlacement();
            bridge.StartBattle();

            // ⚠ 코스트 재생의 스위치는 **UI 가 갖고 있다**(`PlacementPhaseView`). 스크립트
            // 진입은 그 뷰를 지나지 않아 코스트가 0 에 멎고, 그러면 배치 입력이 전부
            // InsufficientCost 로 거부된다. 여기서 하네스가 그 UI 역할을 대신한다.
            var cost = Wassup.Core.GameManager.Instance != null
                ? Wassup.Core.GameManager.Instance.CostRuntime : null;
            if (cost != null) { cost.ResetToStart(); cost.BeginRegen(); }
        }

        // 씬 기본 덱. **첫 호출 때 한 번만** 잡는다 — 덱을 지정한 시나리오가 돈 뒤에
        // 잡으면 그게 기본값으로 굳는다.
        private static Wassup.Data.DefenderUnitData[] _sceneDefaultPool;

        // 시나리오가 덱을 지정했으면 갈아끼우고, 아니면 **씬 기본으로 되돌린다.**
        // ⚠ 되돌리기가 없으면 `SetDefenderPool` 이 브리지의 serialized 필드를 영구히 덮어
        // **다음 시나리오까지 남의 덱으로 돈다** — 코퍼스 전체가 조용히 다른 판이 된다.
        // 못 찾은 id 는 조용히 넘기지 않는다: 빠진 채로 돌면 그 시나리오가 증언하려던 축이
        // 사라지는데 통과는 한다.
        private static void ApplyScenarioPool(BattleBridge bridge, in Scenario sc)
        {
            if (_sceneDefaultPool == null) _sceneDefaultPool = bridge.DefenderPool;
            if (sc.defenderIds == null || sc.defenderIds.Length == 0)
            {
                bridge.SetDefenderPool(_sceneDefaultPool);
                return;
            }
            var all = Resources.FindObjectsOfTypeAll<Wassup.Data.DefenderCatalog>();
            if (all == null || all.Length == 0)
            {
                Debug.LogError($"[SimHarness] '{sc.name}' — DefenderCatalog 를 못 찾았다. 덱 교체 실패.");
                bridge.SetDefenderPool(_sceneDefaultPool);
                return;
            }
            var pool = new List<Wassup.Data.DefenderUnitData>(sc.defenderIds.Length);
            foreach (var id in sc.defenderIds)
            {
                var u = all[0].ById(id);
                if (u == null) { Debug.LogError($"[SimHarness] '{sc.name}' — 덱 id '{id}' 없음."); continue; }
                pool.Add(u);
            }
            bridge.SetDefenderPool(pool.Count > 0 ? pool.ToArray() : _sceneDefaultPool);
        }

        // 입력은 벽시계가 아니라 **틱 번호**로 반입한다 — 그래야 두 판의 입력이 같은 sim 시각에
        // 들어가고, 「입력 타이밍이 달라서 갈렸다」가 원인 후보에서 빠진다.
        private static void ApplyScheduledInput(BattleBridge bridge, in Scenario sc, int tick)
        {
            if (sc.placementTicks == null) return;
            int slot = System.Array.IndexOf(sc.placementTicks, tick);
            if (slot < 0) return;
            var pool = bridge.DefenderPool;
            if (pool == null || pool.Length == 0) return;
            // 슬롯마다 **다른 유닛**. 같은 유닛을 반복하면 첫 배치 뒤 `LimitReached`(타입별 판
            // 상한)로 나머지가 조용히 공전해 스케줄이 반쯤 비어 버린다.
            var unit = pool[slot % pool.Length];
            // 고정 칸을 박지 않는 이유: 맵이 seed 로 정해져 어느 칸이 가능한지 미리 못 박는다.
            //
            // ⚠ **그런데 「가능한 칸」만으로는 부족하다.** 예전엔 (0,0)부터 스캔해 **첫** 가능 칸에
            // 놓았는데, 그건 결정론이긴 해도 「닿는 자리인가」를 묻지 않는다. 맵이 30×9 로 넓어지고
            // 골이 (15,8) 로 가면서 방어유닛 4기가 전부 좌하단 구석에 몰렸고 — 사거리 1~3 인데
            // 골까지 15칸 — **900틱 동안 한 대도 못 때렸다.** 골든 7건의 킬이 전부 0 이 된 원인이며,
            // 「코퍼스에 스킬 발화 기록 0회」의 원인이기도 하다. 통과하지만 아무것도 증언하지 않는
            // 골든은 있으나 마나다(위 restart 시나리오 주석이 같은 교훈을 이미 적어 두었다).
            //
            // 그래서 **골에 가장 가까운 가능 칸**을 고른다. 적은 어느 맵에서든 골로 수렴하므로
            // 이 기준은 맵 모양에 안 기댄다. 동률은 (y, x) 로 깨 **결정론을 유지**한다.
            var goals = bridge.DebugGoalCells;
            int bestX = -1, bestY = -1, bestD = int.MaxValue;
            for (int y = 0; y < ScanExtent; y++)
            for (int x = 0; x < ScanExtent; x++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                int d = int.MaxValue;
                for (int g = 0; g < goals.Length; g++)
                {
                    int dx = x - goals[g].x; if (dx < 0) dx = -dx;
                    int dy = y - goals[g].y; if (dy < 0) dy = -dy;
                    int cheb = dx > dy ? dx : dy;
                    if (cheb < d) d = cheb;
                }
                if (goals.Length == 0) d = y * ScanExtent + x;   // 골 미상 = 옛 스캔 순서로 폴백
                if (d < bestD) { bestD = d; bestX = x; bestY = y; }
            }
            if (bestX < 0) return;   // 가능 칸 없음 — 코스트·상한 소진 등, 조용히 넘어간다
            bridge.PlaceDefenderAs(bestX, bestY, unit);
        }

        // 카운트가 아니라 **상태 지문**이다: 살아 있는 모든 sim 엔티티의 (SimEntityId, 위치,
        // 체력)을 ID 순으로 접은 FNV-1a. 수만 맞고 위치가 갈리는 사고를 통과시키지 않기 위해서다
        // — 그게 정확히 골든이 잡아야 할 종류의 사고다.
        // ⚠ 여기서 `World.DefaultGameObjectInjectionWorld` 를 직접 읽는다. CLAUDE.md 제약 1
        // (BattleBridge 가 유일한 Mono↔ECS 창구)의 예외인 근거는 **이 파일이 Editor 전용**이고
        // (`Assets/_Project/Editor/`), MonoBehaviour 가 아니며, 쿼리 읽기만 한다는 것이다.
        // 런타임으로 옮기는 순간 그 근거가 사라져 제약 위반이 된다 — 옮기려면 Bridge 를 통해라.
        public static Digest Capture(BattleBridge bridge)
        {
            var d = new Digest { clock = bridge.LogElapsedTime };
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return d;
            var em = world.EntityManager;

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<SimEntityId>(),
                ComponentType.ReadOnly<LocalTransform>());
            var ents = q.ToEntityArray(Allocator.Temp);
            var rows = new List<(int id, Vector3 pos, float hp)>(ents.Length);
            for (int i = 0; i < ents.Length; i++)
            {
                var p = em.GetComponentData<LocalTransform>(ents[i]).Position;
                float hp = em.HasComponent<Health>(ents[i]) ? em.GetComponentData<Health>(ents[i]).value : 0f;
                rows.Add((em.GetComponentData<SimEntityId>(ents[i]).value, new Vector3(p.x, p.y, p.z), hp));
            }
            ents.Dispose();
            // ID 오름차순 — 청크 순서가 흔들려도 지문은 흔들리지 않아야 「sim 이 갈렸다」와
            // 「배열 순서가 갈렸다」를 혼동하지 않는다.
            rows.Sort((a, b) => a.id.CompareTo(b.id));

            ulong h = 1469598103934665603UL; // FNV-1a offset basis
            foreach (var r in rows)
            {
                Mix(ref h, (ulong)r.id);
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.x * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.y * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.z * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.hp * 100f));
            }
            d.entities = rows.Count;
            d.fingerprint = h;
            return d;
        }

        private static void Mix(ref ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (i * 8)) & 0xFF;
                h *= 1099511628211UL; // FNV prime
            }
        }
    }
}
