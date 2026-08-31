# 1 — 사거리 술어 수렴 (자 무변경)

## 목적

사거리를 묻는 코드가 **전부 `AttackReach` 를 지나게** 한다. 자는 손대지 않는다 —
체비셰프 그대로이고 **무회귀가 완료 기준**이다. 이게 이 spec 의 실제 작업량이며,
안 하면 unit 4 의 스위치가 곧 교착 스위치가 된다.

## 변경 대상 (7곳 · 전수 확인 2026-08-31)

| 파일:라인 | 게임 안의 질문 | 지금 |
|---|---|---|
| `Combat/AttackSystem.cs:781` | 어그로된 적이 자기 가디언을 때릴 수 있나 | 셀 인라인 |
| `Combat/AttackSystem.cs:812` | 「끝을 보는 눈」이 최전방 락을 유지할까 | 셀 인라인 |
| `Combat/AttackSystem.cs:1527` | **다중타격 2번째 이후 대상**이 사거리 안인가 | 셀 인라인 |
| `Combat/AttackSystem.cs:2134` | 폴백 대상 랭킹의 `tileDist` | 셀 인라인 |
| `Combat/EnemyAiStateSystem.cs:93` | 적이 가디언 앞에서 **멈춰도 되나** | 셀 인라인 |
| `Effects/HazardCastSystem.cs:99` | 캐스터가 시전할 대상이 있나 | 셀 인라인 |
| `Effects/FlowFieldBuilder.cs:188` | 「사거리 안」 칸 = BFS 소스 | 셀 디스크 |
| `Movement/MovementSystem.cs:242` | 이 적이 회오리 안인가 | 셀 인라인 |
| `Bridge/BattleBridge.cs:7509` | `GridMath.ChebyshevDistance` **손복사본** | 사유 없는 중복 — **삭제** |

`FlowFieldBuilder.CollectDefenderSources` 는 `AggroChaseMath`(어그로 추격)와
`DefenderFieldSystem`(보스 사냥) **둘이 공유**한다 — 한 번 고치면 두 레인이 같이 따라온다.

⚠ **이 함수는 시그니처 확장이 필요하다.** 현재 인자가
`(walkMask, gridSize, defenderCells, rangeTiles, outSources)` 로 **셀 좌표뿐이고 `tileSize`·`origin`
이 없다.** unit 4 가 술어를 월드 거리로 바꾸면 셀→월드 변환 재료가 없어 그대로는 못 부른다.
호출자 둘(`DefenderFieldSystem` · `PatrolAreaMath.BuildAreaChaseField`)은 **둘 다 갖고 있으므로**
넘기면 된다 — unit 1 에서 미리 확장해 두고 unit 4 는 술어만 바꾼다.

⚠ 이 함수의 주석이 계약을 하나 들고 있다: 「**FSM 사거리 판정(`HasFireTarget`)과 같은 메트릭이라
소스 도달 = Engaging 전이 보장**」. 수렴이 이 동치를 깨면 적이 도착해 놓고 안 쏜다.

## 구현

- 각 지점을 `AttackReach.InReach` / `InCellRange` 호출로 교체. **식을 다시 쓰지 않는다.**
- ⚠ **이동 쪽 보정을 같이 만든다.** `PatrolAreaMath.CloseInDir` 만 「격자는 도착이라는데 공격이
  멀다고 거부」하는 교착을 갚아 준다. 어그로 추격·보스 사냥 레인에는 **그 등가물이 없다** —
  `MovementSystem` 은 `RecoveryDir == 0` 이면 그냥 멈춘다. 술어를 좁히기 전에 이 둘을 채운다.
- `AttackReach.cs` 헤더 주석의 **「소비처가 다섯」을 실제 수로 갱신**한다. 그 목록이 stale 한 것이
  이 unit 이 존재하는 이유다.
- `PatrolAreaMath.cs:154` 의 `reach = max(1, attackTileRange)` 와 `AttackSystem` 의
  `RangeToTiles(range)` 가 지금 우연히 같다(순찰병 사거리 1). 같은 자로 통일한다.

## 완료 기준

- [x] 골든 **무변화**. ⚠ 「byte-identical」로 적었던 건 **틀린 기대**였다 — 아래 정정 참조.
- [x] unit 0 안전망 초록 유지.
- [ ] ~~순찰병 SO 에 `aggroCapacity` 를 0 초과로 저작해도 **동결이 재현되지 않는다**~~
      → **여전히 열려 있다. unit 4 소관으로 이관.** 이 unit 은 그 잠복 결함을 닫지 못했고,
      형태만 «Standoff 로 멈춰 안 쏨» → «Chasing 인데 못 움직이고 안 쏨» 으로 바뀌었다.
      근거와 이관 사유는 아래 정정 2 참조.
- [ ] **명시한 9 지점이 `AttackReach` 호출로 바뀌었다**(diff 대조).
- [ ] 잔여 인라인은 **허용 목록 대조**로 판정한다 — `AttackReach.cs:49·54`(술어 본체) ·
      `TileAoe.cs:16`(unit 4 소관) · 비목표 4곳(`WaypointProgress:41` · `BlinkMath:46` ·
      `PatternTargeting:45` · `PatrolAreaMath:173` gap 값). **「0건」은 영원히 성립하지 않는다** —
      그리고 `FlowFieldBuilder:188` 은 사각 디스크 이중 루프라 그 grep 이 애초에 못 잡는다.
- [ ] 신규 인라인 금지는 계약 11 로 리뷰가 진다.

---

### 진행 기록 — 완료 2026-08-31

수렴한 곳(성격이 셋으로 갈렸다 — 하나로 뭉뚱그리면 틀린다):

| 성격 | 지점 | 수렴 대상 |
|---|---|---|
| **사거리 판정** | `AttackSystem:781`(어그로 sticky) · `:812`(frontmost 락) · `:1527`(다중타격 2번째 이후) · `EnemyAiStateSystem:93`(멈춰도 되나) · `HazardCastSystem:99`(캐스트) | `AttackReach.InReach` |
| **거리 값** | `AttackSystem:735·878`(→`KeepsLock`) · `:2134`(→`Candidate.tileDist`) · `EnemyAiStateSystem:174` | `GridMath.ChebyshevDistance` |
| **장 멤버십** | `MovementSystem:242`(회오리) | `TileAoe.IsInTileRange` — 사거리가 아니다 |
| **손복사본** | `BattleBridge:7509` | **삭제**(`GridMath` 로 대체) |

**`FlowFieldBuilder.CollectDefenderSources` 는 수렴하지 않았다** — 결정 4 로 셀 기반을 유지하므로
자가 안 바뀌고, 그 함수는 사각 이중 루프라 **복제된 산식이 애초에 없다**(제거할 미러가 0).
시그니처 확장도 불필요해졌다. 건드리면 BFS 핫 경로만 흔든다.

`HazardCastSystem` 은 `bothContinuous` 를 **`PathFollowState` 조회로** 넘긴다. 처음엔 `false`
리터럴이었는데(「오늘 캐스터는 전부 타일 고정」), 그건 **콘텐츠 사실이지 코드의 성질이 아니다** —
게다가 리터럴은 이 unit 의 판정 수단인 「인라인 사거리 판정 grep」에 **구조적으로 안 걸려**
조용히 다른 자가 된다. 타겟 쪽은 조회하지 않는다: 그 쿼리가 `PathFollowState` 를 요구하므로
**정의상 전원 연속**이라 `bothContinuous ≡ casterIsContinuous` 다. 오늘 결과는 동일하다
(`HazardCastState` 부착은 일반 배치 경로 1곳뿐 · `CreatePatrolEntity` 는 안 붙인다).

**검증**
- [x] 골든 7건 **전건 통과**(이벤트·킬 수 재생성 시점과 동일) — 자 무변경이 증명됐다
- [x] EditMode 2659건 / 실패 2건 — 둘 다 선행 실패(`boomerang`·`bomb_man`), 새 빨강 0
- [x] `Battle/` 잔여 인라인 **7건이 전부 허용 목록**: 술어 본체 3(`AttackReach`×2·`TileAoe`) +
      비목표 4(`BlinkMath` 링셸 · `PatternTargeting` 랭킹 · `WaypointProgress` 격자위상 ·
      `PatrolAreaMath` gap 값). **사거리 판정 인라인 0건.**

---

### 정정 (투트랙 리뷰 후 · 2026-08-31)

**정정 1 — 「무회귀」는 거짓이다. `AttackSystem` 다중타격 2번째 이후 대상이 좁아진다.**

커밋 제목과 위 검증의 「자 무변경」은 «`AttackReach` **본체**를 안 고쳤다» 는 뜻으로만 참이다.
판정 **결과**는 한 곳에서 오늘 데이터로도 달라진다 — 다중타격의 2번째 이후 대상 선정이다.
공격자가 **연속 이동 적**(`attackTargetCount ≥ 2` 저작 7종 — `Enemy_Whirlpot` 은 10)이고
타겟이 **순찰병**(`PathFollowState` 보유)이면 `bothContinuous == true` 가 성립해, 그 대상만
셀 기준이었던 것이 이제 월드 게이트를 한 번 더 지난다.

**이건 결함이 아니라 의도한 수렴이다** — 1번째 대상은 이미 같은 게이트를 지나고 있었다.
「내가 때릴 수 있는 적」의 정의가 **발마다 달랐던 것**이 문제였고, 그걸 없앤 게 이 unit 이다.
정정하는 것은 코드가 아니라 **기록**이다: 이 unit 은 「순수 리팩토링」이 아니라
**「한 곳에서 판정을 좁히는 변경」** 이다. 그 문장을 그대로 두면 다음 사람이 오독한다.

**골든이 통과한 이유도 확인했다 — 코퍼스 7건에 순찰병이 0기였다.** 순찰병은 소환사가 만드는데
씬 기본 덱에 소환사가 없어, 이 조합이 **구조적으로 코퍼스에 못 들어왔다.** 통과는 「무회귀」가
아니라 「이 경로가 코퍼스에 없었다」는 뜻이다.

→ **안전망을 코드로 닫았다.** `SimHarnessRunner.Scenario` 에 시나리오별 덱 축(`defenderIds`)을
넣고 소환사 덱 시나리오 `summoner` 를 코퍼스에 추가했다. 이제 「연속 공격자 × 연속 타겟」이
코퍼스 안에 실재하므로, unit 4 가 자를 바꾸면 **여기가 제일 먼저 말한다.**

**정정 2 — 어그로 추격 레인의 동결은 닫히지 않았다(완료 기준 3).**

spec 은 「술어를 좁히기 전에 이동 보정을 채운다」고 못박았는데, 이 unit 은 발사·정지 판정
(`EnemyAiStateSystem` guardianInRange · `AttackSystem` 어그로 sticky)만 좁히고 `MovementSystem`
추격 분기에는 보정을 넣지 않았다. 순찰 이동에는 그 보정이 있다(`PatrolAreaMath.CloseInDir`).

**오늘 라이브는 아니다** — 그 경로가 성립하려면 `PathFollowState`(연속 이동) + `aggroCapacity > 0`
(가디언)을 **동시에** 가진 유닛이 있어야 하는데 저작이 0종이다(가디언 3종 = 배스티온·가디언·
실드셔틀은 전부 타일 고정 · 순찰병은 `aggroCapacity: 0`). 즉 `bothContinuous` 가 그 두 지점에서
**항상 false** 라 오늘 판정은 불변이다.

**그래서 보정을 지금 만들지 않는다.** 오늘 도달 불가한 경로를 위해 `MovementSystem` 핫 루프에
lookup 을 늘리고 죽은 분기를 세우는 건 제약 8 이 막는 구조다. 대신 두 가지를 했다:
- `MovementSystem` 의 「도착 셀은 정의상 발사 조건 충족」 주석을 **조건부로 정정**했다 —
  그 «정의상» 은 발사 판정이 셀 기준일 때만 성립하고, 지금은 아니다. 그 주석이 다음 사람에게
  거짓 보증이 되는 것이 이 정정의 이유다.
- 완료 기준 3 을 **unit 4 로 이관**했다. 자를 실제로 바꾸는 것이 그 unit 이고, 보정도 거기서
  같이 만드는 것이 순서상 맞다.

**정정 3 — `PatrolAreaMath` 의 `reach` 통일은 하지 않는다(사유 기록).**

spec 구현 절이 `max(1, attackTileRange)` 와 `RangeToTiles` 의 통일을 요구했지만, 검토 결과
**안 하는 게 맞다.** `BuildAreaChaseField` 의 소스 수집이 같은 클램프를 쓰므로 도착 판정만
바꾸면 사거리 0 유닛에서 «BFS 는 사격 칸을 세우는데 도착 판정은 후보를 못 찾는» 교착이 난다.
둘 다 바꾸는 것은 순찰 이동의 성격 변경이라 이 unit(술어 수렴) 밖이다. 코드에 같은 사유를 남겼다.
