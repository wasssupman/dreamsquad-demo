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
  이미 갖고 있다 — 일반 필드 매핑은 그대로 통과한다. **신규 슬롯 필드 0.**
- ⚠ **`projectileDataIndex` 는 필수다.** 착지 슬램이 `ProjectileSpawnRequest` 하나로 표현되고
  드레인이 `dataIndex < 0` 이면 요청을 통째로 버린다 — 연출뿐 아니라 **피해까지 사라져** "이탈만
  하고 아무 일도 안 일어나는" 궁극기가 된다. `SelfTileAoe` 가 겪은 그 함정이라 같은 처방을 쓴다:
  bake 의 projectile→index 분기에 kind 추가 + 미지정 시 **loud 거절**(skip + 경고).
- 적용성 판정: `DcApplicability` 의 **self/오라/지역 계열 목록에 넣는다**(`SelfBlink` 옆).
  "보스 전용이라 카드가 아니다" 를 이 레이어에서 표현하려 들면 안 된다 — `Unclassified` 는
  **통합 버그 전용**으로 예약돼 있고 `EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs` 가
  그 불변식을 강제한다(구현 중 이 테스트가 실제로 잡았다). 보스 전용은 authoring 사실이지
  적용성 판정이 아니며, `SelfBlink` 도 같은 처지로 그 목록에 있다.
- `HealthThresholdSystem` 의 "unhandled payload" 경고는 unit 1 이 분기를 추가할 때까지 뜨면
  안 되므로, **unit 0 에서는 에셋 배선을 하지 않는다**(배선은 unit 5).

## 완료 기준

- compile 클린 · EditMode 무회귀 · 런타임 동작 무변경(발동 경로 없음)
- enum 추가가 기존 에셋의 int 값과 충돌하지 않음(뒤에 append 확인)

## 검증 기록

- 2026-08-02 · EditMode 1809 중 1807 통과·실패 0 · compile 클린. 에셋 배선 전이라 런타임 무변경.
- 구현 중 `DcApplicabilityTests.EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs` 가 초안의
  `Unclassified` 반환을 잡았다 — 위 "적용성 판정" 항목 참조.
