# 4 — 런타임 빌더 + 배선 + Play e2e (DreamcatcherDeckPage)

## 목적

새 페이지를 실제 `DreamcatcherPanel`에 심어 로비에서 열면 실화면으로 뜨고 조작 가능하게. 씬 변경 최소·되돌리기 안전.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs` (`Wassup.UI`) — 씬-facing 런타임 빌더.
- 수정 `Assets/_Project/Scenes/OutgameScene.unity` — `DreamcatcherPanel/DreamPage` GO(빌더 + 참조 3: catalog/profileSO/Jua SDF). 옛 `DreamcatcherDeckBuilderView` enabled=false + 옛 자식(PanelTitle/DeckRow/OwnedGrid/StatusText/SaveButton) 비활성. CloseButton 유지. (61+/6-.)

## 구현 노트

- 빌더가 상세(art Image + cardRoot)/덱스트립/브라우저/컨트롤러 런타임 구성 후 리플렉션 주입. **SkeletonGraphic 없음 → 머티리얼/Canvas 채널 불요**(스쿼드 대비 단순).
- 컨트롤러 GO inactive 생성→주입→활성(OnEnable 준비완료 후). 캔버스 = MenuCanvas(ScreenSpaceOverlay) — 검증 스크린샷은 임시 ScreenSpaceCamera 플립.

## 완료 기준

- [x] 컴파일 클린. OutgameScene 저장(clean 씬에 내 변경만).
- [x] Play e2e: 로비 드림캐쳐 열기 → DreamPage 자동 빌드(자식 4), 콘솔 에러 0, 실화면 렌더(타로 art 상세 + 덱 스트립 + 카드 그리드 + 카운트). 브라우즈→상세 비파괴 확인.
- [ ] 사용자 hands-on(로그인→드림캐쳐에서 추가/제거·저장 지속, 무효 덱 방지) — 잔여.

> 구현 2026-07-18 · 커밋 `30d882cf`. 옛 뷰 비파괴 보존(되돌리기 역순).
