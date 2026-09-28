namespace Wassup.BattleCore.Trigger
{
    /// <summary>
    /// skill-data-table unit 3(U7) — 효과 수치의 **방식**. 닫힌 집합 · append-only. 기본 `Flat` = 저작값 그대로(오늘 전량).
    /// </summary>
    public enum MagnitudeMode : byte
    {
        Flat = 0,
        /// <summary>소유자의 기준 스탯(`BasisStat`) × `EffectDef.Ratio` — 값은 **시전 순간** 최종 스탯으로 한 번 정한다(계약 9).</summary>
        OwnerStatRatio = 1,
    }

    /// <summary>비율형의 기준 스탯. 닫힌 집합 · append-only(초안 둘 — U7).</summary>
    public enum BasisStat : byte
    {
        /// <summary>평타 한 발의 피해 출력 합 × **공격자 쪽 배율만**(U11 — 대상 조건 배율 · 강타 제외).</summary>
        Attack = 0,
        /// <summary>`Unit.MaxHealth`(모디파이어 반영값). ⚠ 모디파이어 단계에서만 갱신돼 같은 틱 버프는 한 틱 늦게 보인다(계약 9).</summary>
        MaxHealth = 1,
    }

    // skill-data-table unit 3 — **비율형 수치의 뜻을 한 곳에**(제약 10 — plain 값 입력 → plain 값 출력).
    //
    // 세 가지만 안다: ① 종류마다 비율이 들어가는 칸(`tables.md` §9 — 피해 · 실드량) ② 기준 스탯 「공격력」의 산식
    // ③ 해석(비율형 효과 줄 → 고정값이 실린 사본). 「언제 · 누구의 스탯으로」는 호출부가 정한다 — 시전 순간은 디스패처
    // 드레인(`TriggerDispatcher.Execute`), 주인이 떠난 사건은 감지자 스냅샷(`TriggerEvent.SubjectAttack` · `SubjectMaxHp`).
    //
    // ⚠ 「공격력」은 `CombatPhase.ShotDamage` 를 재사용하지 않는다 — 그 함수는 피해자를 받아 대 CC 배율을 곱한다.
    //    강타(`AttackMod.HeavyStrike`)도 부르지 않는다 — 그 함수는 카운터를 전진시킨다. 여기는 상태를 읽기만 한다.
    public static class EffectMagnitude
    {
        // 비율이 들어가는 원시 칸 — 오늘 저장 칸(`Magnitude` · `Damage`)이 종류마다 다르다(unit 4 가 저작 칸을 통일한다).
        private enum Slot : byte { None = 0, Magnitude = 1, Damage = 2 }

        private static Slot SlotOf(EffectKind kind)
        {
            switch (kind)
            {
                // 피해를 `Magnitude` 로 싣는 종류(concrete 가 `p.Magnitude` / params 뷰 `Damage` · `PerTickDamage` 로 읽는다).
                case EffectKind.ProjectileToTarget:
                case EffectKind.SelfTileAoe:
                case EffectKind.SelfOrbitProjectile:
                case EffectKind.AreaBreath:
                case EffectKind.AreaCc:
                case EffectKind.AreaDot:
                // 실드량.
                case EffectKind.GrantShield:
                    return Slot.Magnitude;
                // 피해 칸(U10) — 발사 명세 한 발 · 장판 DoT · 도약 2종 착지 슬램.
                case EffectKind.EmitProjectilePattern:
                case EffectKind.SpawnHazard:
                case EffectKind.SelfBlink:
                case EffectKind.UltimateLeap:
                    return Slot.Damage;
                // 나머지(버프 · 오라 · CC · 스택 · 배율 · 도발 · 메타 · 표식 · 손패 · 액티브 = `None` 종류) — 비율 칸이 없다.
                default:
                    return Slot.None;
            }
        }

        /// <summary>그 종류에 비율 칸이 있나(`tables.md` §9). 없으면 비율형 저작은 검증에서 거절(`ComboVerdict.NoRatioField`).</summary>
        public static bool AcceptsRatio(EffectKind kind) => SlotOf(kind) != Slot.None;

        /// <summary>
        /// 기준 스탯 「공격력」(U11) = 평타 한 발의 **피해 출력 합** × 공격자 쪽 배율. 피해 아닌 출력(회복 · 스탯 · 스택)은 안 센다.
        /// `attackerDamageMul` 에는 공격자 쪽 배율만 넘긴다(`EffectiveStats.DamageMul`) — 대 CC · 수면 · 최전방 · 강타는 넘기지 않는다.
        /// </summary>
        public static float AttackBasis(AttackOutputDef[] outputs, float attackerDamageMul)
        {
            if (outputs == null) return 0f;
            float sum = 0f;
            for (int o = 0; o < outputs.Length; o++)
                if (outputs[o].Kind == AttackOutputKind.Damage) sum += outputs[o].Magnitude;
            return sum * attackerDamageMul;
        }

        /// <summary>
        /// 그 유닛의 **지금** 기준 스탯(최종값). 읽기만 한다 — 카운터 · 슬롯을 건드리지 않는다. 공격이 없는 유닛의 공격력 = 0.
        /// </summary>
        public static float BasisOf(Unit u, BasisStat stat)
        {
            if (u == null) return 0f;
            if (stat == BasisStat.MaxHealth) return u.MaxHealth;
            return u.Attack != null ? AttackBasis(u.Attack.Outputs, u.Modifiers.Effective.DamageMul) : 0f;
        }

        /// <summary>
        /// 비율형 효과 줄 → 그 종류의 비율 칸에 `basis × Ratio` 를 실은 **사본**(방식은 그대로 둔다 — 사본은 해시에 안 간다).
        /// `Flat` 이거나 비율 칸이 없는 종류면 그대로 돌려준다(후자는 검증이 이미 거절했다).
        /// </summary>
        public static EffectDef Resolve(in EffectDef e, float basis)
        {
            var r = e;
            if (e.MagnitudeMode != MagnitudeMode.OwnerStatRatio) return r;
            float value = basis * e.Ratio;
            switch (SlotOf(e.Kind))
            {
                case Slot.Magnitude: r.Magnitude = value; break;
                case Slot.Damage: r.Damage = value; break;
            }
            return r;
        }
    }
}
