# 0 — 사거리 술어 미러 동치성 안전망

## 목적

**술어를 하나도 안 바꾸고, 현행 동작을 못박는다.** 사거리를 묻는 곳이 9군데인데 그중 7곳이
`AttackReach` 를 안 지난다(unit 1 에서 수렴). 그 상태로 자를 바꾸면 과거 교착이 세 번째로 재발한다.

과거 2회(2026-08-12, `summon-patrol-defender` unit 11) 모두 **사람 눈으로만** 발견됐다 —
정지 187프레임 중 182프레임이 적과 셀 거리 1이었다. 그물 없이 unit 4 에 들어가지 않는다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/RangePredicateMirrorTest.cs` — 신규
- `Assets/_Project/Tests/EditMode/AttackReachTests.cs` — 경계 케이스 보강
- **프로덕션 코드 변경 0**

## 구현

**(a) 술어 지도를 테스트가 소유한다.** 아래 9곳을 상수 표로 들고, 경계 근처에서 같은 답을
내는지 단언한다. 표가 코드와 갈리면 그것 자체가 실패다.

| # | 위치 | 지금 무엇을 쓰나 |
|---|---|---|
| 1 | `AttackSystem.cs:594` 타겟 선정 | `InReach` |
| 2 | `AttackSystem.cs:741` 적 focus 락 | `InReach` + `KeepsLock` |
| 3 | `AttackSystem.cs:879` 방어유닛 락 | `InReach` + `KeepsLock` |
| 4 | `AttackSystem.cs:925` committed 재판정 | `InReach` |
| 5 | `EnemyAiStateSystem.cs:176,200` | `InReach` + `KeepsLock` |
| 6 | `PatrolAreaMath.cs:171-172` | `InCellRange` **AND NOT** `InWorldReach` (분해 사용) |
| 7 | `AttackSystem.cs:781·812·1527·2134` | **셀 체비셰프 인라인** |
| 8 | `EnemyAiStateSystem.cs:93` guardianInRange | **셀 체비셰프 인라인** |
| 9 | `HazardCastSystem.cs:99` · `FlowFieldBuilder.cs:188` | **셀 체비셰프 인라인** |

**(b) 락 경로가 커버리지 밖이라는 게 요점이다.** EditMode 는 술어 자체만 본다. 「락을 문
공격자가 게이트 경계로 벌어졌을 때 `AttackSystem.bestTarget` 과 `EnemyAiState` 가 같은 답을
낸다」를 PlayMode 로 고정하면 교착 클래스 전체가 덮인다.

**(c) 교착 카나리아.** `WhirlpotLiveRepro.cs:139-152` 형태를 재사용한다 — N프레임 안에 한 대도
못 때리면 실패하고 **최소 접근거리 + AI 상태 궤적**을 찍는다. 얼어붙은 유닛도 스폰·컴포넌트
단언은 전부 통과하므로, 실패 메시지에 그 두 값이 없으면 원인을 못 찾는다.

## 완료 기준

- [ ] 현행 코드에서 **초록**. red 가 나오면 이 spec 이 만든 결함이 아니라 **이미 있던 것** —
      unit 1/2 로 이관하고 여기서는 red 사유를 기록만 한다.
- [ ] 카나리아가 인위적 교착(순찰병 `aggroCapacity` > 0 저작)을 실제로 잡는지 1회 확인.
- [ ] PlayMode 라 상시 실행 아님. **전환 중에는 이 파일만 개별 지정**해서 돌린다(전체 8분).
