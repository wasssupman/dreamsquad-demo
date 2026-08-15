using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // defender-clock-out unit 1 — 퇴근(자발적 퇴장)이 **사망 경로를 타지 않는다**는 것을 잡는다.
    //
    // 단정을 고른 기준: 사직서 0장 / 작별선물 0 / 각성 0 을 각각 세팅해 확인하면 기믹 매치 부팅과
    // OnDeath 카드 부착이 테스트의 대부분이 되는데, **그 셋은 전부 "DeadTag 가 붙고 DefenderDied 가
    // 쏘였나"에서 파생된다.** 그래서 `DefenderDied 0회`(여러 프레임에 걸쳐) 하나로 그 가족을 덮는다
    // — DeadTag 가 붙었다면 UnitLifecycleSystem 이 DefenderDeathEvent 를 넣고 드레인이 DefenderDied 를
    // 쏘기 때문이다. CLAUDE.md: "커버리지는 목표가 아니다. 회귀 방지 수준이면 충분하다."
    public class DefenderRetireTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Retire_FreesTile_FiresRetiredNotDied_AndCellIsReusable()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var unit = FindCatalog().ById("ranger");
            Assert.IsNotNull(unit, "ranger in catalog");

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            int retired = 0, died = 0;
            bridge.DefenderRetired += (_, __, ___) => retired++;
            bridge.DefenderDied += (_, __, ___) => died++;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var entity = EntityAt(bridge, em, cell);
            Assert.AreNotEqual(Entity.Null, entity, "entity resolved");

            Assert.IsTrue(bridge.RetireDefender(cell), "retire succeeds on a live, landed defender");

            // 즉시 성립하는 것들 — 퇴근은 프레임을 기다리지 않는다(sim 왕복이 없다).
            Assert.AreEqual(Entity.Null, EntityAt(bridge, em, cell), "binding removed");
            Assert.IsFalse(em.Exists(entity), "entity destroyed by the bridge");
            Assert.AreEqual(1, retired, "DefenderRetired fired once");

            // 사망 결과 가족의 가드. DeadTag 가 붙었다면 UnitLifecycleSystem → DefenderDeathEvent →
            // 드레인 → DefenderDied 로 이어진다. 여러 프레임 지켜봐야 그 왕복을 덮는다.
            for (int i = 0; i < 8; i++) yield return null;
            Assert.AreEqual(0, died, "사망 경로에 진입하지 않는다 (사직서·작별선물·각성의 가드)");
            Assert.AreEqual(1, retired, "DefenderRetired 는 더 쏘이지 않는다");

            // 점유가 실제로 풀렸는가 — 상한 1 유닛이 그 칸에 다시 선다.
            Assert.IsTrue(bridge.CanPlaceDefenderAt(cell.x, cell.y, unit, out var reason),
                $"퇴근한 칸이 다시 배치 가능해야 한다 (reason={reason})");
        }

        // 비행 중(배치/재배치 착지 전)에는 내리지 않는다 — 뷰 오버라이드와 활성화 꼬리가 뜬다.
        [UnityTest]
        public IEnumerator Retire_RejectedWhilePendingDeployment()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = FindCatalog().ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // PlaceDefenderAs 는 pendingDeployment:false 라 즉시 활성이다. 비행 상태를 만들려면
            // 드래그 배치가 쓰는 TryBeginDefenderDeployment 로 들어가야 한다.
            Assert.IsTrue(BeginFirstValidDeployment(bridge, unit, out var cell, out var entity),
                "begin pending deployment");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.IsTrue(em.HasComponent<PendingDeployment>(entity), "still in flight");

            Assert.IsFalse(bridge.RetireDefender(cell), "비행 중에는 퇴근이 거부된다");
            Assert.AreEqual(entity, EntityAt(bridge, em, cell), "거부됐으므로 판에 그대로 남는다");

            // 착지시키면 열린다 — 거부가 영구가 아니라 상태 의존임을 같이 잡는다.
            bridge.ActivateDeployedDefender(cell, entity);
            Assert.IsTrue(bridge.RetireDefender(cell), "착지 후에는 퇴근된다");
        }

        // defender-clock-out unit 2 — 퇴근이 그 유닛 타입을 쿨타임에 넣는가. 사망에는 없는
        // 대가라 DefenderDied 핸들러와 갈라 둔 것이 실제로 성립하는지 잡는다.
        //
        // placementCooldown 은 라이브 에셋 기본값이 0("0 = inert")이라 **런타임 사본**에 값을
        // 넣는다 — 카탈로그 에셋을 직접 고치면 에디터에서 디스크에 박힌다(재배치 스위트 선례).
        [UnityTest]
        public IEnumerator Retire_StartsPlacementCooldown_ForThatUnitType()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = Object.Instantiate(FindCatalog().ById("ranger"));
            unit.placementCooldown = 7f;

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            gm.SetPhase(GamePhase.Placement); // 트레이 슬롯은 페이즈 전환이 만든다
            yield return null;

            var cd = gm.CooldownRuntime;
            cd.ResetAll();
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var cell = SoleCell(bridge);
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            Assert.IsTrue(bridge.RetireDefender(cell), "retire");
            Assert.AreEqual(7f, cd.RemainingFor(unit), 0.01f, "퇴근이 그 타입의 쿨타임을 건다");
            Assert.IsFalse(cd.IsReady(unit), "쿨타임 중에는 준비되지 않았다");

            // 다 흐르면 풀린다 — StartCooldown 이 등록만 하고 끝나는 게 아니라 실제로 만료한다.
            cd.Tick(7.01f);
            Assert.IsTrue(cd.IsReady(unit), "쿨타임이 만료하면 다시 배치 가능");
            Object.Destroy(unit);
        }

        // 사망에는 쿨타임이 없다 — 퇴근 핸들러를 DefenderDied 와 합치면 이 단정이 깨진다.
        // (README 열린 밸런스 항목: 그래서 "죽게 두는 게 빠르다" 가 성립할 수 있다.)
        [UnityTest]
        public IEnumerator Death_DoesNotStartPlacementCooldown()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = Object.Instantiate(FindCatalog().ById("ranger"));
            unit.placementCooldown = 7f;

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            gm.SetPhase(GamePhase.Placement);
            yield return null;

            var cd = gm.CooldownRuntime;
            cd.ResetAll();
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var entity = EntityAt(bridge, em, cell);
            gm.SetPhase(GamePhase.Battle);
            bridge.StartBattle();
            yield return null;

            var hp = em.GetComponentData<Health>(entity);
            hp.value = 0f;
            em.SetComponentData(entity, hp);
            for (int i = 0; i < 180 && em.Exists(entity); i++) yield return null;
            Assert.IsFalse(em.Exists(entity), "죽어서 사라졌다");

            Assert.AreEqual(0f, cd.RemainingFor(unit), 0.01f, "사망에는 쿨타임이 붙지 않는다");
            Object.Destroy(unit);
        }

        // defender-clock-out unit 3 — 퇴근 연출. 잡는 것은 **수명**이다(모양은 육안 몫):
        //   ⑴ 뷰가 풀에서 빠진다(Detach) — 사망 애니 경로(NotifyDeath)를 안 탄다는 뜻
        //   ⑵ 비행이 끝나면 그 GameObject 가 파괴된다 — Detach 계약("수명은 호출자 것")의 이행
        //   ⑶ 두 유닛 연속 퇴근이 각각 끝난다 — 단일 슬롯이 아니라 목록이라는 것
        [UnityTest]
        public IEnumerator Retire_DetachesView_AndFlightDisposesIt()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var flight = Object.FindObjectOfType<Wassup.UI.DefenderRetireFlight>();
            Assert.IsNotNull(flight, "DefenderRetireFlight 가 씬에 배선돼 있어야 한다 (unit 3)");

            var unit = Object.Instantiate(FindCatalog().ById("ranger"));
            unit.maxOnBoard = 99; // 두 기를 연속 퇴근시킨다
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place #1");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell1 = SoleCell(bridge);
            var e1 = EntityAt(bridge, em, cell1);
            Assert.IsTrue(bridge.TryGetUnitView(e1, out var view1), "뷰가 풀에 있다");
            var go1 = view1.gameObject;

            gm.SetPhase(GamePhase.Battle);
            yield return null;
            Assert.IsTrue(bridge.RetireDefender(cell1), "retire #1");

            // ⑴ 풀에서 빠졌다. 사망 경로(NotifyDeath)였다면 여기서도 빠지지만, 그 경우 뷰는
            //    사망 애니를 재생하며 자멸한다 — 아래 ⑵ 가 그 차이를 가른다.
            Assert.IsFalse(bridge.TryGetUnitView(e1, out _), "뷰가 풀에서 떨어졌다");
            Assert.IsNotNull(go1, "떼어낸 직후에는 아직 살아 있다(비행 중)");
            Assert.AreEqual(1, flight.InFlightCount, "비행 1건");

            // ⑶ 두 번째를 곧바로 퇴근 — 단일 슬롯이면 첫 비행이 덮여 고아가 된다.
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place #2");
            var cell2 = SoleCell(bridge);
            Assert.IsTrue(bridge.RetireDefender(cell2), "retire #2");
            Assert.AreEqual(2, flight.InFlightCount, "두 비행이 동시에 산다");

            // ⑵ 끝나면 파괴된다. 비행은 Battle 도메인 시계라 프레임을 흘려보낸다.
            for (int i = 0; i < 300 && flight.InFlightCount > 0; i++) yield return null;
            Assert.AreEqual(0, flight.InFlightCount, "두 비행 모두 종료");
            yield return null;
            Assert.IsTrue(go1 == null, "비행이 끝난 뷰는 파괴된다 (고아 GameObject 0)");
            Object.Destroy(unit);
        }

        // 코드리뷰 2026-08-15 — unit 3 완료 기준("비행 중 매치를 종료해도 고아 GameObject 가 0")에
        // **테스트가 없어서 구현 누락이 통과했다.** teardown 훅이 실제로 없었고, 그 결과 재시작 시
        // 지난 판 유닛과 키링이 새 판 보드 위에서 놀았다. 이 테스트가 그 회귀 가드다.
        //
        // ⚠ OnDisable 에 기대면 안 된다 — 이 컴포넌트가 붙은 GO 는 씬 루트라 매치 재시작으로는
        // 비활성화되지 않는다. BattleBridge.TeardownCurrentBattle 의 명시 호출만이 유효 경로다.
        [UnityTest]
        public IEnumerator RetireFlight_IsCancelled_OnMatchTeardown_NoOrphans()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var flight = Object.FindObjectOfType<Wassup.UI.DefenderRetireFlight>();
            Assert.IsNotNull(flight, "DefenderRetireFlight 배선");

            var unit = FindCatalog().ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var entity = EntityAt(bridge, em, cell);
            Assert.IsTrue(bridge.TryGetUnitView(entity, out var view), "뷰가 풀에 있다");
            var go = view.gameObject;

            gm.SetPhase(GamePhase.Battle);
            yield return null;
            Assert.IsTrue(bridge.RetireDefender(cell), "retire");
            Assert.AreEqual(1, flight.InFlightCount, "비행 중");
            Assert.IsNotNull(go, "아직 살아 있다");

            // 비행이 끝나기 **전에** 매치를 종료한다.
            // ⚠ `BeginPlacement()` 로는 안 된다 — 그건 `TeardownCurrentBattle()` 을 **지나지
            // 않는다**(확인함). 라이브 teardown 경로는 `StopBattle()` 이다
            // (`OnRestartRequested` 는 GameManager 주석대로 dormant).
            // 이 테스트가 처음 실패해서 그 사실을 잡아냈다 — 훅을 안 타는 트리거로 검증하면
            // 통과해도 아무것도 증명하지 못한다.
            bridge.StopBattle();
            yield return null;

            Assert.AreEqual(0, flight.InFlightCount, "teardown 이 진행 중 비행을 취소한다");
            Assert.IsTrue(go == null, "떼어낸 뷰가 파괴된다 (고아 0)");
            Assert.AreEqual(0, GameObject.FindObjectsByType<UnityEngine.LineRenderer>(FindObjectsSortMode.None)
                .Count(l => l != null && l.transform.root != null
                            && l.transform.root.name.Contains("RelocationKeyring")),
                "키링 루트도 남지 않는다");
        }

        // 코드리뷰 2026-08-15 — feature 계약 5(카드 회수)와 unit 2 의 **반파밍 결정**
        // ("각성은 퇴장의 보상이 아니다 — 주면 배치→퇴근 반복이 게이지 파밍")이 한 줄도
        // 검증되지 않고 있었다. DreamcatcherHandController 의 퇴근 핸들러를 사망 핸들러와
        // 합치는 리팩터가 들어오면 파밍이 조용히 부활하고 아무 테스트도 안 빨개진다.
        [UnityTest]
        public IEnumerator Retire_RecoversCards_ButGrantsNoAwakening()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var hand = Object.FindObjectOfType<DreamcatcherHandController>();
            Assert.IsNotNull(hand, "DreamcatcherHandController");

            var unit = FindCatalog().ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            gm.SetPhase(GamePhase.Placement); // 덱 구성 = 배치 진입에서 일어난다
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var cell = SoleCell(bridge);
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            int gaugeBefore = hand.Gauge;
            Assert.IsTrue(bridge.RetireDefender(cell), "retire");
            for (int i = 0; i < 4; i++) yield return null;

            // 각성은 **사망의 보상**이다. 퇴근에 주면 배치→퇴근 반복이 파밍이 된다.
            Assert.AreEqual(gaugeBefore, hand.Gauge,
                "퇴근은 각성 게이지를 올리지 않는다 (반파밍 계약)");
            Assert.Greater(unit.awakeningReward, 0,
                "대조 전제: 이 유닛은 사망 시 지급할 각성이 있다 — 없으면 위 단정이 공허하다");
        }

        // ── helpers (재배치 스위트와 동형) ────────────────────────────────────

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static bool BeginFirstValidDeployment(BattleBridge bridge, DefenderUnitData u,
            out Vector2Int cell, out Entity entity)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                    {
                        cell = new Vector2Int(x, y);
                        return bridge.TryBeginDefenderDeployment(x, y, u, out entity);
                    }
            cell = default; entity = Entity.Null;
            return false;
        }

        private static System.Collections.IDictionary ByTile(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.IDictionary)f.GetValue(bridge);
        }

        private static Vector2Int SoleCell(BattleBridge bridge)
        {
            foreach (System.Collections.DictionaryEntry de in ByTile(bridge))
                return (Vector2Int)de.Key;
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private static Entity EntityAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var dict = ByTile(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var val = dict[cell];
            var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
            return em.Exists(entity) ? entity : Entity.Null;
        }
    }
}
