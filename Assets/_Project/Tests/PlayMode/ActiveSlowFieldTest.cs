using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — SlowField arm 특성화. 이전(port) 전의 동작을 박제한다.
    //
    // SlowField 는 액티브 6종 중 유일하게 **시전 시점 동기 스냅샷**이다: ApplySlowField 가
    // 그 자리에서 반경 내 적을 세며 EnqueueMoveSpeedMul 을 넣는다. 그래서
    //  (1) 반환값(affectedCount)이 실제 적용 수와 일치하고 — 장판/투사체 4종의 «로그 preview» 와
    //      다른 점을 여기 못박는다,
    //  (2) 시전 «후» 반경에 나타난 적은 걸리지 않는다 — Tornado(연속 필드)와 정확히 반대.
    //      이 대칭이 이전 중에 깨지면(예: 장판화) 두 파일 중 하나가 빨개진다.
    //
    // 단언은 「실제 결과」다: 실효 스탯(ModifierStats.moveSpeedMul)과 **실보행 거리**.
    // [EditMode 이관 후보] 반경 선별(체비셰프 대상 수집)은 TestSkillContext(unit 3) 이후
    // 순수 코어로 내려갈 수 있다. 감속의 이동 반영(MovementSystem 소비)은 PlayMode 잔류.
    public class ActiveSlowFieldTest
    {
        private const float Hp = 100000f;

        // duel-live-focus — 계측은 자기 판을 선언한다(라이브 풀이 바뀌어도 같은 판에서 잰다).
        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlowField_SlowsSnapshotEnemies_NotLateArrivals_AndTheyWalkSlower()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            var cell = FindWalkCell(bridge, em);
            var slowed = SpawnWalker(em, bridge, cell, speed: 2f);
            yield return null;

            var skill = MakeSkill(SkillEffectType.SlowField, magnitude: 0.5f, durationSec: 30f, range: 2f);
            Assert.IsTrue(bridge.CastSkillAtTile(skill, cell, out int affected), "시전");
            // 동기 스냅샷이라 이 반환값은 preview 가 아니라 실제 적용 수다(판이 격리돼 1기).
            Assert.AreEqual(1, affected, "시전 순간 반경 내 적 1기가 그 자리에서 적용된다");

            // Apply(슬롯) → Aggregate(실효 스탯) 프레임 여유.
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(0.5f, MoveMul(em, slowed), 0.01f,
                "감속이 실효 스탯(moveSpeedMul)에 실제로 반영된다 — magnitude 가 곧 배율");

            // 스냅샷 특성 — 시전 «후» 같은 자리에 나타난 적은 무영향(장판이 아니다).
            var late = SpawnWalker(em, bridge, cell, speed: 2f);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(1f, MoveMul(em, late), 0.01f,
                "시전 후 진입자는 안 걸린다 — SlowField 를 장판으로 바꾸면 여기가 빨개져야 한다");

            // 실제로 «느리게 걷는다» — 같은 창에서 두 적의 보행 거리를 누적 비교한다.
            // 변위(chord)가 아니라 프레임별 |Δpos| 합 — Serpent 의 굽은 경로에서도 유효하다.
            float sumSlow = 0f, sumFree = 0f, acc = 0f;
            int guard = 0;
            float3 prevS = Pos(em, slowed), prevF = Pos(em, late);
            while (acc < 1.2f && guard++ < 900)
            {
                yield return null;
                acc += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                var ps = Pos(em, slowed); sumSlow += math.distance(ps, prevS); prevS = ps;
                var pf = Pos(em, late);   sumFree += math.distance(pf, prevF); prevF = pf;
            }
            em.DestroyEntity(slowed); em.DestroyEntity(late);

            Assert.Greater(sumSlow, 0.1f, "감속이지 정지가 아니다 — 감속된 적도 걷는다");
            Assert.Greater(sumFree - sumSlow, 0.5f,
                $"x0.5 감속된 적({sumSlow:F2})은 같은 창의 정상 적({sumFree:F2})보다 실제로 덜 걷는다");
        }

        [UnityTest]
        public IEnumerator SlowField_WearsOff_AfterDuration()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            var cell = FindWalkCell(bridge, em);
            var enemy = SpawnWalker(em, bridge, cell, speed: 2f);
            yield return null;

            const float slowLife = 0.8f;
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.SlowField, 0.5f, slowLife, 2f), cell, out _), "시전");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(0.5f, MoveMul(em, enemy), 0.01f, "먼저 걸린다");

            // remaining 은 Battle 도메인 시계로 소진된다 — 같은 시계를 누산해 기다린다.
            yield return PumpFor(slowLife + 0.6f);
            float mul = MoveMul(em, enemy);
            em.DestroyEntity(enemy);
            Assert.AreEqual(1f, mul, 0.01f,
                "durationSec 이 지나면 원속으로 돌아온다 — 영구 감속으로 회귀하면 여기가 잡는다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private BattleBridge _bridge;

        private static IEnumerator LoadBattleAndStart(System.Action<BattleBridge> sink)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge");
            bridge.BeginPlacement();
            bridge.StartBattle();   // 캐스트는 `if (!_running)` 게이트 뒤 — 레포 관례
            for (int i = 0; i < 2; i++) yield return null;
            sink(bridge);
        }

        // 웨이브 소음 차단(SlimeSplitE2ETest 관용구) — 계측 대상 외 적이 섞이면 반경 카운트와
        // 이동 계측이 판마다 다른 값을 잰다. 다음 웨이브는 waveInterval(≥12s) 뒤라 창 밖이다.
        private static void IsolateBoard(BattleBridge bridge, EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var existing = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < existing.Length; i++) em.DestroyEntity(existing[i]);
            existing.Dispose();
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            BattleBridgeTestAccess.SetField(bridge, "_nextWaveIndex", 1);
            BattleBridgeTestAccess.SetField(bridge, "_waveStartSec",
                (float)(double)BattleBridgeTestAccess.Field(bridge, "_battleClock"));
            // 앞선 테스트가 남겼을 수 있는 당김 필드 제거 — 이동 계측 왜곡 방지.
            using var tq = em.CreateEntityQuery(ComponentType.ReadOnly<TornadoField>());
            var fields = tq.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < fields.Length; i++) em.DestroyEntity(fields[i]);
            fields.Dispose();
        }

        // 실제 적이 갖는 부품을 갖춘 더미(OnPlaceStunNearbyTest 관용구) + 모디파이어 프레임워크
        // 부품(ModifierStats/Dirty) — 실스폰(SpawnUnit)과 같은 아키타입이어야 감속 집계가 돈다
        // (ModifierStatsAggregateSystem 의 쿼리가 ModifierStats 를 요구한다).
        private static Entity SpawnWalker(EntityManager em, BattleBridge bridge, Vector2Int cell, float speed)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            em.AddComponentData(e, new PathFollowState { speed = speed, traversalLayers = TraversalSlots.DefaultMask });
            em.AddComponentData(e, new Wassup.Battle.Combat.EnemyAiState { value = Wassup.Battle.Combat.AiState.Marching });
            em.AddComponentData(e, new ModifierStats
            {
                damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f,
                regenPerSec = 0f, moveSpeedMul = 1f, damageVsCcMul = 1f, maxHealthMul = 1f,
            });
            em.AddComponent<ModifierStatsDirty>(e);
            em.SetComponentEnabled<ModifierStatsDirty>(e, false);
            return e;
        }

        // Walk 칸을 고른다 — **배치 가능 여부를 묻지 않는다.**
        // 액티브 스킬은 타일에 시전할 뿐 유닛을 놓지 않는다. 원래 이 탐색기는
        // `CanPlaceDefenderAt` 를 앵커로 썼는데(배치 테스트에서 빌려온 관용구),
        // 이 테스트들은 전투를 시작한 뒤에 돌기 때문에 **배치 페이즈가 닫혀
        // 어디서도 참이 아니다** — 그래서 픽스처가 통째로 죽었다(주행 2026-08-25).
        // 필요한 것은 walk 칸 하나뿐이므로 walkMask 를 직접 본다.
        // 가장자리를 피하는 의도는 margin 으로 살린다(스폰·골 옆 회피).
        private static Vector2Int FindWalkCell(BattleBridge bridge, EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");

            bool IsWalk(int x, int y)
                => x >= 0 && y >= 0 && x < ff.gridSize.x && y < ff.gridSize.y
                   && ff.walkMask[y * ff.gridSize.x + x] != 0;

            const int Margin = 2;
            for (int x = Margin; x < ff.gridSize.x - Margin; x++)
                for (int y = Margin; y < ff.gridSize.y - Margin; y++)
                    if (IsWalk(x, y)) return new Vector2Int(x, y);

            Assert.Fail("Walk 칸을 찾지 못했다");
            return default;
        }

        private static float MoveMul(EntityManager em, Entity e)
        {
            Assert.IsTrue(em.Exists(e), "계측 대상이 사라졌다(골 도달/파괴?) — 테스트 전제 붕괴");
            return em.GetComponentData<ModifierStats>(e).moveSpeedMul;
        }

        private static float3 Pos(EntityManager em, Entity e)
        {
            Assert.IsTrue(em.Exists(e), "계측 대상이 사라졌다 — 테스트 전제 붕괴");
            return em.GetComponentData<LocalTransform>(e).Position;
        }

        // 만료는 Battle 도메인 델타로 진행된다 — 같은 시계를 누산한다(ActiveAllyZoneTest 관용구).
        private static IEnumerator PumpFor(float seconds)
        {
            float acc = 0f;
            int guard = 0;
            while (acc < seconds && guard++ < 6000)
            {
                yield return null;
                acc += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            }
        }

        private static SkillData MakeSkill(SkillEffectType effect, float magnitude, float durationSec, float range)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.id = $"test_{effect}";
            s.effect = effect;
            s.magnitude = magnitude;
            s.durationSec = durationSec;
            s.range = range;
            s.cooldownSec = 0f;
            s.cost = 0;
            return s;
        }
    }
}
