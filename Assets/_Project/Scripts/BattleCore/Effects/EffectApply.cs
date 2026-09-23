namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — **한 번의 타격이 내는 것을 대상에게 얹는 단 하나의 자리.**
    //
    // ⚠ 왜 `CombatPhase` 안이 아닌가: 평타(`CombatPhase.Resolve`)와 **탄 착탄**
    // (`TickProjectilePhase`, unit 6a2)이 같은 산출물 표(`AttackOutputDef[]`)를 소비하는데,
    // 각자 풀면 언젠가 한쪽만 가드(F3 거점 면역)를 갖고 한쪽만 꼬리표(`OnHit`)를 단다.
    // **평타와 탄이 다른 자를 쓰면 안 된다** — 그 규율의 이행 지점이 이 파일이다.
    //
    // ⚠ **출처는 언제나 「때린 자」**다(F30 · 2026-09-23 사용자 결정 (a)). 옛 전투는 탄이
    // 건 디버프의 출처로 **투사체 개체**를 보내서, 발사마다 새 슬롯이 생겨 곱으로 누적됐다
    // (`Enemy_Debuffer` 의 ×0.6 이 0.6ⁿ 이 됐다). 그래서 이 함수들은 출처를 **인자로 받고**,
    // 부르는 쪽이 탄이면 `Projectile.Owner`(발사자)를 넘긴다 — 탄 자신의 id 가 아니다.
    public static class EffectApply
    {
        /// <summary>
        /// 때리는 쪽의 피해 배율. **「군중 제어에 걸린 적」 추가 배율은 피해자별**이다 —
        /// 잠든 적 옆의 깨어 있는 적은 기준값 그대로다.
        ///
        /// ⚠ 그 술어는 `Cc.Any || Dot.Any` 다. 옛 전투의 한 버퍼(기절·수면·넉백·지속 피해,
        /// **감속 제외**)가 두 자리로 갈렸을 뿐이라 집합은 같다.
        /// 공격자가 없으면(환경·스킬) 1 이다.
        /// </summary>
        public static float DamageMul(Unit attacker, Unit victim)
        {
            if (attacker == null) return 1f;
            var eff = attacker.Modifiers.Effective;
            float mul = eff.DamageMul;
            if (eff.DamageVsCcMul != 1f && victim != null && (victim.Cc.Any || victim.Dot.Any))
                mul *= eff.DamageVsCcMul;
            return mul;
        }

        /// <summary>
        /// 산출물 표를 통째로 적용한다. **평타와 착탄이 부르는 같은 함수**다.
        ///
        /// `sourceUnit` 은 사건의 `SiteFired`(건 쪽의 몸)와 피해 배율에만 쓰이고, 없어도 된다 —
        /// 탄이 날아가는 동안 사수가 죽었을 수 있기 때문이다. 그때도 **출처 id 는 살아 있다**
        /// (`SimEntityId` 는 재사용되지 않는다, 계약 5).
        /// </summary>
        public static void Outputs(TickContext ctx, SimEntityId sourceId, Unit sourceUnit,
                                   Unit victim, AttackOutputDef[] outputs, float damageMul)
            => Outputs(ctx, sourceId, sourceUnit, victim, outputs,
                       outputs != null ? outputs.Length : 0, damageMul);

        /// <summary>
        /// 같은 함수의 **길이 지정** 판. 탄이 나르는 표(`Projectile.OnHit`)는 풀에서 빌린
        /// 배열이라 «담긴 줄 수»가 «배열 길이»보다 작다 — 발사마다 정확한 크기로 새로
        /// 잡으면 그것이 틱 중 할당이 된다(계약 「틱 중 할당 0」).
        /// </summary>
        public static void Outputs(TickContext ctx, SimEntityId sourceId, Unit sourceUnit,
                                   Unit victim, AttackOutputDef[] outputs, int count, float damageMul)
        {
            if (victim == null || outputs == null) return;
            if (count > outputs.Length) count = outputs.Length;

            for (int o = 0; o < count; o++)
            {
                var def = outputs[o];
                switch (def.Kind)
                {
                    case AttackOutputKind.Damage:
                        if (def.Magnitude > 0f)
                            victim.Inbox.Damage.Add(new DamageEntry
                            {
                                Amount = def.Magnitude * damageMul,
                                Source = sourceId,
                            });
                        break;

                    case AttackOutputKind.Heal:
                        // 회복은 **배율 밖**이다(공격력 버프가 힐러를 키우지 않는다 — 현행).
                        if (def.Magnitude > 0f) victim.Inbox.Heal.Add(def.Magnitude);
                        break;

                    case AttackOutputKind.ApplyStat:
                        Stat(ctx, sourceId, sourceUnit, victim, (StatKind)def.Stat, (CombineOp)def.Op,
                             def.Magnitude, def.Duration, SlotTag.Default, 0f, ModifierOrigin.OnHit);
                        break;

                    case AttackOutputKind.ApplyStack:
                        Stack(ctx, sourceId, victim, (StackKind)def.StackKind,
                              def.Magnitude >= 1f ? (int)def.Magnitude : 1,
                              def.StackMaxStack, def.Duration);
                        break;
                }
            }
        }

        /// <summary>
        /// 스탯 슬롯 하나. 반환 = **새로 걸렸나**(갱신이면 false, 그리고 사건도 안 난다 —
        /// 매 틱 갱신하는 장판이 초당 60건을 내지 않게 하는 규약이다).
        /// 진입 가드는 **여기 한 곳**이다: 거점 전면 면역(F3).
        /// </summary>
        public static bool Stat(TickContext ctx, SimEntityId sourceId, Unit sourceUnit, Unit victim,
                                StatKind stat, CombineOp op, float magnitude, float seconds,
                                SlotTag tag, float cap = 0f,
                                ModifierOrigin origin = ModifierOrigin.Unspecified)
        {
            if (!EffectEligibility.AcceptsModifier(victim)) return false;

            var key = new ModifierKey(sourceId, stat, op, tag);
            if (!victim.Modifiers.Apply(in key, magnitude, seconds, cap, origin)) return false;

            ctx.Bus.Publish(CoreEvent.ModifierApplied(ctx.Tick, victim, sourceId, stat, magnitude, sourceUnit));
            return true;
        }

        /// <summary>
        /// 스택을 더한다. 반환 = 더한 뒤의 중첩(0 = 아무 일도 없었다).
        /// 상한·지속은 **저작이 있으면 저작, 없으면 줄, 그것도 없으면 폴백 5**(F14).
        /// </summary>
        public static int Stack(TickContext ctx, SimEntityId sourceId, Unit victim, StackKind kind,
                                int delta, int authoredMaxStack, float authoredDuration)
        {
            if (!EffectEligibility.AcceptsModifier(victim)) return 0;
            if (kind == StackKind.None) return 0;

            var rules = ctx.Def.StackRules;
            int ruleIndex = StackRules.Resolve(rules, kind, -1);
            int maxStack = authoredMaxStack > 0
                ? authoredMaxStack : StackRules.MaxStackOf(rules, ruleIndex);
            float duration = StackRules.PerAppDurationOf(rules, ruleIndex);
            if (duration <= 0f) duration = authoredDuration;

            int count = victim.Stacks.Add(sourceId, kind, ruleIndex, delta, maxStack, duration);
            if (count > 0)
                ctx.Bus.Publish(CoreEvent.StackChanged(ctx.Tick, victim, sourceId, kind, count));
            return count;
        }

        /// <summary>
        /// 지속 피해 슬롯 하나. 반환 = **새로 걸렸나**.
        ///
        /// ⚠ **행동불능 면역 술어를 지나지 않는다** — 지속 피해는 crowd control 이 아니라
        /// 전용 파이프라인이라, 보스에게도 통한다(옛 전투와 같다. 의도).
        /// </summary>
        public static bool Dot(TickContext ctx, SimEntityId sourceId, Unit victim,
                               DotOrigin origin, DotElement element,
                               float scalar, float tickInterval, float seconds)
        {
            if (victim == null || victim.Dead) return false;
            if (!victim.Dot.Apply(origin, element, scalar, tickInterval, seconds)) return false;

            ctx.Bus.Publish(CoreEvent.DotApplied(ctx.Tick, victim, sourceId, origin, element, scalar));
            return true;
        }
    }
}
