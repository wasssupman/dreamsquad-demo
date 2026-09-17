# 4 · Handoff Summary — idle-break-shared

## Commit

- (이 문서와 같은 커밋) `feat(idle-break-shared): 대기 컷 규칙 IdleBreakCycle 공유 + CH2/CH3 저작` — 해시는 사용자 Play 확인 뒤 README 상태 라인에 기록.
- 선행: sprite-unit-backend unit 6(`SpriteUnitView` 의 첫 대기 컷) — 이 spec 이 그 틀(0프레임 정지 쉼)을 폐기했다.

## Implemented

- `IdleBreakCycle`(Presentation, 순수 구조체) — 기본 루프 타이머 ↔ 컷 타이머, 직전 컷 회피(`UnitAnimationChoice.ChooseNext`). EditMode 5건.
- `SpriteUnitView`: 필드 4개 → `_idle` 하나. 기본 idle 플립북 루프가 항상 돌고 N초마다 컷 한 바퀴(`FlipbookMath.Duration`). 0프레임 정지·`Stop()` 폐기.
- `UnitSpriteMotionSet`: 풀 = 컷만(`IdleBreakCount`/`IdleBreakAt`), `idleRestGap` → `idleBreakInterval`(FormerlySerializedAs), `PickIdleBreakInterval`.
- Spine 데이터: `idleVariants` → `idleBreaks`(FormerlySerializedAs) + `idleBreakInterval`(맨 뒤) — Defender/Attack 둘 다. `ISpineUnitVisualData.SpineIdleBreaks`/`IdleBreakInterval`.
- `SpineUnitView`: 루프 이어붙임(`AdvanceIdleVariant`·`HookIdleVariantCycle`·`OnIdleVariantComplete`) 은퇴 → `TickIdleCycle`(UpdatePosition, 배틀 스케일). 컷은 **loop:true 엔트리 + Animation.Duration 타이머**(계약 5 유지). 원샷·배치·오버라이드 이탈 시 `StopIdleCycle()` → 큐 복귀는 기본 idle, 그 루프가 실제로 돌기 시작한 프레임에 타이머 재개.
- 저작: 스나이퍼 = CH2(`Idle` / `[Idle2]` / attack / drop / Muzzle / scale 1.04), 실드셔틀 = CH3(`idle2` / `[idle1, idle3]` / attack / drop / scale 0.60) — 초기 0.52/0.30 을 사용자 지시로 2배, 소환사 `[idle2, idle3]`. 셋 다 interval (1,3), `outgameScaleMul` 0.372(CH1 과 동일).

## Key Files

- `Assets/_Project/Scripts/Presentation/IdleBreakCycle.cs` · `Tests/EditMode/IdleBreakCycleTests.cs`
- `Presentation/SpineUnitView.cs`(`TickIdleCycle`·`StopIdleCycle`·`ResolveBaseIdle`) · `Presentation/SpriteUnitView.cs`(`TickIdleCycle`·`BeginIdleLoop`)
- `Data/UnitSpriteMotionSet.cs` · `Data/ISpineUnitVisualData.cs` · `Data/DefenderUnitData.cs` · `Data/AttackUnitData.cs`
- `Data/Defenders/Defender_{Sniper,ShieldShuttle,Summoner}.asset`

## Verified

- 컴파일 0 에러. EditMode 두 lane 2872건 — 기지 실패 2건(bomb_man·boomerang 문안)만 빨강, 그 외 초록.
- MCP Play 계측(BattleScene 배치 단계, 40초): 스나이퍼 `Idle` 루프 → 1~3초 → `Idle2`(3.33s) 한 바퀴 → `Idle` … · 실드셔틀 `idle2` → `idle1`(1.33s)/`idle3`(2.5s) 번갈아 → `idle2` … · 소환사 `idle` → `idle2`/`idle3` 번갈아 → `idle`. 트랙0 은 항상 `(loop)` — 걷기 배율·원샷 게이트 무영향.
- 스크린샷: 세 리그 모두 왼쪽 향함(ScaleX +1, 모디파이어 불필요) · 눈높이 `ApproxWorldHeight` ≈ 1.1 일치 · `UnitVisualDataValidator` 경고 0.
- 미실행: PlayMode lane(`PatrolDefenderPlayTest` 포함 8분) · 사용자 육안 · 공격/drop 타이밍(`deploymentDuration` 0.45 vs drop 1.67/0.67).

## Notes

- **컷 엔트리는 loop:true 다.** loop:false 로 바꾸면 `IsLocomotionLoopPlaying`·원샷 게이트가 컷을 원샷으로 오인한다(summon-patrol unit 10 계약 5). 끊는 건 타이머.
- **순환 시작은 「기본 idle 루프가 실제로 도는 프레임」에서만.** 원샷 큐 복귀 직후엔 트랙0 이 원샷이라 Tick 이 게이트에 걸리고, 큐된 idle 이 돌기 시작하면 `!_idle.Active && current == baseIdle` 로 재개한다. 이 조건을 완화하면 컷이 원샷을 자른다.
- `ResolveLocomotionAnimation` 이 컷 재생 중엔 컷 이름을 답한다 — `UpdateLocomotionAnimation`/`RefreshLocomotionIfLooping` 의 이름 비교가 컷을 되돌리지 않기 위함. 빼면 컷이 한 프레임 만에 idle 로 덮인다.
- 스프라이트 컷은 `FlipbookMath.Duration` 으로 잰다 — 시트 loop 체크박스 무관(sprite-unit-backend critic M1).
- CH2 `attack-Loop`·CH3 `attack2` 는 미연결(사용자 결정). `deploymentDuration` 은 각 유닛 값 유지 — drop 길이와 다르면 Play 에서 보고 사용자 결정.

## Follow-up

- 사용자 Play 육안(대기 리듬·크기·drop 타이밍) → 커밋 해시 기록.
- PlayMode lane(`PatrolDefenderPlayTest` 오버라이드 단언 무수정 초록 확인).
- README 후속 후보: attack-Loop/attack2 슬롯 · CH2/CH3 death 애니 · summon-patrol unit 10 계약 5·7 정식 개정.
