using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Core
{
    // runtime-stat-refresh unit 3 — dreamcatcher counterpart of
    // UnitStatRuntimeRefresher. Fetches the 6 DC tabs and applies them to the
    // catalog / active-card / config SO instances IN MEMORY (no asset writes —
    // editor-only API). Values hold for the app session; a restart reverts.
    // dev/QA-only; scene-local component, not a singleton.
    public class DcSheetRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        // DC tab names are contract-fixed (dreamcatcher-sheet-sync 0_json_schema).
        private static readonly string[] Tabs =
            { "DcCards", "DcCardEffects", "DcMechanics", "DcAttackMods", "DcSkills", "DcConfig" };

        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        // Active cards are not in the deck catalog (per-match awakening cards). Wire
        // them explicitly so their DcCards rows + wrapped skills refresh too.
        [SerializeField] private DreamcatcherCard[] activeCards;
        [SerializeField] private AwakeningConfig awakeningConfig;
        [SerializeField] private string baseUrl = "https://dev-api-somnia.cashroyale.games/demo/google/sheet";

        public bool RequestInFlight { get; private set; }

        public void Refresh(Action<string> onDone)
        {
            if (RequestInFlight)
            {
                onDone?.Invoke("refresh already in progress");
                return;
            }
            RequestInFlight = true;

            var urls = new string[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
                urls[i] = SheetEnvelopeParser.BuildSheetUrl(baseUrl, Tabs[i]);

            SheetFetcher.FetchAll(urls, results =>
            {
                string result;
                try { result = ApplyBodies(results, Tabs, cardCatalog, activeCards, awakeningConfig); }
                catch (Exception e) { result = $"Refresh failed: {e}"; }
                finally { RequestInFlight = false; }
                onDone?.Invoke(result);
            });
        }

        // Pure results-in/string-out core so EditMode tests can drive it without a
        // network. Mirrors the editor ApplyDcFetched but enumerates SOs from the
        // catalog / explicit refs instead of an AssetDatabase scan, and passes a
        // null save callback (in-memory only).
        internal static string ApplyBodies(SheetFetcher.Result[] r, string[] tabs,
            DreamcatcherCardCatalog cardCatalog, DreamcatcherCard[] activeCards,
            AwakeningConfig awakeningConfig)
        {
            var log = new StringBuilder();
            var payload = new DcSheetPayload
            {
                cards = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(r[0].body, r[0].transportError, tabs[0], log),
                cardEffects = SheetEnvelopeParser.ParseSheetLogged<DcCardEffectDto>(r[1].body, r[1].transportError, tabs[1], log),
                mechanics = SheetEnvelopeParser.ParseSheetLogged<DcMechanicDto>(r[2].body, r[2].transportError, tabs[2], log),
                attackMods = SheetEnvelopeParser.ParseSheetLogged<DcAttackModDto>(r[3].body, r[3].transportError, tabs[3], log),
                skills = SheetEnvelopeParser.ParseSheetLogged<DcSkillDto>(r[4].body, r[4].transportError, tabs[4], log),
                configs = SheetEnvelopeParser.ParseSheetLogged<DcConfigDto>(r[5].body, r[5].transportError, tabs[5], log),
            };
            if (payload.cards == null && payload.cardEffects == null && payload.mechanics == null
                && payload.attackMods == null && payload.skills == null && payload.configs == null)
                return log.ToString();

            var allCards = ((cardCatalog != null ? cardCatalog.cards : null) ?? Array.Empty<DreamcatcherCard>())
                .Concat(activeCards ?? Array.Empty<DreamcatcherCard>());
            var cardsById = UnitStatApplier.BuildIndex(allCards, so => so.id, log, nameof(DreamcatcherCard));

            var skills = (activeCards ?? Array.Empty<DreamcatcherCard>())
                .Select(c => c != null ? c.skill : null).Where(s => s != null);
            var skillsById = UnitStatApplier.BuildIndex(skills, so => so.id, log, nameof(SkillData));

            var configsById = new Dictionary<string, ScriptableObject>();
            if (awakeningConfig != null && !string.IsNullOrEmpty(awakeningConfig.id))
                configsById[awakeningConfig.id] = awakeningConfig;
            var rule = cardCatalog != null ? cardCatalog.ruleConfig : null;
            if (rule != null && !string.IsNullOrEmpty(rule.id))
                configsById[rule.id] = rule;

            // in-memory only: no save callback.
            return DcSheetApplier.Apply(payload, cardsById, skillsById, configsById, null, log);
        }
    }
}
