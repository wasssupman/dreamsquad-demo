# 1 — 로비 배경 Image

## 목적

`lobby_bg.png`(밤 항구/풍등 아트, 16:9)를 OutgameScene 로비 배경으로 깐다. 로그인 화면에서도 배경이 보이도록 로그인 게이트(`menuRoot`) 밖에 둔다.

## 변경 대상

- `Assets/_Project/Art/lobby_bg.png` (기존, import 세팅 확인)
- OutgameScene: `MenuCanvas` 하위에 `LobbyBackground` GameObject 신규

## 구현

1. `lobby_bg.png` 임포트 세팅 확인/조정: Texture Type = Sprite (2D and UI). UI Image에 물릴 것이므로 Sprite 모드 필요.
2. `MenuCanvas` 아래 `LobbyBackground` (Image) 생성 후 **첫 번째 형제(sibling index 0)** 로 이동 → 다른 모든 UI 뒤에 렌더.
   - `MenuButtons`(menuRoot)와 형제 레벨. 게이트 밖이라 로그인 여부와 무관하게 항상 표시.
3. RectTransform: 앵커 stretch-fill (min 0,0 / max 1,1 / offset 0) → 캔버스 전체를 덮는다.
4. Image: sprite = lobby_bg, `Preserve Aspect` **끔**(cover 목적, stretch-fill로 화면을 꽉 채움). 색 tint white/불투명.
5. SquadPanel/DreamcatcherPanel/TestModePanel/LoginPanel 등 오버레이는 sibling index가 LobbyBackground보다 뒤라 그대로 위에 그려지는지 확인 (기존 순서 유지).

## 완료 기준

- OutgameScene Play 시 로비 배경으로 항구 아트가 화면을 덮는다.
- 로그아웃(LoginPanel) 상태에서도 배경이 보인다.
- 버튼/타이틀/오버레이 패널이 배경보다 앞에 정상 렌더된다 (배경에 가려지지 않음).
- `read_console` 에러 없음.
