using Unity.Entities;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Bridge
{
    // selection-hand-attach unit 10 — 선택 유닛의 표시용 스탯을 뷰에 내주는 읽기 창구.
    //
    // 기존 경로는 내부 순회로 오버헤드 UI 에 push 하는 것뿐이라 "이 엔티티 하나"를 pull 할
    // 방법이 없었다. 상세 패널(unit 11)이 스탯 3종 + 델타를 그리려면 이 seam 이 필요하다.
    //
    // **읽기 전용이다.** 맥락 경계를 넘지 않는다(쓰기는 소유 맥락만 — CLAUDE.md 제약 2).
    // Health=Units / AttackState·AttackOutputElement=Combat / ModifierStats=Effects 를 읽기만 한다.
    // **ECS 시스템은 건드리지 않는다**(spec unit 10 D) — Scripts/Battle/ diff 0 이 완료 기준이다.
    public partial class BattleBridge
    {
        // 표시용 스탯 3종 + 델타 기준 기본값. 방어 유닛이 아니거나 심이 죽었으면 false —
        // 뷰는 그 프레임 스탯 표시를 생략한다(패널 자체는 유지).
        public bool TryGetUnitStatReadout(Entity entity, out UnitStatReadout readout)
        {
            readout = default;
            if (entity == Entity.Null || !HasLiveEntityManager()) return false;
            if (!_em.Exists(entity)) return false;

            // 기본값 출처는 SO 다. 런타임 버퍼가 SO 와 다를 수 있으므로(TauntAttackGrantSystem 이
            // 공격을 부여하는 등) 실효=런타임 / 기본=SO 로 두면 그 차이도 델타에 정직하게 뜬다.
            var data = FindDefenderData(entity);
            if (data == null) return false; // 적/미배치 — 표시 대상이 아니다

            // ── 체력 ──────────────────────────────────────────────────────────
            // Health.max 에는 maxHealthMul 이 **이미 반영돼 있다**(Units 의 MaxHealthScaleSystem 이
            // Health.max 의 유일한 런타임 writer). 여기서 다시 곱하면 이중 적용이다.
            if (!_em.HasComponent<Health>(entity)) return false;
            var health = _em.GetComponentData<Health>(entity);
            readout.hp = health.value;
            readout.hpMax = health.max;
            readout.hpMaxBase = data.health;

            // ── 배율 ──────────────────────────────────────────────────────────
            // 없으면 중립(1) — 모디파이어가 한 번도 안 붙은 유닛은 컴포넌트가 없을 수 있다.
            float damageMul = 1f, attackSpeedMul = 1f;
            if (_em.HasComponent<ModifierStats>(entity))
            {
                var stats = _em.GetComponentData<ModifierStats>(entity);
                damageMul = stats.damageMul;
                attackSpeedMul = stats.attackSpeedMul;
            }

            // ── 공격력 ────────────────────────────────────────────────────────
            // 조건 없는 타격당 피해만 낸다. 실제 데미지 체인에는 곱이 더 붙지만
            // (attackerVsCc = 대상이 CC 상태일 때 / frontmostMul = START 스냅샷 /
            //  dcBounceMul = 바운스 감쇠) 전부 **대상·시점 의존**이라 유닛 하나만 보고는
            // 값이 정해지지 않는다. 접어 넣으면 표시가 거짓이 되므로 제외한다(사양).
            //
            // 산식은 AttackSystem RESOLVE 의 `amount = o.magnitude × damageMul` 거울이다.
            // 곱셈 하나라 별도 함수로 빼지 않는다(제약 10 — 자명한 산술의 과잉 추상화 금지).
            float baseMagnitude = 0f;
            if (_em.HasBuffer<AttackOutputElement>(entity))
            {
                var outputs = _em.GetBuffer<AttackOutputElement>(entity);
                for (int i = 0; i < outputs.Length; i++)
                {
                    var o = outputs[i].value;
                    if (o.kind == AttackOutputKind.Damage) baseMagnitude += o.magnitude;
                }
            }
            readout.damage = baseMagnitude * damageMul;
            readout.damageBase = AttackOutputStats.TryGetUniqueMagnitude(
                data.outputs, AttackOutputKind.Damage, out var soDamage) ? soDamage : 0f;

            // ── 공격속도 ──────────────────────────────────────────────────────
            // 실효는 런타임 cooldownDuration(런타임에 바뀔 수 있다), 기본은 SO attackCooldown.
            float cooldown = _em.HasComponent<AttackState>(entity)
                ? _em.GetComponentData<AttackState>(entity).cooldownDuration
                : data.attackCooldown;
            readout.attackRate = UnitStatMath.CooldownToRate(cooldown, attackSpeedMul);
            readout.attackRateBase = UnitStatMath.CooldownToRate(data.attackCooldown, 1f);

            return true;
        }
    }
}
