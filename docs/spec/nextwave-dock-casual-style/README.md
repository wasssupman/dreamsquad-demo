# nextwave-dock-casual-style

> 상태: **완료 2026-07-18** — 구현 `27224339`, 사용자 마감 확인 완료.
> 인계: `3_handoff_summary.md`

## 목표

Battle 좌하단의 `NextWaveDock`을 각성 버스트 버튼과 같은 캐주얼 액션 게임 문법으로
개선한다. **상단 남은 시간 / 하단 다음 웨이브 버튼 구조, 데이터, 클릭 기능, 페이즈 노출은
변경하지 않고 스타일과 반응만 변경한다.**

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_style_contract.md` | 정보 위계와 무회귀 계약 |
| 1 | `1_assets_and_motion.md` | 심볼 없는 젤리 프레임·버튼 에셋과 눌림 반응 구현 |
| 2 | `2_visual_verification.md` | Battle Play 캡처, 경고/disabled/클릭 검증 |
| 3 | `3_handoff_summary.md` | 최종 구현·검증·비목표 인계 |

## Feature-wide 계약

- 정보 구조는 **남은 시간(상단) / 다음 웨이브(하단)** 2단을 유지한다.
- `bridge.TimerRemaining`, `NextWaveAvailable`, `NextWaveHasNext`, `NextWaveNumber`,
  `ForceNextWave()` 경로는 변경하지 않는다.
- 좌하단 `SafeAreaRoot`, Battle 전용 노출, 250×150 기준 면적을 유지한다.
- 두꺼운 네이비 실루엣, 보라 젤리 외곽, 청록/노랑 포인트를 사용한다.
- 에셋에는 텍스트·숫자·아이콘을 굽지 않는다. 꿈·별·밤·달·깃털·마법 문법을 사용하지 않는다.
- 타이머 경고색, 초 변경 punch, 버튼 interactable/disabled 의미를 유지한다.
- 순수 UGUI 프레젠테이션 변경이며 ECS/웨이브 로직은 비목표다.

## 기존 스펙 관계

`battle-hud-score-timer-menu/1_timer_nextwave_dock.md`의 기능 계약과
`awakening-hud-resource-button/2_action_corner_layout.md`의 좌하단 위치 계약을 유지하며,
시각 스타일만 본 스펙이 후속 대체한다.
