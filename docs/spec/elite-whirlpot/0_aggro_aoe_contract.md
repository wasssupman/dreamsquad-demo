# 0 — 어그로 광역 접기 철회 (계약을 primary 로 좁힌다)

> **단독 커밋.** 보스 짱쎈을 포함한 기존 적 5종이 같은 코드를 타므로, 되돌릴 때
> 팽이 콘텐츠와 함께 딸려가면 안 된다(`enemy-fire-stack-shooter` unit 0 · `elite-enemy-tier`
> unit 3 선례).

## 목적

`AttackSystem` 이 어그로된 적의 `attackTargetCount` 를 **1 로 강제**하는 것을 제거한다.
어그로는 **primary 선정만** 지배하고, **광역 폭은 어그로와 무관**하다.

이 spec 이 이걸 건드리는 이유는 팽이(`attackTargetCount 10`)가 가디언에게 붙잡히는 순간
회오리가 단일 타격으로 접히기 때문이다. 그런데 조사해 보면 **그 강제 자체가 계약이 아니었다.**

- `aggro-targeting` 계약 4 = *"어그로된 적은 기존 타게팅을 전부 버리고 **타겟**=링크된 가디언으로
  고정"* — **단수**다. 계약이 말한 것은 primary 선정이다.
- 광역 폭까지 줄인 것은 unit 8 의 **MEDIUM 2**(Codex 리뷰 2026-06-18)이고, 그 완료 기준에
  **테스트가 없다.** 어그로 테스트 4종(`AggroPolicyTests`·`AggroStateSystemTests`·
  `AggroChaseMathTests`·`EnemyTargetPriorityTests`)에 `attackTargetCount` 가 등장하지 않는다.
  즉 계약이 아니라 **미검증 판단 한 줄**이었다.
- 그 판단이 만든 실제 결과가 도발과 무관하다 — **어그로가 적의 공격 «형태» 를 바꾼다.**
  광역 적이 붙잡히면 단일 적이 되어, 안 붙잡았을 때보다 **덜** 때린다. 숨은 방어 버프다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `desiredCount` 산출(≈1333행)
- `docs/spec/aggro-targeting/README.md` — 계약 4 를 «primary 한정» 으로 명시
- `docs/spec/aggro-targeting/8_review-fixes.md` — MEDIUM 2 철회 기록
- `Assets/_Project/Tests/EditMode/` — 이번엔 **테스트로 고정**

## 구현

`desiredCount` 를 어그로와 무관하게 `math.max(1, attackTargetCount)` 로 만든다.

★**유지해야 하는 것 — sticky primary override(unit 5).** 어그로면 `bestTarget` = 링크 가디언이고
**사거리 밖이면 미발사(`Entity.Null`)** 다. 이 배타성은 load-bearing 이다: 「가디언 없으면
최근접」으로 풀면, 가디언에게 걸어가는 도중 옆 방어유닛이 사거리에 들어오는 순간
`engageMovement: Halt` 로 그 자리에 멈춰 싸우고 **가디언에 영영 도착하지 않는다.**
「적이 스스로 가디언으로 보행 → 가디언 타일에 겹쳐 정지」가 어그로 루프의 뼈대다.

즉 **지우는 것은 광역 폭 강제 한 줄뿐**이다. 유닛별 예외 플래그도, `AttackState` 신규 필드도,
신규 payload 도 만들지 않는다 — 규칙 하나, 예외 0.

**테스트(신규)** — 어그로 걸린 `attackTargetCount = 2` 적이 **가디언과 이웃 방어유닛 둘 다**에
`IncomingDamage` 를 넣는다. 대조군으로 primary 가 여전히 가디언임을 같이 단언해 «override 는
살아 있다» 를 고정한다(그것까지 지우면 어그로 루프가 깨진다).

## 파급 (기존 콘텐츠)

`attackTargetCount > 1` 인 적 **5종**: `Enemy_Basic` 2 · `Enemy_Tanker` 2 ·
`Enemy_WaypointBasic` 2 · `Enemy_WaypointBasicAlt` 2 · `Enemy_Boss_Jjangssen` 3.
어그로에 걸린 동안 이들이 가디언 + 최근접 1~2기를 다시 때린다.

방어유닛(`attackTargetCount` 2~3 인 5종)은 **영향 없다** — `Aggroed` 는 적에게만 붙는다.

성격은 **버프가 아니라 baseline 복원**이다. 안 붙잡았으면 원래 때렸을 유닛을 때린다.
다만 가디언의 체감 방어력은 내려가므로 Play 확인 대상이다.

## 완료 기준

- [ ] Unity 컴파일 에러 0 · 콘솔 에러 0
- [ ] 신규 EditMode 통과 — 어그로 적의 광역이 이웃까지 닿고, **primary 는 여전히 가디언**
- [ ] EditMode 전량 — 신규 실패 0(기존 4건 맵 폭 계약은 무관)
- [ ] `aggro-targeting` README 계약 4 와 unit 8 문서가 갱신됐다 — 다음 사람이 같은 확장을 다시 하지 않도록
- [ ] Play smoke: 가디언이 `Enemy_Tanker`(count 2)를 붙잡았을 때 **가디언에 계속 어그로가 유지되고** 적이 걸어가지 않는다(override 무회귀)
