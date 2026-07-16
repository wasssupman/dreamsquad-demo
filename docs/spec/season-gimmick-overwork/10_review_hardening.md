# 10. 리뷰 견고화 (findings #1·#2·#3)

## 목적

기믹 시스템 리뷰에서 나온 3개 findings 를 수정한다. 새 기능이 아니라 완료된 두 룰의 **문서 정확성·밸런스·유지보수 견고성** 보강.

## 변경 대상

- `docs/spec/season-gimmick-overwork/README.md` — crash 계약 문안 (#1)
- `Assets/_Project/Scripts/Battle/Effects/PickupConsumeSystem.cs` — 소비 락 (#2)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` — `ModifierOrigin.Burnout` (#3)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierTickSystem.cs` — Fatigue 파생 origin 승격 (#3)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 상태FX 분류 루프 (#3)
- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — 소스 주석 (#3)

## 구현

### #1 — crash 문서 drift (문서만)
README(feature 계약 SoT)가 라스트런 crash 를 옛 설계 *"최대체력 -90% 영구컷"* 으로 기술했으나 실제/unit 5 계약은 **1회성 = 현재 최대체력의 50% 데미지(IncomingDamage)**. README L13·검증질문·파이프라인 표를 현행으로 정정. (코드·unit 5 문서는 원래 정확 — README만 stale 이었다.)

### #2 — 라스트런 소비 락 (밸런스)
`PickupConsumeSystem.TryConsume` 최상단에 `if (em.HasComponent<LastRun>(unit)) return;` 가드. 라스트런 진행 중 유닛은 레드불을 밟아도 **소비하지 않고 픽업을 보드에 남긴다**(만료 또는 타 유닛 소비). 이전엔 재소비 때마다 `LastRun.remaining` 을 리셋해 crash(유일 페널티)를 무한 회피 가능했다. 가드 통과 = LastRun 미보유가 보장되므로 기존 `HasComponent/SetComponent(refresh)` 분기를 제거하고 단순 `AddComponent`.

### #3 — 상태FX 분류 견고화 (유지보수)
번아웃/라스트런 상태FX 가 `ModifierOrigin` 하나(Stack→Burnout, Gimmick→LastRun)에 의존 → "각 origin 의 유일 producer" 가정이 2번째 스택·기믹 효과 추가 시 조용히 깨짐. 각 상태를 **권위 신호**로 전환:

- **번아웃**: `ModifierOrigin.Burnout` 신설(append). `StackModifierTickSystem` 이 `kind==StackKind.Fatigue` 파생 ApplyStat 에만 이 origin 을 심는다(다른 Stack 파생은 `Stack` 유지). BattleBridge 는 `origin==Burnout` 으로 분류.
- **라스트런**: 이미 창(레드불 소비~crash)을 정의하는 `LastRun` 컴포넌트를 `_em.HasComponent<LastRun>` 로 직접 조회. `origin==Gimmick` 추론 제거.

> **설계 노트**: 사용자 초안은 "Burnout 마커 컴포넌트 + 부착/제거 시스템" 이었으나, 그 마커는 번아웃 stat 모디파이어의 duration 을 별도 tick 으로 복제하는 순수 중복이라(안정 신호는 결국 그 모디파이어 자신) 채택하지 않았다. producer-level origin 태그가 같은 견고함을 새 컴포넌트/시스템 없이 달성 (constraint 8 준수). 범용 trigger→domain 통합은 `docs/plans/2026-07-15-effect-trigger-unification-design.md` (파킹) 소관 — 2번째 기믹 착수 시.

**무영향 확인**: `ModifierOrigin.Stack` 소비처 = 위 두 곳뿐, `Gimmick` 은 분류(제거됨)와 AS버프 스탬프(무해 잔존)뿐. 버프/디버프 오라·empower 오라는 origin 비의존/Dreamcatcher 전용이라 무관.

## 완료 기준

- `dotnet build Wassup.Runtime.csproj` 오류 0 (2026-07-16 확인). Unity 리컴파일 콘솔 클린.
- EditMode 기존 스위트 통과(신규 순수 함수 없음 → 신규 테스트 불요).
- Play(RedBull 기믹): 라스트런 중 같은 셀 레드불 재소비 안 됨(픽업 잔존) → crash 후 재소비. 라스트런 창 동안 `LastRun` 상태FX 표시.
- Play(Burnout 기믹): 배치 유닛 번아웃 진입 시 Burnout 상태FX, 15s 해제 시 사라짐.
- Play(off): `BattleConfig.gimmickEnabled=false` → 기믹/픽업/FX 전무.

확인 2026-07-16 (MCP 라이브):
- 컴파일 클린(콘솔 에러/경고 0), `dotnet build Wassup.Runtime` 오류 0.
- Burnout 매치: `BurnoutGimmickConfig 주입=True`, 피로도 누적 라이브(0→3), **번아웃 먹구름 VFX 육안 확인** = #3 번아웃 half 실증(origin=Burnout → 분류 → StatusFxKind.Burnout 경로 동작).
- RedBull 매치: `RedBullGimmickConfig 주입` + `BurnoutGimmickConfig=False`(상호배타/self-gate 양방향), 활성 픽업 5개 전부 Walk/Place(off-tile 0)·수명 tick, 에러 0.
- **미실증(후속 육안)**: #2 소비 락 · #3 라스트런 빨강 먹구름 — 라스트런 5초 창이 일시적이라 일시정지 스냅샷으로 미포착. 라스트런 창 도중 defender 공속x1.50 + 동일 셀 픽업 잔존으로 확인 예정.
