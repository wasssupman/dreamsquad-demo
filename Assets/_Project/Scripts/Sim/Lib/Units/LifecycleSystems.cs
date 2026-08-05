using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 캡처 #11(P3). 구 `HealthDeathSystem` 이식.
    ///
    /// **안전망이다.** 피해 정산(#34) 밖에서 HP 를 깎는 경로(힐 음수·직접 쓰기·저작 실수)가
    /// 있어도 죽음이 공유 경로로 들어오게 한다. #34 가 이미 마킹한 대상은 `DeadTag` 로 걸러진다.
    ///
    /// ⚠ **P3 라서 #34(P9)보다 앞이다** — 즉 이 시스템이 보는 HP 는 **지난 틱 정산 결과**다.
    /// 같은 틱 피해로 죽은 유닛은 #34 가 직접 마킹하므로 둘 사이에 빈틈이 없다.
    /// </summary>
    public sealed class HealthDeathSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public void Run(SimWorld world)
        {
            foreach (var entity in world.With<Health>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                if (world.Get<Health>(entity).value <= 0f)
                    _ecb.Set(entity, new DeadTag());
            }
            _ecb.Playback(world);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 캡처 #12(P3). 구 `LethalTimerSystem` 이식.
    ///
    /// 카미카제 자폭: 타이머가 만료되면 `DeadTag` 를 붙이고 **타이머를 뗀다**. 기존 사망 경로를
    /// 재사용하므로 이 시스템은 파괴도, 폭발도 하지 않는다.
    ///
    /// ⚠ **이미 죽은 유닛은 건너뛴다** — 같은 틱에 피해로 죽은 유닛이 이중 태깅되지 않게.
    /// (구 sim 은 `[UpdateBefore(DamageApplication)]` 였고 신 sim 은 P3 &lt; P9 로 같은 순서다.)
    ///
    /// ⚠ 만료 판정이 `&lt;= 0` 이라 **정확히 0 에 닿은 틱에 터진다**. 남은 시간을 먼저 깎고
    /// 판정하므로 `remaining` 이 0 인 채로 살아 있는 틱은 없다.
    /// </summary>
    public sealed class LethalTimerSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;
            foreach (var entity in world.With<LethalTimer>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                var timer = world.Get<LethalTimer>(entity);
                float rem = timer.remaining - dt;
                if (rem <= 0f)
                {
                    _ecb.Set(entity, new DeadTag());
                    _ecb.RemoveComponent<LethalTimer>(entity);
                }
                else
                {
                    timer.remaining = rem;
                    world.Set(entity, timer);
                }
            }
            _ecb.Playback(world);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 캡처 #36(P10). 구 `PatrolLifecycleSystem` 이식.
    ///
    /// 소환사가 죽으면 순찰병도 죽는다. 파괴는 하지 않고 `DeadTag` 만 붙인다 — 순찰병은
    /// `DefenderTile` 이 없어서 #41 의 **일반 사망 루프**로 정확히 떨어진다.
    ///
    /// ⚠ **생존 판정이 3중인 것이 계약이다**: 파괴됨(`Exists`) · 같은 틱 `DeadTag` · `HP &lt;= 0`.
    /// 하나라도 빼면 소환사가 죽은 프레임에 순찰병이 한 틱 더 살아 공격한다. P10 인 이유도
    /// 같다 — `DeadTag` 생산자 **둘 다**(#34 P9, #11 P3) 뒤에 서야 stale HP 를 읽지 않는다.
    /// </summary>
    public sealed class PatrolLifecycleSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public void Run(SimWorld world)
        {
            foreach (var entity in world.With<SummonedBy>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                var owner = world.Get<SummonedBy>(entity).owner;
                bool ownerAlive = !owner.IsNull
                    && world.Exists(owner)
                    && !world.Has<DeadTag>(owner)
                    && world.TryGet<Health>(owner, out var ownerHealth)
                    && ownerHealth.value > 0f;

                if (!ownerAlive) _ecb.Set(entity, new DeadTag());
            }
            _ecb.Playback(world);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 캡처 #41(P12). 구 `UnitLifecycleSystem` 이식.
    ///
    /// **유일한 파괴자다**(수명 만료 #2·#6 제외 — 그 둘은 릴레이에 참여하지 않는다).
    /// 네 루프가 순서대로 돈다: 목표 도달 → 방어유닛 사망 → 해저드 파괴 → 일반 사망.
    ///
    /// ⚠ **파괴 전에 이벤트를 굽는 것이 이 시스템의 존재 이유**다. 드레인은 파괴 뒤에 돌기 때문에
    /// 타일 좌표도, OnDeath 폭발 파라미터도 지금 읽지 않으면 영영 못 읽는다.
    ///
    /// ⚠ **루프 분할이 곧 중복 파괴 방지 장치**다 — 일반 루프가 `DefenderTile`·`BlockingHazard`
    /// 보유자를 제외하는 것은 앞 두 루프가 이미 처리했기 때문이다.
    /// 따름정리로 **구멍이 둘 생긴다**(구 sim 의 실제 동작이고 재현 대상이다):
    /// <list type="bullet">
    /// <item>`DefenderTile` 은 있는데 `DefenderUnitTag` 가 없는 죽은 엔티티 → 어느 루프도 안 잡는다.</item>
    /// <item>`BlockingHazard` 는 있는데 `Obstacle`/위치가 없는 죽은 엔티티 → 마찬가지.</item>
    /// </list>
    /// 정상 스폰 경로는 항상 쌍으로 붙이므로 실전에서 나지 않는다. **고치지 말 것** — 고치면
    /// 골든이 갈리고, 그 조합이 실제로 생기는 날은 스폰 경로가 이미 깨진 날이다.
    /// </summary>
    public sealed class UnitLifecycleSystem
    {
        private readonly SimChannels _channels;
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public UnitLifecycleSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            // ① 목표 도달 — 마지막 웨이포인트를 지난 적. **사망이 아니다**(점수도 보상도 없다).
            foreach (var entity in world.With<PastGoalTag>())
            {
                if (!world.Has<AttackUnitTag>(entity)) continue;
                _channels.GoalReached.Enqueue(new GoalReachedEvent { entity = entity });
                _ecb.Destroy(entity);
            }

            // ② 방어유닛 사망 — 타일과 OnDeath 폭발을 **파괴 전에** 굽는다.
            foreach (var entity in world.With<DeadTag>())
            {
                if (!world.Has<DefenderUnitTag>(entity)) continue;
                if (!world.TryGet<DefenderTile>(entity, out var tile)) continue;

                var evt = new DefenderDeathEvent { cell = tile.cell };
                var slots = world.GetBuffer<DcTriggerSlot>(entity);
                if (slots != null)
                {
                    for (int s = 0; s < slots.Count; s++)
                    {
                        if (slots[s].trigger != DcTriggerKind.OnDeath ||
                            slots[s].payload != DcPayloadKind.SelfTileAoe) continue;
                        evt.hasOnDeathAoe = true;
                        evt.aoeDamage = slots[s].magnitude;
                        evt.aoeTileRange = slots[s].tileRange;
                        evt.aoeDataIndex = slots[s].projectileDataIndex;
                        break; // 첫 OnDeath 슬롯만 (v1)
                    }
                }
                _channels.DefenderDeath.Enqueue(evt);
                _ecb.Destroy(entity);
            }

            // ③ 차단 해저드 파괴 — 위치·셀·SO index 를 파괴 전에 굽는다.
            foreach (var entity in world.With<DeadTag>())
            {
                if (!world.TryGet<BlockingHazard>(entity, out var hazard)) continue;
                if (!world.TryGet<Obstacle>(entity, out var obstacle)) continue;
                if (!world.TryGet<SimTransform>(entity, out var transform)) continue;

                _channels.HazardDestroyed.Enqueue(new HazardDestroyedEvent
                {
                    hazardEntity = entity,
                    hazardSoIndex = hazard.hazardSoIndex,
                    worldPosition = transform.Position,
                    centerCell = obstacle.cell,
                });
                _ecb.Destroy(entity);
            }

            // ④ 일반 사망 — 위 둘이 잡지 않은 나머지(적·타일 없는 방어유닛 등).
            foreach (var entity in world.With<DeadTag>())
            {
                if (world.Has<DefenderTile>(entity)) continue;
                if (world.Has<BlockingHazard>(entity)) continue;
                _ecb.Destroy(entity);
            }

            _ecb.Playback(world);
        }
    }
}
