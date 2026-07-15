# 7. 야근 시즌 SO 정식화 + Play 통합 검증

## 목적

임시 연결(검증용)을 정식 데이터로 대체하고, 야근 기믹을 실제 시즌으로 활성화한다. spec 의 검증 질문 4개를 통합 Play 로 확인한다.

## 시즌 구조 결정 (2026-07-15)

- **forest 시즌 = 클린 baseline (gimmick 없음)**, **야근 시즌 = forest 테마 + Overwork 기믹** 을 별도 SeasonData 로 분리.
- `SeasonData.gimmick` 만 다르고 `mapTheme` 는 공유 → 시각(테마)은 동일, defaultSeason 교체로 기믹 on/off. 이 구조가 곧 "gimmick=null 무변화" 검증 수단.
- 현 코드에 backdrop 시스템 없음(`SeasonData` = seasonId/displayName/mapTheme/gimmick) → defaultSeason 교체는 테마만 좌우. 안전.

## 변경 대상

- `Assets/_Project/Data/Season/season_overwork.asset` — 신규 (forest mapTheme + Gimmick_Overwork)
- `Assets/_Project/Data/Season/SeasonRegistry.asset` — allSeasons += overwork, defaultSeason → overwork
- `Assets/_Project/Data/Season/season_S1_forest.asset` — 임시 기믹 링크 revert (클린 복원)

## 검증 (Play 통합)

| 검증 질문 | 결과 |
|---|---|
| 야근 시즌 활성 → 두 룰 동작? | ✅ 주입 로그 `season=S_Overwork, gimmick=G1_Overwork`. 피로도 누적→번아웃(unit 3) + 레드불 소비→라스트런 crash(unit 5) 모두 이 시즌서 재확인 |
| gimmick=null → 무변화? | ✅ forest 시즌(기믹 null)=config/PickupSpawnState 미생성 (unit 2/3 실증, 동일 게이트). defaultSeason 을 forest 로 되돌리면 baseline |
| 배치 후 번아웃 + 회복 재누적? | ✅ unit 3 Play 실증 (공속/공격/최대체력 ×0.8 → 15s 해제 → 재누적) |
| 레드불 유닛/적 소비 + 5s 최대체력 컷? | ✅ unit 5 Editor.log (consumed→5s→crash ×0.10) + unit 6 시각 |

- forest 테마 정상 렌더 + 레드불 플레이스홀더 표시 (스크린샷), 콘솔 에러 0.

## 완료 기준

- season_overwork 활성 상태로 Play 시 기믹 두 룰 모두 동작, 테마/뷰 정상, 에러 0.
- defaultSeason 을 forest 로 바꾸면 기믹 완전 비활성(무변화).

확인 2026-07-15 · 커밋 `529b9d09` · Play 실측: `season=S_Overwork` 주입 + 레드불 소비 로그 + 활성 픽업 4개 안정(스폰 셀 전부 Walk/Place, 이동/배치 외 0개) + forest 렌더 + 에러 0.
