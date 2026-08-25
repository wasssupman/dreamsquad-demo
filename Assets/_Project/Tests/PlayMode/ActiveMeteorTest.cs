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
    // skill-layer-foundation unit 1 — Meteor arm 특성화. 이전(port) 전의 동작을 박제한다.
    //
    // ⚠ CastSkillAtTile 의 반환값(affectedCount)은 **로그용 pre-count preview** 다
    // (ApplyMeteor 코드 주석 명시). 실제 피해는 통합 투사체 수명(SkyFall × TileAoe,
    // flightTime = warningSec)을 타고 ProjectileHitSystem 의 TileAoe arm 이 **착탄 시점에**
    // 해결한다(projectile-trajectory-payload unit 7). 그래서 단언은 반환값이 아니라
    // **적의 실제 Health** 로 쓴다.
    //
    // 박제하는 계약:
    //  (1) 시전 직후에는 무피해 — 데미지는 warningSec 뒤 착탄에 실린다
    //  (2) 착탄 시 반경(체비셰프 range 타일) 내 적은 정확히 magnitude 만큼 — flat AoE,
    //      감쇠 없음(TileAoe arm 주석: no falloff)
    //  (3) 반경 밖은 무피해
    //  (4) projectile 미배선(null)은 «가시적 드랍» — 시전 처리 자체는 성공(true·쿨다운
    //      소비)하지만 아무 것도 떨어지지 않는다(:2895 설정 오류 경로)
    //
    // [EditMode 이관 후보] 착탄 반경 선별(TileAoe.IsInTileRange 멤버십)은 TestSkillContext
    // (unit 3) 이후 순수 코어로 내려갈 수 있다. 비행 시계·해결 시스템 합류는 PlayMode 잔류.
    public class ActiveMeteorTest
    {
        private const float Hp = 100000f;
        private const string MeteorSkillPath = "Assets/_Project/Data/Skills/Skill_Meteor.asset";

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
        public IEnumerator Meteor_DamagesInRadiusByMagnitude_AfterWarning_NotBefore()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            FindWalkCells(bridge, em, out var near, out var far);
            // speed 0 — 예고(비행) 동안 칸 소속이 확정적이어야 반경 단언이 결정론이 된다.
            // (TileAoe 는 착탄 «시점» 의 적 칸을 본다 — 걷는 적이면 예고 동안 반경을 벗어난다.)
            var victim = SpawnWalker(em, bridge, near, speed: 0f);
            var outside = SpawnWalker(em, bridge, far, speed: 0f); // 체비셰프 ≥ 4 > range 2
            yield return null;

            const float dmg = 37.5f;   // 홀수값 — 이중 적용/배율 오염이 섞이면 등호가 깨진다
            const float warn = 0.5f;
            var skill = MakeSkill(SkillEffectType.Meteor, magnitude: dmg, durationSec: 0f, range: 2f);
            skill.warningSec = warn;
            skill.projectile = LoadMeteorProjectile();   // null 이면 드랍(:2895) — 실물을 태운다

            Assert.IsTrue(bridge.CastSkillAtTile(skill, near, out int affected), "시전");
            // preview 는 «시전 순간 반경 내» 스냅샷일 뿐(로그 기준선) — 결과 단언이 아니다.
            Assert.AreEqual(1, affected, "preview: 시전 순간 반경 내 1기");

            yield return null;
            Assert.AreEqual(Hp, HpOf(em, victim), 0.01f,
                "예고 중 무피해 — 데미지는 시전이 아니라 착탄(flightTime=warningSec)에 실린다");

            // 비행은 Battle 도메인 시계로 진행된다 — 같은 시계로 착탄 + 해결 여유를 기다린다.
            yield return PumpFor(warn + 0.6f);
            float inHp = HpOf(em, victim), outHp = HpOf(em, outside);
            em.DestroyEntity(victim); em.DestroyEntity(outside);

            Assert.AreEqual(Hp - dmg, inHp, 0.01f,
                "반경 내 적은 정확히 magnitude 만큼(flat AoE·감쇠 없음) — 착탄이 실제로 해결됐다");
            Assert.AreEqual(Hp, outHp, 0.01f, "반경(2타일) 밖은 무피해");
        }

        [UnityTest]
        public IEnumerator Meteor_MissingProjectile_DropsResolution_ButCastStillSucceeds()
        {
            yield return LoadBattleAndStart(v => _bridge = v);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = _bridge;
            IsolateBoard(bridge, em);

            FindWalkCells(bridge, em, out var near, out _);
            var victim = SpawnWalker(em, bridge, near, speed: 0f);
            yield return null;

            var skill = MakeSkill(SkillEffectType.Meteor, 37.5f, 0f, 2f);
            skill.warningSec = 0.2f;
            // projectile 미배선(null) — 설정 오류 경로. 조용한 NRE 대신 경고 로그 + 드랍.
            bool ok = bridge.CastSkillAtTile(skill, near, out int affected);

            // 현행 계약 박제: 드랍이어도 시전 «처리» 는 성공이다(true 반환·쿨다운/로그 소비).
            // 이전 중 이 반환이 false 로 바뀌면 여기가 알린다 — 의도된 변경이면 같이 갱신할 것.
            Assert.IsTrue(ok, "드랍이어도 시전 처리 자체는 성공(현행 동작)");
            Assert.AreEqual(0, affected, "preview 0 — ApplyMeteor 가 초기에 반환했다");

            yield return PumpFor(0.2f + 0.6f);
            float hp = HpOf(em, victim);
            em.DestroyEntity(victim);
            Assert.AreEqual(Hp, hp, 0.01f, "투사체가 없으니 아무 것도 착탄하지 않는다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private BattleBridge _bridge;

        // 실에셋의 projectile 참조만 빌린다 — Skill_Meteor.asset 자체는 읽기 전용.
        // (테스트 SkillData 는 CreateInstance 로 만들되, ProjectileData 는 등록·뷰 배선이
        //  얽힌 실물이 필요하다.)
        private static ProjectileData LoadMeteorProjectile()
        {
            var real = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(MeteorSkillPath);
            Assert.IsNotNull(real, $"메테오 스킬 에셋을 로드하지 못했다: {MeteorSkillPath}");
            Assert.IsNotNull(real.projectile, "에셋에 ProjectileData 가 배선돼 있어야 한다");
            return real.projectile;
        }

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
            using var tq = em.CreateEntityQuery(ComponentType.ReadOnly<TornadoField>());
            var fields = tq.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < fields.Length; i++) em.DestroyEntity(fields[i]);
            fields.Dispose();
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
            return e;
        }

        // near = 반경 안 표적 칸(체비셰프 ≤1 이웃), far = 확실한 반경 밖 칸(체비셰프 >4 →
        // near 로부터 ≥3 > 시험 반경 2). OnPlaceStunNearbyTest 의 검증된 스캔을 따른다.
        private static void FindWalkCells(BattleBridge bridge, EntityManager em,
            out Vector2Int near, out Vector2Int far)
        {
            var catalogs = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.Greater(catalogs.Length, 0, "DefenderCatalog");
            var probe = catalogs[0].ById("ranger");
            Assert.IsNotNull(probe, "probe 유닛(ranger)");

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");

            bool IsWalk(int x, int y)
                => x >= 0 && y >= 0 && x < ff.gridSize.x && y < ff.gridSize.y
                   && ff.walkMask[y * ff.gridSize.x + x] != 0;

            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, probe, out _)) continue;
                    Vector2Int? n = null, f = null;
                    for (int dx = -6; dx <= 6; dx++)
                        for (int dy = -6; dy <= 6; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!IsWalk(x + dx, y + dy)) continue;
                            int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (n == null && cheb <= 1) n = new Vector2Int(x + dx, y + dy);
                            else if (f == null && cheb > 4) f = new Vector2Int(x + dx, y + dy);
                        }
                    if (n != null && f != null)
                    {
                        near = n.Value; far = f.Value;
                        return;
                    }
                }
            Assert.Fail("반경 안팎 Walk 칸 짝을 찾지 못했다");
            near = default; far = default;
        }

        private static float HpOf(EntityManager em, Entity e)
        {
            Assert.IsTrue(em.Exists(e), "계측 대상이 사라졌다 — 테스트 전제 붕괴");
            return em.GetComponentData<Health>(e).value;
        }

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
