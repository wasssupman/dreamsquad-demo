using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — OnPlaceEffectType.ReduceSkillCooldown (레인저) 특성화.
    //
    // 이 arm 은 ISkill 이전 대상이고, 지금까지 PlayMode 커버리지가 없었다. 이 파일은
    // **이전하기 전의 동작을 박제**한다 — 새 동작을 정의하는 것이 아니다.
    //
    // ⚠ 이 arm 은 **ECS 를 만지지 않는다** — 산출물은 Mono 쪽 SkillRuntime 의 쿨다운
    // 잔여 시간 변화다. 그래서 단언 대상도 「쿨다운이 실제로 줄었다」(GetRemainingSeconds)다.
    //
    // 쿨다운이 안 돌고 있으면 관측할 것이 없다 — 그래서 배치 **전에** Consume 으로 쿨다운을
    // 돌려 놓는다. Consume→배치→단언 사이에 yield 가 없으므로 SkillRuntime 의 프레임 틱이
    // 측정에 끼어들지 못하고, 델타는 순수하게 이 arm 의 산출이다.
    public class OnPlaceReduceSkillCooldownTest
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

        // 도는 쿨다운은 저작량만큼 줄고, 저작량보다 짧게 남은 쿨다운은 0 에서 잘려
        // 즉시 준비 상태가 된다(전 스킬 일괄 — arm 은 ReduceAllCooldowns 하나로 끝난다).
        [UnityTest]
        public IEnumerator ReduceCooldown_ShortensRunningCooldowns_AndFloorsAtReady()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var ranger = MakeRanger("test_cd_reduce");
            // 리터럴을 못 박지 않는다 — 감소량은 SO 가 권위다. 테스트 스킬의 쿨다운도
            // 감소량에 대한 비율로 만들어 저작이 바뀌어도 등식이 유지되게 한다.
            float mag = ranger.GetAbility<UnitSkillAbility>().mechanics[0].payload.magnitude;
            Assert.Greater(mag, 0f, "감소량이 저작돼 있어야 이 단언이 의미를 갖는다");

            // 브리지에 배선된 SkillRuntime — arm 이 실제로 만지는 그 인스턴스를 잰다.
            var sr = (SkillRuntime)BattleBridgeTestAccess.Field(bridge, "skillRuntime");
            Assert.IsNotNull(sr, "BattleBridge.skillRuntime 이 씬에 배선돼 있어야 한다");

            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            var cell = FindPlaceableCell(bridge, ranger);

            // 감소량보다 길게 남는 스킬(잔여 관측용)과 짧게 남는 스킬(바닥 클램프 관측용).
            var skillLong = MakeSkill("test_cd_long", mag * 5f);
            var skillShort = MakeSkill("test_cd_short", mag * 0.5f);

            // ⚠ **프레임 틱이 이 측정에 섞인다.** 예전엔 여기부터 yield 를 금지해 격리했지만,
            // skill-layer-migration unit 2c 이후 규칙 경로는 배치 **다음 틱**에 적용되므로
            // 프레임을 흘릴 수밖에 없다. 그래서 격리 대신 **오차로 명시**한다 — 흘리는
            // 프레임의 자연 감소(수십 ms)는 저작량(초 단위)과 자릿수가 달라 단언이 살아 있다.
            sr.Consume(skillLong);
            sr.Consume(skillShort);
            float beforeLong = sr.GetRemainingSeconds(skillLong);
            Assert.AreEqual(skillLong.cooldownSec, beforeLong, 0.001f, "Consume 이 쿨다운을 돌려 놓았다(전제)");
            Assert.IsFalse(sr.IsReady(skillShort), "짧은 스킬도 배치 전에는 쿨다운 중이다(전제)");

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, ranger), "배치");
            for (int f = 0; f < 4; f++) yield return null;   // 규칙 경로는 다음 틱에 적용된다

            float afterLong = sr.GetRemainingSeconds(skillLong);
            bool shortReady = sr.IsReady(skillShort);
            float shortRemain = sr.GetRemainingSeconds(skillShort);

            Object.Destroy(skillLong);
            Object.Destroy(skillShort);
            Object.Destroy(ranger);

            // 오차 0.25s = 흘린 프레임의 자연 감소 여유. 저작량 2s 와 자릿수가 달라
            // 「줄긴 줄었는데 저작량이 아니다」를 여전히 잡는다.
            Assert.AreEqual(mag, beforeLong - afterLong, 0.25f,
                $"배치 순간 도는 쿨다운이 저작량({mag}s)만큼 줄어야 한다");
            Assert.IsTrue(shortReady,
                "감소량보다 짧게 남은 쿨다운은 0 에서 잘려 즉시 준비 상태가 돼야 한다");
            Assert.AreEqual(0f, shortRemain, 0.001f, "준비 상태의 잔여 시간은 0 이다");
            // ⚠ 바닥 클램프 단언 둘은 오차를 안 넓힌다 — 자연 감소는 «더 줄이는» 방향이라
            // 0 을 넘어 음수로 가지 않는 한 이 단언을 느슨하게 만들지 않는다.
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static SkillData MakeSkill(string id, float cooldownSec)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.id = id;
            s.cooldownSec = cooldownSec;
            return s;
        }

        private static DefenderUnitData MakeRanger(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("ranger"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            // skill-layer-migration unit 2c — 레거시 flat 필드에서 규칙 저작으로 이사했다.
            Assert.AreEqual(OnPlaceEffectType.None, unit.onPlaceEffect,
                "레거시 배치 필드가 아직 켜져 있다 — 두 경로가 동시에 돈다");
            Assert.AreEqual(DcPayloadKind.ReduceSkillCooldown,
                unit.GetAbility<UnitSkillAbility>().mechanics[0].payload.kind,
                "레인저의 배치 효과가 ReduceSkillCooldown 이어야 이 특성화가 성립한다");
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
