namespace Wassup.Data.StatImport
{
    // sheet-export-push unit 7 — CostConfig 탭 행. DcConfigDto 형제: nullable 필드 =
    // 부분 갱신(빈 셀은 null 로 역직렬화되어 SO 값을 건드리지 않음), 필드명은 CostConfig
    // 와 1:1 이라 UnitStatFieldMapper 가 이름으로 읽고/적용한다. export(SO→행)와
    // import(행→SO)가 같은 타입을 공유한다.
    public class CostConfigDto
    {
        public string id;
        public int? startingCost;
        public int? maxCost;
        public float? regenPerSec;
        public float? placementPhaseDuration;
    }
}
