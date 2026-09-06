using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // ultimate-leap unit 1 — 이탈 카운트다운과 착지. 시퀀스를 sim 이 소유하는 이유:
    // 2초는 회피 창이자 피해 게이트 = **게임 규칙**이다(일반 도약의 비행 창이 브리지 소유인
    // 것과 비대칭이 맞다 — 그쪽은 연출 정합이라 뷰 시계를 따른다).
    //
    // 시계는 `SystemAPI.Time.DeltaTime` = Battle 도메인. 슬로모 중엔 예고도 함께 느려져야
    // 시뮬과 어긋나지 않는다.
    //
    // 착지 순서 3단: 텔레포트 요청 → 슬램 요청 → 상태 해제. 텔레포트는 위치가 Movement
    // 소유라 기존 `BlinkRequestEventsSingleton` seam 으로 나간다(신규 이동 채널 0).
    // `[UpdateBefore(BlinkApplySystem)]` 이라 요청이 **같은 틱에** 적용된다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(HealthThresholdSystem))]
    [UpdateBefore(typeof(BlinkApplySystem))]
    public partial struct UltimateLeapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UltimateLeapState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            bool hasBlinkQ = SystemAPI.TryGetSingletonRW<BlinkRequestEventsSingleton>(out var blinkRW);
            // 연출 채널 부재면 강하 연출만 없고 sim 은 그대로 착지한다(기존 부재-가드 규약).
            bool hasVisQ = SystemAPI.TryGetSingletonRW<UltimateLeapVisualEventsSingleton>(out var visRW);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // DeadTag 제외는 방어적 가드다 — 계약 3(피해 완전 차단)상 공중 사망이 없으므로
            // 정상 경로에서는 도달하지 않는다. 오버킬 프레임 경합으로 죽었다면 착지 없이
            // 상태만 걷어 시체가 잠긴 채 남지 않게 한다.
            foreach (var (leapRef, entity) in
                     SystemAPI.Query<RefRW<UltimateLeapState>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                leapRef.ValueRW.remaining -= dt;
                if (leapRef.ValueRO.remaining > 0f) continue;

                var leap = leapRef.ValueRO;

                // 1. 텔레포트 — 위치 쓰기는 Movement 소유라 요청만 낸다.
                if (hasBlinkQ)
                {
                    blinkRW.ValueRW.queue.Enqueue(new BlinkRequestEvent
                    {
                        entity = entity,
                        destWorld = leap.landingWorld,
                    });
                }

                // 2. 슬램 — 캐리어 스테이징(브리지 드레인이 스폰 후 캐리어를 파괴한다).
                //    shooter 스냅샷 없이 고정 피해다(보스 도약 슬램·메테오 barrage 와 같은 규약).
                //    dataIndex 는 bake 가 보장한다 — 미지정이면 슬롯 자체가 거절됐다(unit 0).
                if (leap.slamDamage > 0f)
                {
                    var carrier = ecb.CreateEntity();
                    ecb.AddComponent(carrier, new Projectile.ProjectileSpawnRequest
                    {
                        movement        = Projectile.MovementKind.SkyFall,
                        payload         = Projectile.PayloadKind.TileAoe,
                        origin          = leap.landingWorld,
                        impact          = leap.landingWorld,
                        damage          = leap.slamDamage,
                        impactTileRange = leap.slamTileRange,
                        // unit 23b — 강습 슬램도 **그 몸이 내리찍는 것**이다(자리형 아님).
                        originBodyRadius = SystemAPI.HasComponent<Wassup.Battle.Units.HitRadius>(entity)
                            ? SystemAPI.GetComponent<Wassup.Battle.Units.HitRadius>(entity).value : 0f,
                        flightTime      = 0f,   // 즉발 — 예고가 이미 2초를 벌었다
                        arcHeight       = 0f,
                        dataIndex       = leap.projectileDataIndex,
                        visualScale     = 1f,
                        owner           = entity,
                        targetFaction   = Projectile.ProjectileTargetFaction.Defender,
                    });
                    ecb.AddComponent<Projectile.ProjectileRequestCarrier>(carrier);
                }

                // 3. 강하 연출 신호 — 뷰는 지금부터 떨어지기 시작한다. sim 은 이미 착지했으므로
                //    슬램 VFX 타이밍(뷰 도착)은 브리지가 소유한다.
                if (hasVisQ)
                {
                    visRW.ValueRW.queue.Enqueue(new UltimateLeapVisualEvent
                    {
                        entity = entity,
                        kind = UltimateLeapVisualKind.Descend,
                        world = leap.landingWorld,
                        dataIndex = leap.projectileDataIndex,
                    });
                }

                // 4. 상태 해제 — 무적과 잠금이 함께 떨어진다(붙을 때와 대칭).
                ecb.RemoveComponent<UltimateLeapState>(entity);
                ecb.RemoveComponent<LeapFlight>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
