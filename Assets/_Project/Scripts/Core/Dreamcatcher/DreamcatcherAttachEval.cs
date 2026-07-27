using System;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-attach-lockon — 부착 조준 유효성 preflight 의 순수 판정.
    // "이 Unit 카드가 이 유닛에 '기여'하는가"(= ApplyDreamcatcherCardToUnit 이 -1 이
    // 아닌가)를, **유닛-종속 게이트만** 미러해 plain 값으로 판정한다.
    //
    // attack-decoupling unit 1 — host 종속 판정의 source of truth 는 이제
    // `DcApplicability` **한 곳**이다. 이 클래스와 커밋 경로
    // (ApplyDreamcatcherCardToUnit)가 같은 함수를 호출하므로, 예전의 "★ 동기화 계약"
    // (두 미러를 손으로 맞추기)은 폐기됐다 — 그 부채가 통통구슬×머신거너 같은
    // "붙는데 무효" 조합의 원인이었다.
    //
    // 여전히 여기 남는 것: **카드 단위 해석**(어느 메커닉 하나라도 발동하면 기여) +
    // 카드 데이터 검증(magnitude·duration·projectile-null·attachType). 후자는 어느
    // 유닛에서든 같은 결과라 host 판정과 레이어가 다르다.
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
            switch (card.attachType)
            {
                case DcAttachType.None:
                    return true;
                case DcAttachType.Class:
                    return TryParseAttachClass(card.attachValue, out var cls) && hostRole == cls;
                case DcAttachType.UnitId:
                    // id 는 저장 키라 ordinal — 대소문자가 다르면 다른 유닛이다.
                    return !string.IsNullOrEmpty(card.attachValue)
                        && string.Equals(hostUnitId, card.attachValue, StringComparison.Ordinal);
                default:
                    return false; // 미래 type append 시 배선 전까지 안전 기본값
            }
        }

        // unit 7 rev — attachValue 를 DefenderClass 로 읽는 단일 지점. 판정·무효검사·
        // 문안·validator 가 모두 이걸 쓰므로 "무엇이 유효한 클래스 값인가"가 한 곳에 있다.
        //
        // 대소문자는 무시한다(시트에 손으로 적는 값이고 DefenderClass 이름끼리 대소문자만
        // 다른 쌍이 없다). 단 Enum.TryParse 는 "1" 같은 숫자 문자열도 통과시키므로 이름
        // 왕복으로 배제한다 — 시트에 숫자를 적으면 조용히 엉뚱한 클래스가 되는 걸 막는다.
        // None 은 제한으로서 무의미하므로 실패로 취급(fail-closed).
        public static bool TryParseAttachClass(string value, out DefenderClass cls)
        {
            cls = DefenderClass.None;
            if (string.IsNullOrEmpty(value)) return false;
            if (!Enum.TryParse(value, ignoreCase: true, out cls)) { cls = DefenderClass.None; return false; }
            if (!cls.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
            { cls = DefenderClass.None; return false; }
            return cls != DefenderClass.None;
        }

        // unit 1·3 공유 — "제한이 설정됐지만 값이 비어 무의미한가"(= fail-closed 사유).
        // 브리지의 별도 경고 문구(unit 1)와 에디터 validator(unit 3)가 같은 정의를 쓴다.
        // 제한 불일치(정상 거절)와 데이터 실수를 구분하는 것이 목적.
        public static bool HasInvalidAttachRequirement(DreamcatcherCard card)
        {
            if (card == null) return false;
            switch (card.attachType)
            {
                case DcAttachType.Class:
                    // 빈 값 · 알 수 없는 이름 · None — 전부 "어디에도 안 붙는" 설정이다.
                    return !TryParseAttachClass(card.attachValue, out _);
                case DcAttachType.UnitId:
                    return string.IsNullOrEmpty(card.attachValue);
                default:
                    return false;
            }
        }

        // attack-decoupling unit 1 — host 종속 판정은 전부 DcApplicability 로 위임한다.
        // 이 함수에 남는 것은 **카드 단위 해석**뿐: "메커닉/모드 중 하나라도 이 host 에서
        // 발동하면 카드가 기여한다"(spec 계약 4 — 판정 단위는 메커닉, 전량 무효일 때만
        // 카드 거절). host 속성이 profile 하나로 접혀 새 속성이 생겨도 시그니처가
        // 흔들리지 않는다.
        public static bool WouldApply(DreamcatcherCard card, in DcHostProfile host)
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
                // 이중 상태 거부만 카드 '전체' 거부다(apply preflight 가 -1 을 반환하는
                // 유일한 host 사유 — 부분 적용이 원래 상태를 리셋하기 때문).
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var m = card.mechanics[i];
                    if (DcApplicability.EvaluateMechanic(m.payload.kind, m.trigger.kind, host)
                        == DcRejectReason.DuplicateState) return false;
                }
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var m = card.mechanics[i];
                    if (m.payload.kind == DcPayloadKind.None) continue;
                    if (DcApplicability.EvaluateMechanic(m.payload.kind, m.trigger.kind, host)
                        == DcRejectReason.None) return true;
                }
            }

            if (hasMods)
            {
                for (int i = 0; i < card.attackMods.Length; i++)
                {
                    var am = card.attackMods[i];
                    // 카드 데이터 검증(kind/damageMul/count)은 host 무관이라 여기 남는다.
                    if (am.kind == DcAttackModKind.None || am.damageMul <= 0f) continue;
                    if (am.kind == DcAttackModKind.ProjectileBounce && am.count <= 0) continue;
                    if (DcApplicability.EvaluateAttackMod(am.kind, host) == DcRejectReason.None) return true;
                }
            }

            return false;
        }
    }
}
