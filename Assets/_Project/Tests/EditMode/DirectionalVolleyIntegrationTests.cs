using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEditor;
using Unity.Mathematics;
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
                                           float[] directionTs, float[] intervals)
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
                directionTs: new[] { 0.52f, 0.42f, 0.61f, 0.35f, 0.69f, 0.19f, 0.84f, 0.74f, 0.03f, 0.94f },
                intervals: new[] { 0f, 0f, 0f, 0f, 0f, 0.025f, 0f, 0f, 0.025f, 0f });

        private Entity CreateDirectionalDefender(float3 pos, int2 facing, float range,
                                                  float cooldown, float baseDamage,
                                                  PatternSpec? pattern = null,
                                                  float damageMul = 1f,
                                                  float hitDelaySec = 0f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new DeployedFacing { value = facing });
            _em.AddComponentData(e, new AttackState
            {
                range = range,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.Enemy,
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
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private Entity CreateTargetBoundDefender(float hitDelaySec = 0f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                targetMask = (int)Faction.Enemy,
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
            Assert.AreSame(shotgun.projectile, shotgunAbility.pattern.barrel);
            Assert.IsTrue(shotgunAbility.pattern.TryToSpec(3, out var shotgunSpec));
            Assert.AreEqual(10, shotgunSpec.shots.Length);
            Assert.AreEqual(-30f, shotgunSpec.minAngleDeg);
            Assert.AreEqual(30f, shotgunSpec.maxAngleDeg);
            Assert.AreEqual(0.05f, EmitterTick.TotalDuration(shotgunSpec), 1e-5f);
            Assert.AreEqual(4f, shotgun.attackRange);
            Assert.AreEqual(6f, shotgun.outputs[0].magnitude);
            Assert.AreEqual(14f, shotgun.projectile.speed);
            float[] expectedDirectionTs = { 0.52f, 0.42f, 0.61f, 0.35f, 0.69f, 0.19f, 0.84f, 0.74f, 0.03f, 0.94f };
            float[] expectedIntervals = { 0f, 0f, 0f, 0f, 0f, 0.025f, 0f, 0f, 0.025f, 0f };
            for (int i = 0; i < expectedDirectionTs.Length; i++)
            {
                Assert.AreEqual(expectedDirectionTs[i], shotgunSpec.shots[i].directionT, 1e-5f);
                Assert.AreEqual(expectedIntervals[i], shotgunSpec.shots[i].intervalAfterPreviousSec, 1e-5f);
            }

            var machineGun = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                "Assets/_Project/Data/Defenders/Defender_MachineGunner.asset");
            var machineAbility = machineGun.GetAbility<DirectionalVolleyAbility>();
            Assert.IsNotNull(machineAbility?.pattern);
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
        public void Shotgun_TriggerCreatesTenIrregularSpreadShots_WithDamageAndFourTileDistance()
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), range: 4f, cooldown: 2.2f,
                baseDamage: 4f, pattern: ShotgunPattern(), damageMul: 1.5f);
            CreateEnemy(new float3(4f * TileSize, 0f, 0f));

            var angles = new List<float>();
            var clusterSizes = new List<int>();
            int fired = 0;
            for (int frame = 0; frame < 60 && fired < 10; frame++)
            {
                Tick(0.01f);
                var requests = CollectRequests();
                if (requests.Length > 0) clusterSizes.Add(requests.Length);
                foreach (var request in requests)
                {
                    fired++;
                    angles.Add(math.degrees(math.atan2(request.direction.y, request.direction.x)));
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
            CollectionAssert.AreEqual(new[] { 5, 3, 2 }, clusterSizes, "한 번의 발사 안에서 5-3-2 마이크로 클러스터");
            angles.Sort();
            float[] expected = { -28.2f, -18.6f, -9f, -4.8f, 1.2f, 6.6f, 11.4f, 14.4f, 20.4f, 26.4f };
            CollectionAssert.AreEqual(expected, angles, new FloatComparer(0.02f),
                "균등 분할이 아닌 중심 밀집+불규칙 외곽 산개");
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

        [TestCase(false, TestName = "StartedShotgun_FiresAfterWitnessMovesOutOfLane")]
        [TestCase(true, TestName = "StartedShotgun_FiresAfterWitnessDies")]
        public void StartedShotgun_FiresAfterWitnessIsLostDuringWindup(bool killWitness)
        {
            var defender = CreateDirectionalDefender(
                float3.zero, new int2(1, 0), range: 4f, cooldown: 2.2f,
                baseDamage: 6f, pattern: ShotgunPattern(), hitDelaySec: 0.03f);
            var witness = CreateEnemy(new float3(3f, 0f, 0f));

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
                _em.SetComponentData(witness, LocalTransform.FromPosition(new float3(0f, 0f, 3f)));
            }

            Tick(0.02f);
            Assert.AreEqual(0, CollectRequests().Length, "남은 wind-up 동안에는 발사하지 않음");
            Tick(0.011f);

            var requests = CollectRequests();
            Assert.AreEqual(5, requests.Length,
                "START가 성사된 샷건은 witness 소실 후에도 첫 5발 클러스터를 발사");
            for (int i = 0; i < requests.Length; i++)
            {
                Assert.AreEqual(Entity.Null, requests[i].target);
                Assert.AreEqual(new float3(0f), requests[i].origin);
                Assert.AreEqual(4f * TileSize, requests[i].maxDistance, 1e-5f);
            }
            Assert.AreEqual(1, _em.GetBuffer<EmitterInstance>(defender).Length,
                "나머지 5발도 동일 trigger instance에서 계속 진행");
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
