# 8 — 스택 출처 예외 철회 + `CcSource` 축 은퇴 (rev of unit 3)

## 목적

unit 3 은 보스 CC 면역을 `직접 출처 && (IsLock(kind) || Impulse)` 로 정의하고, **스택 임계가 유발한 CC 는 통과**시켰다. 그 근거는 당시 `CcKind.DoT` 가 `CcEffect` 버퍼를 공유해서 "kind 로만 막으면 스택 DoT(출혈·화상)까지 죽는다" 였다(unit 3 문서 "CC — 출처 필드 + 부여 2곳 거절").

그 근거는 **같은 날 사라졌다**:

| 시각 | 커밋 | 내용 |
|---|---|---|
| 07-29 12:44 | `67875169` | 보스 면역 — 출처 축 도입, 스택 출처 통과 |
| 07-29 16:07 | `4ba9a76e` | dot-effect-extraction unit 0 — DoT 를 CC 버퍼에서 분리, 전용 채널로 |

두 spec 이 병행이라 뒤쪽이 앞쪽의 전제를 무효화한 것을 되짚지 않았다. 지금 `CcSource.StackThreshold` 를 달고 CC 큐로 들어가는 생산자는 `StackModifierTickSystem` 의 `ApplyStun` **하나뿐**이고, 리포지토리에서 `derivedKind: ApplyStun` 을 가진 SO 도 `StackModifier_Ice` 하나뿐이다. 즉 이 축이 지금 하는 일은 "보스는 행동정지 면역"에 구멍 하나를 유지하는 것뿐이다.

**철회한다.** 스택 임계 스턴도 보스에게 막는다. 소비처가 사라지는 `CcSource` 축은 죽은 코드로 남기지 않고 함께 은퇴시킨다(제약 8).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/CcActionLock.cs` — `IsBossImmune(CcKind)` 1인자
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs` — `CcSource` enum 제거
- `Assets/_Project/Scripts/Battle/Effects/EnemyCcEvents.cs` — `EnemyCcEvent.source` 제거
- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs` · `EffectSpawner.cs` · `Battle/Combat/AttackSystem.cs` · `Modifiers/StackModifierTickSystem.cs` — 호출부 정리
- `Assets/_Project/Tests/EditMode/BossCcImmunityTests.cs`
- `docs/spec/boss-jjangssen/README.md`(계약 6) · `5_handoff_summary.md`

## 구현

`IsBossImmune(CcKind kind) => IsLock(kind) || kind == CcKind.Impulse`.

부여 거절 지점은 그대로 2곳(`CcApplySystem` 드레인 · `EffectSpawner.ApplyCc`) + 넉업 연출 동반 거절 1곳(`AttackSystem`)이다. 판정 술어 단일 소스도 유지한다 — 바뀌는 건 축 하나가 빠지는 것뿐이다.

**보스전에서 스택 카드가 죽지 않는다** — 이 철회로 막히는 건 Ice 5중첩 스턴 하나다:

- 감속(Ice 1~4중첩)은 `StatModifier(MoveSpeedMul)` 라 CC 면역과 무관하게 통한다.
- DoT(출혈·화상)는 `DotApplyEvents` 전용 채널이라 `CcApplySystem` 을 거치지 않는다.

테스트는 `StackThresholdSourceAlwaysPassesThrough`(축 전제) 를 제거하고, 그 자리에 **스택 출처가 더는 존재하지 않음**을 고정한다. `DirectSourceImmunityMatchesApprovedScope` 의 리터럴 집합 방식(항진 방지)은 그대로 유지한다 — `CcKind` 가 append-only 로 자랄 때 사람이 결정하게 강제하는 것이 그 테스트의 목적이다.

## 완료 기준

- [ ] EditMode 전체 green (`BossCcImmunityTests` 갱신 포함)
- [ ] `rg CcSource` 결과 0건 — 축이 코드에 남지 않는다
- [ ] Play: 보스에게 동상 5중첩을 쌓아도 멈추지 않고, 감속과 출혈은 그대로 걸린다
