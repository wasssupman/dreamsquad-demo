# 4. 유닛 시트 awakeningReward 컬럼 추가

## 목적

하드코딩 감사에서 확인된 유닛 시트 계약 누락 1건을 메운다: `awakeningReward`(처치 시 각성 게이지 보상, Defender 기본 4 / Enemy 기본 1)는 라이브 밸런스 스칼라인데 Defenders/Enemies 탭에 없다.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `DefenderStatDto`/`EnemyStatDto` 에 `public int? awakeningReward;` 각 1필드

## 구현

기존 계약 "새 컬럼 = DTO 필드 1개" 경로 그대로 — 리플렉션 이름복사가 import/export 양방향을 자동 흡수. 별도 매퍼/스킵리스트 변경 없음.

## 완료 기준

- [ ] compile 0 error + 기존 EditMode 테스트 그린
- [ ] 시트 Defenders/Enemies 탭에 `awakeningReward` 헤더 추가(사용자 작업) 후 import 로 값 왕복 확인 (또는 export 파일에 필드 등장 확인)
