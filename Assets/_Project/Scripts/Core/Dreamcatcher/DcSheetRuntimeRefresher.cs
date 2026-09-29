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
    // UnitStatRuntimeRefresher. Fetches the DC tabs (+ skill tabs) and applies them to the
    // catalog / active-card / config SO instances IN MEMORY (no asset writes —
    // editor-only API). Values hold for the app session; a restart reverts.
    // dev/QA-only; scene-local component, not a singleton.
    // skill-data-table unit 5 — 옛 DcMechanics 탭은 은퇴 · 새 두 탭 `Skills` · `SkillOwners`(`DcSheetTabs`)를 같은 fetch 로 받아
    // `SkillSheet` 하나가 효과 에셋 값과 카드 · 방어유닛 · 적의 소유 줄을 메모리에서 고친다(로그인 자동 import 도 이 경로다).
    // skill-data-table unit 8 단계 B — 카드 자식 탭 둘(DcCardEffects · DcAttackMods)은 은퇴 — 받지 않는다(`DcSheetTabs` 5탭).
    public class DcSheetRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        // Active cards are not in the deck catalog (per-match awakening cards). Wire
        // them explicitly so their DcCards rows + wrapped skills refresh too.
        [SerializeField] private DreamcatcherCard[] activeCards;
        [SerializeField] private AwakeningConfig awakeningConfig;
        // skill-data-table unit 5 — `SkillOwners` 의 방어유닛 · 적 소유자(스탯 refresher 와 같은 카탈로그).
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private EnemyCatalog enemyCatalog;
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

            var tabs = DcSheetTabs.Default();
            var urls = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
                urls[i] = SheetEnvelopeParser.BuildSheetUrl(baseUrl, tabs[i]);

            SheetFetcher.FetchAll(urls, results =>
            {
                string result;
                try
                {
                    result = ApplyBodies(results, tabs, cardCatalog, activeCards, awakeningConfig,
                        defenderCatalog != null ? defenderCatalog.units : null, enemyCatalog != null ? enemyCatalog.units : null);
                }
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
            AwakeningConfig awakeningConfig,
            DefenderUnitData[] defenders = null, AttackUnitData[] enemies = null)
        {
            var log = new StringBuilder();
            var payload = new DcSheetPayload
            {
                cards = Parse<DcCardDto>(r, tabs, DcSheetTabs.CardsAt, log),
                skills = Parse<DcSkillDto>(r, tabs, DcSheetTabs.ActiveSkillsAt, log),
                configs = Parse<DcConfigDto>(r, tabs, DcSheetTabs.ConfigAt, log),
            };
            var skillPayload = new SkillSheetPayload
            {
                skills = Parse<SkillRowDto>(r, tabs, DcSheetTabs.SkillsAt, log),
                owners = Parse<SkillOwnerRowDto>(r, tabs, DcSheetTabs.SkillOwnersAt, log),
            };
            bool dcNone = payload.cards == null && payload.skills == null && payload.configs == null;
            if (dcNone && skillPayload.skills == null && skillPayload.owners == null)
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
            string result = DcSheetApplier.Apply(payload, cardsById, skillsById, configsById, null, log);

            // skill-data-table unit 5 — 새 두 탭. 효과 · 탄 · 패턴 · 장판은 이 소유자들이 닿는 것만 안다(`SkillSheetIndex.FromOwners`).
            var skillLog = new StringBuilder();
            var index = SkillSheetIndex.FromOwners(allCards, defenders, enemies, skillLog);
            return result + SkillSheet.Import(skillPayload, index, apply: true, onApplied: null, skillLog);
        }

        private static T[] Parse<T>(SheetFetcher.Result[] r, string[] tabs, int at, StringBuilder log)
            => at < r.Length && at < tabs.Length
                ? SheetEnvelopeParser.ParseSheetLogged<T>(r[at].body, r[at].transportError, tabs[at], log)
                : null;
    }
}
