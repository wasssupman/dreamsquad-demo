# 3 — 카드 에셋 + 테스트

## 목적

신규 3종(shatter_hymn·frost_arrow·ember_bite) 카드 에셋 authoring + 회귀 테스트. 코드 경로(unit 0~2)를 실제 카드로 검증.

## Unity 가동 전제 (현재 세션 제약)

`DreamcatcherCard` `.asset` 생성·카탈로그 등록·PlayMode 실행은 **Unity 에디터 필요**(현 세션 unavailable). 코드/테스트 부분은 지금 작성·컴파일 검증하고, 에셋 authoring 은 Unity 복구 시 진행(아래 authoring 스펙 그대로).

## 카드 authoring 스펙 (Unity 에서 생성)

- **shatter_hymn** (Squad): `type=Squad`, `axis=All`, `effects=[{ kind=DamageVsCc, percent=25 }]`. 아트만.
- **frost_arrow** (Unit): `type=Unit`, `mechanics=[{ trigger={AttackN, period=3}, payload={ kind=ApplyCcToTarget, ccKind=Stun, duration=0.6 } }]`.
- **ember_bite** (Unit): `type=Unit`, `mechanics=[{ trigger={AttackN, period=3}, payload={ kind=ApplyStackToTarget, stackKind=Bleed, magnitude=1(스택), duration=4 } }]`.
  - **전제**: `Bleed` StackKind 의 ThresholdRule(SO, `BattleBridge.GetStackThresholds`)이 DoT 를 만들어야 실효. Unity 에서 Bleed 규칙 존재 확인, 없으면 추가(ApplyDot).
- 3종 catalog 등록 + 기본 덱/시트. 시트: DamageVsCc 효과는 기존 effects 탭(CardBuffKind)으로 round-trip. ccKind/stackKind 는 Unity-authored 구조 → 시트 DTO 변경 불필요(투사체 ref 와 동일 취급).

## 테스트 (이번 세션 작성, 컴파일 검증)

- `ModifierFrameworkTests.cs` (EditMode):
  - `DamageVsCcMul_AggregatesToBaseOne_WhenNoVsCcSlot` — vsCc 슬롯 없어도 집계 base 1(무적 회귀 가드, critic HIGH).
  - `DamageVsCcMul_Combines_Multiplicatively` — +25% → 1.25.
- `DreamcatcherCombatDamageTest.cs` (PlayMode): `DamageVsCc_BoostsDamage_AgainstCcdEnemy` — Stun 걸린 더미에 shatter 로 데미지 급증(멜리 직접 경로). Unity 복구 시 실행.

## 완료 기준

- [x] 4개 어셈블리 `dotnet build` 오류 0개(신규 EditMode·PlayMode 테스트 컴파일 포함).
- [x] EditMode 집계 테스트 2건 통과 (`ModifierFrameworkTests` DamageVsCcMul base-1/combine — 716 스위트 green).
- [x] PlayMode shatter 테스트 통과 (`DreamcatcherCombatDamageTest.DamageVsCc_BoostsDamage_AgainstCcdEnemy`).
- [x] 카드 3종 에셋 생성·카탈로그 등록. **ember Bleed ThresholdRule 신설**: `StackModifierSO` 에셋이 0개였음 → `StackModifier_Bleed`(ApplyDot dps6·4s, placeholder balance) 저작 + `BattleBridge.stackModifierAuthoring`(BattleScene) 배선.
- [x] **온-히트 발동 PlayMode assertion** (`DreamcatcherOnHitTest`): frost N타 → 적 CcEffect(Stun) ✓ / ember N타 → 적 StackModifierSlot(Bleed) + Bleed 규칙 배선 가드 ✓. MapDcCc/MapDcStack 번역 커버.
- [ ] **잔여**: 궁수(투사체) + shatter/frost/ember 경로는 통합 테스트 미커버(멜리 direct 경로만 검증, critic HIGH). shatter 투사체-bake 배율은 unit 2 코드 경로로 존재하나 실기 assertion 없음 — follow-up.

## two-track review 반영(2026-07-13)

- [MED] maxStack 하드코딩 → `tileRange` authorable(fallback 5). [MED] stack count `(byte)clamp(1,255)`. [MED] DamageVsCc copy "둔화" 제거(활성 CcEffect 만). [LOW] Impulse colocation 가드. [LOW] DcCcKind 문서 드리프트 정정. 판정: 양 트랙 APPROVE.
