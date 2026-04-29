# PlayMode Smoke + Handoff

**작업 구분**: 5

## 목적

draft 진입 시점에 맵이 시각적으로 보이고, 옵션 토글이 즉시 반영되며, placement 전환이 매끈한지를 PlayMode 에서 사용자 manual 확인. 이후 `6_handoff_summary.md` 작성으로 spec 종료.

## 변경 대상

- 코드 변경 없음 (Unit 0~4 가 끝난 상태에서 검증).
- Add: `docs/spec/draft-stage-map-prebuild/6_handoff_summary.md`

## 검증 시나리오 (사용자 manual)

| # | 시나리오 | 기대 결과 |
|---|---|---|
| V1 | Play 누르고 첫 화면 | 맵이 카드 fan 뒤로 보인다. tile / background props / obstacles / 골 마커 모두 표시 |
| V2 | MAP SETTINGS 토글 → path shape 변경 | 맵이 즉시 새 path 로 재생성. flow field / obstacles 도 갱신 |
| V3 | MAP SETTINGS → grid size 변경 | 맵 크기 변화 + 카메라 framing 자동 재적용 |
| V4 | MAP SETTINGS → obstacle density 변경 | obstacles 분포 갱신, path 가 막히면 fallback 동작 |
| V5 | 카드 3장 폐기 → DraftConfirmed | placement 진입. mapView 자체는 변화 없음 (자식 카운트 동일) |
| V6 | placement 카운트다운 → StartBattle | battle 정상 시작. 적 spawn / 디펜더 동작 회귀 0 |
| V7 | 게임 종료 → Redraft | 카드 새로 + 맵 새로 (새 seed). 옵션은 그대로 유지 |
| V8 | 게임 종료 → Restart | 카드 그대로 + 맵 그대로. ECS entity 재생성 |
| V9 | Hazard 디버그 메뉴로 hazard spawn (placement 중) | hazard 시각 정상. 이후 RebuildDraftMap 안 호출되는 시점이므로 누수 없음 |
| V10 | 옵션 토글 50회 빠르게 반복 | hitch 가능 (성능 spec 범위 밖) but 메모리/엔티티 누수 없음. 콘솔 에러 0 |

V1~V8 통과 = 시각 + 흐름 검증. V9~V10 = 안정성.

## 6_handoff_summary.md 템플릿

```markdown
# Draft Stage Map Prebuild — Handoff Summary

**완료일**: YYYY-MM-DD
**상태**: 구현 완료 + EditMode 회귀 0 + PlayMode 사용자 확인 통과.

## Commit

| 범위 | 해시 | 설명 |
|---|---|---|
| spec docs | (해시) | spec 문서 작성 |
| Unit 0 | (해시) | EnsureQueriesAndQueues 분리 + PrepareDraftMap / RebuildDraftMap |
| Unit 1 | (해시) | RebuildDraftMap cleanup + MapView.ResetVisualRoots |
| Unit 2 | (해시) | GameManager.Start 호출 순서 |
| Unit 3 | (해시) | DraftController rebuild 트리거 |
| Unit 4 | (해시) | EditMode 테스트 |
| Unit 5 | (해시) | handoff (본 문서) |

## Implemented

- BattleBridge: PrepareDraftMap / RebuildDraftMap / HasGeneratedMap / CleanupDraftMapBeforeRebuild 신설
- BattleBridge.EnsureQueriesAndQueues 에서 BuildMapForBattle 분리 + 멱등화
- BattleBridge.BeginPlacement: HasGeneratedMap 가드로 BuildMapForBattle skip
- MapView.ResetVisualRoots: obstacles / background props / goal marker root 재생성
- GameManager.Start: BeginDraft 직전 PrepareDraftMap 호출
- DraftController: SetMapGenerationOptions / SetMapPathShape 가 RebuildDraftMap 트리거
- BattleBridge.OnRedraftRequested: TeardownCurrentBattle 후 PrepareDraftMap 재호출 (Redraft 시 맵 새로)
- EditMode 테스트: BattleBridgeDraftMapTests 6 + DraftControllerMapRebuildTests 4

## Key Files

Bridge: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
Core: `Assets/_Project/Scripts/Core/GameManager.cs`, `DraftController.cs`, `MapView.cs`
Tests: `BattleBridgeDraftMapTests.cs`, `DraftControllerMapRebuildTests.cs`

## Verified

- 컴파일 + Burst 활성
- EditMode (총 N+10/N+10) 통과 (회귀 0)
- PlayMode V1~V10 사용자 manual 통과
- 콘솔 에러 0

## Notes

- (작성 시점에 기록 — 발견된 edge case, 임시 회피책 등)

## Follow-up

- 옵션 토글 시 grid size 변경 트랜지션 — 별도 spec
- 맵 빌드 비용 프로파일링 + debounce — 측정 후 결정
- 카드 fan 영역 alpha / blur — UI 가독성 차후
- draft 카메라 회전/줌 인터랙션 — 별도 spec
```

## 완료 기준

- V1~V10 사용자 manual 확인 통과.
- `6_handoff_summary.md` 작성 + 모든 commit hash 기재.
- README.md 상단 상태 라인을 "완료 YYYY-MM-DD" 로 갱신.
- 콘솔 에러 0.

검증: 2026-04-30 — `6_handoff_summary.md` 작성 + README 상태 갱신 완료. PlayMode V1~V10 사용자 manual 통과 (카드 fan 뒤 맵 시각, MAP SETTINGS 즉시 갱신, Confirm/Placement/Battle/Redraft/Restart 회귀 0).
