using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wassup.Core
{
    // first-session-tutorial unit 0 — pure version policy plus the loaded-session
    // guard that keeps direct BattleScene Play from overwriting profile.json.
    public static class TutorialProgress
    {
        public const int CoreVersion = 1;
        // unit 17 — 부착 안내는 **경로별로 독립**이다. 드래그(항아리 오픈)와 탭 즉발(선택
        // 오픈)은 서로 다른 조작이라 하나를 봤다고 다른 하나가 필요 없어지지 않는다.
        // 드래그 쪽 저장 위치는 기존 `awakeningHintVersion` 필드다(JSON 호환 — PlayerProfile 주석).
        public const int DragAttachHintVersion = 1;
        public const int TapAttachHintVersion = 1;
        public const int GiftTutorialVersion = 1;
        public const int LobbyIntroVersion = 1;
        public const int LobbyLoadoutHintVersion = 1;
        public const int LobbyKeyringHintVersion = 1;
        public const int GimmickRevealHintVersion = 1;
        public const int LobbyHistoryHintVersion = 1;
        // outgame-tutorial unit 9 — 챕터 D 가 요구하는 최소 매치 수. "두 번째 판 이후" 다.
        public const int HistoryHintMatchesRequired = 2;

        public static bool ShouldRunCore(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsCorePending(holder.profile);

        public static bool ShouldRunDragAttachHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsDragAttachHintPending(holder.profile);

        public static bool ShouldRunTapAttachHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsTapAttachHintPending(holder.profile);

        // unit 17 — 각성 인트로(0·A단계)는 **파생**이다. 별도 플래그로 두면 "둘 다 pending"과
        // 같은 값을 두 곳에 들게 되어 어긋날 수 있다.
        //
        // `||` 가 아니라 `&&` 인 것이 요점이다: 한쪽 경로만 쓰는 플레이어에게 `||` 는 인트로를
        // **영원히** 띄운다(이미 아는 조작을 매 판 안내하는 잔소리). 하나를 배웠으면 "덱을 여는
        // 법"은 이해한 것이므로 인트로의 할 일은 끝났다 — 못 배운 나머지 한쪽은 그 경로를
        // 처음 쓰는 순간 자기 안내가 뜬다.
        public static bool ShouldRunAwakeningIntro(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession &&
            IsDragAttachHintPending(holder.profile) && IsTapAttachHintPending(holder.profile);

        // unit 6 — the gift walkthrough runs on the first battle where the gift
        // presentation is actually visible: core must be complete (the first run
        // suppresses the presentation entirely), so this can never be true while
        // ShouldRunCore is true for the same holder.
        public static bool ShouldRunGiftTutorial(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsCorePending(holder.profile) && IsGiftTutorialPending(holder.profile);

        // outgame-tutorial unit 0 — chapter A greets the first lobby reveal.
        public static bool ShouldRunLobbyIntro(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsLobbyIntroPending(holder.profile);

        // unit 0 — chapter B requires the in-game core tutorial to be complete, so
        // A and B can never be pending at the same time and the order needs no extra
        // state (same shape as ShouldRunGiftTutorial). Note the real meaning of that
        // flag is "the core tutorial ran and reached the Battle phase" — a player who
        // took its fail-open path never sees chapter B. See spec unit 3.
        public static bool ShouldRunLobbyLoadoutHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsCorePending(holder.profile) && IsLobbyLoadoutHintPending(holder.profile);

        // unit 6 — chapter C requires chapter B to be complete, so A·B·C can never be
        // pending at the same time and the A → B → C order needs no extra state (same
        // shape as ShouldRunLobbyLoadoutHint). B in turn requires the in-game core
        // tutorial, so this transitively sits behind that too.
        public static bool ShouldRunLobbyKeyringHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsLobbyLoadoutHintPending(holder.profile) && IsLobbyKeyringHintPending(holder.profile);

        // unit 23 — the gimmick reveal hold. Deliberately chains **nothing**: the
        // sibling gates above (`!IsCorePending`) reproduce a known defect — a player
        // who took a fail-open path on the earlier step never sees the later one (see
        // the backlog note on chapter B). No chain is needed here anyway, because the
        // reveal itself is skipped while core is pending (GimmickPhaseView gates on
        // ShouldRunCore), so there is no hold to hang a hint on during the first match.
        public static bool ShouldRunGimmickRevealHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession &&
            IsGimmickRevealHintPending(holder.profile);

        // unit 9 — 챕터 D. 형제 로비 챕터와 달리 **앞 챕터 완료를 체인하지 않는다**:
        // 게이트는 `matchesPlayed` 라는 독립 신호다(백로그 "챕터 게이트를 독립 신호로" 참조).
        //
        // 계정 조건(`UserSession.HasAccount` — 히스토리 버튼은 게스트에게 숨겨진다)은 여기 넣지
        // 않는다. 진행 정책 순수 함수에 세션/네트워크 상태를 끌어들이면 EditMode 테스트가 전역
        // 상태에 묶인다 — 그 조건은 컨트롤러가 건다.
        public static bool ShouldRunLobbyHistoryHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            holder.profile.matchesPlayed >= HistoryHintMatchesRequired &&
            IsLobbyHistoryHintPending(holder.profile);

        public static bool IsCorePending(PlayerProfile profile) =>
            profile != null && profile.firstBattleTutorialVersion < CoreVersion;

        public static bool IsDragAttachHintPending(PlayerProfile profile) =>
            profile != null && profile.awakeningHintVersion < DragAttachHintVersion;

        public static bool IsTapAttachHintPending(PlayerProfile profile) =>
            profile != null && profile.awakeningTapAttachHintVersion < TapAttachHintVersion;

        public static bool IsGiftTutorialPending(PlayerProfile profile) =>
            profile != null && profile.giftTutorialVersion < GiftTutorialVersion;

        public static bool IsLobbyIntroPending(PlayerProfile profile) =>
            profile != null && profile.lobbyIntroVersion < LobbyIntroVersion;

        public static bool IsLobbyLoadoutHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyLoadoutHintVersion < LobbyLoadoutHintVersion;

        public static bool IsLobbyKeyringHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyKeyringHintVersion < LobbyKeyringHintVersion;

        public static bool IsGimmickRevealHintPending(PlayerProfile profile) =>
            profile != null && profile.gimmickRevealHintVersion < GimmickRevealHintVersion;

        public static bool IsLobbyHistoryHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyHistoryHintVersion < LobbyHistoryHintVersion;

        public static bool CompleteCore(PlayerProfile profile)
        {
            if (profile == null || profile.firstBattleTutorialVersion >= CoreVersion) return false;
            profile.firstBattleTutorialVersion = CoreVersion;
            return true;
        }

        public static bool CompleteDragAttachHint(PlayerProfile profile)
        {
            if (profile == null || profile.awakeningHintVersion >= DragAttachHintVersion) return false;
            profile.awakeningHintVersion = DragAttachHintVersion;
            return true;
        }

        public static bool CompleteTapAttachHint(PlayerProfile profile)
        {
            if (profile == null || profile.awakeningTapAttachHintVersion >= TapAttachHintVersion) return false;
            profile.awakeningTapAttachHintVersion = TapAttachHintVersion;
            return true;
        }

        public static bool CompleteGiftTutorial(PlayerProfile profile)
        {
            if (profile == null || profile.giftTutorialVersion >= GiftTutorialVersion) return false;
            profile.giftTutorialVersion = GiftTutorialVersion;
            return true;
        }

        public static bool CompleteLobbyIntro(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyIntroVersion >= LobbyIntroVersion) return false;
            profile.lobbyIntroVersion = LobbyIntroVersion;
            return true;
        }

        public static bool CompleteLobbyLoadoutHint(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyLoadoutHintVersion >= LobbyLoadoutHintVersion) return false;
            profile.lobbyLoadoutHintVersion = LobbyLoadoutHintVersion;
            return true;
        }

        public static bool CompleteLobbyKeyringHint(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyKeyringHintVersion >= LobbyKeyringHintVersion) return false;
            profile.lobbyKeyringHintVersion = LobbyKeyringHintVersion;
            return true;
        }

        public static bool CompleteGimmickRevealHint(PlayerProfile profile)
        {
            if (profile == null || profile.gimmickRevealHintVersion >= GimmickRevealHintVersion) return false;
            profile.gimmickRevealHintVersion = GimmickRevealHintVersion;
            return true;
        }

        public static bool CompleteLobbyHistoryHint(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyHistoryHintVersion >= LobbyHistoryHintVersion) return false;
            profile.lobbyHistoryHintVersion = LobbyHistoryHintVersion;
            return true;
        }

        // Tutorial replay support. This deliberately touches only tutorial
        // progress; squad, deck, account, and every other profile field remain.
        public static bool ResetAll(PlayerProfile profile)
        {
            if (profile == null) return false;
            // 신규 토큰은 ResetAllInJson 의 `changed` 표현식에도 넣어야 한다 — 이유는 그쪽 주석.
            bool changed = profile.firstBattleTutorialVersion != 0 || profile.awakeningHintVersion != 0 ||
                           profile.awakeningTapAttachHintVersion != 0 ||
                           profile.giftTutorialVersion != 0 || profile.lobbyIntroVersion != 0 ||
                           profile.lobbyLoadoutHintVersion != 0 || profile.lobbyKeyringHintVersion != 0 ||
                           profile.gimmickRevealHintVersion != 0 || profile.lobbyHistoryHintVersion != 0;
            // `matchesPlayed` 는 여기 없다 — 튜토리얼 진행이 아니라 매치 이력이다(unit 8).
            // 넣으면 RESET TUTORIAL 후 챕터 D 를 보려고 두 판을 다시 뛰어야 한다.
            profile.firstBattleTutorialVersion = 0;
            profile.awakeningHintVersion = 0;
            profile.awakeningTapAttachHintVersion = 0;
            profile.giftTutorialVersion = 0;
            profile.lobbyIntroVersion = 0;
            profile.lobbyLoadoutHintVersion = 0;
            profile.lobbyKeyringHintVersion = 0;
            profile.gimmickRevealHintVersion = 0;
            profile.lobbyHistoryHintVersion = 0;
            return changed;
        }

        // Replay support must not deserialize and rewrite the whole profile
        // through PlayerProfile: doing so drops fields introduced by newer builds or
        // external account systems that this client model does not know yet. Patch the
        // tutorial version tokens in the original JSON tree so every other token survives.
        public static string ResetAllInJson(string json, out bool changed)
        {
            var root = JObject.Parse(json);
            int core = root.Value<int?>(nameof(PlayerProfile.firstBattleTutorialVersion)) ?? 0;
            int awakening = root.Value<int?>(nameof(PlayerProfile.awakeningHintVersion)) ?? 0;
            int tapAttach = root.Value<int?>(nameof(PlayerProfile.awakeningTapAttachHintVersion)) ?? 0;
            int gift = root.Value<int?>(nameof(PlayerProfile.giftTutorialVersion)) ?? 0;
            int lobbyIntro = root.Value<int?>(nameof(PlayerProfile.lobbyIntroVersion)) ?? 0;
            int lobbyHint = root.Value<int?>(nameof(PlayerProfile.lobbyLoadoutHintVersion)) ?? 0;
            int lobbyKeyring = root.Value<int?>(nameof(PlayerProfile.lobbyKeyringHintVersion)) ?? 0;
            int gimmickReveal = root.Value<int?>(nameof(PlayerProfile.gimmickRevealHintVersion)) ?? 0;
            // `matchesPlayed` 는 읽지도 쓰지도 않는다 — 튜토리얼 진행이 아니다(unit 8).
            int lobbyHistory = root.Value<int?>(nameof(PlayerProfile.lobbyHistoryHintVersion)) ?? 0;
            // Every token must be in this expression: ProfileStore.ResetTutorialProgressAt
            // gates the backup and the file replacement on it, so a token that is only
            // written below would never reach disk when it is the sole difference.
            changed = core != 0 || awakening != 0 || tapAttach != 0 || gift != 0 ||
                      lobbyIntro != 0 || lobbyHint != 0 || lobbyKeyring != 0 || gimmickReveal != 0 ||
                      lobbyHistory != 0;
            root[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            root[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            root[nameof(PlayerProfile.awakeningTapAttachHintVersion)] = 0;
            root[nameof(PlayerProfile.giftTutorialVersion)] = 0;
            root[nameof(PlayerProfile.lobbyIntroVersion)] = 0;
            root[nameof(PlayerProfile.lobbyLoadoutHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyKeyringHintVersion)] = 0;
            root[nameof(PlayerProfile.gimmickRevealHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyHistoryHintVersion)] = 0;
            return root.ToString(Formatting.Indented);
        }
    }
}
