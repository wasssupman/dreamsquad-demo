using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Core
{
    // runtime-stat-refresh Unit 1 — dev/QA-only. Fetches the balance sheets and
    // applies them to the catalog SO instances IN MEMORY (no asset writes — those
    // are editor-only APIs). Values hold for the app session; a restart reverts
    // to built values. Scene-local component, not a singleton.
    public class UnitStatRuntimeRefresher : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private EnemyCatalog enemyCatalog;
        [SerializeField] private string baseUrl = "https://dev-api-somnia.cashroyale.games/demo/google/sheet";
        [SerializeField] private string defenderSheet = "Defenders";
        [SerializeField] private string enemySheet = "Enemies";

        public bool RequestInFlight { get; private set; }

        public void Refresh(Action<string> onDone)
        {
            if (RequestInFlight)
            {
                onDone?.Invoke("refresh already in progress");
                return;
            }
            RequestInFlight = true;

            Fetch(SheetEnvelopeParser.BuildSheetUrl(baseUrl, defenderSheet), defenderBody =>
                Fetch(SheetEnvelopeParser.BuildSheetUrl(baseUrl, enemySheet), enemyBody =>
                {
                    string result;
                    try
                    {
                        result = ApplyBodies(defenderBody, enemyBody, defenderSheet, enemySheet,
                            defenderCatalog, enemyCatalog);
                    }
                    finally
                    {
                        RequestInFlight = false;
                    }
                    onDone?.Invoke(result);
                }));
        }

        // Pure string-in/string-out core so EditMode tests can drive it without a
        // network. One sheet failing still applies the healthy one (partial-update
        // philosophy); both failing returns only the error lines.
        internal static string ApplyBodies(string defenderBody, string enemyBody,
            string defenderLabel, string enemyLabel,
            DefenderCatalog defenderCatalog, EnemyCatalog enemyCatalog)
        {
            var log = new StringBuilder();
            var defenders = ParseSheet<DefenderStatDto>(defenderBody, defenderLabel, log);
            var enemies = ParseSheet<EnemyStatDto>(enemyBody, enemyLabel, log);

            if (defenders == null && enemies == null) return log.ToString();

            var payload = new UnitStatImportPayload
            {
                defenders = defenders ?? Array.Empty<DefenderStatDto>(),
                enemies = enemies ?? Array.Empty<EnemyStatDto>(),
            };
            var defendersById = UnitStatApplier.BuildIndex(
                defenderCatalog != null ? defenderCatalog.units : null, so => so.id, log, nameof(DefenderCatalog));
            var enemiesById = UnitStatApplier.BuildIndex(
                enemyCatalog != null ? enemyCatalog.units : null, so => so.id, log, nameof(EnemyCatalog));

            // in-memory only: no save callback.
            return UnitStatApplier.Apply(payload, defendersById, enemiesById, null, log);
        }

        private static T[] ParseSheet<T>(string body, string sheetLabel, StringBuilder log)
        {
            var rows = SheetEnvelopeParser.ParseSheetRows<T>(body, out string error);
            if (rows != null)
            {
                log.AppendLine($"[{sheetLabel}] {rows.Length} rows received.");
                return rows;
            }
            log.AppendLine($"[{sheetLabel}] fetch failed: {error}");
            return null;
        }

        private static void Fetch(string url, Action<string> onDone)
        {
            var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — the API returns 500 with a
                // JSON errorDetail the parser surfaces.
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                request.Dispose();
                onDone(body);
            };
        }
    }
}
