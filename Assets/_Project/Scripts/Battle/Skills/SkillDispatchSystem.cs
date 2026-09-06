using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.Battle.Skills
{
    // skill-layer-foundation unit 4 — 감지된 발동을 concrete 로 넘긴다.
    //
    // ⚠ **드레인 지점은 하나가 아니다.** 하나로는 산술적으로 불가능하다 — 감지자들이 각자
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

        // ⚠ **seam 별로 따로 센다**(투트랙 리뷰 잔여 리스크). 합계만 있으면 「하나라도
        // 불렸다」밖에 못 말하고, 라우팅이 **한 seam 에서만** 끊긴 상태가 그대로 초록이 된다 —
        // `7f902e55` 가 잡은 실패 유형의 나머지 절반이 정확히 그것이다.
        // 주기 seam 만 실주행하는 그물로는 경계 seam 의 이전 여부를 알 수 없다.
        private static readonly int[] _perSeam = new int[(int)SkillSeam.Count];
        public static int ExecutedCountOf(SkillSeam seam) => _perSeam[(int)seam];

        // ⚠ **seam 계수기만으론 귀속이 없다**(unit 8 리뷰 H-3). 라이브 판에는 남의 주기
        // 스킬이 늘 돌기 때문에, 「이 배치가 발화했나」를 seam 합계로 재면 **배치한 유닛의
        // 라우팅이 통째로 죽어 있어도 초록**이 난다. 그래서 스킬 단위로도 센다 —
        // 옛 그물(payload 별 loud 경고)이 갖고 있던 해상도를 그대로 돌려놓는 것이다.
        private static readonly Dictionary<int, int> _perSkill = new Dictionary<int, int>();
        public static int ExecutedCountOfSkill(int skillId)
            => _perSkill.TryGetValue(skillId, out var n) ? n : 0;

        public static void ResetExecutedCount()
        {
            ExecutedCount = 0;
            for (int i = 0; i < _perSeam.Length; i++) _perSeam[i] = 0;
            _perSkill.Clear();
        }

        // 파생이 자기 자리를 선언한다.
        protected abstract SkillSeam Seam { get; }

        // ⚠ **이 seam 이 도는 시점에 시전자가 살아 있는가.** 기본은 「그렇다」이고,
        // 아래 드레인 가드가 죽은 시전자의 발동을 버린다(보스 주기에서 「시체가 한 번 더
        // 스킬을 쓴다」를 잡은 가드다).
        //
        // 자기 죽음 seam 만 `false` 다 — 거기선 시전자가 **없는 것이 정상**이라
        // 같은 가드가 「작별 선물이 영영 안 터진다」로 뒤집힌다. 프레임 창은 seam 의
        // 성질이므로 이벤트가 아니라 seam 이 선언한다.
        protected virtual bool RequiresLiveCaster => true;

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

            // ⚠ **재진입 금지**(재리뷰 M-5). 어댑터는 seam 전체가 **공유하는 한 인스턴스**라
            // 드레인 안에서 또 드레인이 열리면 안쪽이 풀·ECB 바인딩을 덮어쓰고, 안쪽
            // `finally` 가 그것을 **해제한 채로** 바깥에 제어를 돌려준다.
            // 그래서 피해는 무한루프가 아니라 **바깥 드레인이 후보 풀을 잃는 것**이다 —
            // 그 뒤의 스킬은 대상 0으로 조용히 no-op 이 된다(예외도 로그도 없다).
            // 오늘 재진입 경로는 브리지의 `RunImmediateSkills()` 뿐이고 실행 중엔 브리지로
            // 돌아가지 않아 닿지 않는다. 그 전제가 깨지는 순간을 침묵으로 두지 않는다.
            if (_draining)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SkillDispatch] {Seam} seam 드레인이 재진입했다 — 무시한다. "
                    + "어댑터 바인딩이 하나뿐이라 중첩 드레인은 바깥 드레인의 후보 풀을 앗아간다.");
                return;
            }
            _draining = true;
            try
            {

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
                SystemAPI.GetComponentLookup<Wassup.Battle.Movement.PathFollowState>(isReadOnly: true),
                SystemAPI.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true),
                TileSize, GridSize, Origin);

            // ⚠ 풀을 **프레임당 한 번** 짓는다. fire 당 재구축하면 발동이 몰리는 프레임에
            // 쿼리가 N번 돈다(unit 0 어댑터 계약). 드레인이 끝나면 해제한다.
            _context.ResetDrainWarnings();
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
            bool hasStack = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>(out var stackS);
            _context.BindStackSink(hasStack ? stackS.queue : default, hasStack);
            bool hasDot = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.DotApplyEventsSingleton>(out var dotS);
            _context.BindDotSink(hasDot ? dotS.queue : default, hasDot);
            bool hasKnock = SystemAPI.TryGetSingleton<Wassup.Battle.Combat.KnockupVisualEventsSingleton>(out var knockS);
            _context.BindKnockupSink(hasKnock ? knockS.queue : default, hasKnock);
            bool hasHazard = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.HazardSpawnRequestsSingleton>(out var hazS);
            _context.BindHazardSink(hasHazard ? hazS.queue : default, hasHazard);
            bool hasAcquire = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.AggroAcquireEventsSingleton>(out var acqS);
            _context.BindTauntSink(hasAcquire ? acqS.queue : default, hasAcquire);
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

            try
            {
            while (budget-- > 0 && queue.TryDequeue(out var evt))
            {
                // 스킬이 아닌 것(발동 규칙·공격의 성질)은 여기 오지 않아야 하지만,
                // 오면 조용히 지나간다 — bake 게이트가 앞에서 거절하므로 여기는 방어선이다.
                if (evt.SkillId == SkillRegistry.NotRouted) continue;

                // ⚠ **남의 seam 것은 돌려보낸다**(unit 3e). `budget` 이 시작 시점 개수라
                // 루프는 반드시 끝나고, 돌려보낸 것은 자기 seam 이 이 프레임 뒤쪽에서
                // (이미 지났으면 다음 프레임에) 가져간다.
                if (evt.Seam == SkillSeam.None)
                {
                    // 생산자가 seam 을 안 채웠다. 돌려보내면 영원히 돈다 — 버리고 짖는다.
                    UnityEngine.Debug.LogWarning(
                        $"[SkillDispatch] skillId {evt.SkillId} 이 seam 을 선언하지 않았다 — "
                        + "발동을 버린다. 생산자가 SkillFiredEvent.Seam 을 채워야 한다.");
                    continue;
                }
                if (evt.Seam != Seam) { queue.Enqueue(evt); continue; }

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
                if (RequiresLiveCaster && evt.Caster != Entity.Null && !em.Exists(evt.Caster))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SkillDispatch] skillId {evt.SkillId} 의 캐스터가 드레인 전에 사라졌다 — 발동을 버린다.");
                    continue;
                }

                var caster = BuildCaster(em, evt.Caster, evt.CasterFaction, evt.CasterBodyRadius);
                var target = BuildTarget(em, evt);
                // ⚠ **사건 자리가 비면 시전자 자리로 접는다**(ECS 리뷰 M-7).
                // 주기·경계·부착 seam 의 생산자는 `TargetPosition` 을 안 채운다 — 오늘
                // `EventPosition` 소비자 셋이 전부 채우는 seam 에만 실려 무해하지만,
                // 그 트리거로 「사건 자리」를 쓰는 concrete 가 처음 생기면 **월드 원점에서
                // 터진다.** 이 spec 이 이미 한 번 잡은 증상이라 0 을 흘려보내지 않는다.
                var eventPos = math.all(evt.TargetPosition == float3.zero)
                    ? evt.FiredPosition : evt.TargetPosition;
                // unit 23b — **자리와 몸은 같이 고른다.** 위에서 `TargetPosition` 을 골랐으면
                // 몸도 그쪽 것이어야 한다 — 짝이 갈리면 「죽은 적 자리에 킬러의 몸」이 된다.
                var eventBodyR = math.all(evt.TargetPosition == float3.zero)
                    ? evt.CasterBodyRadius : evt.EventBodyRadius;
                var p = new SkillParams(
                    evt.Magnitude, evt.Duration, evt.TileRange, evt.Period, evt.DataIndex,
                    evt.Selector, evt.Speed, evt.HitThreshold,
                    evt.SlamDamage, evt.SlamTileRange, evt.StackId, evt.VisualScale,
                    evt.PatternIndex, evt.StatSelector, evt.StackSelector,
                    evt.ProjectileMovement, evt.ProjectilePayload, evt.TargetTraversalLayers,
                    eventPos, evt.HazardDataIndex,
                    evt.Count, evt.IncludesSelf, evt.Selector2, evt.ConeCosSq, eventBodyR);

                // ⚠ **이벤트 하나의 실패가 드레인 전체를 죽이면 안 된다**(투트랙 리뷰 M-4).
                // 어댑터의 `NotWired` 는 **의도된 loud 경로**라 이전 중에 실제로 던진다.
                // 안 잡으면 같은 드레인에서 앞서 스테이징된 **타 발동의 원자 부착이 소실**되고
                // (궁극기라면 임계는 소모됐는데 발동은 없다), 잔여 이벤트가 다음 프레임의
                // 다른 seam 창으로 이월돼 arm 타이밍이 밀린다.
                // 레지스트리 `TryGet` 이 「한 슬롯의 배선 실수가 프레임을 못 죽인다」로 판단한 것과
                // 같은 자리다 — 다만 **삼키지 않는다.** 무엇이 왜 죽었는지 남긴다.
                try
                {
                    skill.Execute(caster, in target, in p, _context);
                    ExecutedCount++;
                    _perSeam[(int)Seam]++;
                    _perSkill.TryGetValue(evt.SkillId, out var prev);
                    _perSkill[evt.SkillId] = prev + 1;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError(
                        $"[SkillDispatch] skillId {evt.SkillId} 실행이 던졌다 — 이 발동만 버린다. {e}");
                }
            }
            }
            finally
            {
                // ⚠ **재생과 해제는 어떤 경로로 나가든 일어난다.** 예외로 건너뛰면
                // 스테이징된 구조 변경이 통째로 사라진다.
                ecb.Playback(EntityManager);
                ecb.Dispose();

                // **수명이 이 드레인인 것만** 끊는다 — 이 프레임에 Dispose 되는 핸들들이다.
                // ECB 만 끊고 풀·격자를 남기면 디스패처 밖에서 어댑터를 재사용하는 날
                // **Dispose 된 핸들**을 쓴다.
                //
                // ⚠ **「전부」가 아니다**(ECS 리뷰 M-5 — 예전 주석이 그렇게 주장했다).
                // `EntityManager` 와 `ComponentLookup` 7종은 남는다. 그것들은 다음 seam 이
                // 쓰기 전에 `Bind` 로 다시 채워지므로 여기서 끊을 이유가 없고, 끊으면
                // 오히려 seam 사이에 어댑터가 반쯤 죽은 상태가 된다. 다만 **그 약속을
                // 믿고 디스패처 밖에서 어댑터를 쓰면 `Update()` 안 된 stale lookup 을 쓴다** —
                // 이 어댑터는 seam 의 드레인 안에서만 유효하다.
                // 큐 싱크(Cc/Stat/Hit/…)도 남지만 브리지가 소유하는 Persistent 라 무해하다.
                _context.BindEcb(default, false);
                _context.BindPools(default, default, default, default);
                _context.BindFlowField(default, false);

                enemyPool.Dispose(); enemyPoolXf.Dispose();
                defPool.Dispose(); defPoolXf.Dispose();
            }
            }
            finally { _draining = false; }
        }

        // seam 시스템들이 어댑터 한 인스턴스를 나눠 쓰므로 이 플래그도 static 이다.
        // 한 seam 의 드레인 중에 다른 seam 이 열리는 것도 같은 사고다.
        private static bool _draining;

        // 격자 파라미터 — 파생이 채운다(호스트마다 같은 값이지만 base 가 싱글턴을 두 번
        // 읽지 않게 한다). 지금은 FlowField 에서 온다.
        protected abstract float TileSize { get; }
        protected abstract Unity.Mathematics.int2 GridSize { get; }
        protected abstract Unity.Mathematics.float3 Origin { get; }

        private static CasterRef BuildCaster(EntityManager em, Entity caster, Faction snapshot,
                                            float bodySnapshot = 0f)
        {
            if (caster == Entity.Null || !em.Exists(caster))
            {
                // ⚠ **시전자가 없을 때 진영을 «추측»하지 않는다**(migration unit 8 선행).
                // 예전엔 무조건 방어유닛 편으로 접었다 — 액티브 카드(시전 주체 없음)에는
                // 맞지만 **자기 죽음 seam 에는 정반대**다. 그 seam 은 정의상 파괴 뒤에
                // 돌아서 적의 작별 선물도 여기로 오고, 그러면 적이 «적» 을 겨눈다.
                // 생산자가 실어 보낸 값이 있으면 그것이 이긴다.
                // ⚠ 몸도 같이 접힌다 — 파괴된 시전자의 몸은 **생산자가 실어 온 값**뿐이다.
                // 여기서 0 으로 접으면 자기중심 광역이 조용히 좁아진다(그게 unit 23 의 결함 모양).
                return new CasterRef(SkillEntityId.None,
                    snapshot != Faction.None ? snapshot : Faction.DefenderUnit, bodySnapshot);
            }

            // 결정은 `FactionRelation.Resolve` 가 소유한다 — 여기서 4단 체인을 복제하면
            // 세 번째 사본이 된다(리뷰 L1). 그 파일이 「복제하면 조용히 갈린다」고 적어뒀다.
            bool hasTag = em.HasComponent<FactionTag>(caster);
            var faction = FactionRelation.Resolve(
                hasTag,
                hasTag ? em.GetComponentData<FactionTag>(caster).value : Faction.None,
                em.HasComponent<AttackUnitTag>(caster),
                em.HasComponent<DefenderUnitTag>(caster));

            // ⚠ **미발급 ID 는 스킬을 통째로 무력화한다.** 어댑터의 핸들 역변환이
            // `SimEntityId` 로 풀을 스캔하기 때문에, 이게 없는 캐스터는 자기 자신도
            // 못 찾는다 — 모든 질의가 빈손이고 모든 스킬이 **조용한 no-op** 이 된다.
            // 이 침묵을 실제로 한 번 겪었다(발사 명세 그물이 「감지도 되고 concrete 도
            // 불렸는데 캐리어 0」). 그러니 소리를 낸다.
            bool hasId = em.HasComponent<SimEntityId>(caster);
            int id = hasId ? em.GetComponentData<SimEntityId>(caster).value
                           : SimEntityId.Unassigned;
            if (!hasId || id == SimEntityId.Unassigned)
            {
                UnityEngine.Debug.LogWarning(
                    "[SkillDispatch] 캐스터에 SimEntityId 가 없다 — 이 시전자는 스킬 레이어에서 " +
                    "자기 자신도 못 찾는다(모든 질의가 빈손). 스폰 지점의 ID 발급을 확인하라.");
            }

            // distance-based-range unit 23a — **시전자의 몸을 «값으로» 싣는다**(제약 13).
            // 자기중심 광역(`RangeMetric.SelfArea`)의 원점 항이 이 값이다.
            // ⚠ 여기서 한 번 읽어 `CasterRef` 에 담는 것이 요점이다 — 어댑터가 **후보 루프 안**에서
            // 조회하면 후보마다 lookup 이고, 그 형태로 되돌리지 말 것(리뷰 LOW 11).
            // ⚠ 시전자가 **이미 파괴된** seam(자기 죽음·퇴근)은 위 분기로 빠져 몸이 0 이다 —
            // 그 경로의 폭발은 unit 23b 가 `SkillFiredEvent` 의 값 스냅샷으로 따로 덮는다.
            // 살아 있으면 실물이 정본, 없으면 생산자가 실어 온 스냅샷(자기 죽음 seam).
            float bodyR = em.HasComponent<Wassup.Battle.Units.HitRadius>(caster)
                ? em.GetComponentData<Wassup.Battle.Units.HitRadius>(caster).value : bodySnapshot;
            return CasterRef.OfUnit(new SkillEntityId(id), faction, bodyR);
        }

        private static SkillTarget BuildTarget(EntityManager em, in SkillFiredEvent evt)
        {
            // ⚠ **엔티티가 없어도 대상이 있을 수 있다**(unit 7a). 액티브는 칸을 찍어
            // 쓴다 — 여기서 `None` 으로 접으면 그 스킬이 조준을 잃는다.
            if (evt.Target == Entity.Null || !em.Exists(evt.Target))
                return new SkillTarget(SkillEntityId.None,
                    evt.TargetCellA, evt.TargetCellB, evt.HasCellB, evt.DirectionXZ);
            int id = em.HasComponent<SimEntityId>(evt.Target)
                ? em.GetComponentData<SimEntityId>(evt.Target).value
                : SimEntityId.Unassigned;
            return new SkillTarget(new SkillEntityId(id),
                evt.TargetCellA, evt.TargetCellB, evt.HasCellB, evt.DirectionXZ);
        }
    }
}
