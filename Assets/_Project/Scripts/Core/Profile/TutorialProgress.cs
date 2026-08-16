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
        // gift-phase-removal unit 1 — 선물 워크스루는 폐지됐다(보여줄 연출이 사라졌다).
        // `PlayerProfile.giftTutorialVersion` 필드와 아래 ResetAll* 의 초기화는 **남긴다**:
        // 기존 세이브에 남은 값을 계속 정리해야 하고, JSON 하위호환도 지킨다.
        public const int LobbyIntroVersion = 1;
        // unit 11 — 로드아웃 시퀀스는 스텝 3개다: 스쿼드(이 토큰) → 드림캐쳐 덱 → (키링) →
        // 재출발 START. const 이름과 JSON 필드명은 짝이라 그대로 두고, 의미는 API 이름이 나른다.
        public const int LobbyLoadoutHintVersion = 1;
        public const int LobbyDeckHintVersion = 1;
        public const int LobbyKeyringHintVersion = 1;
        public const int LobbyStartHintVersion = 1;
        public const int GimmickRevealHintVersion = 1;
        public const int EffectTileHintVersion = 1;
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

        // outgame-tutorial unit 0 — chapter A greets the first lobby reveal.
        public static bool ShouldRunLobbyIntro(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsLobbyIntroPending(holder.profile);

        // 로드아웃 시퀀스는 스텝 4개다: 스쿼드 → 드림캐쳐 덱 → 키링 → 재출발 START.
        // 각 단계가 앞 단계의 완료를 전제하므로 동시에 pending 될 수 없고, 순서를 위한 별도
        // 상태가 필요 없다.
        //
        // unit 0 — 첫 스텝은 인게임 core 튜토리얼 완료를 전제한다. 그 플래그의 실제 의미는
        // "core 튜토리얼이 발동해 Battle 페이즈에 도달했다" 라, fail-open 경로를 탄 계정은
        // 이 시퀀스를 영영 못 본다(백로그 "챕터 B 게이트를 독립 신호로").
        public static bool ShouldRunLobbySquadHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsCorePending(holder.profile) && IsLobbySquadHintPending(holder.profile);

        // unit 11 — 2번째 스텝(드림캐쳐 덱 페이지). 선행은 스쿼드 스텝.
        public static bool ShouldRunLobbyDeckHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsLobbySquadHintPending(holder.profile) && IsLobbyDeckHintPending(holder.profile);

        // unit 6 — 3번째 스텝(로비 캐릭터 드래그). **unit 12 가 선행을 스쿼드 → 덱으로
        // 옮겼다** — 이 한 줄이 없으면 스쿼드만 끝낸 상태에서 키링이 드림캐쳐를 앞지른다.
        public static bool ShouldRunLobbyKeyringHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsLobbyDeckHintPending(holder.profile) && IsLobbyKeyringHintPending(holder.profile);

        // unit 11 — 마지막 스텝(재출발 START 포커스). 선행은 키링 스텝.
        public static bool ShouldRunLobbyStartHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsLobbyKeyringHintPending(holder.profile) && IsLobbyStartHintPending(holder.profile);

        // unit 23 — the gimmick reveal hold. Deliberately chains **nothing**: the
        // sibling gates above (`!IsCorePending`) reproduce a known defect — a player
        // who took a fail-open path on the earlier step never sees the later one (see
        // the backlog note on chapter B). No chain is needed here anyway, because the
        // reveal itself is skipped while core is pending (GimmickPhaseView gates on
        // ShouldRunCore), so there is no hold to hang a hint on during the first match.
        public static bool ShouldRunGimmickRevealHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession &&
            IsGimmickRevealHintPending(holder.profile);

        // unit 26 — 효과 타일 배치 안내. 리빌 안내와 마찬가지로 **아무것도 체인하지 않는다**
        // (형제 게이트의 fail-open 결함 — 위 주석 참조). "두 번째 판 이후" 라는 순서는
        // 컨트롤러가 `_awakeningLockedThisMatch`(= 첫 판) 로 가르고, 리빌 뒤라는 순서는
        // 페이즈 흐름(Gimmick → Placement)이 이미 보장한다.
        public static bool ShouldRunEffectTileHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession &&
            IsEffectTileHintPending(holder.profile);

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

        public static bool IsLobbyIntroPending(PlayerProfile profile) =>
            profile != null && profile.lobbyIntroVersion < LobbyIntroVersion;

        // unit 12 — 저장소는 여전히 `lobbyLoadoutHintVersion` 이다(JSON 호환). 의미만 좁아졌다.
        public static bool IsLobbySquadHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyLoadoutHintVersion < LobbyLoadoutHintVersion;

        public static bool IsLobbyKeyringHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyKeyringHintVersion < LobbyKeyringHintVersion;

        // unit 11 — 신규 두 토큰만 레거시 가드를 문다. 아래 IsLegacyLobbySequenceDone 주석 참조.
        public static bool IsLobbyDeckHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyDeckHintVersion < LobbyDeckHintVersion &&
            !IsLegacyLobbySequenceDone(profile);

        public static bool IsLobbyStartHintPending(PlayerProfile profile) =>
            profile != null && profile.lobbyStartHintVersion < LobbyStartHintVersion &&
            !IsLegacyLobbySequenceDone(profile);

        // unit 11 — 옛 로비 온보딩(챕터 B 한 덩어리 + C)을 이미 마친 계정을 알아본다.
        // 그런 계정은 신규 토큰이 0 이라 그대로 두면 `이번엔 드림캐쳐 덱 차례!` 와 재출발 안내를
        // **맥락 없이** 다시 본다 — 스쿼드 스텝은 완료라 안 뜨므로 "이번엔" 이 가리킬 앞 단계가
        // 없다.
        //
        // 이 조합은 레거시에서만 성립한다: 새 순서는 덱(B2)이 키링(C)보다 **먼저** 완료되므로
        // (ShouldRunLobbyKeyringHint 의 선행이 덱이다 — unit 12) `키링 완료 && 덱 0` 은 새
        // 진행에서 나올 수 없다. 그래서 정상 진행을 삼키지 않는다.
        //
        // 상태를 새로 저장하지 않는 **파생**이다(ShouldRunAwakeningIntro 의 "이중 상태 방지" 와
        // 같은 선택). RESET TUTORIAL 은 키링 토큰도 0 으로 만들므로 가드가 저절로 풀린다.
        // 레거시 계정이 사라지면 이 함수와 두 호출을 통째로 지울 수 있다.
        private static bool IsLegacyLobbySequenceDone(PlayerProfile profile) =>
            profile.lobbyKeyringHintVersion >= LobbyKeyringHintVersion &&
            profile.lobbyDeckHintVersion == 0 && profile.lobbyStartHintVersion == 0;

        public static bool IsGimmickRevealHintPending(PlayerProfile profile) =>
            profile != null && profile.gimmickRevealHintVersion < GimmickRevealHintVersion;

        public static bool IsEffectTileHintPending(PlayerProfile profile) =>
            profile != null && profile.effectTileHintVersion < EffectTileHintVersion;

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

        public static bool CompleteLobbyIntro(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyIntroVersion >= LobbyIntroVersion) return false;
            profile.lobbyIntroVersion = LobbyIntroVersion;
            return true;
        }

        public static bool CompleteLobbySquadHint(PlayerProfile profile)
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

        public static bool CompleteLobbyDeckHint(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyDeckHintVersion >= LobbyDeckHintVersion) return false;
            profile.lobbyDeckHintVersion = LobbyDeckHintVersion;
            return true;
        }

        public static bool CompleteLobbyStartHint(PlayerProfile profile)
        {
            if (profile == null || profile.lobbyStartHintVersion >= LobbyStartHintVersion) return false;
            profile.lobbyStartHintVersion = LobbyStartHintVersion;
            return true;
        }

        public static bool CompleteGimmickRevealHint(PlayerProfile profile)
        {
            if (profile == null || profile.gimmickRevealHintVersion >= GimmickRevealHintVersion) return false;
            profile.gimmickRevealHintVersion = GimmickRevealHintVersion;
            return true;
        }

        public static bool CompleteEffectTileHint(PlayerProfile profile)
        {
            if (profile == null || profile.effectTileHintVersion >= EffectTileHintVersion) return false;
            profile.effectTileHintVersion = EffectTileHintVersion;
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
                           profile.lobbyLoadoutHintVersion != 0 || profile.lobbyDeckHintVersion != 0 ||
                           profile.lobbyKeyringHintVersion != 0 || profile.lobbyStartHintVersion != 0 ||
                           profile.gimmickRevealHintVersion != 0 || profile.lobbyHistoryHintVersion != 0 ||
                           profile.effectTileHintVersion != 0;
            // `matchesPlayed` 는 여기 없다 — 튜토리얼 진행이 아니라 매치 이력이다(unit 8).
            // 넣으면 RESET TUTORIAL 후 챕터 D 를 보려고 두 판을 다시 뛰어야 한다.
            profile.firstBattleTutorialVersion = 0;
            profile.awakeningHintVersion = 0;
            profile.awakeningTapAttachHintVersion = 0;
            profile.giftTutorialVersion = 0;
            profile.lobbyIntroVersion = 0;
            profile.lobbyLoadoutHintVersion = 0;
            profile.lobbyDeckHintVersion = 0;
            profile.lobbyKeyringHintVersion = 0;
            profile.lobbyStartHintVersion = 0;
            profile.gimmickRevealHintVersion = 0;
            profile.lobbyHistoryHintVersion = 0;
            profile.effectTileHintVersion = 0;
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
            int lobbyDeck = root.Value<int?>(nameof(PlayerProfile.lobbyDeckHintVersion)) ?? 0;
            int lobbyKeyring = root.Value<int?>(nameof(PlayerProfile.lobbyKeyringHintVersion)) ?? 0;
            int lobbyStart = root.Value<int?>(nameof(PlayerProfile.lobbyStartHintVersion)) ?? 0;
            int gimmickReveal = root.Value<int?>(nameof(PlayerProfile.gimmickRevealHintVersion)) ?? 0;
            // `matchesPlayed` 는 읽지도 쓰지도 않는다 — 튜토리얼 진행이 아니다(unit 8).
            int lobbyHistory = root.Value<int?>(nameof(PlayerProfile.lobbyHistoryHintVersion)) ?? 0;
            int effectTile = root.Value<int?>(nameof(PlayerProfile.effectTileHintVersion)) ?? 0;
            // Every token must be in this expression: ProfileStore.ResetTutorialProgressAt
            // gates the backup and the file replacement on it, so a token that is only
            // written below would never reach disk when it is the sole difference.
            changed = core != 0 || awakening != 0 || tapAttach != 0 || gift != 0 ||
                      lobbyIntro != 0 || lobbyHint != 0 || lobbyDeck != 0 || lobbyKeyring != 0 ||
                      lobbyStart != 0 || gimmickReveal != 0 || lobbyHistory != 0 || effectTile != 0;
            root[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            root[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            root[nameof(PlayerProfile.awakeningTapAttachHintVersion)] = 0;
            root[nameof(PlayerProfile.giftTutorialVersion)] = 0;
            root[nameof(PlayerProfile.lobbyIntroVersion)] = 0;
            root[nameof(PlayerProfile.lobbyLoadoutHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyDeckHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyKeyringHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyStartHintVersion)] = 0;
            root[nameof(PlayerProfile.gimmickRevealHintVersion)] = 0;
            root[nameof(PlayerProfile.lobbyHistoryHintVersion)] = 0;
            root[nameof(PlayerProfile.effectTileHintVersion)] = 0;
            return root.ToString(Formatting.Indented);
        }
    }
}
