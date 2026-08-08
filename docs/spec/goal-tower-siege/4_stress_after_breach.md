# 4. 붕괴 후 스트레스 (골 파괴 = 패배 → 유출 개통)

rev 3, 2026-08-09 — 사용자 결정. Unity 검증 인계 중 실측으로 밸런스·규칙 공백이 드러나 규칙을 확장했다.

## 목적

rev 2 는 **골 파괴 = 즉시 패배**였다. 그러면 골이 뚫린 뒤의 만회 여지가 0 이고, "골이 뚫린 뒤에도 플레이어가 만회할 수 있는가"라는 이 spec 의 검증 질문이 절반만 성립한다. 파괴를 **유출 개통**으로 바꾸고, 패배는 별도 축(스트레스 상한)이 소유한다.

## 규칙

1. **골 체력(안정도) 0 = 붕괴** — 타워가 사라지고 그 골은 유출 지점이 된다. 붕괴 순간 그 골에 붙어 공성 중이던 적은 **그 자리에서 유출로 전환**된다(때릴 대상이 없는 적이 눌러앉아 웨이브 전멸 판정을 영구히 막는 것을 방지).
2. **붕괴 후 유출 1회 = 스트레스 1.** 도달한 적은 공성하지 않고 통과·소멸한다.
3. **스트레스가 상한에 닿으면 패배** — 제한 시간 전이라도.
4. **상한 0 = 구 동작**: 골 파괴가 곧 패배. (엔드리스가 이 설정 — 유출로는 죽지 않는다.)
5. **스트레스는 붕괴 뒤에만 쌓인다.** 붕괴 전의 도달은 공성 피해이거나 돌격형 자폭 피해라 **안정도 축이 이미 그것을 센다**. 두 축이 같은 사건을 세면 안정도가 멀쩡한데 스트레스로 먼저 죽는다(실측: 안정도 799 잔량에서 패배).
6. **공격 수단 없는 적(Runner·Swift)**: 붕괴 전엔 자폭으로 안정도만 깎고, 붕괴 후엔 다른 적과 동일하게 스트레스를 올린다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_goalBreached` 상태(매치 경계 리셋), `SyncGoalStability` 붕괴 분기, `OpenGoalAfterBreach`/`LeakSiegingEnemy`/`CheckStressDefeat`, `DrainGoalEvents` 붕괴 후 유출 처리, HUD 분모 복원
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `goalStabilityMax` 기본 1000
- `Assets/_Project/Scripts/Data/Decks/*.asset` — 안정도 1000(9종), `Deck_Endless.defeatGoalReachedCount = 0`

## 계약

- **상한의 on/off 는 덱 원본값**(`defeatGoalReachedCount`), **문턱값은 `EffectiveLeakLimit()`** — HUD 분모와 같은 값이라 화면에서 검산된다.
- HUD 배지는 상한이 패배를 만들 때만 분모·위기색을 켠다(`showLimit: StressLimit > 0`).
- 붕괴 후 `EnqueueGoalTowerDamage` 는 대상이 없다 — **정상이므로 경고하지 않는다**(피해 대신 스트레스가 오르는 구간).

## 완료 기준

- EditMode 전량 그린. PlayMode `GoalStabilityTest`·`EndlessModeSmokeTest`·`TallyFlowTest` 그린. ✅ 2026-08-09
- 밸런스 실측(3분 도달 웨이브·붕괴 후 생존 시간)과 Play 육안은 `three-minute-survival/5_verification_checklist.md` §3·§5 에 남아 있다.
