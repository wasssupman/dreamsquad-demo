using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-content-4 unit 4 (악몽 사냥) — 잠든 적을 때린 그 타격만 2배.
    //
    // 계측 설계: 다중 근접 유닛(파이터, attackTargetCount 3)이 **한 번의 공격으로 두 더미를
    // 동시에** 때린다. 같은 공격·같은 output 이므로 두 더미의 HP 감소 비율은 쿨다운 위상
    // 노이즈와 무관하게 결정론적이다 — 창 안 비율은 오차 없이 곱 배율 그 자체가 된다.
    // (창과 창 사이 비교만 ±1 공격의 흔들림을 갖는다.)
    //
    // 잠든 더미는 매 프레임 Sleep 을 다시 세운다. 계약 5 대로 «잠을 깨우는 그 타격»이
    // 2배를 받고 그 직후 wake-on-hit(CcClearRequests)이 Sleep 을 지우기 때문에, 유지하지
    // 않으면 창 전체가 아니라 첫 한 방만 측정된다.
    public class DreamcatcherSleepDamageTest
    {
        private const float DummyHp = 1_000_000f;
        private const float CcHold = 1000f;   // 창 내내 만료되지 않을 잔여 시간
        private const float Decay = 8f;       // 배치 시 임시 버프가 가라앉는 시간
        private const int   HitsPerWindow = 4;   // 측정 창 = 대조 더미가 맞은 횟수
        private const float MaxWindowSec = 25f;  // 표본이 안 모이면 조용히 넘어가지 않고 실패시킨다

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // ① 잠든 더미 = 기준값 × 2 ② **같은 공격에 함께 맞은 깨어 있는 더미 = 기준값 그대로.**
        // ②가 이 카드가 강공(HeavyStrike, 그 공격의 전 victim 배율)과 갈리는 지점이고,
        // 빠지면 사양 초과(옆의 깨어 있는 적까지 2배)가 조용히 통과한다.
        [UnityTest]
        public IEnumerator DamageVsSleeping_DoublesSleeperOnly_NeighborStaysAtBaseline()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity defender = Entity.Null;
            yield return PlaceCleaveDefender(bridge, e => defender = e);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            // 둘 다 공격자 발밑(사거리 1 안) — 매 공격의 cleave 대상 2슬롯을 항상 채운다.
            var sleeper = MakeEnemyDummy(em, defPos + new float3(0.05f, 0f, 0f));
            var awake = MakeEnemyDummy(em, defPos + new float3(-0.05f, 0f, 0f));
            // 깨어 있는 쪽에 **Stun** 을 건다: "버퍼가 있나"가 아니라 "kind 가 Sleep 인가"로
            // 판정한다는 것까지 고정한다(CC 지만 자고 있지는 않은 적).
            HoldCc(em, sleeper, CcKind.Sleep);
            HoldCc(em, awake, CcKind.Stun);

            yield return Settle(em, sleeper, awake, Decay);

            // 창 1 — 무카드 대조군. 두 더미가 같은 공격을 같이 맞으므로 정확히 같아야 한다.
            ResetHp(em, sleeper, awake);
            yield return RunUntilHits(em, sleeper, awake, HitsPerWindow, MaxWindowSec);
            float baseSleeper = Dealt(em, sleeper), baseAwake = Dealt(em, awake);
            Probe(em, defender, "window1", baseSleeper, baseAwake);
            Assert.Greater(baseAwake, 0f, "대조군에서 두 더미 모두 실제로 맞아야 한다");
            Assert.That(baseSleeper / baseAwake, Is.InRange(0.98f, 1.02f),
                $"무카드에서는 잠들었는지와 무관하게 동일 ({baseSleeper:0.0} vs {baseAwake:0.0})");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeNightmareHuntCard(2f));
            Assert.GreaterOrEqual(handle, 0, "악몽 사냥 부착 (bake ok)");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcAttackModSlot>(defender),
                "DcAttackModSlot baked");
            yield return null;

            // 창 2 — 부착 후. 잠든 쪽만 2배, 옆의 깨어 있는 쪽은 기준값.
            ResetHp(em, sleeper, awake);
            yield return RunUntilHits(em, sleeper, awake, HitsPerWindow, MaxWindowSec);
            float sleeperDealt = Dealt(em, sleeper), awakeDealt = Dealt(em, awake);
            Probe(em, defender, "window2", sleeperDealt, awakeDealt);

            DestroyIfAlive(em, sleeper, awake);

            // 창 2 가 통째로 0 이면 «배율이 틀렸다»가 아니라 «공격이 성립하지 않았다»이다.
            // NaN 비율로 터지면 다음 사람이 배율 로직을 의심하게 되므로 여기서 먼저 끊는다.
            Assert.Greater(awakeDealt, 0f,
                "부착 후 창에서 공격이 성립해야 한다 — 0 이면 계측 무대가 무너진 것이다(배율 문제 아님)");

            Assert.That(sleeperDealt / awakeDealt, Is.InRange(1.9f, 2.1f),
                $"잠든 적에게만 ×2 (sleeper {sleeperDealt:0.0} / awake {awakeDealt:0.0})");
            // 창 간 비교라 ±1 공격의 흔들림을 허용한다. 요점은 «깨어 있는 쪽이 안 올랐다».
            Assert.That(awakeDealt / baseAwake, Is.InRange(0.75f, 1.25f),
                $"깨어 있는 이웃은 기준값 그대로여야 한다 ({baseAwake:0.0} → {awakeDealt:0.0})");
            Assert.Greater(sleeperDealt / baseSleeper, 1.5f,
                $"잠든 쪽은 부착 후 확실히 올라야 한다 ({baseSleeper:0.0} → {sleeperDealt:0.0})");
        }

        // shatter_hymn(DamageVsCc) 무회귀 + 곱 중첩. 수면도 CC 이므로 잠든 적에게는 **둘 다**
        // 걸린다 — 의도된 중첩(계약 4-1). Stun 더미가 shatter 만 받는 대조군이라, 같은 창
        // 안의 두 더미 비율이 곧 «수면 특효가 얹은 배율»이다.
        [UnityTest]
        public IEnumerator DamageVsSleeping_StacksMultiplicatively_WithShatterHymn()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity defender = Entity.Null;
            yield return PlaceCleaveDefender(bridge, e => defender = e);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var sleeper = MakeEnemyDummy(em, defPos + new float3(0.05f, 0f, 0f));
            var stunned = MakeEnemyDummy(em, defPos + new float3(-0.05f, 0f, 0f));
            HoldCc(em, sleeper, CcKind.Sleep);
            HoldCc(em, stunned, CcKind.Stun);

            yield return Settle(em, sleeper, stunned, Decay);

            // 창 1 — 무카드.
            ResetHp(em, sleeper, stunned);
            yield return RunUntilHits(em, sleeper, stunned, HitsPerWindow, MaxWindowSec);
            float baseSleeper = Dealt(em, sleeper), baseStunned = Dealt(em, stunned);
            Assert.Greater(baseStunned, 0f, "대조군 피해 관측");
            Assert.That(baseSleeper / baseStunned, Is.InRange(0.98f, 1.02f), "무카드 동일");

            // 창 2 — shatter_hymn 만(+100% DamageVsCc = ×2). 둘 다 CC 라 **비율은 그대로 1**
            // 이고, 절대값이 오른 것으로 shatter 가 살아 있음을 본다(무회귀).
            var shatter = ScriptableObject.CreateInstance<DreamcatcherCard>();
            shatter.axis = CardTargetAxis.All;
            shatter.effects = new[] { new CardEffect { kind = CardBuffKind.DamageVsCc, percent = 100f } };
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardHosted(shatter), 0, "shatter 적용");
            for (int i = 0; i < 3; i++) yield return null; // ModifierApply/Aggregate

            ResetHp(em, sleeper, stunned);
            yield return RunUntilHits(em, sleeper, stunned, HitsPerWindow, MaxWindowSec);
            float shatterSleeper = Dealt(em, sleeper), shatterStunned = Dealt(em, stunned);
            Assert.That(shatterSleeper / shatterStunned, Is.InRange(0.98f, 1.02f),
                "shatter 는 CC 전반이라 수면/기절을 가리지 않는다");
            Assert.Greater(shatterStunned / baseStunned, 1.5f,
                $"shatter_hymn 무회귀: CC 적 피해가 올라야 한다 ({baseStunned:0.0} → {shatterStunned:0.0})");

            // 창 3 — 악몽 사냥 추가. 잠든 쪽만 한 겹 더(×2) → 같은 창 안 비율이 2.
            Assert.GreaterOrEqual(
                bridge.ApplyDreamcatcherCardToUnit(defender, MakeNightmareHuntCard(2f)), 0,
                "악몽 사냥 부착");
            yield return null;

            ResetHp(em, sleeper, stunned);
            yield return RunUntilHits(em, sleeper, stunned, HitsPerWindow, MaxWindowSec);
            float bothSleeper = Dealt(em, sleeper), bothStunned = Dealt(em, stunned);

            DestroyIfAlive(em, sleeper, stunned);

            Assert.That(bothSleeper / bothStunned, Is.InRange(1.9f, 2.1f),
                $"수면 특효가 shatter 위에 곱으로 얹힌다 ({bothSleeper:0.0} / {bothStunned:0.0})");
            // ×2(shatter) × ×2(수면) = ×4. 창 간 비교라 여유를 둔다.
            Assert.Greater(bothSleeper / baseSleeper, 3f,
                $"잠든 적 최종 ≈ 기준값 ×4 ({baseSleeper:0.0} → {bothSleeper:0.0})");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        // 파이터(bruiser): 근접(투사체 없음) + attackTargetCount 3 + aggroCapacity 0.
        // 어그로 선정 분기를 타지 않는 다중 근접이라 «한 공격이 두 더미를 같이 때린다»가
        // 데이터로 보장된다. StartBattle 은 부르지 않는다 — 실웨이브가 cleave 슬롯을
        // 두고 경쟁하면 창마다 대상이 달라진다.
        private static IEnumerator PlaceCleaveDefender(BattleBridge bridge, System.Action<Entity> result)
        {
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog loaded");
            var unit = cat.ById("bruiser");
            Assert.IsNotNull(unit, "bruiser defender data");
            Assert.GreaterOrEqual(unit.attackTargetCount, 2, "다중 근접이어야 같은 공격으로 둘을 때린다");
            // Unity 오브젝트 비교는 == 오버로드로 한다(NUnit IsNull 은 fake-null 을 못 본다).
            Assert.IsTrue(unit.projectile == null, "근접이어야 hitTarget 별 즉시 해결 경로를 탄다");

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place bruiser");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            result(FindDefender(bridge, em));
        }

        private static DreamcatcherCard MakeNightmareHuntCard(float mul)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.mechanics = new DcMechanic[0];
            card.attackMods = new[]
            {
                new DcAttackModSpec { kind = DcAttackModKind.DamageVsSleeping, damageMul = mul },
            };
            return card;
        }

        private static Entity MakeEnemyDummy(EntityManager em, float3 pos)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = DummyHp, max = DummyHp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(e);
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddBuffer<StackModifierSlot>(e);
            return e;
        }

        // 해당 kind 슬롯의 잔여 시간을 다시 채운다(없으면 추가). Sleep 은 피격 시
        // CcClearSystem 이 통째로 지우므로 «갱신»이 아니라 «재부여»가 필요하다.
        private static void HoldCc(EntityManager em, Entity e, CcKind kind)
        {
            if (!em.Exists(e) || !em.HasBuffer<CcEffect>(e)) return;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].kind != kind) continue;
                var slot = buf[i];
                slot.remainingTime = CcHold;
                buf[i] = slot;
                return;
            }
            buf.Add(new CcEffect { kind = kind, remainingTime = CcHold });
        }

        // sleeper 는 Sleep, 상대 더미는 Stun 을 창 내내 유지한다. Stun 은 감소만 하지만
        // Sleep 은 피격마다 통째로 사라지므로 매 프레임 재부여가 필요하다.
        //
        // ⚠ **창은 벽시계가 아니라 «대조 더미가 몇 대 맞았나» 로 닫는다**(2026-08-16).
        // 초 단위 창이던 초판은 에디터 포커스/부하에 따라 같은 6초가 6회 공격이 되기도 하고
        // **0회**가 되기도 해서, 비율이 0/0=NaN 으로 터졌다(단독 실행은 통과, 다른 스위트와
        // 함께 돌리면 실패 — 전형적인 벽시계 의존 불안정성이다). 시뮬 진행량으로 창을 닫으면
        // 프레임률과 무관하게 항상 같은 표본이 모인다. **초 단위로 되돌리지 말 것.**
        private static IEnumerator RunUntilHits(EntityManager em, Entity sleeper, Entity control,
                                                int hits, float maxSeconds)
        {
            float t = 0f;
            int seen = 0;
            float last = Dealt(em, control);
            while (seen < hits && t < maxSeconds)
            {
                HoldCc(em, sleeper, CcKind.Sleep);
                HoldCc(em, control, CcKind.Stun);
                t += Time.deltaTime;
                yield return null;
                if (!em.Exists(control)) break;
                float now = Dealt(em, control);
                if (now > last + 0.01f) { seen++; last = now; }
            }
            HoldCc(em, sleeper, CcKind.Sleep);
            HoldCc(em, control, CcKind.Stun);
            Assert.AreEqual(hits, seen,
                $"창이 표본 {hits}회를 모으지 못했다({seen}회, {t:0.0}s) — 공격이 성립하지 않는 무대다");
        }

        // 배치 직후 임시 버프가 가라앉기를 기다린다. 여기서는 값을 재지 않으므로 벽시계로 충분.
        private static IEnumerator Settle(EntityManager em, Entity sleeper, Entity control, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                HoldCc(em, sleeper, CcKind.Sleep);
                HoldCc(em, control, CcKind.Stun);
                t += Time.deltaTime;
                yield return null;
            }
        }

        private static void ResetHp(EntityManager em, Entity a, Entity b)
        {
            em.SetComponentData(a, new Health { value = DummyHp, max = DummyHp });
            em.SetComponentData(b, new Health { value = DummyHp, max = DummyHp });
        }

        private static float Dealt(EntityManager em, Entity e)
            => DummyHp - em.GetComponentData<Health>(e).value;

        // 창이 0 으로 끝났을 때 «무대가 무너진 것인지 배율이 틀린 것인지» 를 가르는 계측.
        // 두 실패가 같은 증상(NaN)으로 보이기 때문에 값만으로는 구분이 안 된다.
        private static void Probe(EntityManager em, Entity defender, string label, float a, float b)
        {
            var gm = Object.FindObjectOfType<GameManager>();
            // TimeManager 는 **정적 싱글턴**이라 씬 로드와 테스트 경계를 넘어 산다. 앞 테스트가
            // 반납하지 않은 리스가 남으면 Battle 도메인이 느려지거나 멈춰 «공격이 안 나간다».
            float battleScale = Wassup.Core.TimeControl.TimeManager.Instance
                .ScaleOf(Wassup.Core.TimeControl.TimeDomain.Battle);
            Debug.Log($"[SleepProbe] {label} phase={gm?.CurrentPhase} defenderAlive={em.Exists(defender)} " +
                      $"battleScale={battleScale} sleeper={a:0.0} other={b:0.0}");
        }

        private static void DestroyIfAlive(EntityManager em, Entity a, Entity b)
        {
            if (em.Exists(a)) em.DestroyEntity(a);
            if (em.Exists(b)) em.DestroyEntity(b);
        }

        private static DefenderCatalog FindDefenderCatalog()
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

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
