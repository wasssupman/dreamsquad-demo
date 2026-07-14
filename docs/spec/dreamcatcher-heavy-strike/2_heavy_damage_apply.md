# 2 — 강공 데미지 적용 (전 victim ×배율 + Threat 동기)

## 목적

unit 1 이 실어 보낸 `heavyMul` 을 실제 피해에 적용한다. 투사체는 hit-site(`ProjectileHitSystem`)에서, 멜리는 `AttackSystem` 멜리 arm 에서 **그 공격의 모든 Damage victim** 에 곱한다. `IncomingDamage` 와 `ThreatTable.TryCredit` 에 **같은 값**을 넣어 desync 을 막는다. 여기서 강공이 처음 실제 동작한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — `heavyMul` 해석 + 전 Damage victim 곱.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 멜리 arm 에서 `heavyMul` 곱.
- (선택) `Assets/_Project/Tests/EditMode/` — 회귀 스위트 재실행(heavyMul=1 무회귀). 강공 실동작은 unit 3 Play.

## 구현

### ProjectileHitSystem — 전 victim 곱

`prioMul` 해석 옆에 `heavyMul` 추가(기본 1=inert):

```csharp
// dreamcatcher-heavy-strike unit 2 — 강공은 priority(1명)와 달리 이 shot 의 모든
// Damage victim(direct/splash/bounce/TileAoe)에 곱한다. state 에 실려 bounce
// re-home 후에도 유지. 기본 0 → 1.
float heavyMul = projectile.ValueRO.heavyDamageMul > 0f ? projectile.ValueRO.heavyDamageMul : 1f;
```

네 Damage 지점 전부 `heavyMul` 곱(각 지점의 dmg 변수 = IncomingDamage + TryCredit 공통이라 동기 유지):

1. SingleSplash outputs Damage: `(target==prio ? mag*prioMul : mag) * heavyMul`
2. SingleSplash no-outputs: `(target==prio ? dmg*prioMul : dmg) * heavyMul`
3. Splash secondary: `splashDamage = damage * splashDamageMul * heavyMul`
4. TileAoe victim: `(victims[i]==prio ? dmg*prioMul : dmg) * heavyMul`

bounce 는 `next = projectile.ValueRO` 로 heavyDamageMul 를 복사해 다음 hop 도 강공(계약 4 — 한 방의 모든 victim). base decay(bounceDamageMul)와는 곱으로 합성.

### AttackSystem 멜리 arm — cleave 전 대상 곱

멜리 Damage 케이스(`fmPrioMul` 적용 직후, `AppendToBuffer` 앞):

```csharp
// 응축된 일격 (unit 2) — 멜리 cleave 전 대상에 강공 배율(전 victim). heavyMul=1 이면
// 무영향. dmg 가 IncomingDamage + TryCredit 공통 → threat 동기(HIGH). pre-scan(unit 1)
// 이 이 공격의 heavyMul 을 이미 산출.
dmg *= heavyMul;
```

## 합성·비적용 규칙 (계약 4)

- 일반 `damageMul`·`DamageVsCc`·priority(`fmPrioMul`)와 **곱**으로 합성.
- Damage victim 에만. Heal/ApplyStat/ApplyStack 미적용(그 케이스는 안 건드림).
- 투사체 `projectileDamage`(발사 시 합산)에는 **미리 곱하지 않음** — hit-site 에서만(splash/bounce 과증폭·threat desync 방지, unit 1 이 base 유지).
- `AttackOutputLog`: 멜리는 곱해진 dmg 로그. 투사체는 base(hit-site 적용이라 발사 로그엔 미포함) — eye `priorityDamageMul` 선례 동일, 텔레메트리 채널이라 허용.

## 완료 기준

- [x] ProjectileHitSystem 네 Damage 지점(direct outputs / direct no-outputs / splash / TileAoe) 전부 `heavyMul` 곱, 각 dmg 변수 = `IncomingDamage`+`TryCredit` 공통(threat 동기). bounce 는 state 복사로 hop 마다 유지.
- [x] AttackSystem 멜리 arm cleave 전 대상 `heavyMul` 곱, dmg 공통 → threat 동기.
- [x] compile green — `dotnet build` 런타임 0오류 + Unity 콘솔 컴파일 에러 0. 2026-07-14.
- [x] **무회귀(구조 보장)**: heavyMul 기본 1(inert) → HeavyStrike 미부착 유닛/기존 카드/적 경로 동작 불변. (전체 EditMode 스위트 실행은 unit 3 Play 검증에 합류 — 유저 플로우상 테스트 폴링 생략.)
- [ ] (unit 3 Play) 실제 5회째 공격 데미지 2배 육안·로그 — 근접/투사체/splash 각각.
