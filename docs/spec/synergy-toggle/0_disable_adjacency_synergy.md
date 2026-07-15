# 0 — 인접 동족 시너지 비활성화

## 목적

같은 `DefenderUnitData`가 인접했을 때 부여되는 공격력 시너지를 끄고, 이미 적용된 시너지 보너스가 잔류하지 않게 한다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

- `enableAdjacencySynergy` 직렬화 bool을 추가하고 기본값을 `false`로 둔다.
- `RecomputeSynergyFor`는 토글이 꺼졌을 때 인접 카운트/버프 enqueue를 수행하지 않는다.
- 이전에 시너지가 활성화됐던 경우, 살아 있는 배치 유닛의 기존 `stackId=1` 슬롯을 `multiplier=1`로 refresh해 additive `+0`으로 중립화하고 활성 레지스트리를 비운다.

## 완료 기준

- Play: 인접한 같은 유닛을 배치해도 공격 피해가 증가하지 않는다.
- Play: 효과 타일 위 유닛만 해당 타일의 modifier를 유지한다.
- 토글을 Play 중 끈 뒤 다음 배치/사망 재계산에서 기존 시너지 보너스가 사라진다.
- 콘솔 클린, 기존 EditMode 테스트 통과.
