# 2 — 배치 스킬: 개점 일제 조사 (2타일 2초 tick DoT + 대상별 빔)

## 목적

배치 순간 반경 2타일 내 모든 적에게 2초간 빔 공격(0.2s 간격 7 피해 = 총 70)을 가한다.
심 = 기존 이산 tick DoT 재사용, 연출 = unit 1 BeamPresenter 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `OnPlaceEffectType.DotNearby` enum 멤버(맨 뒤) + `onPlaceTickInterval` 필드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — on-place 체인 분기 1개 + BeamPresenter 다중 빔 요청
- `Assets/_Project/Tests/EditMode/` — on-place 케이스 추가

## 구현

1. 심: `CollectEnemiesInTileRange` 로 반경 내 적 수집 → 각 대상에
   `EnemyCcEvent { kind = DoT, scalar = onPlaceMagnitude, tickInterval = onPlaceTickInterval,
   remainingTime = onPlaceDuration }` enqueue — dot-tick-cadence 의 이산 tick 계약 그대로. 신규 시스템 0.
   - ⚠ **환산하지 않는다.** `tickInterval > 0` 이면 `scalar` 는 DPS 가 아니라 **틱당 피해**다
     (`CcEffect` 주석: "주기 도달 시 scalar(=tick당 데미지) 청크 지급"). 이 문서 초안은 `7/0.2=35`
     로 환산하라고 적혀 있었고 그대로 했으면 피해가 5배가 됐다 — `OnPlaceDotNearbyTest` 가 상한으로 잡는다.
   - 총 피해 = `magnitude × (duration / tickInterval)` = 7 × (2/0.2) = 70.
   - `tickTimer = tickInterval` 로 넣어 첫 틱을 즉발시킨다(CcApply add-path 규약).
2. 연출: 같은 분기에서 BeamPresenter 에 "대상별 빔 (duration=2s)" 세션 요청 —
   unit 1 의 TTL 메커니즘에 고정 duration 세션 모드 추가(공격 코얼레스와 같은 풀).
3. enum 멤버 맨 뒤 추가 + 신규 필드 기본값 0 = 기존 에셋 무형 롤아웃.
4. EditMode 케이스: 반경 필터·DoT 파라미터 환산(총 피해 = magnitude × duration/tickInterval) 검증.

## 완료 기준

- [ ] compile clean + 신규 EditMode 케이스 green
- [ ] 에디터 Play: 적 무리 위에 배치 → 반경 내 전원에게 빔 2초 + 도트 데미지 넘버, 반경 밖 무피해
- [ ] `Defender_Busters.asset` 에 배치 스킬 값 기입 (range 2 · magnitude 7 · tickInterval 0.2 · duration 2)
