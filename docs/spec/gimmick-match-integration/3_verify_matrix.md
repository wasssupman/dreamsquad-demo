# 3 — 통합 검증 매트릭스

## 목적

unit 1·2 로 기능이 완성된 상태에서, 세 검증 질문을 on/off·재시작 축으로 교차 확인한다. 씬 배선은 이미 해당 코드 유닛에서 끝났으므로 이 유닛은 **순수 Play 검증 + 회귀 점검**이다.

## 변경 대상

- 없음(코드/씬 변경 없음). 검증 전용. 필요 시 `season_overwork.asset` 잔여 참조 최종 확인.

## 검증 매트릭스

| 경로 | 기대 |
|---|---|
| **A. 기믹 ON (기본)** | 콘솔 `gimmick=<id>` · `OverworkGimmickConfig 주입` · `PickupSpawnState built`. 배치 페이즈 안내 카드 표시(좌상단 메뉴 비가림, raycast 통과). 전투 시작 → 카드 사라짐. 피로도 누적→번아웃 + 레드불→라스트런 동작. |
| **B. 기믹 OFF** (`gimmickEnabled=false`) | 콘솔 `gimmick=none`. config·픽업 스폰 미주입. 안내 카드 미표시. 클린 forest(피로도/레드불 없음). 에러 0. |
| **C. 재시작(Restart)** | 배치 페이즈 재진입 시 카드 재표시(enable-sync). 기믹 재주입 정상. 픽업/피로도 leftover·중복 없음. |
| **D. 결정론** | `debugFixedMatchSeed` 고정 → 재실행마다 동일 기믹 id 로그(현재 pool 1개라 항상 Overwork). |

## 완료 기준

- [ ] A/B/C/D 전 경로 통과, `read_console` 에러 0.
- [ ] OFF→ON 토글 후 정상 복구(BattleConfig 값만으로 기능 게이팅).
- [ ] `season.gimmick` 잔여 참조 0(코드·에셋). 시즌은 forest 테마 정상 유지.
- [ ] 신규 에셋 `.meta` 포함 커밋(unit 0 의 `BattleConfig.asset` + 폴더 `.meta`).
- [ ] 통과 확인 후 `4_handoff_summary.md` 작성 + 파이프라인 맵 구조 변경 없음 확인(N/A 유지).
