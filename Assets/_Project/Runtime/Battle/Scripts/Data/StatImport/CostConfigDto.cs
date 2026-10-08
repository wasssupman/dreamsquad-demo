using Newtonsoft.Json;

namespace Somnia.Battle.Data.StatImport
{
    // sheet-export-push unit 7 — CostConfig 탭 행. DcConfigDto 형제: nullable 필드 =
    // 부분 갱신(빈 셀은 null 로 역직렬화되어 SO 값을 건드리지 않음), 필드명은 CostConfig
    // 와 1:1 이라 UnitStatFieldMapper 가 이름으로 읽고/적용한다. export(SO→행)와
    // import(행→SO)가 같은 타입을 공유한다.
    // skill-data-table unit 9 — 시트 열 = 스네이크(`[JsonProperty]`) · C# 필드 이름은 SO 와 같게 둔다(매퍼는 C# 이름으로 짝짓는다).
    public class CostConfigDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("starting_cost")] public int? startingCost;
        [JsonProperty("max_cost")] public int? maxCost;
        [JsonProperty("regen_per_sec")] public float? regenPerSec;
        [JsonProperty("placement_phase_duration")] public float? placementPhaseDuration;
    }
}
