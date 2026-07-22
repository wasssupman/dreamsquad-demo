using System;
using System.Text;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.PresetImport;
using Wassup.Data.StatImport;

namespace Wassup.Core
{
    // preset-sheet-import unit 3 — dev/QA 런타임 refresher. 로그인 후 Presets 탭을 fetch 해
    // SquadPresetCollection 을 메모리에서 재구성(에셋 저장 없음, 재시작 시 원복). id→SO 는
    // DefenderCatalog/DreamcatcherCardCatalog.ById(프로필이 참조 가능한 authoritative 인덱스).
    // DcSheetRuntimeRefresher 형제. IRuntimeRefresher 3번째 구현체(인터페이스 규칙 위반 아님).
    public class PresetSheetRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        // 탭명 contract-fixed (preset-sheet-import 0_sheet_schema_contract.md).
        private const string Tab = "Presets";

        [SerializeField] private SquadPresetCollection collection;
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        [SerializeField] private string baseUrl = "https://dev-api-somnia.cashroyale.games/demo/google/sheet";

        public bool RequestInFlight { get; private set; }

        public void Refresh(Action<string> onDone)
        {
            if (RequestInFlight) { onDone?.Invoke("refresh already in progress"); return; }
            RequestInFlight = true;

            string url = SheetEnvelopeParser.BuildSheetUrl(baseUrl, Tab);
            SheetFetcher.Fetch(url, result =>
            {
                string res;
                try { res = ApplyBody(result, collection, defenderCatalog, cardCatalog); }
                catch (Exception e) { res = $"Refresh failed: {e}"; }
                finally { RequestInFlight = false; }
                onDone?.Invoke(res);
            });
        }

        // 네트워크 없는 EditMode 구동용 순수 코어(fake Result + in-test 카탈로그).
        // 에디터 ApplyPresetFetched 와 동형이나, SO 인덱스를 AssetDatabase 스캔이 아니라
        // 카탈로그 ById 로 얻고 저장 콜백 없이 in-memory 만 갱신한다.
        internal static string ApplyBody(SheetFetcher.Result r, SquadPresetCollection collection,
            DefenderCatalog defenderCatalog, DreamcatcherCardCatalog cardCatalog)
        {
            var log = new StringBuilder();
            var rows = SheetEnvelopeParser.ParseSheetLogged<PresetDto>(r.body, r.transportError, Tab, log);
            if (rows == null) return log.ToString();
            if (collection == null) { log.AppendLine("[preset] collection 미할당 — refresh 스킵."); return log.ToString(); }

            Func<string, DefenderUnitData> resolveUnit = id => defenderCatalog != null ? defenderCatalog.ById(id) : null;
            Func<string, DreamcatcherCard> resolveCard = id => cardCatalog != null ? cardCatalog.ById(id) : null;

            PresetSheetApplier.Apply(rows, resolveUnit, resolveCard, SquadSave.SlotCount, collection, log);
            return log.ToString();
        }
    }
}
