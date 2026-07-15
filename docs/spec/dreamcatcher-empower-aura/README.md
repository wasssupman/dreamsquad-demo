# Spec — Dreamcatcher Empower Aura (드림캐쳐 강화 온-바디 오라)

> 상태: **구현 중** — 2026-07-15. (구 슬러그 `unit-buff-debuff-aura` — 개념 전환으로 개명)
> 출처: unit-status-fx 후속. 개념이 "제네릭 버프/디버프 오라" → **"드림캐쳐가 강화한 유닛 오라"** 로 전환됨(아래 이력).

## 목표

**드림캐쳐가 스탯 모디파이어로 강화한 유닛**에게 직관적·임팩트 있는(초사이언식) 온-바디 오라 VFX 를
자동 부착/해제한다. 단일 "강화(Empowered)" 오라 1종. 드림스톤 로드아웃·시너지·on-place·슬로우 등
**다른 출처의 스탯 변화는 오라 대상이 아니다** — 오라는 오직 드림캐쳐 출처에만 반응한다.

## 개념 전환 이력 (왜 이렇게 됐나)

1. 최초: "타일/시너지/드림캐쳐 등으로 버프/디버프된 유닛에 오라" (제네릭 순-스탯 판정).
2. 실플레이 검증에서 드러남: 로드아웃/시너지가 **모든 유닛을 상시 버프**해 전 유닛이 빛남 → 오라의 "특별함" 소실.
3. 사용자 결정(2026-07-15): **드림캐쳐 출처에만** 오라. 정확·재사용 가능하게 하려면 stackId/handle
   휴리스틱이 아니라 **모디파이어에 출처(origin)를 1급 태그로** 심어야 한다("땜빵 금지").
4. → `ModifierOrigin` 프레임워크(unit 0) 도입 후 오라가 그 위에서 `origin==Dreamcatcher` 로 판정.

## 검증 질문

> "드림캐쳐 카드/트리거로 강화된 유닛에만 금빛 강화 오라가 뜨고, 드림스톤·시너지·on-place 로만
> 버프된 유닛(또는 무버프 유닛)엔 안 뜨는가? 드림캐쳐 효과가 해제(revoke)되면 오라도 사라지는가?"

## feature-wide 계약

1. **모디파이어 출처 = 1급 태그.** 모든 `StatModifierApplyEvent` 는 `ModifierOrigin` 을 실어 나르고,
   `ModifierApplySystem` 이 `StatModifierSlot.header.origin` 으로 전파한다. origin 은 **메타데이터**(머지 키
   `(source,stat,op,stackId)` 에 넣지 않음). 크기·stat 이 같은 모디파이어도 출처로 구분된다(슬롯 단위 —
   집계 `ModifierStats` 에선 소실). 상세: `0_modifier-origin-framework.md`.
2. **오라 = 드림캐쳐 출처 판정(순수 함수).** `ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots)` —
   `origin==Dreamcatcher` 슬롯의 net 을 재집계해 identity 에서 벗어나면 활성. revoke(mult=1.0 중립화)는
   net=identity 라 자동 비활성. `DamageVsCcMul`/`MaxHealthMul` 은 판정 제외(조건부·비체감). EditMode 대상.
3. **kind 1종 append.** `StatusFxKind.Empowered`. 단일 오라(방향 무관 — 드림캐쳐가 감속을 걸어도 "강화" 취급).
4. **상태 구동 reconcile.** `BattleBridge.ReconcileStatusFx` 가 `StatModifierSlot` 보유 유닛을 매 프레임 스캔,
   `HasActiveDreamcatcherModifier` 면 `Ensure(Empowered)`. 효과 소멸·revoke·사망 시 기존 `EndFrame` 자동 회수.
   **신규 ECS 컴포넌트/큐 0**, 버퍼는 읽기만.
5. **`_activeDcEffects` 상속 출처 보존.** `ActiveDcEffect.origin` 이 카드=Dreamcatcher / 드림스톤=Dreamstone 을
   기억 → 신규 배치 유닛 상속(`ApplyActiveDcEffectsTo`)이 각 효과를 **진짜 출처로** 재적용(드림스톤 오태깅 버그 수정).
6. **on-body 오라.** registry `Empowered` 항목 offset≈0·scale·billboard. 저작 프리팹은 자체 정렬(유닛 실루엣 보존).
7. **기본(fallback) 드림캐쳐 덱 제거.** `DreamcatcherHandController.fallbackDeck` 삭제 — 저장 덱 없으면 부착 덱은
   빈 목록(사용자 결정 2026-07-15). 오라와 독립한 정리지만 같은 세션에서 함께 반영.

## 작업 단위

| 파일 | 작업 | 상태 |
|---|---|---|
| 0 | ModifierOrigin 프레임워크 (enum + header/event 필드 + apply 전파 + 생산자 19곳 태깅) | 구현됨 |
| 1 | `StatusFxKind.Empowered` + 순수 `ModifierAuraClassifier` + EditMode | 구현됨 |
| 2 | `ReconcileStatusFx` origin 필터 소스 훅 + `ActiveDcEffect.origin` 상속 수정 + fallbackDeck 제거 | 구현됨 |
| 3 | Empowered 오라 `_SKELETON` 저작(unity-vfx-authoring) + registry 프리팹 배선 + Play | **다음** |

## 파이프라인 커버리지 (상태 연출 = 온-바디 View, unit-status-fx 아키타입 재사용)

| 정거장 | Empowered Aura |
|---|---|
| 데이터(SO) | `StatusFxRegistry`(Empowered → 프리팹/offset≈0/scale/billboard/tint) |
| ECS 상태 | `StatModifierSlot.header.origin`(Effects, 읽기 전용) — FX용 신규 컴포넌트 없음 |
| 판정 | 순수 `ModifierAuraClassifier.HasActiveDreamcatcherModifier` (EditMode) |
| 생성 트리거 | `BattleBridge.ReconcileStatusFx` 매 프레임 |
| 뷰/풀 | `StatusFxSpawner`(kind별 풀) / `StatusFxView` |
| teardown | `StatusFxSpawner.Clear()` |

## 후속 후보

- **강도별 단계 오라** — net 배율 세기에 따라 scale/emission. 현재 on/off.
- **출처 태그 재사용** — dispel(출처별 해제)·모디파이어 UI(출처 아이콘)·밸런스 로깅이 `origin` 소비. (framework 파생)
- **드림캐쳐 카드별 오라 차등** — 카드/effect kind 별 색·강도. 현재 단일 금빛.
- **StackModifierSlot origin** — 현재 스택 슬롯 header.origin 은 Unspecified(스택은 오라 비대상). 필요 시 채움.

### 리뷰 파생 잔여 (two-track, 2026-07-15)

- **음수 Override 잠재 결함(ecs H1)** — `ModifierAuraClassifier`·`ModifierStatsAggregateSystem` 둘 다 Override
  누산 초기값 `0f` 라 유일 Override 가 음수면 삼켜짐. 현재 음수 Override DC 카드 없어 dormant. 프레임워크
  공유 결함이라 별도 티켓(양쪽 동시 수정 — `float.NegativeInfinity` 초기화).
- **fallbackDeck 씬 잔재(code L1)** — `BattleScene.unity` 에 제거된 SerializeField 의 orphan 참조 잔존.
  Unity 가 무시(무해)하나 BattleScene 열어 재저장하면 정리. 씬 dirty 격리 후.
- **LogDeck "default" 라벨(code L1)** — 저장 덱 없을 때 빈 덱을 여전히 `"default"/"Default+Active"` 로 로깅.
  기본 덱 제거 후 오도성 — Active-only/empty 표기로. 로깅 전용.
- **PlayMode 오라 라이프사이클 e2e** — apply DC→오라 등장→revoke→소멸 통합 테스트(현재 revoke=stat복원은
  `RevokeNeutralizesReductionShapedBuff` 로 가드, 오라 뷰 등장/소멸 자체는 classifier+육안).
