using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // map-rework unit 7~12 — **폭1 개편 맵에서 군집이 실제로 빠져나가는가.**
    //
    // 이 개편의 최대 위험이다. 폭1 복도는 근접이 살아나는 대가로 **마개**가 생길 수 있다:
    // 소프트 분리의 밀어냄 폭이 통로 여유보다 크면 서로 밀다가 전진이 멎는다. 그리고
    // **단독 통과 검산으로는 안 잡힌다** — 한 기는 늘 지나가기 때문이다(과거 실측:
    // 반지름 0.35 에서 6맵이 100초 교착, 0.25 로 낮춰 해소).
    //
    // 그래서 재는 것은 「경로가 있는가」가 아니라 «**떼로 밀어넣어도 계속 도착하는가**».
    // 골 안정도는 적이 도달할 때만 준다 — 그게 통과의 관측치다.
    public class MapCrowdClearanceTest
    {
        // unit 8 = Serpent(풀 0). 9~12 가 재저작될 때마다 여기에 인덱스를 더한다.
        private static readonly int[] ReworkedMapIndices = { 0 };

        private const float MeasureSec = 45f;
        private const int WaveRush = 6;

        private int _savedIndex = -1;

        [SetUp]
        public void SetUp() => _savedIndex = DevMapOverride.Index;

        [TearDown]
        public void TearDown()
        {
            DevMapOverride.Index = _savedIndex;
            LogAssert.ignoreFailingMessages = false;
        }

        private static object GetField(object target, string name)
        {
            var fi = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{name}' not found");
            return fi.GetValue(target);
        }

        [UnityTest]
        public IEnumerator ReworkedMaps_CrowdKeepsReachingTheGoal()
        {
            LogAssert.ignoreFailingMessages = true;

            foreach (int mapIndex in ReworkedMapIndices)
            {
                DevMapOverride.Index = mapIndex;
                yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
                for (int i = 0; i < 6; i++) yield return null;

                var bridge = Object.FindObjectOfType<BattleBridge>();
                Assert.IsNotNull(bridge, "BattleBridge present");
                bridge.BeginPlacement();
                yield return null;
                bridge.StartBattle();      // 방어유닛 0 — 지형만 시험한다
                yield return null;

                var em = (EntityManager)GetField(bridge, "_em");
                for (int i = 0; i < WaveRush && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();

                int startStability = bridge.GoalStabilityCurrent;
                int midStability = startStability;
                int peakEnemies = 0;
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < MeasureSec)
                {
                    using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<FactionTag>()))
                    {
                        var tags = q.ToComponentDataArray<FactionTag>(Allocator.Temp);
                        int alive = 0;
                        for (int k = 0; k < tags.Length; k++)
                            if (tags[k].value == Faction.EnemyUnit) alive++;
                        if (alive > peakEnemies) peakEnemies = alive;
                        tags.Dispose();
                    }
                    if (Time.unscaledTime - t0 < MeasureSec * 0.5f)
                        midStability = bridge.GoalStabilityCurrent;
                    yield return null;
                }

                int endStability = bridge.GoalStabilityCurrent;
                int firstHalf = startStability - midStability;
                int secondHalf = midStability - endStability;

                // 진단은 **단언 메시지에** 싣는다. Debug.Log 는 플레이 종료 시 콘솔이 비워져
                // 실패했을 때 읽을 수가 없다 — 읽을 수 없는 계측은 계측이 아니다.
                string shot = $"맵 {mapIndex} · 동시 최대 적 {peakEnemies}기 · "
                            + $"안정도 {startStability}→{midStability}→{endStability} "
                            + $"(전반 {firstHalf} / 후반 {secondHalf})";
                Debug.Log("[교착검산] " + shot);

                Assert.Greater(peakEnemies, 5, $"적이 거의 없다 — 측정이 공허하다. {shot}");
                Assert.Greater(firstHalf + secondHalf, 0,
                    $"아무도 골에 못 갔다 — 경로가 끊겼거나 교착. {shot}");

                // 「한 번은 갔다」와 「계속 간다」는 다르다. 마개가 생기면 전반만 줄고 후반이 0 이 된다.
                // 단, **골이 이미 붕괴했으면**(안정도 0) 더 줄 값이 없어 후반 0 이 당연하다 —
                // 그건 마개가 아니라 «떼가 완주해서 골을 부쉈다» 는 뜻이라 통과다.
                if (endStability > 0)
                    Assert.Greater(secondHalf, 0,
                        $"후반에 도착이 멎었다 — 폭1 복도에 마개가 생겼을 수 있다. {shot}");
            }
        }
    }
}
