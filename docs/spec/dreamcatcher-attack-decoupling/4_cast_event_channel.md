# 4 — 캐스터 사건 채널 (Effects → Combat)

## 목적

해저드 캐스터 4종(`attackRange: 0`)에 공격 성립 사건을 준다. 캐스트 사건은 **Effects**(`HazardCastSystem`)에서 나는데 `DcTriggerSlot` 카운터는 **Combat 소유**라(계약 7), 직접 쓰지 않고 NativeQueue 로 넘긴다.

이 단위가 끝나면 `HasEventPoint` 의 `AttackN` 조건이 **사라진다** — 모든 아키타입이 자기 사건 지점을 갖는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/CastEvents.cs` — 이벤트 struct + 싱글턴
- `Assets/_Project/Scripts/Battle/Effects/HazardCastSystem.cs` — 캐스트 성사 시 enqueue + `[UpdateBefore(AttackSystem)]`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `OnUpdate` 상단 드레인
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 3점 세트(생성/엔티티 파괴/Dispose)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — `HazardCast` 개통
- `CLAUDE.md` — NativeQueue 채널 목록 21 → 22
- 테스트: 잠금 기대값 뒤집기 + 캐스터 SO 가드

## 구현

### 채널

이벤트 struct 는 **소비자 맥락**에 둔다 — `AggroHitEvent`(Effects 소비 → Effects 폴더) 선례의 대칭이므로 Combat 소비인 이번 건은 `Wassup.Battle.Combat` 이다.

```csharp
public struct CastEvent { public Entity caster; public float3 casterPos; }
public struct CastEventsSingleton : IComponentData { public NativeQueue<CastEvent> queue; }
```

큐 수명은 **BattleBridge 소유**(`_aggroHitEventQueue` 선례 3점): `Allocator.Persistent` 생성 · `DestroyEntitiesByType<CastEventsSingleton>()` · `Dispose()`. 이 대칭이 깨지면 재진입 시 `TryGetSingleton` 이 영구 실패한다(`BattleTimeScale` 전례).

### 생산자 게이트

`HazardCastSystem` 은 캐스트가 **성사된** 지점(`bestTarget != Null && cooldownRemaining <= 0` 통과 후)에서만 enqueue 한다. 그리고 **`DcTriggerSlot` 버퍼를 가진 캐스터만** — 카드가 없는 캐스터 4종이 4초마다 쏟아내는 이벤트가 쌓이지 않게 한다(Combat 컴포넌트 **읽기**라 맥락 경계 위반 아님).

### 시스템 순서

`HazardCastSystem` 과 `AttackSystem` 은 현재 둘 다 `[UpdateAfter(MovementSystem)]` 뿐이라 **상대 순서가 미지정**이다. `HazardCastSystem` 에 `[UpdateBefore(typeof(AttackSystem))]` 을 명시해 같은 프레임 소비를 보장한다(사이클 없음 — 확인됨).

### 드레인 = `AttackSystem.OnUpdate` 상단

attacker foreach **앞**에서 드레인한다. 이유: ① 후보 스냅샷과 `ecb` 를 그대로 재사용 ② 카운터 변경이 루프 바깥에서 끝나므로 **HeavyStrike pre-scan 합성 불변식**(계약 1)에 영향이 없다. 신규 시스템 0.

stale 이벤트는 조용히 버린다 — `HasBuffer<DcTriggerSlot>` 하나로 충분하다(파괴된 엔티티는 false, 위치는 이벤트가 스냅샷으로 들고 온다). enqueue 후 드레인 전에 캐스터가 죽는 창이 있다.

대상 선정은 unit 3 의 폭탄맨 폴백과 **같은 패턴**이다(스냅샷 순회 → `eligible` = 진영 Enemy ∧ 자기 제외 → `SelectNearest`).

### 적용성

`HasEventPoint` 의 `AttackN` 케이스가 `return true` 가 된다(모든 아키타입 개통). `HostProvidesTarget` 은 그대로 — 캐스터는 여전히 대상을 안 주므로 `ProjectileToTarget` 은 `tileRange > 0` 을 요구하고 `ApplyCc/Stack` 은 영구 거절이다.

## 완료 기준

- [x] 컴파일 클린 + EditMode **1472 전원 통과**. `AttackN` 이 전 아키타입에서 `None`(사건 지점 개통 완료).
- [x] 캐스터 SO 가드: `HazardCasters_CannotAlsoCountViaResolve` — `HazardCastAbility` 보유 디펜더는 `attackRange == 0` ∧ `outputs` 없음. 깨지면 그 host 가 RESOLVE + 캐스트로 2 카운트를 먹어 계약 2 가 무너진다.
- [x] `CLAUDE.md` 채널 목록 22개 + 순서 계약 기재(같은 커밋).
- [x] **큐 수명 라이브 검증**: 테스트 매치 **재진입 2회** 후 `CastEventsSingleton` 엔티티 **정확히 1개**, `_castEventQueue.IsCreated=True`, 콘솔 에러 0. 3점 세트가 대칭이라는 증거(비대칭이면 2개가 되어 `TryGetSingleton` 이 영구 실패한다).
- [x] Play: 캐스터(FireCaster)에 비수 부착 **허용** — `Would=True`, `Apply=0`. unit 1 에서 `-1` 이던 것이 뒤집혔다.
- [ ] **남음 — 실전투 시각 확인**: 캐스트 5회마다 니들이 나가는 장면(폭탄맨과 동일 항목).

---

확인 일자 / 커밋: 2026-07-27 · `ed930e2b` (실전투 시각 확인 1건 미완)
