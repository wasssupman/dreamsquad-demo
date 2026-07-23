# 8. Handoff — CostConfig 탭 (unit 7)

> units 0~5 의 인계는 `6_handoff_summary.md`. 이 문서는 unit 7 이후만 다룬다.

## Commit

- `bf68fb63` feat(sheet-export-push): unit 7 — CostConfig 탭 + 로비 dev 버튼
- `a4d15cdc` fix(runtime-stat-refresh): 이미 로그인된 채 로비 진입 시에도 자동 import 발화

## Implemented

- `CostConfig` 시트 탭 신설 — `DcConfig` 와 같은 flat-config(행 키 `id`, 동명 필드 reflection)
- `CostConfigDto` — export/import 공용 행 타입. nullable 필드가 blank=keep 을 양방향 보장
- `CostConfigSheetApplier` — 에디터 import(AssetDatabase 인덱스 + 디스크 저장)와 런타임 refresher(인스펙터 참조 + in-memory)가 공유
- `CostConfigRuntimeRefresher` — `IRuntimeRefresher` 4번째 구현체. 로비 `IMPORT COST` 버튼 + `AllRuntimeRefresher` 편입
- 창에 `Cost Sheet` 탭명 필드(EditorPrefs) + Import/Export 버튼, Push payload 에 탭 추가
- `Code.gs` `KEY_CONFIG` 에 `CostConfig: ['id']` — **웹앱 재배포 완료됨**
- `CostConfig.id` 필드 추가분은 병행 세션 커밋 `78809293` 에 흡수돼 있다(에셋 백필만 `bf68fb63`)

## Key Files

- `Assets/_Project/Scripts/Data/StatImport/CostConfigDto.cs` · `CostConfigSheetApplier.cs`
- `Assets/_Project/Scripts/Core/CostConfigRuntimeRefresher.cs`
- `Assets/_Project/Editor/UnitStatImport/CostConfigSheetExporter.cs` · `SheetPushPayload.cs` · `UnitStatImportWindow.cs`
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs` (발화 수정)
- `docs/spec/sheet-export-push/apps-script/Code.gs`

## Verified

- EditMode 1266 중 실패 0 (신규 `CostConfigSheetTests` 6 포함)
- 라이브 push 왕복: `CostConfig` 탭 생성 `added 1`, 기존 9탭 `added 0`(무회귀), 시트 재조회로 행 확인
- `IMPORT COST` 버튼 반영 (사용자 확인)
- 자동 import 실측: 시트 10 / SO 5 → Play#1 10 → 종료(`IsSignedIn` 잔존) → SO 3 으로 흐트림 → Play#2 **10 복귀**

## Notes (되돌리면 안 되는 것)

- **`SheetPushPayload` 의 `AddTab(costTab)` 은 무조건**이 맞다. `PresetSheetExporter` 의 bool 가드는 list-replace(파괴적) 탭 전용 보호이고, keyed-upsert 는 0행 `[]` 이어도 서버가 `updated 0/added 0` + 고아 리포트로 끝나 비파괴다.
- **`CostConfigSheetApplier` 가 `DcSheetApplier.ApplyFlat` 과 중복**인 것은 의도된 수용이다. 근거와 추출 조건은 `7_costconfig_tab.md` 후속 후보 참조. 이미 드리프트가 있다(이쪽만 null 방어).
- **런타임 refresher 는 `onApplied: null`** — in-memory 전용이라 에셋을 저장하지 않는다. 재시작하면 원복된다.
- **반영 시점은 "다음 전투부터"** — `GameManager` 가 battle-scoped 라 `Awake` 에서 `CostRuntime.Configure` 를 다시 읽는다.
- **읽기 프록시와 시트는 별개 계층** — push 직후 `dev-api-somnia` 가 stale 값을 줄 수 있다. 시트가 진실.

## Follow-up

- 값 검증 없음: 시트에 `startingCost` 0/음수를 넣으면 그대로 `CostRuntime.Configure` 로 간다. import 경로 전반이 동일한 성질이라 별도 판단 사항.
- `BattleConfig`/`ScoreRules` 등 나머지 전역 튜닝 SO 도 같은 flat-config 패턴으로 확장 가능.
- 3번째 flat 탭이 생기면 `ApplyFlat` 공통화 판단.
