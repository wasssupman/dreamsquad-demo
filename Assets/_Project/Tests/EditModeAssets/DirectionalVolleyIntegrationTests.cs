using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-shot-sequence unit 2 — 진짜 producer(AttackSystem)와 consumer
    // (ProjectileEmitterSystem)를 같은 BattleSim 순서로 돌린다. Bridge 경계 직전
    // 산출물인 ProjectileSpawnRequest carrier를 관찰해 trigger 1회→instance 1개→
    // N발, 실효 damage/거리 snapshot, lane·CC 독립 완주를 고정한다.
    public class DirectionalVolleyIntegrationTests
    {
        private const float TileSize = 1.25f;

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DirectionalVolleyIntegrationTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileEmitterSystem>());
            _simGroup.SortSystems();

            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(),
                new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(),
                new EnemyCcEventsSingleton { queue = _ccQueue });

            _em.AddComponentData(_em.CreateEntity(), new FlowFieldSingleton
            {
                tileSize = TileSize,
                gridSize = new int2(32, 32),
                origin = float3.zero,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackEventQueue.IsCreated) _attackEventQueue.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private static PatternSpec Pattern(float damage, float minAngle, float maxAngle,
                                           float[] directionTs, float[] intervals,
                                           bool randomize = false,
                                           float randomIntervalMin = 0f,
                                           float randomIntervalMax = 0f)
        {
            var shots = default(FixedList128Bytes<PatternShotSpec>);
            for (int i = 0; i < directionTs.Length; i++)
            {
                shots.Add(new PatternShotSpec
                {
                    directionT = directionTs[i],
                    intervalAfterPreviousSec = intervals[i],
                });
            }

            return new PatternSpec
            {
                barrelDataIndex = 3,
                damage = damage,
                selection = PatternSelectionRule.None,
                minAngleDeg = minAngle,
                maxAngleDeg = maxAngle,
                shots = shots,
                randomizeShotsPerTrigger = randomize,
                randomIntervalMinSec = randomIntervalMin,
                randomIntervalMaxSec = randomIntervalMax,
            };
        }

        private static PatternSpec MachineGunPattern()
        {
            var directions = new float[10];
            var intervals = new float[10];
            for (int i = 0; i < 10; i++)
            {
                directions[i] = 0.5f;
                intervals[i] = i == 0 ? 0f : 0.1f;
            }
            return Pattern(999f, 0f, 0f, directions, intervals);
        }

        private static PatternSpec ShotgunPattern()
            => Pattern(
                damage: 999f,
                minAngle: -30f,
                maxAngle: 30f,
                directionTs: new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
                intervals: new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                randomize: true,
                randomIntervalMin: 0.006f,
                randomIntervalMax: 0.018f);

        private Entity CreateDirectionalDefender(float3 pos, int2 facing, float range,
                                                  float cooldown, float baseDamage,
                                                  PatternSpec? pattern = null,
                                                  float damageMul = 1f,
                                                  float hitDelaySec = 0f,
                                                  bool addFacing = true)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            if (addFacing)
                _em.AddComponentData(e, new DeployedFacing { value = facing });
            _em.AddComponentData(e, new AttackState
            {
                range = range,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
                hitDelaySec = hitDelaySec,
            });
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = baseDamage },
            });
            _em.AddComponentData(e, new ProjectileRef
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                speed = 22f,
                hitThreshold = 0.4f,
                visualScale = 0.5f,
                dataIndex = 3,
            });
            _em.AddComponentData(e, new ModifierStats
            {
                damageMul = damageMul,
                attackSpeedMul = 1f,
                dmgTakenMul = 1f,
                moveSpeedMul = 1f,
                damageVsCcMul = 1f,
                maxHealthMul = 1f,
            });

            if (pattern.HasValue)
            {
                _em.AddBuffer<PatternSlot>(e);
                _em.AddBuffer<EmitterInstance>(e);
                var slots = _em.GetBuffer<PatternSlot>(e);
                slots.Add(new PatternSlot
                {
                    spec = pattern.Value,
                    template = new ProjectileSpawnRequest
                    {
                        movement = MovementKind.DirectionalLinear,
                        payload = PayloadKind.PathHit,
                        speed = 22f,
                        hitThreshold = 0.4f,
                        visualScale = 0.5f,
                        dataIndex = 3,
                        owner = e,
                        targetFaction = ProjectileTargetFaction.Enemy,
                    },
                });
            }
            return e;
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private Entity CreateTargetBoundDefender(float hitDelaySec = 0f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                targetMask = (int)Faction.EnemyUnit,
                attackTargetCount = 1,
                hitDelaySec = hitDelaySec,
            });
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 8f },
            });
            _em.AddComponentData(e, new ProjectileRef
            {
                movement = MovementKind.HomingToEntity,
                payload = PayloadKind.SingleSplash,
                speed = 12f,
                hitThreshold = 0.35f,
                visualScale = 0.9f,
                dataIndex = 0,
            });
            return e;
        }

        private ProjectileSpawnRequest[] CollectRequests()
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            using var requests = query.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            return requests.ToArray();
        }

        private void ClearRequests()
        {
            using (var carriers = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>()))
                _em.DestroyEntity(carriers);
            using (var requests = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>()))
                _em.RemoveComponent<ProjectileSpawnRequest>(requests);
        }

        [Test]
        public void LaneGate_FiresOnlyWhenEnemyStandsInTheFacingLane()
        {
            CreateDirectionalDefender(float3.zero, new int2(1, 0), 5f, 1f, 8f);
            CreateEnemy(new float3(0f, 0f, 3f));

            Tick();
            Assert.AreEqual(0, CollectRequests().Length);

            CreateEnemy(new float3(3f, 0f, 0f));
            Tick();
            var requests = CollectRequests();
            Assert.AreEqual(1, requests.Length);
            Assert.AreEqual(new float2(1f, 0f), requests[0].direction);
            Assert.AreEqual(Entity.Null, requests[0].target);
        }

        [Test]
        public void AuthoredDefenderPatterns_MatchShotgunAndMachineGunContracts()
        {
            var shotgun = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                "Assets/_Project/Data/Defenders/Defender_Shotgunner.asset");
            var shotgunAbility = shotgun.GetAbility<DirectionalVolleyAbility>();
            Assert.IsNotNull(shotgunAbility?.pattern);
            Assert.IsFalse(shotgun.RequiresFacing,
                "샷건너는 Archer/Ranger와 같은 일반 배치 경로를 사용한다");
            Assert.AreSame(shotgun.projectile, shotgunAbility.pattern.barrel);
            Assert.IsTrue(shotgunAbility.pattern.TryToSpec(3, out var shotgunSpec));
            Assert.AreEqual(10, shotgunSpec.shots.Length);
            Assert.AreEqual(-30f, shotgunSpec.minAngleDeg);
            Assert.AreEqual(30f, shotgunSpec.maxAngleDeg);
            Assert.IsTrue(shotgunSpec.randomizeShotsPerTrigger);
            Assert.AreEqual(0.006f, shotgunSpec.randomIntervalMinSec, 1e-5f);
            Assert.AreEqual(0.018f, shotgunSpec.randomIntervalMaxSec, 1e-5f);
            // attackRange·magnitude 는 시트(Defenders 탭)가 정본이고 로그인 자동 임포트가
            // 매번 에셋에 덮어쓴다 — 값은 자유 튜닝 대상이라 리터럴로 못박지 않는다
            // (test-suite-fast-lane unit 1, 전례: WaveKillBudgetPinTests). 여기서 지키는
            // 것은 «사거리와 피해가 살아 있다» 는 구조뿐이다.
            Assert.Greater(shotgun.attackRange, 0f, "사거리 0 이면 샷건이 영원히 침묵한다");
            Assert.Greater(shotgun.outputs[0].magnitude, 0f, "피해 0 이면 10발이 전부 공포탄이다");
            Assert.AreEqual(14f, shotgun.projectile.speed);
            Assert.AreEqual(0.7f, shotgun.projectile.visualScale, 1e-5f);
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Shard01.prefab"),
                shotgun.projectile.projectilePrefab,
                "샷건 10발은 긴 fireball trail 대신 정리된 GA Shard01을 사용한다");
            Assert.AreEqual(1,
                shotgun.projectile.projectilePrefab.GetComponentsInChildren<ParticleSystem>(true).Length);
            var pelletTrails =
                shotgun.projectile.projectilePrefab.GetComponentsInChildren<TrailRenderer>(true);
            Assert.AreEqual(2, pelletTrails.Length);
            for (int i = 0; i < pelletTrails.Length; i++)
                Assert.LessOrEqual(pelletTrails[i].time, 0.0751f,
                    "10발 spread의 trail은 0.3초 fireball처럼 길게 남지 않아야 한다");
            Assert.AreEqual(0,
                shotgun.projectile.projectilePrefab.GetComponentsInChildren<Rigidbody>(true).Length,
                "vendor Rigidbody가 ECS 이동과 경쟁하면 안 된다");
            Assert.AreEqual(0,
                shotgun.projectile.projectilePrefab.GetComponentsInChildren<Collider>(true).Length,
                "vendor Collider가 ECS hit 판정과 경쟁하면 안 된다");
            for (int i = 0; i < shotgunSpec.shots.Length; i++)
            {
                Assert.AreEqual(0.5f, shotgunSpec.shots[i].directionT, 1e-5f,
                    "authored step은 발수 placeholder이고 runtime trigger가 실제 방향을 스냅샷한다");
                Assert.AreEqual(0f, shotgunSpec.shots[i].intervalAfterPreviousSec, 1e-5f);
            }

            var machineGun = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                "Assets/_Project/Data/Defenders/Defender_MachineGunner.asset");
            var machineAbility = machineGun.GetAbility<DirectionalVolleyAbility>();
            Assert.IsNotNull(machineAbility?.pattern);
            Assert.IsTrue(machineGun.RequiresFacing, "머신거너의 기존 2단계 방향 배치는 유지한다");
            Assert.AreSame(machineGun.projectile, machineAbility.pattern.barrel);
            Assert.IsTrue(machineAbility.pattern.TryToSpec(4, out var machineSpec));
            Assert.AreEqual(10, machineSpec.shots.Length);
            Assert.AreEqual(0.9f, EmitterTick.TotalDuration(machineSpec), 1e-5f);
            Assert.AreEqual(0f, machineGun.hitDelaySec,
                "머신거너는 START와 첫 탄이 같은 프레임인 즉시 방향 공격");
            for (int i = 0; i < machineSpec.shots.Length; i++)
                Assert.AreEqual(0.5f, machineSpec.shots[i].directionT);

            var bombMan = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                "Assets/_Project/Data/Defenders/Defender_BombMan.asset");
            Assert.AreEqual(0f, bombMan.hitDelaySec,
                "폭탄맨은 target RESOLVE가 아니라 별도 blind-fire 분기를 즉시 사용");
        }

        [Test]
        public void Shotgun_NormalTargetTriggerCreatesTenRandomSpreadShots_WithDamageAndFourTileDistance()
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), range: 4f, cooldown: 2.2f,
                baseDamage: 4f, pattern: ShotgunPattern(), damageMul: 1.5f,
                addFacing: false);
            CreateEnemy(new float3(3f * TileSize, 0f, TileSize));

            var angles = new List<float>();
            var fireTimes = new List<float>();
            int fired = 0;
            float elapsed = 0f;
            for (int frame = 0; frame < 300 && fired < 10; frame++)
            {
                Tick(0.001f);
                elapsed += 0.001f;
                var requests = CollectRequests();
                Assert.LessOrEqual(requests.Length, 1,
                    "1ms 관측에서 여러 탄이 같은 timestamp에 몰리면 개별 interval이 아니다");
                foreach (var request in requests)
                {
                    fired++;
                    fireTimes.Add(elapsed);
                    float angle = math.degrees(math.atan2(request.direction.y, request.direction.x));
                    angles.Add(angle);
                    Assert.AreEqual(6f, request.damage, 1e-5f,
                        "pattern authored damage가 아니라 trigger 시점 실효 damage를 전탄 snapshot");
                    Assert.AreEqual(4f * TileSize, request.maxDistance, 1e-5f,
                        "샷건 개별 탄환 lifecycle은 4타일 물리 거리");
                    Assert.AreEqual(float3.zero, request.origin);
                    Assert.AreEqual(defender, request.owner);
                }
                ClearRequests();
            }

            Assert.AreEqual(10, fired);
            float baseAngle = math.degrees(math.atan2(1f, 3f));
            for (int i = 0; i < angles.Count; i++)
                Assert.That(angles[i], Is.InRange(baseAngle - 30f, baseAngle + 30f),
                    "각 탄은 START 타겟 기준 min/max spread 안에 있어야 한다");
            for (int i = 1; i < fireTimes.Count; i++)
                Assert.That(fireTimes[i] - fireTimes[i - 1], Is.InRange(0.005f, 0.019f),
                    "각 탄은 SO의 랜덤 interval 범위를 프레임 양자화 안에서 따라야 한다");
            Assert.Greater(new HashSet<float>(angles).Count, 7,
                "고정 균등 분할이나 한 방향 반복이 아니라 trigger별 난수 방향이어야 한다");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(defender).Length);
        }

        [Test]
        public void MachineGun_FiresTenAtPointOneSecondIntervals_AndDefersNextTrigger()
        {
            const float dt = 0.01f;
            CreateDirectionalDefender(float3.zero, new int2(1, 0), 4f, 1.6f, 5f, MachineGunPattern());
            CreateEnemy(new float3(3f, 0f, 0f));

            var fireTimes = new List<float>();
            float elapsed = 0f;
            while (elapsed < 2.7f && fireTimes.Count < 11)
            {
                Tick(dt);
                elapsed += dt;
                int count = CollectRequests().Length;
                for (int i = 0; i < count; i++) fireTimes.Add(elapsed);
                ClearRequests();
            }

            Assert.AreEqual(11, fireTimes.Count, "첫 10발과 다음 trigger의 첫 탄");
            for (int i = 2; i < 10; i++)
                Assert.AreEqual(0.1f, fireTimes[i] - fireTimes[i - 1], 0.011f);
            Assert.AreEqual(2.5f, fireTimes[10], 0.03f,
                "1.6초 기본 cooldown + sequence 0.9초 뒤 다음 trigger");
        }

        [Test]
        public void ActiveSequence_CompletesAfterLaneEmptiesAndHostBecomesActionLocked()
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), 4f, 2f, 5f, MachineGunPattern());
            var enemy = CreateEnemy(new float3(3f, 0f, 0f));

            Tick(0.01f);
            Assert.AreEqual(1, CollectRequests().Length);
            ClearRequests();

            _em.DestroyEntity(enemy);
            _em.GetBuffer<CcEffect>(defender).Add(new CcEffect
            {
                kind = CcKind.Sleep,
                remainingTime = 10f,
            });

            int remainingShots = 0;
            for (int i = 0; i < 120; i++)
            {
                Tick(0.01f);
                remainingShots += CollectRequests().Length;
                ClearRequests();
            }

            Assert.AreEqual(9, remainingShots, "시작된 sequence는 lane/CC와 무관하게 전탄 완주");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(defender).Length);
        }

        [TestCase(false, TestName = "StartedShotgun_FiresAfterTargetMovesOutOfRange")]
        [TestCase(true, TestName = "StartedShotgun_FiresAfterWitnessDies")]
        public void StartedShotgun_FiresAfterTargetIsLostDuringWindup(bool killWitness)
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), range: 4f, cooldown: 2.2f,
                baseDamage: 6f, pattern: ShotgunPattern(), hitDelaySec: 0.03f,
                addFacing: false);
            var witness = CreateEnemy(new float3(3f, 0f, 1f));

            Tick(0.01f);
            Assert.AreEqual(1, _attackEventQueue.Count, "START 모션은 한 번만 발생");
            Assert.AreEqual(0, CollectRequests().Length, "wind-up 중에는 아직 발사하지 않음");

            if (killWitness)
            {
                _em.SetComponentData(witness, new Health { value = 0f, max = 500f });
                _em.AddComponent<DeadTag>(witness);
            }
            else
            {
                _em.SetComponentData(witness,
                    LocalTransform.FromPosition(new float3(0f, 0f, 8f * TileSize)));
            }

            Tick(0.02f);
            Assert.AreEqual(0, CollectRequests().Length, "남은 wind-up 동안에는 발사하지 않음");
            Tick(0.011f);

            var requests = CollectRequests();
            Assert.AreEqual(1, requests.Length,
                "START가 성사된 샷건은 target 소실 후에도 랜덤 sequence 첫 탄을 발사");
            for (int i = 0; i < requests.Length; i++)
            {
                Assert.AreEqual(Entity.Null, requests[i].target);
                Assert.AreEqual(new float3(0f), requests[i].origin);
                Assert.AreEqual(4f * TileSize, requests[i].maxDistance, 1e-5f);
                float angle = math.degrees(math.atan2(requests[i].direction.y, requests[i].direction.x));
                float startAngle = math.degrees(math.atan2(1f, 3f));
                Assert.That(angle, Is.InRange(startAngle - 30f, startAngle + 30f),
                    "RESOLVE 시 타겟이 없어도 START 방향이 spread 기준축이어야 한다");
            }
            Assert.AreEqual(1, _em.GetBuffer<EmitterInstance>(defender).Length,
                "나머지 9발도 동일 trigger instance에서 계속 진행");
        }

        [Test]
        public void PatternAttack_PushesOneInstancePerTrigger_BeforeEmitterConsumesIt()
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), 4f, 2f, 5f, MachineGunPattern());
            CreateEnemy(new float3(3f, 0f, 0f));

            Tick(0.01f);

            Assert.AreEqual(1, CollectRequests().Length, "trigger frame에는 첫 탄 carrier");
            Assert.IsFalse(_em.HasComponent<ProjectileSpawnRequest>(defender),
                "pattern defender 본체에 direct request를 더하면 한 trigger가 이중 발사된다");
            using (var carriers = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>()))
                Assert.AreEqual(1, carriers.CalculateEntityCount());
            Assert.AreEqual(1, _em.GetBuffer<EmitterInstance>(defender).Length,
                "첫 탄 소비 뒤에도 같은 trigger의 단일 진행 instance만 남는다");
        }

        [Test]
        public void NonFacingDefender_KeepsTargetBoundProjectilePath()
        {
            CreateTargetBoundDefender();
            var enemy = CreateEnemy(new float3(1f, 0f, 3f));

            Tick();
            var requests = CollectRequests();
            Assert.AreEqual(1, requests.Length);
            Assert.AreEqual(MovementKind.HomingToEntity, requests[0].movement);
            Assert.AreEqual(enemy, requests[0].target);
        }

        [Test]
        public void NonFacingTargetBoundProjectile_StillLapsesWhenTargetDiesDuringWindup()
        {
            var defender = CreateTargetBoundDefender(hitDelaySec: 0.03f);
            var enemy = CreateEnemy(new float3(1f, 0f, 3f));

            Tick(0.01f);
            Assert.AreEqual(1, _attackEventQueue.Count, "target-bound 공격도 START 모션은 발생");
            Assert.AreEqual(0, CollectRequests().Length);

            _em.SetComponentData(enemy, new Health { value = 0f, max = 500f });
            _em.AddComponent<DeadTag>(enemy);
            Tick(0.02f);
            Tick(0.011f);

            Assert.AreEqual(0, CollectRequests().Length,
                "호밍/근접의 RESOLVE 타깃 재판정 계약은 방향탄 보정으로 바뀌지 않음");
            Assert.AreEqual(0f, _em.GetComponentData<AttackState>(defender).hitDelayRemaining, 1e-5f);
        }

        private sealed class FloatComparer : System.Collections.IComparer
        {
            private readonly float _epsilon;

            public FloatComparer(float epsilon) => _epsilon = epsilon;

            public int Compare(object x, object y)
                => math.abs((float)x - (float)y) <= _epsilon ? 0 : ((float)x).CompareTo((float)y);
        }
    }
}
