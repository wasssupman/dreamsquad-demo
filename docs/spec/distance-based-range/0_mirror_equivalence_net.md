# 0 — 기준선 굽기 + 사거리 술어 미러 동치성 안전망

## 목적

**술어를 하나도 안 바꾸고, 현행 동작을 못박는다.** 사거리를 묻는 곳이 9군데인데 그중 7곳이
`AttackReach` 를 안 지난다(unit 1 에서 수렴). 그 상태로 자를 바꾸면 과거 교착이 세 번째로 재발한다.

과거 2회(2026-08-12, `summon-patrol-defender` unit 11) 모두 **사람 눈으로만** 발견됐다 —
정지 187프레임 중 182프레임이 적과 셀 거리 1이었다. 그물 없이 unit 4 에 들어가지 않는다.

## ⚠ 먼저 — 골든 기준선을 한 번 굽는다

**오늘 골든 7건은 이미 red 다.** 마지막 재녹화 `682d163d` 이후 **203 커밋**이 쌓였고, 그 사이
`DefenderUnitData` 에 `footprintWidth`/`footprintHeight` **필드가 추가**되어 `configHash` 가 바뀌었다
(`Describe` 가 `GetFields` 로 접고 `[defenders]` 가 `PutAsset` 으로 돈다). `DiffAgainst` 는
해시 불일치에서 **첫 줄 short-circuit** 하므로 이벤트 비교로 내려가지도 않는다.

→ **이 상태로는 units 0~2 의 「무변화」를 증명할 수 없다.** 현행 코드 기준으로 한 번 굽는다.

- Play 중 `Washup/Battle/Sim Harness/Regenerate Golden Corpus`
- ⚠ **무관 dirty 를 반드시 격리**한다 — 안 하면 남의 WIP 가 기준선에 구워진다
- `docs/spec/battle-sim-extraction/golden-corpus.md` 가 같이 나온다. **골든만 담은 커밋**으로 분리
- 이 굽기는 **자를 안 바꾼 상태**의 기준선이다 — units 1~3 이 이것에 대해 무변화를 증명한다

## 변경 대상

- `Assets/_Project/Tests/PlayMode/RangePredicateMirrorTest.cs` — 신규
- `Assets/_Project/Tests/EditMode/AttackReachTests.cs` — 경계 케이스 보강
- **프로덕션 코드 변경 0**
- `Assets/_Project/Tests/Golden/*.trace.txt` — **선행 굽기 1회**(별도 커밋)

## 구현

**(a) 이 테스트는 «상대 동치성»만 단언한다 — 절대값을 박지 않는다.**
자가 바뀌어도(unit 4) **초록이어야 한다.** 절대값 회귀는 골든이 진다(계약 13).
아래 표의 좌표는 **픽스처이지 기대값이 아니다.** 그리고 계약 4 이후에는
「같은 답」이 아니라 **「같은 임계끼리 같은 답」**을 본다(획득 ↔ 획득, 유지 ↔ 유지).

**(b) 술어 지도를 테스트가 소유한다.** 아래 9곳을 상수 표로 들고, 경계 근처에서 같은 답을
내는지 단언한다. 표가 코드와 갈리면 그것 자체가 실패다.

| # | 위치 | 지금 무엇을 쓰나 | 임계 |
|---|---|---|---|
| 1 | `AttackSystem.cs:594` 타겟 선정 | `InReach` | 획득 |
| 2 | `AttackSystem.cs:741` 적 focus 락 | `InReach` + `KeepsLock` | 유지 |
| 3 | `AttackSystem.cs:879` 방어유닛 락 | `InReach` + `KeepsLock` | 유지 |
| 4 | `AttackSystem.cs:925` committed 재판정 | `InReach` | 유지 |
| 5 | `EnemyAiStateSystem.cs:176`(유지) · `:200`(획득) | `InReach` + `KeepsLock` | 둘 다 |
| 6 | `PatrolAreaMath.cs:171-172` | `InCellRange` **AND NOT** `InWorldReach` (분해 사용) | 획득 |
| 7 | `AttackSystem.cs:781·812·1527·2134` | **셀 체비셰프 인라인** | 혼재 |
| 8 | `EnemyAiStateSystem.cs:93` guardianInRange | **셀 체비셰프 인라인** | 획득 |
| 9 | `HazardCastSystem.cs:99` · `FlowFieldBuilder.cs:188` · `MovementSystem.cs:242`(토네이도) | **셀 체비셰프 인라인** | 획득 |
| 10 | **광역 축** — `TileAoe` 소비처 6곳(`AggroTargeting:55` · `DefenderDensity:41` · `BounceRetarget:70` · `ProjectileHitSystem:733` · `EcsSkillContext:440` · `BattleBridge:4617`) + `AllyBuffFieldSystem:64` | 셀 체비셰프 | 획득 |

**(c) 락 경로가 커버리지 밖이라는 게 요점이다.** EditMode 는 술어 자체만 본다. 「락을 문
공격자가 게이트 경계로 벌어졌을 때 `AttackSystem.bestTarget` 과 `EnemyAiState` 가 같은 답을
낸다」를 PlayMode 로 고정하면 교착 클래스 전체가 덮인다.

**(d) 교착 카나리아.** `WhirlpotLiveRepro.cs:139-152` 형태를 재사용한다 — N프레임 안에 한 대도
못 때리면 실패하고 **최소 접근거리 + AI 상태 궤적**을 찍는다. 얼어붙은 유닛도 스폰·컴포넌트
단언은 전부 통과하므로, 실패 메시지에 그 두 값이 없으면 원인을 못 찾는다.

## 완료 기준

- [ ] 현행 코드에서 **초록**. red 가 나오면 이 spec 이 만든 결함이 아니라 **이미 있던 것** —
      unit 1/2 로 이관하고 여기서는 red 사유를 기록만 한다.
- [ ] 카나리아가 인위적 교착(순찰병 `aggroCapacity` > 0 저작)을 실제로 잡는지 1회 확인.
- [ ] PlayMode 라 상시 실행 아님. **전환 중에는 이 파일만 개별 지정**해서 돌린다(전체 8분).
