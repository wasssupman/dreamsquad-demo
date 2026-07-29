# bleed-fighter-defender — 난도질꾼 (단일 대상 출혈 파이터)

> 상태: 초안 (사용자 승인 대기, 2026-07-29)

## 목표

빠른 공속의 낮은 단타 + **히트마다 출혈(Bleed) 도트**를 묻히는 저코스트 근접 파이터
**난도질꾼**(id `slasher`)을 추가한다.

- Fighter 는 2종뿐이고 전부 Epic/Ego(코스트 4·7) — 저코스트 공백과 "단일 대상 지속딜" 정체성 공백을 동시에 메운다.
- 적이 사거리를 벗어나 걸어가도 남은 도트가 계속 닳는 것이 차별점 ("스치면 출혈이 남는다").
- 공격 `outputs` 의 `ApplyStack` 분기(`AttackSystem:1170`)의 **첫 실사용** — 코드는 있으나 사용 유닛 0, 전용 테스트는 Ignored 스텁 상태라 검증 테스트가 unit 0 이다.
- 배치 스킬 = **등장 난도질**: 배치 순간 주변 적 전원에 Bleed 1스택 (`OnPlaceEffectType` 변종 신설).

검증 질문: **"공격 outputs 의 ApplyStack 이 실전에서 발동하며, 히트당 도트가 저코스트 근접 딜러로 배치 가치가 있는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | test | `0_apply_stack_output_test.md` | outputs ApplyStack 경로 회귀 테스트 (첫 실사용 전 검증) |
| 1 | code | `1_onplace_apply_stack_nearby.md` | `OnPlaceEffectType.ApplyStackNearby` 변종 신설 |
| 2 | asset | `2_unit_asset_and_catalog.md` | 유닛 SO + 카탈로그 등록 + Play 검증 |
| 3 | docs | `3_handoff_summary.md` | 인계 요약 (종료 시) |

## Feature-wide 계약

1. **Bleed 파생 규칙 SO(`StackModifier_Bleed`)는 ember 카드와 공유 — 이 spec 은 값을 바꾸지 않는다.**
   현행: 1스택 도달 → 소모 → DoT 3dps × 3s. 재저작(예: 5스택 축적형)은 카드 밸런스가 같이 움직이므로
   별도 결정 + 별도 StackKind 신설이 선결(후속 후보).
   - **현재 구조 = 누적 → 임계 폭발**(2026-07-29 사용자 지적으로 재설계). `atStack 5 · Consume` —
     5타 모이면 발화하고 0으로 리셋. 공속 0.3 기준 **1.5초마다** 터지고 **1초 간격 2틱**. 초판은 `atStack 1` 이라 **누적이 아예 없었고 `maxStack` 이
     죽은 값**이었다(사실상 갱신만 되는 플랫 도트).
   - ⚠ **`stackCount` 는 안정적 관측값이 아니다.** 임계에서 소모되므로 "몇 스택인지"로 단언하면
     타이밍에 따라 실패한다. 관측은 **파생 DoT** 로.
   - ⚠ **강도 누적형(스택마다 dps 가산)으로 바꾸지 말 것.** `StackModifierTickSystem` 은
     `stackCount > lastTriggeredStack` 일 때만 발화하므로, Edge 룰로 여러 임계를 깔면 상한 도달
     후 **더 이상 발화하지 않고** 걸려 있던 DoT 가 지속 종료와 함께 꺼진다. Consume 은 스스로
     0으로 되돌려 이 문제가 없다.
   - ⚠ **폭발 지속 < 폭발 주기** 를 지킬 것(1.4s < 1.5s). `CcEffectMerge` 는 피해자당 kind 슬롯
     하나만 두고 scalar 를 덮으므로, 겹치면 앞 폭발의 남은 틱이 소실된다. 같은 이유로 **여러
     난도질꾼이 한 적을 물어도 출혈은 합산되지 않는다**(합산은 코드 결정 — 후속).
   - `maxStack`/`perAppDuration` 은 **producer**(AttackOutput·onPlace)가 소유하고 `thresholds` 는
     SO 가 소유한다. 한쪽만 바꾸면 조용히 어긋난다 — 배치 스킬(`onPlaceMagnitude`)도 같이 맞출 것.
2. **체인 검증 현황 (2026-07-29 리그 실측)**: 스택 부여(PlayMode `EmberBite` 2/2)·임계 배선
   assert·DoT→데미지(EditMode `DotApplySystemTests` 7/7) 전부 green. **유일한 미실증 구간 =
   outputs→큐 enqueue** — unit 0 이 이 구간을 고정한다.
3. 도트 데미지는 `EnemyCcEventsSingleton`(DoT CcEffect) 경유 — **신규 시스템/채널/컴포넌트 0**,
   신설은 `OnPlaceEffectType` enum 멤버 + bridge 분기 + SO 필드뿐.
4. `ApplyStackNearby` 는 스택 종류를 하드코딩하지 않는다 — `DefenderUnitData.onPlaceStackKind`
   필드로 지정(제약 6). 스택 수 = `onPlaceMagnitude`, 반경 = `onPlaceRange`, perApp 지속 = `onPlaceDuration`.
5. outputs 순서 계약 없음 — Damage 와 ApplyStack 은 독립 적용(같은 히트 루프에서 각각 enqueue).
6. 전 수치는 SO — 하드코딩 금지.

## 초기값 (전부 튜닝 대상, SO 소유)

Fighter · Common · 코스트 2 · HP 350 · 사거리 1 · 쿨다운 0.9s · attackTargetCount 1
· outputs `[Damage 8, ApplyStack(Bleed, 1스택, perApp 4s, max 5)]`
· 등장 난도질: 반경 2 · 1스택. 예상 단일 대상 합산 ~19dps(직격 ~9 + 도트 중첩 ~10).

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Slasher.asset` 신규 + `DefenderUnitData.onPlaceStackKind` 필드 신설 + **DefenderCatalog 등록**(unit 2) |
| 스폰 진입점 | 변경 없음 — `PlaceDefenderAs`→`CreateDefenderEntity` |
| ECS 컴포넌트 (Units) | 표준 세트 그대로. HazardCastState/AggroProvider/DeployedFacing/VolleyFireState N/A(능력 비활성) |
| 시뮬 시스템 | 변경 없음 — AttackSystem `ApplyStack` 분기·StackModifierTick·DotApply 기존 그대로 |
| 이벤트 큐 | 신규 채널 0 — StackModifierApplyEvents·EnemyCcEvents 재사용 |
| View/Pool | 기존 SpineUnitPool(파츠 placeholder 허용). 출혈 아이콘은 기존 StackIconRegistry 경로 |
| 체력 표시 | 변경 없음 — UnitOverheadUiLayer |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 배치 스킬 실행은 BattleBridge 기존 on-place 체인 |

## 후속 후보

- **출혈 상태 표기(아이콘 / VFX)** [M] · 2026-07-29 사용자 결정으로 **후속 이관**. 현재 출혈이
  걸렸는지 화면에서 알 방법이 도트 데미지 숫자뿐이다. 두 경로 모두 아직 전투 스택을 모른다:
  - **아이콘**: `OverheadStackKind` 가 기믹 전용(`Fatigue`/`Heat`) 2종뿐이고 `StackIconRegistry`
    도 그 2종만 매핑. `StackKind → OverheadStackKind` 번역 자체가 없다. 전투 스택 4종을 이
    enum 에 넣을지가 선결 설계 결정.
  - **VFX**(권장): `StatusFxKind` 확장이 더 자연스럽다 — `Empowered`/`Burnout`/`LastRun` 이 이미
    "모디파이어/스택 슬롯 보유 → 온-바디 지속 VFX" 패턴이고, 출혈은 `StackModifierSlot` 중
    `kind == Bleed` 보유를 소스로 같은 형태가 된다. enum 주석도 "Freeze, Poison … 나중에 **끝에**
    추가 + registry 항목 + reconcile 소스 훅"으로 절차를 명시해 둔 상태.
  - 범위 결정 필요: Bleed 만 열지 `Fire·Ice·Poison` 까지 4종을 한 번에 열지.

- **5스택 폭발형 변종 유닛** [M] · 축적→강타 게임감. 별도 StackKind 신설(enum+레지스트리+아이콘)이 선결 — ember 공유 SO 를 건드리지 않기 위함.
- **다중 타겟 출혈(회전베기)** [S] · `attackTargetCount` 만 올리면 성립하는 변형 — 별도 유닛 결정.
- **전용 아트 패스** [S] · portrait/파츠/출혈 히트 VFX (placeholder 교체, guid 유지).
