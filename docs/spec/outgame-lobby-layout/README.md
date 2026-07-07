# Spec — Outgame Lobby Layout

> 상태: 완료 (2026-07-07)

## 상위 목표

OutgameScene 로비를 목업 방향으로 정리한다. `lobby_bg.png`(밤 항구/풍등 아트)를 배경으로 깔고, 중앙 세로 스택이던 로비 버튼을 **3-코너**로 재배치하며, 상단에 "꿈결특공대" 타이틀을 한글 캐주얼 폰트로 표기한다.

## 검증 질문

로그인한 플레이어가 OutgameScene에 진입했을 때, 항구 배경 위에 "꿈결특공대" 타이틀과 3-코너로 정리된 로비 버튼(우상단 개발용 / 좌하단 스쿼드·드림캐쳐 / 우하단 Play)이 보이는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_jua_korean_font.md` | Jua 한글 TMP 폰트 에셋 | 완료 |
| 1 | `1_lobby_background.md` | 로비 배경 Image | 완료 |
| 2 | `2_button_three_corner_layout.md` | 버튼 3-코너 재배치 | 완료 |
| 3 | `3_title_restyle.md` | 타이틀 "꿈결특공대" | 완료 |

## 완료 메모

- `꿈결특공대` 타이틀은 Jua TMP 폰트로 적용했다.
- 로비 배경은 `MenuCanvas` 직속 배경 레이어로 배치했다.
- 로비 버튼은 좌하단(Squad/Dreamcatcher), 우하단(Start), 우상단(dev/TestMode) 코너 배치로 정리했다.
- Squad / Dreamcatcher / Start 버튼은 캐주얼 그래픽 아이콘 버튼으로 리스킨했다.

## Feature-wide 계약

- **코드 변경 없음이 원칙**. 순수 씬 wiring + 에셋 임포트. `OutgameMenuController` 로직/필드는 건드리지 않는다.
- 현재 씬 구조 (건드리는 노드):
  - `MenuCanvas` (Canvas + CanvasScaler) — 배경 Image를 여기 첫 자식으로 추가.
  - `MenuCanvas/MenuButtons` = `OutgameMenuController.menuRoot` (로그인 게이트로 토글되는 컨테이너). 모든 로비 버튼은 이 하위에 유지한다.
  - 버튼: `StartButton`(Play) / `SquadButton` / `DreamcatcherButton` / `TestModeButton` / `DevButtons`(DevOnlyGroup: `StatRefreshButton`+`StatRefreshResult`, `ResetAccountButton`).
- **배경은 게이트 밖**(`MenuCanvas` 직속), **버튼은 게이트 안**(`MenuButtons`) 유지 → 로그인 화면에서도 배경이 보인다.
- **TestModeButton**: 위치만 우상단 dev 클러스터로 이동. `DevOnlyGroup`에는 넣지 않는다 → 비개발 빌드에서도 항상 표시.
- 3-코너 규칙:
  - 우상단(top-right 앵커): `DevButtons` + `TestModeButton`, 세로 스택.
  - 좌하단(bottom-left 앵커): `SquadButton`, `DreamcatcherButton`, 세로 스택.
  - 우하단(bottom-right 앵커): `StartButton`(Play).
- 배경 `LobbyBackground`는 화면 cover (앵커 stretch-fill, `lobby_bg.png` 16:9). 로비 오버레이 패널(SquadPanel 등)은 기존 z-order 유지.
- Unity 씬 wiring은 UnityMCP로 자동화하고 Play 검증까지 완료한 뒤 각 unit을 닫는다 (`unity-feature-wiring` 규칙).

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 신설/경로 변경이 아니라 OutgameScene UI wiring + 폰트 에셋 작업이다. `docs/reference/object-pipeline-map.md` 대상 아님.

## 후속 후보 (이번 스코프 밖)

- 목업 상단의 프로필/재화 헤더(레벨 바, 코인 카운터) UI.
- Play 버튼 대형 CTA(캐릭터 카드) 스타일.
- Jua 폰트를 프로젝트 전역 UI 한글 표준 폰트로 승격(현재는 타이틀 한정).
