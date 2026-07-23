# 3. Handoff — 드림캐쳐 카드 노출 스위치

## Commit

- `9e89c49b` feat(dreamcatcher-card-visibility): units 0~2 — 카드 노출 스위치
- `a4d15cdc` fix(runtime-stat-refresh): 이미 로그인된 채 로비 진입 시에도 자동 import 발화

## Implemented

- `DreamcatcherCard.visible` (int, 기본 1) — `0` 만 숨김으로 해석
- `DcCardDto.visible` (int?) — 이름이 SO 와 1:1 이라 exporter/applier/서버 설정을 건드리지 않고 `DcCards` 탭에 컬럼이 생긴다
- `DreamcatcherDeckPageController.BuildPool` 에서 숨김 카드 제외 — `_pool` 이 그리드 소스이자 추가 가능 목록이라 한 지점이 "보이지도, 넣을 수도 없다"를 만든다
- `DeckPrune.RemoveHiddenCards` — 프로필의 모든 덱에서 숨김 카드를 제거하는 순수 함수
- `HiddenCardDeckPruner` — 로그인/로비 진입 시 정리, 제거가 있을 때만 `ProfileStore.Save`
- OutgameScene 배선 (`UnitStatRefresher` 호스트)

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs`
- `Assets/_Project/Scripts/Core/Profile/DeckPrune.cs`
- `Assets/_Project/Scripts/UI/Outgame/HiddenCardDeckPruner.cs` · `DreamcatcherDeckPageController.cs`
- `Assets/_Project/Tests/EditMode/Profile/DeckPruneTests.cs`

## Verified

- EditMode 1266 중 실패 0 (`DeckPruneTests` 7 포함)
- 인벤토리 필터 재현: 카드 하나 숨김 → 풀 25 → 24
- prune 재현: 숨김 카드 제거, 미해결 id(`ghost_unknown`) 보존
- 시트 왕복: export 37행 전부 `visible` 컬럼 / import 시 `0` 반영 · 키 생략 시 유지(blank=keep) · `1` 복귀
- 기존 37장이 백필 없이 `visible == 1` 로 읽힘

## Notes (되돌리면 안 되는 것)

- **기존 에셋 백필 안 함**이 의도다. Unity 이름 기반 역직렬화라 키가 없으면 초기값 `1`(노출)이 유지된다. `id` 처럼 비면 매칭이 깨지는 키가 아니므로 37장을 dirty 시키지 않는다.
- **카탈로그가 모르는 id 는 prune 이 남긴다.** 조용히 지우면 `LoadoutGate` 의 "카탈로그가 모르는 id" 진단이 사라진다.
- **덱 페이지는 숨김 카드를 `_working` 에서 빼지 않는다.** 이 페이지는 명시적 Save 계약이라 임의 편집을 만들면 안 된다 — 장착 해제는 prune 담당.
- **`HiddenCardDeckPruner` 에는 dev 게이트가 없다.** 빌드에 박힌 값만 읽는 로컬 연산이라 릴리즈에서도 돌아야 한다(`LoginAutoImport` 와 다른 점).
- **정원 미달은 의도된 결과다.** 9/10 이 되면 `DeckRules.Validate` → `LoadoutGate` 가 START 를 막고 덱 페이지로 유도한다. 자동 보충하지 않는다.
- **로그인 seam 함정**: `LoginPanelView.Start()` 의 `if (UserSession.IsSignedIn) return;` 때문에 구독만으로는 재방문·`DisableDomainReload` 경로에서 발화하지 않는다. 상세는 `2_login_deck_prune.md`.

## Follow-up

- **Play 미확인 3건**: 덱 페이지에서 숨긴 카드가 실제로 사라지는지 · `visible=0` 카드가 든 덱으로 로그인 시 장착 해제 + 디스크 반영 · 9/10 일 때 START 차단 안내. 저장 덱을 실제로 바꾸는 경로이므로 `profile.json` 백업 후 확인 권장.
- 선물(림의 선물) 풀은 `CardCategory.Subconscious` 로 뽑으므로 `visible` 을 보지 않는다. 숨김을 gift 에도 적용하려면 그 풀 필터에 조건 추가 — 이번 스코프 밖.
- `SquadPreset.cards` 에 숨김 카드가 남을 수 있다. 덱과 같은 prune 이 필요한지는 프리셋 사용 흐름을 보고 판단.
