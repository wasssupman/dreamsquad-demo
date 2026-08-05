// battle-sim-extraction unit 18-C/3 — 신 sim `ModifierApplySystem` 의 **오라클 복제**.
//
// 계획서 §증인 5: 어서션을 재작성하지 않고 **복제**한다. 재작성하면 그 순간 구 sim 의 오라클이
// 사라져 비교 기준 자체가 없어진다. 구 버전(`ModifierFrameworkTests`)은 unit 20 스왑 때 삭제한다.
//
// 원본 대응:
//   Test 1  StatModifier_SameKey_Refreshes_Slot_Instead_Of_Adding_Duplicate
//   —       StatModifierApply_Ignores_Event_When_Target_Was_Destroyed
//   —       StackModifierApply_Ignores_Event_When_Target_Was_Destroyed
//   (AdditiveBuffs_Sum_ThenCeil 의 "distinct stackId → 2 슬롯" 부분)
//
// 픽스처 차이: 구 오라클은 4시스템을 한 월드에 올려 Tick 이 섞이지만 여기는 Apply **하나**만
// 돌린다. 그래서 감쇠가 없어 `remaining` 을 **정확히** 단정할 수 있다(구 테스트가
// `GreaterOrEqual` 로 느슨했던 자리 — 주장은 같고 관측이 더 날카롭다).
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;

namespace Wassup.Tests.EditMode
{
    public class SimModifierApplyTests
    {
        private SimWorld _world;
        private SimChannel<StatModifierApplyEvent> _statChannel;
        private SimChannel<StackModifierApplyEvent> _stackChannel;
        private ModifierApplySystem _sys;

        [SetUp]
        public void SetUp()
        {
            // SimConfig 는 생성자 필수 인자다(18-A/4) — 배선 누락이 "규칙 없음" 으로 위장하는 것을 막는다.
            _world = new SimWorld(new SimConfig(pickupSeed: 1u, bombSeedBase: 1u));
            _statChannel = new SimChannel<StatModifierApplyEvent>();
            _stackChannel = new SimChannel<StackModifierApplyEvent>();
            _sys = new ModifierApplySystem(_statChannel, _stackChannel);
        }

        private SimEntityId CreateUnit()
        {
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            return e;
        }

        private void Stat(SimEntityId target, StatKind stat, CombineOp op, float magnitude,
                          float duration, SimEntityId source, ushort stackId = 0,
                          ModifierOrigin origin = ModifierOrigin.Unspecified)
            => _statChannel.Enqueue(new StatModifierApplyEvent
            {
                target = target, stat = stat, op = op, magnitude = magnitude,
                duration = duration, source = source, stackId = stackId, origin = origin,
            });

        private void Stack(SimEntityId target, StackKind kind, byte countDelta, byte maxStack,
                           float perAppDuration, SimEntityId source)
            => _stackChannel.Enqueue(new StackModifierApplyEvent
            {
                target = target, kind = kind, countDelta = countDelta, maxStack = maxStack,
                perAppDuration = perAppDuration, source = source,
            });

        // ── 스탯 병합 키 4축 ──────────────────────────────────────────────────────

        [Test]
        public void StatModifier_SameKey_Refreshes_Slot_Instead_Of_Adding_Duplicate()
        {
            var e = CreateUnit();

            Stat(e, StatKind.DamageMul, CombineOp.Multiplicative, 1.5f, 10f, e);
            _sys.Run(_world);

            var slots = _world.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, slots.Count, "첫 적용은 슬롯 1개.");
            Assert.AreEqual(1.5f, slots[0].magnitude, 1e-5f);

            // 같은 키 · 더 짧은 duration.
            Stat(e, StatKind.DamageMul, CombineOp.Multiplicative, 2.0f, 5f, e);
            _sys.Run(_world);

            slots = _world.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, slots.Count, "같은 키 재적용은 refresh 다 — 슬롯이 늘지 않는다.");
            Assert.AreEqual(2.0f, slots[0].magnitude, 1e-5f, "magnitude 는 **새 값**이 이긴다.");
            Assert.AreEqual(10f, slots[0].header.remaining, 1e-5f,
                "remaining = max(old, new) — 짧은 재적용이 긴 버프를 깎지 않는다.");
        }

        [Test]
        public void StatModifier_DistinctStackId_CreatesSeparateSlots()
        {
            var e = CreateUnit();

            Stat(e, StatKind.DamageMul, CombineOp.Additive, 3f, 100f, e, stackId: 0);
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 3f, 100f, e, stackId: 1);
            _sys.Run(_world);

            Assert.AreEqual(2, _world.GetBuffer<StatModifierSlot>(e).Count,
                "stackId 가 다르면 별개 슬롯(병합 키의 4번째 축).");
        }

        [Test]
        public void StatModifier_DistinctOp_CreatesSeparateSlots()
        {
            var e = CreateUnit();

            // `op` 가 키에 있는 이유의 관측점: 한 생산자가 1.0 경계를 넘나들면 Additive 슬롯과
            // Multiplicative 슬롯이 **공존**해 refresh 가 아니라 누적이 된다(슬롯 누수).
            // 구 sim 의 실존 성질이고, 채널을 단방향으로 유지하는 규율의 근거다.
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 0.2f, 100f, e);
            Stat(e, StatKind.DamageMul, CombineOp.Multiplicative, 0.9f, 100f, e);
            _sys.Run(_world);

            Assert.AreEqual(2, _world.GetBuffer<StatModifierSlot>(e).Count,
                "op 가 다르면 별개 슬롯 — refresh 되지 않는다.");
        }

        [Test]
        public void StatModifier_RefreshOverwritesOrigin_WhichIsNotPartOfTheKey()
        {
            var e = CreateUnit();

            Stat(e, StatKind.DamageMul, CombineOp.Additive, 1f, 10f, e, origin: ModifierOrigin.Skill);
            _sys.Run(_world);
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 1f, 10f, e, origin: ModifierOrigin.Tile);
            _sys.Run(_world);

            var slots = _world.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, slots.Count, "origin 은 키가 아니다 — 슬롯이 갈리지 않는다.");
            Assert.AreEqual(ModifierOrigin.Tile, slots[0].header.origin, "refresh 가 origin 을 덮는다.");
        }

        [Test]
        public void StatModifierApply_MarksDirty()
        {
            var e = CreateUnit();
            Assert.IsFalse(_world.Has<ModifierStatsDirty>(e));

            Stat(e, StatKind.DamageMul, CombineOp.Additive, 1f, 10f, e);
            _sys.Run(_world);

            Assert.IsTrue(_world.Has<ModifierStatsDirty>(e),
                "스탯 슬롯 변경은 집계를 깨운다(신 sim 은 **존재 = dirty**).");
        }

        // ── 스택 병합 키 2축 ──────────────────────────────────────────────────────

        [Test]
        public void StackModifier_SameSourceAndKind_Merges_CapsAtMaxStack_AndOverwritesRemaining()
        {
            var e = CreateUnit();

            Stack(e, StackKind.Fire, countDelta: 4, maxStack: 5, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            var slots = _world.GetBuffer<StackModifierSlot>(e);
            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(4, slots[0].stackCount);
            Assert.AreEqual(100f, slots[0].header.remaining, 1e-5f);

            // 같은 (source, kind) — 병합. cap 5 를 넘지 않고 remaining 은 **덮어쓴다**.
            Stack(e, StackKind.Fire, countDelta: 3, maxStack: 5, perAppDuration: 20f, source: e);
            _sys.Run(_world);

            slots = _world.GetBuffer<StackModifierSlot>(e);
            Assert.AreEqual(1, slots.Count, "(source, kind) 2축이 같으면 병합.");
            Assert.AreEqual(5, slots[0].stackCount, "4+3=7 이 cap 5 로 잘린다.");
            Assert.AreEqual(20f, slots[0].header.remaining, 1e-5f,
                "스택은 remaining 을 **덮어쓴다** — 스탯의 max(old,new) 와 **비대칭**이고 그게 계약이다.");
        }

        [Test]
        public void StackModifier_Merge_UsesSlotMaxStack_NotEventMaxStack()
        {
            var e = CreateUnit();

            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 3, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            // 생산자가 더 큰 cap 을 보내도 **기존 슬롯의 maxStack 이 이긴다**.
            Stack(e, StackKind.Fire, countDelta: 9, maxStack: 99, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            var slot = _world.GetBuffer<StackModifierSlot>(e)[0];
            Assert.AreEqual(3, slot.stackCount, "cap 은 슬롯의 maxStack(3) — 이벤트의 99 가 아니다.");
            Assert.AreEqual(3, slot.maxStack, "슬롯의 maxStack 도 갱신되지 않는다.");
        }

        [Test]
        public void StackModifier_Merge_PreservesLastTriggeredStack()
        {
            var e = CreateUnit();
            Stack(e, StackKind.Fire, countDelta: 2, maxStack: 9, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            // 임계 시스템이 남긴 엣지 캐시를 흉내낸다.
            var buf = _world.GetBuffer<StackModifierSlot>(e);
            var s = buf[0]; s.lastTriggeredStack = 2; buf[0] = s;

            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 9, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            Assert.AreEqual(2, _world.GetBuffer<StackModifierSlot>(e)[0].lastTriggeredStack,
                "엣지 캐시를 리셋하면 임계가 매 부착마다 재발화한다.");
        }

        [Test]
        public void StackModifier_DifferentSource_CreatesSeparateSlot()
        {
            var e = CreateUnit();
            var other = CreateUnit();

            Stack(e, StackKind.Bleed, countDelta: 1, maxStack: 9, perAppDuration: 100f, source: e);
            Stack(e, StackKind.Bleed, countDelta: 1, maxStack: 9, perAppDuration: 100f, source: other);
            _sys.Run(_world);

            Assert.AreEqual(2, _world.GetBuffer<StackModifierSlot>(e).Count,
                "source 가 다르면 별개 슬롯(난도질꾼 2기가 이 경로다).");
        }

        [Test]
        public void StackModifierApply_DoesNotMarkDirty()
        {
            var e = CreateUnit();

            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 5, perAppDuration: 100f, source: e);
            _sys.Run(_world);

            Assert.IsFalse(_world.Has<ModifierStatsDirty>(e),
                "스택 버퍼는 ModifierStats 에 직접 기여하지 않는다 — 집계를 깨우지 않는다.");
        }

        // ── 파괴된 대상 ───────────────────────────────────────────────────────────

        [Test]
        public void StatModifierApply_Ignores_Event_When_Target_Was_Destroyed()
        {
            var e = CreateUnit();
            Stat(e, StatKind.MoveSpeedMul, CombineOp.Multiplicative, 0.5f, 1f, SimEntityId.Null);
            _world.Destroy(e);

            Assert.DoesNotThrow(() => _sys.Run(_world));
            Assert.AreEqual(0, _statChannel.Count, "무시해도 채널은 비운다(부분 소비 금지).");
        }

        [Test]
        public void StackModifierApply_Ignores_Event_When_Target_Was_Destroyed()
        {
            var e = CreateUnit();
            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 5, perAppDuration: 1f, source: SimEntityId.Null);
            _world.Destroy(e);

            Assert.DoesNotThrow(() => _sys.Run(_world));
            Assert.AreEqual(0, _stackChannel.Count);
        }

        // ── 버퍼 신설 경로의 회귀 핀 ──────────────────────────────────────────────
        // 구 sim 에 **테스트가 없던** 자리다. 구 코드가 ECB 대신 EntityManager 로 즉시 버퍼를
        // 만든 이유가 여기 있다 — ECB 였다면 같은 드레인의 두 이벤트가 AddBuffer 를 두 번 기록해
        // playback 이 첫 슬롯을 덮어썼다(마지막만 생존). 신 sim 에서 이 함정이 되살아나면
        // 슬롯이 조용히 사라진다.

        [Test]
        public void TwoEventsForSameBufferlessTarget_InOneDrain_BothLand()
        {
            var e = CreateUnit();
            Assert.IsFalse(_world.HasBuffer<StatModifierSlot>(e), "시작 시 버퍼 부재.");

            Stat(e, StatKind.DamageMul, CombineOp.Additive, 1f, 10f, e, stackId: 0);
            Stat(e, StatKind.AttackSpeedMul, CombineOp.Additive, 2f, 10f, e, stackId: 1);
            _sys.Run(_world);   // 한 번의 드레인에서 둘 다 처리

            Assert.AreEqual(2, _world.GetBuffer<StatModifierSlot>(e).Count,
                "버퍼 신설 직후의 두 번째 이벤트도 append 돼야 한다(첫 슬롯 덮어쓰기 금지).");
        }

        [Test]
        public void Buffer_IsAbsent_UntilFirstApply()
        {
            // 부재 ≠ 빈 버퍼 — 조회가 자동 생성하지 않는다는 18-A 계약의 관측점.
            var e = CreateUnit();
            Assert.IsNull(_world.GetBuffer<StatModifierSlot>(e));
            Assert.IsNull(_world.GetBuffer<StackModifierSlot>(e));
        }
    }
}
