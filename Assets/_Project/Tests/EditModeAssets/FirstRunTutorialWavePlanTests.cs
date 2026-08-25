using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class FirstRunTutorialWavePlanTests
    {
        private const string PlanPath =
            "Assets/_Project/Scripts/Data/WavePlans/WavePlan_FirstRunTutorial.asset";

        [Test]
        public void Plan_FillsOneMinuteWithTenNormalMinionWaves()
        {
            var plan = AssetDatabase.LoadAssetAtPath<WavePlanAsset>(PlanPath);

            Assert.IsNotNull(plan, $"첫 실행 튜토리얼 플랜이 없다: {PlanPath}");
            Assert.AreEqual(60f, plan.timerDurationSec, 0.0001f);
            Assert.AreEqual(10, plan.waves.Count);

            var runtimePlan = WavePatternGenerator.FromPlanAsset(plan);
            Assert.AreEqual(60f, runtimePlan.timerDurationSec, 0.0001f);
            Assert.AreEqual(10, runtimePlan.waves.Count);

            float totalDuration = 0f;
            float lastGlobalSpawn = 0f;
            for (int waveIndex = 0; waveIndex < plan.waves.Count; waveIndex++)
            {
                var wave = plan.waves[waveIndex];
                Assert.AreEqual(totalDuration, runtimePlan.waves[waveIndex].triggerTimeSec, 0.0001f,
                    $"wave {waveIndex + 1}: 런타임 시작 시각");
                Assert.Greater(wave.durationSec, 0f, $"wave {waveIndex + 1}: durationSec");
                Assert.IsNotNull(wave.groups, $"wave {waveIndex + 1}: groups");
                Assert.IsNotEmpty(wave.groups, $"wave {waveIndex + 1}: groups");

                foreach (var group in wave.groups)
                {
                    Assert.IsNotNull(group.unit, $"wave {waveIndex + 1}: unit");
                    Assert.AreEqual(EnemyTier.Normal, group.unit.tier,
                        $"wave {waveIndex + 1}: 엘리트/보스는 첫 실행 튜토리얼에 나오면 안 된다");
                    Assert.AreEqual("basic", group.unit.id,
                        $"wave {waveIndex + 1}: 첫 실행 튜토리얼은 기본 잡몹만 사용한다");
                    Assert.Greater(group.count, 0, $"wave {waveIndex + 1}: count");
                    Assert.GreaterOrEqual(group.triggerTimeSec, 0f,
                        $"wave {waveIndex + 1}: triggerTimeSec");

                    float lastSpawn = group.triggerTimeSec + (group.count - 1) * wave.intervalSec;
                    Assert.Less(lastSpawn, wave.durationSec,
                        $"wave {waveIndex + 1}: 마지막 스폰이 웨이브 구간 밖이다");
                    lastGlobalSpawn = System.Math.Max(lastGlobalSpawn, totalDuration + lastSpawn);
                }

                totalDuration += wave.durationSec;
            }

            Assert.AreEqual(60f, totalDuration, 0.0001f,
                "10개 웨이브의 누적 구간이 1분 타이머를 정확히 채워야 한다");
            Assert.AreEqual(58.5f, lastGlobalSpawn, 0.0001f,
                "마지막 잡몹이 제한시간 직전까지 전투를 이어가야 한다");

            var opening = plan.waves[0];
            Assert.AreEqual(15f, opening.durationSec, 0.0001f,
                "기존 온보딩 행동 예산을 위해 첫 웨이브 구간을 유지한다");
            Assert.AreEqual(10, opening.groups[0].count);
            Assert.AreEqual(0, opening.groups[0].laneIndex,
                "첫 산탄 연출용 기본몹 무리는 한 레인에 모여야 한다");
        }
    }
}
