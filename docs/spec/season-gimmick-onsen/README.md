# Season Gimmick — "뜨끈하니 좋네요오오.. 뜨겁네?" (Onsen / Heat) Spec

**상태**: 구현 완료(unit 0~3) 2026-07-22 — 네 번째 시즌 기믹. gimmickId `G4_Onsen`, displayName `"뜨끈하니 좋네요오오.. 뜨겁네?"`. 코드(0~2)+HeatMath 테스트(13/13)+에셋/pool 등록(3) 커밋·컴파일 0. ⚠ 열기 반전(회복→손실) **육안 Play 검증은 사용자 명시 확인 미완**(shield-break 테스트 중 pool=Onsen 단독이라 동시 가동은 됐음). 인계: [4_handoff_summary.md](4_handoff_summary.md).

**주제 전환**: 기존 시즌 3종(Burnout·RedBull·ClockOut)은 전부 **직장/회사** 주제였다. 이 시즌은 **온천(뜨끈한 계절)** — "개운하다가 너무 오래 담그면 앗뜨" 라는 회복↔손실 반전 곡선. 이름이 곧 곡선이다.

**이 문서의 두 파트**: [Part A — 설계 계약](#part-a--설계-계약-아키텍처-중립)은 아키텍처를 모른다(ECS 를 걷어내도 A + `HeatMath` 만 읽으면 규칙이 그대로 선다). [Part B — ECS 바인딩](#part-b--ecs-바인딩-현재-구현)은 A 를 지금 아키텍처에 붙이는 방법이다. Mono 로 옮기면 **B 만 다시 바인딩되고, B 가 호출하는 A·`HeatMath` 는 불변**.

## 목표

시즌 기믹 "뜨끈하니 좋네요오오.. 뜨겁네?"의 첫 룰(gimmick-1, **열기 반전**)을 end-to-end 플레이 가능하게 한다. 룰 자체는 Part A 가 source of truth.

## 검증 질문 (이 spec 이 답해야 할 것)

- 매치에서 일정 주기마다 모든 유닛에 열기가 쌓이고, **1~5스택 구간은 회복 / 초과 구간은 손실**로 반전되는가?
- 열기 손실만으로는 **어떤 유닛도 죽지 않고 HP 1에서 바닥**을 치는가? (전투 데미지로는 정상 사망)
- 적에게도 동일 적용되어 **초반 적은 질겨지고 후반 적은 녹는가?**
- 기믹 비활성(다른 기믹/없음) 매치에서 완전히 무변화인가?
- 결정론: 순수 산식이라 같은 입력 → 같은 델타인가? (EditMode 로 고정)

---

## Part A — 설계 계약 (아키텍처 중립)

> ECS/Mono 어느 쪽이든 이 파트 + `HeatMath.Delta` 만으로 규칙이 완결된다. 여기엔 System·singleton·버퍼 같은 구현 어휘가 없다.

**규칙**
- 맵 위 **모든 유닛(아군+적)** 이 `heatInterval` 마다 **열기(Heat) +1**.
- 열기를 받을 때마다 그 시점 스택 수에 따라 HP 가 변한다:
  - 스택 ≤ `flipThreshold` → **회복** = `maxHP × healPercent`.
  - 스택 > `flipThreshold` → **손실** = `maxHP × lossPercent`, 단 **HP 를 1 밑으로 내리지 않는다**.
- "열기"는 스택 효과의 **명칭일 뿐** — 별도 스탯 디버프 없음. 스택 카운터는 `heatMaxStack`(= `flipThreshold+1` 로 충분, 이후 효과 동일)에서 멈춰도 무방.
- **과열은 아무도 죽이지 못한다**: 열기 손실은 사망 원인이 될 수 없다(HP 1 바닥, 아군·적 공통). 마무리는 전투가 한다 — 내 유닛은 적이, 적은 내 유닛이. (ClockOut "강제 사망 감성" 교훈.)
- **회복은 최대체력을 넘지 않는다**(오버힐/부활 없음).
- **열기로 인한 약화는 킬 크레딧을 만들지 않는다**(스코어 귀속 오염 방지).

**순수 계산 — `HeatMath.Delta`** (유닛 1, EditMode 로 고정)
```
Delta(stacks, flipThreshold, maxHP, currentHP, healPercent, lossPercent) → float (부호 있는 HP 델타)
  stacks ≤ flipThreshold : + maxHP × healPercent
  stacks >  flipThreshold : − min(maxHP × lossPercent, max(0, currentHP − 1))   // HP 1 바닥
  결과 > 0 → 회복 적용 · < 0 → 피해 적용 · 0 → no-op
```
plain 값 in → plain 값 out. 아키텍처 타입(EntityManager/MonoBehaviour/Time)을 모른다. **Mono 로 옮겨도 이 함수는 그대로 복붙**. (제약 10 seam.)

**수치 (전부 SO `OnsenGimmickData`, 하드코딩 금지)**
`heatInterval`(5s) · `flipThreshold`(5) · `healPercent`(0.10) · `lossPercent`(0.10) · `heatMaxStack`(6). SO 는 ScriptableObject 계층이라 아키텍처와 무관.

---

## Part B — ECS 바인딩 (현재 구현)

> Part A 를 지금 아키텍처에 붙이는 배관. 아래는 전부 "본질적으로 아키텍처가 필요한 것" — 전 유닛 순회 / HP 변경 / 활성 판정. Mono 로 가면 이 파트만 갈아끼우고, 호출 대상(Part A·`HeatMath`)은 불변.

- **활성 게이트**: `OnsenGimmickData : GimmickData` → `BattleBridge.CreateGimmickConfigIfActive` 가 배정 시 `OnsenGimmickConfig`(blittable 싱글턴) 주입. `HeatAccrualSystem` 은 `RequireForUpdate<OnsenGimmickConfig>` self-gate. config 부재 = 완전 비활성. (Mono: bool/참조 체크.)
- **누적**: `HeatAccrualSystem`(Effects, Burst) — `FatigueAccrualSystem` 구조 미러. 대상에 `HeatAccrual{elapsed,stacks}` lazy-attach → `heatInterval` 마다 `stacks++`(캡 `heatMaxStack`) → `Health`(Units RO)에서 maxHP·currentHP 읽어 `HeatMath.Delta` 호출. (Mono: `Update()`/코루틴이 유닛 리스트 순회.)
- **HP 채널**: 델타 부호>0 → `IncomingHeal{amount}`, <0 → `IncomingDamage{amount, source=Null}` append. `DamageApplicationSystem`(Units)이 드레인·Health 반영(회복 시 maxHP 클램프)·사망 판정. DoT 가 이미 Effects→Units 로 `IncomingDamage` 를 append 하는 전례와 동형. (Mono: `unit.ApplyHeal/Damage()` 직접 호출.)
- **대상 쿼리**: Health 보유 유닛 전체(아군+적). 투사체/해저드/픽업/사직서 제외. 피로도는 `DefenderUnitTag` 한정이었으나 Onsen 은 모든 유닛(사용자 결정 2026-07-21).
- **버퍼 보장**: 대상에 `IncomingHeal`/`IncomingDamage` 버퍼가 없으면 `HeatAccrualSystem` 이 lazy-add(ECB). 적이 `IncomingHeal` 버퍼를 갖는지 unit 2 에서 확인·처리.
- **미귀속 손실**: `IncomingDamage.source = Entity.Null` → 킬 크레딧 없음(Part A "킬 크레딧 없음"의 ECS 표현). 어차피 HP 1 바닥이라 열기 킬은 발생 안 함.
- **맥락 경계**: 열기 누적·`HeatMath` 호출 = Effects 소유. `Health` = Units 소유 **읽기만**. HP 변경은 canonical 크로스-맥락 채널(`IncomingHeal`/`IncomingDamage`)로만. **새 ECS 맥락·새 NativeQueue·새 `StackKind`/`ThresholdRule` 불필요**(per-tick HP 델타는 엣지-임계 모델에 안 맞음 → 전용 `HeatAccrual` 카운터).

### 재사용 지도 (신규 최소화 — 전부 Part B)

| 조각 | 재사용 | 신규 |
|---|---|---|
| 기믹 프레임 | `BattleConfig.gimmickPool` · `GimmickData` · `CreateGimmickConfigIfActive` self-gate | `OnsenGimmickData` · `OnsenGimmickConfig` |
| 누적 시스템 | `FatigueAccrualSystem` 구조(lazy-attach 타이머 + interval bump) | `HeatAccrualSystem` + `HeatAccrual{elapsed,stacks}` |
| HP 증감 | `IncomingHeal`/`IncomingDamage` 버퍼 + `DamageApplicationSystem` 드레인 (DoT 전례) | 없음(채널 재사용) |
| 산식 | — | `HeatMath.Delta`(순수, Part A) + EditMode 테스트 |
| 대상 모집단 | Health 보유 유닛 쿼리 | 쿼리만 확장(아군+적) |

---

## 작업 단위

각 단위에 seam 태그: `[중립]` = Part A(아키텍처 무관), `[ECS]` = Part B(현재 바인딩).

| # | 문서 | 태그 | 목적 |
|---|---|---|---|
| 0 | `0_gimmick_data_and_config.md` | [ECS] | `OnsenGimmickData` SO + `OnsenGimmickConfig` + BattleBridge 주입 seam (Burnout 미러) — **완료(컴파일 0)** |
| 1 | `1_heat_math.md` | [중립] | `HeatMath.Delta` 순수 함수 + EditMode 테스트 (반전 경계·HP1 바닥 회귀) |
| 2 | `2_heat_accrual_system.md` | [ECS] | `HeatAccrualSystem`(Effects) — lazy-attach·모든 유닛 쿼리·`HeatMath` 소비·부호별 채널 append·버퍼 lazy-add |
| 3 | `3_asset_and_play_verify.md` | [ECS] | `Gimmick_Onsen.asset` + `gimmickPool` 등록 + Play 통합 검증 |

## 파이프라인 커버리지

N/A — Onsen 은 **새 플레이 오브젝트를 스폰하지 않는다**. 기존 유닛에 얹는 상태/HP 효과라 생성→렌더 정거장이 없다. 회복/손실은 기존 힐/데미지 숫자 팝업(초록/빨강)으로 그대로 노출(무료 시각 피드백). "열기 게이지 UI / 전용 상태FX" 는 후속 후보.

## 비목표

- 열기 게이지 UI / 전용 상태FX / 온천 배경 연출 (플레이 검증은 힐·데미지 숫자로).
- 열기 냉각·리셋 상호작용(냉탕 타일 등) — 후속 gimmick-2 후보.
- 온천 시즌의 다른 룰(gimmick-2+) — 이 spec 은 gimmick-1(열기 반전) 한정.
- 열기 배율 정밀 밸런스(SO 초기값으로 진행, 플레이 후 조정).
- 감정효과·기믹 로테이션 메타.

## 후속 후보

- **열기 게이지 UI** — 유닛 머리 위 열기 스택(0~6) 진행 표시, 반전 임계 강조.
- **전용 상태FX** — 회복 구간=김/땀방울, 과열 구간=붉은 아지랑이(온천 배경 연출과 묶음).
- **열기 냉각/리셋 룰(gimmick-2)** — 냉탕 타일·물 뿌리기로 열기를 식히는 관리 레이어.
- **적 전용 열기 밸런스 분리** — 아군/적 heal·loss 를 별도 SO 필드로(현재 공통).
