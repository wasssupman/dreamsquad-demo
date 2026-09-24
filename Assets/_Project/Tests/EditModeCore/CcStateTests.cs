using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 군중 제어 슬롯.
    [TestFixture]
    public class CcStateTests
    {
        private static SimEntityId Src => new SimEntityId(1);

        [Test]
        public void 런타임_슬롯은_넉백_기절_수면_셋뿐이다()
        {
            // 저작 어휘(`CcRequestKind`)에는 감속과 지속 피해도 있지만 그 둘은 **다른
            // 파이프라인**으로 간다 — 감속은 이동속도 모디파이어, 지속 피해는 `DotSet`.
            Assert.AreEqual(3, System.Enum.GetValues(typeof(CcSlotKind)).Length,
                "슬롯을 늘리는 것은 규칙 변경이다");
            Assert.AreEqual(5, System.Enum.GetValues(typeof(CcRequestKind)).Length,
                "저작 어휘는 None·Stun·Sleep·Slow·Impulse 다섯이다");
        }

        [Test]
        public void 종류당_슬롯_하나이고_시간은_긴_쪽이다()
        {
            var cc = new CcState();
            Assert.IsTrue(cc.Apply(CcSlotKind.Stun, 2f, float3.zero, Src));
            Assert.IsFalse(cc.Apply(CcSlotKind.Stun, 1f, float3.zero, Src), "두 번째는 갱신이다");
            Assert.AreEqual(2f, cc.Slot(CcSlotKind.Stun).Remaining, 1e-4f, "짧은 쪽이 덮지 않는다");

            cc.Apply(CcSlotKind.Stun, 5f, float3.zero, Src);
            Assert.AreEqual(5f, cc.Slot(CcSlotKind.Stun).Remaining, 1e-4f);
        }

        [Test]
        public void 벡터는_들어온_값으로_갱신된다()
        {
            var cc = new CcState();
            cc.Apply(CcSlotKind.Impulse, 1f, new float3(1f, 0f, 0f), Src);
            cc.Apply(CcSlotKind.Impulse, 0.5f, new float3(0f, 0f, 2f), Src);
            Assert.AreEqual(2f, cc.Slot(CcSlotKind.Impulse).Vector.z, 1e-4f);
            Assert.AreEqual(1f, cc.Slot(CcSlotKind.Impulse).Remaining, 1e-4f, "시간만 긴 쪽이다");
        }

        [Test]
        public void 주기가_바뀌면_진행률을_새_주기로_비례_환산한다()
        {
            // F6 — 큰 주기에서 쌓인 타이머가 작은 주기로 그대로 넘어가면 **조기 발동**한다.
            // ⚠ 오늘 이 환산의 소비자는 `DotSet` 하나다(런타임 군중 제어 슬롯에는 주기가
            // 없다 — 지속 피해가 자기 버퍼로 빠지면서 그 축이 이쪽에서 사라졌다).
            Assert.AreEqual(0.25f, CcMerge.CarryTimer(0.5f, 1f, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, CcMerge.CarryTimer(0.5f, 0f, 0.5f), 1e-4f, "연속이면 환산할 진행률이 없다");
            Assert.AreEqual(0.5f, CcMerge.CarryTimer(0.5f, 1f, 1f), 1e-4f);
        }

        [Test]
        public void 잠금은_기절과_수면뿐이고_넉백은_아니다()
        {
            var cc = new CcState();
            Assert.IsFalse(cc.IsLocked);

            cc.Apply(CcSlotKind.Impulse, 1f, new float3(1f, 0f, 0f), Src);
            Assert.IsFalse(cc.IsLocked, "밀리는 중에도 때린다");
            Assert.IsTrue(cc.Any, "그래도 「군중 제어에 걸린 적」이긴 하다");

            cc.Apply(CcSlotKind.Sleep, 1f, float3.zero, Src);
            Assert.IsTrue(cc.IsLocked);
        }

        [Test]
        public void 넉백은_초당_속도라_변위가_dt_에_비례한다()
        {
            var cc = new CcState();
            cc.Apply(CcSlotKind.Impulse, 0.5f, new float3(6f, 0f, 0f), Src);
            Assert.AreEqual(0.1f, cc.ImpulseStep(1f / 60f).x, 1e-4f);

            cc.Clear(CcSlotKind.Impulse);
            Assert.AreEqual(0f, cc.ImpulseStep(1f / 60f).x, 1e-6f);
        }

        [Test]
        public void 감쇠는_만료된_종류를_비트로_알린다()
        {
            var cc = new CcState();
            cc.Apply(CcSlotKind.Stun, 0.1f, float3.zero, Src);
            cc.Apply(CcSlotKind.Sleep, 1f, float3.zero, Src);

            Assert.AreEqual(0, cc.Decay(0.05f));
            Assert.AreEqual(1 << (int)CcSlotKind.Stun, cc.Decay(0.05f));
            Assert.IsFalse(cc.IsActive(CcSlotKind.Stun));
            Assert.IsTrue(cc.IsActive(CcSlotKind.Sleep));
        }

        [Test]
        public void 무한_슬롯은_감쇠를_자연_통과한다()
        {
            var cc = new CcState();
            cc.Apply(CcSlotKind.Sleep, float.PositiveInfinity, float3.zero, Src);
            for (int i = 0; i < 600; i++) cc.Decay(1f / 60f);
            Assert.IsTrue(cc.IsActive(CcSlotKind.Sleep));
        }

        // ── 자격(F3) ─────────────────────────────────────────────────────────

        [Test]
        public void 거점은_상태이상과_모디파이어에_전면_면역이다()
        {
            // 옛 전투는 이 규칙이 진입 가드 셋에 흩어져 있어 새 효과마다 넷째 구멍이 열렸다.
            var structure = new Unit { Kind = UnitKind.Structure };
            Assert.IsFalse(EffectEligibility.AcceptsCc(structure, CcRequestKind.Slow), "거점은 종류 불문");
            Assert.IsFalse(EffectEligibility.AcceptsModifier(structure));
            Assert.IsTrue(EffectEligibility.AcceptsHeal(structure), "회복만 열려 있다");
        }

        [Test]
        public void 보스는_행동불능_면역이지만_버프_디버프는_받는다()
        {
            var boss = new Unit { Kind = UnitKind.Enemy, Attack = new AttackState { BossImmune = true } };
            Assert.IsFalse(EffectEligibility.AcceptsCc(boss, CcRequestKind.Stun));
            Assert.IsTrue(EffectEligibility.AcceptsModifier(boss),
                "버프·디버프까지 막으면 가호가 보스에 안 걸린다");
        }

        [Test]
        public void 보스_면역은_기절_수면_넉백만_막고_감속은_받는다()
        {
            // 2026-09-24 드리프트 감사 M6 — 옛 `CcActionLock.IsBossImmune(kind) = IsLock(kind) ||
            // kind == Impulse`. 종류 축을 잃은 술어가 **감속까지** 막아 보스에 둔화 카드가 안 걸렸다.
            var def = CoreCombatFixtures.Definition();
            def.Enemies[0].Attack.BossImmune = true;
            def.ConfigHash = def.ComputeConfigHash();
            var m = new BattleMatch(def);
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var boss = CoreCombatFixtures.First(m, UnitKind.Enemy);

            Assert.IsTrue(m.World.RequestCc(CcRequest.Of(boss.Id, CcRequestKind.Slow, 1f, Src)),
                "보스가 감속을 거절했다 — 면역은 행동 잠금·넉백 축뿐이다");
            Assert.IsFalse(m.World.RequestCc(CcRequest.Of(boss.Id, CcRequestKind.Stun, 1f, Src)));
            Assert.IsFalse(m.World.RequestCc(CcRequest.Of(boss.Id, CcRequestKind.Sleep, 1f, Src)));
            Assert.IsFalse(m.World.RequestCc(CcRequest.Push(boss.Id, new float3(1f, 0f, 0f), 0.2f, Src)));
        }

        [Test]
        public void 이미_사라진_대상에_거는_것은_요청이_아니라_사고다()
        {
            Assert.IsFalse(EffectEligibility.AcceptsCc(null, CcRequestKind.Slow));
            Assert.IsFalse(EffectEligibility.AcceptsModifier(null));
        }
    }
}
