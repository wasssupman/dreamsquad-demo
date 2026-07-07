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

## `run_tests` 필터는 0-match — 전체 실행 후 failures 스캔

`run_tests(test_names=...)`/`group_names=...` 로 지정하면 이 셋업에선 total=0 으로 아무것도 안 돈다.

- **처방**: `assembly_names=["Wassup.Tests.EditMode"]` 만 주고 **전체 실행** + `include_failed_tests=true` → `failures_so_far` 스캔. `failures_capped=false` 면 거기 없는 테스트는 통과.
- **알려진 무관 사전실패**: `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`(Expected ≥36, was 31) 는 상시 실패 — 회귀로 오판 금지.

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
