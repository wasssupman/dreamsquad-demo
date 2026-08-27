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
    // skill-layer-foundation unit 1 — Tornado arm 특성화. 이전(port) 전의 동작을 박제한다.
    //
    // ⚠ CastSkillAtTile 의 반환값(affectedCount)은 **로그용 pre-count preview** 다
    // (ApplyTornado 코드 주석 명시). 실제 당김은 MovementSystem 이 살아 있는 TornadoField
    // 를 매 프레임 질의해 수행하는 **후처리 가산 변위**다(aggro-tile-chase unit 3 계약 7).
    // 그래서 단언은 반환값이 아니라 **적의 실제 위치**로 쓴다.
    //
    // 박제하는 계약(Phase 8 §17 — 연속 필드):
    //  (1) 반경 내 적은 중심으로 끌려와 붙잡힌다(pullSpeed 가 보행을 이기는 동안)
    //  (2) 시전 «후» 태어난 적도 잡힌다 — SlowField(시전 시점 스냅샷·후속 진입 무영향)와
    //      정확히 반대. 이 대칭이 이전 중 깨지면 두 파일 중 하나가 빨개진다.
    //  (3) durationSec 소진 시 캐리어가 파괴되고 당김이 끝난다(영구 속박 금지)
    //
    // [EditMode 이관 후보] 당김 스텝 산식(중심 방향·pullStep 클램프)은 TestSkillContext
    // (unit 3) 이후 순수 코어로 내려갈 수 있다. 필드 수명·이동 합성은 PlayMode 잔류.
    public class ActiveTornadoTest
    {
        private const float Hp = 100000f;

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
        public IEnumerator Tornado_PullsEnemyToCenter_HoldsIt_ThenReleasesOnExpiry()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            var cell = FindWalkCell(bridge, em);
            var enemy = SpawnWalker(em, bridge, cell, speed: 2f);
            yield return null;

            // 자유 보행 대조 — 아래 «붙잡힌다» 단언이 «원래 안 움직이는 적» 으로도
            // 통과하는 것을 막는다(OnPlaceStunNearbyTest 의 대조군과 같은 이유).
            float freeSum = 0f, acc = 0f;
            int guard = 0;
            float3 prev = Pos(em, enemy);
            while (acc < 0.8f && guard++ < 600)
            {
                yield return null;
                acc += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                var p = Pos(em, enemy); freeSum += math.distance(p, prev); prev = p;
            }
            Assert.Greater(freeSum, 0.3f, "대조: 필드가 없으면 걷는다");

            // 적의 «현재» 칸에 시전 — 중심이 Walk 칸이어야 당김 변위가 벽 트림에 막히지 않는다.
            var castCell = ToCell(bridge, Pos(em, enemy));
            const float pullSpeed = 6f;   // 보행(2)보다 확실히 큰 당김 — 평형점이 중심 부근
            const float life = 2f;
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.Tornado, pullSpeed, life, 2f), castCell, out int affected), "시전");
            // preview 는 «시전 순간 반경 내» 스냅샷일 뿐이다(로그 기준선) — 결과 단언이 아니다.
            Assert.AreEqual(1, affected, "preview: 시전 순간 반경 내 1기");
            Assert.AreEqual(1, CountTornadoes(em), "캐리어 필드 1기 생성");

            var center = bridge.GridToWorldCenterVector(castCell);
            yield return PumpFor(1f);
            float held = Planar(Pos(em, enemy), center);
            Assert.Less(held, 0.6f * bridge.TileSize,
                $"당김(x{pullSpeed})이 보행(x2)을 이겨 중심 부근에 붙잡힌다(실측 {held:F2})");

            // 만료 — remaining 소진 시 EffectTickSystem 이 캐리어를 파괴한다.
            // (시전 후 총 경과 ≥ life + 1s — 위 hold 창이 이미 1s 를 썼다.)
            yield return PumpFor(life);
            Assert.AreEqual(0, CountTornadoes(em), "수명이 다한 필드는 파괴된다");

            float3 releasedFrom = Pos(em, enemy);
            yield return PumpFor(0.8f);
            float resumed = math.distance(Pos(em, enemy), releasedFrom);
            em.DestroyEntity(enemy);
            Assert.Greater(resumed, 0.3f,
                "필드가 사라지면 자기주도 보행이 재개된다 — 영구 속박으로 회귀하면 여기가 잡는다");
        }

        [UnityTest]
        public IEnumerator Tornado_ContinuousField_CatchesEnemySpawnedAfterCast()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            var cell = FindWalkCell(bridge, em);

            // 빈 판에 먼저 시전한다 — preview 0 인데도 필드는 실제로 일한다는 것이
            // 이 arm 의 «반환값 ≠ 결과» 를 가장 선명하게 박제한다.
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.Tornado, 6f, 3f, 2f), cell, out int affected), "빈 판 시전");
            Assert.AreEqual(0, affected, "preview 0 — 시전 순간 반경 내 적이 없다");

            // 시전 «후» 태어난 적 — Phase 7 의 시전 시점 스냅샷이었다면 이 적은 자유였다.
            var late = SpawnWalker(em, bridge, cell, speed: 2f);
            yield return null;
            var center = bridge.GridToWorldCenterVector(cell);
            yield return PumpFor(1f);
            float held = Planar(Pos(em, late), center);
            em.DestroyEntity(late);
            DestroyTornadoes(em);   // 수명(3s)이 테스트보다 길다 — 다음 테스트로 새지 않게 회수

            Assert.Less(held, 0.6f * bridge.TileSize,
                $"사후 진입자도 당겨 붙잡힌다(실측 {held:F2}) — 연속 필드(Phase 8 §17)의 본질");
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
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;
            sink(bridge);
        }

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
            DestroyTornadoes(em);
        }

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
            // ⚠ 스킬 레이어의 핸들 축 — 라이브 스포너는 발급한다. 없으면 어댑터가
            // 이 적을 못 가리켜 액티브가 조용히 아무도 안 건드린다.
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            return e;
        }

        // Walk 칸을 고른다 — **배치 가능 여부를 묻지 않는다.**
        // 액티브 스킬은 타일에 시전할 뿐 유닛을 놓지 않는다. 원래 이 탐색기는
        // `CanPlaceDefenderAt` 를 앵커로 썼는데(배치 테스트에서 빌려온 관용구),
        // 이 테스트들은 전투를 시작한 뒤에 돌기 때문에 **배치 페이즈가 닫혀
        // 어디서도 참이 아니다** — 그래서 픽스처가 통째로 죽었다(주행 2026-08-25).
        // 필요한 것은 walk 칸뿐이므로 walkMask 를 직접 본다.
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

            // 판형 비의존(map-diorama-stage 5차 병합) — 원점+margin 부터 훑으면 Street 기본판에선 골 (1,5) 옆 칸이
            // 뽑혀 더미 워커가 «자유 보행 대조» 중 골에 닿아 소멸했다(«계측 대상이 사라졌다»). 골에서 **가장 먼**
            // Walk 칸을 고른다 — 토네이도는 타일 시전이라 배치 가능 여부와 무관, 가장자리 회피는 margin 유지.
            const int Margin = 2;
            Vector2Int best = default; float bestCells = -1f;
            for (int x = Margin; x < ff.gridSize.x - Margin; x++)
                for (int y = Margin; y < ff.gridSize.y - Margin; y++)
                {
                    if (!IsWalk(x, y)) continue;
                    float cells = BattleBridgeTestAccess.CellsToGoal(ff, new int2(x, y));
                    if (float.IsPositiveInfinity(cells)) continue;   // 도달불가 칸은 흐름이 없다 — 보행 대조가 무의미
                    if (cells > bestCells) { bestCells = cells; best = new Vector2Int(x, y); }
                }
            if (bestCells >= 0f) return best;

            Assert.Fail("골에 닿는 Walk 칸을 찾지 못했다");
            return default;
        }

        private static int CountTornadoes(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TornadoField>());
            return q.CalculateEntityCount();
        }

        private static void DestroyTornadoes(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TornadoField>());
            var fields = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < fields.Length; i++) em.DestroyEntity(fields[i]);
            fields.Dispose();
        }

        private static float3 Pos(EntityManager em, Entity e)
        {
            Assert.IsTrue(em.Exists(e), "계측 대상이 사라졌다(골 도달/파괴?) — 테스트 전제 붕괴");
            return em.GetComponentData<LocalTransform>(e).Position;
        }

        private static Vector2Int ToCell(BattleBridge bridge, float3 p)
        {
            var c = bridge.DebugWorldToCell(new Vector3(p.x, p.y, p.z));
            return new Vector2Int(c.x, c.y);
        }

        // 당김도 보행도 평면 사건이다 — y 를 빼고 잰다(lift/뷰 y 개입 배제).
        private static float Planar(float3 p, Vector3 c)
            => math.distance(new float2(p.x, p.z), new float2(c.x, c.z));

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
