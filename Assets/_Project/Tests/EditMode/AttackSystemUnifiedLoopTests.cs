using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // Regression gate for the unified attacker loop (spec: attack-system-loop-unify Unit 0).
    // Covers EditMode equivalents of PlayMode scenarios U1–U8, plus three additional guards
    // (self-exclusion, PendingDeployment exclusion, DeadTag exclusion).
    public class AttackSystemUnifiedLoopTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackSystemUnifiedLoopTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<StatModifierTickSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ModifierStatsAggregateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            // Singleton: unified attack visual events (Spine animation trigger for any attacker)
            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            var attackSingleton = _em.CreateEntity();
            _em.AddComponentData(attackSingleton, new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });

            // Singleton: enemy CC events (knockback)
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            var ccSingleton = _em.CreateEntity();
            _em.AddComponentData(ccSingleton, new EnemyCcEventsSingleton { queue = _ccQueue });
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

        // Creates an attacker entity with the given faction, position, and AttackState.
        private Entity CreateAttacker(
            Faction faction,
            float3 position,
            float damage,
            float range,
            float cooldownDuration,
            int targetMask,
            int attackTargetCount = 1,
            bool defenderTag = false,
            bool attackerTag = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(position));
            _em.AddComponentData(e, new FactionTag { value = faction });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range,
                cooldownDuration = cooldownDuration,
                cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount,
                targetMask = targetMask,
            });
            if (defenderTag) _em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) _em.AddComponent<AttackUnitTag>(e);
            if ((defenderTag || attackerTag) && damage > 0f)
            {
                var outputs = _em.AddBuffer<AttackOutputElement>(e);
                outputs.Add(new AttackOutputElement
                {
                    value = new AttackOutput
                    {
                        kind = AttackOutputKind.Damage,
                        magnitude = damage,
                    }
                });
            }
            return e;
        }

        // Creates a pure target entity (no AttackState).
        private Entity CreateTarget(
            Faction faction,
            float3 position,
            bool defenderTag = false,
            bool attackerTag = false,
            bool pendingDeployment = false,
            bool deadTag = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(position));
            _em.AddComponentData(e, new FactionTag { value = faction });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            if (defenderTag) _em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) _em.AddComponent<AttackUnitTag>(e);
            if (pendingDeployment) _em.AddComponent<PendingDeployment>(e);
            if (deadTag) _em.AddComponent<DeadTag>(e);
            return e;
        }

        // ─── U1: Defender with ProjectileRef → produces ProjectileSpawnRequest, no direct damage ───

        [Test]
        public void U1_Defender_ProjectileRef_Produces_SpawnRequest_Not_Direct_Damage()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new ProjectileRef
            {
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
                dataIndex = 0,
                splashRadius = 0f,
                splashDamageMul = 1f,
            });

            var enemy = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.IsTrue(_em.HasComponent<ProjectileSpawnRequest>(defender),
                "Defender with ProjectileRef should have ProjectileSpawnRequest added");
            Assert.AreEqual(MovementKind.HomingToEntity, _em.GetComponentData<ProjectileSpawnRequest>(defender).movement,
                "default ProjectileRef (movement=0) should stage a homing request");
            Assert.AreEqual(5f, _em.GetComponentData<ProjectileSpawnRequest>(defender).damage, 1e-4f,
                "Projectile defender should snapshot Damage output as projectile damage");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "No direct IncomingDamage when projectile path is taken");
        }

        // ─── U2: Defender melee AoE (attackTargetCount=2) hits two enemies ───

        [Test]
        public void U2_Defender_Melee_AoE_Hits_Two_Targets()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 4f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                attackTargetCount: 2,
                defenderTag: true);

            var enemy1 = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);
            var enemy2 = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy1).Length, "Enemy1 should be hit");
            Assert.AreEqual(4f, _em.GetBuffer<IncomingDamage>(enemy1)[0].amount, 1e-4f);
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy2).Length, "Enemy2 should be hit by AoE");
            Assert.AreEqual(4f, _em.GetBuffer<IncomingDamage>(enemy2)[0].amount, 1e-4f);
        }

        // ─── U3: Defender with DefenderCcData enqueues EnemyCcEvent (knockback) ───

        [Test]
        public void U3_Defender_DefenderCcData_Enqueues_Knockback_CC()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 3f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new DefenderCcData
            {
                knockbackDistance = 2f,
                knockbackDuration = 0.5f,
            });

            var enemy = CreateTarget(Faction.Enemy, new float3(3f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _ccQueue.Count, "One knockback CC event should be enqueued");
            var ev = _ccQueue.Dequeue();
            Assert.AreEqual(enemy, ev.target);
            Assert.AreEqual(CcKind.Impulse, ev.effect.kind);
        }

        // ─── U4: Defender with damageMul (×1.5 boost + ×2.0 synergy) and attackSpeedMul (×2) via ModifierStats ───

        [Test]
        public void U4_Defender_Buff_Multipliers_Applied_To_Damage_And_Cooldown()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 10f, range: 10f, cooldownDuration: 2f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            // Wire ModifierStats: damageMul = 1.5 × 2.0 = 3.0, attackSpeedMul = 2.0 (cooldown ÷ 2).
            _em.AddComponentData(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f, moveSpeedMul = 1f });
            _em.AddComponent<ModifierStatsDirty>(defender);
            _em.SetComponentEnabled<ModifierStatsDirty>(defender, true);
            var slots = _em.AddBuffer<StatModifierSlot>(defender);
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul,      op = CombineOp.Multiplicative, magnitude = 1.5f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul,      op = CombineOp.Multiplicative, magnitude = 2f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.AttackSpeedMul, op = CombineOp.Multiplicative, magnitude = 2f });

            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick();

            // damage = 10 * 1.5 (boost) * 2.0 (synergy) = 30
            var dmgBuf = _em.GetBuffer<IncomingDamage>(enemy);
            Assert.AreEqual(1, dmgBuf.Length);
            Assert.AreEqual(30f, dmgBuf[0].amount, 1e-4f, "damageMul(boost) * damageMul(synergy) should multiply base damage");

            // cooldown reset = cooldownDuration / attackSpeedMul = 2.0 / 2.0 = 1.0
            var atkState = _em.GetComponentData<AttackState>(defender);
            Assert.AreEqual(1f, atkState.cooldownRemaining, 1e-4f, "attackSpeedMul 2.0 should halve reset interval");
        }

        // ─── U5: Enemy (AttackUnitTag) attacks defender → direct IncomingDamage, no projectile/CC ───

        [Test]
        public void U5_Enemy_Attacks_Defender_Direct_Damage_No_Event()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 6f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            var defender = CreateTarget(Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick();

            var dmgBuf = _em.GetBuffer<IncomingDamage>(defender);
            Assert.AreEqual(1, dmgBuf.Length, "Defender should receive direct IncomingDamage from enemy");
            Assert.AreEqual(6f, dmgBuf[0].amount, 1e-4f);

            Assert.IsFalse(_em.HasComponent<ProjectileSpawnRequest>(enemy),
                "Enemy should not generate ProjectileSpawnRequest");
        }

        // ─── U6: Enemy attacks blocking hazard → hazard receives IncomingDamage ───

        [Test]
        public void U6_Enemy_Attacks_Hazard_Damages_Hazard()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 4f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            // Hazard is closer so it should be preferred target
            var hazard = CreateTarget(Faction.BlockingHazard, new float3(1f, 0f, 0f));
            var defender = CreateTarget(Faction.Defender, new float3(5f, 0f, 0f), defenderTag: true);

            Tick();

            var hazardDmg = _em.GetBuffer<IncomingDamage>(hazard);
            Assert.AreEqual(1, hazardDmg.Length, "Hazard should receive IncomingDamage");
            Assert.AreEqual(4f, hazardDmg[0].amount, 1e-4f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(defender).Length,
                "Farther defender should not be targeted");
        }

        // ─── U7: Defender fire enqueues exactly one UnitAttackVisualEvent ───

        [Test]
        public void U7_Defender_Fire_Enqueues_UnitAttackVisualEvent()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            var enemy = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _attackEventQueue.Count,
                "Exactly one UnitAttackVisualEvent should be enqueued when defender fires");
            var ev = _attackEventQueue.Dequeue();
            Assert.AreEqual(defender, ev.attacker);
        }

        // ─── U8: Enemy fire also enqueues UnitAttackVisualEvent (unified channel) ───

        [Test]
        public void U8_Enemy_Fire_Enqueues_UnitAttackVisualEvent()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            var defender = CreateTarget(Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick();

            Assert.AreEqual(1, _attackEventQueue.Count,
                "Enemy fire must enqueue a UnitAttackVisualEvent so SpineUnitPool can play the attack animation");
            var ev = _attackEventQueue.Dequeue();
            Assert.AreEqual(enemy, ev.attacker);
        }

        // ─── Additional: Self-exclusion guard — attacker does not target itself ───

        [Test]
        public void SelfExclusion_Attacker_Does_Not_Target_Itself()
        {
            // Defender with both DefenderUnitTag and EnemyFaction mask — unusual but
            // tests the self-exclusion guard explicitly.
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Defender,   // would hit itself if no guard
                defenderTag: true);
            _em.AddBuffer<IncomingDamage>(defender); // already added by CreateAttacker

            Tick();

            // No valid target other than itself → no fire
            var selfDmg = _em.GetBuffer<IncomingDamage>(defender);
            Assert.AreEqual(0, selfDmg.Length,
                "Attacker must never select itself as target");
        }

        // ─── Additional: PendingDeployment excludes entity from attacker query ───

        [Test]
        public void PendingDeployment_Excludes_Attacker_From_Loop()
        {
            // Defender with PendingDeployment should not fire even if in range
            var pendingDefender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);
            _em.AddComponent<PendingDeployment>(pendingDefender);

            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "Attacker with PendingDeployment must not fire");
            Assert.AreEqual(0, _attackEventQueue.Count,
                "No attack event when PendingDeployment attacker is excluded");
        }

        // ─── Additional: DeadTag excludes entity from target pool ───

        [Test]
        public void DeadTag_Excludes_Target_From_Pool()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            // Only available target has DeadTag → should not be selected
            var deadEnemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true, deadTag: true);

            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(deadEnemy).Length,
                "DeadTag entity must be excluded from target pool");
            Assert.AreEqual(0, _attackEventQueue.Count,
                "No attack event when no valid targets exist");
        }

        // ─── projectile-trajectory-payload unit 5: a ballistic ProjectileRef stages a
        // BallisticArcToPoint spawn request (NOT a homing one), with the target's cell
        // locked as the impact and no tracked target entity. Guards the RESOLVE branch
        // that must fire before the homing request would be added. ───

        [Test]
        public void Ballistic_ProjectileRef_Stages_Ballistic_Request_With_Locked_Impact()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 7f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new ProjectileRef
            {
                speed = 10f,
                visualScale = 1f,
                dataIndex = 0,
                movement = MovementKind.BallisticArcToPoint,
                payload = PayloadKind.TileAoe,
                arcHeight = 2f,
                impactTileRange = 1,
            });

            var enemy = CreateTarget(Faction.Enemy, new float3(3f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.IsTrue(_em.HasComponent<ProjectileSpawnRequest>(defender),
                "ballistic defender stages a spawn request");
            var req = _em.GetComponentData<ProjectileSpawnRequest>(defender);
            Assert.AreEqual(MovementKind.BallisticArcToPoint, req.movement, "ballistic, not homing");
            Assert.AreEqual(PayloadKind.TileAoe, req.payload);
            Assert.AreEqual(Entity.Null, req.target, "ballistic tracks no target entity");
            Assert.AreEqual(7f, req.damage, 1e-4f, "damage = summed Damage output");
            Assert.AreEqual(1, req.impactTileRange);
            Assert.AreEqual(2f, req.arcHeight, 1e-4f);
            // Impact locked near the enemy's cell (x≈3), not left at the origin.
            Assert.Greater(req.impact.x, 2f, "impact locked to the target's cell, not the shooter");
            Assert.Less(req.impact.x, 4f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "no direct damage on the ballistic fire frame (resolves at impact)");
        }

        // ─── dreamcatcher-unit-trigger: 콕콕바늘(AttackN×ProjectileToTarget)이 MELEE
        // 디펜더(ProjectileRef 없음)에서도 트리거되는지 실증. 사용자 질문 검증
        // (2026-07-10): 부착/발동 경로에 근접 게이트가 없으므로 근접 유닛도 5회째
        // 공격에 니들 캐리어를 스폰해야 한다. 동시에 근접 직접타(IncomingDamage)는
        // 매 틱 들어가야 한다(투사체 경로가 아니라 근접 경로임을 증명). ───

        [Test]
        public void Melee_PokeNeedle_Fires_Needle_Carrier_On_Fifth_Attack()
        {
            // 근접 디펜더: ProjectileRef 없음 → RESOLVE 의 outputs(근접) 분기.
            // cooldown 을 dt 보다 작게 둬 매 틱 재발사.
            var melee = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 4f, range: 10f, cooldownDuration: 0.01f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);
            Assert.IsFalse(_em.HasComponent<ProjectileRef>(melee),
                "sanity: this defender is melee (no ProjectileRef)");

            // 콕콕바늘 슬롯: 5회마다 20뎀 호밍 니들(dataIndex 0 은 sim 레벨 임의값).
            var slots = _em.AddBuffer<DcTriggerSlot>(melee);
            slots.Add(new DcTriggerSlot
            {
                instanceId = 1,
                trigger = DcTriggerKind.AttackN,
                period = 5,
                counter = 0,
                payload = DcPayloadKind.ProjectileToTarget,
                magnitude = 20f,
                projectileDataIndex = 0,
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
            });

            // 근접 사거리(1타일)에 적.
            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            using var carrierQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<ProjectileRequestCarrier>(),
                ComponentType.ReadOnly<ProjectileSpawnRequest>());

            // 4회 공격: 카운트만 쌓이고 니들은 아직 안 나감.
            for (int i = 0; i < 4; i++) Tick();
            Assert.AreEqual(0, carrierQuery.CalculateEntityCount(),
                "니들은 5회째 전에 발사되면 안 된다(카운팅만)");
            Assert.AreEqual(4, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "근접 디펜더는 매 공격 직접타를 넣는다(투사체 경로 아님 → 근접 경로 증명)");

            // 5회째 공격: 니들 캐리어 스폰.
            Tick();
            Assert.AreEqual(1, carrierQuery.CalculateEntityCount(),
                "MELEE 디펜더도 5회째 공격에 콕콕바늘 캐리어를 스폰해야 한다");

            var carrier = carrierQuery.ToEntityArray(Allocator.Temp);
            var req = _em.GetComponentData<ProjectileSpawnRequest>(carrier[0]);
            carrier.Dispose();
            Assert.AreEqual(MovementKind.HomingToEntity, req.movement, "니들은 대상 호밍");
            Assert.AreEqual(20f, req.damage, 1e-4f, "니들 데미지는 flat magnitude(damageMul 미적용)");
            Assert.AreEqual(0, req.dataIndex, "슬롯의 투사체 데이터 인덱스 사용");
            Assert.AreEqual(enemy, req.target, "근접 유닛이 때리던 대상으로 호밍");
            Assert.AreEqual(5, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "5회째에도 근접 직접타는 그대로(니들은 별도 산출물)");
        }

        // ─── attack-decoupling unit 3 — 폭탄맨 사건 지점. 폭탄맨은 타겟팅/RESOLVE
        // 경로를 타지 않고 early-continue 하므로 dc 트리거가 영영 안 돌았다. 이제
        // **폭탄이 실제로 손을 떠난** 프레임이 1카운트이고, 니들 대상은 host 가
        // 아니라 페이로드가 스스로 고른다(unit 2 폴백). ───

        private Entity CreateBombThrower(float3 position, int2 facing, int landingTiles = 2)
        {
            // 폭탄맨도 AttackState 를 갖지만(쿨다운 소유) 타겟팅 루프는 타지 않는다.
            var e = CreateAttacker(
                Faction.Defender, position,
                damage: 0f, range: 5f, cooldownDuration: 0.01f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);
            _em.AddComponentData(e, new DeployedFacing { value = facing });
            _em.AddComponentData(e, new ProjectileRef { dataIndex = 0, speed = 10f, visualScale = 1f });
            _em.AddComponentData(e, new BombLauncherState
            {
                landingTiles = landingTiles,
                travelSec = 0.2f,
                fuseSec = 0.2f,
                aoeTileRange = 1,
                aoeTargetCap = 3,
                dmgBombDamage = 5f,
                rng = new Unity.Mathematics.Random(12345u),
            });
            return e;
        }

        [Test]
        public void BombThrower_PokeNeedle_FiresOnFifthBombWithSelfChosenTarget()
        {
            var bomber = CreateBombThrower(new float3(0f, 0f, 0f), new int2(1, 0));

            var slots = _em.AddBuffer<DcTriggerSlot>(bomber);
            slots.Add(new DcTriggerSlot
            {
                instanceId = 1,
                trigger = DcTriggerKind.AttackN,
                period = 5,
                counter = 0,
                payload = DcPayloadKind.ProjectileToTarget,
                magnitude = 20f,
                projectileDataIndex = 0,
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
                tileRange = 4, // 폴백 탐색 반경 — host 가 대상을 안 주므로 이게 유일한 수단
            });

            // 반경 안 적 2기. 니들은 **최근접**을 골라야 한다(폭탄 착지셀과 무관).
            var far = CreateTarget(Faction.Enemy, new float3(3f, 0f, 0f), attackerTag: true);
            var near = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            using var carrierQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<ProjectileRequestCarrier>(),
                ComponentType.ReadOnly<ProjectileSpawnRequest>());

            for (int i = 0; i < 4; i++) Tick();
            Assert.AreEqual(0, carrierQuery.CalculateEntityCount(),
                "5발째 전에는 니들이 나가면 안 된다(폭탄만 나간다)");

            Tick();
            Assert.AreEqual(1, carrierQuery.CalculateEntityCount(),
                "폭탄맨도 5번째 발사에 니들 캐리어를 스폰해야 한다");

            var carrier = carrierQuery.ToEntityArray(Allocator.Temp);
            var req = _em.GetComponentData<ProjectileSpawnRequest>(carrier[0]);
            carrier.Dispose();
            Assert.AreEqual(MovementKind.HomingToEntity, req.movement);
            Assert.AreEqual(20f, req.damage, 1e-4f, "flat magnitude(계약 7)");
            Assert.AreEqual(near, req.target,
                "host 가 대상을 안 주므로 페이로드가 스스로 최근접 적을 고른다");
            Assert.AreEqual(bomber, req.owner, "위협 귀속은 폭탄맨 본인");
            Assert.AreNotEqual(far, req.target);
        }

        [Test]
        public void BombThrower_PokeNeedle_DoesNotCountWhenBombCannotLaunch()
        {
            // 그리드 밖을 향해 배치 → BombLanding.ResolveCell 이 landValid=false.
            // 쿨다운은 돌지만 폭탄이 손을 떠나지 않으므로 카운트도 없다(계약 2).
            var bomber = CreateBombThrower(new float3(0f, 0f, 0f), new int2(-1, 0), landingTiles: 5);

            var slots = _em.AddBuffer<DcTriggerSlot>(bomber);
            slots.Add(new DcTriggerSlot
            {
                instanceId = 1,
                trigger = DcTriggerKind.AttackN,
                period = 1, // 매 발사마다 발동 — 카운트가 돌면 즉시 드러난다
                counter = 0,
                payload = DcPayloadKind.ProjectileToTarget,
                magnitude = 20f,
                projectileDataIndex = 0,
                speed = 10f,
                visualScale = 1f,
                tileRange = 4,
            });
            CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            using var carrierQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<ProjectileRequestCarrier>(),
                ComponentType.ReadOnly<ProjectileSpawnRequest>());

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(0, _em.GetBuffer<DcTriggerSlot>(bomber)[0].counter,
                "폭탄이 안 나간 프레임은 공격 사건이 아니다 — 카운터가 움직이면 안 된다");
            Assert.AreEqual(0, carrierQuery.CalculateEntityCount());
        }
    }
}
