# Unity MCP 운용 함정

Unity Editor 를 MCP(MCP for Unity)로 구동·검증할 때 반복해서 겪은 것들.

## Play 시뮬은 에디터 포커스가 있어야 tick 한다

MCP `execute_code` 로 Play 를 구동해도, **에디터 창이 포커스를 잃으면 시뮬레이션이 frame 을 진행하지 않는다** (Time.time/frameCount 고정, ECS 미실행 → 이동/aggro/공격/데미지 정지). `Application.runInBackground=true` 도 에디터에선 안 먹힘.

- **처방**: 시뮬 진행이 필요한 라이브 측정(적 이동·전투·aggro)은 **사용자에게 Game 뷰 포커스를 요청**한 뒤 측정. 한 프레임 정적 스냅샷(위치/컴포넌트 읽기)은 포커스 없이 가능.

## `execute_code` 는 method body — using 금지, 풀네임

`execute_code` 는 코드를 **method body** 로 컴파일한다(CodeDom, C#6).

- `using` 지시문 금지 → `Wassup.Data.PropData` 처럼 풀네임. UnityEngine/UnityEditor 는 암시적.
- bridge 내부 상태(`_defenderByTile`/`_effectTilesByCell`/`_generatedMap`)는 reflection 으로 조회.
- const 필드는 reflection 으로 못 바꾼다.

## 씬 저장 없이 in-memory 로 배선 검증

`BattleScene.unity` 가 무관한 미커밋 변경으로 dirty 일 때, scene-dependent 기능을 씬 저장으로 검증하면 오염이 섞인다. 회피:

1. `execute_code`(edit 모드)로 GameObject 생성 + private SerializeField 를 reflection 주입. **SaveScene 안 함.**
2. `manage_editor play` — in-memory 씬이 그대로 Play 진입(디스크 미반영).
3. 빌드/전투 트리거(`bb.PrepareDraftMap()`/`StartBattle()`) → reflection·ECS 쿼리·screenshot 으로 검증.
4. `stop` → 임시 GO `DestroyImmediate` + 필드 원복. **저장 안 함** → 디스크 baseline 유지.

영속(빌드/실기기) 동작은 결국 씬 저장 필요 — in-memory 는 "코드 맞음"까지.

## 반복 Play 후 EditMode 거짓 실패 = Play 잔류 오염

MCP 로 `play/stop` + `execute_code` 로 객체 생성/파괴·static 수정을 여러 번 반복한 직후 EditMode 전체 스위트를 돌리면 `BattleBridgeDraftMapTests`/`DraftControllerMapRebuildTests` 가 `Destroy may not be called from edit mode!` 로그 누출로 **거짓 실패**할 수 있다(격리 실행에서도 재현).

- **처방**: 회귀로 단정하기 전에 `EditorUtility.RequestScriptReload()` → `refresh_unity(wait_for_ready)` → 재실행. **도메인 리로드 후 깨끗한 상태의 결과만 신뢰.**

## `refresh_unity mode=force` 는 브리지를 끊는다

`refresh_unity mode=force`(특히 Play 중)는 **전 프로젝트 에셋 reimport** 를 트리거 → 수 분 정지 + **MCP 브리지 단절**(`instances` → `instance_count:0`). macOS 가 비포커스 Unity 를 스로틀해 자동 재연결 안 됨.

- **머티리얼 값(색/두께/토글)**: reimport 불필요 → `execute_code` 로 `mat.SetFloat/SetColor` (+Play 중 즉시 반영) → 영속 필요 시 `SetDirty`+`AssetDatabase.SaveAssets()`(해당 에셋만). **`mode=force` 금지.**
- **셰이더 신규/수정**: `refresh_unity(mode=if_dirty, scope=assets)`, 컴파일 확인은 `ShaderUtil.ShaderHasError`.
- 브리지가 `instance_count:0` 이면: 사용자에게 **Unity 창 클릭/포커스** 요청 → 수 초 내 재등록(CLI 복구 불가).

## `run_tests` 필터는 0-match — 어셈블리 단위 실행 후 failures 스캔

`run_tests(test_names=...)`/`group_names=...` 로 지정하면 이 셋업에선 total=0 으로 아무것도 안 돈다. **동작하는 유일한 입도 = `assembly_names`.**

- **EditMode 는 두 lane** (test-suite-fast-lane unit 0, 2026-08-16 분리):
  - `assembly_names=["Wassup.Tests.EditMode"]` = **고속 코어** (~2,230개 ~30초). 실제 프로젝트 에셋을 로드하지 않아 시트 임포트·에셋·맵 편집으로 깨지지 않는다. 코드 변경 루프는 이것만.
  - `assembly_names=["Wassup.Tests.EditMode.Assets"]` = **에셋/어소링 검증** (~155개 ~10초). 실에셋(SO·맵·덱·카탈로그) 로드 테스트 전부. 시트 임포트·에셋·맵·콘텐츠 편집 후에는 이것을 **반드시 추가 실행**.
  - `assembly_names` 를 아예 안 주면 둘 다 + `Wassup.DepthParallax.Tests`(6개)까지 전체 실행 — 커밋 전 게이트.
- **처방**: `include_failed_tests=true` → `failures_so_far` 스캔. `failures_capped=false` 면 거기 없는 테스트는 통과.
- **알려진 무관 사전실패**: `MultiGoalPoolSeparationTests` 4건(Coil/Twin/Spiral/Zig, 근접 차단칸 ≥40%)은 map-rework 재저작 대기의 **의도적 빨강** — Assets lane 에 있다. 회귀로 오판 금지. (~~ObstaclePlacerTests 상시 실패~~ 는 2026-08-16 실측에서 통과 — 해소된 것으로 보이며 재발 시 여기 갱신.)

## 신규 `.cs` 는 `scope=all` refresh 필수

Write 로 만든 새 `.cs` 는 `refresh_unity(scope=scripts)` 로는 import 안 됨 → .meta 미생성 → 어셈블리 타입 누락 → 참조 전부 cascading CS0246. 반드시 `refresh_unity(scope=all, compile=request)`. (기존 파일 수정은 `scope=scripts` 로 충분.) 어셈블리 하나라도 컴파일 실패하면 전 타입이 CS0246 이니 진짜 원인(한 파일의 CS0102 등)을 먼저 찾을 것.

## 드래그/프리뷰 UI 애니메이션 검증 = TimeManager 동결

매치가 프레임 진행으로 저절로 끝나 UI 애니 검증이 날아갈 때:

- 배틀 완전 동결은 `TimeManager.Request(TimeDomain.Battle, 0f)`. (`Time.timeScale=0` 은 time-manager 커밋 c2fe03d 이후 웨이브/타이머를 못 멈춘다 — `_battleClock` 이 unscaledDeltaTime 기반. → `04-sim-design.md`, `Time.timeScale` 금지.)
- sway/프리뷰 Update 는 `Time.unscaledDeltaTime` 을 써서 동결 중에도 애니메이트됨.
- 컨트롤러 **`.enabled=false` 금지** — `OnDisable→CleanupSession` 이 프리뷰를 파괴. 정적 고정은 상태 필드 직접 세팅.

## VFX 후보 비교는 파티클 Simulate 동결 라인업으로

움직이는 VFX(투사체 낙하, 폭발)를 MCP 스크린샷으로 비교하려면 실시간 캡처는 실패한다 — MCP 왕복(1~4초)이 재생 창(0.5~1.5초)을 계속 놓친다. 정답:

1. 후보 프리팹들을 보드 위에 **일렬로 Instantiate**(빈 GO 부모 `__CandidateLineup` 아래).
2. `ps.Simulate(0.25~0.55f, true, true); ps.Pause(true);` 로 **원하는 재생 시점에 동결**.
3. 스크린샷 1장에 전 후보 비교 → 사용자 픽. 다른 시점은 재-Simulate 후 재촬영.
4. 부모 GO `DestroyImmediate` 로 정리. (TrailRenderer 는 Simulate 안 되므로 트레일 느낌은 실플레이/사용자 육안으로.)
## `execute_code` 가 mono 커맨드라인 길이로 고장난 환경

이 프로젝트/머신에서 `execute_code` 는 코드·경로와 무관하게 `mono.exe: 파일 이름이나 확장명이 너무 깁니다` 로 즉사한다 (CodeDom 컴파일이 전 어셈블리 참조를 커맨드라인에 나열 → Windows 한계 초과).

- **처방**: 에디터 내 일회성 실행이 필요하면 **임시 `[MenuItem]` 정적 메서드 스크립트**를 Write → `refresh_unity(scope=all, compile=request)` → `execute_menu_item` → 결과는 `Debug.Log` 대신 **파일로 기록** (`read_console` 은 멀티라인 메시지를 첫 줄로 자른다) → 검증 후 스크립트+meta 삭제 + refresh. 실례: unit-stat-spreadsheet-schema 왕복 검증 (2026-07-06).
- 세션 중 `claude mcp add` 로 등록한 MCP 서버는 그 세션에서 안 잡힌다 — 브리지에 HTTP JSON-RPC 직결(initialize → Mcp-Session-Id 헤더 유지)로 우회 가능.

## Codex 도 unityMCP 를 쓸 수 있다

Codex 에도 unityMCP 가 붙어 있어(`~/.codex/config.toml`) 에디터 작업 위임이 가능하다. 단 긴 Play 작업은 백그라운드로 빠져 회수가 불안정 — 짧은 조회/조작 위주로.

## PlayMode 전투 테스트: 합성 더미는 **멜리 전용**, 투사체는 안 맞는다

**증상**: PlayMode 통합 테스트에서 디펜더를 배치하고 `em.CreateEntity()` 로 만든 합성 더미 적(`Health`+`FactionTag`+`IncomingDamage`+`LocalTransform`)을 사거리 안에 두면 — **멜리 유닛(guardian)은 정상 공격·데미지**가 들어가는데, **투사체 유닛(ranger)은 대상을 아예 못 맞힌다**. 피격 데미지·`ProjectileState.damage` 둘 다 0 (거리 0.05/2 무관, dreamcatcher-new-abilities 마감 때 4회 시도 전부 0).

**원인(추정)**: 멜리 RESOLVE 는 대상에 `IncomingDamage` 를 직접 append 한다(더미로 충분). 투사체는 `AttackSystem` 이 spawn → `ProjectileMoveSystem`(호밍/impact) → `ProjectileHitSystem`(`impactReached` 시 데미지)로 이어지는데, 이 경로가 실 enemy 아키타입에만 있는 무언가(스폰 배선/컴포넌트)를 요구한다. **기존 dreamcatcher combat 테스트가 전부 melee guardian 인 게 이 이유.**

- **처방**:
  - 멜리 경로(직접 데미지·온-히트 CC/스택)는 합성 더미로 검증 가능.
  - 투사체 고유 경로(bake 데미지·splash·bounce·homing impact)를 실기로 검증하려면 **실 enemy 를 웨이브로 스폰**(`StartBattle()` + `bridge.ForceNextWave()`, `MovementIntegritySmokeTest` 참고)해야 한다. 단 실 적은 이동하므로 데미지-윈도 비교가 지저분함.
  - 검증 대상이 **"데미지 산식"**(예: shatter DamageVsCc 배율)이면 melee 경로 통합 테스트로 산식을 고정하고, 투사체 bake 지점(`AttackSystem` 의 projectile 분기)은 동일 곱을 쓰는지 **코드/리뷰로 확인** — melee 테스트가 산식을 증명하면 bake 지점은 같은 `attackerVsCc` 곱 한 줄이라 회귀 위험이 낮다.

## `execute_code` 안의 `Screen.*` 는 게임뷰가 아니라 에디터 창이다

`execute_code` 는 플레이어 루프 **밖**(에디터 콜백)에서 돈다. 이때 `Screen.width/height/safeArea` 는 **현재 에디터 뷰**의 크기를 답한다 — 게임뷰가 아니다.

- **실측(2026-07-15)**: `Screen`=519x830 인데 `Camera.main.pixelRect`=1920x1080. 즉 `Screen` 이 완전히 다른 값을 준다.
- **증상**: 스크린 좌표를 쓰는 UI 로직(safe area 클램프·좌우 플립)을 execute_code 에서 검사하면 **없는 버그를 만들어낸다**. 실제로 "패널 좌측 플립이 안 먹는다"고 오진했다가, 플레이어 루프의 `LateUpdate` 는 올바른 값을 본다는 걸 확인하고 철회했다.
- **처방**: 게임뷰 해상도는 **`cam.pixelRect`** 로 읽는다. `Screen.*` 에 의존하는 코드를 execute_code 에서 **직접 호출**하면(예: `view.Show(...)`) 그 안의 계산도 오염된다 — 다음 프레임 `LateUpdate` 가 교정하므로 **한 프레임 기다렸다가** 값을 읽을 것.

## `ScreenSpaceOverlay` 는 카메라 스크린샷에 안 잡힌다

`manage_camera screenshot` 은 카메라 렌더 경로다. **Overlay 캔버스는 카메라가 아니라 화면이 합성**하므로 UI 가 통째로 빠진다(HUD 전부 사라짐 → "패널이 안 뜬다"로 오진).

- **처방**: UI 를 포함한 최종 프레임은 `UnityEngine.ScreenCapture.CaptureScreenshot(path)`. 프레임 끝에 기록되므로 **다음 execute_code 호출에서** 파일을 읽는다.

## 스크립트 배틀은 캡처하는 사이 끝난다 → 배틀 클럭을 스톨

Play e2e 중 스크린샷을 몇 번 찍으면 매치가 `Result` 로 넘어가 버린다. 페이즈 이탈 로직이 정상 동작해 UI 가 닫히면 **"내 기능이 깨졌다"로 오진**한다.

- **처방**: `TimeManager.Instance.Request(TimeDomain.Battle, 0.02f, priority: 1000)` 로 배틀 클럭만 늦춘다. `Time.timeScale` 은 건드리지 않는다(프로젝트 계약). 높은 priority 로 다른 lease 를 눌러 두면 캡처 시간이 넉넉해진다.
- **주의**: 그 스톨 lease 가 승자가 되므로 `ScaleOf(Battle)` 로는 피검증 lease 를 못 본다. lease 검증은 `TimeManager._requests` 를 reflection 으로 열어 **priority 로 세는 게** 정확하다.

## 도메인 리로드는 런타임 생성 UI 를 "고아 자식"으로 남긴다

Play 중 스크립트를 고치면 도메인 리로드가 일어나는데, 리로드는 필드를 **선택적으로만** 복원한다. 절차적으로 UI 를 짓는 컴포넌트(`SquadRosterBrowser`, `SquadUnitDetailView` 등)에서 이 비대칭이 함정이 된다:

- `_grid` / `cardRoot` 같은 **`UnityEngine.Object` 참조 → 살아남는다**
- `_built` / `_cardBuilt` 같은 **bool → 살아남는다** (그래서 `EnsureGridBuilt()` 가 no-op 로 넘어간다)
- `List<Cell>` 처럼 **`[Serializable]` 아닌 클래스의 컬렉션 → 비워진다**

결과: `ClearCells()` 가 빈 `_cells` 를 돌며 **아무것도 못 지우고**, 새 셀만 덧붙는다. 그리드에 64 고아 + 64 신규 = **128 자식**이 쌓이고, 레이아웃상 **옛 셀이 앞에 오므로** 스크린샷은 변경 전 모습 그대로다 → "내 정렬/레이아웃 코드가 안 먹었다"로 오진하기 딱 좋다(실제로 `_cells` 안의 순서는 정상이었다).

`_cardBuilt` 쪽은 반대 방향으로 샌다 — true 로 복원되니 `EnsureCardBuilt()` 가 통째로 스킵되어 **옛 레이아웃이 그대로 서 있는다**.

- **진단**: `_cells.Count` 와 `grid.childCount` 를 **같이** 찍는다. 어긋나면 고아다. 자식 수가 기대치의 정확히 2배면 거의 확정.
- **처방**: 컨테이너를 통째로 날리고 플래그를 되돌린 뒤 정상 경로로 재진입한다.
  ```csharp
  for (int i = host.transform.childCount - 1; i >= 0; i--)
      UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
  // _built=false, _grid=null, _cells.Clear()  (reflection)
  // 그 다음 EnterUnitMode/EnterStoneMode 같은 실제 진입점을 Invoke
  ```
- **빌드에는 없는 문제다.** 도메인 리로드는 에디터 전용이라 실기기/빌드에서는 재현되지 않는다. 고치려 들지 말고 **검증 절차로만 우회**한다.
- 곁가지: `ClearCells` 는 `Destroy`(지연) 를 쓴다. 같은 `execute_code` 안에서 파괴 직후 `childCount` 를 읽으면 아직 옛 자식이 잡힌다(17+64=81 같은 수치). **프레임을 넘겨 다시 읽어야** 진짜 상태다 — 위의 고아 문제와 증상이 비슷하니 혼동하지 말 것.

## 실행 중인 에디터에서 **에셋 강제 언로드·전역 저장 금지**

이 워크트리는 에디터 하나를 **여러 세션·실행 중인 게임과 공유**한다. 그 상태에서 두 API 가 사고를 냈다.

- **`Resources.UnloadAsset(so)`** — 그 에셋을 참조하던 **다른 쪽의 참조까지 죽인다.** 유닛 3종을
  언로드했더니 `DefenderCatalog.units[]` 26칸 중 3칸이 빈 칸이 됐고, id 로 카탈로그를 뒤지는
  스쿼드 화면이 그 셋만 못 찾아 **빈 슬롯**으로 보였다(에러 로그 0 — 조용히 깨진다).
  - ⚠ **Play 를 껐다 켜도 안 돌아온다.** ScriptableObject 인스턴스는 play mode 전환을 넘어
    메모리에 살아남는다. 복구는 **도메인 리로드**: `EditorApplication.isPlaying = false` →
    `EditorUtility.RequestScriptReload()`. 파일 touch·`ImportAsset(ForceUpdate)`·`AssetDatabase.Refresh`
    전부 실패했다(실측).
- **`AssetDatabase.SaveAssets()`** — 지금 dirty 한 **모든** 객체를 디스크로 민다. 문안 한 줄
  고치려고 불렀다가, 로비 임포터가 메모리에 넣어 둔 **남의 밸런스 값**(유닛 공격력)이 같이 디스크로
  나갔다. 저장은 `AssetDatabase.SaveAssetIfDirty(obj)` 로 **대상만**.

**메모리가 디스크와 어긋나 보일 때**(git 은 깨끗한데 `LoadAssetAtPath` 가 옛 값을 준다): 강제로
밀어내지 말고 도메인 리로드를 건다. 특히 게임이 Play 중이면 임포터가 메모리를 계속 덮으므로,
디스크 값이 필요한 작업(예: 시트 push 페이로드)은 **파일에서 직접 읽어 쓰는 편이 안전하다**.

## 시트 push 의 두 함정

- **페이로드는 Unity 메모리에서 만들어진다.** `SheetPushPayload.BuildCombinedJson` 이 exporter 를
  돌려 SO 를 읽는데, 로비 진입 임포트가 메모리를 시트 값으로 되돌려 놓으면 **방금 고친 값이 아니라
  옛 시트 값이 그대로 시트로 되돌아간다**(= 아무 일도 안 일어난 push). 디스크 값을 보내려면
  페이로드 JSON 의 해당 셀을 직접 갈아끼운다.
- **curl 로는 안 된다.** Apps Script `/exec` 은 POST 를 googleusercontent 로 리다이렉트하는데
  거기서 **405** 가 떨어진다(`-L --post301/302/303` 도 동일). 에디터의 기존 경로
  (`SheetPushClient.Push`, UnityWebRequest)로 보내면 통한다.
- **보내기 전에 읽기 전용으로 대조하라.** push 는 9탭 전량 업서트라 SO↔시트 드리프트가 있으면
  **남이 시트에서 조정한 값이 되돌아간다.** 탭별 키가 다르다(`Defenders/Enemies`=`id`,
  `DcCardEffects/DcMechanics`=`cardId`+`slot`) — 키를 잘못 잡으면 멀쩡한 탭이 200칸 바뀌는 것처럼 보인다.
