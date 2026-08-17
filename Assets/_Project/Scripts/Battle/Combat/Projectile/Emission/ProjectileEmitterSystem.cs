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

                    // on-place-skill-rework unit 1 — 반경 스코프. **인스턴스당 1회**만 만든다:
                    // 풀은 프레임-로컬이고 hostCell 은 버스트 내내 고정이라 발마다 다시 걸러도
                    // 결과가 같은데, 발-루프 안에 두면 Temp 가 host×instance×shot 만큼 쌓인다.
                    // scope 0(기존 패턴)은 **할당도 분기도 없이** 원본 풀을 그대로 쓴다.
                    var scopedIdx = default(NativeArray<int>);
                    var scopedCells = default(NativeArray<int2>);
                    int scopedCount = 0;
                    bool scoped = false;
                    if (shots > 0 && binding != BindingClass.Direction && inst.spec.scopeTileRange > 0)
                    {
                        var srcCells = hostIsEnemy ? defCells : enemyCells;
                        int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                        var raw = new NativeArray<int>(srcCells.Length, Allocator.Temp);
                        scopedCount = PatternScope.Filter(srcCells, hostCell, inst.spec.scopeTileRange, raw);
                        scopedIdx = new NativeArray<int>(scopedCount, Allocator.Temp);
                        scopedCells = new NativeArray<int2>(scopedCount, Allocator.Temp);
                        for (int k = 0; k < scopedCount; k++)
                        {
                            scopedIdx[k] = raw[k];
                            scopedCells[k] = srcCells[raw[k]];
                        }
                        raw.Dispose();
                        scoped = true;
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

                            // on-place-skill-rework unit 1 — fan-out: 이 shot 이 스코프 안 후보
                            // **전원** 에게 1발씩 나간다(1:1 융단폭격). 갈래마다 다른 것은 타겟뿐이라
                            // BuildOrder 는 shot 당 1회만 부른다 — 카운터도 1회만 전진한다.
                            // 잠금(`reselectPerShot`)은 잠글 단일 대상이 없어 이 경로를 타지 않는다.
                            // on-place-skill-rework unit 11 — fan-out 은 **엔티티 조준 전용**이다.
                            // 셀 조준 궤적으로 「적 전원에게 1발씩」을 표현하려던 두 시도(unit 1 의
                            // 칸당 1발 접기, unit 8 의 착탄 임자 게이트)가 전부 여기서 걷혔다:
                            // 셀 궤적은 발사 시점의 칸에 고정되고 다시 조준하지 않으므로, 적 단위
                            // 발사와 짝지으면 한 탄에 조준이 둘이 되어 예고 시간만큼 어긋난다.
                            // 그 조합은 이제 bake 가 loud 로 거절한다(`BattleBridge.TryBuildPatternSlot`).
                            // ⚠ 여기 조건이 거짓이면 아래 **단일 선택 경로로 흘러** shot 당 1발만
                            // 나간다(조용한 축소). 그래서 거절은 authoring 시점에 있어야 한다.
                            if (inst.spec.fanOutToAllCandidates && binding == BindingClass.Entity)
                            {
                                int fanCount = scoped ? scopedCount : poolCells.Length;
                                // 후보 0 이면 기존 규약대로 발사를 소모하고 넘어간다(위상 보존).
                                order = PatternLogic.BuildOrder(inst.spec, ref inst.runtime,
                                                                fanCount > 0 ? (scoped ? scopedIdx[0] : 0) : -1);
                                if (order.targetCandidateIndex < 0) continue;

                                // ── 후보마다 1발. 조준은 **적 하나**다(Entity 바인딩) ──
                                //
                                // 이 자리에 두 번 실패한 설계가 있었다. 둘 다 「반경 안 적 전원에게
                                // 1발씩」을 **셀 조준** 궤적으로 표현하려 한 것이다:
                                //  · unit 1 — 칸당 1발로 접었다. 셀 낙하탄은 `impactTileRange 0`
                                //    이어도 그 칸 전원을 때려서(`TileAoe`) 접지 않으면 같은 칸 2기가
                                //    각자 2배를 맞았기 때문이다(실측 각 160). 대가로 뭉친 적에게
                                //    1발만 떨어져 발수가 적 수와 어긋났다.
                                //  · unit 8 — 각 발에 임자(`target`)를 실어 착탄 팔이 그 적만 고르게
                                //    했다. 그러자 **한 탄에 조준이 둘**이 됐다: 궤적은 칸(발사 시점)에
                                //    고정되고 페이로드는 적(착탄 시점)을 본다. 실측 예고 0.40s ×
                                //    적 속도 2.00 = 0.80타일 이동인데 칸 소속 유지 폭은 ±0.50타일 →
                                //    **피해 0**. 뒤 슬롯(0.72s = 1.44타일)은 전원 헛방이었다.
                                //
                                // 그래서 조준을 하나로 되돌렸다: 이 fan-out 은 **Entity 바인딩 전용**
                                // 이고 캐논은 `SkyFallOnEntity`(하늘낙하 × 적 조준)를 쓴다. 위치
                                // 갱신은 궤적이 소유하므로 여기서 착탄점을 계산하지 않는다 — 같은 칸
                                // 여분 발을 0.28타일 비켜 떨어뜨리던 `SubCellOffset` 도 함께 사라졌다
                                // (착탄점이 각 적의 위치가 되고, 적끼리는 분리 반경으로 늘 떨어져
                                //  있어서 겹칠 수가 없다).
                                var fanPool = new NativeList<int>(fanCount, Allocator.Temp);
                                var fanRank = new NativeList<long>(fanCount, Allocator.Temp);
                                for (int c = 0; c < fanCount; c++)
                                {
                                    int pi = scoped ? scopedIdx[c] : c;
                                    int2 cellOf = poolCells[pi];
                                    fanPool.Add(pi);
                                    fanRank.Add((long)cellOf.y * ff.gridSize.x + cellOf.x);
                                }

                                // **쓸어가는 순서는 row-major 셀 rank 로 고정한다.** 후보 배열 순서는
                                // ECS 청크 순서라 프레임마다 다를 수 있고, 시차가 있으면 그 순서가
                                // 화면에 그대로 보인다 — 연출이 프레임마다 달라지지 않게 결정론으로
                                // 못박는다. 같은 칸끼리는 pool index 로 안정 tie-break 한다(rank 만으로는
                                // 동순위라 청크 순서가 다시 새어 든다).
                                for (int a = 1; a < fanPool.Length; a++)
                                {
                                    int keyPi = fanPool[a];
                                    long keyRank = fanRank[a];
                                    int b = a - 1;
                                    while (b >= 0 && (fanRank[b] > keyRank ||
                                                      (fanRank[b] == keyRank && fanPool[b] > keyPi)))
                                    {
                                        // 두 배열은 **lockstep** 으로 옮긴다.
                                        fanPool[b + 1] = fanPool[b];
                                        fanRank[b + 1] = fanRank[b];
                                        b--;
                                    }
                                    fanPool[b + 1] = keyPi;
                                    fanRank[b + 1] = keyRank;
                                }

                                float stagger = math.max(0f, inst.spec.fanOutStaggerSec);
                                long prevRank = long.MinValue;
                                int cellSlot = -1;   // 칸 순번 = 시차 slot
                                for (int k = 0; k < fanPool.Length; k++)
                                {
                                    if (fanRank[k] != prevRank) { prevRank = fanRank[k]; cellSlot++; }

                                    int pi = fanPool[k];
                                    var fanReq = inst.template;
                                    fanReq.origin = hostPos;
                                    // 이 발의 **유일한** 조준. 착탄점도 낙하 위치도 궤적이 여기서
                                    // 파생하므로, 발사 쪽이 좌표를 따로 정하지 않는다.
                                    fanReq.target = poolEntities[pi];
                                    fanReq.swingIndex = order.shotIndex;
                                    fanReq.damage = order.damage;
                                    // 시차는 **예고 시간**에 준다 — 발사는 한 프레임에 다 나가고 착탄만
                                    // 순서대로 밀린다(연타로 읽힌다). `DrainMeteorBarrage` 의
                                    // `landed * staggerSec` 관용구와 동형. 낙하 예고를 안 쓰는 궤적
                                    // (호밍·베지어)은 이 필드를 읽지 않아 무해하다.
                                    //
                                    // ⚠ slot 은 **칸** 이 센다. 적 순번으로 밀면 뭉친 웨이브에서 폭격이
                                    // 적 수만큼 길어져 앞뒤 착탄 간격이 저작값과 무관해진다 —
                                    // 쓸어가는 길이는 «칸 수» 가 정한다. 같은 칸의 적들은 같은 순간에
                                    // 맞지만 착탄점이 각자의 위치라 화면에서는 여전히 갈린다.
                                    fanReq.flightTime = order.telegraphSec + cellSlot * stagger;
                                    fanReq.dataIndex = order.barrelDataIndex;
                                    var fanCarrier = ecb.CreateEntity();
                                    ecb.AddComponent(fanCarrier, fanReq);
                                    ecb.AddComponent<ProjectileRequestCarrier>(fanCarrier);
                                }
                                fanRank.Dispose();
                                fanPool.Dispose();
                                continue; // 이 shot 의 전개 완료
                            }

                            // 스코프가 있으면 선택은 **스코프 배열 안에서** 하고, 결과 index 는
                            // 반드시 원본 풀 index 로 되돌린다 — 아래 잠금 경로가
                            // `IndexOf(poolEntities, …)` 로 원본 index 를 만들어 쓰기 때문에
                            // 두 index 공간이 섞이면 엉뚱한 칸을 때리거나 범위를 벗어난다.
                            // (gridSize 는 원본 그대로 넘긴다 — rank 가 row-major 키를 만든다.)
                            int sel = PatternTargeting.Select(scoped ? scopedCells : poolCells,
                                                              inst.spec.selection,
                                                              inst.runtime.fireCount, ff.gridSize);
                            int idx = sel < 0 ? -1 : (scoped ? scopedIdx[sel] : sel);
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

                    if (scoped) { scopedIdx.Dispose(); scopedCells.Dispose(); }

                    if (EmitterTick.IsComplete(inst.runtime)) instances.RemoveAtSwapBack(i);
                    else instances[i] = inst;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (defBuilt) { defEntities.Dispose(); defCells.Dispose(); }
            if (enemyBuilt) { enemyEntities.Dispose(); enemyCells.Dispose(); }
        }

        // on-place-skill-rework unit 11 — `SubCellOffset`(같은 칸 여분 발을 0.28타일 비켜
        // 떨어뜨리던 황금각 오프셋)은 삭제했다. 착탄점이 «칸 중심» 이 아니라 «그 적의 위치» 가
        // 되면서 겹칠 수가 없어졌기 때문이다(적끼리는 분리 반경으로 늘 떨어져 있다).
        // 오프셋으로 시인성을 벌던 시절엔 미사일 `visualScale` 이 6 이라 0.28타일로는
        // 화면에서 한 발로 접혔다 — 제보 「범위 안 5~7기인데 낙하가 2개」의 정체.

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
