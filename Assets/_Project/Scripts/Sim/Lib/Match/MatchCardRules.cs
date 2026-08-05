using Wassup.Core.Session;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 16-C — 드림캐쳐 카드 커밋의 **적법성 판정 단일 지점**.
    ///
    /// 적출 전에는 같은 판정이 네 군데에 흩어져 있었다: `TryGetUsable`·`TryGetUsableAttach`·
    /// `TryGetUsableActive` 가 거의 같은 3~4조건을 각자 복제했고, 유출 선불 가능성과 부착 캡은
    /// `CommitAttach` 본문에 따로 있었다. 넷 다 `bool` 만 돌려줘서 **거절 사유 8종이 `false` 하나로
    /// 접혔고**, UI 는 그것을 preflight 로 다시 계산했다(청사진 ① §3).
    ///
    /// **순수 함수**다 — plain 값 in / 사유 out. `DreamcatcherCard`(SO)·`Entity`·덱을 모른다.
    /// 그래서 `Wassup.Sim` 안에 산다(unit 17 게이트: 여기서 `using UnityEngine` 은 컴파일 에러).
    ///
    /// **새 사유 enum 을 만들지 않았다.** `CommandReject` 가 이미 `Card_*` 5종을 갖고 있고 unit 17
    /// 에서 같은 어셈블리로 졸업했으므로, 규칙이 그것을 직접 돌려주면 된다. `PlacementRejectReason`
    /// 처럼 전용 enum + 매핑을 두는 형태는 그쪽이 뷰 소비자를 이미 갖고 있어서 생긴 것이지 규범이
    /// 아니다 — 여기서 흉내내면 1:1 매핑 계층만 늘어난다(제약 8).
    ///
    /// **판정 순서가 계약이다.** 적출 전 순서를 그대로 옮겼다 — 특히 게이지가 스킬 배선보다
    /// **앞**이다(Active 카드가 둘 다 실패하면 이전에도 게이지 사유가 나왔다).
    /// </summary>
    public static class MatchCardRules
    {
        /// <summary>
        /// 호출자만 아는 것을 풀어서 담는 입력. 필드가 많은 것은 판정이 실제로 그만큼을 보기
        /// 때문이고, 그 전에는 이것이 네 함수에 흩어져 있었다.
        /// </summary>
        public struct CommitInputs
        {
            /// 덱(큐 또는 부착)에 그 entryId 가 있는가.
            public bool CardExists;
            /// **손패 앞 N칸**에 있는가. `CardExists` 와 다른 조건이다 — 이 둘이 어긋나서
            /// 부분 커밋 구멍이 났었다(`2d4fab98`).
            public bool InHand;
            /// 커밋 경로가 기대하는 카드 종류인가(Active 경로 ↔ 부착 경로).
            public bool TypeMatches;
            /// Active 카드의 skill 이 배선됐는가. **Active 가 아니면 true 로 넘긴다.**
            public bool SkillWired;

            public int Gauge;
            public int Cost;

            /// 잔여 유출 허용치. `LeakCost == 0` 이면 보지 않는다.
            public int LeakRemaining;
            public int LeakCost;

            /// 이 host 에 이미 붙은 개수. `AttachCap <= 0` = **캡 미적용**(적 표식 경로).
            public int AttachedToHost;
            public int AttachCap;
        }

        /// <summary>
        /// `CommandReject.None` 이면 커밋해도 된다.
        ///
        /// 유출 선불이 "지불 후 잔여 &lt; 1" 이면 거절하는 것은 **지불로 즉시 패배하는 것을 구조적으로
        /// 금지**하기 때문이다(`MatchOutcomeRules.TryPayLeakAllowance` 와 같은 부등식 — 두 곳이
        /// 같은 규칙을 보는 것이 아니라, 여기는 *사전* 게이트고 저기는 *지불* 이다).
        ///
        /// Active 카드의 skill 미배선은 시트/배선 버그라 `Session_InternalError` 다 — 플레이어가
        /// 고칠 수 있는 거절이 아니므로 `Card_*` 로 뭉뚱그리면 진단이 사라진다.
        /// </summary>
        public static CommandReject Check(in CommitInputs c)
        {
            if (!c.CardExists || !c.InHand) return CommandReject.Card_NotInHand;
            if (!c.TypeMatches) return CommandReject.Card_WrongType;
            if (c.Gauge < c.Cost) return CommandReject.Card_InsufficientGauge;
            if (!c.SkillWired) return CommandReject.Session_InternalError;
            if (c.LeakCost > 0 && c.LeakRemaining - c.LeakCost < 1)
                return CommandReject.Card_LeakAllowanceTooLow;
            if (c.AttachCap > 0 && c.AttachedToHost >= c.AttachCap)
                return CommandReject.Card_AttachCapReached;
            return CommandReject.None;
        }
    }
}
