# 5 · handoff summary (dreamcatcher-orb-dock)

세션 인계 지도. 최신 계약은 README/번호 문서 우선.

## Commit (main)

- `676e7f8f` unit 0 완료 스탬프 / `1f97f564`+`df2df7ef` unit 0 구현·meta (Verlet 물리 코어)
- `8f70eaa6` unit 1 항아리 독 뷰 / `ad61b06f` critic 반영 / `22793f8b` 스탬프
- `1d7b199b` unit 2a 게이지 구동 피규어 물리 더미
- `836aef63` unit 4 오버플로우 경고 + 절차적 피규어 최종 확정

## Implemented (검증 완료)

- **unit 0** — `JarFigurePhysics`(Verlet+위치제약 순수 코어). EditMode 6개 + 오프스크린. **완료**
- **unit 1** — 트레이 우측 항아리 독(`AwakeningGaugeView` in-place 재작성). 큰 숫자·데이터 파생
  코스트 틱·3단계 ready 림·발견성 라벨. `BindTray`(HandView.Start). critic clean. **완료**
- **unit 2a** — `JarFigurePile`: 게이지 비례 절차적 피규어를 물리로 쌓음(상한 20, 고정 스텝).
  오프스크린 25/60/100 검증. **완료. 절차적이 최종**(2b Spine 은 24px 에서 미식별 → 보류).
- **unit 4 오버플로우** — 컨트롤러 `AwakeningOverflowed` + 뷰 골드 림 플래시. compile 그린. **완료**

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePhysics.cs` (순수 코어, EditMode 테스트 있음)
- `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePile.cs` (피규어 풀/물리 뷰)
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` (항아리 독 — 센터피스)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` (Config getter,
  AwakeningOverflowed)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (Start→BindTray; close 는 미구현)

## Verified

- compile 그린(전 커밋). EditMode 1269/0-fail(내 6개 포함, 회귀 없음).
- 오프스크린 렌더: unit 1(15/60/100 상태), unit 2a(25/60/100 피규어 더미). 실제 컴포넌트를
  reflection 으로 구동해 ScreenSpaceCamera→RT 캡처(BattleScene 미로딩 시에도 가능).
- **미검증(라이브 Play 필요)**: 트레이 우측 실제 배치·탭 토글·오버플로우 플래시·실기 16:9/20:9.

## 남은 작업 (전부 라이브 배틀 Play 로 수렴)

- **unit 4 닫기(바깥 탭)**: 손패 뒤 전체화면 캐처 → 빈 영역 탭 Close. 손패 backing 이 이미
  드래그 cancel region(`DreamcatcherHandView.cs:822`)이라 **카드 드래그·취소와의 판별을 Play 로
  확인 후** 구현(미검증 상호작용 코드 금지). 현재도 항아리 재탭 → Close 존재.
- **unit 3 흡수 비행**: 킬 위치 → 항아리 mote 비행. `EnemyKilledEvent.position` 은 있으나 bridge
  C# 이벤트(`EnemyKilledAwakening(int)`)가 위치를 안 실어보냄 → **BattleBridge 이벤트 위드닝
  선행**(위치 surfacing, 브리지 게이트웨이 역할 내 — ECS write 아님). 컨트롤러가 위치 포함
  `AwakeningGained(int, Vector3)` 재노출 → 뷰가 worldToScreen 으로 비행. Play 검증 필수.
  주의: 항아리가 이제 트레이 우측으로 가시라 "숨은 위치 학습" 필요성이 줄어 clutter 리스크 재평가.
- **unit 5 Play 검증 하네스**: 현재 Play 진입 = **Draft 페이즈**. Battle 도달은 Draft→Placement→
  Battle 인터랙티브 진행 필요. 스크립트 배틀 진입(참고: `project_scripted_battle_e2e_verify`,
  `TestModeContext`)으로 Battle 직행 하네스 구축 후 위 항목 일괄 검증.

## Notes / 되돌리면 안 되는 것

- **절차적 피규어가 최종**(사용자 결정): 20단계 granularity 의 피규어 크기(~24px=1.4mm)에서
  Spine 은 미식별. 2b Spine 은 "적게·크게(6~8개, ~44px)" 재설계 시에만 가치.
- 오버플로우 플래시는 이벤트 반응이라 unit-5 "상시 pulse 금지" 계약과 무충돌.
- BattleScene 배선은 절차적 경로엔 불요(다음 세션에서 BattleScene 로드해 Play 검증만).
