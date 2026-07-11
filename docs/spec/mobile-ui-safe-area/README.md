# Mobile UI Safe Area — Height 기준 스케일·안전영역 통일 Spec

**작성일**: 2026-07-11
**상태**: 설계 완료, 구현 승인 대기
**검증 질문**: 16:9의 현재 구도를 보존하면서 19.5:9·20:9 Android에서 UI가 확대·클립되지 않고, 모든 핵심 조작부가 cutout/gesture safe area 안에 남는가?

## 목표

현재 씬 authored Canvas와 런타임 생성 Canvas가 서로 다른 `CanvasScaler` 설정을 쓰는 문제를 먼저 해소한다. 공통 Height 기준 스케일과 `FullBleedRoot`/`SafeAreaRoot` 계약을 만든 뒤 Battle·Outgame UI를 전부 이관한다. 이 spec 완료가 `battle-hud-action-tray`의 선행 조건이다.

## 연결 문서

- 제안 근거: `docs/plans/2026-07-11-battle-hud-layout-review-proposal.md`
- 선행 변경: `GameManager.Awake`의 세로 1080 기준 해상도 정규화
- 후속: `docs/spec/battle-hud-action-tray/`

## 작업 단위

| 순서 | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_safe_area_math.md` | 순수 좌표 계약 | safe rect를 Canvas anchor로 바꾸는 계산과 회귀 테스트 |
| 1 | `1_canvas_setup_runtime.md` | 공통 런타임 기반 | CanvasScaler 정규화 + full-bleed/safe root 생성 |
| 2 | `2_battle_ui_migration.md` | Battle 이관 | 런타임 Canvas 12종과 BattleScene authored Canvas 정합 |
| 3 | `3_outgame_ui_migration.md` | Outgame 이관 | 로비·스쿼드 준비 화면의 스케일/safe root 정합 |
| 4 | `4_aspect_safearea_qa.md` | 검증 | 16:9~20:9 + Android cutout/gesture QA와 회귀 테스트 |

## 공통 계약

- 모든 ScreenSpace UI는 `ScaleWithScreenSize`, reference `1920×1080`, `matchWidthOrHeight=1`을 사용한다.
- Canvas 자체는 물리 화면 전체를 유지한다. 전면 scrim/배경/화면 cover는 `FullBleedRoot`, 조작부·텍스트·HUD는 `SafeAreaRoot` 아래에 둔다.
- `SafeAreaRoot`는 `Screen.safeArea`를 정규화한 anchor만 적용하며 별도 scale이나 pixel offset을 중복 적용하지 않는다.
- 해상도·orientation·safe rect가 실제로 바뀔 때만 재계산한다. 매 프레임 할당/계층 검색은 금지한다.
- 이미 존재하는 CanvasScaler도 공통 설정으로 교정한다. `GetComponent()==null`인 경우만 설정하는 현재 패턴을 남기지 않는다.
- centered HUD는 safe rect 중심 기준, edge HUD는 해당 safe edge 기준으로 앵커링한다.
- 16:9에서는 픽셀 구도 회귀가 없어야 한다. wide aspect는 세로 크기를 유지하고 추가 가로 영역만 사용한다.
- 신규 싱글톤/Manager/외부 UI 라이브러리를 만들지 않는다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 생성→렌더 경로 변경 없음. ScreenSpace UI의 Canvas/RectTransform 계층만 변경한다.

## 비목표 / 후속 후보

- 하단 트레이의 아트·비용 정보·드래그 피드백은 `battle-hud-action-tray` 범위다.
- portrait orientation 지원은 하지 않는다. 타겟은 landscape Android다.
- 전장 카메라 framing과 `GameManager` 해상도 정책은 변경하지 않는다.
- safe area를 빌드에서 강제 시뮬레이션하는 개발자 메뉴는 만들지 않는다. 순수 계산 테스트 + 실기 QA로 검증한다.
