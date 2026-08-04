using System;
using System.Text;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Core
{
    // runtime-stat-refresh Unit 1 — dev/QA-only. Fetches the balance sheets and
    // applies them to the catalog SO instances IN MEMORY (no asset writes — those
    // are editor-only APIs). Values hold for the app session; a restart reverts
    // to built values. Scene-local component, not a singleton.
    public class UnitStatRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private EnemyCatalog enemyCatalog;
        [SerializeField] private string baseUrl = "https://dev-api-somnia.cashroyale.games/demo/google/sheet";
        [SerializeField] private string defenderSheet = "Defenders";
        [SerializeField] private string enemySheet = "Enemies";

        // Test seam for the request/callback boundary. Production keeps the
        // SheetFetcher method group; EditMode tests can hold the callback until
        // the deterministic-config lock is armed.
        internal Action<string, string, Action<SheetFetcher.Result, SheetFetcher.Result>> FetchBoth
            = SheetFetcher.FetchBoth;

        public bool RequestInFlight { get; private set; }

        public void Refresh(Action<string> onDone)
        {
            if (TestModeContext.RuntimeImportsBlocked)
            {
                onDone?.Invoke(TestModeContext.RuntimeImportBlockedLog);
                return;
            }
            if (RequestInFlight)
            {
                onDone?.Invoke("refresh already in progress");
                return;
            }
            RequestInFlight = true;

            FetchBoth(
                SheetEnvelopeParser.BuildSheetUrl(baseUrl, defenderSheet),
                SheetEnvelopeParser.BuildSheetUrl(baseUrl, enemySheet),
                (defenderFetch, enemyFetch) =>
                {
                    string result;
                    try
                    {
                        result = TestModeContext.RuntimeImportsBlocked
                            ? TestModeContext.RuntimeImportBlockedLog
                            : ApplyBodies(defenderFetch.body, defenderFetch.transportError,
                                enemyFetch.body, enemyFetch.transportError,
                                defenderSheet, enemySheet, defenderCatalog, enemyCatalog);
                    }
                    finally
                    {
                        RequestInFlight = false;
                    }
                    onDone?.Invoke(result);
                });
        }

        // Pure string-in/string-out core so EditMode tests can drive it without a
        // network. One sheet failing still applies the healthy one (partial-update
        // philosophy); both failing returns only the error lines.
        internal static string ApplyBodies(string defenderBody, string defenderTransportError,
            string enemyBody, string enemyTransportError,
            string defenderLabel, string enemyLabel,
            DefenderCatalog defenderCatalog, EnemyCatalog enemyCatalog)
        {
            var log = new StringBuilder();
            var defenders = SheetEnvelopeParser.ParseSheetLogged<DefenderStatDto>(
                defenderBody, defenderTransportError, defenderLabel, log);
            var enemies = SheetEnvelopeParser.ParseSheetLogged<EnemyStatDto>(
                enemyBody, enemyTransportError, enemyLabel, log);

            var payload = UnitStatApplier.BuildPayload(defenders, enemies);
            if (payload == null) return log.ToString();

            var defendersById = UnitStatApplier.BuildIndex(
                defenderCatalog != null ? defenderCatalog.units : null, so => so.id, log, nameof(DefenderCatalog));
            var enemiesById = UnitStatApplier.BuildIndex(
                enemyCatalog != null ? enemyCatalog.units : null, so => so.id, log, nameof(EnemyCatalog));

            // in-memory only: no save callback.
            return UnitStatApplier.Apply(payload, defendersById, enemiesById, null, log);
        }
    }
}
