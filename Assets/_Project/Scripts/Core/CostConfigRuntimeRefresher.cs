using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Core
{
    // sheet-export-push unit 7 — dev/QA 런타임 refresher. 로비에서 CostConfig 탭을
    // fetch 해 코스트 경제 SO 를 메모리에서 갱신한다(에셋 저장 없음, 재시작 시 원복).
    // PresetSheetRuntimeRefresher 형제 — IRuntimeRefresher 4번째 구현체.
    // 반영 시점: GameManager 는 battle-scoped 라 Awake 에서 CostRuntime.Configure 를
    // 다시 호출한다 → 로비에서 누른 refresh 는 "다음 전투부터" 적용된다(형제들과 동일).
    public class CostConfigRuntimeRefresher : MonoBehaviour, IRuntimeRefresher
    {
        // 탭명 contract-fixed (docs/spec/sheet-export-push/7_costconfig_tab.md).
        private const string Tab = "CostConfig";

        [SerializeField] private CostConfig config;
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
                try { res = ApplyBody(result, config); }
                catch (Exception e) { res = $"Refresh failed: {e}"; }
                finally { RequestInFlight = false; }
                onDone?.Invoke(res);
            });
        }

        // 네트워크 없는 EditMode 구동용 순수 코어(fake Result + in-test SO).
        // 에디터 import 와 같은 applier 를 쓰되, SO 인덱스를 AssetDatabase 스캔이 아니라
        // 인스펙터 참조 하나로 만들고 저장 콜백 없이 in-memory 만 갱신한다.
        internal static string ApplyBody(SheetFetcher.Result r, CostConfig config)
        {
            var log = new StringBuilder();
            var rows = SheetEnvelopeParser.ParseSheetLogged<CostConfigDto>(r.body, r.transportError, Tab, log);
            if (rows == null) return log.ToString();
            if (config == null) { log.AppendLine("[cost-config] config 미할당 — refresh 스킵."); return log.ToString(); }

            var byId = new Dictionary<string, CostConfig>();
            if (string.IsNullOrEmpty(config.id))
                log.AppendLine($"[cost-config] '{config.name}' 의 id 가 비어 있어 어떤 행과도 매칭되지 않는다.");
            else
                byId[config.id] = config;

            return CostConfigSheetApplier.Apply(rows, byId, null, log);
        }
    }
}
