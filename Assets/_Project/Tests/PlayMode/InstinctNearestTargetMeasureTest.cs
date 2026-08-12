using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // instinct-content unit 2 — 「가까운 거점부터 팬다」가 **저절로** 되는지 잰다.
    //
    // 이 파일은 단정보다 **관측**이 목적이다. 사용자 판단:
    //   «오른쪽 끝에서 스폰된 적은 자연스럽게 가까운 본능을 선택하고, 본능이 다 부서지면
    //    자연스럽게 마음이 우선 타겟된다. 별도의 웨이포인트가 필요한 게 아니라고 생각.»
    // 맞다면 목적지 선택 기계를 **만들지 않는다**. 그 판정을 숫자로 받는다.
    //
    // 판: MapDocument_Duel(21×12). 적 마음(18,5)=스폰 → 내 마음(2,5). x10 이 강이라
    // 적은 y2·y3·y8·y9 다리로만 건넌다. 내 본능 (4,3)·(4,8)의 footprint 가 그 두 갈래
    // 위에 정확히 놓여, 「경로 위 본능은 맞고 경로 밖 본능은 무시되는가」가 한 판에서 갈린다.
    //
    // 공허 방지: 적이 실제로 움직였는가(방문 셀 수)와 판이 진행했는가(웨이브 스폰)를
    // 먼저 단정한다. 마음사냥꾼 실측이 «도발 0회» 를 냈다가 사실은 유닛이 활성조차
    // 안 됐던 사고를 되풀이하지 않는다.
    public class InstinctNearestTargetMeasureTest
    {
        private const float MeasureSec = 75f;
        private const int DuelMapIndex = 9;
        private const int WaveRush = 3;   // 홍수는 순서 판정을 오염시킨다 — 현실적 압력만 준다
        private static readonly int2 NorthInstinct = new int2(4, 3);
        private static readonly int2 SouthInstinct = new int2(4, 8);

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

        private static bool InFootprint(int2 cell, int2 center)
            => math.abs(cell.x - center.x) <= 1 && math.abs(cell.y - center.y) <= 1;

        [UnityTest]
        public IEnumerator Duel_DoEnemiesEngageTheNearestInstinctOnTheirWay()
        {
            LogAssert.ignoreFailingMessages = true;
            DevMapOverride.Index = DuelMapIndex;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");

            var map = (Wassup.Data.GeneratedMap)GetField(bridge, "_generatedMap");
            Assert.AreEqual(new int2(21, 12), map.gridSize,
                "dev 슬롯이 Duel 이 아니다 — 슬롯이 갈렸으면 이 측정은 다른 판을 잰 것이다");

            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;

            var em = (EntityManager)GetField(bridge, "_em");

            // 방어 본능 2기 — 이 측정의 대상. 없으면 전부 공허하다.
            var instincts = new Dictionary<int2, Entity>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>()))
            {
                var arr = q.ToEntityArray(Allocator.Temp);
                foreach (var e in arr)
                {
                    var st = em.GetComponentData<StructureTag>(e);
                    if (st.faction == Faction.DefenderInstinct) instincts[st.cell] = e;
                }
                arr.Dispose();
            }
            Assert.AreEqual(2, instincts.Count, "Duel 의 방어 본능 2기가 라이브에 있어야 한다");

            var maxHp = new Dictionary<int2, float>();
            foreach (var kv in instincts) maxHp[kv.Key] = em.GetComponentData<Health>(kv.Value).max;

            var flow = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.FlowFieldSingleton>())
                         .GetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>();

            // ── 관측 누적 ──
            var visitedAll = new HashSet<int2>();
            int visitsNorth = 0, visitsSouth = 0;
            float deathNorth = -1f, deathSouth = -1f;
            float firstStabilityDropAt = -1f;
            int startStability = bridge.GoalStabilityCurrent;
            int enemiesSeenMax = 0;

            for (int i = 0; i < WaveRush && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();

            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < MeasureSec)
            {
                float t = Time.unscaledTime - t0;

                using (var q = em.CreateEntityQuery(
                    ComponentType.ReadOnly<FactionTag>(),
                    ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
                {
                    var tags = q.ToComponentDataArray<FactionTag>(Allocator.Temp);
                    var xf = q.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
                    int enemies = 0;
                    for (int k = 0; k < tags.Length; k++)
                    {
                        if (tags[k].value != Faction.EnemyUnit) continue;
                        enemies++;
                        var cell = Wassup.Battle.Movement.GridMath.WorldToCell(
                            xf[k].Position, flow.tileSize, map.gridSize, origin: flow.origin);
                        visitedAll.Add(cell);
                        if (InFootprint(cell, NorthInstinct)) visitsNorth++;
                        if (InFootprint(cell, SouthInstinct)) visitsSouth++;
                    }
                    if (enemies > enemiesSeenMax) enemiesSeenMax = enemies;
                    tags.Dispose(); xf.Dispose();
                }

                if (deathNorth < 0f && !em.Exists(instincts[NorthInstinct])) deathNorth = t;
                if (deathSouth < 0f && !em.Exists(instincts[SouthInstinct])) deathSouth = t;
                if (firstStabilityDropAt < 0f && bridge.GoalStabilityCurrent < startStability)
                    firstStabilityDropAt = t;

                yield return null;
            }

            float hpNorth = em.Exists(instincts[NorthInstinct])
                ? em.GetComponentData<Health>(instincts[NorthInstinct]).value : 0f;
            float hpSouth = em.Exists(instincts[SouthInstinct])
                ? em.GetComponentData<Health>(instincts[SouthInstinct]).value : 0f;

            Debug.Log(
                "[unit2 측정] Duel " + MeasureSec + "s\n"
                + $"  대조(비공허): 동시 최대 적 {enemiesSeenMax}기 · 방문 셀 {visitedAll.Count}종\n"
                + $"  북 본능(4,3): footprint 방문 {visitsNorth} · HP {hpNorth}/{maxHp[NorthInstinct]} · 파괴 {deathNorth:F1}s\n"
                + $"  남 본능(4,8): footprint 방문 {visitsSouth} · HP {hpSouth}/{maxHp[SouthInstinct]} · 파괴 {deathSouth:F1}s\n"
                + $"  마음 안정도: {startStability} → {bridge.GoalStabilityCurrent} · 첫 감소 {firstStabilityDropAt:F1}s");

            // 공허 방지 단정만 건다 — 나머지는 판정용 관측치다.
            Assert.Greater(enemiesSeenMax, 0, "적이 한 기도 안 나왔다 — 측정 전체가 공허하다");
            Assert.Greater(visitedAll.Count, 5, "적이 움직이지 않았다 — 측정 전체가 공허하다");
        }
    }
}
