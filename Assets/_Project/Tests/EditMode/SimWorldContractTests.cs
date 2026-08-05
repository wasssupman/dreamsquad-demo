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

        // ── 계약 ① id 비재사용 ────────────────────────────────────────────────

        [Test]
        public void 파괴된_id_는_재사용되지_않는다()
        {
            var w = new SimWorld();
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
            var w = new SimWorld();
            var ids = Enumerable.Range(0, 8).Select(_ => w.Create()).ToList();
            CollectionAssert.AreEqual(ids.Select(i => i.Value).ToList(),
                                      ids.Select(i => i.Value).OrderBy(v => v).ToList(),
                                      "id 는 단조 증가해야 한다");
            CollectionAssert.AreEqual(ids, w.Entities().ToList(), "순회는 생성 순서다");
        }

        [Test]
        public void 중간이_파괴돼도_남은_순회_순서가_보존된다()
        {
            var w = new SimWorld();
            var e = Enumerable.Range(0, 5).Select(_ => w.Create()).ToList();
            w.Destroy(e[1]);
            w.Destroy(e[3]);
            CollectionAssert.AreEqual(new[] { e[0], e[2], e[4] }, w.Entities().ToList());
        }

        [Test]
        public void Null_id_는_존재하지_않는다()
        {
            var w = new SimWorld();
            Assert.IsFalse(w.Exists(SimEntityId.Null));
            Assert.IsTrue(SimEntityId.Null.IsNull);
            Assert.AreNotEqual(0, w.Create().Value, "0 은 Null 예약이라 발급되면 안 된다");
        }

        // ── 계약 ② 지연 적용 ─────────────────────────────────────────────────

        [Test]
        public void 커맨드버퍼는_Playback_전까지_세계를_바꾸지_않는다()
        {
            var w = new SimWorld();
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
            var w = new SimWorld();
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
            var w = new SimWorld();
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
            var w = new SimWorld();
            var ch = new SimChannel<Hit>();
            var consumed = new List<int>();
            var tick = new SimTick();

            tick.Register(SimPhase.Intake, _ => consumed.AddRange(ch.Drain().Select(h => h.amount)));
            tick.Register(SimPhase.Attack, _ => ch.Enqueue(new Hit { amount = 7 }));

            tick.Run(w);
            CollectionAssert.IsEmpty(consumed, "1틱차: 생산이 소비 뒤라 이번 틱엔 안 보인다");
            tick.Run(w);
            CollectionAssert.AreEqual(new[] { 7 }, consumed, "2틱차에 소비된다");
        }

        [Test]
        public void 생산자가_소비자보다_앞이면_같은_틱에_소비된다()
        {
            // 같은 채널에 같은틱·지연 생산자가 공존한다(`StatModifierApply` 10 producer).
            var w = new SimWorld();
            var ch = new SimChannel<Hit>();
            var consumed = new List<int>();
            var tick = new SimTick();

            tick.Register(SimPhase.FieldsAndPeriodic, _ => ch.Enqueue(new Hit { amount = 1 }));
            tick.Register(SimPhase.Intake, _ => consumed.AddRange(ch.Drain().Select(h => h.amount)));
            tick.Register(SimPhase.Attack, _ => ch.Enqueue(new Hit { amount = 9 }));

            tick.Run(w);
            CollectionAssert.AreEqual(new[] { 1 }, consumed, "앞선 생산자만 같은 틱에 잡힌다");
            tick.Run(w);
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
            Assert.AreEqual(12, o.Count);
        }

        [Test]
        public void 같은_phase_안에서는_등록_순서가_실행_순서다()
        {
            var w = new SimWorld();
            var log = new List<int>();
            var tick = new SimTick();
            tick.Register(SimPhase.Intake, _ => log.Add(1));
            tick.Register(SimPhase.Intake, _ => log.Add(2));
            tick.Register(SimPhase.FieldsAndPeriodic, _ => log.Add(0));
            tick.Run(w);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, log);
        }

        [Test]
        public void 부재와_빈_버퍼는_다른_상태다()
        {
            // `DamageApplication` 게이트는 버퍼 **부재**만 본다(청사진 ② 함의 보존 3건).
            var w = new SimWorld();
            var e = w.Create();
            Assert.IsFalse(w.HasBuffer<Hit>(e));
            Assert.IsNull(w.GetBuffer<Hit>(e), "조회가 자동 생성하면 부재 상태가 사라진다");

            w.AddBuffer<Hit>(e);
            Assert.IsTrue(w.HasBuffer<Hit>(e));
            Assert.AreEqual(0, w.GetBuffer<Hit>(e).Count, "빈 버퍼는 부재가 아니다");
        }
    }
}
