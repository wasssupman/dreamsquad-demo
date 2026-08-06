using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim;

// battle-sim-extraction unit 18-A — 스캐폴딩 3계약.
//
// 계획서 중단 기준 ②: **이 셋을 테스트로 고정하지 못하면 18-C 로 넘어가지 않는다.**
// 스캐폴딩 결함은 뒤 조각 전부를 오염시키고, 그때는 7,000줄이 그 위에 얹혀 있다.
namespace Wassup.Tests.EditMode
{
    public class SimWorldContractTests
    {
        struct Health { public float value; }
        struct Tag { public int unused; }
        struct Hit { public int amount; }

        /// config 는 sim 생성의 **필수 인자**다(배선 누락이 규칙 부재로 위장하는 것을 막는다).
        static SimWorld NewWorld() => new SimWorld(new SimConfig(1u, 1u));

        // ── 계약 ① id 비재사용 ────────────────────────────────────────────────

        [Test]
        public void 파괴된_id_는_재사용되지_않는다()
        {
            var w = NewWorld();
            var a = w.Create();
            w.Destroy(a);
            var b = w.Create();

            Assert.AreNotEqual(a.Value, b.Value, "재사용하면 뷰 키가 죽은 유닛의 연출을 새 유닛에 붙인다");
            Assert.IsFalse(w.Exists(a));
            Assert.IsTrue(w.Exists(b));
        }

        [Test]
        public void id_는_생성_순서로_증가하고_그것이_동률_축이다()
        {
            // unit 1 이 타겟팅 동률·RNG seed 축을 simId 로 바꿨다. 순회 순서가 그것과 어긋나면
            // 같은 상태에서 다른 답이 나온다.
            var w = NewWorld();
            var ids = Enumerable.Range(0, 8).Select(_ => w.Create()).ToList();
            CollectionAssert.AreEqual(ids.Select(i => i.Value).ToList(),
                                      ids.Select(i => i.Value).OrderBy(v => v).ToList(),
                                      "id 는 단조 증가해야 한다");
            CollectionAssert.AreEqual(ids, w.Entities().ToList(), "순회는 생성 순서다");
        }

        [Test]
        public void 중간이_파괴돼도_남은_순회_순서가_보존된다()
        {
            var w = NewWorld();
            var e = Enumerable.Range(0, 5).Select(_ => w.Create()).ToList();
            w.Destroy(e[1]);
            w.Destroy(e[3]);
            CollectionAssert.AreEqual(new[] { e[0], e[2], e[4] }, w.Entities().ToList());
        }

        [Test]
        public void Null_id_는_존재하지_않는다()
        {
            var w = NewWorld();
            Assert.IsFalse(w.Exists(SimEntityId.Null));
            Assert.IsTrue(SimEntityId.Null.IsNull);
            Assert.AreNotEqual(0, w.Create().Value, "0 은 Null 예약이라 발급되면 안 된다");
        }

        // ── 계약 ② 지연 적용 ─────────────────────────────────────────────────

        [Test]
        public void 커맨드버퍼는_Playback_전까지_세계를_바꾸지_않는다()
        {
            var w = NewWorld();
            var e = w.Create();
            var cb = new SimCommandBuffer();

            cb.Set(e, new Health { value = 10f });
            Assert.IsFalse(w.Has<Health>(e), "기록만 했는데 적용되면 '루프 중 기록, 루프 후 적용' 이 아니다");

            cb.Playback(w);
            Assert.IsTrue(w.Has<Health>(e));
            Assert.AreEqual(10f, w.Get<Health>(e).value);
            Assert.AreEqual(0, cb.Count, "Playback 은 버퍼를 비운다");
        }

        [Test]
        public void 같은_엔티티_2연산은_기록_순서대로_적용된다()
        {
            // `ModifierApplySystem` 선례 — 같은 엔티티에 add→remove 가 쌓이는 함정.
            var w = NewWorld();
            var e = w.Create();
            var cb = new SimCommandBuffer();

            cb.Set(e, new Health { value = 1f });
            cb.Set(e, new Health { value = 2f });
            cb.RemoveComponent<Health>(e);
            cb.Playback(w);

            Assert.IsFalse(w.Has<Health>(e), "마지막 연산이 이긴다");
        }

        [Test]
        public void 순회_중_기록한_파괴가_그_순회를_망가뜨리지_않는다()
        {
            var w = NewWorld();
            var e = Enumerable.Range(0, 4).Select(_ => w.Create()).ToList();
            foreach (var x in e) w.Set(x, new Tag());
            var cb = new SimCommandBuffer();

            var seen = new List<SimEntityId>();
            foreach (var x in w.With<Tag>()) { seen.Add(x); cb.Destroy(x); }
            Assert.AreEqual(4, seen.Count, "순회는 파괴 기록에 영향받지 않는다");

            cb.Playback(w);
            Assert.AreEqual(0, w.AliveCount);
        }

        // ── 계약 ③ 지연 채널 순서 ────────────────────────────────────────────

        [Test]
        public void 소비자가_생산자보다_앞이면_1틱_지연이_구조적으로_생긴다()
        {
            // 청사진 ③ §2 의 AggroHit — "선언 없음, 구조가 보장". 플래그가 아니라 phase 순서다.
            var w = NewWorld();
            var ch = new SimChannel<Hit>();
            var consumed = new List<int>();
            var tick = new SimTick();

            tick.Register(SimPhase.Intake, _ => consumed.AddRange(ch.Drain().Select(h => h.amount)));
            tick.Register(SimPhase.Attack, _ => ch.Enqueue(new Hit { amount = 7 }));

            tick.Run(w, 0.016f);
            CollectionAssert.IsEmpty(consumed, "1틱차: 생산이 소비 뒤라 이번 틱엔 안 보인다");
            tick.Run(w, 0.016f);
            CollectionAssert.AreEqual(new[] { 7 }, consumed, "2틱차에 소비된다");
        }

        [Test]
        public void 생산자가_소비자보다_앞이면_같은_틱에_소비된다()
        {
            // 같은 채널에 같은틱·지연 생산자가 공존한다(`StatModifierApply` 10 producer).
            var w = NewWorld();
            var ch = new SimChannel<Hit>();
            var consumed = new List<int>();
            var tick = new SimTick();

            tick.Register(SimPhase.FieldsAndPeriodic, _ => ch.Enqueue(new Hit { amount = 1 }));
            tick.Register(SimPhase.Intake, _ => consumed.AddRange(ch.Drain().Select(h => h.amount)));
            tick.Register(SimPhase.Attack, _ => ch.Enqueue(new Hit { amount = 9 }));

            tick.Run(w, 0.016f);
            CollectionAssert.AreEqual(new[] { 1 }, consumed, "앞선 생산자만 같은 틱에 잡힌다");
            tick.Run(w, 0.016f);
            CollectionAssert.AreEqual(new[] { 1, 9, 1 }, consumed, "지연분이 다음 틱 앞머리에 온다");
        }

        [Test]
        public void 드레인은_통째로_비운다_부분소비는_계약위반이다()
        {
            var ch = new SimChannel<Hit>();
            ch.Enqueue(new Hit { amount = 1 });
            ch.Enqueue(new Hit { amount = 2 });
            Assert.AreEqual(2, ch.Drain().Count);
            Assert.AreEqual(0, ch.Count, "남으면 다음 틱 분과 섞여 순서가 무너진다");
        }

        [Test]
        public void 드레인_중_같은_채널_적재는_다음_드레인_몫이다()
        {
            var ch = new SimChannel<Hit>();
            ch.Enqueue(new Hit { amount = 1 });
            var first = ch.Drain().ToList();
            ch.Enqueue(new Hit { amount = 2 });
            CollectionAssert.AreEqual(new[] { 1 }, first.Select(h => h.amount).ToList());
            CollectionAssert.AreEqual(new[] { 2 }, ch.Drain().Select(h => h.amount).ToList());
        }

        // ── phase 골격 ────────────────────────────────────────────────────────

        [Test]
        public void phase_순서가_캡처_접기와_같다()
        {
            // 직관으로 고치고 싶어지는 3지점을 명시로 못박는다(캡처가 정본).
            var o = SimTick.PhaseOrder;
            Assert.Less(o.ToList().IndexOf(SimPhase.Projectiles), o.ToList().IndexOf(SimPhase.Attack),
                "투사체가 공격보다 앞");
            Assert.Less(o.ToList().IndexOf(SimPhase.PreCombat), o.ToList().IndexOf(SimPhase.Movement),
                "DotApply(P3)가 이동(P4) 앞");
            Assert.Less(o.ToList().IndexOf(SimPhase.DeathWindow), o.ToList().IndexOf(SimPhase.PostProcess),
                "CC 감쇠(P11)는 사망 창(P10) 뒤");
            Assert.AreEqual(15, o.Count, "P1~P12 + P0 두 조각 + P13 (18-K/3)");
        }

        // ── P0 / P13 (18-K/3) ────────────────────────────────────────────────

        [Test]
        public void P0_두_조각과_P13_이_양_끝을_감싼다()
        {
            var o = SimTick.PhaseOrder.ToList();
            Assert.AreEqual(SimPhase.CommandIntake, o[0]);
            Assert.AreEqual(SimPhase.FramePrologue, o[1]);
            Assert.AreEqual(SimPhase.PostSim, o[o.Count - 1]);
            Assert.AreEqual(SimPhase.FieldsAndPeriodic, o[2], "P1 은 P0 두 조각 뒤");
            Assert.AreEqual(SimPhase.Destruction, o[o.Count - 2], "P12 는 P13 앞");
        }

        [Test]
        public void 시계는_커맨드_반입_뒤_sim_그룹_앞에서_움직인다()
        {
            // ⚠ 구 `StepOneTick`: 커맨드 → `AdvanceBattleFrame`(첫 줄이 `_battleClock += dt`) → sim.
            //   P1~P12 는 **이번 틱이 더해진** 시계를 본다. 뒤로 옮기면 규칙이 한 틱 밀린다.
            var w = NewWorld();
            var seen = new List<double>();
            var tick = new SimTick();
            tick.Register(SimPhase.CommandIntake, x => seen.Add(x.BattleClock));
            tick.Register(SimPhase.FramePrologue, x => seen.Add(x.BattleClock));
            tick.Register(SimPhase.Attack, x => seen.Add(x.BattleClock));
            tick.Register(SimPhase.PostSim, x => seen.Add(x.BattleClock));

            tick.Run(w, 0.5f);
            CollectionAssert.AreEqual(new[] { 0.0, 0.5, 0.5, 0.5 }, seen,
                "커맨드만 전진 전 시계를 본다");
            Assert.AreEqual(0.5, w.BattleClock);
        }

        [Test]
        public void 시계는_double_로_누적한다()
        {
            // ⚠ 구가 `double` 로 누적하고 읽을 때만 내린다. `float` 누적은 긴 판에서 갈리고
            //   그 값은 **상태 해시의 첫 줄**이다.
            var w = NewWorld();
            var tick = new SimTick();
            for (int i = 0; i < 1000; i++) tick.Run(w, 0.1f);

            double asDouble = 0.0;
            float asFloat = 0f;
            for (int i = 0; i < 1000; i++) { asDouble += 0.1f; asFloat += 0.1f; }
            Assert.AreEqual(asDouble, w.BattleClock, "double 누적과 비트까지 같다");
            Assert.AreNotEqual((double)asFloat, w.BattleClock, "float 누적과는 갈린다 — 그래서 double 이다");
        }

        [Test]
        public void 틱_번호는_틱_끝에서_오르고_0_부터_센다()
        {
            var w = NewWorld();
            var seen = new List<int>();
            var tick = new SimTick();
            tick.Register(SimPhase.PostSim, x => seen.Add(x.Tick));

            Assert.AreEqual(0, w.Tick, "구 `_harnessTick` 은 0 에서 시작한다");
            tick.Run(w, 0.016f);
            tick.Run(w, 0.016f);
            CollectionAssert.AreEqual(new[] { 0, 1 }, seen, "P13 스탬프는 **이번 틱** 번호로 찍힌다");
            Assert.AreEqual(2, w.Tick, "실행한 틱 수");
        }

        [Test]
        public void 이벤트_귀속은_P0_가_직전틱_P13_이_이번틱이다()
        {
            // 구 sim 의 16채널(`tick-1`) / 2채널(`tick`) 이원화가 이 두 자리의 차이다.
            var w = NewWorld();
            Assert.AreEqual(-1, w.PreSimEventTick, "⚠ 첫 틱의 P0 드레인은 -1 에 귀속된다");
            Assert.AreEqual(0, w.PostSimEventTick);

            new SimTick().Run(w, 0.016f);
            Assert.AreEqual(0, w.PreSimEventTick);
            Assert.AreEqual(1, w.PostSimEventTick);
        }

        [Test]
        public void 같은_phase_안에서는_등록_순서가_실행_순서다()
        {
            var w = NewWorld();
            var log = new List<int>();
            var tick = new SimTick();
            tick.Register(SimPhase.Intake, _ => log.Add(1));
            tick.Register(SimPhase.Intake, _ => log.Add(2));
            tick.Register(SimPhase.FieldsAndPeriodic, _ => log.Add(0));
            tick.Run(w, 0.016f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, log);
        }

        [Test]
        public void 부재와_빈_버퍼는_다른_상태다()
        {
            // `DamageApplication` 게이트는 버퍼 **부재**만 본다(청사진 ② 함의 보존 3건).
            var w = NewWorld();
            var e = w.Create();
            Assert.IsFalse(w.HasBuffer<Hit>(e));
            Assert.IsNull(w.GetBuffer<Hit>(e), "조회가 자동 생성하면 부재 상태가 사라진다");

            w.AddBuffer<Hit>(e);
            Assert.IsTrue(w.HasBuffer<Hit>(e));
            Assert.AreEqual(0, w.GetBuffer<Hit>(e).Count, "빈 버퍼는 부재가 아니다");
        }

        /// <summary>
        /// **박싱 비교 경로**를 지킨다. `Dictionary`/`List.Contains` 는
        /// `EqualityComparer&lt;T&gt;.Default` → `IEquatable` 로 가므로 이 경로를 **밟지 않는다** —
        /// 그래서 `Equals(object)` 가 무한 재귀여도 스위트 전체가 초록일 수 있다(실제로 그랬다).
        ///
        /// 이 경로가 처음 밟히는 곳은 직렬화·리플렉션·비제네릭 컬렉션, 즉 **엔진 밖 호스팅**이다.
        /// 거기서 터지면 `StackOverflowException` 이라 catch 도 못 하고 진행 중인 판이 사라진다.
        ///
        /// 값 타입 4종을 한 자리에서 본다 — 넷이 같은 관용구를 쓰고, 하나만 틀려도 나머지가
        /// 맞다는 사실이 오히려 눈을 가린다.
        /// </summary>
        [Test]
        public void ValueTypes_SurviveBoxedEquality()
        {
            object a = new SimEntityId(7);
            object same = new SimEntityId(7);
            object other = new SimEntityId(8);
            Assert.IsTrue(a.Equals(same));
            Assert.IsFalse(a.Equals(other));
            Assert.IsFalse(a.Equals("not an id"), "다른 타입은 false — 던지지도, 재귀하지도 않는다");
            Assert.IsFalse(a.Equals(null));

            object v3 = new SimVec3(1f, 2f, 3f);
            Assert.IsTrue(v3.Equals(new SimVec3(1f, 2f, 3f)));
            Assert.IsFalse(v3.Equals(new SimVec3(1f, 2f, 4f)));

            object v2 = new SimVec2(1f, 2f);
            Assert.IsTrue(v2.Equals(new SimVec2(1f, 2f)));
            Assert.IsFalse(v2.Equals(new SimVec2(1f, 3f)));

            object i2 = new SimInt2(3, 4);
            Assert.IsTrue(i2.Equals(new SimInt2(3, 4)));
            Assert.IsFalse(i2.Equals(new SimInt2(3, 5)));
        }

        /// 해시 계약 — 같은 값이면 같은 해시여야 `Dictionary` 키로 쓸 수 있다.
        [Test]
        public void ValueTypes_HashAgreesWithEquality()
        {
            Assert.AreEqual(new SimEntityId(7).GetHashCode(), new SimEntityId(7).GetHashCode());
            Assert.AreEqual(new SimVec3(1f, 2f, 3f).GetHashCode(), new SimVec3(1f, 2f, 3f).GetHashCode());
            Assert.AreEqual(new SimVec2(1f, 2f).GetHashCode(), new SimVec2(1f, 2f).GetHashCode());
            Assert.AreEqual(new SimInt2(3, 4).GetHashCode(), new SimInt2(3, 4).GetHashCode());
        }
    }
}
