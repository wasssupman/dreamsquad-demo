# 4 — Handoff (defender-clock-out)

## Commit

- `0bf2dfe7` unit 0 — 이동 진입구 차단 + 스펙(rev 2)
- `aac10fbe` unit 1 — 퇴근 경로 (`ReleaseDefenderTile` · `RetireDefender` · `DefenderRetired`)
- `e0da411f` unit 2 — 액션 슬롯 중립화 · 퇴근 버튼 · 쿨타임 · 카드 회수
- `6efe0f07` unit 3 — 퇴근 이탈 연출 (뷰 detach + 아치)
- `25835696` unit 3 **rev 2** — 퇴근 스냅(웅크림 → 즉발 뽑힘 → 배치 링). 아치 폐기
- (rev 3 키링 회수 = 시도 후 기각·롤백, 커밋 없음 — 0.28초에 부착이 안 읽혔다)
- unit 3 rev 4 — "퇴근 중"(연결 → 저항 → 뱅글뱅글 튕겨나감). 길이를 늘려 rev 3 제약 해소
- unit 3 **rev 5 = 확정** — 저항 1.75→0.85s, 뽑힘에 **depth 축**(카메라로 다가왔다 멀어짐)
  + 회전 900→1440°. 총 ~1.6초. 상세·튜닝값은 `3_retire_exit_flight.md` 상단

## Implemented

- 판 위 유닛 선택 → **"퇴근"** 버튼 → 즉시 판에서 내려온다(확인 절차 없음, 무료, 환급 없음).
- 퇴근은 **사망 경로를 타지 않는다.** `DeadTag` 를 안 달고 `DefenderDied` 를 안 쏘므로
  사직서·작별선물·각성이 **배제 코드 없이** 안 일어난다.
- 순찰병은 `PatrolLifecycleSystem` 의 `Exists(owner)` 판정에 얹혀 자동 회수된다.
- 재배치 시 `placementCooldown` 이 걸린다. **사망에는 안 걸린다**(의도 — 열린 밸런스 항목).
- 부착 드림캐쳐 카드는 회수되고 **각성 게이지는 안 오른다**(파밍 차단).
- 퇴근 유닛은 쓰러지지 않는다. **키링 줄이 걸려 당기고, 유닛은 박힌 채 움찔거리며 버티다가**
  뽑혀서 **카메라 쪽으로 훅 다가왔다가 멀어지며** 뱅글뱅글 4바퀴 돌아 화면 밖으로 나간다
  (rev 5, ~1.6초). 뽑히는 순간 떠난 칸에 배치 링이 치고, 트레이 칸이 1회 튕긴다.
- 이동/재배치는 **진입구만** 꺼졌다. 코드·설정·테스트 전부 살아 있다.

## Key Files

- `Bridge/BattleBridge.cs` — `ReleaseDefenderTile`(공유 정리) · `RetireDefender`(진입점)
- `Bridge/BattleBridge.Dreamcatcher.cs` — `DefenderRetired` 이벤트
- `Presentation/SpineUnitPool.cs` — `Detach()`(세 번째 출구)
- `UI/DefenderRetireFlight.cs` — 이탈 연출
- `UI/DefenderSelector.cs` — 쿨타임 시작 + 슬롯 펄스
- `UI/Dreamcatcher/DcInspectPanelView.cs` — `SetActionState(bool, string)`
- `UI/Dreamcatcher/DcInspectController.cs` — `RelocationEnabled` · `ResolveActionCallback` · `CanRetire`
- `Core/Dreamcatcher/DreamcatcherHandController.cs` — `RecoverCardsHostedBy`

## Verified

- `DefenderRetireTest` **5/5** · 회귀 9개 클래스 **23/23**(재배치×3 · BoardLimit×2 · 순찰병 ·
  PlacementAura · BountyMark · SlimeSplit) · EditMode `RelocationCheckTests` **8/8**.
  **rev 3 롤백 후 전부 재실행해 동일 결과 확인**(2026-08-14).
- **사망 관련 테스트는 한 건도 수정하지 않았다** — `ReleaseDefenderTile` 추출이 순수 이동이라는 증거.
- 사용자 Play 확인: 퇴근 버튼 · 즉시 퇴장 · 트레이 복귀(2026-08-13, unit 2 시점).
- **사용자 Play 확인 2026-08-15 — 연출 rev 5 통과.** 이 spec 의 육안 검증은 이것으로 종료.

## Notes (되돌리면 안 되는 의도)

- **퇴근에 `DeadTag` 를 달지 말 것.** 갈라짐이 전부 그 한 가지에 걸려 있다. 다는 순간 사직서·
  작별선물이 되살아나고 `DcTriggerKind.OnDeath` 카드가 퇴근에서도 터진다.
- **`DefenderRetired` 를 `DefenderDied` 에 플래그로 합치지 말 것.** 구독자 2개가 퇴근에서 둘 다
  다르게 군다(트레이는 쿨타임 추가, 손패는 각성 제거).
- **`DefenderSelector` 의 퇴근 핸들러를 사망 핸들러와 합치지 말 것.**
  unit 5 로 이유가 **강해졌다** — 예전엔 "한쪽에만 쿨타임이 있다"였고 지금은 "**두 쪽이 서로 다른
  값을 건다**"다. 경은 `Death_StartsLongerCooldown_ThanRetire`.
- **`ReleaseDefenderTile` 에 뷰 반납·엔티티 파괴를 넣지 말 것.** 둘 다 호출처마다 달라
  `bool playDeathAnim` 같은 플래그 파라미터를 부르게 된다.
- **액션 슬롯을 다시 기능 이름으로 특화하지 말 것**(`SetActionState(bool, string)` 유지).
  특화하면 "상수 한 줄이면 이동 부활"이 거짓이 된다.
- **`SpineUnitPool.Detach` 로 받은 뷰는 반드시 `Dispose`.** 풀이 더 이상 모르므로 teardown 이
  안 치운다 — `DefenderRetireFlight.OnDisable` 이 그 책임을 진다.
- **`DefenderRetireFlight` 의 `defenderSelector` 를 `dragController` 직렬화로 바꾸지 말 것.**
  드래그 컨트롤러는 런타임 `AddComponent` 라 인스펙터에서 영영 비고, 그러면 **키링이 조용히
  안 생긴다**(연출이 모션만 남아 "뽑는 주체" 가 사라진다).
- **떼어낸 뷰의 `Billboard`(Tilted) 를 다시 켜지 말 것.** 그게 회전을 매 LateUpdate 로 소유해서,
  켜져 있으면 뱅글뱅글이 다음 프레임에 조용히 덮인다. 회전 기준은 끄는 순간의 틸트를 캡처해 쓴다.
- **뽑힘의 "가까워짐" 을 스케일로 만들지 말 것.** 배틀 카메라가 **퍼스펙티브**(fov 36)라
  `-camera.forward` 이동을 원근이 알아서 확대한다 — 스케일까지 올리면 두 배로 부푼다.
  (직교로 바뀌면 정반대가 된다: depth 이동이 무의미해져 스케일이 필수다.)
- **회전 구간의 스케일은 균일하게.** 늘어난 채로 돌리면 매 프레임 실루엣이 찌그러진다 —
  rev 5 가 발사 직후 균일 복귀를 넣은 이유다.
- `RelocationEnabled = true` 로 되돌리면 이동이 부활한다. 그때 `RelocationMoveModeTest` 의
  드래그 단정도 함께 뒤집는다.

## Follow-up

- **연출은 rev 5 로 확정**(≈1.6초). ⚠ 여기서 더 줄이려면 **키링·저항을 함께 버려야** 한다 —
  rev 3 이 증명한 대로 짧은 연출과 부착 어휘는 양립하지 않는다. 둘은 한 묶음이다.
- ~~**열린 밸런스**: 사망엔 쿨타임이 없고 퇴근엔 있다~~ → **unit 5 에서 닫힘**(2026-08-15).
  두 출구가 각자 대기를 갖고 퇴근 = 사망 × ratio(0~1)라 뒤집힐 수 없다. `deathCooldown` /
  `retireCooldownRatio` 는 **시트 저작**(Defenders 탭에 열 2개 추가 필요 — 없으면 이니셜라이저
  10 / 0.4 로 도는 것이 정상 폴백).
- **`DcTriggerKind.OnRetire`** (핫식스 드랍 카드 등) — README 후속 후보에 필요 항목·함정 정리됨.
- **네이밍**: "퇴근"이 시즌 기믹 clock-out 과 화면에서 겹친다. UI 문안만 바꾸면 된다.
- **`placementCooldown` 은 이제 다른 축**(배치가 거는 연사 게이트, `maxOnBoard > 1` 전용).
  라이브 26종이 4를 들고 있는데 상한 1 이라 죽은 값이다 — 정리하려면 별도 저작 결정.

## 작업 환경 메모 (이 spec 특유)

워크트리를 다른 세션과 공유한 상태로 진행했다. `BattleBridge.cs` 와 `BattleScene.unity` 둘 다
남의 미커밋 작업을 담고 있어 **hunk 단위로 갈라 스테이징**했다(패치 추출 → `git apply --cached`).
그쪽 파일을 stash 하거나 되돌리지 않았다. 검증 중 두 차례 그쪽 컴파일 에러로 빌드가 멈췄고,
그때마다 손대지 않고 기다렸다.

`run_tests` 를 `test_names` 로 필터하면 **`total: 0` 인데 `Passed`** 가 나온다(거짓 통과).
**`group_names`(클래스명)로 돌릴 것.** 신규 테스트 파일은 `scope=all` 리프레시 전엔 안 잡힌다.

## 코드리뷰 반영 (2026-08-15)

두 트랙(일반 · ECS 경계)을 돌렸다. **ECS 경계는 6항목 전부 통과**(고칠 것 없음).
결정적 근거 하나가 새로 확보됐다 — **이 프로젝트의 Battle 시스템은 `IJobEntity` 를 쓰되 전부
`.Run()` 이고 `.Schedule()`/`.ScheduleParallel()` 은 `Scripts/Battle/` 전체에 0건**이다.
그래서 Mono Update 시점의 구조적 변경(`_em.DestroyEntity`)이 강제하는 sync point 가 사실상
no-op 이다. "사망도 매번 파괴하니 내성이 있다"는 간접 논거를 대체하는 직접 근거다.

일반 리뷰가 찾은 **실결함 2건 + 견고성 2건**을 고쳤다:

1. **teardown 훅 누락(MEDIUM·스펙 요구사항 미이행)** — unit 3 완료 기준이 "비행 중 매치 종료 시
   고아 0" 을 요구했는데 구현이 없었다. `OnDisable` 에 기댔지만 이 컴포넌트의 GO 는 **씬 루트라
   매치 재시작으로 비활성화되지 않는다**. `TeardownCurrentBattle` 에 `CancelAll()` 을 추가.
2. **파괴 순서(MEDIUM)** — `_em.DestroyEntity` 가 프레젠테이션 코드(키링 생성·코루틴 시작) **뒤**에
   있었다. 그 사이가 던지면 **엔티티는 살아 있는데 바인딩만 사라져** 그 유닛은 다시 못 만지고,
   나중에 죽어도 `hasBinding=false` 라 `DefenderDied` 가 안 나가 **부착 카드가 영구 소실**된다.
   되돌릴 수 없는 sim 변경을 `ReleaseDefenderTile` 직후로 올렸다(뷰 경로는 EntityManager 를 안 쓴다).
3. **`_inFlight.Add` 원자화** — `Run` 코루틴 안에 있어서, GO 비활성 등으로 코루틴이 시작되지
   않으면 떼어낸 뷰가 풀에도 `_inFlight` 에도 없는 **추적 불가 유령**이 됐다. `Fly` 안
   `StartCoroutine` 앞으로 옮기고 키링 루트는 `AttachKeyringRoot` 로 이어 붙인다.
4. **스케일 소유권 주장이 거짓이었다** — `SpineUnitView` 는 자기 코루틴 2개(`PunchRoutine`·
   `SquashRoutine`)로 `localScale` 을 덮는데 `Detach` 가 그걸 안 멈춘다. `Fly` 에서
   `view.StopAllCoroutines()` 를 불러 주석이 사실이 되게 했다.

테스트 2건 추가: **teardown 고아 0**(1번의 회귀 가드), **퇴근 시 각성 게이지 불변**(반파밍 계약의
유일한 방벽 — 그전엔 한 줄도 검증되지 않았다).

### ⚠ 이 과정에서 내가 만든 함정 두 개 (둘 다 테스트가 잡았다)

**⑴ `retireFlight?.CancelAll()` 이 빌드를 무너뜨렸다.** C# 의 `?.` 는 **Unity 의 fake-null 을
모른다.** `TeardownCurrentBattle` 은 `OnDestroy` 에서도 불리는데 그 시점엔 컴포넌트가 이미 파괴돼
있어 `MissingReferenceException` 이 나고, 그러면 그 메서드가 **중단돼 뒤의
`DestroyEntitiesByType<BattleTimeScale>()` 이 실행되지 않는다** → 싱글턴 누수 → 다음 씬에서
`found 2 instances` 로 5개 테스트가 무너졌다. `if (x != null)` 로 고쳤다 — 그 메서드의 형제 줄들이
전부 그 형태인 이유가 이것이다. **원인 규명은 신규 테스트를 먼저 빼고 돌려서**(여전히 실패 →
테스트 탓 아님) 갈랐다.

**⑵ teardown 테스트가 훅을 안 타는 트리거를 썼다.** `BeginPlacement()` 는
`TeardownCurrentBattle()` 을 **지나지 않는다.** 라이브 경로는 `StopBattle()` 이다
(`OnRestartRequested` 는 GameManager 주석대로 dormant). 첫 실행에서 이 테스트가 빨개져서 그 사실을
잡아냈다 — 훅을 안 타는 트리거로 검증하면 통과해도 아무것도 증명하지 못한다.

**검증**: `DefenderRetireTest` **7/7** · 회귀 9개 클래스 **23/23**.

### 안 고친 것 (의도)

- `PulseSlotFor` 의 `Tween.StopAll` 은 실제 경쟁자인 `ReadyFlourishRoutine` **코루틴**을 못 멈춘다
  → 같은 타입 2기를 연속 퇴근시키면 슬롯 펀치가 씹힌다. **순수 시각 결함**이라 보류.
- ②→③ 구간 전환 시 `sin(phase)` 가 임의 값이라 최대 0.16 view 단위 · 9° 스냅이 난다(NIT).
- `CanRetire` 에 `DeadTag` 검사가 없어 죽는 중인 유닛의 버튼이 켜진 채다 — 눌러도 `RetireDefender`
  가 false 라 규칙은 옳고 입력만 삼켜진다(NIT).
