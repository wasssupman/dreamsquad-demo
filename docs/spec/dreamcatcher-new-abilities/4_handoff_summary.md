# 4 — handoff summary (Spec A)

기존 sim 기반 위에 저위험 신규 드림캐쳐 3종(shatter_hymn·frost_arrow·ember_bite)을 얹은 Spec A 인계. 최신 계약은 README + 0~3 번호 문서 우선.

## Commit

- (미커밋 — Spec A 전체 완료 후 묶어 1커밋 예정, 사용자 지시 "a")

## Implemented (code, compile-verified)

- **unit 0**: enum/필드 선언 — `DcPayloadKind.{ApplyCcToTarget,ApplyStackToTarget}`, `DcCcKind{Stun,Impulse}`/`DcStackKind{Fire,Ice,Bleed,Poison}`(데이터 미러), `DcPayloadSpec.{ccKind,stackKind}`, `CardBuffKind.DamageVsCc`, `StatKind.DamageVsCcMul`, `ModifierStats.damageVsCcMul`, `DcTriggerSlot.{ccKind,stackKind}`(Battle enum).
- **unit 1**: frost/ember — bake(`BattleBridge.Dreamcatcher.cs`: ApplyCc/ApplyStack 분기 + `MapDcCc`/`MapDcStack` 번역) + fire(`AttackSystem.cs` RESOLVE DC-block 을 3-way 디스패치로: Projectile/ApplyCc(EnemyCc 채널, Stun=remaining·Impulse=넉백)/ApplyStack(StackModifier 채널)).
- **unit 2**: shatter — `ModifierStatsAggregateSystem` 6번째 stat(base 1) + ModifierStats add-site ×2 `damageVsCcMul=1f` + `MapDcEffect` 매핑 + `AttackSystem` 투사체 bake·멜리 양 경로 배율 + `AnyActiveCc` 헬퍼.
- **unit 3**: 테스트(EditMode 집계 base-1/combine 2건, PlayMode shatter 1건) 작성. 카드 `.asset` authoring 은 Unity 대기.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — 데이터 계층 enum/선택자
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — bake + 번역
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — fire 디스패치 + DamageVsCc 소비
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStatsAggregateSystem.cs` — 6번째 stat
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — ModifierStats add-site base 1

## Verified

- `dotnet build` 4어셈블리 오류 0개(Layout stale-csproj 파일 세션 주입 후 신호 clean).
- **two-track review (2026-07-13): 양 트랙 APPROVE** (code-reviewer + ecs-reviewer, CRITICAL/HIGH 0). 반영한 하드닝: maxStack authorable(tileRange), stack count clamp(1,255), DamageVsCc copy 정정(둔화 제거), Impulse colocation 가드, 문서 드리프트. 맥락 경계·Burst·NativeQueue lifecycle·base-1 가드 PASS.
- **한계**: Unity unavailable → 테스트 **실행**·카드 에셋 생성·PlayMode 검증 미실시. 컴파일만. 온-히트(frost/ember) 발동 assertion 은 test-gap 으로 Unity 복구 시 필수(3_card_assets.md).

## Notes (되돌리면 안 됨)

- **base-1 무적 가드**: `ModifierStats.damageVsCcMul` 는 add-site(BattleBridge:3358/4167)에서 1f 필수 — `ModifierStatsDirty` 가 disabled 로 추가돼 무-모디파이어 유닛은 집계가 안 돌기 때문. 0 이면 CC 적 무적(critic HIGH).
- **frost=Stun**(얼림): 이 엔진의 "Slow" 는 CcEffect 가 아니라 MoveSpeedMul StatModifier(ZoneApplySystem) — CC 파이프/shatter 시너지 위해 Stun 채택. `DcCcKind` 에 Slow 없음(의도).
- **DamageVsCc 투사체 경로**: 발사 시점 bestTarget CC 로 판정(homing 명중 대상 불일치 허용) — 궁수 콤보 살리는 critic HIGH 대응.
- **ember 전제**: Bleed StackKind ThresholdRule(SO)이 DoT 를 만들어야 실효(unit 3 Unity 확인).
- 세션 중 `Wassup.Runtime.csproj` 에 Layout/BattleHudTrayConfig 파일 5개를 임시 주입(빌드 신호용) — Unity 가 재생성하므로 무해, 커밋 대상 아님.

## Follow-up

- Unity 복구: 테스트 실행 + 카드 3종 에셋 + Bleed ThresholdRule 확인 + 궁수(투사체) shatter 육안 검증.
- **Spec B `dreamcatcher-kill-and-threshold`**: last_stand + devouring_craving(인프라).
- `ApplyCcToTarget(Impulse)` 넉백 카드 — payload 이미 wired, 데이터만.
