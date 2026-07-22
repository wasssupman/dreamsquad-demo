# 3 — 런타임: PresetSheetRuntimeRefresher + 페이지 재빌드

## 목적

dev/QA 빌드에서 로그인 후 `Presets` 탭을 fetch 해 `SquadPresetCollection` 을 **메모리에서** 갱신하고(에셋 저장 없음, 재시작 원복), 프리셋 페이지가 재오픈 시 최신을 반영하게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/PresetSheetRuntimeRefresher.cs` (신규 — `IRuntimeRefresher`)
- `Assets/_Project/Scripts/UI/Outgame/PresetPageController.cs` (편집 — OnEnable 재빌드)
- `Assets/_Project/Tests/EditMode/PresetImport/PresetSheetRuntimeRefresherTests.cs` (신규, 선택)

## 구현

**PresetSheetRuntimeRefresher : MonoBehaviour, IRuntimeRefresher** (`Wassup.Core`) — `DcSheetRuntimeRefresher` 형제:
- `[SerializeField]` `SquadPresetCollection collection`, `DefenderCatalog defenderCatalog`, `DreamcatcherCardCatalog cardCatalog`, `string baseUrl`(=dev 기본). 탭 상수 `"Presets"`.
- `Refresh(Action<string>)`: `RequestInFlight` 가드 → `SheetFetcher.Fetch(BuildSheetUrl(baseUrl,"Presets"))` → `ParseSheetLogged<PresetDto>` → `PresetSheetApplier.Apply(rows, defenderCatalog.ById, cardCatalog.ById, SquadSave.SlotCount, collection, log)` **in-memory**(저장 콜백 없음) → try/catch/finally 로 `RequestInFlight` 해제 + onDone(log). (`DefenderCatalog.ById`/`DreamcatcherCardCatalog.ById` 는 `Func<string,SO>` 로 그대로 전달 — 프로필이 참조 가능한 authoritative 인덱스.)
- `internal static ApplyBody(SheetFetcher.Result, collection, defenderCatalog, cardCatalog)` — 네트워크 없는 EditMode 구동용(기존 `ApplyBodies` 기법).
- `IRuntimeRefresher` 구현체는 이미 2개(Unit/DC) → 3번째 추가는 인터페이스 규칙 위반 아님(제약 8).

**PresetPageController 재빌드** (계약 4 예외):
- 현재 `OnEnable` 은 `_built` 로 1회만 `BuildItems`. 런타임 갱신이 재오픈 시 보이도록 **매 OnEnable 재빌드**로 변경:
  - `content` 하위 기존 `PresetItem` 자식 파괴 후 `BuildItems` 재실행(중복 자연 방지).
  - `confirmPopup.Init(font)` 는 **1회만**(별도 guard) — 팝업은 정적.
- 근거: 런타임 refresh 로 `collection.presets` 가 바뀌므로 "authoring 정적 → 1회 빌드" 전제(loadout-preset-page 계약 7)가 dev 빌드에선 성립하지 않는다. 재빌드가 일관된 진화.

**배선**: 이 컴포넌트를 `AllRuntimeRefresher.refresherSources` 에 추가 → `LoginAutoImport` 가 컴포지트를 이미 구동하므로 로그인 자동 반영(실 배선은 unit 4).

## 완료 기준

- compile green.
- (선택) EditMode: fake `Result` + in-test 카탈로그로 `ApplyBody` 가 collection 재구성.
- 페이지 재오픈 시 최신 `collection.presets` 반영(중복 아이템 없음). unit 4 Play 에서 실증.
