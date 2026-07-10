# 5 — 우하단 각성수치 게이지 UI (숫자 + 버튼)

## 목적

전투 HUD 우하단에 각성수치를 표기하는 버튼형 UI 를 만든다. 클릭 = 손패 뷰 토글 신호(실제 플립 전환은 unit 6).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` (신규)
- 씬: BattleScene HUD 캔버스 우하단 배선 (unity-feature-wiring)

## 구현

1. **레이아웃**: 우하단 앵커. 기존 전투 HUD 요소(코스트 UI/NextWaveDock/구 SkillBar 자리 등)와 겹치지 않는 위치 — 현 우하단 점유물을 확인하고 배치(겹치면 정지하고 질문). 참고: SkillBar 가 unit 8 에서 dormant 되므로 우측 세로 영역이 비게 된다. 런타임 빌드(`DefenderSelector`/`SkillBar` 의 코드 주도 UI 관례).
2. **표시**: 현재 수치 숫자(TMP) + 게이지 필(선택: `gaugeMax` 대비 fillAmount). `DreamcatcherHandController.GaugeChanged` 구독 갱신.
3. **버튼**: 클릭 시 `event Action Toggled` 발화 — 손패 뷰(unit 6)가 구독. 자체 상태 없음(토글 상태의 주인은 unit 6).
4. **연출 최소**: 수치 변경 시 펀치 스케일 정도(선택). 획득 플로팅/가득참 강조는 후속 후보.
5. **표기**: battle-ui-korean 관례(한글 라벨 "각성"). 판내 "카드/코스트" 용어 금지(기획 문서 문체 원칙).
6. **노출 phase**: 배치 스트립과 동일 타이밍(Placement 진입~결과 전) 노출 — Placement 에서도 보이되 게이지 시작값이 자연 제약(critic Open Question 확정).

## 완료 기준

- [ ] Play 중 우하단에 게이지 표시, 적/아군 사망 시 수치 실시간 상승(상한 100).
- [ ] 클릭 시 `Toggled` 발화(unit 6 전 임시 로그로 확인).
- [ ] 기존 HUD(코스트/스코어/메뉴/NextWave)와 겹침·가림 없음 (에디터 + 세로 모바일 비율 확인).

> 확인 2026-07-10 — 커밋 e41ddf37 (사용자 Play 확인 · 겹침 없음)
