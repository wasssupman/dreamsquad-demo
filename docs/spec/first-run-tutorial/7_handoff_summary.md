# 7 — Handoff Summary

## Commit

- `66673d05` docs — spec 최초 작성 (설계 critic 3종 반영 rev 2 포함)
- `cacee249` feat — units 0~6 구현
- `52aba94d` feat — Play 검증 결함 4건 수정 + 온보딩 전용 웨이브(60초)
- (이 문서와 같은 커밋) refactor — 구현 critic 반영

## Implemented

- **로비** 딤 + START 구멍 + 안내 문구(로비만 딤이 보인다 — 배틀은 투명 차단막). 로그인 전·로드아웃 미충족 계정에는 뜨지 않는다.
- **맵 설명** 배치 가능 ↔ 불가 하이라이트 왕복 → 목표 문구. 카운트다운을 붙잡아 두고 돈다.
- **첫 배치** 전투 N초 후 정지 → 캐논 셀 → **적이 강을 건너올 때까지 대기** → 배치 →
  정지 풀고 배치 스킬 관람 → 문구.
- **드림캐쳐 부착** 재개(플레이 구간) → **트레이 셀 탭으로** 유닛 선택 → 부착 가능한 Unit 카드만 열기 → 부착 → 마무리.
- **온보딩 전용 웨이브** `WavePlan_FirstRunTutorial`(60초 · 4웨이브 · 적 20). 맵·덱은 라이브 그대로.
- **토너먼트 제외** — 온보딩 판은 참가 신청을 생략한다.
- **RESET TUTORIAL** 개발 버튼이 진행을 되돌린다.

## Key Files

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` — 배틀 시퀀스 전부
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/LobbyTutorialStep.cs` — 로비 한 스텝
- `Assets/_Project/Scripts/Data/FirstRunTutorialConfig.cs` + `Data/Config/…asset` — 수치 전량
- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_FirstRunTutorial.asset`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `Start` 호출 · 토너먼트 우회 · RESET
- `Assets/_Project/Scripts/Core/GameManager.cs` — 웨이브 플랜 주입
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `MapHasTile` · `AnyEnemyWithinTilesOf` · blocked 하이라이트
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — 인트로 홀드

## Verified

- EditMode 코어 2,334(0 fail · 3 사전 skip) · 에셋 177/177
- Play 로 배치·부착·반복 결함을 재현한 뒤 수정 확인(라이브 계측: `targets`/`holes`/`scale`/`deployed`)
- 씬 배선 참조 12/12 non-null

## Notes — 되돌리면 안 되는 것

1. **`SetHoles(null)` 은 «전부 열기» 가 아니라 «구멍 없는 전면 차단»** 이다. 보드를 열어야
   하면 차단막을 **내려야** 한다(보드는 UGUI 가 아니라 감쌀 rect 가 없다). 세 번 물렸다.
2. **구멍은 한 번 잡고 끝내면 안 된다.** `SetHoles` 는 비활성 대상을 버린 뒤 다시 담지 않고,
   오버레이는 코너 «변화» 만 추적한다. 손패 카드(딜인)와 보드 프록시(카메라 이동) 둘 다
   **대기 루프에서 매 프레임 다시 잡는다.**
3. **타임아웃이 없다**(계약 11). 조건 대기는 반드시 만족 가능해야 하고, 만족 불가면 기다리지
   말고 **진입 전에 건너뛴다**. 정지 중에는 코스트·쿨타임·각성이 회복되지 않고 **매치 타이머도
   안 흐른다** — 만족 불가한 대기 하나가 곧 앱 강종이다.
4. **정지는 우선순위 100.** 손패·유닛 선택이 50 으로 슬로모를 요청한다.
5. **배틀 차단막은 불투명도 0.** 「입력 차단」과 「화면 어둡게」는 별개 축이다 — 알파를 올리면
   판이 도는 동안 화면이 덮여 어색하다(사용자 판단). 안내는 포커스 링이 한다.
6. **4.1 은 트레이 셀로 유도한다.** 소진 셀 탭이 이미 «판 위 그 유닛으로 데려간다» 라서
   있는 어휘를 가르치는 것이고, 구멍도 고정 UI rect 하나면 된다.
7. **재개 구간(B3→B4)은 차단막을 내린다.** 켜두면 캐논이 혼자 죽는 걸 구경만 하게 되고
   B4 가 구조적으로 실패한다.
8. **`_introHeld = false` 는 `SetPhase` 앞.** `SetPhase` 가 `PhaseChanged` 를 동기 호출하고
   구독자가 그 자리에서 홀드를 세운다.
9. **로비 안내는 `Start` 에서.** `Awake` 에서 띄우면 `TutorialGuidanceView.Awake` 의 `Hide()` 가
   지운다(차단막만 남아 «버튼만 포커스된» 화면).
10. **`ShouldRun` 은 세 곳을 동시에 결정한다** — 안내·웨이브 플랜·토너먼트 제출(계약 16).

## Follow-up

- **Play 재검증** — 리팩토링(구멍 매 프레임 갱신 · 카드 상한 대기 · 홀드 순서 · 판 종료
  중단)과 UX 변경(투명 차단막 · 4.1 트레이 셀) 이후 전 구간을 한 번 태워야 한다.
- **문구 다듬기** — 현재는 사용자 원문 그대로(띄어쓰기 포함).
- **`battleFreezeAtSeconds` 실측 튜닝** — 정지 시점에 적이 화면에 있어야 한다.
- **`TryGetAffordableTutorialSlot`** — teardown 이 남긴 프로덕션 소비자 0 API. 정리 후보.
- 나머지 후속 후보는 README 하단 참조.
