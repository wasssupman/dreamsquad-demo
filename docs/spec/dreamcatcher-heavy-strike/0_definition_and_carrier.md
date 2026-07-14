# 0 — 정의 계층 + 투사체 캐리어 필드

## 목적

강공을 표현할 **정의 계층 어휘 1개**와, 그 배율을 투사체가 나를 **inert 캐리어 필드**를 추가한다. 이 단위는 순수 데이터/컴포넌트 필드만 — 실제 발동/적용 로직은 unit 1·2. 모든 기존 스폰은 기본값으로 무회귀(inert).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind` 에 `HeavyStrike` append (=13).
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — `heavyDamageMul` 필드 추가.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` — `heavyDamageMul` 필드 추가.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — drain(`~2308`)에서 `heavyDamageMul` verbatim 복사.

## 구현

### 1. `DcPayloadKind.HeavyStrike`

`SelfStatBuff = 12` 뒤에 append (append-only — 기존 에셋 int 순서 보존).

```csharp
// dreamcatcher-heavy-strike unit 0 — 응축된 일격. AttackN(period=N) 으로 발동하는
// 강공: 추가 캐리어를 발사하는 다른 payload 와 달리 그 발동 공격 자신의 출력
// 데미지를 magnitude 배(2.0=×2)로 만든다. 전 victim(근접 cleave/splash/bounce)
// 에 적용 — primary 한정인 끝을 보는 눈과 다르다. 발동은 unit 1(AttackSystem),
// 적용은 unit 2(melee + ProjectileHitSystem, hit-site 배율).
HeavyStrike = 13,
```

- `DcPayloadSpec.magnitude` 를 배율로 재사용(2.0 = ×2). 신규 필드 없음.
- 기존 `magnitude` 주석에 HeavyStrike 의미 한 줄 보강.

### 2. 투사체 캐리어 `heavyDamageMul`

`ProjectileSpawnRequest` 와 `ProjectileState` 양쪽에 동일 필드. `priorityDamageMul` 바로 뒤에 배치(패턴 일치).

```csharp
// dreamcatcher-heavy-strike unit 0 — 강공 전-victim 배율. priorityDamageMul(끝을
// 보는 눈)이 priority victim 한 명에만 곱하는 것과 달리, 이 배율은 이 공격의 모든
// Damage victim 에 곱한다(강공은 한 방 통째). 기본 0 = 비활성(실적용 mul>0?mul:1).
// launch 시 request→state verbatim 복사, bounce re-home 후에도 유지. 적용 unit 2.
public float heavyDamageMul;
```

- `ProjectileSpawnRequest`: `priorityDamageMul` 다음 줄.
- `ProjectileState`: `priorityDamageMul` 다음 줄.

### 3. Bridge drain 복사

`BattleBridge.cs` 의 `new ProjectileState { ... priorityDamageMul = req.priorityDamageMul, }` 뒤에:

```csharp
// dreamcatcher-heavy-strike unit 0 — 강공 전-victim 배율 verbatim 복사(기본 0=inert).
heavyDamageMul = req.heavyDamageMul,
```

## 완료 기준

- [x] `DcPayloadKind.HeavyStrike = 13` 존재, 기존 멤버 값 불변(append-only).
- [x] `ProjectileSpawnRequest.heavyDamageMul` / `ProjectileState.heavyDamageMul` 존재, 기본값 0.
- [x] drain 이 `req.heavyDamageMul` 를 state 로 복사.
- [x] compile green — `dotnet build Wassup.Runtime.csproj` 오류 0(경고 14 기존). 2026-07-14.
- [x] 이 단위만으로는 동작 변화 0 (필드 미소비, 모든 스폰 inert). AttackSystem/HitSystem 미수정 확인.
