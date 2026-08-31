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

- [ ] 골든 7건 **무변화**(자를 안 바꿨으므로 byte-identical). 하나라도 움직이면 수렴이 아니라 변경이다.
- [ ] unit 0 안전망 초록 유지.
- [ ] 순찰병 SO 에 `aggroCapacity` 를 0 초과로 저작해도 **동결이 재현되지 않는다**
      (그 잠복 결함이 이 unit 으로 닫힌다).
- [ ] **명시한 9 지점이 `AttackReach` 호출로 바뀌었다**(diff 대조).
- [ ] 잔여 인라인은 **허용 목록 대조**로 판정한다 — `AttackReach.cs:49·54`(술어 본체) ·
      `TileAoe.cs:16`(unit 4 소관) · 비목표 4곳(`WaypointProgress:41` · `BlinkMath:46` ·
      `PatternTargeting:45` · `PatrolAreaMath:173` gap 값). **「0건」은 영원히 성립하지 않는다** —
      그리고 `FlowFieldBuilder:188` 은 사각 디스크 이중 루프라 그 grep 이 애초에 못 잡는다.
- [ ] 신규 인라인 금지는 계약 11 로 리뷰가 진다.
