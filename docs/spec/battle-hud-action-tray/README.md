# Battle HUD Action Tray — 비용 가독성·통합 하단 트레이 Spec

**작성일**: 2026-07-11
**상태**: 설계 완료, 선행 spec 대기
**선행 조건**: `docs/spec/mobile-ui-safe-area/` 완료
**검증 질문**: 플레이어가 1초 안에 현재 구매 가능한 유닛을 식별하고, Placement/Battle/드림캐쳐 전환 중에도 전장 가림과 실패 드래그가 줄어드는가?

## 목표

`battle-hud-layout`이 확정한 bottom-center 축과 제자리 플립은 유지한다. 현재의 부유 코스트 배지 + 투명 슬롯 나열을 하나의 시각적 Action Tray로 통합하고, 슬롯에 비용·역할·구매 가능 상태를 추가한다. Battle에서는 트레이와 코스트 레일이 함께 축소되어 클러스터가 벌어지지 않게 한다.

## 연결 문서

- 선행 완료: `docs/spec/battle-hud-layout/` (`9e6895fd`)
- 디자인 근거/시안: `docs/plans/2026-07-11-battle-hud-layout-review-proposal.md`
- 모바일 기반: `docs/spec/mobile-ui-safe-area/`

## 작업 단위

| 순서 | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_tray_config_and_shell.md` | 데이터·외곽 셸 | 공유 치수/색/역할 표기와 슬롯 배킹 도입 |
| 1 | `1_slot_cost_affordance.md` | 슬롯 정보 | 비용·역할·이름·구매 가능 상태를 즉시 표시 |
| 2 | `2_energy_rail_phase_density.md` | 경제 클러스터 | 코스트를 부유 배지에서 트레이 결합 레일로 축소 |
| 3 | `3_hand_flip_visual_parity.md` | 손패 전환 | 유닛 트레이와 드림캐쳐 핸드의 외곽 문법 통일 |
| 4 | `4_reject_reason_feedback.md` | 실패 피드백 | 비용 부족 드래그 차단 + 배치 거부 원인 구분 |
| 5 | `5_play_qa_and_metrics.md` | 검증 | 상태/비율/비용 경계/긴 이름/실기 회귀 게이트 |

## 공통 계약

- bottom-center 축과 스트립↔핸드 제자리 플립을 유지한다. 좌우 순간이동이나 phase별 anchor 변경은 금지한다.
- Action Tray 외곽 폭은 980 기준, Placement 약 980×136, Battle 약 980×104에서 시각 검증 후 확정한다.
- 코스트 레일은 트레이 상단에 겹쳐 붙으며 phase 높이를 함께 추종한다. 별도 부유 배지로 되돌리지 않는다.
- 비용·role은 기존 `DefenderUnitData.cost`/`role`을 읽는다. UI 전용 중복 데이터를 유닛 asset에 추가하지 않는다.
- affordability 표시는 `CostRuntime` read-only 구독/조회이며 배치 비용 차감 권한은 기존 `BattleBridge` 경로에 남긴다.
- 비용 부족은 색만으로 알리지 않는다. dim + 비용 강조 + glyph/텍스트 피드백을 함께 쓴다.
- `CostDisplay`의 `_phaseVisible && !_suppressed` 상태 소유권과 핸드 open/close 신호 계약을 유지한다.
- 트레이 수치는 `BattleHudTrayConfig` ScriptableObject에서 조정하고, 컴포넌트별 중복 매직 넘버를 만들지 않는다.
- 신규 Manager/인터페이스/외부 tween 라이브러리를 만들지 않는다.

## 파이프라인 커버리지

N/A — Defender/적/투사체 생성→렌더 경로 변경 없음. 기존 MonoBehaviour UI 표현과 드래그 진입 피드백만 변경한다.

## 비목표 / 후속 후보

- 첫 세션 3~4슬롯 게이팅/점진 해금은 현재 7픽 검증 조건을 바꾸므로 제외한다.
- 남은 허용 유출/패배 임계치 HUD는 별도 `battle-survival-hud` 후보로 둔다.
- idle 60px 자동 축소와 좌 3/우 4 분할 그립안은 1차 고정 트레이 데이터 확인 뒤 A/B 후보로 남긴다.
- 각성 게이지의 위치 재설계, 신규 역할 아이콘 원화, SFX/햅틱은 본 spec 필수 범위가 아니다.
