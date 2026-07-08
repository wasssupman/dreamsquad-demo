# 3 · 배선/검증

## 목적

씬 참조 무결성 확인 + 에디터 Play 검증.

## 변경 대상

- (필요 시) `Assets/_Project/Scenes/OutgameScene.unity` — 컨테이너 rect 정리. 단 레이아웃은 코드 주도이므로 씬 편집 최소화.

## 구현

- 이번 세션은 Unity MCP 미연결 → 산출물은 텍스트 저작. 임포트/컴파일/Play 는 **사용자가 에디터 포커스로 트리거**.
- `DreamcatcherDeckBuilderView` 직렬화 참조(catalog/profileSO/deckContainer/ownedContainer/statusText/saveButton/font) 불변 — 씬 재배선 불필요.
- 컨테이너(`DeckRow`/`OwnedGrid`)의 기존 GridLayoutGroup 은 코드에서 제거/재구성하므로 씬 컴포넌트 잔존해도 무해.

## 완료 기준 (사용자 검증)

- [ ] 에디터 콘솔 에러 0 (임포트+컴파일).
- [ ] OutgameScene Play → 드림캐쳐 페이지 진입 → 보유 카드가 아트+효과 카드 그리드(5열), 스크롤 동작.
- [ ] 카드 탭으로 덱 추가/제거, 10/10·고유≤2 규칙, SAVE 정상.
- [ ] 이미지 배정이 순서대로 반영(추후 인스펙터 조정 가능).
