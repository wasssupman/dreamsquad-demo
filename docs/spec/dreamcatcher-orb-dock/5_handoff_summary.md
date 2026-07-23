# 5 · handoff summary (dreamcatcher-orb-dock)

세션 인계 지도. 최신 계약은 README/번호 문서 우선.

## Commit (main)

- unit 0: `1f97f564`(구현)·`df2df7ef`(meta)·`676e7f8f`(스탬프) — Verlet 물리 코어
- unit 1: `8f70eaa6`(뷰)·`ad61b06f`(critic)·`22793f8b`(스탬프) — 항아리 독
- unit 2a: `1d7b199b` — 게이지 구동 피규어 물리 더미
- unit 4: `836aef63`(오버플로우 경고)·`1b7a21ff`(바깥 탭 닫기)

## Implemented (검증 완료)

- **unit 0** — `JarFigurePhysics`(Verlet+위치제약 순수코어). EditMode 6개 + 오프스크린.
- **unit 1** — 트레이 우측 항아리 독(`AwakeningGaugeView` in-place). 큰 숫자·데이터 파생
  코스트 틱·3단계 ready 림·라벨. `BindTray`(HandView.Start). critic clean.
- **unit 2a** — `JarFigurePile`: 게이지 비례 절차적 피규어 물리 더미(상한 20, 고정 스텝).
  **절차적이 최종**(24px 에서 Spine 미식별 → 2b 보류).
- **unit 4** — 오버플로우 경고(컨트롤러 `AwakeningOverflowed` + 뷰 골드 림 플래시) +
  바깥 탭 닫기(`DreamcatcherHandView._dismissCatcher` 보드 영역 캐처).

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePhysics.cs` (순수 코어, EditMode 테스트)
- `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePile.cs` (피규어 풀/물리)
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` (항아리 독 — 센터피스)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` (Config getter,
  AwakeningOverflowed)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (Start→BindTray, 바깥 탭 캐처)

## Verified

- compile 그린(전 커밋). EditMode 1269/0-fail(내 6개 포함, 회귀 없음).
- 오프스크린 렌더(실 컴포넌트 reflection 구동 → ScreenSpaceCamera→RT): unit 1(15/60/100),
  unit 2a(25/60/100 피규어 더미).
- **라이브 배틀 하네스**(아래 "하네스" 참조): 독 활성·트레이 우측 배치(dockPos 192,18)·피규어
  12개 스폰(gauge 60)·바깥 탭 닫기(State 전이·캐처 토글)·오버플로우 코루틴 기동 확인.
- **미완(육안만)**: 라이브 랜드스케이프 스크린샷(게임뷰 aspect·overlay 캡처 제약), 실기 16:9/20:9.

## 배틀 직행 Play 하네스 (재사용)

Play 진입=Draft. `execute_code`(codedom)로 Battle 직행:
`gm=FindObjectOfType<GameManager>()`, `bridge=FindObjectOfType<BattleBridge>()`,
`hand=FindObjectOfType<DreamcatcherHandController>()`. 유닛=`bridge.DefenderPool[0]` 또는
`AssetDatabase.FindAssets("t:DefenderUnitData")`. 순서: `SetDefenderPool` → `SetPhase(Placement)`
→ `BeginPlacement()` → (reflection) `costRuntime.ResetToStart()` → `DebugGridSize` 순회
`PlaceDefenderAs(x,y,unit)` 첫 성공 → `SetPhase(Battle)`(패널 활성) → `costRuntime.BeginRegen()`
→ `StartBattle()` → (reflection) `GainAwakening(hand, 60)`(게이지 강제). **주의**: 단일 유닛 강제
배틀은 금방 끝나 Result 로 전이(패널 비활성) → 오버플로우 테스트 전 `SetPhase(Battle)` 재설정 필요.
Draft 는 레거시(사용자: 제거 가능) — 스킵 하네스가 지저분한 근본 원인.

## 보류 (후속 후보)

- **unit 2b Spine 스킨**: "6~8개·~44px" 재설계 시에만 가치. 절차적이 최종.
- **unit 3 흡수 비행**: 항아리 가시라 위치학습 목적 약화 + clutter + ECS 경계(bridge 위드닝). 필요
  시 가벼운(GaugeChanged generic-origin mote) 또는 풀 버전 별도 판단.
- 라이브 랜드스케이프 스크린샷 / 실기 QA / 미니멀 HUD 토글 / 즉발 캐스트.

## Notes / 되돌리면 안 되는 것

- **절차적 피규어가 최종**(사용자 결정). Spine 은 크기 문제로 미식별.
- 항아리 독 클래스명 `AwakeningGaugeView` **유지**(씬 GameObject 1012444853 + 참조 2곳 보존).
- 오버플로우/닫기 캐처는 이벤트/상태 반응이라 상시 pulse 금지 계약과 무충돌. 캐처는 카드 뒤
  sibling 이라 드래그·backing 취소 로직 무간섭(order7 독/NextWaveDock 도 무영향).
