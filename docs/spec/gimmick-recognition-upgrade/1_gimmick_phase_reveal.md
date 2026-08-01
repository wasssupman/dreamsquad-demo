# 1 — 기믹 페이즈 리빌

## 목적

배치 페이즈 안에서 안내를 띄우면 30초 타이머·코스트·슬롯 선택과 인지 예산을 다투고 진다. 안내를 **배치 앞의 독립 페이즈**로 빼내 경쟁을 제거하고, 텍스트 3줄을 아이콘·색조·움직임으로 옮겨 정보량을 줄이면서 인지를 올린다.

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase` 에 `Gimmick` **append**
- `Assets/_Project/Scripts/UI/GimmickPhaseView.cs` — 신규 리빌 뷰
- `Assets/_Project/Scripts/Data/GimmickRevealConfig.cs` + `Data/Config/GimmickRevealConfig.asset` — 신규 SO
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — `ProceedToPlacement()` 라우팅
- `Assets/_Project/Scenes/BattleScene.unity` — `GimmickPhaseView` GO + 참조 배선

**기존 `GimmickGuideView` 는 건드리지 않는다.** 이 커밋 착지 후 리빌과 배치 카드가 잠시 공존한다 — 의도된 중간 상태다(계약 10). 카드 은퇴는 unit 3.

## 구현

**⚠ enum 은 맨 뒤에 붙인다.** `GamePhase { None, Draft, Gift, Placement, Battle, Result, Tally, Gimmick }` — 값 7. `Gift` 뒤에 끼워 넣으면 `CameraDirectionConfig.asset` 의 raw int 직렬화(`phase: 1/3/4/5`, `breathPhases: 010000000300000004000000`)가 밀려 카메라 포즈·브리딩이 통째로 어긋난다. 카메라 포즈 엔트리는 **추가하지 않는다** — `Gift` 도 자체 연출이라 엔트리가 없다. `breathPhases` 에도 넣지 않는다(연출 중 브리딩은 간섭).

**라우팅** — 훅은 `GiftPhaseView.ProceedToPlacement()` **한 곳**이다(계약 4).

```
GiftPhaseView.ProceedToPlacement()
    └→ GimmickPhaseView.BeginReveal(onDone: BeginPlacementPhase)
           ├ 스킵 조건이면 즉시 onDone() (페이즈 전이 없음)
           └ 아니면 리빌 → onDone()
```

`GiftPhaseView` 가 이미 `placementPhaseView` 참조를 가지므로 콜백으로 넘긴다 — 리빌 뷰가 배치 뷰를 직접 알 필요는 없다.

**스킵 조건** (하나라도 참이면 연출 없이 즉시 `onDone()`):
1. `GameManager.AssignedGimmick == null` (기믹 비활성)
2. `TutorialProgress.ShouldRunCore(profileSO)` — 첫 판(계약 8). 훅 지점이 튜토리얼 스킵 경로까지 삼키는 퍼널이라 **리빌 뷰가 스스로** 판정한다. `GiftPhaseView` 와 같은 `profileSO` 참조.
3. `GimmickPhaseView` 미배선 — `GiftPhaseView` 참조가 null 이면 기존대로 `BeginPlacementPhase()` 직행(fail-open).

**`GimmickRevealConfig`** — 공통 타이밍 + 기믹당 엔트리:

```
공통:   beatStampSec(0.6) · beatNameSec(0.8) · beatOutSec(0.6) · dimAlpha · autoAdvance
엔트리: GimmickData ref · Color tintColor · GameObject revealVfxPrefab(null 허용) · AudioClip sfxClip(null 허용, unit 2)
```

프리팹·클립 null 허용이 계약 5다. 번아웃은 `VFX/Burnout_Smoke.prefab`, 레드불은 `VFX/LastRun_Torchlight.prefab` 을 꽂고, 사직서·온천은 비운다. 미등록 기믹은 tint 기본값 + 절차적 폴백으로 돈다 — **엔트리가 없어도 리빌은 성립한다**.

**3비트 안무** (총 ~2초, 타이밍은 전부 SO):

| 비트 | 내용 |
|---|---|
| ① 도장 | 딤 + `tintColor` 화면 물들임 + `icon` 대형이 찍히듯 등장(스케일 오버슈트). `revealVfxPrefab` 있으면 여기서 재생 |
| ② 명명 | `ruleLabel` 대형 + `displayName` 부제 |
| ③ 퇴장 | `summary` 한 줄 노출 후 전체 페이드아웃 — **흔적 없이 사라진다**(계약 6) |

파티클은 절차적으로 만든다 — `UiRoundedSprite.MakeCircle` 로 스프라이트를 생성해 트윈으로 흩뿌린다. 신규 아트 0. 딤은 `UiOverlay.Dim` 공용 값을 쓴다.

**탭 스킵** — 화면 어디를 탭하든 즉시 ③의 끝으로 점프. 반복 플레이에서 매판 2초 강제는 마찰이 된다(`GiftPhaseView` 탭 스킵 전례).

**연출 규율** — `sortingOrder` 는 `GiftPhaseView`(30)와 배치 HUD(7) 사이. 시퀀스는 PrimeTween `useUnscaledTime`. 자기 콜백 안에서 `Stop()` 금지(`BossWarningView` 교훈). **뷰가 비활성화돼도 `onDone` 을 반드시 호출한다** — 콜백이 유실되면 배치 페이즈가 영영 시작되지 않는다. 이게 이 유닛의 단일 최대 위험이다.

**VFX 얹기 주의** — `revealVfxPrefab` 은 스크린 오버레이 위가 아니라 **월드 카메라 앞**에 배치한다. 기존 두 프리팹은 월드 파티클이라 UI 캔버스 자식으로 넣으면 스케일·정렬이 깨진다. 보드 위 배치가 아니라 카메라 정면 고정 오프셋이라 바닥 평면(XY↔XZ) 문제는 발생하지 않는다.

## 완료 기준

- 컴파일 에러 0, EditMode 회귀 없음.
- `CameraDirectionConfig.asset` **무변경** — 페이즈 전이에서 카메라 포즈/브리딩이 기존과 동일(Play 육안).
- Play: 선물 페이즈 종료 → 리빌 3비트 → 페이드아웃 → 배치 타이머 시작. **연출 중 배치 타이머가 돌지 않는다.**
- 리빌 종료 후 화면에 기믹 UI 흔적이 없다(기존 배치 카드는 공존 상태라 별개).
- 탭 스킵: 연출 중 탭 → 즉시 배치 시작.
- `gimmickEnabled=false`: 리빌 없이 선물 → 배치 직행, 페이즈 로그에 `Gimmick` 없음.
- 첫 세션 튜토리얼 판: 리빌 생략, 배치 튜토리얼 정상 진행.
- `GimmickPhaseView` 참조를 비운 상태에서도 배치가 정상 시작(fail-open).
- 리빌 재생 중 뷰를 강제 비활성화해도 배치가 시작된다(`onDone` 보장).
- VFX 슬롯이 빈 기믹(사직서·온천)도 tint + 절차 파티클로 연출이 성립한다.
- **이 커밋 단독 revert 시** 기존 배치 카드만 남고 정상 동작한다.

## 2026-07-31 검증 기록

**통과** — 컴파일/콘솔 에러 0. Play 리플렉션 하네스(`AssignedGimmick` 주입 + `BeginReveal` 직접 호출)로:
- 스킵 경로: `AssignedGimmick=null` → 콜백 **동기 즉시** 발화, 페이즈 전이 없음(Placement 유지).
- 정상 경로: 페이즈 `Gimmick` 전이, 콜백 **3.42s 뒤 1회**(설정 합 3.0s + 프레임 오버헤드 — 일치).
- 내용: `ruleLabel`/`displayName`/`summary` 3층이 정확히 표시. 아이콘 없는 기믹(사직서)은 아이콘 GameObject 가 꺼지고 라벨만.
- VFX: 번아웃 `Burnout_Smoke` 스폰 확인(카메라 앞). 슬롯 빈 기믹은 절차 파티클만으로 성립.
- 딤/틴트 트윈 실제 실행(0 → 최종값 도달).

**튜닝 1건** — `tintAlpha` 0.28 → 0.12 (보수적 기본값, 실기 확인 대기). 틴트는 딤 **위에** 평평하게 덧발리므로 올릴수록 어두운 바탕을 되들어올려 텍스트 대비를 깎는 구조는 맞다. 다만 아래 함정 3 때문에 **화면 색은 스크린샷으로 판단할 수 없다** — 이 값은 실기 Play 에서 확정한다.

**검증 함정 3개** (다음 세션용):
- **오버레이 캔버스는 카메라 샷에 안 잡힌다.** Play 중 `manage_camera screenshot` 은 UI 가 통째로 빠진 그림을 준다(MENU 버튼까지 사라지는 게 판별 신호). **일시정지 상태에서 찍어야** 오버레이가 그림에 들어온다.
- **일시정지로 찍어도 색은 못 믿는다.** 오버레이가 **부분 합성**돼 실제보다 옅게 나온다. 근거: 딤이 `RGBA(0,0,0,0.82)` 풀스크린 + `rootAlpha=1` 인데 캡처의 숲 픽셀은 `(31,30,45)` — 정직하게 합성하면 `(14,17,23)` 이하여야 한다. **레이아웃·문구·줄바꿈 판단에만 쓰고, 딤/틴트/대비는 실기로 본다.** 색까지 봐야 하면 오프스크린 RT 하네스(ScreenSpaceCamera→RenderTexture)를 써야 한다.
- **툴 호출 왕복이 연출(~3s)보다 길다.** 연출 중을 잡으려면 `GimmickRevealConfig` 의 beat 값을 일시적으로 크게 올리고, 끝나면 반드시 원복 + `git diff` 로 확인한다(Play 중 SO 변경은 에셋에 그대로 남는다). 정지 시점은 `EditorApplication.isPaused = true` 를 알파 조건과 함께 걸면 원하는 비트에서 정확히 멈출 수 있다.

## 2026-08-01 rev — 드라이 문구 + 레이아웃 (사용자 요청)

- **문구를 메커니즘 중심으로 교체.** 추상 표현("내 유닛은 오래 둘수록 약해진다") → 수치 포함 드라이 서술("아군 10초마다 피로 +1 · 5스택에 공격력·공속·최대체력 -20% (15초)"). 수치는 각 기믹 SO 와 `StackModifier_Fatigue`(5스택 → magnitude 0.8, 15초)에서 실측해 옮겼다.
- **레이아웃 값을 SO 로 승격.** `iconOffsetY`(260) · `titleOffsetY`(20) · `subtitleGap`(76) · `summaryOffsetY`(-140) · `titleRiseFrom`(-64). 아이콘을 위로 올리고 제목/요약이 화면 중앙에 오게 했다. 레이아웃 적용은 `BuildCanvas`(Awake)가 아니라 `ResetVisualState` 에서 매번 — config 를 Play 중에 만져도 다음 리빌에 바로 반영되고 Awake 시점 config null 을 걱정할 필요가 없다.
- **알려진 리스크**: `summary` 에 수치가 문자열로 박혀 있어 SO 값을 재튜닝하면 **문구가 조용히 stale** 해진다.

## 2026-08-01 rev2 — 요약 읽을 시간 확보 (사용자 피드백)

**문제**: 핵심 정보인 요약이 3초 연출의 **마지막 1/3에만** 떴고, 온전히 읽을 수 있는 건 홀드 0.7초뿐이었다. 앞 1.4초는 아이콘·이름만 보여주며 비어 있었다.

**핵심 통찰**: **탭 스킵이 이미 있으므로 자동 진행 시간은 "탭하지 않은 사람에게만 적용되는 하한"이다.** 아는 사람은 탭으로 즉시 넘어간다. 그런데 탭 가능하다는 표시가 없어 빠른 길이 안 보였고, 그래서 자동 시간을 못 늘리는 교착이 있었다. 힌트를 넣으면 홀드를 늘려도 아무도 안 막힌다.

- **텀 축소** — `summaryLeadSec`(0.35) 신설. 요약이 ② 명명 **종료 전에** 미리 들어온다(`Chain` 대신 `Group` + `startDelay`). 처음부터 띄우는 안은 연출이 어색해져 기각(사용자 판정).
- **두 줄 + 의미 색** — 1행 조건 / 2행 결과. 색은 시안=조건·트리거, 초록=이득, 산호=손해, 흐린 회청=부가. 라벨 기본색을 골드 → 중립 밝은 톤으로 바꿔 강조와 경쟁하지 않게 했다. 요약 rect 높이를 `fontSize × 3.4` 로 키워야 2행이 안 잘린다.
- **"탭하여 계속" 힌트** — 요약 반 박자 뒤 알파 0.55 로 등장. 문구는 UI 고정이라 코드 상수(계약 9).
- **홀드 0.7 → 2.0s.**

순 효과: 요약 노출 ~1.6s → **~3.5s**, 온전한 읽기 0.7s → **~2.6s**. 아는 사람 체감은 오히려 빨라진다(전엔 탭 가능한 줄 몰라 끝까지 봤다).

**부족하면 다음 후보**: 그 기믹을 N번 본 뒤 자동으로 짧아지는 familiarity 게이팅. `TutorialProgress`/`PlayerProfileSO` 플래그 전례가 있다.

## 완료 확인

**2026-08-01** — 커밋 `b725ea14`(신설) · `4b257454`(틴트) · `89a38d43`(레이아웃 SO) · `27317ac5`(읽을 시간).

**사용자 실기 확인 완료** — 리빌을 직접 보고 세 건의 개정을 지시했다: ① 문구를 드라이 메커니즘으로 ② 아이콘을 위로·제목을 화면 중앙으로 ③ 설명 노출·유지가 짧다. 레드불 리빌 VFX 가 딤 아래 깔리는 것도 실기에서 관찰했고 **현행 유지로 판정**했다. 홀드는 사용자 지정 2.2s.

**전체 흐름 사용자 Play 확인 2026-08-01** — 선물 → 리빌 → 배치 라우팅 정상. 우려했던 `GamePhase.Gimmick` 전이와 첫 판 선물 튜토리얼 홀드의 간섭도 발생하지 않았다(같은 날 튜토리얼 units 19~21 확인과 함께). 드림캐쳐 카드가 같은 문제를 `DreamcatcherCardText` 포매터(에셋 description 은 폴백)로 푼 전례가 있다. 기믹은 4종·저빈도 튜닝이라 지금은 정적 문자열로 두되, 수치를 자주 만지게 되면 같은 포매터 패턴으로 옮긴다.
