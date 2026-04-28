# Draft UX Upgrade

상태: 완료 2026-04-28 (코드 완성, 씬 wiring + Play 검증은 Unity Editor 정상화 후)

게임 시작 시 공격 패턴 페이즈와 드래프트 페이즈를 한 흐름으로 통합한다. 공격 패턴은 상단 strip 으로 unroll/roll, 드래프트는 하단 fan 으로 Slay-the-Spire 식 카드 인터랙션. 7장 픽 → 3장 폐기로 의미 반전, 모든 트랜지션은 PrimeTween.

## 연결 문서

- 디자인 요약: `docs/plans/2026-04-27-draft-ux-upgrade-design.md`
- 의존: `docs/spec/wave-pattern/` (WavePatternGenerator), `docs/spec/defender-drag-drop-deployment/`(Placement 페이즈 진입)

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_primetween_import_and_asmdef.md` | PrimeTween 은 이미 UPM tgz 설치됨. `Wassup.Runtime.asmdef` 참조 추가 + 후속 task 가 쓸 API 확정 smoke |
| 1 | `1_remove_briefing_phase.md` | `GamePhase.Briefing` 제거, GameManager 직진. `TimelineBriefingView.cs` 는 task 3 가 추출 끝낼 때까지 보존 |
| 2 | `2_draft_session_discard_model.md` | `DraftSession` 폐기 모델 반전 + `Reset` 시그니처 변경 + BattleLogSchema 코멘트 + 기존 EditMode 테스트 갱신 + 신규 폐기 테스트 |
| 3 | `3_map_settings_panel_extract.md` | MAP SETTINGS UI 추출 + 좌상단 토글로 이관. 추출 끝나면 `TimelineBriefingView.cs` + 씬 GameObject 일괄 삭제 |
| 4 | `4_wave_pattern_strip_view.md` | 상단 strip + 좌측 토글 버튼 + unroll/roll PrimeTween + OnDwellInterrupt 이벤트 |
| 5 | `5_draft_card_fan_view.md` | 10장 fan 빌드 + 입장 staggered slide + 재배치 + toss 시퀀스. 옛 `DraftCardView.cs` 삭제 |
| 6 | `6_draft_card_input_and_discard.md` | EventSystems 인터페이스 + 클릭/스와이프 폐기 + `_discardFired` 더블 발화 가드 |
| 7 | `7_draft_view_orchestrator.md` | sub-state 머신, 2초 dwell + 입력 race, 재진입 가드, toss-complete 후 자동 confirm, PlayMode 테스트 인프라 + smoke |
| 8 | `8_handoff_summary.md` | 1차 구현 종료 인계 |
| 9 | `9_wave_strip_slam_in_redesign.md` | Slam-In 재디자인 + 카드 fan 활성 타이밍 + 드래그/클릭 더블 가드 |
| 10 | `10_handoff_summary.md` | 2차 인계: 웨이브 strip 레이아웃(상단 낙하·좌정렬·스크롤·FadeIn·오프스크린 Roll) + 카드 속도 기반 discard + LayoutRemaining 덮어쓰기 버그 수정 |

## 공통 원칙 (feature-wide 계약)

- **단일 진입점**: `DraftController.DraftStarted` 가 통합 시퀀스의 유일한 트리거. 게임 시작 / Redraft 모두 동일 시퀀스. **재진입 가드** 는 task 7 오케스트레이터가 책임.
- **페이즈 의미**: `GamePhase.Briefing` 은 enum 에서 제거된다. Briefing 의미는 Draft 의 sub-state `Unrolling → Dwelling → Rolling → Drafting → Confirming` 로 흡수.
- **폐기 모델**: 폐기 카운트 = 3 고정. 3번째 폐기 시 **마지막 toss 시퀀스 완료를 기다린 후** `DraftController.TryConfirm()` 호출. CONFIRM 버튼 없음.
- **입력 임계값** (1080 ref height 기준):
  - 위 스와이프: 누적 delta.y ≥ 120px AND drag duration ≤ 0.45s → 폐기
  - 클릭: 드래그 거리 < 30px → 폐기
  - 같은 제스처에서 OnEndDrag 폐기 후 OnPointerClick 더블 발화는 `_discardFired` 플래그로 차단
  - 임계 미만 드래그 종료: fan 정위치 복귀 (~0.25s OutBack)
- **호버 효과 없음**: 모바일 우선. `IPointerEnterHandler`/`IPointerExitHandler` 사용 금지.
- **트윈 라이브러리**: PrimeTween 만 사용. 코루틴 기반 수동 Lerp 금지. task 0 확정 API: `Tween.ScaleX`, `Tween.UIAnchoredPosition`, `Tween.LocalRotation`, `Tween.LocalPositionY`, `Tween.Alpha`, `Tween.Delay(target, dur, onComplete)`, `Tween.StopAll(target)`. 시퀀스: `Sequence.Create()` + `.Chain` / `.Group` / `.ChainCallback`. 종료 대기: `await tween` / `tween.ToYieldInstruction()`. (`Tween.UIScaleX` 는 PrimeTween 에 존재하지 않음 — `Tween.ScaleX` 사용.)
- **화면 영역 분할** (1920×1080 ref):
  - 상단 (0~140px): wave pattern strip
  - 좌측 상단 (40, -40): MAP SETTINGS 작은 토글 (개발 옵션)
  - 좌측 중앙 (40, ~540): wave pattern 토글 버튼
  - 우측 중앙: THIS ROUND SKILLS (현 비주얼 그대로 유지, 본 spec 변경 없음)
  - 하단 중앙 (0, 0~360): 카드 fan
- **신규 폴더**: 모든 새 컴포넌트는 `Assets/_Project/Scripts/UI/Draft/`. 기존 `DraftView.cs` / `DraftCardView.cs` 는 task 5/7 가 새 위치로 옮긴 뒤 옛 파일 삭제.
- **테스트 범위**: `DraftSession` 폐기 모델은 EditMode 단위 테스트. UI 시퀀스는 PlayMode smoke 1개 (DraftStarted → 3 click → DraftConfirmed). PlayMode asmdef 는 task 7 가 사전 생성.
- **task 의존 / 컴파일 안전**: task 1 은 `TimelineBriefingView.cs` 파일을 보존만, 실제 삭제는 task 3 가 추출 후 수행. task 7 은 4/5/6 의존이며 옛 DraftView 를 임시 rename 후 새 파일 작성 → 자식 wiring → 옛 파일 삭제 순.

## 비목표 (후속 후보)

- THIS ROUND SKILLS 패널 카드형 비주얼 통합
- Battle/Placement 중 공격 패턴 strip 토글 지속
- 카드 일러스트 / 프레임 아트
- Undo / 폐기 취소
- Redraft 전용 단축 연출
