using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Core.TimeControl;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — **슬로모·정지는 규칙이 아니라 틱 발행률이다**(계약 5).
    //
    // 코어의 `dt` 는 언제나 1/60 이고, 느려지는 것은 「몇 번 부르나」뿐이다. 이 테스트가
    // 막는 것은 누군가 `Time.timeScale` 이나 가변 dt 로 되돌리는 날이다 — 그러면 같은 seed 의
    // 두 판이 프레임 레이트에 따라 갈린다.
    public sealed class CoreTickRateTests
    {
        private const int Frames = 60;

        [UnityTest]
        public IEnumerator BattleScaleThreeTenths_IssuesAboutEighteenTicksPerSixtyFrames()
        {
            var go = new GameObject("BattleDriver_RateProbe");
            var driver = go.AddComponent<BattleDriver>();
            driver.Begin(MinimalDefinition());

            using (TimeManager.Instance.Request(TimeDomain.Battle, 0.3f))
            {
                // 첫 프레임은 리스가 걸리기 전 누산을 물고 있을 수 있으므로 한 번 흘려보낸다.
                yield return null;
                int before = driver.Match.Clock.Tick;
                float elapsed = 0f;
                for (int i = 0; i < Frames; i++) { yield return null; elapsed += Time.unscaledDeltaTime; }
                int ticks = driver.Match.Clock.Tick - before;

                // 기대값은 프레임 수가 아니라 **흐른 시간**에서 나온다 — 러너의 프레임 간격은
                // 기기마다 다르다. 0.3 배율 · 1/60 틱 ⇒ elapsed × 0.3 × 60.
                int expected = Mathf.RoundToInt(elapsed * 0.3f * 60f);
                Assert.That(ticks, Is.EqualTo(expected).Within(1),
                    $"0.3 배율에서 {elapsed:F3}초 동안 {expected}±1 틱이어야 하는데 {ticks} 틱이었다");
            }

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator BattleScaleZero_IssuesNoTicks()
        {
            var go = new GameObject("BattleDriver_PauseProbe");
            var driver = go.AddComponent<BattleDriver>();
            driver.Begin(MinimalDefinition());

            using (TimeManager.Instance.Request(TimeDomain.Battle, 0f))
            {
                yield return null;
                int before = driver.Match.Clock.Tick;
                for (int i = 0; i < Frames; i++) yield return null;
                Assert.AreEqual(before, driver.Match.Clock.Tick,
                    "배율 0 에서는 틱이 한 번도 돌지 않는다");
            }

            Object.DestroyImmediate(go);
        }

        // 최소 판. 이 테스트가 묻는 것은 **방출 순서**뿐이지만, 판을 세우려면 격자가 있어야
        // 한다(흐름장 슬롯이 셀 수에서 나온다) — 그래서 4×3 빈 판을 준다.
        private static MatchDefinition MinimalDefinition()
        {
            var map = new Wassup.BattleCore.Map.MapSnapshot
            {
                Width = 4,
                Height = 3,
                TileSize = 1f,
                Goals = new[] { new Unity.Mathematics.int2(3, 1) },
                Spawns = new[] { new Unity.Mathematics.int2(0, 1) },
            };
            map.Normalize();
            var def = new MatchDefinition { Seed = 1, Map = map };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }
    }
}
