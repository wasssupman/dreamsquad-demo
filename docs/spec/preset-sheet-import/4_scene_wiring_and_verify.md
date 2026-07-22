# 4 — 씬 배선 + Play 왕복 검증

## 목적

`PresetSheetRuntimeRefresher` 를 OutgameScene 에 배치·배선하고, 에디터 import 와 로그인 자동 import 를 실제로 왕복 검증한다.

## 변경 대상

- `Assets/_Project/Scenes/OutgameScene.unity` (refresher GO + `AllRuntimeRefresher.refresherSources` 항목 추가 + refs 배선)

## 구현

**배선** (UnityMCP; execute_code 불가 시 loadout-preset-page 처럼 일회용 에디터 MenuItem 스크립트 후 삭제):
1. 기존 refresher 루트 하위에 `PresetSheetRuntimeRefresher` 컴포넌트 GO 추가.
2. refs 배선: `collection` = `SquadPresetCollection.asset`, `defenderCatalog` = `DefenderCatalog.asset`, `cardCatalog` = `DreamcatcherCardCatalog.asset`, `baseUrl` = dev 기본.
3. `AllRuntimeRefresher.refresherSources` 배열에 이 컴포넌트 append(기존 Unit/DC refresher 옆).

**검증**:
- **에디터 import 왕복**: (a) 현 프리셋 export → `Presets` 탭 seed, (b) 시트에서 이름/한 슬롯 변경, (c) Import → `git diff Assets/_Project/Data/Preset/` 가 그 변경만 반영, (d) 미해결 id 넣어 unmatched 로그 확인.
- **로그인 자동 import**: Play → 로그인 게이트 통과 → 콘솔 `[LoginAutoImport]` / `ALL:` 로그에 Preset refresher 결과 포함 확인.
- **페이지 반영**: 프리셋 패널 열어 시트대로 렌더(스크린샷), 재오픈 시 중복 없음.
- 검증용 부수효과(seed 되돌림/스크린샷/일회용 배선 스크립트) 정리.

**주의**(loadout-preset-page 교훈 승계):
- 씬 저장이 사용자 미저장 WIP 를 박을 수 있음 — 배선 hunk 만 격리 커밋(commitPop/hash-object 기법).
- 런타임 refresh 는 in-memory — import 결과를 커밋하려면 **에디터 import** 로 `.asset` 에 써야 함(런타임은 저장 안 함).

## 완료 기준

- 씬 YAML: refresher refs(collection/defenderCatalog/cardCatalog) non-zero, `AllRuntimeRefresher.refresherSources` 에 항목 존재.
- 에디터 import 왕복 1회 성공(diff = 의도한 변경만).
- 로그인 자동 import 로그에 Preset 포함 + 페이지 반영 스크린샷.
- feature 종료 → README 상단 "완료" + `5_handoff_summary.md`.
- ✅ 배선 완료 2026-07-22 · commit `0ae9a2c7` — `UnitStatRefresher` GO 에 PresetSheetRuntimeRefresher 추가, refs(collection/defenderCatalog/cardCatalog/baseUrl) non-zero, `refresherSources` 2→3. 씬 clean 상태에서 배선 delta 18줄만 저장(WIP 베이킹 0).
- ⏳ **라이브 검증 대기**: 에디터 import 왕복 + 로그인 자동 import + 페이지 반영은 **`Presets` 시트 탭 신설 후**. (에디터/런타임 apply 는 unit 2·3 에서 export 바디 round-trip 으로 구조 검증됨 — units 14/0/0·cards 20/0.)
