using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // elite-enemy-tier unit 5/6 — 엘리트 슬라임 분열 e2e.
    //
    // 사슬 전체를 태운다: 배송 에셋(Enemy_Slime) 스폰 → 치사 피해 → 킬 이벤트 → 브리지 드레인이
    // **SO 를 직독**해 중간 2기 스폰 → 중간을 죽이면 작은 4기 → 작은에서 사슬이 끝난다.
    //
    // 이 테스트가 지키는 것이 왜 «순수 함수 그린» 으로 대체되지 않나: 분열은 슬롯도 이벤트
    // 필드도 sim 변경도 없는 **브리지 드레인 한 곳**이라, 검증할 수 있는 순수 조각이 없다.
    // 유일한 증거는 «죽였더니 둘이 생겼다» 다.
    //
    // ⚠ 슬라임은 라이브 덱 풀에 없어서 BattleScene 로드로 메모리에 올라오지 않는다
    // (Resources.FindObjectsOfTypeAll 로는 못 찾는다) → AssetDatabase 로 직접 로드한다
    // (BossShieldTest 선례).
    public class SlimeSplitE2ETest
    {
        private const string ParentPath = "Assets/_Project/Data/Enemies/Enemy_Slime.asset";
        private const string MidPath = "Assets/_Project/Data/Enemies/Enemy_Slime_Mid.asset";
        private const string ChildPath = "Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset";

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_SplitsTwice_AtDeathSpot_AndChainTerminates()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge");

            // ★배틀을 실제로 시작해야 한다 — Update 가 `if (!_running) return;` 으로 막혀 있어서
            // 시작하지 않으면 **브리지 드레인이 한 번도 돌지 않는다.** 분열은 그 드레인에 살아
            // 있으므로(unit 5 ②) 이 한 줄이 없으면 «자식 0» 이 되고, 원인이 구현처럼 보인다.
            // (순수 ECS 를 보는 테스트들 — 예: BossShieldTest — 은 이게 없어도 통과한다.)
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var parentSo = LoadEnemy(ParentPath);
            var midSo = LoadEnemy(MidPath);
            var childSo = LoadEnemy(ChildPath);
            Assert.AreEqual(EnemyTier.Elite, parentSo.tier, "슬라임은 엘리트여야 한다");

            // 엘리트는 보스가 아니다 — 이 spec 의 핵심 계약. 스폰된 실체로 확인한다.
            var parent = SpawnEnemy(bridge, em, parentSo);
            Assert.AreNotEqual(Entity.Null, parent, "슬라임 스폰 실패");
            Assert.IsFalse(em.HasComponent<Wassup.Battle.Combat.BossTag>(parent),
                "엘리트에 BossTag 가 붙었다 — CC·어그로 면역이 딸려온다");

            var deathPos = em.GetComponentData<LocalTransform>(parent).Position;

            // 치사 피해 — 표준 경로(IncomingDamage → DamageApplicationSystem → EnemyKilledEvent).
            int killCountBefore = KillCount(bridge);
            var h = em.GetComponentData<Health>(parent);
            em.GetBuffer<IncomingDamage>(parent).Add(new IncomingDamage { amount = h.max * 10f });

            // ── 1단계: 본체 → 중간 2기 ──────────────────────────────────────────
            List<Entity> mids = null;
            // 저작 magnitude 가 드리프트하면 «2기 채우면 탈출» 이 3번째 스폰과 레이스한다.
            // 저작값을 읽어 기대치를 만든다(리뷰 A-L8).
            int expectedMids = Wassup.Data.SplitChain.CountAt(parentSo);
            Assert.AreEqual(2, expectedMids, "저작이 2기 분열이 아니다 — 아래 단언의 전제");
            for (int i = 0; i < 30 && (mids == null || mids.Count < expectedMids); i++)
            {
                yield return null;
                mids = FindEnemiesOfType(bridge, em, midSo);
            }

            // ★경계 계측 — 「킬 드레인이 돌았나」와 「분열이 돌았나」를 분리한다.
            // 이게 없으면 실패가 «자식 0» 한 덩어리로 뭉쳐서 어디서 끊겼는지 안 보인다.
            Assert.Greater(KillCount(bridge), killCountBefore,
                "킬 드레인 자체가 돌지 않았다(_killCount 불변) — EnemyKilledEvent 발화 또는 " +
                "DrainEnemyKilledEvents 호출 경로 문제. 분열 코드는 아직 의심 대상이 아니다");

            Assert.IsNotNull(mids);
            Assert.AreEqual(2, mids.Count,
                $"중간 슬라임이 정확히 2기여야 한다(실제 {mids?.Count}) — magnitude 저작 또는 드레인 배선");
            Assert.IsFalse(em.Exists(parent) && !em.HasComponent<DeadTag>(parent), "부모가 살아 있다");

            foreach (var c in mids)
            {
                var ch = em.GetComponentData<Health>(c);
                Assert.AreEqual(parentSo.health * 0.5f, ch.max, 0.01f, "중간 최대체력 = 본체의 50%");
                // ★셀 동일성으로 단언한다. `planar < TileSize` 는 자식이 **한 셀 온전히**
                // 떨어져도 통과해서 계약(같은 셀)과 검사가 어긋나 있었다(리뷰 B-H1 잔여).
                var p = em.GetComponentData<LocalTransform>(c).Position;
                Assert.AreEqual(bridge.DebugWorldToCell(deathPos), bridge.DebugWorldToCell(p),
                    "자식이 부모가 죽은 셀 밖에 태어났다 — 골 셀이면 «처치했는데 유출» 이 된다");
            }

            // 분열체가 실제로 **움직인다** — 스폰만 되고 굳는 계열 회귀 방지
            // (summon-patrol-defender 가 겪은 «뷰가 제자리에 선다» 와 같은 종류).
            var start = em.GetComponentData<LocalTransform>(mids[0]).Position;
            for (int i = 0; i < 120; i++) yield return null;
            if (em.Exists(mids[0]) && !em.HasComponent<DeadTag>(mids[0]))
            {
                var now = em.GetComponentData<LocalTransform>(mids[0]).Position;
                Assert.Greater(Vector3.Distance(start, now), 0.05f,
                    "분열체가 한 칸도 움직이지 않았다 — PathFollowState bake 또는 임의 위치 스폰 문제");
            }

            // ── 2단계: 중간 → 작은 4기 ──────────────────────────────────────────
            mids = FindEnemiesOfType(bridge, em, midSo);
            int midsKilled = mids.Count;
            // 이동 대기(120프레임) 사이에 중간이 유출되면 midsKilled=0 이 되고 아래 단언이
            // AreEqual(0, 0) 으로 **진공 통과**한다 — 2단계 회귀가 초록으로 지나간다(리뷰 B-M4).
            Assert.Greater(midsKilled, 0,
                "중간 슬라임이 이동 대기 중 전부 사라졌다 — 이 단언은 진공이다(테스트 전제 붕괴)");
            foreach (var c in mids)
                if (em.Exists(c) && em.HasBuffer<IncomingDamage>(c))
                    em.GetBuffer<IncomingDamage>(c).Add(new IncomingDamage { amount = 99999f });

            List<Entity> smalls = null;
            for (int i = 0; i < 40 && (smalls == null || smalls.Count < midsKilled * 2); i++)
            {
                yield return null;
                smalls = FindEnemiesOfType(bridge, em, childSo);
            }
            Assert.AreEqual(midsKilled * 2, smalls.Count,
                $"중간 {midsKilled}기가 죽으면 작은 슬라임 {midsKilled * 2}기가 나와야 한다(2단계 분열)");
            foreach (var c in smalls)
                Assert.AreEqual(LoadEnemy(MidPath).health * 0.5f,
                    em.GetComponentData<Health>(c).max, 0.01f, "작은 최대체력 = 중간의 50%");

            // ── 사슬 종료: 작은 슬라임을 죽여도 더 안 생긴다 ─────────────────────
            foreach (var c in smalls)
                if (em.Exists(c) && em.HasBuffer<IncomingDamage>(c))
                    em.GetBuffer<IncomingDamage>(c).Add(new IncomingDamage { amount = 99999f });

            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(0, FindEnemiesOfType(bridge, em, childSo).Count,
                "작은 슬라임이 남았거나 다시 태어났다 — 사슬이 끝나지 않는다");
            Assert.AreEqual(0, FindEnemiesOfType(bridge, em, midSo).Count,
                "중간 슬라임이 다시 태어났다 — 사슬이 순환한다");
        }

        // 드레인 순서 계약(unit 5 ④) — spec 이 「이 spec 이 만드는 가장 조용한 버그」라 부른 것.
        //
        // ★초판은 「킬 집계와 자식 존재가 같은 관측에 보인다」를 단언하고 그것이 「드레인을
        // QueueDueWaves 뒤로 되돌리는 변경」을 잡는다고 주석에 적었다. **거짓이었다**
        // (2026-08-12 코드리뷰 H1): `_killCount++` 와 분열 스폰은 같은 `while (TryDequeue)`
        // 이터레이션 안에 있어서 둘의 동시성은 드레인 **자체**의 성질이고, 드레인이 Update 안
        // 어디에 있든 참이다. 테스트가 그 사이를 관측할 수도 없다(둘이 한 Update 본문 안이라
        // 어떤 yield 케이던스도 끼어들지 못한다).
        //
        // 그래서 spec 이 원래 요구한 **관측 가능한** 단언으로 교체한다: 부모가 마지막 적일 때
        // 죽여도 `_nextWaveIndex` 가 오르지 않는다. 전제를 리플렉션으로 만들어 스케줄러 타이밍
        // 의존을 없앤다 — ① 생성 웨이브 모드 ② `_nextWaveIndex >= 1`(first 분기 배제)
        // ③ `_waveStartSec` 갱신(상한 간격 배제) ④ `_pending` 비움 ⑤ 슬라임이 유일한 적.
        [UnityTest]
        public IEnumerator KillingLastSlime_DoesNotAdvanceWave_BecauseChildrenAreBornFirst()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            Assert.IsFalse((bool)GetField(bridge, "_usingAuthoredPlan"),
                "이 계약은 생성 웨이브 경로에만 있다 — 작성 플랜은 순수 시각 스케줄이라 검증 불가");

            var parentSo = LoadEnemy(ParentPath);
            var midSo = LoadEnemy(MidPath);

            // 판을 비우고 슬라임만 남긴다 = 「부모가 마지막 적」 재현.
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var existing = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < existing.Length; i++) em.DestroyEntity(existing[i]);
            existing.Dispose();
            ((System.Collections.IList)GetField(bridge, "_pending")).Clear();

            var parent = SpawnEnemy(bridge, em, parentSo);
            Assert.AreNotEqual(Entity.Null, parent);

            // first / capReached 분기를 배제한다.
            SetField(bridge, "_nextWaveIndex", 1);
            SetField(bridge, "_waveStartSec", (float)(double)GetField(bridge, "_battleClock"));
            yield return null;

            int waveBefore = (int)GetField(bridge, "_nextWaveIndex");
            em.GetBuffer<IncomingDamage>(parent).Add(new IncomingDamage { amount = 999999f });

            // 사망(sim) → 다음 Update 의 드레인에서 자식이 태어난다. 그 Update 안에서
            // QueueDueWaves 가 「적 0」을 보면 웨이브가 넘어간다 — 그것이 회귀다.
            List<Entity> kids = null;
            for (int i = 0; i < 40 && (kids == null || kids.Count < 2); i++)
            {
                yield return null;
                kids = FindEnemiesOfType(bridge, em, midSo);
                Assert.AreEqual(waveBefore, (int)GetField(bridge, "_nextWaveIndex"),
                    "부모가 마지막 적일 때 죽였는데 웨이브가 넘어갔다 — DrainEnemyKilledEvents 가 " +
                    "QueueDueWaves 뒤로 돌아갔다(unit 5 ④)");
            }
            Assert.AreEqual(2, kids.Count, "자식이 안 태어났다 — 이 테스트의 전제가 깨졌다");
        }

        // ── helpers ─────────────────────────────────────────────────────────────
        // 씬 로드·에셋 직독·리플렉션 스폰·브리지 필드 접근은 `BattleBridgeTestAccess` 가
        // 소유한다. 그 파일의 주석에 「왜 한 자리로 모았나」가 있다(개명 한 번에 테스트가
        // 조용히 죽은 이력).

        private static IEnumerator LoadBattle() => BattleBridgeTestAccess.LoadBattleScene();

        private static AttackUnitData LoadEnemy(string path)
            => BattleBridgeTestAccess.LoadEnemy(path);

        private static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, AttackUnitData unit)
            => BattleBridgeTestAccess.SpawnEnemy(bridge, em, unit);

        private static List<Entity> FindEnemiesOfType(
            BattleBridge bridge, EntityManager em, AttackUnitData so)
            => BattleBridgeTestAccess.FindEnemiesOfType(bridge, em, so);

        private static object GetField(BattleBridge bridge, string name)
            => BattleBridgeTestAccess.Field(bridge, name);

        private static void SetField(BattleBridge bridge, string name, object value)
            => BattleBridgeTestAccess.SetField(bridge, name, value);

        // 킬 드레인이 실제로 돌았는지의 관측창(그 루프가 매 이벤트마다 올린다).
        private static int KillCount(BattleBridge bridge)
            => (int)BattleBridgeTestAccess.Field(bridge, "_killCount");
    }
}
