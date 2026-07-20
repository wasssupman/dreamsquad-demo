using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wassup.Core
{
    // first-session-tutorial unit 0 — pure version policy plus the loaded-session
    // guard that keeps direct BattleScene Play from overwriting profile.json.
    public static class TutorialProgress
    {
        public const int CoreVersion = 1;
        public const int AwakeningHintVersion = 1;
        public const int GiftTutorialVersion = 1;

        public static bool ShouldRunCore(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsCorePending(holder.profile);

        public static bool ShouldRunAwakeningHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsAwakeningHintPending(holder.profile);

        // unit 6 — the gift walkthrough runs on the first battle where the gift
        // presentation is actually visible: core must be complete (the first run
        // suppresses the presentation entirely), so this can never be true while
        // ShouldRunCore is true for the same holder.
        public static bool ShouldRunGiftTutorial(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && holder.profile != null &&
            !IsCorePending(holder.profile) && IsGiftTutorialPending(holder.profile);

        public static bool IsCorePending(PlayerProfile profile) =>
            profile != null && profile.firstBattleTutorialVersion < CoreVersion;

        public static bool IsAwakeningHintPending(PlayerProfile profile) =>
            profile != null && profile.awakeningHintVersion < AwakeningHintVersion;

        public static bool IsGiftTutorialPending(PlayerProfile profile) =>
            profile != null && profile.giftTutorialVersion < GiftTutorialVersion;

        public static bool CompleteCore(PlayerProfile profile)
        {
            if (profile == null || profile.firstBattleTutorialVersion >= CoreVersion) return false;
            profile.firstBattleTutorialVersion = CoreVersion;
            return true;
        }

        public static bool CompleteAwakeningHint(PlayerProfile profile)
        {
            if (profile == null || profile.awakeningHintVersion >= AwakeningHintVersion) return false;
            profile.awakeningHintVersion = AwakeningHintVersion;
            return true;
        }

        public static bool CompleteGiftTutorial(PlayerProfile profile)
        {
            if (profile == null || profile.giftTutorialVersion >= GiftTutorialVersion) return false;
            profile.giftTutorialVersion = GiftTutorialVersion;
            return true;
        }

        // Tutorial replay support. This deliberately touches only tutorial
        // progress; squad, deck, account, and every other profile field remain.
        public static bool ResetAll(PlayerProfile profile)
        {
            if (profile == null) return false;
            bool changed = profile.firstBattleTutorialVersion != 0 || profile.awakeningHintVersion != 0 ||
                           profile.giftTutorialVersion != 0;
            profile.firstBattleTutorialVersion = 0;
            profile.awakeningHintVersion = 0;
            profile.giftTutorialVersion = 0;
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
            int gift = root.Value<int?>(nameof(PlayerProfile.giftTutorialVersion)) ?? 0;
            changed = core != 0 || awakening != 0 || gift != 0;
            root[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            root[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            root[nameof(PlayerProfile.giftTutorialVersion)] = 0;
            return root.ToString(Formatting.Indented);
        }
    }
}
