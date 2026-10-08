// 적응: Assets/_Project/Tests/EditMode/Battle/AgentCollisionTests.cs (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class AgentCollisionTests
    {
        private const float R = 0.25f;

        private static NavGrid WallAt(int2 wall, int w = 5, int h = 5)
        {
            var walk = new byte[w * h];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            walk[wall.y * w + wall.x] = 0;
            return CoreMapFixtures.NavOf(walk, w, h);
        }

        [Test]
        public void 벽이_없으면_요청한_자리로_간다()
        {
            var nav = CoreMapFixtures.FlatNav(5, 5);
            var to = new float3(2.3f, 0f, 2f);
            Assert.AreEqual(to.x, AgentCollision.Resolve(new float3(2f, 0f, 2f), to, R, in nav).x, 1e-5f);
        }

        [Test]
        public void 벽에_정면으로_가면_면_앞에_선다()
        {
            var nav = WallAt(new int2(3, 2));
            var next = AgentCollision.Resolve(new float3(2f, 0f, 2f), new float3(2.8f, 0f, 2f), R, in nav);

            // 벽 칸 (3,2) 의 진입면은 x = 2.5 — 몸 반지름과 skin 만큼 앞에서 멈춘다.
            Assert.Less(next.x, 2.5f - R + 1e-3f);
            Assert.Greater(next.x, 2f, "뒤로 튕기지 않는다");
        }

        [Test]
        public void 한_축이_막히면_다른_축으로_미끄러진다()
        {
            var nav = WallAt(new int2(3, 2));
            var next = AgentCollision.Resolve(new float3(2f, 0f, 2f), new float3(2.8f, 0f, 2.2f), R, in nav);

            Assert.Greater(next.z, 2f, "자유 축은 계속 간다");
        }

        [Test]
        public void 막힌_성분은_접선으로_재분배된다()
        {
            // 실측 사고: 방향이 거의 순수 벽 법선이면 실이동이 정상 속도의 1.5% 로 붕괴했다.
            // 느린 원인은 충돌이 아니라 「애초에 접선 성분을 거의 요청하지 않은 것」이다.
            var nav = WallAt(new int2(3, 2));
            var from = new float3(2f, 0f, 2f);
            var want = new float3(2.2f, 0f, 2.002f);   // 거의 순수 법선
            var next = AgentCollision.Resolve(from, want, R, in nav);

            float got = math.length(new float2(next.x - from.x, next.z - from.z));
            float requested = math.length(new float2(want.x - from.x, want.z - from.z));
            Assert.Greater(got, requested * 0.5f, "접선으로 재분배돼 속도가 살아야 한다");
            Assert.LessOrEqual(got, requested + 1e-4f, "요청량을 넘지 않는다");
        }

        [Test]
        public void 스윕이_벽_한_칸을_건너뛰지_못하게_한다()
        {
            // 최종 위치만 검사하면 중간 칸을 건너뛴다 — 칸 경계에서 전속으로 밀리면 가장자리가
            // 최대 `0.5 + 0.9 + r` 칸까지 가서 벽 너머 빈 칸에 도달할 수 있다.
            var nav = WallAt(new int2(3, 2), 6, 5);
            var next = AgentCollision.Resolve(new float3(2.4f, 0f, 2f), new float3(3.9f, 0f, 2f), R, in nav);
            Assert.Less(next.x, 2.5f, "벽 앞에서 멈춘다");
        }

        [Test]
        public void 코너_조준_오프셋은_충돌_여유와_같은_값을_쓴다()
        {
            // M12 — 갈리면 「조준한 자리에 실제로 설 수 없다」가 된다.
            var nav = WallAt(new int2(2, 2));
            Assert.IsTrue(PathSmoothing.TryCornerAim(new int2(2, 2), new float3(1f, 0f, 3f),
                                                     new float3(1f, 0f, 1f), R, in nav, out float3 aim));
            // 조준점은 벽 칸 중심에서 (칸 반폭 + 반지름 + skin) 만큼 밖이다.
            float expected = 0.5f + R + AgentCollision.Skin;
            Assert.AreEqual(expected, math.abs(aim.x - 2f), 1e-4f, "x 축");
            Assert.AreEqual(expected, math.abs(aim.z - 2f), 1e-4f, "z 축");
        }
    }
}
