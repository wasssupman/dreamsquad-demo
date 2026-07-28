# 2 — 배치 스킬: 개점 일제 조사 (2타일 2초 tick DoT + 대상별 빔)

## 목적

배치 순간 반경 2타일 내 모든 적에게 2초간 빔 공격(0.2s 간격 7 피해 = 총 70)을 가한다.
심 = 기존 이산 tick DoT 재사용, 연출 = unit 1 BeamPresenter 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `OnPlaceEffectType.DotNearby` enum 멤버(맨 뒤) + `onPlaceTickInterval` 필드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — on-place 체인 분기 1개 + BeamPresenter 다중 빔 요청
- `Assets/_Project/Tests/EditMode/` — on-place 케이스 추가

## 구현

1. 심: `onPlaceRange` 내 적 수집(BindNearby 공간 질의 재사용) → 각 대상에
   `EnemyCcEvent { kind = DoT, scalar = 틱당피해/tickInterval(dps 환산), tickInterval = onPlaceTickInterval,
   remainingTime = onPlaceDuration }` enqueue — dot-tick-cadence 의 이산 tick 계약 그대로
   (`Tick_Dot_First_Tick_Is_Immediate` 등 기존 테스트가 지배). 신규 시스템 0.
   - dps 환산식: scalar = onPlaceMagnitude / onPlaceTickInterval (7 / 0.2 = 35). 환산은
     bridge 분기 안 한 줄 — 자명 산술이라 순수 함수 추출 안 함(제약 10 각주 기준).
2. 연출: 같은 분기에서 BeamPresenter 에 "대상별 빔 (duration=2s)" 세션 요청 —
   unit 1 의 TTL 메커니즘에 고정 duration 세션 모드 추가(공격 코얼레스와 같은 풀).
3. enum 멤버 맨 뒤 추가 + 신규 필드 기본값 0 = 기존 에셋 무형 롤아웃.
4. EditMode 케이스: 반경 필터·DoT 파라미터 환산(총 피해 = magnitude × duration/tickInterval) 검증.

## 완료 기준

- [ ] compile clean + 신규 EditMode 케이스 green
- [ ] 에디터 Play: 적 무리 위에 배치 → 반경 내 전원에게 빔 2초 + 도트 데미지 넘버, 반경 밖 무피해
- [ ] `Defender_Busters.asset` 에 배치 스킬 값 기입 (range 2 · magnitude 7 · tickInterval 0.2 · duration 2)
