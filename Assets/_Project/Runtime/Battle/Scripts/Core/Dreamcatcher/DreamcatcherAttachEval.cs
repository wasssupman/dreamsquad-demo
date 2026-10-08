using System;
using Somnia.Battle.Data;
using Somnia.Battle.BattleCore.Trigger;

namespace Somnia.Battle.Core
{
    // dreamcatcher-attach-requirement — 부착 제한(`attachType` · `attachValue`)의 정적 술어 셋. host 종속 판정(옛 `WouldApply` ·
    // `DcApplicability`)은 코어 `Applicability` 로 옮겨 갔고 옛 사본은 skill-data-table 4-정리(B21)에서 지웠다.
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

        // skill-data-table 4-정리(B21) — `WouldApply`(host 종속 부착 판정의 옛 사본 · 라이브 호출 0)는 삭제했다. 정본 = 코어
        // `Applicability` · `CardBindings.Plan`. 여기 남는 것은 부착 제한(정적 술어) 셋이다.
    }
}
