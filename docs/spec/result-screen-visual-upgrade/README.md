# result-screen-visual-upgrade

상태: 완료 2026-07-08

## 목표

인게임 종료 후 뜨는 결과 팝업(`ResultScreen`)의 리더보드를 기본 UI → 게임 무드(홀로그램/골드)에 맞는 비주얼로 업그레이드한다. 동시에 RESTART 버튼이 랭킹 화면 중앙에 겹쳐 뜨는 레이아웃 결함을 제거한다.

검증 질문: **"결과 팝업이 인게임 HUD(`ScoreHudView`)와 같은 골드/네이비 홀로그램 언어로 보이고, TOP3 배지·본인 강조가 한눈에 읽히며, RESTART 버튼이 리스트를 가리지 않고 하단에 고정되는가?"**

## 디자인 결정 (사용자 확정 2026-07-08)

- **레이아웃**: 행별 라운드 플레이트 + 순위 배지. TOP3 = 금/은/동 배지, 본인 행 = 골드 글로우 강조, 미배정 슬롯 = 회색 `WAITING...`. RESTART = 하단 고정 바.
- **배경**: ~~시즌 배경 아트~~ → **기존대로 dim 오버레이(`UiOverlay.Dim`) 팝업**. (2026-07-08 인게임 확인 후 사용자 번복: 시즌 아트를 풀스크린으로 깔면 배틀 보드까지 덮어 화면 전체가 아트로 보임 → "개판". 시즌 배경 접근 폐기, 새 아트/스프라이트 배선 0.) 패널만 절차적 네이비 프레임 + 골드 테두리로 리스킨.
- 이 환경엔 이미지 생성 도구가 없음 → 새 아트 0. 절차적 스프라이트(`UiRoundedSprite`)로만 구성.

## feature-wide 계약

- **팔레트는 `ScoreHudView`와 통일**: 골드 `(1, 0.78, 0.28)`, 네이비 플레이트 `(0.04, 0.055, 0.08)`, 골드 테두리 `(1, 0.78, 0.28, 0.95)`. 색은 `ResultScreen`의 `[SerializeField]` 로 노출하되 이 값이 기본값.
- **절차적 스프라이트 단일 소스**: 라운드렉트/원 배지 스프라이트는 `Wassup.UI.UiRoundedSprite` 공용 헬퍼로 생성. `ScoreHudView`의 사설 `MakeRoundedRectSprite` 도 이 헬퍼로 이관(동작 동일).
- **레이아웃은 앵커 기반 3영역**: 헤더(top) / 리스트(stretch, 중앙) / 푸터(bottom, RESTART 고정). 단일 `VerticalLayoutGroup` 에 버튼을 태워 떠다니게 하던 구조를 폐기 — 이것이 겹침 결함의 원인.
- **행 데이터는 순수 함수로 산출**: `BuildRows(entries, maxEntryCount, ownUserId)` 가 표시 행 모델(rank/name/score/isPlayer/isWaiting)을 만든다. 봇 폴백·실서버 랭킹 둘 다 이 함수를 거친다. rank 는 score 내림차순 위치(서버 rank>0 이면 우선) — tournament-play-report 계약 유지.
- **UI 영문 고정**: TMP 폰트에 한글 글리프 없음(login-gate 계약). `WAITING...`/`RESTART`/`VICTORY`/`DEFEAT` 영문.
- **팔레트/dim 은 코드 상수**: goldColor/navyFill/defeatColor 는 `private static readonly`(시각 상수, 튜닝 노브 아님), backdrop = `UiOverlay.Dim`. `ResultScreen` 에 직렬화 필드 0 → BattleScene diff 0(HEAD clean 유지).
- **tournament-play-report 배선 불변**: `ShowVictory/ShowDefeat/UpdateLeaderboard/RestartRequested` 시그니처·호출부(BattleBridge) 유지. 본 spec 은 `ResultScreen` 내부 렌더링만 교체.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | refactor | `0_ui_rounded_sprite.md` | 절차적 라운드렉트/원 스프라이트 공용 헬퍼 추출 + ScoreHudView 이관 |
| 1 | UI | `1_panel_frame_and_layout.md` | 패널 셸(dim + 네이비 프레임 + 골드 탭 헤더) + 3영역 앵커 레이아웃(RESTART 하단 고정) |
| 2 | UI | `2_leaderboard_rows.md` | 행별 플레이트 + 순위 배지 렌더 + 순수 `BuildRows` + EditMode 테스트 |
| 3 | verify | `3_scene_wire_and_verify.md` | Play 검증(승/패·게스트·실랭킹·겹침 해소). ~~시즌 배경 와이어링~~ 폐기 |
| 4 | handoff | `4_handoff_summary.md` | 인계 요약 |

## 파이프라인 커버리지

N/A — 본 spec 은 UI 오버레이(`ResultScreen`) 내부 렌더링 변경으로, 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 생성→렌더 파이프라인을 건드리지 않는다. `docs/reference/object-pipeline-map.md` 대상 아키타입 없음.

## 비목표 / 후속 후보

- 결과 화면 등장 애니메이션(패널 슬라이드/카운트업/배지 팝) — 정적 레이아웃 확정 후 별도.
- 리스트 10행 초과 시 ScrollRect — 현재 maxEntryCount=10 고정이라 영역에 맞춤. 초과 서버 정책 생기면 도입.
- 한글 폰트 fallback (login-gate 후속 백로그와 공통).
- 시즌별 배경 자동 스왑 (seasonal-map-backdrop 후속 "토너먼트 메타 hook" 과 공통).
