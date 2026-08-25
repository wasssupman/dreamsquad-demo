using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — OnPlaceEffectType.BoostNearbyDefenders (가디언) 특성화.
    //
    // 이 arm 은 ISkill 이전 대상이고, 지금까지 PlayMode 커버리지가 없었다. 이 파일은
    // **이전하기 전의 동작을 박제**한다 — 새 동작을 정의하는 것이 아니다.
    //
    // ⚠ 단언 대상은 「모디파이어 슬롯이 붙었다」가 아니라 **「실효 공격력이 올랐다」**다.
    // AttackSystem 은 매 타격에 `ModifierStats.damageMul` 을 곱한다 — 그 집계 결과가
    // 곧 화면의 공격력이다. 슬롯이 있어도 집계가 안 되면(예: ModifierStats 부재로
    // AggregateSystem 쿼리에서 빠짐) 화면에선 아무 일도 없으므로, 집계 결과를 본다.
    public class OnPlaceBoostNearbyTest
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

        // 반경 경계 안(cheb == tileRange) 아군의 실효 공격력은 저작 배율만큼 오르고,
        // 경계 바로 밖(cheb == tileRange+1) 아군은 오르지 않는다. 자기 자신도 버프를 받는다
        // (PHASE4 §4 자율 결정 — self-inclusion 이 현행 사양이다. 이전 후 self 가 빠지면
        // 여기서 빨개져야 한다).
        [UnityTest]
        public IEnumerator Boost_RaisesEffectiveDamage_InRangeAndSelf_ButNotOutside()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var booster = MakeBooster("test_boost_src");
            // 리터럴을 못 박지 않는다 — 배율은 SO 가 권위다. 여기서 재는 것은
            // 「저작된 배율이 반경 안에만 실효로 들어간다」이지 그 값이 1.3 이라는 사실이 아니다.
            // ⚠ 저작이 **배율에서 퍼센트로** 바뀌었다(1.3 → 30). 그 환산은 concrete 소유이고
            // 여기서는 기대 배율만 복원한다 — 리터럴로 못박지 않는다.
            var boostSpec = booster.GetAbility<UnitSkillAbility>().mechanics[0].payload;
            float mag = 1f + boostSpec.magnitude / 100f;
            Assert.Greater(mag, 1f, "버프(>1 배율)가 저작돼 있어야 이 단언이 의미를 갖는다");

            // 반경 아군은 헬퍼 2기 — on-place 를 꺼서(None) 헬퍼 자신의 배치 효과가
            // 측정에 섞이지 않게 한다.
            var allyIn = MakeNeutralAlly("test_boost_ally_in");
            var allyOut = MakeNeutralAlly("test_boost_ally_out");

            bridge.SetDefenderPool(new[] { booster, allyIn, allyOut });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            // 경계를 정확히 가른다: arm 은 GridMath.RangeToTiles + Chebyshev ≤ tileRange 로
            // 판정하므로, 안쪽 아군은 정확히 경계 위(== R)에, 바깥 아군은 첫 이탈 칸(== R+1)에
            // 세운다 — 반경 해석이 이전 중에 미끄러지면 가장 먼저 여기가 갈라진다.
            int tileRange = boostSpec.tileRange;
            Assert.Greater(tileRange, 0, "반경이 저작돼 있어야 경계 단언이 선다");
            var origin = FindTripleCells(bridge, booster, tileRange, out var inCell, out var outCell);

            Assert.IsTrue(bridge.PlaceDefenderAs(inCell.x, inCell.y, allyIn), "반경 안 아군 배치");
            Assert.IsTrue(bridge.PlaceDefenderAs(outCell.x, outCell.y, allyOut), "반경 밖 아군 배치");
            Assert.IsTrue(bridge.TryGetDefenderAt(inCell, out var inEntity), "반경 안 아군 엔티티");
            Assert.IsTrue(bridge.TryGetDefenderAt(outCell, out var outEntity), "반경 밖 아군 엔티티");

            // 사전 상태 고정 — 이미 1 이 아니면(다른 효과 오염) 아래 단언이 거짓 증언이 된다.
            Assert.AreEqual(1f, DamageMul(em, inEntity), 0.001f, "배치 직후 아군 배율은 1(오염 가드)");
            Assert.AreEqual(1f, DamageMul(em, outEntity), 0.001f, "배치 직후 아군 배율은 1(오염 가드)");

            Assert.IsTrue(bridge.PlaceDefenderAs(origin.x, origin.y, booster), "가디언 배치");
            Assert.IsTrue(bridge.TryGetDefenderAt(origin, out var selfEntity), "가디언 엔티티");

            // 모디파이어는 큐 → ApplySystem → AggregateSystem 경로라 반영까지 프레임이 필요하다.
            yield return Frames(6);

            float inMul = DamageMul(em, inEntity);
            float outMul = DamageMul(em, outEntity);
            float selfMul = DamageMul(em, selfEntity);

            Object.Destroy(booster);
            Object.Destroy(allyIn);
            Object.Destroy(allyOut);

            // 단일 버프는 authoring 정책(증가 = 가산 버킷)과 무관하게 저작 배율이 그대로
            // 실효값이 된다: (1 + (m-1)) == m. 이전 후 이 등식이 깨지면 화면 공격력이 달라진 것.
            Assert.AreEqual(mag, inMul, 0.01f, "반경 안 아군의 실효 공격력 배율이 저작 배율만큼 올라야 한다");
            Assert.AreEqual(1f, outMul, 0.01f, "반경 밖(경계+1) 아군이 버프를 받았다 — 반경이 넓어졌다");
            Assert.AreEqual(mag, selfMul, 0.01f, "자기 자신 포함이 현행 사양이다(PHASE4 §4)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static float DamageMul(EntityManager em, Entity e)
            => em.HasComponent<ModifierStats>(e) ? em.GetComponentData<ModifierStats>(e).damageMul : 1f;

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static DefenderUnitData MakeBooster(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("guardian"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            // unit 2g — 「레거시 배치 필드가 꺼져 있다」 단언은 은퇴했다.
            // 그 필드군 자체가 철거돼 켤 방법이 없다.
            var skill = unit.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(skill, "가디언에 배치 스킬(UnitSkillAbility)이 배선돼야 한다");
            Assert.AreEqual(DcPayloadKind.AllyStatAura, skill.mechanics[0].payload.kind,
                "페이로드 = 아군 스탯 오라");
            Assert.AreEqual(CardBuffKind.AttackDamage, skill.mechanics[0].payload.buffStat,
                "가디언 오라는 공격력이다");
            return unit;
        }

        // 배치 효과가 없는 아군 — 헬퍼의 on-place 가 측정에 끼어들지 않게 한다.
        private static DefenderUnitData MakeNeutralAlly(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("guardian"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            unit.abilities = new System.Collections.Generic.List<DefenderAbilityData>();
            return unit;
        }

        // 배치 가능한 원점 + 경계 위(cheb == tileRange) + 경계 밖(cheb == tileRange+1)
        // 배치 가능 칸 세 개를 함께 고른다. 세 자리 모두 타일 정수 좌표라 거리 판정이
        // 반올림 없이 정확하다(적 대상 arm 들과 달리 월드 좌표 변환이 끼지 않는다).
        private static Vector2Int FindTripleCells(
            BattleBridge bridge, DefenderUnitData u, int tileRange,
            out Vector2Int inCell, out Vector2Int outCell)
        {
            int r = tileRange + 1;
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    Vector2Int? cin = null, cout = null;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (cheb != tileRange && cheb != tileRange + 1) continue;
                            if (!bridge.CanPlaceDefenderAt(x + dx, y + dy, u, out _)) continue;
                            if (cin == null && cheb == tileRange) cin = new Vector2Int(x + dx, y + dy);
                            else if (cout == null && cheb == tileRange + 1) cout = new Vector2Int(x + dx, y + dy);
                        }
                    if (cin != null && cout != null)
                    {
                        inCell = cin.Value; outCell = cout.Value;
                        return new Vector2Int(x, y);
                    }
                }
            Assert.Fail("경계 안팎 배치 가능 칸을 가진 원점이 없다");
            inCell = default; outCell = default;
            return default;
        }
    }
}
