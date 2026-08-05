using NUnit.Framework;
using Wassup.Sim.Match;

// battle-sim-extraction unit 15-C-2 — 인접 시너지 판정.
//
// 적출 전에는 이 규칙을 확인하려면 `World` + `EntityManager` + `BattleBridge` 를 세우고 실제로
// 디펜더를 배치해야 했다(이웃 세기가 ECS 조회와 한 덩어리였다). 이제 타입 키 창 하나로 단정한다.
//
// 골든은 이 규칙의 증인이 **아니다** — 하네스는 유닛 타입마다 1회만 배치해서 같은 종류가 인접하는
// 배치를 만들지 않는다. 즉 시너지 배율은 코퍼스에 사실상 나타나지 않는다. 그래서 여기가 유일한 증인이다.
namespace Wassup.Tests.EditMode
{
    public class MatchSynergyRulesTests
    {
        const int A = 101;   // 타입 키(호출자는 SO 인스턴스 ID 를 쓴다 — 값 자체는 의미 없고 동일성만 계약)
        const int B = 202;
        const int None = 0;

        static int[] EmptyWindow() => new int[MatchSynergyRules.WindowSize];

        static void Put(int[] window, int dx, int dy, int key)
            => window[MatchSynergyRules.WindowIndex(dx, dy)] = key;

        static int[] Count(int[] window)
        {
            var into = new int[MatchSynergyRules.BlockSize];
            MatchSynergyRules.CountBlock(window, into);
            return into;
        }

        /// 블록 인덱스 → 오프셋 역인덱스. 테스트가 "가운데 칸의 결과" 를 오프셋으로 찾게 한다.
        static int BlockIndexOf(int dx, int dy)
        {
            for (int i = 0; i < MatchSynergyRules.BlockSize; i++)
            {
                (int ox, int oy) = MatchSynergyRules.BlockOffset(i);
                if (ox == dx && oy == dy) return i;
            }
            Assert.Fail($"({dx},{dy}) 는 재계산 블록에 없다");
            return -1;
        }

        [Test]
        public void 빈_창이면_아홉_칸_모두_미점유()
        {
            var counts = Count(EmptyWindow());
            for (int i = 0; i < MatchSynergyRules.BlockSize; i++)
                Assert.AreEqual(MatchSynergyRules.Unoccupied, counts[i], $"block[{i}]");
        }

        [Test]
        public void 홀로_선_디펜더는_이웃_0_이고_미점유와_구분된다()
        {
            var w = EmptyWindow();
            Put(w, 0, 0, A);

            var counts = Count(w);
            Assert.AreEqual(0, counts[BlockIndexOf(0, 0)], "점유돼 있고 이웃이 없다");
            Assert.AreEqual(MatchSynergyRules.Unoccupied, counts[BlockIndexOf(1, 0)], "빈 칸은 미점유");
        }

        [Test]
        public void 같은_종류_인접만_센다()
        {
            var w = EmptyWindow();
            Put(w, 0, 0, A);
            Put(w, 1, 0, A);    // 같은 종류
            Put(w, -1, 0, B);   // 다른 종류 — 세지 않는다

            var counts = Count(w);
            Assert.AreEqual(1, counts[BlockIndexOf(0, 0)]);
            Assert.AreEqual(1, counts[BlockIndexOf(1, 0)]);
            Assert.AreEqual(0, counts[BlockIndexOf(-1, 0)], "B 는 이웃에 B 가 없다");
        }

        [Test]
        public void 대각선도_이웃이다()
        {
            var w = EmptyWindow();
            Put(w, 0, 0, A);
            Put(w, 1, 1, A);

            var counts = Count(w);
            Assert.AreEqual(1, counts[BlockIndexOf(0, 0)]);
        }

        [Test]
        public void 여덟_방향_전부_채우면_이웃_여덟()
        {
            var w = EmptyWindow();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                Put(w, dx, dy, A);

            var counts = Count(w);
            Assert.AreEqual(8, counts[BlockIndexOf(0, 0)]);
        }

        [Test]
        public void 창_가장자리_이웃도_센다_반경2_까지_읽는다()
        {
            // 블록 끝 칸(1,1)의 이웃은 중심에서 ±2 까지 뻗는다. 창이 3×3 이었다면 놓쳤을 자리.
            var w = EmptyWindow();
            Put(w, 1, 1, A);
            Put(w, 2, 2, A);

            var counts = Count(w);
            Assert.AreEqual(1, counts[BlockIndexOf(1, 1)], "(2,2) 는 (1,1)의 대각 이웃이다");
            Assert.AreEqual(MatchSynergyRules.Unoccupied, counts[BlockIndexOf(0, 0)]);
        }

        [Test]
        public void 자기_자신은_이웃에_넣지_않는다()
        {
            var w = EmptyWindow();
            Put(w, 0, 0, A);
            Put(w, 0, 1, A);

            var counts = Count(w);
            Assert.AreEqual(1, counts[BlockIndexOf(0, 0)], "자기 칸을 세면 2 가 나온다");
        }

        [Test]
        public void 블록_순회_순서가_계약이다()
        {
            // 이 순서가 곧 모디파이어 채널 enqueue 순서이고 골든의 StatModifierSlot 라인에 실린다.
            var expected = new[]
            {
                (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1),
                (1, 1), (-1, 1), (1, -1), (-1, -1),
            };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], MatchSynergyRules.BlockOffset(i), $"block[{i}]");
        }

        [Test]
        public void 배율은_이웃당_가산이고_이웃_0_은_곱셈_항등()
        {
            Assert.AreEqual(1f, MatchSynergyRules.Multiplier(0, 0.1f), 1e-6f);
            Assert.AreEqual(1.1f, MatchSynergyRules.Multiplier(1, 0.1f), 1e-6f);
            Assert.AreEqual(1.8f, MatchSynergyRules.Multiplier(8, 0.1f), 1e-6f);
        }

        [Test]
        public void 미점유는_배율_계산에_들어가지_않는다()
        {
            // Unoccupied(-1) 를 실수로 그대로 흘려도 항등으로 접힌다 — 호출자가 먼저 걸러야 하지만
            // 규칙이 음수에서 1 미만 배율(= 피해 감소)을 만들지는 않는다.
            Assert.AreEqual(1f, MatchSynergyRules.Multiplier(MatchSynergyRules.Unoccupied, 0.1f), 1e-6f);
        }
    }
}
