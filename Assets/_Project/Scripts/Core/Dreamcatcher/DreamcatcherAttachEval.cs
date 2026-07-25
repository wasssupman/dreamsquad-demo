using System;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-attach-lockon — 부착 조준 유효성 preflight 의 순수 판정.
    // "이 Unit 카드가 이 유닛에 '기여'하는가"(= ApplyDreamcatcherCardToUnit 이 -1 이
    // 아닌가)를, **유닛-종속 게이트만** 미러해 plain 값으로 판정한다.
    //
    // ApplyDreamcatcherCardToUnit(BattleBridge.Dreamcatcher.cs)의 유닛-종속 skip 은
    // 딱 셋 — ProjectileBounce→ProjectileRef(투사체) / FrontmostTarget·HeavyStrike→
    // 데미지 output / 이중 LethalTimer·DreamCocoon(상태). 나머지 guard(magnitude·
    // duration·projectile-null·mapping)는 **카드 데이터 검증**이라 어느 유닛에서든 같은
    // 결과 → 여기선 유닛 종속 조건만 본다(authored-valid 전제). 그래서 UI 답이 '유닛별로'
    // 정확하다(같은 카드가 궁수엔 가능·가디언엔 불가).
    //
    // ★ 동기화 계약: 새 **유닛-클래스 게이트** kind 를 apply 에 추가하면 여기도 갱신 +
    //   DreamcatcherAttachEvalTests 케이스 추가. 데이터-검증 guard 추가는 무관.
    public static class DreamcatcherAttachEval
    {
        // dreamcatcher-attach-requirement unit 0 — 부착 대상 제한(정적 술어) 판정.
        // WouldApply 와 합치지 않고 별도 함수로 둔 이유: 커밋 경로
        // (ApplyDreamcatcherCardToUnit)는 WouldApply 를 부르지 않고 자체 preflight 체인을
        // 쓰므로, 두 소비처(UI attachable 스냅샷 · 커밋 preflight)가 각각 이 함수를 직접
        // 호출한다. WouldApply 에 인자를 늘리면 Squad 조기 return 호출처가 절대 읽지 않는
        // 더미를 넘겨야 한다.
        //
        // bake/UI 시점 전용 — per-frame 호출 금지(managed SO 필드 읽기, mechanics 규율 동일).
        // 무효 설정(Class×None / UnitId×빈문자열)은 fail-closed(false): 제한이 조용히
        // 풀리는 것보다 카드가 눈에 띄게 안 붙는 쪽을 택한다.
        public static bool MeetsAttachRequirement(DreamcatcherCard card,
            DefenderClass hostRole, string hostUnitId)
        {
            if (card == null) return false;
            switch (card.attachRequire)
            {
                case DcAttachRequireKind.None:
                    return true;
                case DcAttachRequireKind.Class:
                    return card.attachRequireClass != DefenderClass.None
                        && hostRole == card.attachRequireClass;
                case DcAttachRequireKind.UnitId:
                    return !string.IsNullOrEmpty(card.attachRequireUnitId)
                        && string.Equals(hostUnitId, card.attachRequireUnitId, StringComparison.Ordinal);
                default:
                    return false; // 미래 kind append 시 배선 전까지 안전 기본값
            }
        }

        // unit 1·3 공유 — "제한이 설정됐지만 값이 비어 무의미한가"(= fail-closed 사유).
        // 브리지의 별도 경고 문구(unit 1)와 에디터 validator(unit 3)가 같은 정의를 쓴다.
        // 제한 불일치(정상 거절)와 데이터 실수를 구분하는 것이 목적.
        public static bool HasInvalidAttachRequirement(DreamcatcherCard card)
        {
            if (card == null) return false;
            switch (card.attachRequire)
            {
                case DcAttachRequireKind.Class:
                    return card.attachRequireClass == DefenderClass.None;
                case DcAttachRequireKind.UnitId:
                    return string.IsNullOrEmpty(card.attachRequireUnitId);
                default:
                    return false;
            }
        }

        public static bool WouldApply(DreamcatcherCard card,
            bool hasProjectile, bool hasDamageOutput, bool hasLethalTimer, bool hasDreamCocoon)
        {
            if (card == null) return false;
            // Squad = 축-집합 버프(host 무제약, unit 9) → host 종속 거부 없음. Active 는 이 경로 밖.
            if (card.type == CardType.Squad) return true;
            if (card.type != CardType.Unit) return false;

            bool hasMech = card.mechanics != null && card.mechanics.Length > 0;
            bool hasMods = card.attackMods != null && card.attackMods.Length > 0;
            if (!hasMech && !hasMods) return false;

            if (hasMech)
            {
                // 이중 상태 거부 = 카드 '전체' 거부(apply preflight, return -1).
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var pk = card.mechanics[i].payload.kind;
                    if (hasLethalTimer && pk == DcPayloadKind.SelfBuffLethal) return false;
                    if (hasDreamCocoon && pk == DcPayloadKind.DreamCocoon) return false;
                }
                // 하나라도 이 유닛에 먹는 mechanic 이 있으면 기여.
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var pk = card.mechanics[i].payload.kind;
                    if (pk == DcPayloadKind.None) continue;
                    if (pk == DcPayloadKind.HeavyStrike) { if (hasDamageOutput) return true; continue; }
                    return true; // 그 외 mechanic 은 유닛 클래스 무관(데미지/투사체 불요)
                }
            }

            if (hasMods)
            {
                for (int i = 0; i < card.attackMods.Length; i++)
                {
                    var am = card.attackMods[i];
                    if (am.kind == DcAttackModKind.None || am.damageMul <= 0f) continue;
                    if (am.kind == DcAttackModKind.ProjectileBounce)
                    {
                        if (am.count > 0 && hasProjectile) return true; // 통통구슬 — 투사체 유닛만
                        continue;
                    }
                    if (am.kind == DcAttackModKind.FrontmostTarget)
                    {
                        if (hasDamageOutput) return true; // 끝을 보는 눈 — 데미지 output 필요
                        continue;
                    }
                    return true; // 그 외 mod(damageMul>0)는 유닛 클래스 무관
                }
            }

            return false;
        }
    }
}
