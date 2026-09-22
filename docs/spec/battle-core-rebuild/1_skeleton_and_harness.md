# 1 — 뼈대 + 하네스 (조각 A)

## 목적

`Wassup.BattleCore` 가 **엔진 없이** 판 하나를 시작부터 끝까지 틱으로 돌리고, 커맨드를 receipt 로 받고, 값 스냅샷 이벤트를 내보내고, 자기 골든을 굽는다. 이동·전투·매치 규칙은 아직 없다 — 이 unit 의 판은 「비어 있는 판이 180초를 결정론적으로 돈다」까지다. 이후 unit 2·3·4 는 이 뼈대에 phase 와 담당자를 **끼우기만** 한다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 코어 asm | `Assets/_Project/Scripts/BattleCore/Wassup.BattleCore.asmdef` — `noEngineReferences: true`, refs `Unity.Mathematics`·`Wassup.Skills`·`Wassup.UnitAi` 만 |
| 코어 소스 | `BattleCore/Match/`(`BattleMatch` · `MatchDefinition` · `ModeDef` · `Command`/`Receipt`/`RejectReason` · `CoreEvent`/`Site` · `EventBus` · `TickPipeline`/`ITickPhase`/`TickContext` · `RngStreams`) · `BattleCore/World/`(`BattleWorld` · `Unit` · `SimEntityId` · 풀) · `BattleCore/Owners/MatchClock.cs`(타이머·종료 통로 — unit 4 에서 확장) · `BattleCore/Harness/`(`CoreHarness` · `CoreTrace`) |
| 코어로 **이동**(엔진 무참조 확인됨) | `Core/MatchSeed.cs` · `Core/Trace/LegacyTraceV0.cs` → `BattleCore/…` (네임스페이스 유지, `Wassup.Runtime` 이 `Wassup.BattleCore` 를 참조) |
| Unity 층 | `Assets/_Project/Scripts/BattleCoreUnity/`(Runtime asm 안, asmdef 없음): `MatchDefinitionBuilder`(SO→정의표 + `configHash`) · `BattleDriver`(누산기·틱 발행률 — 최소형) |
| 에디터 | `Assets/_Project/Editor/BattleCore/`: `CoreHarnessRunMenu` · `CoreGoldenMenu`(얇은 메뉴 — 몸통은 코어의 `CoreHarness`) |
| 테스트 | `Assets/_Project/Tests/EditModeCore/Wassup.Tests.EditMode.Core.asmdef`(refs BattleCore·Skills·UnitAi·nunit — **Entities 0**) · 골든 `Assets/_Project/Tests/GoldenCore/*.trace.txt` |
| 헤드리스 lane | `Tools/battle-core-rebuild/headless/BattleCore.csproj` + `BattleCore.Tests.csproj`(NUnit) — 같은 소스를 dotnet 으로 컴파일·실행. Unity 가 없을 때의 반복 lane. 정본 lane 은 Unity EditMode |
| 감지기 | `.claude/hooks/ecs-review-detector.mjs` 에 `Scripts/BattleCoreUnity/`·`Editor/BattleCore/`·`Tests/EditModeCore/` 경로 추가 |

## 구현

1. **개체.** `Unit` 은 plain class, `SimEntityId` 1급(Match 호스트 0 · 스폰 1~ · `None = -1`). `BattleWorld` 는 id 오름차순 리스트 + 풀 대여. 이 unit 에서 `Unit` 은 `Id·Kind·Faction·Position·HitRadius·Health·Dead·Deploying` 까지(부분 객체는 unit 2·3 에서).
2. **커맨드.** `Command{Kind: PlaceDefender·Retire·Submit·DebugSpawnEnemy·DebugDestroy}` struct. phase 0 에서 동기 적용, `Receipt{accepted, RejectReason}`. `RejectReason` 은 코어 enum(옛 `PlacementRejectReason`·`DcRejectReason` 값을 **이름으로** 옮김, 번호 무관). 배치 판정 자체는 unit 4(`PlacementService`) — 이 unit 은 「빈 칸이면 수락」 스텁.
3. **이벤트.** `CoreEvent{Kind, tick, a, b, SiteFired, SiteTarget, faction, amount}` struct. `EventBus.Publish/Subscribe(kind, order)`. 이 unit 의 종류: `MatchStarted·UnitSpawned·UnitDestroyed·MatchEnded`. **모든 소멸 경로는 `UnitDestroyed` 를 낸다**(계약 7) — `BattleWorld.Destroy` 한 곳만 지운다.
4. **틱.** `TickPipeline` 은 `ITickPhase[]` 순서 나열. 이 unit 의 phase: `CommandPhase` → (빈 자리 6개는 unit 2~4 가 끼움) → `MatchClock.Step` → `FlushPhase`. `dt = 1/60` 상수. `BattleMatch` 는 담당자 생성 + 순서 나열 + `Apply/Tick/Events` 위임만(계약 12).
5. **시계·종료.** `MatchClock` 이 `tick·battleTime·timer` 소유. 만료 → `EndMatch("complete")` → `MatchEnded` 이벤트 → 이후 `Tick()` 은 no-op(계약 5). `Submit` 커맨드 → `EndMatch("submitted")`(해금 60초는 `ModeDef.submitUnlockSec`). `stress_full` 은 unit 4.
6. **정의표.** `MatchDefinition{seed, configHash, ModeDef, UnitDef[], EnemyDef[], MapSnapshot(stub: size·spawns·goals)}`. `MatchDefinitionBuilder` 가 `DefenderUnitData`/`AttackUnitData`/`MatchModeData`/스테이지에서 plain 값으로 굽고 **`configHash` 를 정의표에서 계산**(옛 `MatchConfigSnapshot` 의 리플렉션 접기 대신 명시 필드 직렬화 — 아트 참조 없음, `modeId` 포함). `MatchModeData` SO 는 unit 4 에서 — 이 unit 은 `ModeDef` 를 코드 기본값(180s·submit 60)으로 채운다.
7. **RNG.** `RngStreams` = `MatchSeed.Derive*` 6계열 → `Unity.Mathematics.Random` 6개. 옛 상수 계승.
8. **하네스.** `CoreHarness.Run(MatchDefinition, CommandSchedule, ticks) → CoreTrace`. `CoreTrace` 는 `LegacyTraceV0` 포맷(채널 append-only, 값은 `SimEntityId`). 새 채널 번호는 옛 22 뒤에 이어 붙이지 않고 **새 트레이스 파일 계열**(`GoldenCore/`)로 분리 — 옛 골든과의 비교는 순서 비교(계약 3).
9. **골든.** 시나리오 `empty_board`(커맨드 0 · 10,800틱) · `spawn_destroy`(DebugSpawn 3 · DebugDestroy 1) 두 종. `Bake Missing` / `Verify` 메뉴.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `MatchConfigSnapshot` 의 리플렉션 전량 접기 | 정의표가 명시 필드이므로 직렬화가 곧 해시 입력. 리플렉션은 「SO 가 무엇을 갖는지 모른다」는 전제의 산물 | 보류(의도만: 조건 물질화 + 해시) |
| `SimHarnessClock` 의 `Time.captureDeltaTime` 고정 | 코어는 프레임을 모른다. 드라이버가 틱 수로 시간을 만든다 | 보류 |
| 옛 트레이스 채널 22 번호 | 새 계열로 분리 | 보류 |
| `Entity.Null` 센티널·`int.MaxValue` 미발급 | `None = -1`, 정렬 밖 | 보류 |

## 완료 기준

- [ ] Unity: `Wassup.BattleCore.asmdef` 컴파일, `noEngineReferences` 위반 시 컴파일 실패 확인(의도적 `using UnityEngine` 1줄로 빨강 → 제거).
- [ ] 헤드리스: `dotnet build Tools/battle-core-rebuild/headless/BattleCore.csproj` 오류 0 · `dotnet test …/BattleCore.Tests.csproj` 초록.
- [ ] EditMode.Core 테스트: ① 10,800틱 뒤 `MatchEnded(complete)` 정확히 1회, 이후 틱 no-op ② 같은 정의표+스케줄 2회 → 트레이스 해시 동일 ③ `Submit` 은 60초 전 거절·후 수락 ④ `SimEntityId` 스폰 순번·`None` 정렬 배제 ⑤ `UnitDestroyed` 없이 사라진 유닛 0(소멸 경로 전수 = `BattleWorld.Destroy` 1곳) ⑥ (Assets lane) 같은 SO 로 두 번 빌드 → `configHash` 동일, 아트 참조 교체 → 동일, `modeId` 변경 → 상이.
- [ ] 골든 2종 베이크·검증 통과.
- [ ] `core-reviewer` 리뷰 APPROVE(매니저 재생성 0 · 엔진 참조 0).
- [ ] `check_ledgers.py` exit 0(옛 브리지는 무변이라 자명).
