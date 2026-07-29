# bleed-fighter-defender — 난도질꾼 (단일 대상 출혈 파이터)

> 상태: **구현 완료 · 사용자 Play 확인 통과 (2026-07-29)** — units 0~2 커밋. 상세는 `3_handoff_summary.md`

## 목표

빠른 공속의 낮은 단타 + **히트마다 출혈(Bleed) 누적, 5스택에서 터지는** 저코스트 근접 파이터
**난도질꾼**(id `slasher`)을 추가한다.

- Fighter 는 2종뿐이고 전부 Epic/Ego(코스트 4·7) — 저코스트 공백과 "단일 대상 지속딜" 정체성 공백을 동시에 메운다.
- 적이 사거리를 벗어나 걸어가도 남은 도트가 계속 닳는 것이 차별점 ("스치면 출혈이 남는다").
- 공격 `outputs` 의 `ApplyStack` 분기(`AttackSystem:1170`)의 **첫 실사용** — 코드는 있으나 사용 유닛 0, 전용 테스트는 Ignored 스텁 상태라 검증 테스트가 unit 0 이다.
- 배치 스킬 = **등장 난도질**: 배치 순간 주변 적 전원에 Bleed **임계치(5스택)** — 즉시 출혈이 터진다 (`OnPlaceEffectType` 변종 신설).

검증 질문: **"공격 outputs 의 ApplyStack 이 실전에서 발동하며, 누적→폭발 리듬이 스택 UI 없이도 읽히는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | test | `0_apply_stack_output_test.md` | outputs ApplyStack 경로 회귀 테스트 (첫 실사용 전 검증) |
| 1 | code | `1_onplace_apply_stack_nearby.md` | `OnPlaceEffectType.ApplyStackNearby` 변종 신설 |
| 2 | asset | `2_unit_asset_and_catalog.md` | 유닛 SO + 카탈로그 등록 + Play 검증 |
| 3 | docs | `3_handoff_summary.md` | 인계 요약 |

## Feature-wide 계약

1. **출혈 = 누적 → 임계 폭발**(`StackModifier_Bleed`). 타격당 1스택, `atStack 5 · Consume` 로
   5타에서 발화하고 0으로 리셋. 공속 0.3 기준 **1.5초마다** 터지고 **0.3초 간격 5틱**.
   - 초판은 `atStack 1` 이라 **누적이 아예 일어나지 않았고 `maxStack` 이 죽은 값**이었다(사실상
     갱신만 되는 플랫 도트). 2026-07-29 사용자 지적으로 재설계 — 업계 관례 조사 결과 스택 UI 가
     없는 상황에서는 **폭발 자체가 신호**인 이 패턴이 표준이다(엘든링·몬헌·명일방주 원소축적 계열).
   - 이 SO 를 쓰는 **배포 에셋은 난도질꾼뿐**이다(ember 는 테스트가 런타임 생성하는 카드).
     Bleed 를 쓰는 카드가 생기면 그때 밸런스 공유를 재검토할 것.
   - ⚠ **`stackCount` 는 안정적 관측값이 아니다.** 임계에서 소모되므로 "몇 스택인지"로 단언하면
     타이밍에 따라 실패한다. 관측은 **파생 DoT** 로.
   - ⚠ **강도 누적형(스택마다 dps 가산)으로 바꾸지 말 것.** `StackModifierTickSystem` 은
     `stackCount > lastTriggeredStack` 일 때만 발화하므로, Edge 룰로 여러 임계를 깔면 상한 도달
     후 **더 이상 발화하지 않고** 걸려 있던 DoT 가 지속 종료와 함께 꺼진다. Consume 은 스스로
     0으로 되돌려 이 문제가 없다.
   - ⚠ **`duration` 을 `tickInterval` 의 정확한 배수에 걸치지 말 것.** 첫 틱은 즉발이고
     (`CcEffectMerge` add 경로가 `tickTimer = tickInterval` 로 넣는다) 이후 `tickTimer` 가
     프레임 dt 를 누적하므로, 마지막 틱 시각이 만료 시각과 같으면 둘이 같은 프레임에서 경합해
     틱 수가 프레임레이트에 따라 흔들린다. 현재값은 마지막 틱 1.2s · 만료 1.35s 로 **양쪽 0.15s
     여유**를 둔다(60fps 기준 9프레임). 틱 수 = `floor((duration − ε) ÷ tickInterval) + 1`.
   - ⚠ **폭발 지속 < 폭발 주기** 를 지킬 것(1.35s < 1.5s). `CcEffectMerge` 는 피해자당 kind 슬롯
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

Fighter · Common · 코스트 2 · HP 350 · 사거리 1 · **쿨다운 0.3s** · attackTargetCount 1
· outputs `[Damage 2.67, ApplyStack(Bleed, 1스택, perApp 2s, max 5)]`
· 등장 난도질: 반경 2 · **5스택**(임계치를 한 번에 = 배치 순간 즉시 출혈)
· `StackModifier_Bleed`: `atStack 5 · Consume` · 틱 1.8 / 0.3s · 지속 1.35s (**5틱**)

**단일 대상 DPS 14.9** = 직격 8.9(2.67 ÷ 0.3) + 출혈 6.0(5틱 × 1.8 ÷ 1.5s).
⚠ **한 벌로 움직이는 값들이다.** 공속을 바꾸면 발화 주기(= `atStack` × 쿨다운)가 따라 움직이고,
그러면 폭발 지속과 틱 수도 다시 잡아야 한다. 산식: `틱당피해 = 목표출혈DPS × 발화주기 ÷ 틱수`.

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

- ~~출혈 상태 표기(VFX)~~ → **구현 완료**(2026-07-29, `c4d799b6`). `StatusFxKind` 에 전투 스택
  4종(Bleed/Fire/Ice/Poison) 오라 추가 — PixPlays ElementalAuras 사본.
  **소스 = 스택 슬롯 보유 AND DoT 진행 중**: `CcEffect` 는 kind 하나로 병합돼 어느 스택이 만든
  DoT 인지 모르므로(종류 식별 불가), 슬롯으로 종류를 알고 DoT 로 점등 여부를 판단한다.
  기존 Empowered/Burnout/LastRun 과 같은 reconcile 패턴이라 신규 로직 0.
- **아이콘 표기** [M] · 여전히 미구현. `OverheadStackKind` 가 기믹 전용(`Fatigue`/`Heat`) 2종뿐이고
  `StackKind → OverheadStackKind` 번역이 없다. 전투 스택을 이 enum 에 넣을지가 선결 결정.
  VFX 가 붙었으므로 우선순위는 낮다.
- ~~5스택 폭발형 변종 유닛~~ → **본편에 흡수**(2026-07-29). 난도질꾼 자체가 누적→폭발 구조가 됐다.
- **다중 공격자 출혈 합산** [M] · 스택 슬롯은 `(source, kind)` 로 분리되지만 폭발이 만드는 DoT 는 피해자당 `kind` 슬롯 하나를 공유해, 난도질꾼 2기가 한 적을 물어도 합산되지 않는다(`CcEffectMerge` 가 scalar 를 덮음). 합산하려면 도트 전용 가산 병합이 필요 — 코드 결정이라 별도 spec.
- **다중 타겟 출혈(회전베기)** [S] · `attackTargetCount` 만 올리면 성립하는 변형 — 별도 유닛 결정.
- **전용 아트 패스** [S] · portrait/파츠/출혈 히트 VFX (placeholder 교체, guid 유지).
