# 0 — payload kind + 상태 컴포넌트 + bake

## 목적

`UltimateLeap` 을 트리거×페이로드 시스템의 정식 payload 로 등록하고, 이탈 상태를 담는
`UltimateLeapState` 를 정의한다. 이 유닛까지는 **발동 코드가 없어 런타임 무변경**.

## 변경 대상

- `Assets/_Project/Scripts/Data/DcMechanic.cs` (또는 `DcPayloadKind` 정의 파일) — enum 멤버 추가
- `Assets/_Project/Scripts/Battle/Combat/UltimateLeapState.cs` — **신규**
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` `BakeNightmareMechanics` — 슬롯 bake 통과 확인
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` · `Core/Dreamcatcher/DcApplicability.cs`
  — `SelfBlink` 이 분기하는 곳에 새 kind 의 기본 처리(카드 시스템에는 미노출이므로 명시적 제외)

## 구현

```csharp
// DcPayloadKind — 기존 멤버 뒤에 추가(순서 재배열 금지 — int 직렬화)
UltimateLeap,   // ultimate-leap unit 0 — 이탈→예고→강습. duration=예고 초, slamDamage/slamTileRange=착지 피해

namespace Wassup.Battle.Combat
{
    // ultimate-leap unit 0 — 이탈 상태(사실). 존재 = 판 밖(피격·타겟팅 차단, unit 2).
    // 공격·이동 잠금은 LeapFlight(leap-flight-state)가 담당 — 레이어 분리가 계약(README 6).
    // remaining 은 Battle 도메인 dt 로 감소(README 5). landingCell 은 발동 프레임 고정(README 4).
    public struct UltimateLeapState : IComponentData
    {
        public float remaining;      // 예고 잔여 초 (payload.duration 에서 시작)
        public int2 landingCell;     // 발동 시 고정된 착지 셀
        public float slamDamage;
        public int slamTileRange;
        public int projectileDataIndex; // 착지 VFX/슬램 연출
    }
}
```

- bake: `DcTriggerSlot` 은 `duration`·`slamDamage`·`slamTileRange`·`projectileDataIndex` 필드를
  이미 갖고 있다 — `BakeNightmareMechanics` 가 새 kind 를 기존 필드 매핑 그대로 통과시키는지 확인만.
  신규 슬롯 필드 0.
- 카드 텍스트/적용성 분기: 이 payload 는 드림캐쳐 카드로 노출되지 않는다 — `DcApplicability` 에서
  명시적으로 제외(보스 전용 arm). `HealthThresholdSystem` 의 "unhandled payload" 경고는 unit 1 이
  분기를 추가할 때까지 이 kind 에 대해 뜨면 안 되므로, **unit 0 에서는 에셋 배선을 하지 않는다**
  (배선은 unit 5).

## 완료 기준

- compile 클린 · EditMode 무회귀 · 런타임 동작 무변경(발동 경로 없음)
- enum 추가가 기존 에셋의 int 값과 충돌하지 않음(뒤에 append 확인)
