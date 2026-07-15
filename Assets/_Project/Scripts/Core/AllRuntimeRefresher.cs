using System;
using System.Text;
using UnityEngine;

namespace Wassup.Core
{
    // runtime-stat-refresh unit 6 — composite refresher so one lobby button pulls
    // every sheet tab (Defenders/Enemies + the 6 DC tabs). Children run
    // concurrently (they are independent) and the join counter needs no lock:
    // their callbacks land on the main thread — same shape as SheetFetcher.FetchAll.
    // Implements IRuntimeRefresher itself, so StatRefreshButtonView drives it like
    // any other refresher and the view stays untouched.
    public class AllRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        // Unity can't serialize interface refs — take MonoBehaviours and cast at
        // use time, same as StatRefreshButtonView.refresherSource.
        [SerializeField] private MonoBehaviour[] refresherSources;

        public bool RequestInFlight { get; private set; }

        public void Refresh(Action<string> onDone)
        {
            if (RequestInFlight)
            {
                onDone?.Invoke("refresh already in progress");
                return;
            }
            RefreshAll(Resolve(), onDone);
        }

        // Non-IRuntimeRefresher entries are dropped with a warning rather than
        // failing the whole refresh — a mis-wired slot must not lock QA out of the
        // tabs that are wired correctly.
        private IRuntimeRefresher[] Resolve()
        {
            if (refresherSources == null) return Array.Empty<IRuntimeRefresher>();
            var resolved = new System.Collections.Generic.List<IRuntimeRefresher>(refresherSources.Length);
            for (int i = 0; i < refresherSources.Length; i++)
            {
                if (refresherSources[i] is IRuntimeRefresher r) resolved.Add(r);
                else if (refresherSources[i] != null)
                    Debug.LogWarning($"[AllRefresh] '{refresherSources[i].GetType().Name}' is not an IRuntimeRefresher — skipped.", this);
            }
            return resolved.ToArray();
        }

        // Fan-out core: interface-in / string-out so EditMode tests can drive it
        // with fakes and no network (same technique as DcSheetRuntimeRefresher.ApplyBodies).
        internal void RefreshAll(IRuntimeRefresher[] children, Action<string> onDone)
        {
            if (children == null || children.Length == 0)
            {
                onDone?.Invoke("no refreshers wired");
                return;
            }

            RequestInFlight = true;
            var logs = new string[children.Length];
            int remaining = children.Length;

            for (int i = 0; i < children.Length; i++)
            {
                int slot = i;
                // A child may call back synchronously (e.g. its own in-flight guard).
                // remaining is seeded before the loop, so an early callback can never
                // reach 0 while later children are still unstarted.
                children[slot].Refresh(log =>
                {
                    logs[slot] = log;
                    if (--remaining == 0)
                    {
                        RequestInFlight = false;
                        onDone?.Invoke(Compose(children, logs));
                    }
                });
            }
        }

        // StatRefreshButtonView shows only the first line of the log, so line 1 is a
        // one-line digest of every child (their own first lines joined), and the
        // per-child detail follows underneath.
        private static string Compose(IRuntimeRefresher[] children, string[] logs)
        {
            var summary = new StringBuilder("ALL:");
            for (int i = 0; i < logs.Length; i++)
            {
                summary.Append(i == 0 ? " " : " | ");
                summary.Append(FirstLine(logs[i]));
            }

            var sb = new StringBuilder(summary.ToString());
            for (int i = 0; i < logs.Length; i++)
            {
                sb.Append("\n\n== ").Append(children[i].GetType().Name).Append(" ==\n");
                sb.Append(string.IsNullOrEmpty(logs[i]) ? "(empty)" : logs[i]);
            }
            return sb.ToString();
        }

        private static string FirstLine(string log)
        {
            if (string.IsNullOrEmpty(log)) return "(empty)";
            int newline = log.IndexOf('\n');
            return newline < 0 ? log : log.Substring(0, newline);
        }
    }
}
