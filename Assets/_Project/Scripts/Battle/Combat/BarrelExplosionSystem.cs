using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // bomb-barrel-on-place unit 0 — 「판 위 설치물이 부서지면 터진다」.
    //
    // 이 시스템이 여는 것은 폭탄 배럴 하나가 아니라 **축**이다: 길막 설치물이 **적에게
    // 부서지는** 순간 그 칸을 중심으로 광역 피해를 낸다. 어느 설치물이 터지는지는
    // 전적으로 저작값(`BlockingHazardSO.explodeDamage`)이 정한다 — 0 이면 안 터지므로 기존
    // 길막 설치물은 무회귀다.
    //
    // **폭발 로직은 여기 없다.** 「칸 중심 광역 피해」는 폭탄맨 평타가 이미 쓰는 길이라
    // (`PayloadKind.TileAoe`), 이 시스템은 **즉발 투사체 요청 하나를 놓을 뿐**이고 해결은
    // `ProjectileHitSystem` 이 그대로 한다. 별도 데미지 경로를 만들지 말 것 — 두 벌이 되는
    // 순간 cap·진영 마스크·거점 취급이 갈린다.
    //
    // 맥락(CLAUDE.md): `BlockingHazard`(Effects)·`DeadTag`(Units)를 **읽기만** 하고, 만드는 것은
    // Combat 자기 것(`ProjectileSpawnRequest`)뿐이다. 타 맥락 컴포넌트 읽기는 허용.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    // ⚠ 순서를 안 잡으면 정렬기 tie-break 에 따라 그 프레임엔 `DeadTag` 가 아직 없고 다음
    // 프레임엔 엔티티가 이미 없다 = **폭발이 통째로 소실**된다. 「가끔 안 터진다」는 Play 로
    // 못 잡는 종류의 결함이라 EditMode 가 순서를 핀한다.
    [UpdateAfter(typeof(DamageApplicationSystem))]      // 체력 0 → DeadTag (유일한 폭발 계기)
    // unit 7 로 수명 만료 경로는 은퇴했지만 이 핀은 남긴다 — M0 unit 0 이 얼린
    // BattleSimGroup 총순서를 유지하는 장치이고, 떼면 골든 트레이스가 이유 없이 갈린다.
    [UpdateAfter(typeof(ObstacleLifetimeSystem))]
    [UpdateBefore(typeof(UnitLifecycleSystem))]         // 죽은 설치물을 치우는 쪽
    public partial struct BarrelExplosionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlockingHazard>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            foreach (var (hazard, obstacle, transform, entity) in
                     SystemAPI.Query<RefRO<BlockingHazard>, RefRO<Obstacle>, RefRO<LocalTransform>>()
                              .WithAll<DeadTag>()
                              .WithEntityAccess())
            {
                if (hazard.ValueRO.explodeDamage <= 0f) continue;

                // 폭발 중심은 **설치물이 차지한 칸의 중심**이다. transform 을 그대로 쓰지 않는
                // 이유: 여러 칸 설치물은 transform 이 중심 칸이지만 스폰이 정한 `Obstacle.cell`
                // 이 정본이고, 그 둘이 갈리면 폭발이 반 칸 밀린다.
                float3 impact = GridMath.CellToWorldCenter(
                    obstacle.ValueRO.cell, tileSize, transform.ValueRO.Position.y, origin: ffOrigin);

                // ⚠ 요청을 **죽는 설치물에 걸면 안 된다.** 같은 프레임에 UnitLifecycleSystem 이
                // 그 엔티티를 파괴하고, 브리지 드레인은 ECS 가 전부 끝난 뒤 도는 MonoBehaviour
                // Update 라 요청을 영영 못 본다. 전용 캐리어가 그래서 존재한다 —
                // 드레인이 캐리어를 통째로 파괴하므로 잔여 엔티티도 안 남는다.
                // 요청 모양은 방어유닛 사망 폭발(`DrainDefenderDeathEvents` 의 OnDeath TileAoe)과
                // **동형**이다 — 그쪽이 이미 「죽는 순간 그 칸에 즉발 광역」을 라이브로 굴리고
                // 있으므로 검증된 조합을 그대로 쓴다. 안 채운 필드(origin·owner·
                // targetTraversalLayers)는 기본 0 이 곧 「무필터」다.
                var carrier = ecb.CreateEntity();
                ecb.AddComponent<ProjectileRequestCarrier>(carrier);
                ecb.AddComponent(carrier, new ProjectileSpawnRequest
                {
                    movement = MovementKind.SkyFall,
                    payload = PayloadKind.TileAoe,
                    impact = impact,
                    // flightTime 0 = 예고 없는 즉발. 설치물이 부서지는 그 순간이 폭발이다 —
                    // 낙하 예고를 주면 「부서짐」과 「폭발」이 화면에서 두 사건으로 갈린다.
                    flightTime = 0f,
                    impactTileRange = hazard.ValueRO.explodeTileRange,
                    aoeTargetCap = hazard.ValueRO.explodeTargetCap,
                    damage = hazard.ValueRO.explodeDamage,
                    dataIndex = hazard.ValueRO.explodeDataIndex,
                    visualScale = 1f,
                    // ⚠ **명시한다.** 기본 0 이 곧 Enemy 라 생략해도 지금은 맞지만, 형제 경로엔
                    // `FactionTag == EnemyUnit ? Defender : Enemy` 로 **파생**하는 곳이 있고
                    // 설치물은 그 삼항식의 else 로 떨어져 우연히 정답이 된다. 적이 던지는
                    // 설치물이 생기는 순간 그 파생은 진영을 못 가른다 — 여기선 파생에 기대지 않는다.
                    targetFaction = ProjectileTargetFaction.Enemy,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
