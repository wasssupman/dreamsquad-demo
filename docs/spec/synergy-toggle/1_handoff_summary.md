# 1 — Handoff Summary

## Commit

- 이번 커밋: 인접 동족 시너지 기본 비활성화 + 기존 슬롯 중립화.

## Implemented

- `BattleBridge.enableAdjacencySynergy` 직렬화 토글을 추가하고 기본값을 껐다.
- 토글이 꺼진 상태에서는 인접 유닛 카운트와 시너지 bonus enqueue를 건너뛴다.
- 이전에 활성화된 시너지 레지스트리가 있으면 살아 있는 배치 유닛의 `stackId=1`을 `multiplier=1`로 refresh해 `DamageMul +0`으로 중립화한다.
- 효과 타일의 전용 `stackId=2`와 그 외 modifier 슬롯은 변경하지 않는다.
- `SynergySlot_NeutralRefresh_RemovesOnlyItsBonus` EditMode 회귀 테스트를 추가했다.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/EffectTileModifierTests.cs`

## Verified

- 카메라·문서 범위를 포함한 변경 경로의 `git diff --check` 통과.
- Unity EditMode/Play 실행은 이 세션에서 미실행.

## Notes

- 토글을 Play 중 끈 경우 기존 시너지 슬롯은 다음 배치 또는 방어유닛 사망으로 `RecomputeSynergyFor`가 호출될 때 중립화된다.
- 신규 세션에서는 기본값이 false라 시너지 슬롯이 생성되지 않는다.

## Follow-up

- 사용자 Play: 같은 유닛 인접 배치 시 공격력 상승 없음, 효과 타일은 배치 유닛 한 명에게만 적용되는지 확인.
