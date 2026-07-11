# 1. 시드 JSON + 시트 입력 규약

## 목적

현 SO 에셋 값의 스냅샷(`1_seed_dreamcatcher.json`)을 보존하고, 기획이 시트에 초기 입력하는 규약을 남긴다. 이후 SoT 는 시트로 넘어간다 (unit-stat 의 `3_seed_unit_stats.json` 과 동일 역할 — 역사 기록).

## 변경 대상

- `docs/spec/dreamcatcher-sheet-sync/1_seed_dreamcatcher.json` (신규) — 탭 6종 × 47행. 스크립트로 .asset YAML 에서 추출 (enum int→멤버명, \uXXXX/\xXX 디코드).

## 시트 입력 규약

- JSON 의 최상위 키 6개(`DcCards`…`DcConfig`) = 시트 탭 이름. 각 배열 = 해당 탭의 행, 객체 키 = 헤더 행.
- 행 순서 자유 (id/cardId+slot 매칭). `_` 접두 컬럼은 참고용 — 수정해도 게임에 반영되지 않음.
- 소수/음수 그대로 (`maxUnit: -1` = 무제한). 빈 셀 = 해당 SO 값 유지.
- displayName/description 의 기존 오타(예: "끕어올린다")는 에셋 원본 그대로다 — 시트에서 바로 수정하면 다음 import 때 반영된다.

## 완료 기준

- [ ] 시드 JSON 이 unit 0 스키마와 컬럼/타입 일치
- [ ] 기획 시트에 6탭 입력 완료 (사용자 작업) 후, unit 3 의 첫 import 가 no-op 에 준하는 결과(텍스트 수정분 제외)를 보고
