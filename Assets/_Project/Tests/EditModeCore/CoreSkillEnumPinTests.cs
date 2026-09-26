using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.Skills;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 코어 어휘와 `Wassup.Skills` 미러의 **값 일치**.
    //
    // 어댑터가 캐스트로 번역하는데 **어셈블리가 갈려 컴파일러가 못 잡는다** — 갈리면
    // 컴파일은 통과하고 «이속 버프가 조용히 공격력 버프가 된다». 옛 `SkillModifierKindPinTests`
    // 는 unit 9 에서 옛 전투와 함께 죽으므로, 그 그물을 여기서 다시 친다.
    [TestFixture]
    public class CoreSkillEnumPinTests
    {
        [Test]
        public void 스탯_종류는_값도_개수도_같다()
        {
            Assert.AreEqual((byte)SkillStatKind.DamageMul, (byte)StatKind.DamageMul);
            Assert.AreEqual((byte)SkillStatKind.AttackSpeedMul, (byte)StatKind.AttackSpeedMul);
            Assert.AreEqual((byte)SkillStatKind.DmgTakenMul, (byte)StatKind.DmgTakenMul);
            Assert.AreEqual((byte)SkillStatKind.RegenPerSec, (byte)StatKind.RegenPerSec);
            Assert.AreEqual((byte)SkillStatKind.MoveSpeedMul, (byte)StatKind.MoveSpeedMul);
            Assert.AreEqual((byte)SkillStatKind.DamageVsCcMul, (byte)StatKind.DamageVsCcMul);
            Assert.AreEqual((byte)SkillStatKind.MaxHealthMul, (byte)StatKind.MaxHealthMul);

            Assert.AreEqual(System.Enum.GetValues(typeof(SkillStatKind)).Length,
                            System.Enum.GetValues(typeof(StatKind)).Length,
                            "한쪽에만 스탯이 늘었다");
        }

        [Test]
        public void 결합_연산자는_값이_같고_도메인에만_저작_배율_칸이_하나_더_있다()
        {
            Assert.AreEqual((byte)SkillCombineOp.Multiplicative, (byte)CombineOp.Multiplicative);
            Assert.AreEqual((byte)SkillCombineOp.Additive, (byte)CombineOp.Additive);
            Assert.AreEqual((byte)SkillCombineOp.Override, (byte)CombineOp.Override);

            // `FromAuthoredMultiplier` 는 **도메인 전용**이다 — 「이건 저작 배율이다」까지만
            // 말하고 (버킷, 값) 변환은 어댑터(`ModifierAuthoring`)가 한다. 그 규칙을 도메인에
            // 복제하면 두 벌이 되고, 상한 계산이 버킷 선택에 매여 있어 한쪽만 고치면
            // 조용히 한 스택만큼 어긋난다.
            Assert.AreEqual(3, System.Enum.GetValues(typeof(CombineOp)).Length);
            Assert.AreEqual(4, System.Enum.GetValues(typeof(SkillCombineOp)).Length);
            Assert.AreEqual(3, (byte)SkillCombineOp.FromAuthoredMultiplier,
                "코어에 없는 칸이라 캐스트로 넘어오면 안 된다");
        }

        [Test]
        public void 스택_종류는_값도_개수도_같다()
        {
            Assert.AreEqual((byte)SkillStackKind.None, (byte)StackKind.None);
            Assert.AreEqual((byte)SkillStackKind.Fire, (byte)StackKind.Fire);
            Assert.AreEqual((byte)SkillStackKind.Ice, (byte)StackKind.Ice);
            Assert.AreEqual((byte)SkillStackKind.Bleed, (byte)StackKind.Bleed);
            Assert.AreEqual((byte)SkillStackKind.Poison, (byte)StackKind.Poison);
            Assert.AreEqual((byte)SkillStackKind.Fatigue, (byte)StackKind.Fatigue);

            Assert.AreEqual(System.Enum.GetValues(typeof(SkillStackKind)).Length,
                            System.Enum.GetValues(typeof(StackKind)).Length);
        }

        [Test]
        public void 자리형_궤적_토큰은_코어_궤적_페이로드와_값이_같다()
        {
            // unified-effect-layer unit 1 — 자리형 concrete 가 의도에 명시하는 궤적(부분 미러). 갈리면 운석이 조용히 다른 궤적을 탄다.
            Assert.AreEqual((int)Wassup.BattleCore.Combat.Projectile.MovementKind.SkyFall, SkillProjectileAxis.SkyFall);
            Assert.AreEqual((int)Wassup.BattleCore.Combat.Projectile.PayloadKind.TileAoe, SkillProjectileAxis.TileAoe);
            Assert.AreNotEqual(0, SkillProjectileAxis.SkyFall, "0 = 저작 없음(탄 정의) — 명시로 못 쓴다");
            Assert.AreNotEqual(0, SkillProjectileAxis.TileAoe);
        }

        [Test]
        public void 출처_꼬리표는_미러한_값만_고정한다()
        {
            // 부분 미러다(스킬이 실제로 쓰는 것만) — 나머지는 도메인 밖에서 나온다.
            Assert.AreEqual((byte)SkillModifierOrigin.Unspecified, (byte)ModifierOrigin.Unspecified);
            Assert.AreEqual((byte)SkillModifierOrigin.OnPlace, (byte)ModifierOrigin.OnPlace);
            Assert.AreEqual((byte)SkillModifierOrigin.Skill, (byte)ModifierOrigin.Skill);
            Assert.AreEqual((byte)SkillModifierOrigin.Dreamcatcher, (byte)ModifierOrigin.Dreamcatcher);
            Assert.AreEqual((byte)SkillModifierOrigin.Boss, (byte)ModifierOrigin.Boss);
            Assert.AreEqual((byte)SkillModifierOrigin.HealthThreshold, (byte)ModifierOrigin.HealthThreshold);

            // 3 은 **은퇴 슬롯**(시너지 · 2026-09-03 기능 제거)이다. 지우면 뒤 값이 밀려
            // 캐스트가 통째로 어긋난다 — 번호만 보존한다.
            Assert.AreEqual(3, (byte)ModifierOrigin.Synergy);
        }

        [Test]
        public void 군중_제어_어휘는_값이_다르므로_캐스트로_번역하면_안_된다()
        {
            // ⚠ 여기는 **일치를 요구하지 않는다.** 코어의 런타임 축(`None` 센티널 + 잠금 순)과
            // 도메인의 저작 축(옛 `CcKind` 순)이 서로 다른 질문에 답하기 때문이다.
            // 그래서 이 그물이 고정하는 것은 「같다」가 아니라 **「다르다는 사실」**이다 —
            // 어댑터가 명시 switch 로 번역해야 한다는 계약을 여기서 못박는다.
            Assert.AreEqual(0, (byte)CcRequestKind.None);
            Assert.AreEqual(1, (byte)CcRequestKind.Stun);
            Assert.AreEqual(2, (byte)CcRequestKind.Sleep);
            Assert.AreEqual(3, (byte)CcRequestKind.Slow);
            Assert.AreEqual(4, (byte)CcRequestKind.Impulse);

            Assert.AreEqual(3, (byte)SkillCcKind.Stun);
            Assert.AreEqual(4, (byte)SkillCcKind.Sleep);
            Assert.AreNotEqual((byte)SkillCcKind.Stun, (byte)CcRequestKind.Stun,
                "값이 우연히 맞으면 다음 사람이 캐스트를 쓴다 — 그때 이 단언이 빨개져야 한다");
        }

        [Test]
        public void 런타임_슬롯_종류는_저작_어휘의_부분집합이다()
        {
            // 감속과 지속 피해는 **저작 토큰**이고 런타임 슬롯이 아니다(다른 파이프라인).
            Assert.AreEqual(3, System.Enum.GetValues(typeof(CcSlotKind)).Length);
            Assert.AreEqual(5, System.Enum.GetValues(typeof(CcRequestKind)).Length);
        }
    }
}
