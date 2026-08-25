using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.Battle.Skills
{
    // skill-layer-foundation unit 4 — 감지된 발동을 concrete 로 넘긴다.
    //
    // ⚠ **드레인 지점이 셋이다.** 하나로는 산술적으로 불가능하다 — 감지자들이 각자
    // same-frame 하류 계약을 갖는데 그 구간이 서로 겹치지 않는다:
    //
    //   · BossPeriodic(#4)  → ProjectileEmitter·ModifierApply·AggroState 가 같은 틱
    //   · AttackN(#35)      → 피해 정산(#36) · 발사(#40)
    //   · HealthThreshold(#45) → 궁극기 카운트다운(#46) · blink(#47)
    //
    // `#8 < #45` 이므로 한 지점이 「모든 감지 뒤 + 모든 하류 앞」일 수 없다. 어디에 두든
    // 일부 arm 이 1프레임 밀리고, 자장가·도발·오라·blink 가 전부 이산적으로 달라진다.
    //
    // ⚠ `BattleBridge.Update` 는 **원리적으로 탈락**이다. 라이브 루프가
    // `Mono Update → SimulationSystemGroup` 이라 그룹이 낸 이벤트는 **다음 틱** 브리지
    // 페이즈에 드레인된다. 하네스 스텝 순서(Bridge→ECS)가 이를 박제한다.
    //
    // ⚠ 「단일 클래스 · 인스턴스 3개」는 ECS 에서 문자 그대로는 불가능하다 — 한 시스템
    // 타입은 월드당 인스턴스 하나다. **공용 구현 하나 + 얇은 파생 3개**가 그 실체이고,
    // 로직은 이 base 에만 있다. 파생은 어트리뷰트만 갖는다.
    //
    // managed `SystemBase` 인 이유: 레지스트리가 managed 라 Burst ISystem 이 될 수 없다.
    // MonoBehaviour 가 아니므로 제약 1(브리지 유일 창구)과 충돌하지 않고, 제약 3 의
    // 「managed 참조가 진짜 필요할 때」 요건을 충족하는 첫 사례다.
    public abstract partial class SkillDispatchSystemBase : SystemBase
    {
        // 레지스트리와 어댑터는 **브리지가 주입**한다. 시스템이 스스로 만들면 배틀마다
        // 새로 생기고, 저작 계층(SO)에 닿을 방법도 없다.
        private static SkillRegistry _registry;
        private static EcsSkillContext _context;

        public static void Install(SkillRegistry registry, EcsSkillContext context)
        {
            _registry = registry;
            _context = context;
        }

        // ⚠ **이전 여부를 묻는 계측.**
        //
        // 이 카운터가 없으면 「그물이 초록」이 「이전이 됐다」를 뜻하지 않는다.
        // 실제로 한 번 당했다: 라우팅 분기를 payload 분기들 **뒤**에 두는 바람에
        // 이전한 스킬 셋이 여전히 legacy arm 을 탔는데, legacy 가 그대로 잘 돌아서
        // PlayMode 그물 전체가 초록이었다. 조용한 실패의 교과서적 사례다.
        //
        // 테스트는 이 값으로 「concrete 가 진짜 불렸나」를 단언한다.
        public static int ExecutedCount { get; private set; }
        public static void ResetExecutedCount() => ExecutedCount = 0;

        public static void Uninstall()
        {
            _registry = null;
            _context = null;
        }

        private EntityQuery _enemyQuery, _defQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<SkillFiredEventsSingleton>();
            // 후보 풀 소스. 어댑터는 풀을 **받아서** 쓴다 — 쿼리를 만들 수 있는 건
            // 시스템뿐이고, 그걸 어댑터에 넣으면 포트가 ECS 를 알게 된다.
            _enemyQuery = GetEntityQuery(
                ComponentType.ReadOnly<AttackUnitTag>(), ComponentType.ReadOnly<LocalTransform>());
            _defQuery = GetEntityQuery(
                ComponentType.ReadOnly<DefenderUnitTag>(), ComponentType.ReadOnly<LocalTransform>());
        }

        protected override void OnUpdate()
        {
            if (_registry == null || _context == null) return;

            var queue = SystemAPI.GetSingleton<SkillFiredEventsSingleton>().queue;
            if (queue.Count == 0) return;

            // ⚠ **시작 시점 스냅샷 1회.** 드레인 중에 의도가 새 감지를 성사시키면
            // (피해 intent → 같은 프레임 OnDamagedN) 재유입이 생긴다. 지금은 감지가
            // 분산돼 프레임 구조가 자연 차단기인데, 통합 드레인이 그걸 잃는다.
            int budget = queue.Count;

            var em = EntityManager;
            _context.Bind(
                em,
                SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true),
                SystemAPI.GetComponentLookup<FactionTag>(isReadOnly: true),
                SystemAPI.GetComponentLookup<AttackUnitTag>(isReadOnly: true),
                SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true),
                SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true),
                SystemAPI.GetComponentLookup<Health>(isReadOnly: true),
                TileSize, GridSize, Origin);

            // ⚠ 풀을 **프레임당 한 번** 짓는다. fire 당 재구축하면 발동이 몰리는 프레임에
            // 쿼리가 N번 돈다(unit 0 어댑터 계약). 드레인이 끝나면 해제한다.
            var enemyPool = _enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyPoolXf = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var defPool = _defQuery.ToEntityArray(Allocator.Temp);
            var defPoolXf = _defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            _context.BindPools(enemyPool, enemyPoolXf, defPool, defPoolXf);

            // 의도 싱크. 배선 안 된 의도는 어댑터가 loud 하게 거절한다.
            bool hasCc = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.EnemyCcEventsSingleton>(out var ccS);
            _context.BindCcSink(hasCc ? ccS.queue : default, hasCc);
            bool hasStat = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>(out var statS);
            _context.BindStatSink(hasStat ? statS.queue : default, hasStat);
            bool hasHit = SystemAPI.TryGetSingleton<Wassup.Battle.Combat.Projectile.ProjectileHitEventsSingleton>(out var hitS);
            _context.BindVisualSink(hasHit ? hitS.queue : default, hasHit);
            bool hasShieldVfx = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.ShieldGrantedEventsSingleton>(out var svS);
            _context.BindShieldVisualSink(hasShieldVfx ? svS.queue : default, hasShieldVfx);

            // 구조 변경은 여기서 스테이징하고 **이 OnUpdate 끝에** 재생한다.
            // 브리지 드레인 전에 materialize 돼야 캐리어가 그 프레임에 스폰된다
            // (AttackSystem dcCarrier·HealthThreshold 진동갑주 선례).
            bool hasBlink = SystemAPI.TryGetSingleton<Wassup.Battle.Movement.BlinkRequestEventsSingleton>(out var blS);
            _context.BindBlinkSink(hasBlink ? blS.queue : default, hasBlink);
            bool hasLeapVfx = SystemAPI.TryGetSingleton<Wassup.Battle.Combat.BossLeapVisualEventsSingleton>(out var lvS);
            _context.BindLeapVisualSink(hasLeapVfx ? lvS.queue : default, hasLeapVfx);
            bool hasUltVfx = SystemAPI.TryGetSingleton<Wassup.Battle.Combat.UltimateLeapVisualEventsSingleton>(out var uvS);
            _context.BindUltimateVisualSink(hasUltVfx ? uvS.queue : default, hasUltVfx);
            bool hasFf = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var ffS);
            _context.BindFlowField(in ffS, hasFf);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            _context.BindEcb(ecb, true);

            while (budget-- > 0 && queue.TryDequeue(out var evt))
            {
                if (evt.SkillId == SkillRegistry.LegacyArmId) continue;   // legacy arm 이 처리한다

                if (!_registry.TryGet(evt.SkillId, out var skill))
                {
                    // fail-closed. 배선 누락을 침묵으로 넘기면 「스킬이 안 나가는데
                    // 아무도 모르는」 상태가 된다.
                    UnityEngine.Debug.LogWarning(
                        $"[SkillDispatch] skillId {evt.SkillId} 가 레지스트리에 없다 — 발동을 버린다.");
                    continue;
                }

                // ⚠ 감지와 드레인 사이에 캐스터가 죽거나 슬롯이 무효가 될 수 있다.
                // 이 부류는 이미 한 번 잡은 전력이 있다 — "죽음 큐가 끼면 시체가 한 번 더
                // 스킬을 쓴다"(BossPeriodicTriggerSystem). 무효면 drop + loud.
                if (evt.Caster != Entity.Null && !em.Exists(evt.Caster))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SkillDispatch] skillId {evt.SkillId} 의 캐스터가 드레인 전에 사라졌다 — 발동을 버린다.");
                    continue;
                }

                var caster = BuildCaster(em, evt.Caster);
                var target = BuildTarget(em, evt);
                var p = new SkillParams(
                    evt.Magnitude, evt.Duration, evt.TileRange, evt.Period, evt.DataIndex,
                    evt.Selector, evt.Speed, evt.HitThreshold,
                    evt.SlamDamage, evt.SlamTileRange, evt.StackId, evt.VisualScale);

                skill.Execute(caster, in target, in p, _context);
                ExecutedCount++;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            _context.BindEcb(default, false);

            enemyPool.Dispose(); enemyPoolXf.Dispose();
            defPool.Dispose(); defPoolXf.Dispose();
        }

        // 격자 파라미터 — 파생이 채운다(호스트마다 같은 값이지만 base 가 싱글턴을 두 번
        // 읽지 않게 한다). 지금은 FlowField 에서 온다.
        protected abstract float TileSize { get; }
        protected abstract Unity.Mathematics.int2 GridSize { get; }
        protected abstract Unity.Mathematics.float3 Origin { get; }

        private static CasterRef BuildCaster(EntityManager em, Entity caster)
        {
            if (caster == Entity.Null || !em.Exists(caster))
                return CasterRef.Player(Faction.DefenderUnit);   // 액티브 = 플레이어 시전

            // 결정은 `FactionRelation.Resolve` 가 소유한다 — 여기서 4단 체인을 복제하면
            // 세 번째 사본이 된다(리뷰 L1). 그 파일이 「복제하면 조용히 갈린다」고 적어뒀다.
            bool hasTag = em.HasComponent<FactionTag>(caster);
            var faction = FactionRelation.Resolve(
                hasTag,
                hasTag ? em.GetComponentData<FactionTag>(caster).value : Faction.None,
                em.HasComponent<AttackUnitTag>(caster),
                em.HasComponent<DefenderUnitTag>(caster));

            int id = em.HasComponent<SimEntityId>(caster)
                ? em.GetComponentData<SimEntityId>(caster).value
                : SimEntityId.Unassigned;

            return CasterRef.OfUnit(new SkillEntityId(id), faction);
        }

        private static SkillTarget BuildTarget(EntityManager em, in SkillFiredEvent evt)
        {
            if (evt.Target == Entity.Null || !em.Exists(evt.Target))
                return SkillTarget.None;
            int id = em.HasComponent<SimEntityId>(evt.Target)
                ? em.GetComponentData<SimEntityId>(evt.Target).value
                : SimEntityId.Unassigned;
            return SkillTarget.OfUnit(new SkillEntityId(id), default);
        }
    }
}
