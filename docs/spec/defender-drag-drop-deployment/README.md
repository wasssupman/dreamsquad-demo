# Defender Drag & Drop Deployment Spec

**작성일**: 2026-04-20  
**상위 계획**: `docs/plans/2026-04-20-defender-drag-drop-deployment.md`  
**목표**: Defender 배치를 click-to-place 에서 drag-and-drop 으로 전환하고, Drop 이후 배치 VFX/애니메이션과 유닛 고유 on-place 스킬이 끝난 뒤 일반 전투모드로 진입하도록 만든다.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Phase 0 | `0_current_flow.md` | 현재 구조와 전환 기준 고정 |
| Phase 1 | `1_validation_api.md` | 배치 검증 API 분리 |
| Phase 2 | `2_drag_session.md` | Drag source/session 추가 |
| Phase 3 | `3_hover_highlight.md` | Tile hover highlight |
| Phase 4 | `4_drag_preview.md` | Drag silhouette preview |
| Phase 5 | `5_drop_pending_deployment.md` | Drop to pending deployment |
| Phase 6 | `6_deploy_animation.md` | Deployment VFX/animation |
| Phase 7 | `7_on_place_sequence.md` | On-place skill sequence 편입 |
| Phase 8 | `8_fallback_validation.md` | Click fallback 정리 및 최종 검증 |

## 공통 원칙

- Drop 성공 즉시 tile 은 점유한다.
- 일반 전투 활성화는 배치 VFX/애니메이션과 on-place 스킬 이후에만 허용한다.
- Drag preview 는 실제 defender entity 가 아니다.
- Invalid drop 은 비용 차감, tile 점유, on-place 스킬을 발생시키지 않는다.
- 기존 click-to-place 는 D&D 안정화 전까지 fallback 으로 유지한다.
