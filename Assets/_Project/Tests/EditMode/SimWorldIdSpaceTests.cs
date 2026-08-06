using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/2 — **추적/비추적 id 공간 분리의 게이트.**
    ///
    /// ## 왜 이 분리가 필요했나
    ///
    /// 구 sim 에서 `SimEntityId` 는 **Bridge 가 7 개 스폰 경로에서 부착하는 컴포넌트**였다
    /// (unit 1: 적·방어·순찰·투사체·존해저드·차단해저드·장애물). 시스템이 ECB 로 만드는
    /// 캐리어와 영속 픽업/사직서/필드 캐리어는 **부착 대상이 아니었고**, 따라서
    /// `_simEntityIdCounter` 를 전진시키지 않았다.
    ///
    /// 신 sim 에서는 캐리어도 핸들이 필요하다. 같은 카운터에서 뽑으면 **캐리어 하나가
    /// 그 뒤 유닛의 번호를 전부 민다.** 그 번호는 장식이 아니다 —
    ///
    /// <list type="bullet">
    /// <item>타겟팅 동률 tie-break 축 (`NearestTargeting`·`Frontmost`·`LowestHealth`·`Aggro`·`Threat`·`HazardCast`)</item>
    /// <item>**발사 패턴 RNG seed** — `hash(int2(simId, fireCountBase))`</item>
    /// <item>골든 상태 해시의 엔티티 블록 키 (`entity+N` · `sim:N`)</item>
    /// </list>
    ///
    /// ⇒ 밀리면 A/B parity 가 "규칙이 틀렸다"가 아니라 **"다른 판을 돌렸다"** 로 깨지고,
    /// 그건 unit 20 에서 원인을 찾기 가장 어려운 종류의 실패다.
    /// </summary>
    public class SimWorldIdSpaceTests
    {
        private static SimWorld NewWorld() => new SimWorld(new SimConfig(1u, 1u));

        // ── 두 공간이 만나지 않는다 ──────────────────────────────────────────

        [Test]
        public void 추적_id_는_1_부터_오름차순이다()
        {
            var world = NewWorld();
            Assert.AreEqual(1, world.Create().Value);
            Assert.AreEqual(2, world.Create().Value);
            Assert.AreEqual(3, world.Create().Value);
        }

        [Test]
        public void 비추적_id_는_음수로_내려간다()
        {
            var world = NewWorld();
            Assert.AreEqual(-1, world.CreateInternal().Value);
            Assert.AreEqual(-2, world.CreateInternal().Value);
        }

        [Test]
        public void 비추적_발급이_추적_시퀀스를_밀지_않는다()
        {
            // ⚠ 이것이 이 분리의 존재 이유다. 유닛 사이에 캐리어가 아무리 끼어도
            //   유닛 번호는 0,1,2 로 이어져야 구 sim 과 같은 판이 된다.
            var world = NewWorld();
            SimEntityId u0 = world.Create();
            world.CreateInternal();
            world.CreateInternal();
            SimEntityId u1 = world.Create();
            world.CreateInternal();
            SimEntityId u2 = world.Create();

            CollectionAssert.AreEqual(new[] { 0, 1, 2 },
                new[] { u0.SpawnOrdinal, u1.SpawnOrdinal, u2.SpawnOrdinal });
        }

        [Test]
        public void 두_공간은_id_를_공유하지_않는다()
        {
            var world = NewWorld();
            var ids = new HashSet<int>();
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(ids.Add(world.Create().Value), "추적 id 중복");
                Assert.IsTrue(ids.Add(world.CreateInternal().Value), "비추적 id 중복");
            }
        }

        [Test]
        public void 카운터가_공간별로_따로_센다()
        {
            var world = NewWorld();
            world.Create();
            world.CreateInternal();
            world.CreateInternal();
            world.Create();

            Assert.AreEqual(2, world.SpawnedCount, "`simEntityIdCounter` 는 추적분만 센다");
            Assert.AreEqual(2, world.InternalSpawnedCount);
        }

        [Test]
        public void 파괴해도_두_공간_모두_재사용하지_않는다()
        {
            var world = NewWorld();
            SimEntityId a = world.Create();
            SimEntityId b = world.CreateInternal();
            world.Destroy(a);
            world.Destroy(b);

            Assert.AreNotEqual(a.Value, world.Create().Value);
            Assert.AreNotEqual(b.Value, world.CreateInternal().Value);
        }

        // ── 분류 ─────────────────────────────────────────────────────────────

        [Test]
        public void IsInternal_이_세_종류를_가른다()
        {
            var world = NewWorld();
            Assert.IsFalse(world.Create().IsInternal, "추적");
            Assert.IsTrue(world.CreateInternal().IsInternal, "비추적");
            Assert.IsFalse(SimEntityId.Null.IsInternal, "Null 은 비추적이 아니라 부재다");
            Assert.IsTrue(SimEntityId.Null.IsNull);
        }

        // ── 순회 ─────────────────────────────────────────────────────────────

        [Test]
        public void 추적_엔티티_순회는_스폰_순번_오름차순이다()
        {
            // 트레이스의 엔티티 블록이 요구하는 순서. 추적 id 는 생성 순으로 오르므로
            // `Entities()`(생성 순) 를 거르면 그대로 오름차순이 된다 — 별도 정렬이 필요 없다.
            var world = NewWorld();
            var expected = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                if (i % 2 == 0) expected.Add(world.Create().SpawnOrdinal);
                else world.CreateInternal();
            }

            List<int> traced = world.Entities().Where(e => !e.IsInternal)
                                    .Select(e => e.SpawnOrdinal).ToList();
            CollectionAssert.AreEqual(expected, traced);
            CollectionAssert.AreEqual(traced.OrderBy(v => v).ToList(), traced, "이미 정렬돼 있다");
        }

        [Test]
        public void 두_공간_모두_컴포넌트와_버퍼를_갖는다()
        {
            // 비추적이라고 저장 규칙이 다르지 않다 — 트레이스에서 빠질 뿐이다.
            var world = NewWorld();
            SimEntityId carrier = world.CreateInternal();
            world.Set(carrier, new Wassup.Sim.Combat.ProjectileRequestCarrier());
            Assert.IsTrue(world.Has<Wassup.Sim.Combat.ProjectileRequestCarrier>(carrier));

            var buf = world.AddBuffer<int>(carrier);
            Assert.IsNotNull(buf);
            buf.Add(3);
            Assert.AreEqual(1, world.GetBuffer<int>(carrier).Count);

            CollectionAssert.Contains(
                world.With<Wassup.Sim.Combat.ProjectileRequestCarrier>().ToList(), carrier);
        }

        // ── RNG seed 축 ──────────────────────────────────────────────────────

        [Test]
        public void 발사_RNG_시드는_핸들이_아니라_스폰_순번을_먹는다()
        {
            // 구: `math.hash(new int2(simId, fireCountBase))`, simId 는 0-base.
            // 핸들(1-base)을 그대로 먹이면 **모든 유닛의 난수열이 한 칸씩 밀린 판**이 된다.
            var world = NewWorld();
            SimEntityId first = world.Create();

            Assert.AreEqual(SimMath.Hash(new SimInt2(0, 4)),
                            SimMath.Hash(new SimInt2(first.SpawnOrdinal, 4)));
            Assert.AreNotEqual(SimMath.Hash(new SimInt2(first.Value, 4)),
                               SimMath.Hash(new SimInt2(first.SpawnOrdinal, 4)),
                               "두 축이 우연히 같은 해시를 내면 이 게이트는 아무것도 못 막는다");
        }
    }
}
