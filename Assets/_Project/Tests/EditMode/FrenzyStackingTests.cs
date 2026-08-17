// dreamcatcher-berserker unit 1 — 최대 중첩 저작 개방 + 「공격 N회 × 자기 버프」 arm.
//
// 세 층을 각각 고정한다:
//   ① 상한 환산(StackCap) — 순수
//   ② bake — 최대 중첩이 슬롯까지 내려가는가 · 성립 안 하는 저작을 loud 로 세우는가
//   ③ arm — 공격이 성사되면 **자기에게** 올바른 이벤트가 나가는가
//
// ③ 이 큐를 직접 들여다보는 이유: 슬롯이 생기는 것까지 한 픽스처에서 보려면
// ModifierApplySystem 과 AttackSystem 의 실행 순서에 기대야 하는데, 이 픽스처의
// SimulationSystemGroup 에는 둘 사이 순서 제약이 없다. 누적 자체는
// StackingModifierMergeTests(병합 층)가 이미 고정하므로, 여기서는 **arm 이 무엇을
// 보내는가**만 본다 — 두 층이 합쳐져 사슬 전체가 덮인다.
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class FrenzyStackingTests
    {
        // ── ① 상한 환산 ───────────────────────────────────────────────────────────

        [Test]
        public void StackCap_ZeroOrOneStack_MeansNoAccumulation()
        {
            Assert.AreEqual(0f, ModifierAuthoring.StackCap(1.08f, 0), 1e-6f, "0 = 안 쌓임");
            // 1중첩은 «1회분이 곧 상한» 이라 두 번째 적용이 자기 값에서 멈춘다 = 안 쌓임.
            Assert.AreEqual(0.08f, ModifierAuthoring.StackCap(1.08f, 1), 1e-6f);
        }

        [Test]
        public void StackCap_NonBuff_IsRejected()
        {
            Assert.AreEqual(0f, ModifierAuthoring.StackCap(1f, 10), 1e-6f, "배율 1 = 버프가 아니다");
            Assert.AreEqual(0f, ModifierAuthoring.StackCap(0.87f, 10), 1e-6f,
                "1 미만은 곱셈 버킷으로 가서 더하기가 성립하지 않는다");
        }

        [Test]
        public void StackCap_IsPerStackTimesMaxStacks_InAdditiveBucket()
        {
            // ⚠ 배율(1.08)이 아니라 «배율 − 1»(0.08)이 슬롯에 실린다 — FromMultiplier 가
            // 버프를 가산 버킷으로 보내기 때문. 상한을 배율 기준으로 잡으면 한 스택 어긋난다.
            ModifierAuthoring.FromMultiplier(1.08f, out var op, out var mag);
            Assert.AreEqual(CombineOp.Additive, op);
            Assert.AreEqual(0.08f, mag, 1e-6f);
            Assert.AreEqual(0.8f, ModifierAuthoring.StackCap(1.08f, 10), 1e-6f);
        }

        // ── ② bake ────────────────────────────────────────────────────────────────

        private World _bakeWorld;
        private GameObject _go;
        private BattleBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _bakeWorld = new World("FrenzyStackingTests");
            _go = new GameObject("BattleBridge_FrenzyBake");
            // inactive 로 붙여 Awake/씬 의존 validation 을 건너뛴다(PatternBakeTests 레시피).
            _go.SetActive(false);
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "_world", _bakeWorld);
            SetField(_bridge, "_em", _bakeWorld.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _bakeWorld?.Dispose();
        }

        private static DreamcatcherCard FrenzyCard(int maxStacks, float percent = 8f,
            CardBuffKind buffStat = CardBuffKind.AttackSpeed)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_frenzy";
            card.type = CardType.Unit;
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 1 },
                    payload = new DcPayloadSpec
                    {
                        kind      = DcPayloadKind.SelfStatBuff,
                        magnitude = percent,
                        tileRange = maxStacks,
                        duration  = 4f,
                        buffStat  = buffStat,
                    },
                },
            };
            return card;
        }

        [Test]
        public void Bake_CarriesMaxStacks_IntoTheSlot()
        {
            var em = _bakeWorld.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<DefenderUnitTag>(defender);

            var card = FrenzyCard(maxStacks: 10);
            int handle = _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            // 반환값은 부착 개수가 아니라 오라 핸들이다(오라 없으면 0) — 아무것도 안 붙었을
            // 때만 -1 이라 개통 단언은 «-1 이 아니다» 다.
            Assert.AreNotEqual(-1, handle);
            var slots = em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(10, slots[0].tileRange,
                "최대 중첩이 슬롯까지 안 내려가면 arm 이 상한을 못 실어 조용히 안 쌓인다");
            Assert.AreEqual(1.08f, slots[0].magnitude, 1e-5f, "8% → 배율 1.08");

            Object.DestroyImmediate(card);
        }

        [Test]
        public void Bake_WithoutMaxStacks_StaysOverwrite()
        {
            var em = _bakeWorld.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<DefenderUnitTag>(defender);

            var card = FrenzyCard(maxStacks: 0);
            _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            var slots = em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(0, slots[0].tileRange,
                "최대 중첩을 안 적은 자기 버프는 예전처럼 덮어쓴다(기존 카드 무변화)");

            Object.DestroyImmediate(card);
        }

        [Test]
        public void Bake_RejectsMaxStacks_OnNonBuffMultiplier()
        {
            var em = _bakeWorld.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<DefenderUnitTag>(defender);

            // 방어력(EffectiveHealth)은 배율이 1/(1+p) 라 **1 미만** 이다 → 곱셈 버킷.
            // 곱셈 값을 더하면 의미가 뒤집히므로(0.87+0.87=1.74=강화) 거절돼야 한다.
            var card = FrenzyCard(maxStacks: 5, percent: 15f, buffStat: CardBuffKind.EffectiveHealth);
            LogAssert.Expect(LogType.Warning, new Regex("최대 중첩"));
            _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            bool leaked = em.HasBuffer<DcTriggerSlot>(defender)
                          && em.GetBuffer<DcTriggerSlot>(defender).Length > 0;
            Assert.IsFalse(leaked, "거절했으면 슬롯이 남으면 안 된다");

            Object.DestroyImmediate(card);
        }

        // ── ③ arm ─────────────────────────────────────────────────────────────────

        [Test]
        public void AttackArm_SendsSelfBuff_WithCap()
        {
            var ev = RunOneAttackAndReadBuffEvent(maxStacks: 10, out var defender);

            Assert.AreEqual(defender, ev.target, "자기에게 걸린다");
            Assert.AreEqual(defender, ev.source, "출처도 자기 — 슬롯 병합 키가 안정적이어야 한다");
            Assert.AreEqual(StatKind.AttackSpeedMul, ev.stat);
            Assert.AreEqual(CombineOp.Additive, ev.op, "버프는 가산 버킷");
            Assert.AreEqual(0.08f, ev.magnitude, 1e-5f, "1회분 = 배율 − 1");
            Assert.AreEqual(4f, ev.duration, 1e-5f);
            Assert.AreEqual(0.8f, ev.magnitudeCap, 1e-5f, "상한 = 1회분 × 최대 중첩");
            Assert.AreEqual(ModifierOrigin.Dreamcatcher, ev.origin,
                "경계 arm 의 HealthThreshold 를 복사하면 상태FX 가 «빈사에서 켜졌다» 로 읽는다");
        }

        [Test]
        public void AttackArm_WithoutMaxStacks_SendsNoCap()
        {
            var ev = RunOneAttackAndReadBuffEvent(maxStacks: 0, out _);
            Assert.AreEqual(0f, ev.magnitudeCap, 1e-6f,
                "상한 0 = 기존 덮어쓰기. 이게 새면 기존 자기 버프 카드의 동작이 바뀐다");
        }

        private StatModifierApplyEvent RunOneAttackAndReadBuffEvent(int maxStacks, out Entity defender)
        {
            using var world = new World("FrenzyArmTests");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var statQueue = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            em.AddComponentData(em.CreateEntity(),
                new StatModifierApplyEventsSingleton { queue = statQueue });

            defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(defender, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponentData(defender, new Health { value = 10f, max = 10f });
            em.AddBuffer<IncomingDamage>(defender);
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddComponentData(defender, new AttackState
            {
                range = 10f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
            });
            var outputs = em.AddBuffer<AttackOutputElement>(defender);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
            });

            var slots = em.AddBuffer<DcTriggerSlot>(defender);
            slots.Add(new DcTriggerSlot
            {
                instanceId        = 1,
                trigger           = DcTriggerKind.AttackN,
                period            = 1,
                payload           = DcPayloadKind.SelfStatBuff,
                magnitude         = 1.08f,       // bake 가 % → 배율로 이미 바꿔 실은 값
                duration          = 4f,
                buffStat          = StatKind.AttackSpeedMul,
                statBuffStackId   = 7,
                tileRange         = maxStacks,
                patternIndex      = -1,
                hazardDataIndex   = -1,
            });

            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponentData(enemy, new Health { value = 100f, max = 100f });
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddComponent<AttackUnitTag>(enemy);

            world.SetTime(new TimeData(0.016d, 0.016f));
            simGroup.Update();

            Assert.IsTrue(statQueue.TryDequeue(out var ev),
                "공격이 성사됐는데 자기 버프 이벤트가 안 나갔다 — arm 이 없으면 여태 " +
                "«unhandled payload kind» 경고만 남기고 카운트만 태웠다");
            statQueue.Dispose();
            return ev;
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"field '{name}' 을 찾지 못했다");
            fi.SetValue(target, value);
        }
    }
}
