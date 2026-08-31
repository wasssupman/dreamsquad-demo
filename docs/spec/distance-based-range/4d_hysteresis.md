# 4d — 히스테리시스와 어설션

## 목적
경계 진동을 막는다. **마지막에 둔다** — 4a~4c 로 자만 바꿔 놓고 「진동이 실제로 나는가」를
관측한 뒤 폭을 정한다. 지금 `h` 의 값 근거는 어디에도 없다.

## 변경 대상
- `Combat/TargetPersistence.cs:29` — **시그니처 변경 필수**: `KeepsLock(bool, int, int)` → 실수 gap + 히스테리시스
- 호출부 **3곳**: `AttackSystem:743` · `AttackSystem:881` · **`EnemyAiStateSystem:179`**
- 설정 검증(어설션)

## 구현
획득 `gap ≤ N`, 유지 `gap ≤ N + h`. **원칙: 「여기서 쏠 수 있나 · 멈춰도 되나」는 획득,
「이미 문 것을 놓나」는 유지.** 이동 정지 판정에 유지 임계를 쓰면 **적이 사거리 밖에서 멈춘다.**

| 지점 | 임계 |
|---|---|
| `AttackSystem:594` 타겟 선정 | 획득 |
| `AttackSystem:741·879` 락 유지 (+`KeepsLock`) | 유지 |
| `AttackSystem:925` committed 재판정 | 유지 |
| `EnemyAiStateSystem:200` `HasFireTarget` | 획득 |
| `EnemyAiStateSystem:176` 락 미러 | 유지 |
| `PatrolAreaMath.CloseInDir` | 획득 |
| `FlowFieldBuilder.CollectDefenderSources` | 획득 |
| `AllyBuffFieldSystem` 오라 멤버십 | 유지 |

⚠ **「장판」은 대상이 아니다.** 해저드 존 멤버십은 `ZoneApplySystem.cs:55` 의 **셀 해시 조회**라
거리 축이 없다 — 히스테리시스를 걸 대상 자체가 없다(비목표 「해저드 형상」과 같은 사유).

⚠ **`h` 는 코드 상수다.** `sceneKnobs`(`BattleBridge.cs:3303`)에 **등재하지 않는다** — 계약 9.
등재하면 `configHash` 가 움직여 골든 red 가 「조건 드리프트」로 읽히고, 계약 13 의 「관측 도구」
성격이 무너진다. 튜닝이 필요해지면 그건 SO 의 문제이고 이 spec 범위 밖이다.

**어설션**: `프레임당 최대 변위 ≤ h`. 적 속도 상한이나 `tileSize` 가 바뀌면 조용히 깨지는
독립 조건이라 기하에 맡기지 않는다(반폭은 경계의 *위치*를 옮길 뿐 *무디게* 하지 않는다).

## 완료 기준
- [ ] 4c 시점에 관측한 진동이 사라진다(경계에 적을 세워 프레임별 발사 연속성 확인).
- [ ] 어설션이 인위적 위반(적 속도 상한 상향)을 실제로 잡는다 — 1회 확인.
- [ ] unit 0 미러가 **「같은 임계끼리 같은 답」**으로 초록.
- [ ] 락이 늦게 풀려 깨지는 것이 없는지: 도발 해제·마스크 변경(`target-persistence` 계약) 회귀 확인.
