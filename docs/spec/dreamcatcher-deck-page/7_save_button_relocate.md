# 7 — 저장 버튼을 그리드 우하단 플로팅 확정 버튼으로

## 목적

덱 스트립 오른쪽 끝에 끼어 있던 저장 버튼(168×66)이 작고 눈에 띄지 않는다. 덱빌더 관례대로 **카드 그리드 우하단에 크게 뜨는 플로팅 확정 버튼**으로 옮겨 "편집 끝 → 저장" 동선을 직관화한다. 유효성 게이트·상태 라벨 계약은 불변.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs` — 우하단 `SaveHost` RectTransform 생성 + 스트립에 주입, `saveButtonSize` 노출
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckStrip.cs` — Save 버튼을 스트립 HLG 내부 대신 주입된 host 에 빌드(호스트 채움), 라벨 확대
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherCardBrowser.cs` — 그리드 하단 패딩 확대(스크롤 시 마지막 줄이 버튼 위로 드러나게)

## 구현

- **소유 불변**: 저장 로직·유효성 게이트(`DeckRules.Validate` → interactable + SaveOn/SaveOff 색)·`SaveClicked` 이벤트는 계속 `DreamcatcherDeckStrip` 소유. 버튼의 **위치만** 페이지 빌더가 주는 host(브라우저 패널 우하단 앵커, 기본 280×96, 여백 28)로 이동. 버튼은 host 를 stretch 로 채운다 — 크기 튜닝은 페이지의 `saveButtonSize` 한 곳.
- 상태 라벨(`{n}/{size} · reason`)은 스트립에 잔류(자리 여유 증가). 스트립 쪽 Save 전용 LayoutElement 제거.
- host 는 브라우저 패널 뒤에 생성해 그리드 위에 렌더. `UiLayer.Apply` 로 레이어 정합.
- 그리드 `padding.bottom` 12→120: 플로팅 버튼이 마지막 줄을 영구 가림 방지(스크롤로 노출 가능).

## 완료 기준

- compile 클린.
- Play: 저장 버튼이 카드 그리드 우하단에 크게 표시. 유효 덱(정확히 10)일 때 초록+클릭 가능, 무효면 회색+비활성 — 기존과 동일. 클릭 시 저장 동작 불변.
- Play: 스트립에는 슬롯 10 + 상태 라벨만 남고, 그리드 끝까지 스크롤하면 마지막 줄이 버튼 위로 보인다.

2026-07-19 마무리 확정 · 커밋 41c8a6ff (compile 클린. Play 세부 확인(게이트/저장/스크롤)은 사용자 hands-on 잔여)
