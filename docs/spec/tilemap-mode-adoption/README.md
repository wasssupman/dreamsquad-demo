# Tilemap Mode Adoption Spec

**작성일**: 2026-06-14
**상태**: 구현·검증 완료 — unit 0~2 (2026-06-14). 0: `016a29a` · 1: `4a3fc91` · 2: `3531660`. dirty 실험물 되돌림 후 `_TilemapBoard`+BattleBridge 필드 영속 저장 — **인스펙터 `boardViewMode` 1값으로 Legacy3D/Rect/Iso 토글**, iso 깔끔히 렌더(모드별 스케일/틸트 + bounds 카메라 + solid 배경). 기본=Legacy3D(비파괴). **남은 것: ① iso/rect 기본 채택 여부 product 결정 ② 타일 아트/2D 배경(후속 theming).** handoff: `3_handoff_summary.md`.
**선행 spec**: `docs/spec/tilemap-view-backend/` (프레임워크·정렬·결정론 완료. 영속 씬 저장 미완 → 본 spec 이 흡수)

## 목표

`tilemap-view-backend` 가 검증한 Tilemap 뷰(특히 Isometric)를 **실제로 봐줄 만한 수준**으로 끌어올려 게임에서 채택 가능하게 한다. 실험으로 드러난 "개판"의 원인(3D 전용 씬 환경 충돌 + 3D 기준 유닛 스케일/틸트 + rect 가정 카메라)을 제거한다. 시뮬레이션·결정론·Legacy3D 동작은 변경하지 않는다.

## 검증 질문

1. **시각 채택성**: TilemapIso/Rect Play 에서 보드가 단독으로 깔끔히 보이는가(하늘/3D 배경 충돌 없음), 유닛이 셀 대비 적정 크기인가?
2. **모드 격리**: Tilemap 모드 환경 게이팅이 Legacy3D 진입 시 완전히 원복되는가?
3. **결정론·회귀 유지**: 본 spec 적용 후에도 3모드 sim 동일(byte-identical) + Legacy3D 시각/동작 무변경인가?

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Unit scale | `0_mode_scale_and_tilt.md` | `CharacterVisualScale` const 제거 → 모드별 값(SO/SerializeField). billboard tilt mode-aware(Tilemap=0). |
| 1 | Environment | `1_env_gating_and_camera.md` | Tilemap 모드: 카메라 clearFlags=Solid + Legacy 환경(skybox/3D board/backdrop) 게이팅, Legacy3D 원복. iso 카메라 보드 bounds 기반 프레이밍. |
| 2 | Persist | `2_scene_persist_and_verify.md` | dirty `BattleScene.unity` 정리 **선행** 후 `_TilemapBoard` + `BattleBridge` 필드/프리셋 영속 저장 + 실 Play 검증. |
| 3 | Handoff | `3_handoff_summary.md` | 종료 요약. |

의존 순서: `0 → 1 → 2`.

## Feature-wide 계약

- **결정 (승인됨)**: ① 유닛 스케일 = 모드별 값(const 제거). ② Tilemap 환경 = 최소(Legacy 환경 비활성 + solid 배경; 전용 2D 배경은 후속).
- **시뮬레이션 불변**: `tilemap-view-backend` 계약 계승 — `Battle/**`/`GeneratedMap`/`FlowField`/`GridMath`/생성기 무변경. 결정론 유지.
- **Legacy3D 무변경**: 모든 단계의 회귀 기준. 모드별 값/환경 게이팅은 Tilemap 모드에서만, Legacy3D 진입 시 원복.
- **하드코딩 금지**: 모드별 스케일·카메라·게이팅 대상은 SO/SerializeField.
- **선행 차단**: unit 2(영속 저장)는 dirty `BattleScene.unity`(무관 827줄) 정리 전 진입 금지. 정리 주체는 사용자.

## 후속 후보 (본 spec 범위 밖)

- iso/rect 전용 **타일 아트** (RuleTile/시즌 스프라이트) — theming spec.
- Tilemap 모드 **전용 2D 배경 연출** — 최소 게이팅 이후.
- 해저드/장애물 비주얼 Tilemap 정렬 (tilemap-view-backend 후속에서 이관).
- Tilemap 모드 Mono 헬스바 오버레이.
