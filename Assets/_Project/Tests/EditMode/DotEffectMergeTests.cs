using NUnit.Framework;
using Unity.Entities;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // dot-effect-extraction unit 0 — 지속 피해 병합 정책을 표로 고정한다.
    //
    // 핵심 계약: **키(origin, element)가 다르면 슬롯이 갈린다.** 한 슬롯이던 시절엔 나중에 온 도트가
    // scalar·tickInterval 을 덮어쓰고 remainingTime 만 max 로 남아, 출혈 중인 적이 화염 장판을
    // 밟으면 장판을 나가도 장판 요율로 4.85초를 더 타는 과피해가 났다.
    public class DotEffectMergeTests
    {
        private World _world;
        private EntityManager _em;
        private Entity _e;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DotEffectMergeTestWorld");
            _em = _world.EntityManager;
            _e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(_e);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void Apply(DotEffect incoming)
        {
            var buffer = _em.GetBuffer<DotEffect>(_e); // 버퍼는 재할당될 수 있어 매번 재획득
            DotEffectMerge.Apply(ref buffer, incoming);
        }

        // 출혈 = 스택 임계 파생 / 화염 = 해저드 장판. 축 두 개가 다 다른 실제 조합.
        private static DotEffect Bleed => new DotEffect
        { origin = DotOrigin.Stack, element = DotElement.Bleed, scalar = 5f, tickInterval = 0.5f, remainingTime = 4.85f };

        private static DotEffect Fire => new DotEffect
        { origin = DotOrigin.Zone, element = DotElement.Fire, scalar = 10f, tickInterval = 0.25f, remainingTime = 0.2f };

        [Test]
        public void DifferentKeys_GetSeparateSlots()
        {
            Apply(Bleed);
            Apply(Fire);

            var buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(2, buffer.Length, "키가 다르면 슬롯이 갈려야 한다");

            // 각자 자기 값을 온전히 유지한다 — 이것이 과피해가 사라지는 이유다.
            var bleed = buffer[0].element == DotElement.Bleed ? buffer[0] : buffer[1];
            var fire  = buffer[0].element == DotElement.Fire  ? buffer[0] : buffer[1];
            Assert.AreEqual(5f, bleed.scalar, 1e-4f);
            Assert.AreEqual(0.5f, bleed.tickInterval, 1e-4f);
            Assert.AreEqual(4.85f, bleed.remainingTime, 1e-4f);
            Assert.AreEqual(10f, fire.scalar, 1e-4f);
            Assert.AreEqual(0.25f, fire.tickInterval, 1e-4f);
            Assert.AreEqual(0.2f, fire.remainingTime, 1e-4f, "짧은 장판 지속이 출혈 지속으로 늘지 않는다");
        }

        [Test]
        public void SameKey_Merges_WithMaxRemaining()
        {
            Apply(Bleed);
            var refreshed = Bleed;
            refreshed.remainingTime = 1f; // 더 짧은 갱신이 와도 남은 지속이 줄지 않아야 한다
            Apply(refreshed);

            var buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(4.85f, buffer[0].remainingTime, 1e-4f);
        }

        [Test]
        public void SameElement_DifferentOrigin_GetsSeparateSlot()
        {
            // 설계의 핵심: 장판이 준 화염과 화염 스택 임계가 터뜨린 화염은 **그림은 같지만
            // 다른 파이프라인**이라 서로를 덮으면 안 된다. element 만으로 키를 잡으면 여기서
            // 다시 과피해가 난다.
            Apply(Fire);                                   // Zone · Fire
            Apply(new DotEffect { origin = DotOrigin.Stack, element = DotElement.Fire,
                                  scalar = 4f, tickInterval = 1f, remainingTime = 3f });

            var buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(2, buffer.Length, "같은 원소라도 origin 이 다르면 각자 탄다");
        }

        [Test]
        public void SameOrigin_NoElement_MergesTogether()
        {
            // 원소 없는 배치 도트(버스터즈)끼리는 한 슬롯 — 이관 전 동작 유지.
            Apply(new DotEffect { origin = DotOrigin.OnPlace, scalar = 7f, tickInterval = 0.2f, remainingTime = 2f });
            Apply(new DotEffect { origin = DotOrigin.OnPlace, scalar = 3f, tickInterval = 0.2f, remainingTime = 1f });

            var buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(3f, buffer[0].scalar, 1e-4f, "같은 슬롯이면 나중 값이 덮는다");
        }

        [Test]
        public void AddPath_FiresFirstTickImmediately()
        {
            Apply(Bleed);
            var buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(0.5f, buffer[0].tickTimer, 1e-4f,
                "신규 슬롯은 tickTimer = tickInterval 로 들어가 첫 틱이 즉발이어야 한다");
        }

        [Test]
        public void Merge_PreservesTickTimer_AndRescalesOnIntervalChange()
        {
            Apply(Bleed);
            // 누적기를 인위적으로 진행시킨 뒤 갱신이 와도 리셋되면 안 된다(존 refresh 시나리오).
            var buffer = _em.GetBuffer<DotEffect>(_e);
            var slot = buffer[0];
            slot.tickTimer = 0.25f; // 주기 0.5 의 50% 진행
            buffer[0] = slot;

            var faster = Bleed;
            faster.tickInterval = 0.25f;
            Apply(faster);

            buffer = _em.GetBuffer<DotEffect>(_e);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(0.125f, buffer[0].tickTimer, 1e-4f,
                "주기가 바뀌면 진행률(50%)을 보존해 환산해야 한다 — 그대로 넘기면 조기 발동");
        }
    }
}
