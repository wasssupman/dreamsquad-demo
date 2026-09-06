using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // active-ally-zone unit 0 — 아군 버프 장판의 멤버십 갱신.
    //
    // 살아 있는 AllyBuffField × 배치 완료 방어유닛을 체비셰프로 맞춰 **매 프레임**
    // StatModifierApplyEvent 를 재발행한다. ZoneApplySystem(해저드 장판이 안에 있는 적에게 매
    // 프레임 재발행)과 같은 형태 — 이 레포의 존 관용구이지 새 발명이 아니다.
    //
    // 갱신이 곧 회수다: 유닛이 장판을 벗어나거나(재배치) 장판이 파괴되면 더 이상 재발행되지 않아
    // 모디파이어가 EffectSpawner.AllyBuffApplySec 안에 자연 소멸한다.
    //
    // ⚠ duration 은 **항상** AllyBuffApplySec 이다. ModifierApplySystem 의 refresh 가
    // remaining = max(old, new) 라서 한 번이라도 스킬 지속시간(8초)으로 걸면 이후 갱신이 그 값을
    // 내릴 수 없고, 장판을 벗어나도 8초간 버프가 남는다 = 장판화가 없애려던 스냅샷 동작으로 회귀한다.
    // 스킬 지속시간은 캐리어 수명(remaining)에만 쓴다.
    //
    // cadence 누산기를 두지 않는다: 매 프레임이 repo norm 이고, 주기를 두면 "정지/슬로모에서 어느
    // 시계를 쓰나" 라는 결정이 새로 생긴다. 그룹이 정지에서 통째로 skip 되므로 정지 처리도 공짜다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(ModifierApplySystem))]
    public partial struct AllyBuffFieldSystem : ISystem
    {
        // unit 18 — 멤버십이 유닛 몸을 본다. 로컬 형태 금지(Burst NRE 재발 이력) — 필드로.
        ComponentLookup<Wassup.Battle.Units.HitRadius> _bodyRadiusLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllyBuffField>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
            // 리뷰 M6 (unit 18) — 셀 대 셀 시절엔 격자 파라미터가 불필요했지만, 이제
            // centerCell 을 월드로 펴야 해서 필수다. 폴백(1,0)은 실제 좌표계와 달라
            // 멤버십이 조용히 전부 틀린다 — ZoneApplySystem 과 같은 Require 로 못박는다.
            state.RequireForUpdate<FlowFieldSingleton>();
            _bodyRadiusLookup = state.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fieldQuery = SystemAPI.QueryBuilder().WithAll<AllyBuffField>().Build();
            var fields = fieldQuery.ToComponentDataArray<AllyBuffField>(Allocator.Temp);
            if (fields.Length == 0) { fields.Dispose(); return; }

            var statQueue = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
            // unit 18 — 셀 대조 → 몸 기준 연속 멤버십. 격자 파라미터는 셀 중심의 월드 환산에만
            // 쓴다(Require 로 존재 보장 — 리뷰 M6).
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();
            float tileSize = ff.tileSize;
            float3 ffOrigin = ff.origin;
            float buffInvT = tileSize > 1e-6f ? 1f / tileSize : 1f;
            _bodyRadiusLookup.Update(ref state);
            var hitRadiusLookup = _bodyRadiusLookup;

            // 배치 완료 방어유닛만 — 배치 대기(PendingDeployment)는 아직 판에 서지 않았고
            // (on-place 오라와 같은 규칙), 죽은 유닛은 제외(DefenderFieldSystem 과 같은 필터).
            foreach (var (tile, xf, entity) in
                     SystemAPI.Query<RefRO<DefenderFootprint>, RefRO<Unity.Transforms.LocalTransform>>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                // unit 18 — 판정 기준은 앵커 셀이 아니라 **몸**(베이스 중심 + 파생 반경)이다.
                float3 defPos = xf.ValueRO.Position;
                float defBodyR = hitRadiusLookup.HasComponent(entity) ? hitRadiusLookup[entity].value : 0f;

                // 겹친 장판은 누적되지 않는다(merge 키가 stat 단위로 같다). 그러면 "어느 값이 이기나"
                // 를 chunk 순회 순서에 맡기게 되는데, 만료 시 swap-back 으로 순서가 런타임에 바뀌므로
                // 배율이 다른 장판 2장이 겹치면 승자가 무작위가 된다 → **가장 강한 값으로 못박는다.**
                // 부수 효과로 유닛당 enqueue 가 (장판 수)에서 (stat 수 ≤2)로 줄어든다.
                float bestDamage = 0f, bestSpeed = 0f;
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    // unit 18 — 오라 멤버십 = 원 + 유닛 몸(광역과 같은 자, 입력은 연속).
                    float3 fCenter = Wassup.Battle.Movement.GridMath.CellToWorldCenter(
                        f.centerCell, tileSize, defPos.y, origin: ffOrigin);
                    if (!Wassup.Skills.SkillMath.ReachFromCell(
                            (defPos.x - fCenter.x) * buffInvT, (defPos.z - fCenter.z) * buffInvT,
                            f.tileRange, defBodyR)) continue;
                    if (f.stat == StatKind.DamageMul) bestDamage = math.max(bestDamage, f.magnitude);
                    else if (f.stat == StatKind.AttackSpeedMul) bestSpeed = math.max(bestSpeed, f.magnitude);
                }

                if (bestDamage > 0f) Enqueue(ref statQueue, entity, StatKind.DamageMul, bestDamage);
                if (bestSpeed > 0f) Enqueue(ref statQueue, entity, StatKind.AttackSpeedMul, bestSpeed);
            }

            fields.Dispose();
        }

        // duration 은 여기 한 곳에서만 정해진다 — 호출부가 실수로 스킬 지속시간을 넣을 여지를 없앤다.
        private static void Enqueue(ref NativeQueue<StatModifierApplyEvent> queue,
            Entity target, StatKind stat, float multiplier)
        {
            ModifierAuthoring.FromMultiplier(multiplier, out var op, out var magnitude);
            queue.Enqueue(new StatModifierApplyEvent
            {
                target    = target,
                stat      = stat,
                op        = op,
                magnitude = magnitude,
                duration  = EffectSpawner.AllyBuffApplySec,
                source    = target,
                stackId   = AllyBuffField.StackId,
                origin    = ModifierOrigin.Skill,
            });
        }
    }
}
