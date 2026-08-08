using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 2 — 발사 명세의 아키텍처 계층. 활성 인스턴스를
    // tick 하고, 로직 계층이 산출한 ShotOrder 를 기존 ProjectileSpawnRequest 캐리어로
    // 번역한다. **투사체 라이프사이클은 신설하지 않는다**(계약 5): 캐리어 → 브리지
    // 드레인 → ProjectileState → Move/Hit → 파괴 경로를 그대로 탄다.
    //
    // 분기 축은 개별 MovementKind 가 아니라 타겟 바인딩 클래스다(계약 11) — 발사
    // 시점에 궤적이 요구하는 것은 "엔티티냐 셀이냐 방향이냐" 뿐이므로, 기존 바인딩으로
    // 분류되는 새 이동 수학은 이 시스템을 건드리지 않고 발사된다.
    //
    // 트리거가 push 한 프레임에 첫 발이 나가도록 arm 뒤에 돈다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(BossPeriodicTriggerSystem))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct ProjectileEmitterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EmitterInstance>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // BattleSimGroup dt — TimeManager Battle 도메인 스케일(슬로모 포함).
            float dt = SystemAPI.Time.DeltaTime;
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

            // 후보 풀은 진영별 lazy 빌드 — 인스턴스가 없거나 이 프레임에 발사가
            // 없으면 쿼리·할당 0 (BossPeriodicTriggerSystem whip 풀 선례).
            NativeArray<Entity> defEntities = default, enemyEntities = default;
            NativeArray<int2> defCells = default, enemyCells = default;
            bool defBuilt = false, enemyBuilt = false;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (instancesRef, entity) in
                     SystemAPI.Query<DynamicBuffer<EmitterInstance>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                // foreach 변수는 readonly — DynamicBuffer 는 뷰 struct 라 로컬 복사가
                // 같은 버퍼 메모리를 가리킨다(CS1654 회피 관용구).
                var instances = instancesRef;
                if (instances.Length == 0) continue;
                // host 위치가 발사 원점이다. 없으면(소멸 중) 이 프레임은 건너뛴다.
                if (!transformLookup.HasComponent(entity)) continue;
                float3 hostPos = transformLookup[entity].Position;

                // 진영은 host 에서 도출한다 — 패턴 SO 에 faction 필드를 두지 않는다
                // (계약 7, 채찍질 arm 의 hostIsEnemy/hostIsDefender 판정 선례).
                bool hostIsEnemy = SystemAPI.HasComponent<AttackUnitTag>(entity);
                bool hostIsDefender = !hostIsEnemy && SystemAPI.HasComponent<DefenderUnitTag>(entity);
                if (!hostIsEnemy && !hostIsDefender) continue; // 진영 불명 host = no-op

                for (int i = instances.Length - 1; i >= 0; i--)
                {
                    var inst = instances[i];
                    var binding = MovementBinding.Of(inst.template.movement);
                    int shots = EmitterTick.Advance(ref inst.runtime, dt, inst.spec);

                    // 후보 풀은 이 프레임에 실제로 발사가 있을 때 1회만 만든다
                    // (쿼리 생성을 발-루프 안에 두지 않는다).
                    if (shots > 0 && binding != BindingClass.Direction)
                    {
                        if (hostIsEnemy && !defBuilt)
                        {
                            // 융단폭격 동작 보존: 기존 arm 과 같은 쿼리(살아있는 방어유닛).
                            var defQuery = SystemAPI.QueryBuilder()
                                .WithAll<DefenderUnitTag, LocalTransform>().Build();
                            defEntities = defQuery.ToEntityArray(Allocator.Temp);
                            var defXf = defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                            defCells = new NativeArray<int2>(defXf.Length, Allocator.Temp);
                            for (int c = 0; c < defXf.Length; c++)
                                defCells[c] = GridMath.WorldToCell(defXf[c].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
                            defXf.Dispose();
                            defBuilt = true;
                        }
                        else if (hostIsDefender && !enemyBuilt)
                        {
                            // 재조준 풀 관례: 죽은·유출된 적은 후보에서 뺀다.
                            // ultimate-leap unit 2 — 판 밖(이탈) 적도 뺀다. 빠뜨리면 발사 명세
                            // 패턴 유닛이 화면 밖 보스를 골라 빈 타일에 사격한다(피해는 버퍼 드랍이
                            // 막지만 "사라졌다" 는 읽힘이 깨진다).
                            var enemyQuery = SystemAPI.QueryBuilder()
                                .WithAll<AttackUnitTag, LocalTransform>()
                                .WithNone<DeadTag>()
                                // goal-tower-siege unit 1 — PastGoal 배제 제거(골에 붙은 적도
                                // 살아 있는 사격 대상이다). 판 밖 이탈(UltimateLeapState)만 남긴다.
                                .WithNone<Wassup.Battle.Combat.UltimateLeapState>().Build();
                            enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
                            var enemyXf = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                            enemyCells = new NativeArray<int2>(enemyXf.Length, Allocator.Temp);
                            for (int c = 0; c < enemyXf.Length; c++)
                                enemyCells[c] = GridMath.WorldToCell(enemyXf[c].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
                            enemyXf.Dispose();
                            enemyBuilt = true;
                        }

                    }

                    // 발마다 — Advance 가 반환한 발수를 전부 소비한다. 이 루프가 없으면
                    // burstRemaining 은 N 만큼 차감됐는데 캐리어는 1개만 생겨 나머지가
                    // 증발하고(shot 목록 사문화), continue 가 아래 write-back·완주
                    // 판정까지 건너뛰어 인스턴스가 영구 적재된다(두 리뷰어 CRITICAL).
                    for (int s = 0; s < shots; s++)
                    {
                        var req = inst.template;
                        ShotOrder order;
                        if (binding == BindingClass.Direction)
                        {
                            // 무타겟 정상 경로. 원점·기준방향·최대거리는 trigger가
                            // template에 스냅샷했고 emitter는 개별 각도만 결정한다.
                            order = PatternLogic.BuildOrder(inst.spec, ref inst.runtime, -1);
                            req.direction = PatternDirection.Resolve(
                                inst.template.direction,
                                inst.spec.minAngleDeg,
                                inst.spec.maxAngleDeg,
                                order.directionT);
                        }
                        else
                        {
                            var poolEntities = hostIsEnemy ? defEntities : enemyEntities;
                            var poolCells = hostIsEnemy ? defCells : enemyCells;

                            int idx = PatternTargeting.Select(poolCells, inst.spec.selection,
                                                              inst.runtime.fireCount, ff.gridSize);
                            // 명령 완성은 로직 계층이 한다(카운터 전진 포함).
                            order = PatternLogic.BuildOrder(inst.spec, ref inst.runtime, idx);

                            // 잠금 해석 — reselectPerShot=false 면 첫 성공 선택의 Entity 를
                            // 재사용한다(index 재사용 금지: 후보 스냅샷은 프레임-로컬).
                            Entity target = Entity.Null;
                            int cellIdx = order.targetCandidateIndex;
                            if (!inst.spec.reselectPerShot && inst.lockedTarget != Entity.Null)
                            {
                                target = inst.lockedTarget;
                                cellIdx = IndexOf(poolEntities, target);
                                if (cellIdx < 0)
                                {
                                    // 잠근 대상이 버스트 도중 사라졌다 → 남은 발 조용히 소모.
                                    continue;
                                }
                            }
                            else if (cellIdx >= 0)
                            {
                                target = poolEntities[cellIdx];
                                if (!inst.spec.reselectPerShot) inst.lockedTarget = target;
                            }

                            if (cellIdx < 0) continue; // 후보 0 = 발사 소모(위상은 이미 전진)
                            req.origin = hostPos;

                            switch (binding)
                            {
                                case BindingClass.Entity:
                                    req.target = target;
                                    // 비-베지어 궤적은 이 필드를 읽지 않아 무해. 제어점 산출은
                                    // 드레인 몫이다(SO 접근 seam) — emitter 는 SO 를 모른다.
                                    req.swingIndex = order.shotIndex;
                                    break;

                                case BindingClass.Cell:
                                    req.impact = GridMath.CellToWorldCenter(
                                        poolCells[cellIdx], ff.tileSize, 0f, origin: ff.origin);
                                    req.flightTime = order.telegraphSec;
                                    break;

                                default:
                                    continue;
                            }
                        }

                        // 명령이 결정한 값을 그대로 쓴다. template 의 dataIndex 와 오늘은
                        // 같지만(bake 가 같은 barrelIndex 를 양쪽에 넣는다), 거기 기대면
                        // 로직 계층의 결정이 bake 불변식에 묶인다 — order 가 source 다.
                        req.damage = order.damage;
                        req.dataIndex = order.barrelDataIndex;

                        var carrier = ecb.CreateEntity();
                        ecb.AddComponent(carrier, req);
                        ecb.AddComponent<ProjectileRequestCarrier>(carrier);
                    }

                    if (EmitterTick.IsComplete(inst.runtime)) instances.RemoveAtSwapBack(i);
                    else instances[i] = inst;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (defBuilt) { defEntities.Dispose(); defCells.Dispose(); }
            if (enemyBuilt) { enemyEntities.Dispose(); enemyCells.Dispose(); }
        }

        // 잠근 대상의 이번 프레임 후보 index. 없으면 -1(대상 소멸/유출).
        private static int IndexOf(in NativeArray<Entity> pool, Entity target)
        {
            if (!pool.IsCreated) return -1;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] == target) return i;
            return -1;
        }
    }
}
