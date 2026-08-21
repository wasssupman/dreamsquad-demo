# 5 · handoff summary (dreamcatcher-orb-dock)

세션 인계 지도. 최신 계약은 README/번호 문서 우선.

## Commit (main)

- unit 0: `1f97f564`(구현)·`df2df7ef`(meta)·`676e7f8f`(스탬프) — Verlet 물리 코어
- unit 1: `8f70eaa6`(뷰)·`ad61b06f`(critic)·`22793f8b`(스탬프) — 항아리 독
- unit 2a: `1d7b199b` — 게이지 구동 피규어 물리 더미
- unit 4: `836aef63`(오버플로우 경고)·`1b7a21ff`(바깥 탭 닫기)
- unit 3: `9c19eee8`(흡수 비행)·`837bc9c6`(투트랙 리뷰 반영)
- 게이머 리뷰: `4525f89a`(오버플로우 낭비색/-N·비행 코얼레스·오탭·노이즈)·`b06371e4`(딜 게이트 터치-완주)
- unit 6: `b4e90da2` — backing 제거 + 100 가득(maxFigures 44) + 죽은 유닛 스킨(적별 re-skin)
- unit 7: `052509fa`(누운자세/강조20/어필)·`5da07b3c`(텍스트→소나 펄스 링)·`e61ce94b`(넛지+피규어 홉)

## Implemented (검증 완료)

- **unit 0** — `JarFigurePhysics`(Verlet+위치제약 순수코어). EditMode 6개 + 오프스크린.
- **unit 1** — 트레이 우측 항아리 독(`AwakeningGaugeView` in-place). 큰 숫자·데이터 파생
  코스트 틱·3단계 ready 림·라벨. `BindTray`(HandView.Start). critic clean.
- **unit 2a** — `JarFigurePile`: 게이지 비례 절차적 피규어 물리 더미(상한 20, 고정 스텝).
  **절차적이 최종**(24px 에서 Spine 미식별 → 2b 보류).
- **unit 4** — 오버플로우 경고(컨트롤러 `AwakeningOverflowed` + 뷰 골드 림 플래시) +
  바깥 탭 닫기(`DreamcatcherHandView._dismissCatcher` 보드 영역 캐처).
- **unit 3** — 흡수 비행(입자=피규어). 킬/사망 위치에서 피규어가 항아리로 아치 비행 → 도착 시
  pile. bridge 이벤트에 사망 view-space 위치 위드닝(ecs-review CLEAN), 고스트 풀링 + 전투 이탈
  CancelFlights. committed=active+pending 로 desync 방지.
- **unit 6** (2026-07-23 육안 완료) — 단색 backing 제거 · maxFigures 44(100 에서 항아리 가득,
  순수 FillHeight 97%) · **죽은 유닛 스킨**: 적 전부 한 스켈레톤 공유 → 브리지 `_enemyTypeByEntity`
  등록부(ECS 무변경, 파괴 Entity 값 키 유효) → relay 에 `ISpineUnitVisualData` 위드닝 →
  `SpineFigureBuilder.Reskin`(스켈레톤 일치 시 스킨 교체, 불일치 스킵).
- **unit 7** (2026-07-23 육안 완료) — 피규어 회전(낙하 텀블→RestRot 감쇠스프링 정착, 누운 자세
  포함) · 강조 시점 100→20(rim 골드를 `_ready`에) · **"이거 눌러봐" 어필**(텍스트 금지): 소나
  펄스 링(밖으로 퍼지는 골드 hollow 링 2개) + 통통 바운스 + 골드 림 브리딩 + 넛지(중앙쪽 기욺)
  + 피규어 홉(`JarFigurePile.Hop`, 바운스 리듬 들썩). `_ready && !_open && 패널활성`에만 구동.

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

- 다발 킬 clutter(흡수 비행 배칭/감쇠) / 라이브 랜드스케이프 스크린샷·실기 QA(16:9/20:9 그립) /
  미니멀 HUD 토글 / 즉발 캐스트 / 스와이프 여닫기 / NextWaveDock 리스킨.
- 어필 강도 튜닝(과하면 `attentionPeriod`/`attentionLean`/홉 세기/링 크기 개별 조절).
- 게이머 리뷰 미선택 2건: 슬로모를 손패열기→카드-lift 이동 / 코스트 스프레드 복원(전부 20→15/20/30).

## Notes / 되돌리면 안 되는 것

- **피규어 = Spine 미니어처가 최종**(unit 6·7 로 갱신). 이전 "절차적이 최종·Spine 미식별"은
  **뒤집혔다** — maxFigures 44 로 "적게·크게" 대신 "많이·작게 크라우드"로 재설계, 적별 스킨 re-skin.
  절차적 원은 미배선 폴백일 뿐.
- **44 = 100 에서 항아리 가득**(순수 FillHeight 97%). 바꾸면 채움 비례 깨짐(50=넘침, 32=72%).
- **죽은 유닛 스킨**: 적 전부 한 스켈레톤 공유 전제 → `Reskin` 이 스켈레톤 불일치면 스킵(디펜더 rig
  다르면 대표 스킨 유지). 브리지 `_enemyTypeByEntity` 는 파괴 Entity 값도 키 유효(역참조 금지).
- 항아리 독 클래스명 `AwakeningGaugeView` **유지**(씬 GameObject 1012444853 + 참조 2곳 보존).
- ~~**어필 상시성**: ready & 닫힘에만 절제된 주기 강조(펄스링/바운스/넛지/홉).~~
  **unit 8(2026-08-21)에서 전량 은퇴** — 항아리 탭이 꺼져(`JarTapEnabled = false`) 누를 수 없는
  대상이 손짓하는 상태였다. 지금 독의 유일한 자발적 움직임은 «회차(=한 회분 코스트)가 오르는
  0.3초» 하나뿐이고, 피규어 주기 튕김과 코스트 눈금도 같이 걷혔다. `8_charge_readout.md` 참조.
- 강조 시점 = **최소 코스트(20)**, 100 아님. rim 골드는 `_ready`(=`minCost/max`)에 켠다.
