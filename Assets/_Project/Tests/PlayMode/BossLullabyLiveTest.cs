using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // boss-mamemo — **증상 재현 테스트** (사용자 보고 2026-08-11: "재우는 효과가 발생하지 않는다").
    //
    // 기존 BossLullabyTest 는 유닛을 도넛 안으로 **순간이동**시킨 합성 상황이라 초록이었다.
    // 그건 "내 분기가 옳은 값을 내는가" 를 물을 뿐, 사용자의 문장을 재현하지 못한다.
    // 여기서는 **실제 배치 경로**로 방어유닛을 세우고, StartBattle 후 마메모가 **스스로 걸어와**
    // 교전하게 둔 다음 — 누가 자는지를 본다. 좌표를 손으로 만지지 않는다.
    //
    // 실패 시에는 매 프레임 계측(보스↔각 방어유닛 체비셰프 거리 · 도넛 경계 · 발동 횟수)을
    // 같이 덤프한다. "안 잔다" 만으로는 어느 경계에서 끊겼는지 알 수 없기 때문이다.
    public class BossLullabyLiveTest
    {
        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveBattle_MamemoWalksIn_AndSomeoneActuallySleeps()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            // defender-board-limit — 이 테스트는 자장가 대상이 여럿이어야 성립해 같은 유닛을 4기
            // 세운다. 라이브 기본값 maxOnBoard=1 이면 1기에서 멈추므로 상한을 푼다 — 카탈로그
            // 에셋이 아니라 **런타임 사본**에만 쓴다(에셋 직접 수정은 디스크에 박힌다).
            var unit = Object.Instantiate(cat.ById("ranger"));
            unit.maxOnBoard = 99;
            Assert.IsNotNull(unit, "ranger");

            // 실제 배치 경로 — 좌표를 손으로 주지 않는다.
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var placed = new System.Collections.Generic.List<Entity>();
            for (int n = 0; n < 4; n++)
            {
                if (!PlaceNext(bridge, unit)) break;
                yield return null;
            }
            bridge.StartBattle();
            yield return null;
            CollectDefenders(bridge, em, placed);
            Assert.GreaterOrEqual(placed.Count, 2, "방어유닛이 실제 배치 경로로 2기 이상 섰다");

            var boss = SpawnMamemo(bridge, em);
            Assert.AreNotEqual(Entity.Null, boss);

            // 계측 — 매 프레임 보스↔방어유닛 최소 체비셰프 거리와 도넛 통과 여부를 센다.
            int attackTiles = AttackTilesOf(em, boss);
            int inner = attackTiles + 1, outer = SleepRangeOf(em, boss);
            float tile = ResolveTileSize(em);
            int framesInsideDonut = 0, framesInsideAttack = 0, framesFar = 0;
            int minDistSeen = int.MaxValue, maxDistSeen = int.MinValue;
            bool anySlept = false;

            // 실제 조우 길이(다른 보스 생존 4~7초) 를 넉넉히 덮는 12초.
            // **끝까지 돌린다** — 첫 수면에서 멈추면 "몇 프레임이나 자는가"(체감의 실체)를 못 본다.
            const int Frames = 720;
            int framesAnyAsleep = 0, sleepOnsets = 0;
            bool prevAsleep = false;
            int firstSleepFrame = -1;
            var timeline = new StringBuilder();
            for (int f = 0; f < Frames; f++)
            {
                yield return null;
                if (!em.Exists(boss)) { timeline.AppendLine($"  f{f}: 보스 사망 — 조우 종료"); break; }

                int best = int.MaxValue;
                int asleepNow = 0;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (!em.Exists(placed[i])) continue;
                    int d = ChebyshevBetween(em, boss, placed[i], tile);
                    if (d < best) best = d;
                    if (HasCc(em, placed[i], CcKind.Sleep)) asleepNow++;
                }
                bool nowAsleep = asleepNow > 0;
                if (nowAsleep)
                {
                    anySlept = true;
                    framesAnyAsleep++;
                    if (firstSleepFrame < 0) firstSleepFrame = f;
                    if (!prevAsleep) { sleepOnsets++; timeline.AppendLine($"  f{f}: 수면 시작 ({asleepNow}기, 최근접 {best}타일)"); }
                }
                else if (prevAsleep) timeline.AppendLine($"  f{f}: 전원 기상 (최근접 {best}타일)");
                prevAsleep = nowAsleep;

                if (best == int.MaxValue) continue;
                minDistSeen = math.min(minDistSeen, best);
                maxDistSeen = math.max(maxDistSeen, best);
                if (best <= attackTiles) framesInsideAttack++;
                else if (best <= outer) framesInsideDonut++;
                else framesFar++;
            }

            var log = new StringBuilder();
            log.AppendLine("=== 자장가 실플레이 계측 ===");
            log.AppendLine($"방어유닛 {placed.Count}기 · 보스 사거리 {attackTiles}타일 · 도넛 {inner}~{outer}");
            log.AppendLine($"최근접 거리 범위 {minDistSeen}~{maxDistSeen}");
            log.AppendLine($"프레임 분포 — 사거리 안(<={attackTiles}) {framesInsideAttack} · **도넛({inner}~{outer}) {framesInsideDonut}** · 도넛 밖(>{outer}) {framesFar}");
            log.AppendLine($"수면: 최초 f{firstSleepFrame} · 시작 {sleepOnsets}회 · 누적 {framesAnyAsleep}프레임 ({framesAnyAsleep / 60f:F1}초)");
            log.Append(timeline);
            Debug.Log(log.ToString());   // ← 통과해도 남긴다. 증상은 "안 잔다" 가 아니라 "거의 안 잔다" 일 수 있다.

            Assert.IsTrue(anySlept, "실플레이 조우에서 방어유닛이 실제로 잔다 (사용자 보고 증상)\n" + log);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static int ChebyshevBetween(EntityManager em, Entity a, Entity b, float tile)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            var ff = q.GetSingleton<FlowFieldSingleton>();
            int2 ca = Wassup.Battle.Movement.GridMath.WorldToCell(
                em.GetComponentData<LocalTransform>(a).Position, ff.tileSize, ff.gridSize, origin: ff.origin);
            int2 cb = Wassup.Battle.Movement.GridMath.WorldToCell(
                em.GetComponentData<LocalTransform>(b).Position, ff.tileSize, ff.gridSize, origin: ff.origin);
            return Wassup.Battle.Movement.GridMath.ChebyshevDistance(ca, cb);
        }

        private static int AttackTilesOf(EntityManager em, Entity e)
            => em.HasComponent<Wassup.Battle.Combat.AttackState>(e)
                ? Wassup.Battle.Movement.GridMath.RangeToTiles(
                    em.GetComponentData<Wassup.Battle.Combat.AttackState>(e).range)
                : 0;

        private static int SleepRangeOf(EntityManager em, Entity boss)
        {
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].payload == DcPayloadKind.AreaSleep) return slots[i].tileRange;
            return 0;
        }

        private static float ResolveTileSize(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            return q.CalculateEntityCount() == 0 ? 1f : q.GetSingleton<FlowFieldSingleton>().tileSize;
        }

        private static bool HasCc(EntityManager em, Entity e, CcKind kind)
        {
            if (e == Entity.Null || !em.Exists(e) || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++) if (buf[i].kind == kind) return true;
            return false;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        // 배치는 브리지의 실경로 — 이미 점유된 셀은 건너뛰므로 호출마다 다른 셀에 선다.
        private static bool PlaceNext(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static void CollectDefenders(BattleBridge bridge, EntityManager em,
            System.Collections.Generic.List<Entity> outList)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value; var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                if (em.Exists(entity)) outList.Add(entity);
            }
        }

        private static Entity SpawnMamemo(BattleBridge bridge, EntityManager em)
        {
            var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(
                "Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset");
            Assert.IsNotNull(unit);
            var bt = typeof(BattleBridge);
            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            pendingType.GetField("entry").SetValue(pending,
                new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 });
            pendingType.GetField("laneIndex").SetValue(pending, 0);
            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Combat.BossTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity found = Entity.Null;
            for (int i = 0; i < arr.Length; i++) found = arr[i];
            arr.Dispose();
            return found;
        }
    }
}
