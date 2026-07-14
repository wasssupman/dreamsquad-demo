# 4 — Handoff Summary: dreamcatcher-heavy-strike (응축된 일격)

## Commit

- `b2dc9edb` unit 0 — HeavyStrike payload + 투사체 heavyDamageMul 캐리어
- `ae0384ff` unit 1 — bake + AttackN 강공 배율 산출·전달
- `35e443cc` unit 2 — 강공 전-victim 데미지 적용 + Threat 동기
- (이 커밋) unit 3 — 카드 에셋(SO+아트+catalog) + Play/로그 검증 + handoff

## Implemented

- **응축된 일격**: 부착 유닛의 `N`(=5)회째 기본 공격이 피해 `M`(=2.0)배가 되는 Unit 카드. 크리티컬 명칭 없이 "강공".
- `DcPayloadKind.HeavyStrike`(=13) append. `AttackN` 트리거 + `DcTriggerSlot.counter` 재사용 — 새 시스템/큐 0.
- `DcTrigger.WouldFire` 순수 predicate — AttackSystem RESOLVE pre-scan 이 카운터를 안 건드리고 "이번이 N회째"를 예측(카운터 쓰기 소유는 dc-trigger loop).
- 배율은 hit-site 적용: 투사체는 `ProjectileState.heavyDamageMul` 캐리어 → `ProjectileHitSystem` 이 **전 victim**(direct/splash/bounce/TileAoe)에 곱, 멜리는 `AttackSystem` arm 이 cleave 전 대상에 곱. `IncomingDamage`==`ThreatTable.TryCredit`(desync 없음).
- Bridge bake 가드: `AttackN` + `magnitude>1` + `HasPositiveDamageOutput`(eye 재사용) host 에서만. 아니면 skip+warn.
- 카드 SO `Card_HeavyStrike.asset`(guid `55b4f3ae2e2646b3a1963e2f9170583a`) + placeholder 아트 `dreamcatcher_card_23`(guid `a2546ca7be13ed84aa75f0181a61a219`) + catalog 등록(26장).

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `HeavyStrike` enum.
- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — `WouldFire`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — bake 가드.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — pre-scan + 투사체 캐리어 세팅 + 멜리 곱 + dc-loop no-op.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — 전 victim ×heavyMul.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/{ProjectileSpawnRequest,ProjectileState}.cs` — `heavyDamageMul` 필드.

## Verified

- compile: `dotnet build` 런타임/EditMode asmdef 0오류, Unity 콘솔 0 error.
- EditMode: `DcTriggerTests` 에 WouldFire↔Tick 일치 2케이스(units 0·1 EditMode 런 TestResults 저장 확인).
- **Play + 로그(2026-07-14)**: 배스티온(근접) #1~9=31.0 평타 / #10=62.0 정확히 ×2.00(부착 후 5회째). 전체 로그 유일 exact-2.0×, 무회귀. → 근접 강공 확증.

## Notes

- **로그 검증은 근접만 가능**: 투사체 강공은 hit-site 적용이라 `AttackOutputLog`(발사 시점, base)엔 안 남는다 — 의도된 설계(splash/bounce 과증폭·Threat desync 방지). 투사체 ×2 는 화면 데미지 숫자로만 확인.
- 카운터는 **부착 시점부터** 카운트 — 스폰이 아니라. 부착 전 공격은 강공 대상 아님(로그의 #1~5 평타가 이 때문).
- `magnitude` 는 배율(2.0=×2). 일반 damageMul·DamageVsCc·priority 와 곱 합성.
- 아트는 **placeholder** — 실아트 배정 후 교체(guid 유지 교체) + 재확인.

## Follow-up

- **실아트 배정** — `dreamcatcher_card_23.png` placeholder 교체.
- **특수 데미지 폰트(사이버펑크) sibling 스펙** — README 후속 후보 참조. 강공/드림캐쳐 투사체/CC·스택 데미지를 source-kind 태깅 → 강렬 팔레트 + 커스텀 TMP 셰이더. 강공이 첫 소비자.
- 투사체 강공의 자동(로그/테스트) 회귀 커버 — 현재 근접만 로그 검증. PlayMode 통합 테스트 검토.
- 밸런스(period/magnitude) 튜닝, 기본 덱 편입 여부.
