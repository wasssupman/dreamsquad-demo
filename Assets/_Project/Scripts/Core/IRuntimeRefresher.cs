using System;

namespace Wassup.Core
{
    // runtime-stat-refresh unit 4 — shared contract for the dev/QA lobby refresh
    // buttons. Two implementers (UnitStatRuntimeRefresher / DcSheetRuntimeRefresher)
    // justify the interface; StatRefreshButtonView drives either one.
    public interface IRuntimeRefresher
    {
        bool RequestInFlight { get; }
        void Refresh(Action<string> onDone);
    }
}
