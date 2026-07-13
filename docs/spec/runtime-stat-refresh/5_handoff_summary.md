# 5. Handoff Summary — runtime-stat-refresh 드림캐쳐 확장 (units 3~4)

## Commit

- (이 커밋) feat(dreamcatcher): runtime-stat-refresh 드림캐쳐 확장 — 로비 IMPORT DREAMCATCHER 버튼 + in-memory 리프레셔

## Implemented

- `DcSheetRuntimeRefresher` (Core, MonoBehaviour): 로비에서 6 DC탭 fetch → 메모리 SO 인스턴스에 apply(디스크 미저장, 세션 한정). 유닛 리프레셔와 동일 코어(`SheetFetcher`/`SheetEnvelopeParser`/`UnitStatApplier.BuildIndex`/`DcSheetApplier`) 재사용, 열거 소스만 카탈로그+명시참조.
- 런타임 SO 열거 전량: catalog.cards(23) ∪ activeCards(6) = 29카드, activeCards[].skill = 6스킬, awakeningConfig + catalog.ruleConfig = 2config.
- `IRuntimeRefresher` 인터페이스(구현체 2개=추출 정당): 두 리프레셔가 구현, `StatRefreshButtonView` 가 refresher-agnostic 로 일반화(MonoBehaviour 참조 → 캐스트, idleLabel 직렬화).
- OutgameScene 2버튼: 기존 버튼 재배선(refresherSource=UnitStatRuntimeRefresher, "IMPORT UNIT") + 신규 DreamcatcherRefreshButton(refresherSource=DcSheetRuntimeRefresher, "IMPORT DREAMCATCHER"). dev 게이트(Debug.isDebugBuild || isEditor) 동일.

## Key Files

- `Assets/_Project/Scripts/Core/Dreamcatcher/DcSheetRuntimeRefresher.cs` — 드림캐쳐 런타임 apply 코어
- `Assets/_Project/Scripts/Core/IRuntimeRefresher.cs` — 버튼 공유 계약
- `Assets/_Project/Scripts/UI/Outgame/StatRefreshButtonView.cs` — 일반화된 버튼(2 인스턴스 구동)
- `Assets/_Project/Scenes/OutgameScene.unity` — 2버튼 + DcSheetRuntimeRefresher 배선
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetRuntimeRefreshTests.cs`

## Verified

- EditMode 722 pass / 0 fail / 2 pre-existing skip (신규 DC 리프레셔 2케이스 포함).
- 씬 배선 컴포넌트 실구동: Matched 60 / unmatched 0 / skipped 0 (fetch→apply end-to-end, 실 시트).
- Play: DreamcatcherRefreshButton 활성 노출 + onClick→Refresh 전 경로 완주(`[StatRefresh]` 콜백).
- YAML: 두 refresherSource 올바른 컴포넌트 매핑 + cardCatalog/activeCards/awakeningConfig non-null.

## Notes

- **빌드에선 in-memory only** — .asset 디스크 저장 불가(에디터 전용 API). 값은 세션 한정, 앱 재시작 시 빌드값 복귀. 에디터 영구 반영은 `Window/Wassup/Unit Stat Import`.
- resultLabel 은 두 버튼 공유(최근 클릭 결과 표시) — dev 툴이라 의도적 단순화.
- activeCards[] 는 명시 참조 → 새 Active 카드 추가 시 이 배열에도 등록해야 런타임 갱신 대상에 포함.
- 릴리즈 빌드는 두 버튼 숨김(dev API 주소·밸런스 변조 노출 방지).

## Follow-up

- 실기기 Development Build 에서 두 버튼 1회 확인(units 2 잔여와 동일).
- README 후속 후보: 앱 시작 자동 fetch · 갱신 diff UI · 제네릭 `ScriptableCatalog<T>`(5번째 카탈로그 조건).
