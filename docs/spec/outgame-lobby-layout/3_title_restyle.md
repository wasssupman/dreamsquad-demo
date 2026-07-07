# 3 — 타이틀 "꿈결특공대"

## 목적

상단 Title 텍스트를 "꿈결특공대"로 교체하고, unit 0에서 만든 `Jua SDF` 한글 폰트를 적용해 말랑말랑/캐주얼한 게임 타이틀로 스타일링한다.

## 변경 대상

- OutgameScene: `MenuCanvas/Title` (TextMeshProUGUI)
- 의존: unit 0 `Jua SDF.asset` 완료 필요

## 구현

1. `MenuCanvas/Title`의 TextMeshProUGUI:
   - text = "꿈결특공대"
   - font asset = `Jua SDF`
   - 크기/자간/색상을 타이틀답게(예: 큰 폰트 사이즈, 밝은 색 + 배경 대비용 아웃라인/그림자). 항구 배경(어두운 남색) 위에서 가독성 확보 — 필요 시 흰색 계열 + 어두운 아웃라인.
2. 위치: 상단(top 앵커, 가로 중앙). 우상단 개발용 클러스터와 겹치지 않게 폭/위치 조정.
3. 배경/버튼과의 z-order: Title은 LobbyBackground보다 앞, 코너 버튼과 겹치지 않는 상단 영역.

## 완료 기준

- Play 시 상단에 "꿈결특공대"가 Jua 폰트로 tofu(□) 없이 렌더된다.
- 배경 위에서 글자가 또렷하게 읽힌다 (대비 확보).
- 우상단 개발용 버튼과 겹치지 않는다.
- `read_console` 에러 없음.
