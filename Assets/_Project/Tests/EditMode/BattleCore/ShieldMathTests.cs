using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 실드.
    //
    // ⚠ **실드는 시간으로 사라지지 않는다.** 만료 경로가 «구조적으로 없는 것»이 파열 판정
    // (합 &gt; 0 → 0)의 전제다 — 수명을 열면 「아무도 안 때렸는데 파열이 터진다」가 된다.
    [TestFixture]
    public class ShieldMathTests
    {
        private static SimEntityId Id(int v) => new SimEntityId(v);

        [Test]
        public void 같은_출처는_max_다른_출처는_합산이다()
        {
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, Id(1), 30f);
            ShieldMath.Merge(slots, Id(1), 10f);
            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(30f, slots[0].Value, 1e-4f, "같은 출처는 중첩하지 않는다");

            ShieldMath.Merge(slots, Id(1), 50f);
            Assert.AreEqual(50f, slots[0].Value, 1e-4f, "더 센 값은 올라간다");

            ShieldMath.Merge(slots, Id(2), 20f);
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(70f, ShieldMath.Sum(slots), 1e-4f);
        }

        [Test]
        public void 소모는_오래된_것부터다()
        {
            // 삽입 순서 = 부여 순서 = 소모 순서(E10). FIFO 라 그 순서가 곧 규칙이다.
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, Id(1), 10f);
            ShieldMath.Merge(slots, Id(2), 10f);

            Assert.AreEqual(0f, ShieldMath.Absorb(slots, 15f), 1e-4f, "둘이 합쳐 20 이라 15 는 다 막힌다");
            Assert.AreEqual(1, slots.Count, "앞 슬롯이 먼저 소진된다");
            Assert.AreEqual(Id(2), slots[0].Source);
            Assert.AreEqual(5f, slots[0].Value, 1e-4f);
        }

        [Test]
        public void 완전_흡수는_피격이_아니다()
        {
            // 관통 0 = 「맞지 않았다」. 기상·가시갑옷·피해 숫자·킬 귀속이 전부 이 분기로 갈린다.
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, Id(1), 100f);
            Assert.AreEqual(0f, ShieldMath.Absorb(slots, 40f), 1e-4f);
            Assert.AreEqual(60f, ShieldMath.Sum(slots), 1e-4f);

            Assert.AreEqual(40f, ShieldMath.Absorb(slots, 100f), 1e-4f, "넘치는 만큼만 관통한다");
            Assert.AreEqual(0, slots.Count);
        }

        [Test]
        public void 출처_키는_수명_링크가_아니다()
        {
            // F24 — 건 사람이 죽어도 잔여 실드는 산다. 「출처 id 재활용이 없다」(계약 5)가
            // 그 근거이고, 그래서 키를 수명으로 읽으면 안 된다.
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, Id(9), 25f);
            Assert.AreEqual(25f, ShieldMath.ValueFromSource(slots, Id(9)), 1e-4f);
            Assert.AreEqual(0f, ShieldMath.ValueFromSource(slots, Id(8)), 1e-4f);
        }

        [Test]
        public void 시간_만료_경로가_없다()
        {
            // 이 단언은 **API 의 부재**를 고정한다. `ShieldSlot` 에 남은 시간이 생기는 날
            // 여기서 빨개지고, 그때 `dreamcatcher-shield-break` 계약을 먼저 읽게 된다.
            var fields = typeof(ShieldSlot).GetFields();
            foreach (var f in fields)
                Assert.IsFalse(f.Name.ToLowerInvariant().Contains("remain")
                               || f.Name.ToLowerInvariant().Contains("duration"),
                    $"실드 슬롯에 수명 필드({f.Name})가 생기면 파열 판정이 무너진다");
            Assert.AreEqual(2, fields.Length, "출처와 값 둘뿐이다");
        }

        // ── 부여 관문(F20) ───────────────────────────────────────────────────

        [Test]
        public void 이미_더_센_실드가_있으면_다시_걸지도_않고_사건도_안_난다()
        {
            var m = new BattleMatch(Definition());
            var granted = Listen(m, CoreEventKind.ShieldGranted);
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            // ⚠ 사건은 **틱 끝에 배달된다**(`FlushPhase`) — 틱 밖에서 낸 것은 다음 틱에 온다.
            Assert.IsTrue(m.World.GrantShield(e.Id, Id(1), 50f, 0));
            Tick(m, 1);
            Assert.AreEqual(1, granted.Count);

            Assert.IsFalse(m.World.GrantShield(e.Id, Id(1), 20f, 0),
                "병합이 최댓값이라 무동작인데 연출만 나가면 헛발동으로 보인다");
            Tick(m, 1);
            Assert.AreEqual(1, granted.Count);

            Assert.IsTrue(m.World.GrantShield(e.Id, Id(1), 80f, 0), "더 센 것은 통한다");
            Tick(m, 1);
            Assert.AreEqual(2, granted.Count);
        }

        [Test]
        public void 부여는_한_틱_늦게_든다()
        {
            // C17 의 비대칭(unit 3 결정) — 그대로 계승한다. 뒤집으려면 별도 근거와
            // 골든 재굽기가 따로 필요하다.
            var m = new BattleMatch(Definition());
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            m.World.GrantShield(e.Id, Id(1), 40f, 0);
            Assert.AreEqual(0, e.Shield.Slots.Count, "부여한 그 자리에서는 아직 안 든다");

            Tick(m, 1);
            Assert.AreEqual(0, e.Shield.Slots.Count, "이 틱 끝에서 스테이징된다");
            Tick(m, 1);
            Assert.AreEqual(40f, ShieldMath.Sum(e.Shield.Slots), 1e-4f);
        }
    }
}
