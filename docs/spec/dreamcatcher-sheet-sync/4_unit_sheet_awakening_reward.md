# 4. 유닛 시트 awakeningReward 컬럼 추가

## 목적

하드코딩 감사에서 확인된 유닛 시트 계약 누락 1건을 메운다: `awakeningReward`(처치 시 각성 게이지 보상, Defender 기본 4 / Enemy 기본 1)는 라이브 밸런스 스칼라인데 Defenders/Enemies 탭에 없다.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `DefenderStatDto`/`EnemyStatDto` 에 `public int? awakeningReward;` 각 1필드

## 구현

기존 계약 "새 컬럼 = DTO 필드 1개" 경로 그대로 — 리플렉션 이름복사가 import/export 양방향을 자동 흡수. 별도 매퍼/스킵리스트 변경 없음.

## 완료 기준

- [x] compile 0 error + 기존 EditMode 테스트 그린 (65/65)
- [x] 리플렉션 왕복 테스트로 export 읽기/import 쓰기 확인 (`AwakeningReward_RoundTripsThroughUnitDtos`). 시트 Defenders/Enemies 탭 헤더 추가는 사용자 후속 작업 — 빈 헤더 전이라도 파이프라인은 무해(키 생략=유지)

확인 2026-07-11 — 커밋 `a3f4c9a9`.
