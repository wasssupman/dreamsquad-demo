using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // beam-ranger-defender unit 1 — **빔이 실제로 그려지는지**를 보는 테스트.
    //
    // 왜 필요했나: 기존 버스터즈 테스트(HitscanDefenderTest / OnPlaceDotNearbyTest)는 ECS
    // 데미지만 봤다. 그런데 빔은 `BattleBridge.Update()` 의 드레인에서 구동되고, 그 Update 는
    // **`if (!_running) return;`** 로 막혀 있다. 즉 `StartBattle()` 없이는 빔 경로가 통째로
    // 안 도는데도 데미지 테스트는 전부 통과한다 — 실제로 이 구멍으로 "빔이 끊긴다" 결함이
    // green 을 뚫고 나갔다. 그래서 이 테스트는 반드시 StartBattle 후에 검증한다.
    //
    // 확인하는 것: 세션이 열리고, 빔 몸통이 **실제 사거리만큼 늘어나** 배치되는가.
    // (프리팹 원본 스케일 4.17 / 원본 위치 그대로면 배치가 한 번도 안 된 것이다.)
    public class BeamPresentationTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator BeamUnit_Attacking_StretchesBeamBodyBetweenMuzzleAndTarget()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var busters = FindCatalog().ById("busters");
            Assert.IsNotNull(busters.beamVfxPrefab, "빔 유닛 판별자 = beamVfxPrefab. 비면 빔이 없다");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            Vector2Int cell = FindPlaceableCell(bridge, busters);
            Assert.AreNotEqual(int.MinValue, cell.x, "placeable cell");
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, busters), "place busters");

            // ★ 이것이 없으면 드레인이 안 돌아 빔이 영원히 안 생긴다.
            bridge.StartBattle();

            var defender = FindDefender(bridge, em);
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var enemy = SpawnDummyEnemy(em, defPos + new float3(1.6f, 0f, 0f));

            // 세션이 열리고 배치될 때까지.
            Transform body = null;
            float t = 0f;
            while (t < 4f && body == null)
            {
                t += Time.deltaTime;
                var presenter = GameObject.Find("BeamPresenter (auto)");
                if (presenter != null && presenter.transform.childCount > 0)
                {
                    var beam = presenter.transform.GetChild(0);
                    if (beam.gameObject.activeSelf) body = beam.Find("BeamBody");
                }
                yield return null;
            }

            Assert.IsNotNull(body, "공격 중이면 빔 세션이 열려 있어야 한다(활성 BeamBody)");

            // 프리팹 원본은 z=4.17 · 위치 (0, 2.41, 0). 그대로면 배치가 한 번도 안 된 것.
            float z = body.localScale.z;
            Assert.That(z, Is.Not.EqualTo(4.17f).Within(0.001f),
                "BeamBody 의 z 가 프리팹 원본 그대로다 = TryPlace 가 한 번도 성공하지 않았다");
            Assert.Greater(z, 0.01f, "빔 길이가 0 이면 몸통이 안 보인다");

            // 정렬 회귀 가드: 벤더 프리팹 기본값(0~2)이면 유닛(수백대) 뒤에 깔려
            // 빈 땅 구간만 보이고 유닛에 가린 곳은 끊겨 보인다 — 실제 제보의 원인.
            var renderers = body.parent.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.Greater(renderers.Length, 0, "빔 렌더러가 있어야 한다");
            foreach (var r in renderers)
                Assert.GreaterOrEqual(r.sortingOrder, Wassup.Presentation.BoardSortOrder.BeamOrder,
                    $"빔 렌더러 '{r.name}' 의 sortingOrder({r.sortingOrder})가 유닛 대역 아래다 — 가려진다");

            if (em.Exists(enemy)) em.DestroyEntity(enemy);
        }

        [UnityTest]
        public IEnumerator OnPlaceBarrage_OpensOneBeamPerTarget_AndHoldsFire()
        {
            // 배치 스킬(개점 일제 조사)은 **반경 내 대상 수만큼** 빔을 연다. 그리고 조사가
            // 끝날 때까지 기본 공격을 하지 않는다(유닛이 스킬에 묶여 있는 것이 사양).
            //
            // 회귀 가드: 초판은 빔 끝점을 spineUnitPool 로만 찾아, 풀에 없는 적이 끝점이면
            // 세션이 첫 프레임에 죽었다. 배치 빔은 한 번만 열리므로 그대로 전멸했고, 0.2초마다
            // 다시 열리는 **공격 빔 하나만** 남아 "빔이 1개만 나온다"로 보였다.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var busters = FindCatalog().ById("busters");
            Assert.AreEqual(OnPlaceEffectType.DotNearby, busters.onPlaceEffect, "배치 스킬 = DotNearby");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            Vector2Int cell = FindPlaceableCell(bridge, busters);
            Assert.AreNotEqual(int.MinValue, cell.x, "placeable cell");

            // 반경(2) 안에 3체 — 배치 **전에** 세운다(on-place 는 그 순간의 스냅샷을 본다).
            var spots = new[]
            {
                new Vector2Int(cell.x + 1, cell.y),
                new Vector2Int(cell.x, cell.y + 1),
                new Vector2Int(cell.x + 1, cell.y + 1),
            };
            var enemies = new Entity[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var w = bridge.GridToWorldCenterVector(spots[i]);
                enemies[i] = SpawnDummyEnemy(em, new float3(w.x, w.y, w.z));
            }

            bridge.StartBattle();
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, busters), "place busters");

            // 조사 지속(2초) 동안 동시에 살아있는 빔 세션의 최대치.
            int maxActive = 0;
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                var presenter = GameObject.Find("BeamPresenter (auto)");
                int active = 0;
                if (presenter != null)
                    foreach (Transform c in presenter.transform)
                        if (c.gameObject.activeSelf) active++;
                if (active > maxActive) maxActive = active;
                yield return null;
            }

            for (int i = 0; i < enemies.Length; i++) if (em.Exists(enemies[i])) em.DestroyEntity(enemies[i]);

            Assert.GreaterOrEqual(maxActive, spots.Length,
                $"반경 내 {spots.Length}체 전원에게 빔이 이어져야 한다(실측 최대 {maxActive}). "
                + "1 이면 배치 빔이 죽고 공격 빔만 남은 것");
        }

        [UnityTest]
        public IEnumerator OnPlaceBarrage_ViaDragPath_AlsoOpensOneBeamPerTarget()
        {
            // 실사용은 드래그 배치다: TryBeginDefenderDeployment(PendingDeployment) →
            // ActivateDeployedDefender → TriggerDeploymentOnPlaceSkill → ApplyOnPlaceEffect.
            // 즉시 배치(PlaceDefenderAs)만 검증하면 이 경로의 차이를 놓친다.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var busters = FindCatalog().ById("busters");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            Vector2Int cell = FindPlaceableCell(bridge, busters);
            Assert.AreNotEqual(int.MinValue, cell.x, "placeable cell");

            var spots = new[]
            {
                new Vector2Int(cell.x + 1, cell.y),
                new Vector2Int(cell.x, cell.y + 1),
                new Vector2Int(cell.x + 1, cell.y + 1),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var w = bridge.GridToWorldCenterVector(spots[i]);
                SpawnDummyEnemy(em, new float3(w.x, w.y, w.z));
            }

            bridge.StartBattle();
            Assert.IsTrue(bridge.TryBeginDefenderDeployment(cell.x, cell.y, busters, out var entity),
                "drag 배치 시작");
            bridge.ActivateDeployedDefender(cell, entity); // 배치 확정 = on-place 발동 시점

            var presenter = GameObject.Find("BeamPresenter (auto)");
            Assert.IsNotNull(presenter, "활성화 직후 빔 프레젠터가 있어야 한다");
            var bp = presenter.GetComponent<Wassup.Presentation.BeamPresenter>();
            Assert.GreaterOrEqual(bp.LiveSessionCount, spots.Length,
                $"드래그 경로에서도 대상 {spots.Length}체 전원에게 빔이 열려야 한다"
                + $"(실측 {bp.LiveSessionCount})");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnPlaceBarrage_SuppressesBasicAttackForItsDuration()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var busters = FindCatalog().ById("busters");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Vector2Int cell = FindPlaceableCell(bridge, busters);
            bridge.StartBattle();
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, busters), "place busters");

            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            // 배치 직후 쿨다운이 조사 지속만큼 밀려 있어야 한다 = 그동안 기본 공격 없음.
            var atk = em.GetComponentData<Wassup.Battle.Combat.AttackState>(defender);
            Assert.GreaterOrEqual(atk.cooldownRemaining, busters.onPlaceDuration - 0.05f,
                $"조사 중에는 기본 공격을 하지 않아야 한다(쿨다운 {atk.cooldownRemaining} < 지속 {busters.onPlaceDuration})");
            yield return null;
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static Entity SpawnDummyEnemy(EntityManager em, float3 pos)
        {
            const float Hp = 1_000_000f;
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            // 배치 조사(DotNearby)가 이 적에게 DoT 를 건다 — 실적 아키타입처럼 CcEffect 버퍼가
            // 없으면 CcApply 가 던진다. PlayMode 는 세션을 공유하므로 이 예외가 **다른 테스트**
            // 실패로 전가돼 원인 추적이 어려워진다(실제로 그렇게 한 번 헤맸다).
            em.AddBuffer<Wassup.Battle.Effects.CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return new Vector2Int(x, y);
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var v = de.Value;
                var entity = (Entity)v.GetType().GetField("Item1").GetValue(v);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
