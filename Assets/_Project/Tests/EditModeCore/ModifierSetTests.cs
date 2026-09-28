using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 스탯 슬롯의 병합·회수·만료·접기.
    //
    // 규칙 번호는 `ledgers/rules.md` 효과·스탯 절의 것이다.
    [TestFixture]
    public class ModifierSetTests
    {
        private static SimEntityId Id(int v) => new SimEntityId(v);

        private static ModifierKey Key(int source, StatKind stat, CombineOp op)
            => ModifierKey.Of(Id(source), stat, op);

        // ── 병합 키 4축 ──────────────────────────────────────────────────────

        [Test]
        public void 네_축_중_하나라도_다르면_슬롯이_갈린다()
        {
            var set = new ModifierSet();
            Assert.IsTrue(set.Apply(Key(1, StatKind.MoveSpeedMul, CombineOp.Multiplicative), 0.5f, 5f));
            Assert.IsTrue(set.Apply(Key(2, StatKind.MoveSpeedMul, CombineOp.Multiplicative), 0.5f, 5f),
                "출처가 다르면 새 슬롯");
            Assert.IsTrue(set.Apply(Key(1, StatKind.DamageMul, CombineOp.Multiplicative), 0.5f, 5f),
                "스탯이 다르면 새 슬롯");
            Assert.IsTrue(set.Apply(Key(1, StatKind.MoveSpeedMul, CombineOp.Additive), 0.5f, 5f),
                "연산자가 다르면 새 슬롯");
            Assert.IsTrue(set.Apply(
                new ModifierKey(Id(1), StatKind.MoveSpeedMul, CombineOp.Multiplicative,
                                SlotTag.OfStack(StackKind.Fire)), 0.5f, 5f),
                "칸이 다르면 새 슬롯");

            Assert.AreEqual(5, set.Count);
            Assert.IsFalse(set.Apply(Key(1, StatKind.MoveSpeedMul, CombineOp.Multiplicative), 0.7f, 5f),
                "넷이 모두 같으면 기존 슬롯을 갱신한다");
            Assert.AreEqual(5, set.Count);
        }

        [Test]
        public void 강한_배치_감속이_약한_스택_감속으로_안_깎인다()
        {
            // F26 의 회귀 가드. 옛 전투는 칸 번호가 전역 번호판이라 스택 파생 감속(출처 =
            // 피해자 자신)과 배치 감속(출처 = 대상, 번호 0)이 **4키가 전부 겹쳤고**, 병합이
            // 크기 덮어쓰기라 강한 쪽이 약한 쪽으로 깎였다.
            var victim = Id(7);
            var set = new ModifierSet();

            // 배치 스킬 감속 — 출처는 대상 자신, 일반 칸.
            set.Apply(new ModifierKey(victim, StatKind.MoveSpeedMul, CombineOp.Multiplicative,
                                      SlotTag.Default), 0.4f, 5f);
            // 스택 파생 감속 — 출처도 대상 자신인데 **칸이 다르다**.
            set.Apply(new ModifierKey(victim, StatKind.MoveSpeedMul, CombineOp.Multiplicative,
                                      SlotTag.OfStack(StackKind.Ice)), 0.9f, 5f);

            Assert.AreEqual(2, set.Count, "칸 판별자를 접으면 여기서 1 이 된다");
            Assert.AreEqual(0.36f, set.Effective.MoveSpeedMul, 1e-4f, "둘이 곱으로 겹친다");
        }

        [Test]
        public void 카드는_부착_인스턴스마다_자기_칸을_갖는다()
        {
            var set = new ModifierSet();
            set.Apply(new ModifierKey(Id(1), StatKind.DamageMul, CombineOp.Additive,
                                      SlotTag.OfBinding(11)), 0.2f, 5f);
            set.Apply(new ModifierKey(Id(1), StatKind.DamageMul, CombineOp.Additive,
                                      SlotTag.OfBinding(12)), 0.2f, 5f);
            Assert.AreEqual(2, set.Count, "같은 카드 두 장이 서로를 덮으면 안 된다");
            Assert.AreEqual(1.4f, set.Effective.DamageMul, 1e-4f);
        }

        [Test]
        public void 두_번째_사건이_첫_슬롯을_덮지_않는다()
        {
            // F22 — 순수 C# 리스트에서는 공짜지만, 그 함정이 있었다는 사실이 테스트로 남는다.
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.DamageMul, CombineOp.Additive), 0.1f, 5f);
            set.Apply(Key(2, StatKind.DamageMul, CombineOp.Additive), 0.2f, 5f);
            Assert.AreEqual(2, set.Count);
            Assert.AreEqual(1.3f, set.Effective.DamageMul, 1e-4f);
        }

        // ── 상한 ─────────────────────────────────────────────────────────────

        [Test]
        public void 상한은_배율_빼기_1에_최대_중첩을_곱한_값이다()
        {
            // 가산 버킷에 실리는 값이 «배율 − 1» 이라 상한만 배율 기준으로 재면
            // **조용히 한 스택만큼 어긋난다.**
            Assert.AreEqual(0.4f, ModifierAuthoring.StackCap(1.08f, 5), 1e-4f);
            Assert.AreEqual(0f, ModifierAuthoring.StackCap(1.08f, 0), 1e-4f, "0 = 누적 안 함");
            Assert.AreEqual(0.08f, ModifierAuthoring.StackCap(1.08f, 1), 1e-4f,
                "최대 중첩 1 은 1회분이 곧 상한이다");
            Assert.AreEqual(0f, ModifierAuthoring.StackCap(0.8f, 5), 1e-4f, "깎는 쪽은 누적이 없다");
        }

        [Test]
        public void 상한은_크기만_막고_남은_시간은_안_막는다()
        {
            // 둘을 같이 막으면 **가장 뜨거운 지점에서 버프가 스스로 꺼진다**(광란의 함정).
            ModifierAuthoring.FromMultiplier(1.08f, out var op, out float magnitude);
            float cap = ModifierAuthoring.StackCap(1.08f, 3);

            var set = new ModifierSet();
            var key = Key(1, StatKind.AttackSpeedMul, op);
            for (int i = 0; i < 10; i++) set.Apply(in key, magnitude, 4f, cap);

            Assert.AreEqual(cap, set.Slots[0].Magnitude, 1e-4f, "크기는 상한에서 멈춘다");
            Assert.AreEqual(4f, set.Slots[0].Remaining, 1e-4f, "지속은 매 발동이 갱신한다");
        }

        [Test]
        public void 상한은_신규_슬롯_경로에도_걸린다()
        {
            // F4 — 옛 클램프는 **기존 슬롯 갱신 경로에만** 있어 신규 슬롯 2경로로 샜다.
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.AttackSpeedMul, CombineOp.Additive), 5f, 4f, cap: 0.4f);
            Assert.AreEqual(0.4f, set.Slots[0].Magnitude, 1e-4f);
        }

        // ── 회수 ─────────────────────────────────────────────────────────────

        [Test]
        public void 회수는_항등값_재발행이_아니라_슬롯_삭제다()
        {
            // F27·F28·F33 — 항등 재발행은 ⑴ 강제 고정에 항등이 없고 ⑵ 상한을 실으면
            // 조용히 실패하며 ⑶ 효과 타일은 회수 자체가 없어 개체당 1회로 봉인됐다.
            var set = new ModifierSet();
            var key = new ModifierKey(Id(1), StatKind.DamageMul, CombineOp.Override, SlotTag.Default);
            set.Apply(in key, 3f, 5f);
            Assert.AreEqual(3f, set.Effective.DamageMul, 1e-4f, "강제 고정은 곱·합을 무시한다");

            Assert.IsTrue(set.Revoke(in key));
            Assert.AreEqual(0, set.Count);
            Assert.AreEqual(1f, set.Effective.DamageMul, 1e-4f, "항등이 없는 축도 풀린다");
            Assert.IsFalse(set.Revoke(in key), "두 번 회수는 사건이 아니다");
        }

        [Test]
        public void 칸_단위_회수는_그_칸의_슬롯을_전부_지운다()
        {
            var set = new ModifierSet();
            set.Apply(new ModifierKey(Id(1), StatKind.DamageMul, CombineOp.Additive, SlotTag.OfBinding(3)), 0.2f, 5f);
            set.Apply(new ModifierKey(Id(1), StatKind.MoveSpeedMul, CombineOp.Additive, SlotTag.OfBinding(3)), 0.2f, 5f);
            set.Apply(new ModifierKey(Id(1), StatKind.DamageMul, CombineOp.Additive, SlotTag.OfBinding(4)), 0.2f, 5f);

            Assert.AreEqual(2, set.RevokeTag(SlotKind.BindingInstance, 3));
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(1.2f, set.Effective.DamageMul, 1e-4f);
        }

        // ── 만료 · dirty ─────────────────────────────────────────────────────

        [Test]
        public void 만료도_다시_접으라는_표시를_켠다()
        {
            // F21 — 안 켜서 만료가 영원히 안 돌고 모디파이어가 무한 지속된 이력이 있다.
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.MoveSpeedMul, CombineOp.Multiplicative), 0.5f, 0.1f);
            Assert.AreEqual(0.5f, set.Effective.MoveSpeedMul, 1e-4f);   // 여기서 한 번 접힌다

            var removed = new List<ModifierSlot>();
            Assert.AreEqual(0, set.Expire(0.05f, removed));
            Assert.AreEqual(1, set.Expire(0.05f, removed), "0.1초가 다 됐다");
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(StatKind.MoveSpeedMul, removed[0].Key.Stat);
            Assert.AreEqual(1f, set.Effective.MoveSpeedMul, 1e-4f, "접기가 다시 돌지 않으면 0.5 로 남는다");
        }

        [Test]
        public void 무한_슬롯은_만료를_자연_통과한다()
        {
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.DamageMul, CombineOp.Additive), 0.5f, float.PositiveInfinity);
            for (int i = 0; i < 100; i++) set.Expire(1f);
            Assert.AreEqual(1, set.Count);
        }

        // ── 접기 ─────────────────────────────────────────────────────────────

        [Test]
        public void 결합식은_가산_합에_곱셈_곱이다()
        {
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.DamageMul, CombineOp.Additive), 0.2f, 5f);
            set.Apply(Key(2, StatKind.DamageMul, CombineOp.Additive), 0.3f, 5f);
            set.Apply(Key(3, StatKind.DamageMul, CombineOp.Multiplicative), 0.5f, 5f);
            Assert.AreEqual((1f + 0.5f) * 0.5f, set.Effective.DamageMul, 1e-4f);
        }

        [Test]
        public void 클램프_경계는_스탯마다_다르다()
        {
            var set = new ModifierSet();
            for (int i = 0; i < 10; i++)
                set.Apply(Key(i, StatKind.MoveSpeedMul, CombineOp.Multiplicative), 0.5f, 5f);
            Assert.AreEqual(ModifierMath.MoveMulFloor, set.Effective.MoveSpeedMul, 1e-4f,
                "이동 바닥은 0.15 — 감속으로 완전 정지를 만들 수 없다");

            var set2 = new ModifierSet();
            for (int i = 0; i < 10; i++)
                set2.Apply(Key(i, StatKind.MaxHealthMul, CombineOp.Multiplicative), 0.5f, 5f);
            Assert.AreEqual(ModifierMath.MaxHealthMulFloor, set2.Effective.MaxHealthMul, 1e-4f,
                "최대 체력 바닥은 0.05 — 라스트런 ×0.1 이 일반 바닥에 걸리면 안 된다");
        }

        [Test]
        public void 재생은_곱셈_슬롯만_있으면_0_이다()
        {
            // F5 — 결합식이 `(0 + Σadd) × Πmul` 이라 사실상 더하기 전용 스탯이다.
            var set = new ModifierSet();
            set.Apply(Key(1, StatKind.RegenPerSec, CombineOp.Multiplicative), 2f, 5f);
            Assert.AreEqual(0f, set.Effective.RegenPerSec, 1e-4f);

            set.Apply(Key(2, StatKind.RegenPerSec, CombineOp.Additive), 3f, 5f);
            Assert.AreEqual(6f, set.Effective.RegenPerSec, 1e-4f, "더하기가 있어야 곱하기가 산다");
        }

        [Test]
        public void 저작_분류는_올리면_가산_깎으면_곱셈이다()
        {
            ModifierAuthoring.FromMultiplier(1.2f, out var up, out float upMag);
            Assert.AreEqual(CombineOp.Additive, up);
            Assert.AreEqual(0.2f, upMag, 1e-4f);

            ModifierAuthoring.FromMultiplier(0.8f, out var down, out float downMag);
            Assert.AreEqual(CombineOp.Multiplicative, down);
            Assert.AreEqual(0.8f, downMag, 1e-4f);
        }
    }
}
