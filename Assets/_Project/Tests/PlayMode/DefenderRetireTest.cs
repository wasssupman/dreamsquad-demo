using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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

        // defender-clock-out unit 2 → unit 5 — 퇴근이 그 유닛 타입을 쿨타임에 넣는가.
        // unit 5 부터는 사망에도 대가가 있으므로 이 테스트가 잡는 것은 "대가의 유무"가 아니라
        // **퇴근이 자기 값(사망의 ratio 배)을 쓴다**는 것이다.
        //
        // 값은 **런타임 사본**에 넣는다 — 카탈로그 에셋을 직접 고치면 에디터에서 디스크에
        // 박힌다(재배치 스위트 선례).
        [UnityTest]
        public IEnumerator Retire_StartsRetireCooldown_ForThatUnitType()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = Object.Instantiate(FindCatalog().ById("ranger"));
            unit.deathCooldown = 20f;
            unit.retireCooldownRatio = 0.35f; // → 퇴근 7초

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
            Assert.AreEqual(7f, cd.RemainingFor(unit), 0.01f,
                "퇴근이 거는 것은 사망 초가 아니라 그 ratio 배다 (20 × 0.35)");
            Assert.IsFalse(cd.IsReady(unit), "쿨타임 중에는 준비되지 않았다");

            // 다 흐르면 풀린다 — StartCooldown 이 등록만 하고 끝나는 게 아니라 실제로 만료한다.
            cd.Tick(7.01f);
            Assert.IsTrue(cd.IsReady(unit), "쿨타임이 만료하면 다시 배치 가능");
            Object.Destroy(unit);
        }

        // defender-clock-out unit 5 — **이 테스트는 뒤집혔다.** 원래 이름은
        // `Death_DoesNotStartPlacementCooldown` 이었고 "사망에는 대가가 없다"를 지키는 경비였다.
        // 그 계약이 만든 것은 인버전이었다 — 퇴근 4초 / 사망 0초 = 죽게 두는 쪽이 항상 빠름.
        // unit 5 가 그 계약을 폐기하고, 이제 잡는 것은 **사망이 퇴근보다 길다**는 방향이다.
        //
        // 초 값이 아니라 부등호를 단정한다 — 밸런스 숫자는 시트에서 바뀌지만 방향은 규칙이다.
        [UnityTest]
        public IEnumerator Death_StartsLongerCooldown_ThanRetire()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = Object.Instantiate(FindCatalog().ById("ranger"));
            unit.deathCooldown = 20f;
            unit.retireCooldownRatio = 0.35f;

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

            Assert.AreEqual(20f, cd.RemainingFor(unit), 0.01f, "사망도 이탈 쿨타임을 건다");
            Assert.Greater(cd.RemainingFor(unit), unit.EffectiveRetireCooldown,
                "방치가 회수보다 이득이면 퇴근 버튼은 존재 이유가 없다 — 이 부등호가 이 spec 이다");
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

        // ── dreamcatcher-retire-recall unit 1 (인수인계) ──────────────────────
        //
        // load-bearing 계약 셋을 두 테스트가 나눠 고정한다:
        //   ① 나머지 부착분이 **부착 순서 그대로** 큐 맨 앞으로 (retire)
        //   ② 선언 카드 자신은 맨 뒤 (retire · README 계약 2)
        //   ③ **사망에서는 안 일어난다** (death · 계약 12 의 회귀 방어선 — 앞 삽입이 비동기가
        //      되는 순간 조준 중 손패 창이 밀려 CommitAttach 가 롤백 없이 실패한다)
        //
        // ⚠ 부착 순서를 **entryId 오름차순과 어긋나게** 만든다(둘째를 먼저 붙인다). 같으면
        // 이 테스트가 이 unit 이 새로 만든 축(부착 seq)을 아무것도 증명하지 못한다.
        // ⚠ 채움 카드 3장을 큐에 남긴다. 큐가 비면 «앞으로»와 «뒤로»가 같은 화면이 되어
        // ③ 이 공허해진다.

        [UnityTest]
        public IEnumerator Retire_WithHandoverCard_RecallsOthersToFront_SelfToBack()
        {
            yield return SetupHandoverBoard();

            Assert.IsTrue(_bridge.RetireDefender(_cell), "retire");
            for (int i = 0; i < 3; i++) yield return null;

            var hand = _deck.Hand(6);
            Assert.AreEqual(6, hand.Count, "전부 큐로 돌아왔다");
            Assert.AreEqual("test_second", hand[0].card.id, "부착 1번이 맨 앞");
            Assert.AreEqual("test_first", hand[1].card.id, "부착 2번이 그다음 (부착 순서 보존)");
            Assert.AreEqual("test_handover", hand[5].card.id, "선언 카드 자신은 맨 뒤");

            Object.Destroy(_ctrlGo);
        }

        [UnityTest]
        public IEnumerator Death_WithHandoverCard_RecoversToBack_AsBefore()
        {
            yield return SetupHandoverBoard();

            InvokeOnDefenderDied(_ctrl, _host, FindCatalog().ById("ranger"));
            for (int i = 0; i < 3; i++) yield return null;

            var hand = _deck.Hand(6);
            Assert.AreEqual(6, hand.Count);
            StringAssert.StartsWith("test_filler", hand[0].card.id,
                "사망 회수는 앞을 건드리지 않는다 — 맨 앞은 여전히 채움 카드");
            Assert.AreEqual("test_second", hand[3].card.id, "회수분은 큐 뒤에 부착 순서로 붙는다");
            Assert.AreEqual("test_first", hand[4].card.id);
            Assert.AreEqual("test_handover", hand[5].card.id);

            Object.Destroy(_ctrlGo);
        }

        private BattleBridge _bridge;
        private DreamcatcherHandController _ctrl;
        private DreamcatcherCycleDeck _deck;
        private GameObject _ctrlGo;
        private Vector2Int _cell;
        private Entity _host;

        // 배치된 ranger 하나에 [second → first → handover] 를 **그 순서로** 부착한다.
        // 컨트롤러는 실제 CommitAttach 경로를 타야 한다(부착 seq 는 거기서만 기록된다).
        private IEnumerator SetupHandoverBoard()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = FindCatalog().ById("ranger");
            _bridge.SetDefenderPool(new[] { unit });
            _bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(_bridge, unit), "place defender");
            _cell = SoleCell(_bridge);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _host = EntityAt(_bridge, em, _cell);
            Assert.AreNotEqual(Entity.Null, _host, "host resolved");

            // 실제 컨트롤러(비활성 → 필드 주입 → 활성) — PlacementAuraTest 선례.
            // handSize 를 덱 크기와 맞춰 셔플이 부착 가능 여부를 흔들지 않게 한다.
            var cfg = ScriptableObject.CreateInstance<AwakeningConfig>();
            cfg.costUnit = 0; cfg.handSize = 6; cfg.maxAttachPerUnit = 3;
            var cards = new List<DreamcatcherCard>
            {
                MakeSelfTileAoeCard("test_first", DcTriggerKind.OnDeath, 0f),
                MakeSelfTileAoeCard("test_second", DcTriggerKind.OnDeath, 0f),
                MakeHandoverCard(),
                MakePlainCard("test_filler0"), MakePlainCard("test_filler1"), MakePlainCard("test_filler2"),
            };
            _deck = new DreamcatcherCycleDeck(cards, seed: 0);

            _ctrlGo = new GameObject("HandController_Handover");
            _ctrlGo.SetActive(false);
            _ctrl = _ctrlGo.AddComponent<DreamcatcherHandController>();
            SetField(_ctrl, "bridge", _bridge);
            SetField(_ctrl, "config", cfg);
            SetField(_ctrl, "_deck", _deck);
            _ctrlGo.SetActive(true);

            // 부착 순서 ≠ entryId 순서 (second 를 먼저).
            Assert.IsTrue(_ctrl.CommitAttach(EntryOf(_deck, "test_second"), _host), "attach second");
            Assert.IsTrue(_ctrl.CommitAttach(EntryOf(_deck, "test_first"), _host), "attach first");
            Assert.IsTrue(_ctrl.CommitAttach(EntryOf(_deck, "test_handover"), _host), "attach handover");
            Assert.AreEqual(3, _deck.QueueCount, "채움 3장만 큐에 남는다");

            gm.SetPhase(GamePhase.Battle);
            yield return null;
        }

        private static DreamcatcherCard MakeHandoverCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_handover";
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnRetire },
                payload = new DcPayloadSpec { kind = DcPayloadKind.RecallAttachedToFront },
            }};
            return card;
        }

        // 큐 채움 전용 — 부착되지 않으므로 mechanics 가 필요 없다.
        private static DreamcatcherCard MakePlainCard(string id)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = id;
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new DcMechanic[0];
            return card;
        }

        private static int EntryOf(DreamcatcherCycleDeck deck, string cardId)
        {
            foreach (var e in deck.Hand(64))
                if (e.card != null && e.card.id == cardId) return e.entryId;
            Assert.Fail($"'{cardId}' not in queue");
            return -1;
        }

        private static void SetField(object obj, string name, object value)
            => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);

        private static void InvokeOnDefenderDied(DreamcatcherHandController ctrl, Entity host, DefenderUnitData data)
            => typeof(DreamcatcherHandController)
                .GetMethod("OnDefenderDied", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ctrl, new object[] { host, data, Vector3.zero });

        // ── dreamcatcher-content-4 unit 5 (퇴직 위로금) ───────────────────────
        //
        // 이 카드의 load-bearing 계약은 **퇴근 ≠ 사망의 교차 무발동**이다. 아래 3건이 그 양방향을
        // 고정한다 — 하나라도 빠지면 "퇴근에서만 터진다"가 검증되지 않는다:
        //   ① OnRetire 카드 + 퇴근 = 비워진 칸에 운석이 떨어진다
        //   ② OnRetire 카드 + 사망 = 운석이 없다
        //   ③ OnDeath 카드 + 퇴근 = 폭발이 없다 (위 스위트의 "퇴근은 사망의 결과를 하나도
        //      일으키지 않는다" 단정의 payload 판 — DefenderDied 카운트가 아니라 실제 피해로)
        //
        // host 를 **힐러**로 고른 이유: 공격 출력이 힐(45)뿐이라 host 자신은 더미를 때리지 않는다.
        //
        // ⚠ 다만 «더미의 유일한 데미지 소스가 운석» 은 **거짓이다**(2026-08-16 실측). 대조군
        // (카드 0 · 퇴근 0 · 사망 0)으로 재보니 인접 더미가 **20씩 약 1.5초 주기로** 깎였고,
        // 12칸 밖 더미는 0 이었다. 판 위 방어유닛은 힐러 하나뿐이고 힐러의 attackCooldown 은
        // 3.15초라 host 의 공격도 아니다 — StartBattle 이 돌린 **라이브 웨이브가 만드는 주변
        // 피해**이며 이 feature 와 무관하다.
        //
        // 그래서 단정을 «체력이 정확히 그대로» 에서 **«운석 한 발만큼 빠졌나/안 빠졌나»** 로
        // 바꾼다. 주변 피해(수십)와 운석(137)은 자릿수가 달라 판별이 흐려지지 않는다.
        // 절대값 단정으로 되돌리지 말 것 — 웨이브 구성이 바뀔 때마다 깨진다.

        private const float MeteorDamage = 137f;   // 다른 어떤 소스와도 겹치지 않는 값
        private const float DummyHp = 5000f;
        private const float MeteorWarnSec = 0.25f; // 낙하 예고. 에셋(0.8초)보다 짧게 — 테스트 시간

        [UnityTest]
        public IEnumerator Retire_WithOnRetireCard_DropsMeteorOnVacatedCell()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            yield return PlaceHealerWithCard(bridge, MakeRetireMeteorCard());
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var host = EntityAt(bridge, em, cell);
            var dummy = MakeEnemyDummy(em,
                em.GetComponentData<LocalTransform>(host).Position + new float3(TileSize(bridge), 0f, 0f));

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            Assert.IsTrue(bridge.RetireDefender(cell), "retire");
            yield return RunSeconds(MeteorWarnSec + 1.5f);

            float drop = DummyHp - em.GetComponentData<Health>(dummy).value;
            Assert.GreaterOrEqual(drop, MeteorDamage,
                "퇴근한 칸에 운석이 떨어져 인접 더미를 카드가 가진 flat 피해만큼 깎는다");
            Assert.Less(drop, MeteorDamage * 2f,
                "운석은 한 발이다 — 두 발 몫이 빠졌으면 슬롯이 중복 발동한 것이다");

            // ⚠ 위 피해 단언은 legacy arm 이 쏴도 똑같이 초록이다(skill-layer-migration
            // unit 3e). 퇴근은 **시뮬 밖 생산자**라 이벤트가 자기 seam 을 말해야 하고,
            // 그게 틀리면 프레임 첫 seam 이 집어가 「시전자 생존」 가드에 조용히 걸린다 —
            // 그 상태를 잡을 수 있는 단언은 이것뿐이다.
            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Lifecycle), 1,
                "퇴근 운석이 스킬 레이어를 안 거쳤다 — 라우팅이 조용히 죽었다");
        }

        [UnityTest]
        public IEnumerator Death_WithOnRetireCard_DropsNoMeteor()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            yield return PlaceHealerWithCard(bridge, MakeRetireMeteorCard());
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var host = EntityAt(bridge, em, cell);
            var dummy = MakeEnemyDummy(em,
                em.GetComponentData<LocalTransform>(host).Position + new float3(TileSize(bridge), 0f, 0f));

            // 같은 유닛을 **죽인다**. 여기서 운석이 떨어지면 두 사건이 한 채널을 공유한다는 뜻이다.
            var hp = em.GetComponentData<Health>(host);
            em.SetComponentData(host, new Health { value = 0f, max = hp.max });
            for (int i = 0; i < 180 && em.Exists(host); i++) yield return null;
            Assert.IsFalse(em.Exists(host), "대조 전제: 실제로 죽어서 사라졌다");

            yield return RunSeconds(MeteorWarnSec + 1.5f); // 떨어졌다면 이미 착탄했을 시간
            Assert.Less(DummyHp - em.GetComponentData<Health>(dummy).value, MeteorDamage,
                "사망은 OnRetire 를 발동시키지 않는다 (퇴근 전용 사건)");
        }

        [UnityTest]
        public IEnumerator Retire_WithOnDeathCard_DoesNotExplode()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            yield return PlaceHealerWithCard(bridge, MakeFarewellCard());
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var host = EntityAt(bridge, em, cell);
            var dummy = MakeEnemyDummy(em,
                em.GetComponentData<LocalTransform>(host).Position + new float3(TileSize(bridge), 0f, 0f));

            Assert.IsTrue(bridge.RetireDefender(cell), "retire");
            yield return RunSeconds(1.5f);
            Assert.Less(DummyHp - em.GetComponentData<Health>(dummy).value, MeteorDamage,
                "퇴근은 작별 선물(OnDeath 폭발)을 일으키지 않는다 — 역방향 무발동");
        }

        // 힐러 1기 배치 + 카드 부착 + StartBattle 까지. 세 케이스가 완전히 같은 무대를 쓴다.
        private static IEnumerator PlaceHealerWithCard(BattleBridge bridge, DreamcatcherCard card)
        {
            var gm = Object.FindObjectOfType<GameManager>();
            var healer = FindCatalog().ById("healer");
            Assert.IsNotNull(healer, "healer defender data (공격 출력 없음 = 더미의 유일 소스가 운석)");

            bridge.SetDefenderPool(new[] { healer });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, healer), "place healer");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = EntityAt(bridge, em, SoleCell(bridge));
            Assert.AreNotEqual(Entity.Null, host, "host resolved");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(host, card), 0,
                "카드 부착 (bake 통과)");

            bridge.StartBattle(); // 투사체 sim 이 도는 상태에서 검증한다
            yield return null;
        }

        private static Entity MakeEnemyDummy(EntityManager em, float3 pos)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = DummyHp, max = DummyHp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(e);
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<Wassup.Battle.Effects.CcEffect>(e);
            em.AddBuffer<Wassup.Battle.Effects.StackModifierSlot>(e);
            return e;
        }

        private static DreamcatcherCard MakeRetireMeteorCard() => MakeSelfTileAoeCard(
            "test_severance_meteor", DcTriggerKind.OnRetire, MeteorWarnSec);

        // 대조군 — 작별 선물(사망 폭발). 예고 없이 즉시 착탄(기존 카드는 전부 duration 0).
        private static DreamcatcherCard MakeFarewellCard() => MakeSelfTileAoeCard(
            "test_farewell", DcTriggerKind.OnDeath, 0f);

        private static DreamcatcherCard MakeSelfTileAoeCard(string id, DcTriggerKind trigger, float warnSec)
        {
            var view = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath("1705ed345dda4014bc5b6019f1a84e77"));
            Assert.IsNotNull(view, "AOE-view ProjectileData (Projectile_Meteor)");

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = id;
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = trigger },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfTileAoe,
                    magnitude = MeteorDamage,
                    tileRange = 1,
                    duration = warnSec, // SelfTileAoe 의 duration = 낙하 예고 초 (content-4 계약 8)
                    projectile = view,
                },
            }};
            return card;
        }

        // 이름을 붙여 단언한다 — 필드가 개명되면 NRE 대신 이유가 뜬다(BattleBridgeTestAccess 규약).
        private static float TileSize(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("tileSize", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "BattleBridge.tileSize 를 찾지 못했다(이름 변경?)");
            return (float)f.GetValue(bridge);
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
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
