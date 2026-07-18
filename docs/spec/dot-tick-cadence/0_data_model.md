# 0 — DoT tick 데이터 모델 + plumbing

## 목적

DoT가 주기 tick을 표현할 수 있도록 authoring/runtime 필드를 추가하고, 존→CC 경로에 값을 흘린다. **이 단위는 행동을 바꾸지 않는다**(DotApplySystem 미변경 → 여전히 연속). 컴파일·기존 테스트 그린 유지.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/HazardEffect.cs`
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs`
- `Assets/_Project/Scripts/Battle/Effects/ZoneApplySystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs`

## 구현

### HazardEffect (SO 직렬화 필드)
```csharp
public struct HazardEffect
{
    public CcKind kind;
    public float param1;
    public float param2;
    public float restDuration;
    public float tickInterval; // NEW: >0 = 이산 tick 주기(초). 0 = 연속(레거시). DoT 전용.
}
```
append-only(맨 끝 추가) → 기존 에셋은 tickInterval=0으로 역직렬화 = 연속 유지.

### CcEffect (런타임 버퍼 필드)
```csharp
public struct CcEffect : IBufferElementData
{
    public CcKind kind;
    public float3 vector;
    public float scalar;
    public float remainingTime;
    public float tickInterval; // NEW: 0이면 연속
    public float tickTimer;    // NEW: 누적기(주기 도달 시 청크 지급). 슬롯 지속 상태.
}
```

### ZoneApplySystem.HazardEffectToCcEffect
DoT 이벤트에 `tickInterval = hazardEffect.tickInterval` 전달. `tickTimer`는 여기서 세팅하지 않음(CcApply add-path가 초기화). `remainingTime`은 기존대로 `restDuration`.

### CcApplySystem.MergeOrAdd (계약 3·4 핵심)
- **add(신규 슬롯)**: `tickInterval = incoming.tickInterval`, `tickTimer = incoming.tickInterval` (첫 tick 즉발).
- **merge(기존 슬롯 refresh)**: `tickInterval = incoming.tickInterval`, **`tickTimer`는 기존 슬롯 값 유지**(incoming 무시), `remainingTime = max(old,new)`, `scalar = incoming`. 누적기를 보존해야 매 프레임 refresh에도 tick이 진행됨.

## 완료 기준

- [x] 컴파일 그린 (신규 필드 default 0 → 기존 CcEffect/HazardEffect 생성부 무영향)
- [x] 기존 `DotApplySystemTests`·`CcApplySystemTests`·`HazardCasterTests` 전부 그린 (행동 불변)
- [x] Fire/Poison 존 위 적 데미지가 **이전과 동일**(아직 연속) — 이 단위는 표시 개선 없음, 회귀만 없으면 통과

> 확인 2026-07-18 · 커밋 aedcb66f (병합 정책은 리뷰 반영으로 CcEffectMerge 로 추출됨)
