# 0 — footprint 데이터 레이어

## 목적

유닛 점유 크기(W×H)의 데이터 축과 대표 셀 규약 산식을 세운다. 컴파일·테스트만으로 라이브 동작 무변(전 유닛 기본 1×1)이 이 unit 의 경계다 — 판정·UX 는 unit 1~2 몫.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `footprintWidth`/`footprintHeight`(int, 기본 1) + `Footprint` 클램프 프로퍼티
- `Assets/_Project/Scripts/Data/FootprintMath.cs` **신규** — 셀 rect·대표 셀 순수 static
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `DefenderStatDto` 에 `footprintWidth`/`footprintHeight`(int?) 추가
- `Assets/_Project/Tests/EditMode/FootprintMathTests.cs` **신규**

## 구현

- **앵커 규약 = 점유 rect 의 min 코너.** 기존 `MapStageMath.FootprintCells`(프랍/차단존)와 동일 규약이며, 셀 rect 는 그 함수에 위임해 클램프(최소 1×1) 규칙을 한 곳에 둔다.
- **대표 셀 = anchor + ((W−1)/2, (H−1)/2)** — 홀수 변 정중앙, 짝수 변 floor (README 계약 2). `PrimaryCell(anchor, size)` / `AnchorFromPrimary(primary, size)` 는 왕복 대칭.
- 유닛 SO 필드명은 DTO 와 1:1 (`footprintWidth`/`footprintHeight` — `HazardCastState` 와 같은 명명). 읽기는 `Footprint` 프로퍼티에서 최소 1 클램프(시트가 0/음수를 밀어도 읽는 자리에서 조임 — `retireCooldownRatio` Clamp01 선례).
- **시트 계약은 필드 추가로 끝** — 이름 일치 리플렉션(unit-stat-spreadsheet-schema)이라 임포트(컬럼 부재 → null → SO 유지)·익스포트(스냅샷에 자동 포함) 양방향 자동. 실제 구글 시트 컬럼 추가는 콘텐츠 저작 시점의 별도 작업.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `FootprintMathTests` 그린 — 1×1 항등 · 홀/짝 대표 셀 오프셋 · 앵커↔대표 셀 왕복 대칭 · rect 클램프 · SO `Footprint` 클램프
- [x] 기존 EditMode 코어 lane 무회귀 (시트 매퍼 테스트 포함) — 2474 중 2471 통과 · 실패 0 · 스킵 3(선행 Ignore)
- [x] 라이브 동작 무변 — 전 유닛 1×1 기본값, 판정·스폰 경로 미변경

확인 2026-08-28 — 사용자 일괄 진행 승인(units 끝까지 + 코드 리뷰). 커밋 해시는 handoff 에 기록.
