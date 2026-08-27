using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — GrantShield arm 특성화 (실드셔틀 배치 스킬).
    //
    // 이전(ISkill 이관) 전의 동작을 박제한다. 이 arm 의 계약(BossPeriodicTriggerSystem):
    //  · OnPlace 발화 시 host 반경 tileRange(Chebyshev) 안 **같은 진영** 유닛에게
    //    IncomingShield{source=host, amount=magnitude} 를 append 한다.
    //  · **host 자신은 제외**한다 — ShieldMath 가 source 를 병합 키로 쓰므로 자기 실드
    //    능력과 슬롯을 공유하면 「경계에 생기는 벽」이 「상시 실드」로 붕괴한다(arm 주석).
    //
    // ⚠ 단언은 「실드가 실제로 피해를 흡수한다」까지 간다 — ShieldSlot 이 붙었다/Sum 이
    // 얼마다로 끝내면, 부여는 됐는데 DamageApplication 흡수 경로에 안 물리는 회귀
    // (예: 버퍼 쌍 한쪽 유실)가 초록으로 통과한다.
    //
    // StartBattle 을 **하지 않는다** — 실드 부여는 상태라 드레인이 필요 없고(브리지
    // StartBattle 주석: "배치 페이즈에 놓으면 sim 이 실드를 즉시 붙인다"), 전투를 안
    // 열면 웨이브도 구조물도 없어 판이 완전히 조용하다. 흡수 경로가 전투 없이 도는 것은
    // EnemyShieldTest 가 이미 증명했다.
    public class AbilityAreaShieldTest
    {
        // duel-live-focus — 계측은 자기 판을 선언한다(라이브 풀이 바뀌어도 같은 판에서 잰다).
        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTest]
        public IEnumerator AreaShield_GrantsAuthoredAmountInRadius_HostAndFarExcluded_AndShieldAbsorbs()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var shuttle = MakeShuttle("test_areashield_host");
            var mech = shuttle.GetAbility<UnitSkillAbility>().mechanics[0];
            // 저작값은 하드코딩하지 않는다 — 밸런스 튜닝이 테스트를 깨면 안 된다.
            float authored = mech.payload.magnitude;
            int radius = mech.payload.tileRange;
            Assert.Greater(authored, 0f, "실드량이 저작돼 있어야 이 테스트가 의미를 갖는다");
            Assert.Greater(radius, 0, "GrantShield arm 은 tileRange 0 이면 반경 확산을 아예 안 돈다");

            // 수혜자/대조군 — 배치 스킬·능력을 전부 벗긴 불활성 방어유닛.
            var nearUnit = MakeInert("test_areashield_near");
            var farUnit = MakeInert("test_areashield_far");

            bridge.SetDefenderPool(new[] { shuttle, nearUnit, farUnit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            var hostCell = FindShieldLayout(bridge, shuttle, nearUnit, farUnit, radius,
                out var nearCell, out var farCell);

            // **수혜자 먼저, 셔틀 나중** — GrantShield 는 배치 순간 스냅샷이라, 셔틀보다
            // 늦게 배치된 유닛은 실드를 못 받는다. 순서가 곧 발동 조건이다.
            Assert.IsTrue(bridge.PlaceDefenderAs(nearCell.x, nearCell.y, nearUnit), "근거리 수혜자 배치");
            Assert.IsTrue(bridge.PlaceDefenderAs(farCell.x, farCell.y, farUnit), "원거리 대조군 배치");
            Assert.IsTrue(bridge.PlaceDefenderAs(hostCell.x, hostCell.y, shuttle), "실드셔틀 배치");

            // OnPlace 발화(JustDeployed 소비) 1프레임 + IncomingShield→ShieldSlot 드레인
            // 1프레임이 최소 — 넉넉히 흘린다(실드는 다음 프레임 드레인이 의도된 동작).
            yield return Frames(10);

            Assert.IsTrue(bridge.TryGetDefenderAt(nearCell, out Entity near), "근거리 수혜자 엔티티");
            Assert.IsTrue(bridge.TryGetDefenderAt(farCell, out Entity far), "원거리 대조군 엔티티");
            Assert.IsTrue(bridge.TryGetDefenderAt(hostCell, out Entity host), "셔틀 엔티티");

            Assert.AreEqual(authored, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(near)), 0.01f,
                $"반경 {radius} 안 아군의 실드가 저작값({authored})과 다르다 — 부여가 안 갔거나 양이 샜다");
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(far)), 0.01f,
                $"반경 {radius} 밖 아군이 실드를 받았다 — 반경 게이트가 죽었다");
            // host 제외는 이 kind 의 계약이다(자기 실드 능력과의 슬롯 공유 붕괴 방지).
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(host)), 0.01f,
                "셔틀 자신이 실드를 받았다 — host 제외 계약이 깨졌다(상시 실드 붕괴의 씨앗)");

            // ── 흡수 — 실드가 «장식»이 아니라 실제로 피해를 먼저 받는가 ──
            float chip = authored * 0.4f;
            float hp0 = em.GetComponentData<Health>(near).value;
            em.GetBuffer<IncomingDamage>(near).Add(new IncomingDamage { amount = chip });
            yield return Frames(4);

            Assert.AreEqual(hp0, em.GetComponentData<Health>(near).value, 0.01f,
                "실드가 있는데 체력이 깎였다 — 부여는 됐지만 흡수 경로에 안 물렸다");
            Assert.AreEqual(authored - chip, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(near)), 0.01f,
                $"실드가 {chip} 만큼 깎이지 않았다 — 흡수가 실드 잔량에 반영돼야 한다");

            // ── 관통 — 실드를 넘는 피해는 체력으로 간다(실드가 무적이 아니다) ──
            em.GetBuffer<IncomingDamage>(near).Add(new IncomingDamage { amount = authored });
            yield return Frames(4);
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(near)), 0.01f, "실드 소진");
            Assert.Less(em.GetComponentData<Health>(near).value, hp0,
                "실드 소진 후에도 체력이 그대로다 — 관통분이 사라졌다");

            Object.Destroy(shuttle); Object.Destroy(nearUnit); Object.Destroy(farUnit);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static DefenderUnitData MakeShuttle(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("shield_shuttle"));
            unit.id = testId;
            unit.attackRange = 0f;   // 평타가 섞이면 배치 스킬분을 분리 측정할 수 없다
            unit.cost = 0;
            unit.maxOnBoard = 100;
            // 주기 실드 캐스트(ShieldCastAbility)를 벗긴다 — 같은 host 가 주기적으로
            // 실드를 또 부여하면 «배치 순간 1회 부여량» 을 잴 수 없다. 사본만 고친다.
            unit.abilities.RemoveAll(a => a is ShieldCastAbility);
            var skill = unit.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(skill, "실드셔틀에 UnitSkillAbility(GrantShield 규칙)가 배선돼야 한다");
            Assert.AreEqual(DcTriggerKind.OnPlace, skill.mechanics[0].trigger.kind, "트리거 = 배치");
            Assert.AreEqual(DcPayloadKind.GrantShield, skill.mechanics[0].payload.kind, "페이로드 = 실드 부여");
            return unit;
        }

        // 수혜자/대조군 — 배치 부수효과가 전부 죽은 불활성 유닛(사본만 수정).
        private static DefenderUnitData MakeInert(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("malphite"));
            unit.id = testId;
            unit.attackRange = 0f;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            unit.abilities.Clear();
            return unit;
        }

        // 셔틀 배치 칸 + 반경 안 배치 칸 + 반경 밖 배치 칸을 함께 고른다.
        // 반경 밖은 radius+2 이상 — 경계 한 칸 차이로 게이트를 오판하지 않게 여유를 둔다.
        private static Vector2Int FindShieldLayout(
            BattleBridge bridge, DefenderUnitData host, DefenderUnitData nearU, DefenderUnitData farU,
            int radius, out Vector2Int nearCell, out Vector2Int farCell)
        {
            for (int x = 0; x < 48; x++)
                for (int y = 0; y < 48; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, host, out _)) continue;
                    Vector2Int? n = null, f = null;
                    for (int dx = -radius - 4; dx <= radius + 4; dx++)
                        for (int dy = -radius - 4; dy <= radius + 4; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            int cx = x + dx, cy = y + dy;
                            if (n == null && cheb <= radius
                                && bridge.CanPlaceDefenderAt(cx, cy, nearU, out _))
                                n = new Vector2Int(cx, cy);
                            else if (f == null && cheb >= radius + 2
                                && bridge.CanPlaceDefenderAt(cx, cy, farU, out _))
                                f = new Vector2Int(cx, cy);
                        }
                    if (n != null && f != null)
                    {
                        nearCell = n.Value; farCell = f.Value;
                        return new Vector2Int(x, y);
                    }
                }
            Assert.Fail("반경 안팎 배치 칸을 가진 셔틀 배치 칸이 없다");
            nearCell = default; farCell = default;
            return default;
        }
    }
}
