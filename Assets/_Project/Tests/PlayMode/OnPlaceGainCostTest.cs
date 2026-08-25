using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — OnPlaceEffectType.GainCost (스카우트) 특성화.
    //
    // 이 arm 은 ISkill 이전 대상이고, 지금까지 PlayMode 커버리지가 없었다. 이 파일은
    // **이전하기 전의 동작을 박제**한다 — 새 동작을 정의하는 것이 아니다.
    //
    // ⚠ 이 arm 은 **ECS 를 만지지 않는다** — 산출물은 Mono 쪽 CostRuntime 의 잔량 변화다.
    // 그래서 단언 대상도 엔티티/컴포넌트가 아니라 「코스트가 실제로 늘었다」(CostRuntime.Current)다.
    //
    // 측정 격리: 배치→단언 사이에 yield 가 없다 — 코스트 회복(regen)은 Update 틱이라
    // 프레임을 넘기지 않으면 끼어들 수 없고, PlaceDefenderAs 경로는 배치 비용을 차감하지
    // 않는다(차감은 D&D 배치 경로 TryBeginDefenderDeployment 소관). 따라서 전후 델타는
    // 순수하게 이 arm 의 산출이다. 그래도 클론 cost 는 0 으로 둬 경로가 바뀌어도 안전하다.
    public class OnPlaceGainCostTest
    {
        // duel-live-focus — 이 계측은 자기 판을 선언한다(라이브 풀이 바뀌어도 같은 판에서 잰다).
        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        // 배치 순간 코스트가 저작량만큼 실제로 늘어난다.
        [UnityTest]
        public IEnumerator GainCost_AddsAuthoredAmount_OnPlacement()
        {
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var scout = MakeScout("test_gaincost_add");
            // 리터럴을 못 박지 않는다 — 저작량은 SO 가 권위다. arm 은 RoundToInt 로 소비하므로
            // 기대값도 같은 변환을 태운다(소수 저작이 생겨도 등식이 유지된다).
            int gain = Mathf.RoundToInt(scout.GetAbility<UnitSkillAbility>().mechanics[0].payload.magnitude);
            Assert.GreaterOrEqual(gain, 1, "코스트 저작이 있어야 이 단언이 의미를 갖는다");

            bridge.SetDefenderPool(new[] { scout });
            bridge.BeginPlacement();
            var cost = gm.CostRuntime;
            cost.ResetToStart();
            // 우물을 비워 상한 클램프가 이 측정에 끼지 못하게 한다(클램프는 아래 테스트가 잰다).
            cost.TrySpend(cost.CurrentInt);
            Assert.LessOrEqual(cost.Current + gain, cost.Max, "상한 여유(클램프 미개입) 전제");

            // ⚠ **자연 회복을 멈춘다.** 아래에서 프레임을 흘리므로 안 멈추면 회복분이
            // 측정에 섞인다(레거시는 배치 호출 «안»에서 동기 적용이라 프레임이 필요 없었다).
            cost.StopRegen();

            var cell = FindPlaceableCell(bridge, scout);
            float before = cost.Current;
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, scout), "배치");
            // skill-layer-migration unit 2c — 규칙 경로는 배치 **다음 틱**에 적용된다.
            // 계약은 「배치하면 코스트가 들어온다」이지 「반환 전에 들어온다」가 아니다.
            for (int f = 0; f < 4; f++) yield return null;
            float after = cost.Current;
            Object.Destroy(scout);

            Assert.AreEqual(gain, after - before, 0.001f,
                $"배치 순간 코스트가 저작량({gain})만큼 늘어야 한다");
        }

        // 우물이 가득 차 있으면 넘치지 않는다 — 획득이 상한에서 잘리는 것도 현행 동작이다.
        // (arm 의 반환값 affected 도 실제 증가분을 따르지만, 외부 관측 가능한 결과는 잔량이다.)
        [UnityTest]
        public IEnumerator GainCost_DoesNotOverflow_WhenWellIsFull()
        {
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var scout = MakeScout("test_gaincost_clamp");
            bridge.SetDefenderPool(new[] { scout });
            bridge.BeginPlacement();
            var cost = gm.CostRuntime;
            cost.ResetToStart();
            cost.AddCost(100000);
            Assert.AreEqual(cost.Max, cost.Current, 0.001f, "우물 가득 채움 전제");

            var cell = FindPlaceableCell(bridge, scout);
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, scout), "배치");
            for (int f = 0; f < 4; f++) yield return null;   // 규칙 경로는 다음 틱에 적용된다
            float after = cost.Current;
            Object.Destroy(scout);

            Assert.AreEqual(cost.Max, after, 0.001f, "가득 찬 우물 위에서 코스트가 상한을 넘었다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
        }

        private static DefenderUnitData MakeScout(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("scout"));
            unit.id = testId;
            unit.cost = 0;   // 배치 비용이 측정에 끼지 않게 — 델타 = 순수 GainCost 산출
            unit.maxOnBoard = 100;
            // unit 2g — 「레거시 배치 필드가 꺼져 있다」 단언은 은퇴했다.
            // 그 필드군 자체가 철거돼 켤 방법이 없다.
            Assert.AreEqual(DcPayloadKind.GainCost,
                unit.GetAbility<UnitSkillAbility>().mechanics[0].payload.kind,
                "스카우트의 배치 효과가 GainCost 여야 이 특성화가 성립한다");
            return unit;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return new Vector2Int(x, y);
            Assert.Fail("배치 가능한 칸이 없다");
            return default;
        }
    }
}
