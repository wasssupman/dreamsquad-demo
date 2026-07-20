# 5 — 씬 배선 + Play e2e (SquadCharacterPage 빌더)

## 목적

새 캐릭터 페이지를 실제 `SquadPanel`(OutgameScene)에 심어, 로비에서 스쿼드를 열면 실화면으로 뜨고 조작 가능하게 한다. 씬 변경을 최소화하고 되돌리기 안전하게.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs` (`Wassup.UI`) — 씬-facing 런타임 빌더. 한 GO에서 상세/헤더/브라우저 + 라이브 Spine SkeletonGraphic을 런타임 구성(검증된 프리뷰 하네스의 정식화)하고 `SquadCharacterPageController`에 주입. 레이아웃 상수 = Play 프리뷰 검증값(spine scale 2.2 / feet 0.42 / 상세 1/3 / 헤더 상단 밴드).
- 수정 `Assets/_Project/Scenes/OutgameScene.unity` — `SquadPanel/CharacterPage` GO 신설(빌더 + 에셋 5 참조: catalog/stoneCatalog/profileSO/SkeletonGraphicDefault-Straight.mat/Jua SDF). 옛 `SquadBuilderView` 컴포넌트 `enabled=false` + 옛 자식(SlotsRow/OwnedGrid/StatusText/SaveButton/PanelTitle) 비활성. CloseButton 유지. (총 65+/6- 라인, 되돌리기 = 역순.)

## 구현 노트

- **SkeletonGraphic 런타임 렌더 조건**: 머티리얼 `SkeletonGraphicDefault-Straight.mat` + 루트 Canvas `additionalShaderChannels`(TexCoord1/2·Normal·Tangent). 빌더가 Build에서 세팅.
- **컨트롤러 주입 순서**: Controller GO를 **inactive로 생성 → 필드 주입 → SetActive(true)** 로 OnEnable이 참조 준비된 상태에서 실행되게 한다(AddComponent 즉시 OnEnable 회피).
- **뷰 필드 주입**: 뷰들의 private SerializeField(spineView/cardRoot/catalog 등)는 빌더가 리플렉션으로 런타임 주입(전량 내 코드, 직렬화 불요 — 매 open 재빌드).
- 캔버스 = MenuCanvas(ScreenSpaceOverlay). 카메라 렌더에 안 잡혀 검증 스크린샷은 임시 ScreenSpaceCamera 플립(Play 전용, 저장 안 함).

## 완료 기준

- [x] 컴파일 클린. OutgameScene 저장(65+/6-, dirty 없던 클린 씬에 내 변경만).
- [x] Play e2e — 로비 스쿼드 열기 → CharacterPage 자동 빌드(자식 4), spineInit=True, 콘솔 에러 0. 실화면 클린 렌더(상세 라이브 Spine + 카드 + 헤더 편성/스톤 + 그리드 + CLOSE).
- [x] 상호작용(비파괴) — 컨트롤러 OnEntrySelected 경유 브라우즈→상세 갱신 확인('guardian'→가디언→복원). 편성 편집/저장 경로는 실 데이터라 사용자 조작으로 확인.
- [ ] 사용자 실기기/에디터 체감(로그인 후 스쿼드 열기, 출전/해제·스톤 장착·저장 지속) — 남은 확인.

> 구현 2026-07-18 · 커밋 대기. 옛 UI 비활성 보존(되돌리기 안전).
