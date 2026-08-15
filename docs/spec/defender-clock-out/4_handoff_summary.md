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

## Notes (되돌리면 안 되는 의도)

- **퇴근에 `DeadTag` 를 달지 말 것.** 갈라짐이 전부 그 한 가지에 걸려 있다. 다는 순간 사직서·
  작별선물이 되살아나고 `DcTriggerKind.OnDeath` 카드가 퇴근에서도 터진다.
- **`DefenderRetired` 를 `DefenderDied` 에 플래그로 합치지 말 것.** 구독자 2개가 퇴근에서 둘 다
  다르게 군다(트레이는 쿨타임 추가, 손패는 각성 제거).
- **`DefenderSelector` 의 퇴근 핸들러를 사망 핸들러와 합치지 말 것.**
  `Death_DoesNotStartPlacementCooldown` 이 그 경보다 — 합치면 즉시 빨개진다.
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

- **육안 확인 남음**: 퇴근 연출 전체 — 줄이 걸리는 순간 · 움찔거리며 버티는 1.75초 ·
  뽑히며 뱅글뱅글 튕겨나감 · 배치 링 · 그림자 제거 · 트레이 칸 펄스.
- **연출은 rev 5 로 확정**(≈1.6초). ⚠ 여기서 더 줄이려면 **키링·저항을 함께 버려야** 한다 —
  rev 3 이 증명한 대로 짧은 연출과 부착 어휘는 양립하지 않는다. 둘은 한 묶음이다.
- **열린 밸런스**: 사망엔 쿨타임이 없고 퇴근엔 있다 → `placementCooldown` 저작 시 "죽게 두는 게
  빠른가"를 의식할 것. 실플레이에서 방치가 최적으로 굳으면 사망에도 태우는 rev.
- **`DcTriggerKind.OnRetire`** (핫식스 드랍 카드 등) — README 후속 후보에 필요 항목·함정 정리됨.
- **네이밍**: "퇴근"이 시즌 기믹 clock-out 과 화면에서 겹친다. UI 문안만 바꾸면 된다.
- **`placementCooldown` 값 저작** — 지금 전 유닛 0(= 즉시 재배치). 켜는 것은 저작 행위다.

## 작업 환경 메모 (이 spec 특유)

워크트리를 다른 세션과 공유한 상태로 진행했다. `BattleBridge.cs` 와 `BattleScene.unity` 둘 다
남의 미커밋 작업을 담고 있어 **hunk 단위로 갈라 스테이징**했다(패치 추출 → `git apply --cached`).
그쪽 파일을 stash 하거나 되돌리지 않았다. 검증 중 두 차례 그쪽 컴파일 에러로 빌드가 멈췄고,
그때마다 손대지 않고 기다렸다.

`run_tests` 를 `test_names` 로 필터하면 **`total: 0` 인데 `Passed`** 가 나온다(거짓 통과).
**`group_names`(클래스명)로 돌릴 것.** 신규 테스트 파일은 `scope=all` 리프레시 전엔 안 잡힌다.
