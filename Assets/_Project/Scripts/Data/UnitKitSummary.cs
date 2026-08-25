using System.Collections.Generic;

namespace Wassup.Data
{
    // squad-character-page Unit 0 — the detail card's "설명문" is assembled purely
    // from existing DefenderUnitData fields (class + attack archetype + traits),
    // so no per-unit lore has to be authored to give the character page a
    // description. Pure projection helper, mirrors AttackOutputStats: plain input
    // -> plain string, EditMode tested (UnitKitSummaryTests fixes phrasing/order).
    // enum -> Korean label is presentation, not a hardcoded stat; every number in
    // the sentence is read from the SO, never literalised here.
    public static class UnitKitSummary
    {
        // squad-character-page unit 6 — display accessor. Authored `desc` (sheet-synced)
        // wins; empty falls back to the generated summary so new/unauthored units are
        // never blank. Build stays the generator (also used to seed desc).
        public static string Describe(DefenderUnitData u)
        {
            if (u == null) return "";
            return string.IsNullOrEmpty(u.desc) ? Build(u) : u.desc;
        }

        public static string Build(DefenderUnitData u)
        {
            if (u == null) return "";

            string cls = UnitLabels.ClassLabel(u.role);
            string archetype = Archetype(u);
            string head = string.IsNullOrEmpty(cls) ? archetype : $"{cls} · {archetype}";

            var traits = new List<string>();
            if (u.attackTargetCount > 1)
                traits.Add($"최대 {u.attackTargetCount}체 동시 타격");
            // defender-ability-assets unit 2 — trait 문구의 소스가 능력 서브에셋으로 이동.
            // 문구 텍스트/순서는 불변(테스트 고정). 사격 문구는 볼리 능력에 귀속 —
            // 폭탄 등 다른 facing 능력의 문구는 후속(현재 해당 유닛은 authored desc 사용).
            var volley = u.GetAbility<DirectionalVolleyAbility>();
            if (volley != null)
            {
                int shotCount = volley.pattern?.shots?.Length ?? 0;
                string aim = volley.RequiresFacing ? "지정 방향" : "대상 방향";
                traits.Add(shotCount > 1 ? $"{aim}으로 {shotCount}연발 사격" : $"{aim} 사격");
            }
            if (u.aggroCapacity > 0)
                traits.Add($"최대 {u.aggroCapacity}체 도발 유지");
            // on-place-skill-rework unit 6 — 배치 스킬의 출처가 둘이다: 레거시 enum 과
            // 규칙(UnitSkillAbility). 규칙을 먼저 보고, 없을 때만 enum 으로 떨어진다 —
            // 둘 다 선언된 유닛은 bake 가 경고하므로 여기선 한 줄만 낸다.
            string onPlace = OnPlaceRuleClause(u);
            if (string.IsNullOrEmpty(onPlace)) onPlace = OnPlaceClause(u.onPlaceEffect);
            if (!string.IsNullOrEmpty(onPlace))
                traits.Add(onPlace);
            if (u.GetAbility<HazardCastAbility>() != null)
                traits.Add("지속 해저드 설치");

            if (traits.Count == 0) return head + ".";
            return head + ". " + string.Join(", ", traits) + ".";
        }

        private static string Archetype(DefenderUnitData u)
        {
            if (u.targetAllies)
                return HasOutput(u.outputs, AttackOutputKind.Heal) ? "아군 치유형" : "아군 강화형";
            return u.projectile != null ? "원거리형" : "근접형";
        }

        private static bool HasOutput(AttackOutput[] outputs, AttackOutputKind kind)
        {
            if (outputs == null) return false;
            for (int i = 0; i < outputs.Length; i++)
                if (outputs[i].kind == kind) return true;
            return false;
        }

        private static string OnPlaceClause(OnPlaceEffectType effect)
        {
            switch (effect)
            {
                case OnPlaceEffectType.SlowPulse: return "배치 시 주변 둔화";
                case OnPlaceEffectType.BoostNearbyDefenders: return "배치 시 주변 아군 강화";
                case OnPlaceEffectType.BindNearby: return "배치 시 주변 속박";
                case OnPlaceEffectType.MeleeBurst: return "배치 즉시 광역 폭발";
                case OnPlaceEffectType.ForwardProjectile: return "배치 시 전방 발사";
                case OnPlaceEffectType.GainCost: return "배치 시 코스트 획득";
                case OnPlaceEffectType.ReduceSkillCooldown: return "배치 시 스킬 쿨다운 감소";
                case OnPlaceEffectType.ApplyStackNearby: return "배치 시 주변 상태 중첩";
                // 「기절」이 아니라 **「스턴」** (사용자 결정 2026-08-16) — 게임 문안이 쓰는 어휘로
                // 통일한다. 말파이트 desc 가 이 축이다.
                case OnPlaceEffectType.StunNearby: return "배치 시 주변 스턴";
                case OnPlaceEffectType.DotNearby: return "배치 시 주변 지속 피해";
                // default 가 빈 문자열이라 신규 enum 멤버는 **조용히 설명이 빈다**.
                // OnPlaceEffectType 을 늘리면 여기도 같이 늘릴 것.
                default: return "";
            }
        }

        // on-place-skill-rework unit 6 — 규칙(트리거 × 페이로드)으로 선언된 배치 스킬의 문안.
        //
        // 「어그로」가 아니라 **「도발」** 계열 어휘를 쓴다(게임 문안의 기존 표현).
        // ⚠ 배스티온은 **두 도발을 화면에서 갈라야 한다** — 특수 효과 줄이 이미 「공격한 적이
        // 나를 노림」이고, 같은 단어·같은 상태(Aggroed)·같은 아이콘이라 플레이어가 구분할 수 없다.
        // sim 상 차이(상한 무시·즉시·시한)는 전부 숫자고 화면에 그 어휘가 없다. 가르는 축은
        // **「광역 ↔ 공격한 적」** 이다(사용자 결정 2026-08-16 — 「한꺼번에」에서 「광역 도발」로).
        //
        // **사거리를 문안에 노출한다**(같은 결정). 저작값을 그대로 읽어 쓰고 **여기서 숫자를
        // 만들지 않는다** — 문안이 자기 숫자를 갖는 순간 저작값과 갈린다.
        private static string OnPlaceRuleClause(DefenderUnitData u)
        {
            var ability = u.GetAbility<UnitSkillAbility>();
            if (ability?.mechanics == null) return "";
            for (int i = 0; i < ability.mechanics.Length; i++)
            {
                var m = ability.mechanics[i];
                if (m.trigger.kind != DcTriggerKind.OnPlace) continue;
                switch (m.payload.kind)
                {
                    case DcPayloadKind.EmitProjectilePattern:
                        // ⚠ **같은 payload 가 두 그림이다.** 낙하 융단폭격(캐논)과 방향 발사
                        // (샷건맨)가 같은 «발사 명세» 를 쓰므로, 문안은 탄의 **궤적**으로 갈라야
                        // 한다. 안 가르면 샷건맨 충격파가 "미사일 낙하" 로 소개된다(실측 오문안).
                        // 판정은 데이터 계층 enum 하나로 끝난다 — Battle 타입은 참조하지 않는다.
                        if (m.payload.pattern != null && m.payload.pattern.barrel != null
                            && m.payload.pattern.barrel.flightMode == ProjectileFlightMode.Directional)
                        {
                            // 방향의 주어는 유닛마다 다르다 — 조준 UX 가 있으면 «지정 방향», 없으면
                            // «가까운 적». 문안이 없는 조작을 약속하지 않게 여기서 갈라 준다(리뷰 L1).
                            string aimWord = u.RequiresFacing ? "지정 방향으로" : "가까운 적 쪽으로";
                            return m.payload.pattern.barrel.knockbackDistance > 0f
                                ? $"배치 시 {aimWord} 충격파 · 밀어냄"
                                : $"배치 시 {aimWord} 일제 사격";
                        }
                        // bomb-barrel-on-place unit 5 — **같은 payload 의 세 번째 그림**이다.
                        // 위 둘(방향 발사·미사일 낙하)과 달리 이 탄은 착탄해서 피해를 주지 않고
                        // **물건을 세운다** — 궤적이 아니라 착탄에서 하는 일로 갈라야 한다.
                        // 안 가르면 배럴 투척이 「주변 2타일 적 전원에게 미사일 낙하」로 소개된다.
                        if (m.payload.pattern != null && m.payload.pattern.barrel != null
                            && m.payload.pattern.barrel.flightMode == ProjectileFlightMode.BallisticBlocker)
                            return "배치 시 가까운 적에게 폭탄 배럴";
                        // 반경은 패턴이 소유한다(`scopeTileRange`). 패턴 미배선이면 bake 가 loud
                        // 로 거절하므로 여기선 숫자 없이 낸다.
                        return m.payload.pattern != null && m.payload.pattern.scopeTileRange > 0
                            ? $"배치 시 주변 {m.payload.pattern.scopeTileRange}타일 적 전원에게 미사일 낙하"
                            : "배치 시 주변 적 전원에게 미사일 낙하";
                    case DcPayloadKind.AreaTaunt:
                        return m.payload.tileRange > 0
                            ? $"배치 시 주변 {m.payload.tileRange}타일 광역 도발"
                            : "배치 시 광역 도발";
                    // on-place-shuttle-shotgun unit 0 — 배치 즉시 주변 아군 보호막.
                    // 「자신 제외」를 문안에 넣지 않는 것은 다른 절들과 같은 규율이다:
                    // 이 요약은 **무슨 일이 일어나는가**만 말하고 예외는 스펙이 갖는다.
                    case DcPayloadKind.GrantShield:
                        return m.payload.tileRange > 0
                            ? $"배치 시 주변 {m.payload.tileRange}타일 아군에게 보호막"
                            : "배치 시 주변 아군에게 보호막";
                    // skill-layer-migration unit 2a — 브루저 배치 충격파(레거시 `MeleeBurst`).
                    case DcPayloadKind.SelfTileAoe:
                        return m.payload.tileRange > 0
                            ? $"배치 시 주변 {m.payload.tileRange}타일 적에게 충격파"
                            : "배치 시 주변 적에게 충격파";
                    // unit 2b — 스탯 오라 둘. **진영은 payload 가, 스탯은 저작이 정한다** —
                    // 문안도 같은 두 축으로 만든다. 숫자는 저작값을 그대로 읽는다.
                    case DcPayloadKind.AllyStatAura:
                        return $"배치 시 주변 {m.payload.tileRange}타일 아군 "
                               + $"{StatWord(m.payload.buffStat)} {SignedPercent(m.payload.magnitude)}"
                               + $" ({m.payload.duration:0.#}초)";
                    case DcPayloadKind.OpponentStatAura:
                        return $"배치 시 주변 {m.payload.tileRange}타일 적 "
                               + $"{StatWord(m.payload.buffStat)} {SignedPercent(m.payload.magnitude)}"
                               + $" ({m.payload.duration:0.#}초)";
                    // unit 2c — 판 밖 런타임. 반경도 지속도 없어 숫자는 하나뿐이다.
                    case DcPayloadKind.GainCost:
                        return $"배치 시 코스트 +{m.payload.magnitude:0.#}";
                    case DcPayloadKind.ReduceSkillCooldown:
                        return $"배치 시 스킬 쿨다운 −{m.payload.magnitude:0.#}초";
                    // ⚠ 배선하지 않은 payload 는 조용히 문안이 빈다(위 enum 경로와 같은 함정).
                    // **`return` 이 아니라 `continue` 다** — 여기서 반환하면 규칙이 둘일 때
                    // 첫 번째가 미배선이라는 이유로 두 번째 문안까지 사라진다.
                    // `UnitKitSummaryTests` 의 전수 순회가 배선 누락 자체를 잡는다.
                    default: continue;
                }
            }
            return "";
        }

        // 스탯 축의 화면 어휘. **정의 계층이 `Battle.StatKind` 를 모르게 유지**하므로
        // 저작 enum(`CardBuffKind`)에서 바로 만든다.
        private static string StatWord(CardBuffKind kind)
        {
            switch (kind)
            {
                case CardBuffKind.AttackDamage: return "공격력";
                case CardBuffKind.AttackSpeed: return "공격 속도";
                case CardBuffKind.EffectiveHealth: return "체력";
                case CardBuffKind.MoveSpeed: return "이동 속도";
                case CardBuffKind.DamageVsCc: return "상태이상 적 피해";
                default: return "능력치";
            }
        }

        // 저작은 퍼센트다(30 = +30%, −90 = −90%). 부호를 화면 어휘로 만든다 —
        // **여기서 숫자를 만들지 않는다**(저작값과 갈리는 것을 막는 이 파일의 규율).
        private static string SignedPercent(float percent)
            => percent >= 0f ? $"+{percent:0.#}%" : $"{percent:0.#}%";
    }
}
