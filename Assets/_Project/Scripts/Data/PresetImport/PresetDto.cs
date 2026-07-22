namespace Wassup.Data.PresetImport
{
    // preset-sheet-import unit 1 — Presets 탭 행 DTO. 필드명 = 시트 헤더(기존 DTO 규약).
    // squad/dreamcatcher 는 "," 구분 id 원문이고, 분해·id→SO 해석은 PresetSheetApplier 가 한다.
    // 빈 셀은 파서(SheetEnvelopeParser)가 바인딩 전 제거하므로 null 로 도착할 수 있다.
    // import 파싱과 export 직렬화가 이 한 DTO 를 공유한다(양방향 컬럼 계약).
    public class PresetDto
    {
        public string presetName;
        public string squad;        // "," 구분 DefenderUnitData id 목록 (≤ maxUnits)
        public string dreamcatcher; // "," 구분 DreamcatcherCard id 목록 (≤ 라이브 deckSize)
    }
}
