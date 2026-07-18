# 7 — Handoff Summary

## Commit

- `043aa694` feat(tap-to-place): 던지기 비행과 도착 정착 (unit 6)

## Implemented

- 선택 타일의 `endFeet`를 유닛·고리 최종 좌표의 불변 기준으로 사용.
- 2차 베지어를 시작/착지 접선을 따로 조정하는 3차 베지어로 교체.
- 결정론 좌우 변주는 유지하며 두 제어점에 같은 오프셋 적용.
- `OutCubic` 진행률로 빠른 출발→감속 착지 프로파일 적용.
- 탭 프리뷰는 비행부터 정착까지 동일한 비진동 `SmoothDamp`로 추종.
- 거리·속도 조건을 모두 만족한 뒤 공용 `CommitPlacementAt`으로 확정.
- 최대 정착시간 초과 시 최종 자세로 정렬하고 같은 프레임에 확정.
- 착지 시 기존 타일 hover 팝을 재사용.

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/UI/KeyringSim.cs`
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs`
- `Assets/_Project/Data/Config/DragSwaySettings.asset`
- `Assets/_Project/Tests/EditMode/KeyringSimTests.cs`
- `docs/spec/defender-tap-to-place/6_throw_arrival_settle.md`

## Verified

- Unity 6000.4.3f1 컴파일 오류 0.
- EditMode 964 total: 962 pass, 0 fail, 기존 의도적 skip 2.
- 근/중/원거리 Play: 도착 30~40% 전 급가속 제거, 사용자 체감 통과 2026-07-18.
- scoped `git diff --check` 통과.

## Notes

- 실제 드래그는 기존 `spring/damping/maxSpeed` 손맛을 그대로 사용한다.
- 탭 시뮬만 `tapFollowSmoothTime`을 사용한다.
- SO 조절 가능: 비행시간·거리 clamp, 아치 높이·좌우 폭, 시작/착지 제어점,
  추종 smooth time, 정착 거리·속도·최대시간.
- `OutCubic` 공식은 후반 재가속 방지 동작 계약이라 SO 튜닝 대상이 아니다.
- 공유 SO의 rope/snap/기존 비행시간 등 별도 dirty 튜닝은 구현 커밋에서 제외했다.

## Follow-up

- 기능 후속 후보는 README 하단을 따른다.
- 구조 변경이나 신규 플레이 오브젝트가 없어 파이프라인 맵 갱신은 N/A.
