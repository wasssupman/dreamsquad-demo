# 4 — START CTA 네온 리본 배너 (unit 1 rev)

## 목적

unit 1 의 START 는 기존 버튼 rect(240×292 세로 박스)에 단순 그라디언트 라운드렉트를 채워
시안과 형태가 전혀 달랐다(리뷰 minor 4 "텅 빈 세로 슬래브"). 시안의 CTA 실루엣을 재현한다.

## 시안 실측 (근거)

- **형상**: 좌우 끝이 뾰족한 **180° 회전대칭 리본**. 왼쪽 꼭짓점이 아래(높이의 75%), 오른쪽이 위 —
  이 비대칭이 "기울어 보이는" 인상의 정체다. 상하대칭 육각형으로 만들면 시안 느낌이 사라진다.
- **림**: 본체 바깥 약 9px 떨어진 자리에 흰-시안 링(≈4px), 링 안팎으로 청색 글로우.
  본체와 링 사이 빈틈으로 배경이 비쳐 링이 떠 보인다.
- **본체**: 핑크 `rgb(252,86,176)` → 보라 `rgb(126,72,223)`, 축은 수평이 아니라 **오른쪽 아래 45°**.
- **셰브론**: 양쪽 끝 안쪽에 2개씩, 둘 다 가운데를 향한다(왼쪽 », 오른쪽 «). 흰색 알파 ≈0.38.
- **텍스트**: 흰 이탤릭 + 보라 `rgb(105,5,146)` 굵은 아웃라인.

## 변경 대상

- New: `Assets/_Project/Scripts/UI/Outgame/LobbyNeonCta.cs`
- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonChip.cs` — `Kind` enum·Cta 분기 제거(칩 전용으로 축소)
- `Assets/_Project/Scripts/UI/UiRoundedSprite.cs` — `MakeHorizontalGradient` 제거(소비처 소멸)
- `Assets/_Project/Scenes/OutgameScene.unity` — StartButton rect 500×180, 컴포넌트 교체, 라벨 확대

## 구현 노트 (되돌리면 안 되는 것)

- **CTA 는 9-slice 가 아니라 full-rect 베이크**다. 사선 모서리·대각 그라디언트·셰브론은 늘리면 깨진다.
- 본체를 `rimGap + 링/2 + glowReach` 만큼 **안쪽으로 들여서** 굽는다 → 글로우가 rect 에서 잘리지
  않고, 터치 rect ≥ 보이는 배너가 된다.
- 그라디언트 정규화는 **가로 폭 기준**. 도형 대각선으로 나누면 좌우 끝에서 t 가 0/1 에 못 가
  양 끝 채도가 죽는다(실제로 겪음).
- 라벨 아웃라인은 세 가지가 모두 있어야 보인다: ① `fontMaterial` 인스턴스(공유 머티리얼 오염 방지)
  ② **`OUTLINE_ON` 키워드 enable** — TMP 모바일 SDF 셰이더가 아웃라인을 키워드로 가르므로
  `_OutlineWidth` 만 올리면 화면에 아무 변화가 없다 ③ `UpdateMeshPadding()` — 쿼드 여백이
  아웃라인 0 기준이라 재계산 없이는 넓힌 아웃라인이 잘린다.
- 버튼 rect 확대는 안전하다. 튜토리얼은 `startButton` RectTransform 을 **런타임에 읽어** 구멍을
  뚫는다(`OutgameTutorialController` → `overlay.SetHoles`). `OutgameTutorialDimLayout` 의 좌표는
  알고리즘 설명 주석이지 하드코딩된 위치가 아니다.

## 완료 기준

- 컴파일 에러 0, Play 콘솔 에러/워닝 0.
- 시안 대비 육안 검증: 리본 실루엣·떠 있는 네온 링·대각 그라디언트·양끝 셰브론·아웃라인 텍스트.
- 칩 3종은 회귀 없음(같은 커밋에서 `Kind` 필드만 사라지고 색·형태 불변).

> 2026-07-31 완료 — Play 검증 `Assets/Screenshots/neon_lobby_final.png`, 시안 대비 크롭 비교 완료.
> 콘솔 에러/워닝 0. 남은 차이는 자체 폰트(Anton=콘덴스드)라 시안의 넓은 헤비 이탤릭보다 좁다는 점.
