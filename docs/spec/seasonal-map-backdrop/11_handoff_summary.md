# 11. Handoff Summary — Seasonal Map Backdrop

## Commit

- `b9c12ec` docs(spec): mark seasonal-map-backdrop unit 5 complete
- `db3aa97` feat(seasonal-map-backdrop): unit 5 — forest season SOs, EdgeProp assets, SeasonRegistry, BattleScene wire
- `75e7d01` feat(seasonal-map-backdrop): unit 4 — 백드롭 + EdgeProp 2종 이미지 생성
- `84a6103` feat(seasonal-map-backdrop): unit 3 — BattleBridge season integration
- `729f1e9` feat(seasonal-map-backdrop): unit 2 — BackdropMounter + AnchorTable + Backdrop_Unlit shader + 테스트
- `49b209c` feat(seasonal-map-backdrop): unit 1 — SeasonData/BackdropData/Registry/Runtime 데이터 모델
- `4883741` feat(seasonal-map-backdrop): unit 7~9 Lava/Lunar/Cosmic + Skybox/Panoramic 전환

## Implemented

- 시즌 SO 1개에 `MapThemeData + SeasonBackdropData` 묶음. `SeasonRegistry.allSeasons` 4종 (Forest/Lava/Lunar/Cosmic), `defaultSeason = Forest`.
- `BackdropMounter.Mount / Unmount` 단일 게이트웨이. Skybox/Panoramic 머티리얼을 `RenderSettings.skybox` 에 주입, 카메라 `clearFlags = Skybox` 토글. Unmount 시 이전 skybox/clearFlags/backgroundColor 원복.
- `BattleBridge` 가 유일 호출자. `Awake` 에서 `SeasonRuntime.Bind(seasonRegistry)`, `BuildMapForBattle` 의 모든 mapTheme read 가 `SeasonRuntime.Active.mapTheme` 으로 통일. 라이프사이클 hook (Teardown/CleanupDraftMap/StopBattle/OnDestroy/Mount 직전) 에서 Unmount 호출.
- backdrop 텍스처 사양: 4096×2048 equirectangular PNG, 좌우 seam 일치, sRGB, mipmap on.
- Forest 8 EdgeProp + Lava/Lunar/Cosmic 6 EdgeProp 매핑 (시즌간 generic 6종 공유, forest-specific 2종은 Forest 만).
- EdgeProp 격리 계약: PropData `placementWeight = 0` + `billboardMode = None`, Mount 직후 PropBillboard disable 이중 안전망.
- `SeasonBackdropData` 에 `skyboxExposure (0~8)` / `skyboxRotationDegrees (0~360)` 추가, Inspector Live Preview Editor 추가.
- BattleScene 카메라 pitch 52 / FOV 42 / padding 1.12 로 Skybox framing 맞춤 튜닝.
- ECS 컴포넌트/시스템 신규 0개. 맥락 경계 영향 없음.

## Key Files

- `Assets/_Project/Scripts/Presentation/Backdrop/BackdropMounter.cs`
- `Assets/_Project/Scripts/Presentation/Backdrop/BackdropAnchorTable.cs`
- `Assets/_Project/Scripts/Data/Season/SeasonBackdropData.cs`
- `Assets/_Project/Scripts/Data/Season/SeasonData.cs`
- `Assets/_Project/Scripts/Data/Season/SeasonRegistry.cs`
- `Assets/_Project/Scripts/Runtime/SeasonRuntime.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (season hook)
- `Assets/_Project/Editor/SeasonBackdropDataEditor.cs`
- `Assets/_Project/Data/Season/SeasonRegistry.asset`, `season_S{1..4}_*.asset`, `backdrop_S{1..4}_*.asset`
- `Assets/_Project/Art/Season/{forest,lava,lunar,cosmic}/backdrop_*.png`

## Verified

- EditMode: `BackdropAnchorTableTests` (12 anchor × 2 조합) 통과. (unit 2 시점 검증, 이후 코드 변경 없음.)
- 컴파일 clean (unit 5 시점 + commit 4883741 후 사용자 verbal 확인).
- Forest Play 시각: 사용자 verbal 확인 — 사방 숲 skybox + 보드 둘레 EdgeProp + 콘솔 에러 0 + Play 종료 시 skybox 복원.
- 4시즌 swap (defaultSeason 교체 → Play) 동작: 사용자 verbal 확인. `defaultSeason` 최종값 = `season_S1_forest`.
- **미완료**: 시각 검증 스크린샷 4장 (`Assets/Screenshots/seasonal_backdrop_*_verify_2026-05-22.png`) 캡처 — 후속에서 보강.

## Notes

- `BackdropMounter` 의 Skybox state (이전 skybox/clearFlags/backgroundColor) 는 static 으로 유지된다. 동시에 두 BattleScene 이 활성화될 일은 없다고 가정. 그래도 재진입 시 항상 Unmount → Mount 순서가 강제되도록 BattleBridge 가 `Mount 직전에도 Unmount` 한 번 호출.
- `Backdrop_Unlit.shader` 는 Skybox 전환 후 사용처가 없지만 자산은 보존. 후속에 부분 quad overlay 등으로 활용 가능.
- 4 시즌 모두 `forest.asset` 을 mapTheme 으로 공유한다. 시즌별 차별화된 타일/장애물은 별도 spec.
- `SeasonRegistry.defaultSeason` 은 토너먼트 server hook 이전까지 Inspector 수동 교체로 시즌을 바꾼다. 코드에서 swap 하지 말 것.
- backdrop PNG 는 4096×2048 equirectangular 가 강제 사양. seam 어긋나면 Play 카메라 회전 시 솔기 보임.

## Follow-up

종료된 follow-up 항목은 `docs/spec/README.md` 의 Follow-up Backlog 으로 이관한다. 본 spec 에서 남긴 항목:

- 시즌별 차별화된 MapThemeData (Lava/Lunar/Cosmic 전용 타일/장애물) — 별도 spec.
- 시각 검증 스크린샷 4장 캡처 + 시즌별 tint/exposure 미세 튜닝 라운드.
- 백드롭 미세 시차 (camera 미세 이동에 skybox `_Rotation` 살짝 변화).
- Backdrop ↔ MapTheme 라이팅/포그 매칭 룩 패스.
- 토너먼트 메타 hook: 서버 응답 → `SeasonRuntime` 의 active swap API.
- 시즌 활성 시 매치 시작 UI 에 시즌 배지 노출.
