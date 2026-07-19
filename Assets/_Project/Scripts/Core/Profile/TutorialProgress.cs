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

        public static bool ShouldRunCore(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsCorePending(holder.profile);

        public static bool ShouldRunAwakeningHint(PlayerProfileSO holder) =>
            holder != null && holder.IsLoadedThisSession && IsAwakeningHintPending(holder.profile);

        public static bool IsCorePending(PlayerProfile profile) =>
            profile != null && profile.firstBattleTutorialVersion < CoreVersion;

        public static bool IsAwakeningHintPending(PlayerProfile profile) =>
            profile != null && profile.awakeningHintVersion < AwakeningHintVersion;

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

        // Tutorial replay support. This deliberately touches only tutorial
        // progress; squad, deck, account, and every other profile field remain.
        public static bool ResetAll(PlayerProfile profile)
        {
            if (profile == null) return false;
            bool changed = profile.firstBattleTutorialVersion != 0 || profile.awakeningHintVersion != 0;
            profile.firstBattleTutorialVersion = 0;
            profile.awakeningHintVersion = 0;
            return changed;
        }

        // Replay support must not deserialize and rewrite the whole profile
        // through PlayerProfile: doing so drops fields introduced by newer builds or
        // external account systems that this client model does not know yet. Patch the
        // two top-level tokens in the original JSON tree so every other token survives.
        public static string ResetAllInJson(string json, out bool changed)
        {
            var root = JObject.Parse(json);
            int core = root.Value<int?>(nameof(PlayerProfile.firstBattleTutorialVersion)) ?? 0;
            int awakening = root.Value<int?>(nameof(PlayerProfile.awakeningHintVersion)) ?? 0;
            changed = core != 0 || awakening != 0;
            root[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            root[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            return root.ToString(Formatting.Indented);
        }
    }
}
