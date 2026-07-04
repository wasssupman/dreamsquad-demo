# placement-drag-preview-polish

상태: 구현 완료 (units 0~1. 커밋 `d96cd82` 빌보드 / sway `354e418`→`aa17880`→**`4e51f1c` velocity-lean 최종**).
빌보드 각도 정합(euler 45==45). sway = **매달린 키링**(머리 위 pivot·velocity-lean, Play 검증: 700px/s→-24° tilt 육안 확인).
잔여: 사용자 취향 튜닝(swayLeanPerVel/swayHangHeight/spring·damping).

## 목표

드래그 배치 프리뷰의 시각을 배치 결과와 일치시키고(빌보드 각도 버그 수정),
키링처럼 포인터 좌우 이동에 반응해 기울었다가 감쇠하는 sway 를 더한다.

**검증 질문**: 드래그 프리뷰가 배치된 유닛과 같은 빌보드 각도로 서고,
포인터를 좌우로 움직이면 자연스럽게 기울었다가 멈추면 감쇠해 제자리로 돌아오는가?

## 연결 문서

- 배치 범위 하이라이트: `docs/spec/placement-attack-range-preview/`
- 빌보드 각도 소유권(`Billboard` / `CharacterBillboardTilt`): `docs/spec/tilted-billboard/`

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_preview_billboard_hierarchy.md` | bugfix | 프리뷰를 root(Billboard 틸트)+child(스켈레톤) 2노드로 재구성 → 배치 유닛과 각도 일치 |
| 1 | `1_pointer_sway.md` | feature | 포인터 수평 속도 → 감쇠 스프링 Z-roll, child 에 합성. 매 프레임 Update 로 settle |

의존: `0 → 1` (sway 는 unit 0 이 만든 child pivot 에 합성).

## Feature-wide 계약

- **회전 소유권**: root 에 `Billboard`(BillboardMode.Tilted, `BattleBridge.CharacterBillboardTilt`) —
  매 `LateUpdate` 로 root 의 X 틸트를 **통째로 덮어씀**(배치 유닛과 동일). 그래서 sway 는 root 를 못 얹는다.
- **계층(load-bearing)**: unit 0 이 **처음부터 최종 2노드 계층**을 만든다 —
  `root`(빈 wrapper: `Billboard` + 드래그 position/scale/SetActive/Destroy 대상) →
  `child`(SkeletonAnimation 보유, localPos/rot identity). `_session.preview` 는 **root** 참조.
  child 는 root 틸트를 상속(localPos 0)해 unit 0 시각은 "Billboard 를 스켈레톤 GO 에 붙인 것"과 동일.
  → unit 1 은 순수 가산(`child.localRotation` Z + 스프링 Update 만).
- **sway 물리(F1 — load-bearing)**: 감쇠 스프링. 각도 `θ`, 각속도 `ω`. 포인터 수평 delta 는 `ω` 에
  **impulse** 만 준다. **매 프레임 `Update()` 가 적분·감쇠**(`ω += (-k·θ - c·ω)·dt; θ += ω·dt`).
  드래그 입력(`OnDrag → UpdateDrag`)은 포인터가 **움직일 때만** 발화하므로, 스프링을 입력 콜백에서
  적분하면 멈추는 순간 각도가 얼어붙는다 → **반드시 `Update()` 소유**. `dt = Time.unscaledDeltaTime`.
- **피벗**: 유닛 원점 = 발(Billboard 주석 "피벗=발"). child local-Z roll 은 발 피벗에서 좌우 lean —
  **피벗 오프셋 없음**(위로 올리면 sway 중 발이 뜬다). 45° X-틸트 + 카메라 위에서 Z-roll 은 좌우 lean 으로 읽힘.
- **버그/기능 분리**: unit 0(버그)과 unit 1(기능)은 **별도 커밋**. unit 0 은 sway 없이 각도 정합만으로 독립 완결.
- **데이터 주도(하드코딩 금지)**: sway 파라미터(최대각 / 스프링 k / 감쇠 c / 수평 delta→impulse 스케일)는
  `DefenderDragPlacementController` 의 SerializeField.
- **뷰 전용**: 프리뷰 GameObject 만 변경. ECS/데이터/배치 로직 무변경. 배치 결과(실제 유닛)에는 sway 없음.
- **정리**: 프리뷰 파괴(CleanupSession) 시 sway 상태도 리셋(프리뷰는 매 드래그 새로 생성됨).
- **스코프**: fallback capsule 프리뷰는 대상 아님(3D 프리미티브라 각도 어색함 없음, sway 도 스킵).

## 비목표 / 후속 후보

- 배치 완료된 유닛의 상시 sway / idle 흔들림.
- 세로 / 전후 흔들림, overshoot 이상의 물리(2D 진자, 다중 관절).
- 드롭 시 bounce / 착지 반동.
- fallback capsule 프리뷰의 각도 / sway.
