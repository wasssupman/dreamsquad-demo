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

> 실측 2026-09-23. **헤드리스 lane** 은 실행해 확인했고, **Unity lane** 은 에디터가
> 열릴 때 확인한다(작성 시점 에디터 세션 없음 — 컴파일을 주장하지 않는다).

- [x] 헤드리스: `dotnet build tools/battle-core-rebuild/headless/BattleCore.csproj` → 오류 0 · 경고 0.
- [x] 헤드리스: `dotnet test …/BattleCore.Tests.csproj` → **통과 31 / 실패 0**.
- [x] 엔진 참조 게이트: 코어 파일에 `using UnityEngine;` 1줄 → `error CS0246` 로 빨강, 제거 후 초록(의도적 확인).
- [x] 코어 소스 전수 grep(`UnityEngine`·`Unity.Entities`·`Unity.Collections`·`System.Random`·`DateTime`·`Time.`) → **코드 0건**(주석·README 만).
- [x] EditMode.Core 테스트 ①~⑤ 초록(헤드리스 lane 에서 같은 소스로 실행):
      ① 10,800틱 뒤 `MatchEnded(complete)` 정확히 1회 · 이후 틱 no-op
      ② 같은 정의표+스케줄 2회 → 트레이스 바이트 동일 · receipt 동일
      ③ `Submit` 은 3,600틱 전 거절(`SubmitLocked`) · 그 틱부터 수락
      ④ `SimEntityId` 스폰 순번 1~ · `None(-1)` 정렬 밖 · 소멸 id 재사용 없음 · 목록 오름차순 유지
      ⑤ 「스폰 − 소멸 이벤트 = 월드 잔존」 정확히 성립(소멸 경로 = `BattleWorld.Destroy` 1곳)
- [x] 골든 2종(`empty_board` 10,800틱 · `spawn_destroy`) 베이크 후 재실행 대조 통과. 왕복 게이트 통과.
- [x] `check_ledgers.py` exit 0 — 메서드 367 · 필드 91(옛 브리지 무변이라 자명).
- [x] 감지기에 새 경로 3종 추가 · `node --check` 통과.
- [ ] Unity: `Wassup.BattleCore.asmdef` 컴파일 · EditMode.Core lane 초록 · Assets lane ⑥(`configHash`) 초록 — **에디터 열릴 때**.
- [ ] `core-reviewer` 리뷰 APPROVE(매니저 재생성 0 · 엔진 참조 0).

### 이 unit 에서 갈린 결정 (spec 본문과 다른 것)

| 항목 | 문서 | 실제 | 이유 |
|---|---|---|---|
| `MatchSeed.GenerateRandom()` | 「엔진 무참조 확인됨」 | `UnityEngine.Random.Range` → `Guid.NewGuid()` | 그 한 줄만 엔진에 매여 있었다. 시그니처는 유지(호출처 둘 중 하나가 **동결된 `BattleBridge`**) |
| `CoreTrace` | 「`LegacyTraceV0` 포맷」 | 포맷은 같고 **클래스는 별도** + 헤더 `channels=core` | 채널 enum 이 다른데 한 타입을 쓰면 골든이 옛 채널 **이름**으로 읽힌다. 구분자가 없으면 두 계열이 육안으로 같다 |
| 거절 사유 이름 | — | `Occupied`(옛 `PlacementRejectReason` 그대로) | 「이름으로 옮긴다」를 문자 그대로 |
| `CoreEvent` 필드 | `{Kind, tick, a, b, Site×2, faction, amount}` | + `Arg`(int) | 트레이스 한 줄의 `i`/`f` 와 1:1 이 되어 뷰도 골든도 되묻지 않는다. `MatchEnded` 사유가 실릴 자리 |
| `configHash` 소유 | 「빌더가 계산」 | `MatchDefinition.ComputeConfigHash()`(코어) · 빌더는 호출만 | README 가 소유자를 `MatchDefinition` 이라 적었고, 코어에 두면 아트가 **타입 수준에서** 못 샌다 · 헤드리스에서 테스트 가능 |
| 헤드리스 경로 대문자 | `Tools/…` | `tools/…` | 인덱스에 이미 `tools/`(소문자)가 있다. macOS 는 같은 폴더지만 대문자로 커밋하면 대소문자 구분 파일시스템에서 디렉터리가 **둘**이 된다 |
