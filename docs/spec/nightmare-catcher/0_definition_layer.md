# 0 — 정의 계층 확장 (enum + spec 필드)

## 목적

나이트매어캐쳐 두 메커닉이 참조할 **어휘**를 정의 계층에 추가한다. 이 계층은 ECS 무참조·컴파일만이 완료 기준(dreamcatcher-unit-trigger 계약 1·2 유지).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`

## 구현

### 트리거 enum (append)

```csharp
public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold }
```

- **PeriodicTimer** — 매치 경과시간 기준 주기 발동. accumulator 가 `periodSeconds` 도달 시 1회 발동 후 리셋. **`periodSeconds<=0` 이면 발동 안 함**(가드).
- **HealthThreshold** — 최대체력 대비 누적 소실이 `fraction` 경계를 넘을 때마다 발동(반복·하향 엣지·래치). **`fraction<=0` 이면 발동 안 함**(가드). §3 참조.

> ⚠ 가드는 **함수 내부**(계약 9). kind 디스패치만 믿으면 새 카드가 값 누락(0) 시 매 틱 스핀-발동한다(렌즈 A HIGH/MED-3).

### 페이로드 enum (append)

```csharp
public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal, AreaBarrage, SelfBlink }
```

- **AreaBarrage** — 원격 진앙 셀 중심 TileAoe 폭격. 기존 SkyFall×TileAoe(플레이어 Meteor) 프리미티브 재사용. §2.
- **SelfBlink** — 시전자 자신을 지정 위치로 순간이동. **신규 세만틱**(Movement 소유 위치 쓰기 → Combat→Movement seam). §3.

### DcTriggerSpec 필드 (append, back-compat)

```csharp
public struct DcTriggerSpec
{
    public DcTriggerKind kind;
    public int period;          // AttackN/OnDamagedN: N회
    public float periodSeconds; // PeriodicTimer: 주기 초. 기본 0 = 기존 카드 inert
    public float fraction;      // HealthThreshold: 경계 간격(최대체력 비율, 예 0.10). 기본 0
}
```

### DcPayloadSpec — 재사용 (신규 필드 최소)

- **AreaBarrage**: `magnitude`(타일당 데미지) + `tileRange`(AoE 반경) + `projectile`(SkyFall 낙하 비주얼) — **기존 필드 재사용**.
- **SelfBlink**: `tileRange`(착지 탐색 반경) 재사용. 목적지 정책은 §3 에서 고정(추가 필드 불요 판단, 두 번째 blink 변종 등장 시 재평가).

기본값 0 이면 기존 카드 에셋은 전부 inert — 직렬화 append-only 계약(계약 8) 준수.

## 완료 기준

- [ ] `DcMechanic.cs` 컴파일 (ECS/Battle 타입 무참조 유지).
- [ ] 기존 드림캐쳐 카드 에셋 재직렬화 시 값 보존(새 필드 기본 0/None).
- [ ] 이 문서 어휘가 §2·§3 로직 정의를 표현하기에 충분한지 렌즈 A 크리틱 확인.
