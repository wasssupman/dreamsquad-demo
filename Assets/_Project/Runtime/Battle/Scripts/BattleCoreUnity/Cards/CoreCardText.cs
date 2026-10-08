using Somnia.Battle.BattleCore;
using Somnia.Battle.Data;
using Somnia.Battle.UI;

namespace Somnia.Battle.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **카드 화면의 말**.
    //
    // ① 카드 본문은 **formatter 가 이긴다**(`dc-card-text-formatter`). 옛 `DreamcatcherCardText`(648줄)를 **복사하지 않고
    //    부른다** — 그 formatter 는 아웃게임 덱 페이지·카드 상세(범위 밖, 옛 컨트롤러 그대로)도 쓰는 **단일 문안원**이고,
    //    두 벌이 되면 로비와 전투가 같은 카드를 다르게 설명한다(= 화면이 규칙을 틀리게 가르친다). 엔진·브리지 참조 0 인
    //    순수 static 이라 새 층이 불러도 옛 전투에 묶이지 않는다(unit 9 는 `Battle/`·`Bridge/` 만 지운다).
    // ② 조작 브리핑(상단 중앙 툴팁)의 문안 — 옛 `DreamcatcherCardDragSlot.ControlsFor`/`StatusFor`/`PressStatus` 그대로.
    // ③ **거절 사유 → 사람 말.** 코어가 돌려준 `RejectReason` 을 **옮겨 적기만** 한다(5b 트레이 `TextOf` 와 같은 규율) —
    //    어떤 사유가 먼저인지는 코어가 정한다. 옛 문구가 있던 사유는 옛 문구 그대로다.
    public static class CoreCardText
    {
        public const string Bad = "#FF9B8A";   // 옛 브리핑 실패색 — 딤 카드 면의 `TextDim` 과 같은 값(옛 어휘 묶음)
        public const string Good = "#9FE6A0";
        public const string Hint = "#FFD98A";

        /// <summary>`cost` = 정의표 카드 값(`CardDef.Cost`) — 액티브 문안의 비용 칸이 이 값을 보인다(U18 · 배지와 같은 숫자).</summary>
        public static string Body(DreamcatcherCard card, System.Func<string, string> unitNameOf, int cost)
            => card != null ? DreamcatcherCardText.BodyLinesOnly(card, unitNameOf, cost) : "";

        public static string Red(string s) => $"<color={Bad}>{s}</color>";
        public static string Green(string s) => $"<color={Good}>{s}</color>";

        /// <summary>
        /// 카드 커맨드의 거절 사유 → 문구. 옛 브리핑이 쓰던 네 문장(각성치 · 부착 불가 · 적 전용 · 이미 표식)은 그대로이고,
        /// 그 밖의 사유는 코어 어휘를 그대로 옮긴다(조용히 빈 줄을 내지 않는다).
        /// </summary>
        public static string RejectTextOf(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.None: return "";
                case RejectReason.InsufficientAwakening: return "각성치가 부족합니다";
                case RejectReason.CardOnCooldown: return "아직 다시 쓸 수 없습니다";
                case RejectReason.CardNotInHand: return "손패에 없는 카드입니다";
                case RejectReason.WrongCardKind: return "이 카드는 그렇게 쓸 수 없습니다";
                case RejectReason.AttachCapReached:
                case RejectReason.AttachRequirementUnmet:
                case RejectReason.NoContribution:
                case RejectReason.NotADefender:
                case RejectReason.NeedsEnemyTargeting:
                case RejectReason.NeedsDamageOutput:
                case RejectReason.NeedsHomingRoute:
                case RejectReason.NeedsTargetContext:
                case RejectReason.NeedsFallbackRange:
                    return "이 유닛에는 부착할 수 없습니다";
                case RejectReason.NotAnEnemy: return "적에게만 쓸 수 있습니다";
                case RejectReason.DuplicateState: return "이미 걸려 있습니다";
                case RejectReason.NeedsSecondCell: return "출구 타일을 탭하세요";
                case RejectReason.SameCell: return "입구와 다른 타일을 고르세요";
                case RejectReason.NotRunningOrPlacementClosed: return "전투 중에만 쓸 수 있습니다";
                case RejectReason.MatchEnded: return "판이 끝났습니다";
                case RejectReason.NoSuchEntity: return "대상이 사라졌습니다";
                default: return reason.ToString();
            }
        }

        // ── 조작 브리핑(옛 드래그 슬롯) ─────────────────────────────────────

        public static string ControlsFor(CoreCardAim aim, bool twoCells)
        {
            switch (aim)
            {
                case CoreCardAim.Defender: return "아군 유닛 위에서 놓으면 부착  ·  손패로 놓으면 취소";
                case CoreCardAim.TileAim:
                    return twoCells
                        ? "놓아서 입구 지정 → 출구 타일 탭  ·  손패로 놓으면 취소"
                        : "원하는 타일에서 놓으면 시전  ·  손패로 놓으면 취소";
                case CoreCardAim.EnemyMark: return "적 근처에서 놓으면 표식 부여  ·  손패로 놓으면 취소";
                default: return "";
            }
        }

        /// <summary>눌렀을 때의 상태 줄. 딤 사유는 두 갈래이고 다음 행동이 다르다(각성치를 모아라 / 다른 유닛을 골라라).</summary>
        public static string PressStatus(RejectReason usable, bool attachBlocked)
        {
            if (usable != RejectReason.None) return Red(RejectTextOf(usable));
            if (attachBlocked) return Red("이 유닛에는 부착할 수 없습니다");
            return "위로 끌어 올려 사용하세요";
        }

        public const string CancelHere = "여기서 놓으면 취소";
        public const string DragToAlly = "아군 유닛 위로 끌어가세요";
        public const string DropToAttach = "놓으면 이 유닛에 부착";
        public const string EnemyOnly = "적에게만 쓸 수 있습니다";
        public const string DropToMark = "놓으면 이 적에게 표식";
        public const string AlreadyMarked = "이미 표식이 있는 적입니다";
        public const string DragToTile = "타일 위로 끌어가세요";
        public const string EntrySet = "입구 지정됨";
        public const string TapExit = " — 출구 타일을 탭하세요";
        public const string DropToLink = "놓으면 여기로 연결";
        public const string DropSetsEntry = "놓으면 입구가 지정됩니다";
        public const string DropToCast = "놓으면 이 위치에 시전";
        public const string DragInstead = "이 카드는 <color=" + Hint + ">끌어서</color> 사용하세요";
    }

    /// <summary>무엇을 겨누나(옛 `DreamcatcherCardDragSlot.AimMode`). **코어의 두 칸**으로 가른다 — 종류(`CardKind`)와 `TargetsEnemies`.</summary>
    public enum CoreCardAim : byte { None = 0, Defender = 1, TileAim = 2, EnemyMark = 3 }
}
