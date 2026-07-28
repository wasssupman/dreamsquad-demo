# 5 — PlayMode 검증

## 목적

설계 계약을 회귀 가드로 고정: 핸드오프 팝 없음(계약 5), 활성화 타이밍 불변(계약 4), 경로 게이트(계약 1), 세션 독립(계약 7).

## 변경 대상

- `Assets/_Project/Tests/PlayMode/DropDismountTest.cs` (신규)

## 구현

`DragPlacementReachTest` 하네스 재사용(씬 로드 → 풀 세팅 → `BeginDrag`/`UpdateDrag`/`EndDrag` 직접 구동). 검증 4건:

1. **핸드오프 연속성**: 유효 셀에서 `EndDrag` → 커밋 프레임에 (a) 고스트 마지막 발점(reflection: `_unitPosWorld` − previewHeight 변환)과 (b) 첫 오버라이드 값(`TryGetDefenderViewOverride`)의 거리 < 0.05 world 단정. 점유·코스트가 같은 프레임에 확정됐는지 함께 단정(계약 2).
2. **활성화 타이밍**: 커밋 시각 기록 → `PendingDeployment` 해제까지 실측 경과가 `deploymentDuration −0.05s ~ +0.25s`(구현 중 정정 — 에디터 프레임 히치 여유. 진짜 회귀인 +0.45s 시프트와 명확히 구분). 착지(오버라이드 clear)가 활성화보다 늦지 않음(클램프 동작).
3. **경로 게이트**: `SimulateDragTo`(탭 경로)로 배치 → 비행 종료까지 `TryGetDefenderViewOverride == false` 유지(탭 경로는 dismount 미발동 — 탭은 자체 고스트 비행).
4. **세션 독립**: 드롭 커밋 직후(비행 중) 즉시 `BeginDrag` 로 새 세션 시작 → 이전 entity 오버라이드가 프레임 진행에 따라 계속 갱신됨(값 변화 관찰) + 새 세션 정상 동작.

reflection 접근은 기존 테스트 관례(`Field`/`SessionHover` 헬퍼) 복제. 카탈로그·코스트 세팅은 `RelocationPlacementSessionTest` 패턴.

## 완료 기준

- 신규 4건 green · 기존 PlayMode 스위트에서 **이 변경으로 새로 깨지는 테스트 0건** (main 기존 실패 7건과 구분 — 2026-07-28 baseline 기록 있음)
- 완료 시 README 상태 갱신 + `6_handoff_summary.md` 작성
