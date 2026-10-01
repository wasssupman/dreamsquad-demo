# 1 — 씬 컴포넌트 · SO · 텍스처 · 디펜더 SO 정리

## 목적

패럴랙스를 참조하던 직렬화 에셋을 지워 **missing script / missing reference 가 0** 이 되게 하고, 로비가 패럴랙스 없이 정상 렌더됨을 Play 로 확인한다.

## 변경 대상

1. `Assets/_Project/Scenes/OutgameScene.unity`
   - GameObject `&1248283347` 의 `m_Component` 목록에서 `LobbyBackgroundParallax` 컴포넌트 항목 제거
   - 그 MonoBehaviour 블록(`m_Script: {guid: 984d579e45c4f4490bb5cb5be4b8ba01}`, `dissolve`·`underImage`·`depthMap`·`settings`·`ambient*`·`pointerGain`) 제거
2. 삭제: `Assets/_Project/Data/DepthParallaxSettings.asset` · `LobbyParallaxSettings.asset` (+`.meta`)
3. 삭제: `Assets/_Project/Art/Depth/` 폴더(`lobby_bg_depth.png` · `lobby_bg_neon_depth.png`)
4. 삭제: `Assets/_Project/Sprites/Cutscene/{Archer,Cannon,FireCaster,Guardian,Healer,Ranger,Sniper}/Depth/` 7개 폴더(각 png 1장)
5. 수정: `Assets/_Project/Data/Defenders/Defender_*.asset` 30개 — `deployCutsceneDepth:` 블록(빈 `[]` 또는 텍스처 참조 1~2줄)과 `deployCutsceneTiltGain: 1` 줄 제거

## 구현

- **씬 편집 경로 판정이 먼저다**: 에디터에 `OutgameScene` 이 열려 있으면 YAML 을 밖에서 고치지 않는다(Reload 모달이 MCP 를 멈춘다). 열려 있으면 MCP 로 컴포넌트 제거 → `SaveAssetIfDirty`/씬 저장. 안 열려 있으면 YAML 직접 편집. 손대기 전부터 씬이 dirty 였으면 `lessons/02` 의 delta 격리 절차.
- 단위 0 이 먼저 커밋돼 있으면 컴포넌트는 이미 missing script 상태다 — 어느 경로든 블록만 지우면 된다.
- `underImage`(fileID `1415990802`)는 런타임에 모듈 머티리얼을 붙이던 뒤 레이어다. 컴포넌트가 사라지면 기본 UI 머티리얼로 그려진다 — 디졸브가 앞/뒤 두 장을 블렌드하는 구조는 그대로라 낮/밤 전환은 유지된다.
- 디펜더 SO 27개(필드가 직렬화된 파일)는 스크립트로 일괄 정리(두 키의 줄과 `deployCutsceneDepth` 하위 `- {fileID…}` 줄). 정리 후 `Wassup.Tests.EditMode.Assets` 가 SO 파싱을 검증한다.
- 텍스처 삭제 전 참조 재확인: 조사 시점 `lobby_bg_depth.png` 참조 0 · 나머지 전부 이 단위에서 지우는 컴포넌트/필드만 참조.

## 완료 기준

- [x] `OutgameScene` 열었을 때 콘솔 missing script 0 · `[LobbyBackgroundParallax]`/`Wassup/UI/DepthParallax` 경고 0
- [x] Play(에디터): 로비 배경이 그려지고 낮/밤 디졸브 전환이 동작, 키링 스와이프 중 배경이 **움직이지 않음**(의도)
- [x] `Wassup.Tests.EditMode.Assets` 초록(디펜더 SO 27개 파싱·스냅샷 무변 — 스냅샷은 카드/바인딩이라 영향 없어야 함)
- [x] `rg -l '4c4630c748933964586217b4e6c19a57|f8ada7d6f9d9840fbabbe2ba2a7295d8|8f24adcbcc77c4a7e99643f7367c2627' Assets` 0건(지운 텍스처·SO 의 GUID)
- [x] 커밋(경로 지정): 씬 · SO 2 · `Art/Depth/` · `Sprites/Cutscene/*/Depth/` · 디펜더 SO 27

확인 2026-10-01 · 커밋 `362eafbc3`. 사용자 에디터 Play 육안 통과(로비 배경 · 낮/밤 디졸브 · 스와이프 중 배경 정지). 배치 EditMode 2650 실패 집합이 단위 0 과 동일(새 빨강 0) · 지운 에셋 GUID 참조 0 · 씬 dangling fileID 0.
