using NUnit.Framework;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender unit 10 — idle 변형 선택 회귀.
    //
    // 이 함수가 지켜야 하는 것은 두 가지뿐이다:
    //   (1) 반환 인덱스가 항상 유효하다 (0 <= i < count, 또는 변형 없음 -1)
    //   (2) 변형이 2개 이상이면 직전과 같은 것을 연속으로 주지 않는다
    // (2)가 깨지면 3종을 저작해 둬도 화면에선 "안 바뀐다"로 읽힌다.
    public class UnitAnimationChoiceTests
    {
        [Test]
        public void NoVariants_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, UnitAnimationChoice.ChooseNext(0, -1, 0.5f));
            Assert.AreEqual(-1, UnitAnimationChoice.ChooseNext(-3, 0, 0.5f));
        }

        [Test]
        public void SingleVariant_AlwaysZero_EvenWhenItIsCurrent()
        {
            // 선택지가 하나면 연속 회피가 불가능하다 — 그대로 반복하는 것이 맞다.
            Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(1, -1, 0f));
            Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(1, 0, 0.99f));
        }

        [Test]
        public void FirstEntry_NoCurrent_SpansWholeRange()
        {
            Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(3, -1, 0f));
            Assert.AreEqual(1, UnitAnimationChoice.ChooseNext(3, -1, 0.5f));
            Assert.AreEqual(2, UnitAnimationChoice.ChooseNext(3, -1, 0.99f));
        }

        [Test]
        public void TwoVariants_AlwaysAlternates()
        {
            for (int i = 0; i <= 20; i++)
            {
                float roll = i / 20f;
                Assert.AreEqual(1, UnitAnimationChoice.ChooseNext(2, 0, roll), $"roll={roll}");
                Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(2, 1, roll), $"roll={roll}");
            }
        }

        [Test]
        public void ThreeVariants_NeverRepeatsCurrent_AndStaysInRange()
        {
            for (int current = 0; current < 3; current++)
            for (int i = 0; i <= 40; i++)
            {
                float roll = i / 40f;
                int next = UnitAnimationChoice.ChooseNext(3, current, roll);
                Assert.AreNotEqual(current, next, $"current={current} roll={roll} 가 자기 자신을 다시 뽑았다");
                Assert.GreaterOrEqual(next, 0);
                Assert.Less(next, 3);
            }
        }

        [Test]
        public void ThreeVariants_BothAlternativesAreReachable()
        {
            // 연속 회피가 "항상 같은 하나로 도망가는" 것으로 퇴화하지 않는지.
            bool sawLow = false, sawHigh = false;
            for (int i = 0; i <= 40; i++)
            {
                int next = UnitAnimationChoice.ChooseNext(3, 1, i / 40f);
                if (next == 0) sawLow = true;
                if (next == 2) sawHigh = true;
            }
            Assert.IsTrue(sawLow && sawHigh, "current=1 에서 0 과 2 가 모두 나와야 한다");
        }

        [Test]
        public void RollAtOrAboveOne_DoesNotOverflow()
        {
            // UnityEngine.Random.value 는 1.0 을 포함한다 — 클램프가 없으면 여기서 터진다.
            Assert.Less(UnitAnimationChoice.ChooseNext(3, -1, 1f), 3);
            Assert.Less(UnitAnimationChoice.ChooseNext(3, 0, 1f), 3);
            Assert.Less(UnitAnimationChoice.ChooseNext(2, 1, 1f), 2);
        }

        [Test]
        public void NegativeRoll_IsTreatedAsZero()
        {
            Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(3, -1, -0.5f));
        }

        [Test]
        public void CurrentOutOfRange_IsTreatedAsNoCurrent()
        {
            // 변형 목록이 런타임에 짧아진 경우에도 유효 인덱스를 낸다.
            Assert.AreEqual(0, UnitAnimationChoice.ChooseNext(2, 7, 0f));
            Assert.AreEqual(1, UnitAnimationChoice.ChooseNext(2, 7, 0.9f));
        }
    }
}
