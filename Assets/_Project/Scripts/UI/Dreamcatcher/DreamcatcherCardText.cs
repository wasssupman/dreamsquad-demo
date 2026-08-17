using System;
using System.Collections.Generic;
using System.Globalization;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // Shared card body formatter for the deck builder, inspect panel and hand tooltip.
    // Numbers come from the serialized effect/mechanic/skill data; authored prose is
    // only a fallback for cards whose data has no supported summary rule yet.
    public static class DreamcatcherCardText
    {
        // dreamcatcher-attach-requirement unit 4 — unitNameOf 는 부착 제한이
        // UnitId 일 때 id→표시명 해석기(주입). optional 이라 기존 호출처는 무수정으로
        // 컴파일되고, 미주입 시 id 문자열로 폴백한다(unit 5 가 실제 해석기를 넘긴다).
        // 포매터는 DefenderCatalog 를 직접 알지 않는다 — 순수 유지.
        public static string Body(DreamcatcherCard card, Func<string, string> unitNameOf = null)
            => Assemble(card, compact: false, unitNameOf);

        public static string BodyCompact(DreamcatcherCard card, Func<string, string> unitNameOf = null)
            => Assemble(card, compact: true, unitNameOf);

        // dreamcatcher-hand-card-face unit 0 — 손패 카드 본문: 타입/대상은 카드 면의
        // 색·태그 칩이 담당하므로 헤더 줄(축·타입) 없이 효과 라인만. 라인 빌드와
        // description 폴백 규칙은 Assemble 과 같은 소스(LinesWithFallback)를 공유한다.
        // 화살표 강제 줄바꿈(2026-07-25 사용자 확정): 좁은 카드 폭에서 "트리거 →" 와
        // 효과가 줄로 분리되어 구조가 읽힌다. 손패 전용 — 툴팁/덱빌더(넓은 패널)는 미적용.
        public static string BodyLinesOnly(DreamcatcherCard card, Func<string, string> unitNameOf = null)
            => string.Join("\n", LinesWithFallback(card, unitNameOf)).Replace(" → ", " →\n");

        // selection-hand-attach unit 11 rev2 — 부착 셀용 압축 문안.
        // **"항상 → " 만 떼고 나머지 트리거는 남긴다**(사용자 결정 2026-07-30).
        //
        // rev1 은 화살표 뒤를 전부 취했는데, 그러면 조건부 카드의 뜻이 무너진다:
        // 궁지폭발 "HP 30% 이하일 때 1번째 피격마다 → 반경 1칸 피해 20" 이 "반경 1칸 피해 20"
        // 만 남고, 시한폭탄의 "3번째 공격마다 → 피해 x2" 는 "피해 x2" 가 돼 상시 효과처럼 읽힌다.
        // **"언제"가 곧 그 카드의 정체인 경우가 많다.**
        //
        // "항상" 은 정보가 0 인 유일한 트리거라 이것만 떼는 것이 안전하다.
        // 조건 표기(부착 제한 "레인저 전용")는 효과가 아니므로 계속 버린다 — 판별은 휴리스틱이
        // 아니라 그 줄을 만든 AttachRequirementLine 과 직접 대조한다.
        public static string EffectOnly(DreamcatcherCard card, Func<string, string> unitNameOf = null)
        {
            var lines = LinesWithFallback(card, unitNameOf);
            string requirement = AttachRequirementLine(card, unitNameOf);
            var kept = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (requirement != null && line == requirement) continue; // 조건 표기 ≠ 효과
                kept.Add(line.StartsWith(AlwaysPrefix, StringComparison.Ordinal)
                    ? line.Substring(AlwaysPrefix.Length).Trim()
                    : line.Trim());
            }
            return string.Join("\n", kept);
        }

        // "트리거 → 효과" 의 구분자. 앞뒤 공백까지가 한 토큰이다(BodyLinesOnly 도 같은 문자열로
        // 줄바꿈을 넣는다) — 여기서만 다르게 쓰면 두 표면이 어긋난다.
        private const string Arrow = " → ";

        // 조건 없는 상시 효과의 트리거. **생산자와 소비자가 같은 상수를 본다** —
        // EffectOnly 가 이 접두만 떼어내므로 리터럴로 흩어두면 조용히 어긋난다.
        private const string AlwaysPrefix = "항상" + Arrow;

        private static string Assemble(DreamcatcherCard card, bool compact, Func<string, string> unitNameOf = null)
        {
            var lines = LinesWithFallback(card, unitNameOf);

            string axis = AxisLabel(card == null ? CardTargetAxis.All : card.axis);
            string typeLabel = TypeLabel(card == null ? CardType.Squad : card.type);
            string header = card != null && card.type == CardType.Squad
                ? $"<color=#F5D480><b>{axis}</b></color>  ·  {typeLabel}"
                : typeLabel;
            string gap = compact ? "\n" : "\n\n";
            string headSize = compact ? "115%" : "22";
            string body = $"<size={headSize}>{header}</size>";
            if (lines.Count > 0) body += gap + string.Join("\n", lines);
            return body;
        }

        private static List<string> LinesWithFallback(DreamcatcherCard card,
            Func<string, string> unitNameOf = null)
        {
            bool hasUnsupportedData;
            var lines = BuildSummaryLines(card, out hasUnsupportedData);
            if ((lines.Count == 0 || hasUnsupportedData)
                && card != null && !string.IsNullOrEmpty(card.description))
            {
                if (hasUnsupportedData) lines.Clear();
                lines.Add(card.description);
            }
            // unit 4 — 부착 제한 접두는 description 폴백(위 Clear) **뒤**에 얹는다.
            // 여기 한 곳에 넣으면 Body/BodyCompact/BodyLinesOnly 세 표면이 함께 얻는다.
            string requirement = AttachRequirementLine(card, unitNameOf);
            if (requirement != null) lines.Insert(0, requirement);
            return lines;
        }

        // unit 4(+unit 7 rev) — "가디언 전용" / "{유닛명} 전용". 무효 설정(빈 값·알 수 없는
        // 클래스 이름)에는
        // 붙이지 않는다 — fail-closed 는 게이트와 validator 가 담당하고, 플레이어에게
        // "None 전용" 같은 문구를 보이지 않는다. 클래스 라벨은 기존 UnitLabels 재사용.
        internal static string AttachRequirementLine(DreamcatcherCard card, Func<string, string> unitNameOf)
        {
            if (card == null) return null;
            switch (card.attachType)
            {
                case DcAttachType.Class:
                    if (!DreamcatcherAttachEval.TryParseAttachClass(card.attachValue, out var role)) return null;
                    string cls = UnitLabels.ClassLabel(role);
                    return string.IsNullOrEmpty(cls) ? null : $"{cls} 전용";
                case DcAttachType.UnitId:
                    if (string.IsNullOrEmpty(card.attachValue)) return null;
                    string name = unitNameOf == null ? null : unitNameOf(card.attachValue);
                    return $"{(string.IsNullOrEmpty(name) ? card.attachValue : name)} 전용";
                default:
                    return null;
            }
        }

        private static List<string> BuildSummaryLines(DreamcatcherCard card, out bool hasUnsupportedData)
        {
            var lines = new List<string>();
            hasUnsupportedData = false;
            if (card == null) return lines;

            switch (card.type)
            {
                case CardType.Squad:
                    BuildSquadLines(card, lines);
                    break;
                case CardType.Unit:
                    hasUnsupportedData = !BuildUnitLines(card, lines);
                    break;
                case CardType.Active:
                    if (card.skill != null) hasUnsupportedData = !BuildSkillLine(card.skill, lines);
                    break;
            }

            return lines;
        }

        private static void BuildSquadLines(DreamcatcherCard card, List<string> lines)
        {
            if (card.leakAllowanceCost > 0)
            {
                lines.Add($"부착 즉시 → 유출 허용치 -{card.leakAllowanceCost} (환불 없음)");
            }

            if (card.effects == null || card.effects.Length == 0) return;

            var effects = new List<string>();
            foreach (var effect in card.effects)
                effects.Add(FormatSquadEffect(card.axis, effect));

            lines.Add(AlwaysPrefix + string.Join(" · ", effects));
        }

        private static string FormatSquadEffect(CardTargetAxis axis, CardEffect effect)
        {
            string target = AxisTargetLabel(axis);
            if (effect.kind == CardBuffKind.DamageVsCc)
                return $"CC 상태 적에게 {target} 피해 {SignedPercent(effect.percent)}";

            return $"{target} {BuffLabel(effect.kind)} {SignedPercent(effect.percent)}";
        }

        private static bool BuildUnitLines(DreamcatcherCard card, List<string> lines)
        {
            bool supported = true;
            if (card.attackMods != null)
            {
                foreach (var mod in card.attackMods)
                {
                    string line;
                    if (TryFormatAttackMod(mod, out line)) lines.Add(line);
                    else supported = false;
                }
            }

            if (card.mechanics != null)
            {
                foreach (var mechanic in card.mechanics)
                    if (!TryAppendMechanic(lines, mechanic)) supported = false;
            }

            return supported;
        }

        private static bool TryFormatAttackMod(DcAttackModSpec mod, out string line)
        {
            line = null;
            switch (mod.kind)
            {
                case DcAttackModKind.ProjectileBounce:
                    string falloff = Approximately(mod.damageMul, 1f)
                        ? "감쇠 없음"
                        : $"튕길 때마다 피해 {Multiplier(mod.damageMul)}";
                    line = AlwaysPrefix + $"공격 투사체가 최대 {Count(mod.tileRange)}칸 범위 내 "
                         + $"{Count(mod.count)}회 튕김 ({falloff})";
                    return true;
                case DcAttackModKind.FrontmostTarget:
                    line = AlwaysPrefix + "목표 지점에 가장 가까운 적 우선 공격"
                         + $" · 해당 적 직접 피해 {SignedPercent((mod.damageMul - 1f) * 100f)}";
                    return true;
                // content-4 unit 0 — 수면 특효. "잠든 적에게만" 이 카드 판단의 핵심이라
                // 대상 조건을 문안 앞에 둔다(ApplyCcToTarget 의 Sleep 문안과 같은 판단).
                case DcAttackModKind.DamageVsSleeping:
                    line = AlwaysPrefix + $"잠든 적에게 주는 피해 {Multiplier(mod.damageMul)}";
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryAppendMechanic(List<string> lines, DcMechanic mechanic)
        {
            DcPayloadSpec payload = mechanic.payload;
            string trigger;

            switch (payload.kind)
            {
                case DcPayloadKind.DreamCocoon:
                    lines.Add($"부착 즉시 → 수면 {Duration(payload.duration)} (피격 시 종료)");
                    lines.Add($"무피격 완주 시 → 남은 전투 동안 {BuffLabel(payload.buffStat)} "
                           + $"{SignedPercent(payload.magnitude)}");
                    return true;
                case DcPayloadKind.BountyMark:
                    lines.Add($"적 지정 → 대상 받는 피해 -{Count(payload.tileRange)}%");
                    lines.Add($"표식 대상 처치 시 → 각성치 {Multiplier(payload.magnitude)}");
                    lines.Add("표식 대상 이탈 시 → 보상 없음");
                    return true;
                case DcPayloadKind.SelfBuffLethal:
                    lines.Add($"부착 즉시 → 공격 속도 {SignedPercent(payload.magnitude)}"
                           + $" · {Duration(payload.duration)} 후 사망");
                    return true;
                case DcPayloadKind.PlacementAura:
                    lines.Add($"호스트 생존 중 → 새 유닛 배치 {Duration(payload.duration)} 후 "
                           + $"공격 속도 {SignedPercent(payload.magnitude)}");
                    return true;
            }

            if (!TryFormatTrigger(mechanic.trigger, out trigger)) return false;

            string effect;
            switch (payload.kind)
            {
                case DcPayloadKind.ProjectileToTarget:
                    // content-5 unit 3 — 이 payload 는 이제 **탄 에셋의 궤적을 따른다**.
                    // 왕복(부메랑)은 «대상에게» 가 아니라 경로를 훑고 돌아오므로 문안이
                    // 갈려야 한다 — 판정은 탄 SO 를 읽어서 하고 문자열에 수치를 복제하지 않는다.
                    effect = payload.projectile != null
                             && payload.projectile.flightMode == ProjectileFlightMode.Boomerang
                        ? $"부메랑이 날아갔다 돌아오며 스치는 적에게 피해 {Count(payload.magnitude)}"
                        : $"대상에게 추가 투사체 피해 {Count(payload.magnitude)}";
                    break;
                case DcPayloadKind.SelfTileAoe:
                    // content-4 unit 0 — duration>0 = 낙하 예고(퇴근 운석). 기존 SelfTileAoe
                    // 카드는 전부 0 이라 문안이 바뀌지 않는다. 접두 형태는 액티브 운석 문안
                    // (BuildSkillLine 의 warningSec)과 같게 맞춘다.
                    effect = payload.duration > 0f
                        ? $"{Duration(payload.duration)} 후 반경 {Count(payload.tileRange)}칸 피해 {Count(payload.magnitude)}"
                        : $"반경 {Count(payload.tileRange)}칸 피해 {Count(payload.magnitude)}";
                    break;
                case DcPayloadKind.NextAttackDoubleFire:
                    effect = "다음 공격 2연발";
                    break;
                case DcPayloadKind.AreaBarrage:
                    effect = $"반경 {Count(payload.tileRange)}칸 피해 {Count(payload.magnitude)}";
                    break;
                case DcPayloadKind.SelfBlink:
                    effect = $"반경 {Count(payload.tileRange)}칸 내 위치로 이동";
                    break;
                case DcPayloadKind.AllyMoveSpeedAura:
                    effect = $"반경 {Count(payload.tileRange)}칸 아군 이동 속도 "
                           + $"{SignedPercent(payload.magnitude)} · {Duration(payload.duration)}";
                    break;
                case DcPayloadKind.ApplyCcToTarget:
                    // Sleep 은 wake-on-hit 리스크가 카드 판단의 핵심이라 문안에 명시 (content-3 unit 2)
                    effect = payload.ccKind == DcCcKind.Impulse
                        ? $"대상에게 넉백 속도 {Count(payload.magnitude)} · {Duration(payload.duration)}"
                        : payload.ccKind == DcCcKind.Sleep
                            ? $"대상에게 수면 {Duration(payload.duration)} (피격 시 해제)"
                            : $"대상에게 {CcLabel(payload.ccKind)} {Duration(payload.duration)}";
                    break;
                case DcPayloadKind.ApplyStackToTarget:
                    effect = $"대상에게 {StackLabel(payload.stackKind)} {Count(payload.magnitude)}스택"
                           + $" · {Duration(payload.duration)}";
                    break;
                // berserker unit 1 — tileRange > 0 이면 재발동이 덮어쓰기가 아니라 **누적**이다
                // (광란). 중첩과 상한을 말해주지 않으면 「+8%」만 보고 약한 카드로 읽힌다.
                // 상한 %는 문자열에 박지 않고 저작값에서 뽑는다(제약 6) — 시트가 배율이나
                // 중첩 중 하나만 바꿔도 문안이 따라간다.
                case DcPayloadKind.SelfStatBuff:
                    bool stacks = payload.tileRange > 0;
                    effect = $"{BuffLabel(payload.buffStat)} {SignedPercent(payload.magnitude)}";
                    if (stacks) effect += $" 중첩 (최대 {Count(payload.tileRange)}중첩";
                    if (payload.duration > 0f)
                        effect += stacks ? $" · {Duration(payload.duration)})" : $" · {Duration(payload.duration)}";
                    else if (stacks) effect += ")";
                    else if (mechanic.trigger.kind == DcTriggerKind.HealthThreshold)
                        effect += " · 전투 중 1회";
                    else effect += " · 남은 전투 동안";
                    break;
                case DcPayloadKind.HeavyStrike:
                    effect = $"피해 {Multiplier(payload.magnitude)}";
                    break;
                case DcPayloadKind.AreaSleep:
                    effect = $"반경 {Count(payload.tileRange)}칸 내 적 최대 "
                           + $"{Count(payload.magnitude)}명 수면 {Duration(payload.duration)}";
                    break;
                // content-4 unit 0 — 궤도 화염구. 재타격 간격은 탄 SO 소유라 이 문안이 모른다
                // (수치를 문자열에 복제하지 않는다 — 제약 6). 반복 타격이라는 **사실**만 적는다.
                case DcPayloadKind.SelfOrbitProjectile:
                    effect = $"주위를 도는 화염구 {Duration(payload.duration)}"
                           + $" · 스치는 적에게 피해 {Count(payload.magnitude)}";
                    break;
                // content-5 unit 4 — 잿불(장판 설치). **수치를 하나도 적지 않는다** — 피해·
                // 지속·반경이 전부 해저드 SO 소유라 여기서 읽으면 문자열 복제가 된다(제약 6).
                // 카드가 말할 수 있는 것은 「무슨 일이 일어나는가」뿐이다.
                case DcPayloadKind.SpawnHazard:
                    effect = "그 자리에 불씨를 남긴다";
                    break;
                // content-5 unit 5 — 발사 명세. 발수·피해는 **패턴 SO 에서 읽는다**(문자열
                // 복제 금지 — 제약 6). 「어떻게 흩어지나」는 selection/재추첨 조합이라
                // 문안으로 옮기지 않는다 — 눈으로 보이는 사실만 적는다.
                case DcPayloadKind.EmitProjectilePattern:
                {
                    var pat = payload.pattern;
                    if (pat == null) return false;
                    int shotCount = pat.shots != null ? pat.shots.Length : 0;
                    effect = $"주변 적에게 {Count(shotCount)}발 발사 · 발당 피해 {Count(pat.damage)}";
                    break;
                }
                default:
                    return false;
            }

            lines.Add($"{trigger} → {effect}");
            // content-3 unit 6 — 스택 카드는 "무엇이 쌓이나" 만으로는 정체가 안 읽힌다.
            // 임계에서 무엇이 터지는지가 그 카드의 실체라 요약 라인을 한 줄 덧붙인다.
            if (payload.kind == DcPayloadKind.ApplyStackToTarget)
            {
                string summary = StackThresholdSummary(payload.stackModifier, payload.stackKind);
                if (summary != null) lines.Add(summary);
            }
            return true;
        }

        // content-3 unit 6 — ThresholdRule[] → 한 줄 요약. 수치는 전부 SO 에서 나온다
        // (문자열에 복제 금지 — 제약 6). so 미연결 = null 반환 = 라인 생략.
        internal static string StackThresholdSummary(StackModifierSO so, DcStackKind stackKind)
        {
            if (so == null || so.thresholds == null || so.thresholds.Length == 0) return null;

            var parts = new List<string>();
            var rules = so.thresholds;

            // 균등 계단(1중첩부터 1씩, 같은 stat, 등차)이면 중첩별 나열 대신 "중첩당" 으로 접는다.
            int folded = 0;
            if (TryFoldStatRamp(rules, out float stepPercent, out StatKind rampStat, out folded))
                parts.Add($"중첩당 {StatLabel(rampStat)} {SignedPercent(stepPercent)}");

            for (int i = folded; i < rules.Length; i++)
            {
                var rule = rules[i];
                string body;
                switch (rule.derivedKind)
                {
                    case DerivedEffectKind.ApplyStat:
                        body = $"{StatLabel(rule.stat)} {SignedPercent(StatPercent(rule.op, rule.magnitude))}";
                        break;
                    case DerivedEffectKind.ApplyStun:
                        // 심에서 이 파생은 CcKind.Stun 이다 — 별도 상태로 오해시키지 않게 CcLabel 과 통일.
                        body = $"{CcLabel(DcCcKind.Stun)} {Duration(rule.magnitude)}";
                        break;
                    case DerivedEffectKind.ApplyDot:
                        // tickInterval>0 이면 magnitude 는 틱당 피해다(dot-tick-cadence 계약) —
                        // 카드에는 초당으로 환산해 적는다.
                        body = $"초당 피해 {Count(DotPerSecond(rule.magnitude, rule.tickInterval))}"
                             + $" · {Duration(rule.duration)}";
                        break;
                    default:
                        continue;
                }
                if (rule.mode == ThresholdMode.Consume) body += " (중첩 소모)";
                parts.Add($"{Count(rule.atStack)}중첩 {body}");
            }

            return parts.Count == 0 ? null : $"{StackLabel(stackKind)} {string.Join(" · ", parts)}";
        }

        // 선두 ApplyStat 규칙들이 "1중첩부터 1씩 · 같은 stat/op · 등차" 인지 판정한다.
        // 참이면 stepPercent(중첩당 증감 %)와 접힌 개수를 돌려준다. 규칙 2개 미만이면 접지 않는다.
        internal static bool TryFoldStatRamp(ThresholdRule[] rules, out float stepPercent,
            out StatKind stat, out int foldedCount)
        {
            stepPercent = 0f;
            stat = default;
            foldedCount = 0;
            if (rules == null || rules.Length < 2) return false;

            var first = rules[0];
            if (first.derivedKind != DerivedEffectKind.ApplyStat || first.atStack != 1) return false;
            stat = first.stat;
            stepPercent = StatPercent(first.op, first.magnitude);
            if (Approximately(stepPercent, 0f)) return false;

            int n = 1;
            while (n < rules.Length)
            {
                var r = rules[n];
                if (r.derivedKind != DerivedEffectKind.ApplyStat) break;
                if (r.atStack != n + 1 || r.stat != stat || r.op != first.op) break;
                if (!Approximately(StatPercent(r.op, r.magnitude), stepPercent * (n + 1))) break;
                n++;
            }

            // 접기 실패 시 foldedCount 를 0 으로 되돌린다 — 호출부가 이 값을 나열 시작
            // 인덱스로 쓰므로, 실패한 채 1 을 남기면 첫 규칙이 문안에서 통째로 사라진다.
            if (n < 2) { foldedCount = 0; return false; }
            foldedCount = n;
            return true;
        }

        // Multiplicative 는 배율(0.9 = -10%), Additive 는 델타(0.3 = +30%) — ModifierAuthoring 규약.
        internal static float StatPercent(CombineOp op, float magnitude)
            => op == CombineOp.Multiplicative ? (magnitude - 1f) * 100f : magnitude * 100f;

        internal static float DotPerSecond(float magnitude, float tickInterval)
            => tickInterval > 0f ? magnitude / tickInterval : magnitude;

        private static string StatLabel(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.DamageMul: return "공격력";
                case StatKind.AttackSpeedMul: return "공격 속도";
                case StatKind.DmgTakenMul: return "받는 피해";
                case StatKind.MoveSpeedMul: return "이동 속도";
                case StatKind.MaxHealthMul: return "최대 체력";
                default: return stat.ToString();
            }
        }

        private static bool TryFormatTrigger(DcTriggerSpec trigger, out string text)
        {
            // trigger-gates unit 1 — 게이트 접두는 트리거 문안에 직교 합성 (조립 위치 고정).
            // 표시 가능한 조합도 bake 와 같은 배선 표를 소비해 "보이지만 무효"인 카드를 막는다.
            if (!DcTrigger.GateComboSupported(trigger.kind, trigger.gate, trigger.gateSubject))
            {
                text = null;
                return false;
            }
            if (!TryFormatTriggerCore(trigger, out text)) return false;
            if (trigger.gate == DcGateKind.HpBelow)
                text = (trigger.gateSubject == DcGateSubject.EventTarget
                    ? $"HP {Number(trigger.gateValue * 100f)}% 이하인 적에게 "
                    : $"HP {Number(trigger.gateValue * 100f)}% 이하일 때 ") + text;
            return true;
        }

        private static bool TryFormatTriggerCore(DcTriggerSpec trigger, out string text)
        {
            switch (trigger.kind)
            {
                case DcTriggerKind.AttackN:
                    // period 1 은 "1번째 공격마다"가 아니라 "공격마다" (frostbite, content-3 unit 1)
                    text = trigger.period == 1 ? "공격마다" : $"{Count(trigger.period)}번째 공격마다";
                    return trigger.period > 0;
                case DcTriggerKind.OnDamagedN:
                    text = $"{Count(trigger.period)}번째 피격마다";
                    return trigger.period > 0;
                case DcTriggerKind.OnDeath:
                    text = "이 유닛이 사망하면";
                    return true;
                case DcTriggerKind.PeriodicTimer:
                    text = $"{Duration(trigger.periodSeconds)}마다";
                    return trigger.periodSeconds > 0f;
                case DcTriggerKind.HealthThreshold:
                    text = $"HP {Number((1f - trigger.fraction) * 100f)}% 이하";
                    return trigger.fraction > 0f;
                case DcTriggerKind.OnKill:
                    text = "이 유닛이 적을 처치하면";
                    return true;
                case DcTriggerKind.OnShieldBreak:
                    text = "실드 파괴 시";
                    return true;
                // content-4 unit 0 — 퇴근. 사망("이 유닛이 사망하면")과 **문안에서도 갈라야**
                // 한다 — 두 트리거가 교차 발동하지 않는다는 것이 이 카드의 계약이라,
                // 문안이 흐리면 플레이어가 죽어도 터질 거라 기대한다.
                case DcTriggerKind.OnRetire:
                    text = "이 유닛이 퇴근하면";
                    return true;
                default:
                    text = null;
                    return false;
            }
        }

        private static bool BuildSkillLine(SkillData skill, List<string> lines)
        {
            string effect;
            switch (skill.effect)
            {
                case SkillEffectType.Meteor:
                    effect = "타일 지정 → ";
                    if (skill.warningSec > 0f) effect += $"{Duration(skill.warningSec)} 후 ";
                    effect += $"반경 {Count(skill.range)}칸 피해 {Count(skill.magnitude)}";
                    break;
                case SkillEffectType.Portal:
                    effect = $"두 타일 지정 → 입구 진입 적을 출구로 이동 · {Duration(skill.durationSec)}";
                    break;
                // active-dreamcatcher-tile-aim unit 0 — 아군 버프도 타일 지정 + 반경이다.
                case SkillEffectType.PowerSurge:
                    effect = $"타일 지정 → 반경 {Count(skill.range)}칸 아군 공격력 "
                           + $"{Multiplier(skill.magnitude)} · {Duration(skill.durationSec)}";
                    break;
                case SkillEffectType.RapidFire:
                    effect = $"타일 지정 → 반경 {Count(skill.range)}칸 아군 공격 속도 "
                           + $"{Multiplier(skill.magnitude)} · {Duration(skill.durationSec)}";
                    break;
                case SkillEffectType.SlowField:
                    effect = $"타일 지정 → 반경 {Count(skill.range)}칸 적 이동 속도 "
                           + $"{Multiplier(skill.magnitude)} · {Duration(skill.durationSec)}";
                    break;
                case SkillEffectType.Tornado:
                    effect = $"타일 지정 → 반경 {Count(skill.range)}칸 적을 중심으로 끌어당김"
                           + $" · 끌어당김 속도 {Count(skill.magnitude)} · {Duration(skill.durationSec)}";
                    break;
                default:
                    return false;
            }

            lines.Add($"{effect} · 비용 {skill.cost} · 재사용 {Duration(skill.cooldownSec)}");
            return true;
        }

        // dreamcatcher-hand-card-face unit 0 — CardCategoryStyle.TargetTag 가 위임(축 라벨
        // 이중 정의 금지). 같은 어셈블리(Wassup.Runtime) 한정.
        internal static string AxisLabel(CardTargetAxis axis)
        {
            switch (axis)
            {
                case CardTargetAxis.ClassRanger: return "레인저";
                case CardTargetAxis.ClassGuardian: return "가디언";
                case CardTargetAxis.Cost1: return "1코스트";
                default: return "전체";
            }
        }

        private static string AxisTargetLabel(CardTargetAxis axis)
        {
            switch (axis)
            {
                case CardTargetAxis.ClassRanger: return "레인저 아군";
                case CardTargetAxis.ClassGuardian: return "가디언 아군";
                case CardTargetAxis.Cost1: return "1코스트 유닛";
                default: return "모든 아군";
            }
        }

        private static string TypeLabel(CardType type)
        {
            switch (type)
            {
                case CardType.Unit: return "유닛 부착";
                case CardType.Active: return "액티브";
                default: return "스쿼드 버프";
            }
        }

        private static string BuffLabel(CardBuffKind kind)
        {
            switch (kind)
            {
                case CardBuffKind.AttackDamage: return "공격력";
                case CardBuffKind.AttackSpeed: return "공격 속도";
                case CardBuffKind.EffectiveHealth: return "체력";
                case CardBuffKind.MoveSpeed: return "이동 속도";
                case CardBuffKind.CostRate: return "각성 회복 속도";
                case CardBuffKind.DamageVsCc: return "피해";
                default: return kind.ToString();
            }
        }

        private static string CcLabel(DcCcKind kind)
        {
            switch (kind)
            {
                case DcCcKind.Stun: return "기절";
                case DcCcKind.Impulse: return "넉백";
                case DcCcKind.Sleep: return "수면";
                default: return kind.ToString();
            }
        }

        private static string StackLabel(DcStackKind kind)
        {
            switch (kind)
            {
                case DcStackKind.Fire: return "화상";
                case DcStackKind.Ice: return "빙결";
                case DcStackKind.Bleed: return "출혈";
                case DcStackKind.Poison: return "중독";
                default: return kind.ToString();
            }
        }

        private static string SignedPercent(float value)
        {
            return value >= 0f ? $"+{Number(value)}%" : $"{Number(value)}%";
        }

        private static string Multiplier(float value) => $"x{Number(value)}";

        private static string Duration(float value) => $"{Number(value)}초";

        private static string Count(float value) => Number(value);

        private static string Number(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            if (Math.Abs(value) < 0.005f) value = 0f;
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) < 0.005f;
        }
    }
}
