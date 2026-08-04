# 11 — 선행 머지 3건 (적출 전에 Bridge 를 가볍게)

## 목적

M1 의 첫 코드 변경. sim 규칙 적출(unit 12+)에 앞서 **Bridge 에서 sim 과 무관한 표면을 떼어내고,
sim → Bridge 역방향 결합을 끊는다**. 세 작업 모두 **행동 변화 0**이 계약이며 각각 독립 커밋이라
문제 시 단독 revert 가 가능하다. salvage 판정표(`m1_salvage_matrix.md` §4)가 대상을 확정했다.

이 unit 이 CLAUDE.md 제약 1~4 **정식 개정의 시점**이다(README 이행표 계약) — 머지 2 가 끝나면
"sim 은 Bridge 를 모른다"가 코드로 성립하므로, 제약 1(BattleBridge 유일 창구)의 후계 불변식
(**asmdef 의존 방향**)을 명문화할 근거가 생긴다.

## 변경 대상

### 머지 1 — 비주얼 statics 분리

- 신규 `Assets/_Project/Scripts/Presentation/BattleVisualKnobs.cs` — static 21개 이주
  (CharacterVisualScale·CharacterBillboardTilt·PropDistanceTilt{Factor,Min,Max}·BlobShadow{Sprite,Size,Color,GroundY}·LiftScale{PerHeight,Max}·LiftShadow{FullHeight,MinScale,MinAlpha}·UseRealShadows·WalkAnim{SpeedEnabled,RefSpeed,MinTimeScale,MaxTimeScale,Smoothing,TeleportGuard})
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 선언 삭제(`:260-298`), 할당 4지점을 새 클래스로
  리다이렉트(`Awake:456` · `OnValidate:465` · `BuildMapForBattle:1077-1100` · `MirrorLiftKnobs:281-288`)
- 소비자 7파일 참조 교체: `Presentation/{SpineUnitView,QuadUnitView,BlobShadow,PropBillboard,UnitLiftVisual,AllyMarkerDecal}.cs` · `UI/DefenderDragPlacementController.cs`

**왜 지금**: Bridge 를 sim/뷰로 가르기 전에 **뷰-only 표면**을 먼저 뺀다. 청사진 ① §6 이 이미
"세션 계약 밖(SO 미러 상수)"으로 판정했으므로 이관 방향에 이견이 없다. SerializeField 는
Bridge 에 남는다(인스펙터 저작 지점 = 씬 배선 유지) — 옮기는 것은 **런타임 미러**뿐이다.

### 머지 2 — `GetStackThresholds` 의존 역전

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierTickSystem.cs:90` —
  `BattleBridge.GetStackThresholds(kind)` 호출 제거
- 신규 또는 이주: 임계 규칙 조회를 **sim 쪽 소유**로. 방향은 청사진 ② config-singleton 규칙과
  동형(= `MatchConfig` 물질화분 주입). 현 단계에선 ECS 인 만큼 **Effects 맥락의 조회 표면**으로
  옮기고 Bridge 가 등록만 한다(managed Dictionary 는 non-Burst 제약을 유지)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:6852` — public static 조회 API 를 등록 API 로 전환

**왜 지금**: 이것이 **sim → Bridge 프로덕션 결합의 유일한 지점**이다(나머지 5개 Battle→Bridge
참조는 전부 디버그 메뉴 = 머지 3 이 제거). 이 1줄을 뒤집으면 sim 폴더가 Bridge 를 전혀 모르게
되고, 그때 asmdef 분리가 **기계적으로 검증 가능**해진다(제약 1 후계 불변식).

### 머지 3 — sim 폴더의 비-sim 코드 퇴거

**실측 정정 (2026-08-04 리뷰 MEDIUM 1)**: 초안은 대상을 "MonoBehaviour 인 DebugMenu 5개"로 적었으나
둘 다 틀렸다 — 디버그 메뉴는 **MonoBehaviour 가 아니라 `#if UNITY_EDITOR` 정적 클래스**(`[MenuItem]`
툴)이고, `Battle/` 의 MonoBehaviour 는 **presenter 3종**이다. 두 부류의 성격이 달라 목적지도 다르다.

- **A. 에디터 툴 5파일 → `Assets/_Project/Editor/BattleDebug/`**:
  `Battle/Effects/{BlockingHazard,Fatigue,Hazard,Obstacle}DebugMenu.cs` · `Battle/Movement/PatrolDebugMenu.cs`.
  전부 `#if UNITY_EDITOR` + `using UnityEditor` + `FindAnyObjectByType<BattleBridge>()`. 런타임 폴더에
  에디터 API 가 사는 상태이고, **Battle→Bridge 잔존 참조 전부가 여기**다.
- **B. 뷰 MonoBehaviour 3파일 → `Assets/_Project/Scripts/Presentation/`**:
  `Battle/Effects/{BlockingHazardPresenter,PickupPresenter,ResignationPresenter}.cs`.
  Bridge 가 런타임에 프리팹에 붙이는 프레젠테이션이다.
- 이동은 **`.cs` + `.meta` 동반**으로 GUID 를 보존한다(프리팹·씬 참조 유지).
- Bridge 쪽 `Debug*` 멤버는 **이 unit 에서 옮기지 않는다** — 대상이 이동해도 호출부는 Bridge
  공개면이므로 정리는 규칙 적출 단계에서 함께(범위 관리).

**왜 지금**: sim 폴더에서 에디터 API 와 MonoBehaviour 가 사라지면 salvage 판정의 노이즈가 없어지고,
머지 2 와 합쳐 `Battle/` 의 Bridge 참조가 **0** 이 된다 — asmdef 분리의 기계적 게이트가 성립한다.

## 구현

세 머지를 **순서대로 독립 커밋**한다. 각 커밋 후 컴파일 + EditMode 전체를 돌려 회귀 0 을 확인한다
(라이브 행동 변화가 없으므로 골든 재생성은 불필요 — 값이 그대로면 `configHash`·trace 도 그대로다).
머지 2 는 스택 임계가 실제로 발동하는 경로(피로도→번아웃)가 EditMode 커버리지에 있는지 확인하고,
없으면 **최소 회귀 테스트를 함께 추가**한다.

## 완료 기준

- 머지별 독립 커밋 3개, 각각 컴파일 오류 0 · EditMode 회귀 0.
- 머지 1 후: Bridge 에 비주얼 static 선언 0(SerializeField 는 잔류), 소비자 7파일이 새 클래스 참조.
  **쓰기 표면은 타입이 강제**한다 — `Apply*` 3개 + 의도적 예외 `CharacterBillboardTilt` 만 열림
  (리뷰 MEDIUM 2).
- 머지 2 후: `Battle/` 의 **비-디버그 프로덕션** `BattleBridge` 코드 참조 0(잔존은 에디터 디버그
  메뉴 5개뿐 — 그 전부 제거는 머지 3 게이트다. 리뷰 MEDIUM 1 정정).
- 머지 3 후: `Battle/` 안 **MonoBehaviour 0**(presenter 3종 이동) **· `using UnityEditor` 0**
  (디버그 메뉴 5개 이동) **· `BattleBridge` 코드 참조 0**(주석/Tooltip 문자열은 제외).
- CLAUDE.md 제약 1~4 개정(이행표의 M1 열을 본문 규칙으로 승격) — 머지 3 완료 후 같은 커밋 또는
  직후 docs 커밋.
- 라이브 Play smoke 1판(전투 진행·콘솔 에러 0) — 뷰 상수 이관이 실제 렌더에 영향 없음 확인.

## 진행 기록

| 머지 | 커밋 | 컴파일 | EditMode | 골든 parity |
|---|---|---|---|---|
| 1 비주얼 statics | `b564e768` · writer 제한 `bfc75f09` | ✅ 에디터 리컴파일 오류 0 | ✅ 전체 통과 | ✅ (뷰는 트레이스 밖 — 아래 주석) |
| 2 임계 조회 역전 | `c0a361cb` · **PlayMode 호출부 보정** (후속) | ✅ 4어셈블리 오류 0 | ✅ **집중 12/12** | ✅ **byte diff 0** |
| 3 비-sim 코드 퇴거 | `562f83b7` | ✅ 4어셈블리 오류 0 | ✅ 전체 통과 | ✅ (파일 이동뿐) |

**골든 parity 실측 (HEAD `229ccd00`, 2026-08-04 23:15)**: 7 시나리오 × 2회 = 14 Play 세션,
전 시나리오 two-run diff 0 → 승격, 재생성본이 커밋본과 **byte 동일**(git clean + 백업 대비
`cmp` 7/7). 세부 판정 근거는 README "선행 머지 + unit 12 이후 기준선 재확인" 절.
머지 2 는 M1 에서 **유일하게 sim 을 건드린 커밋**이었고, 골든 녹음(12:44)보다 뒤(18:22)여서
기준선에 담겨 있지 않았다 — 그래서 unit 13 **앞에서** 돌린 것이다(용의자가 1개일 때 재는 것이
82파일 재배선 뒤에 재는 것보다 싸다).

> 머지 1 의 **렌더 정합은 이 초록이 답하지 않는다**(골든에 뷰 상태가 없다). 14판이 신규 콘솔
> 에러 0 으로 완주해 스모크의 "에러 0" 은 충족, 눈으로 보는 확인만 남았다.

**PlayMode 스위트 전체는 이 표의 게이트가 아니다.** 2026-08-04 실행에서 `passed=76 failed=15`
였고 전부 이 spec 이 건드리지 않은 경로다(골든이 byte 동일하므로 sim 은 무변). 진단·이관은
README 후속 후보의 "PlayMode 스위트 수리" 항목에 있다. 이 spec 의 sim 회귀 계측기는
**골든 byte diff** 이며, 콘텐츠 드리프트에 흔들리는 스위트를 게이트로 쓰지 않는다.

**전체 EditMode 실측 (2026-08-04)**: `passed=1895 failed=0 skipped=1` · 23.7s.
총계가 이전 1,888 → 1,896 으로 **+8**(unit 12 계약 테스트 7 + 레지스트리 미등록 1) 이고 skip 이
2→1 로 줄었다(머지 2 가 해제한 multiThreshold). **신규 테스트가 실제로 실행됐다는 증거는 이 카운트
증가**다 — "failed=0" 만으로는 테스트가 돌았는지 알 수 없다(리뷰 HIGH 2 의 교훈).
잔존 skip 1건은 `ModifierFrameworkTests.cs` 의 의도적 `[Ignore]`(AttackOutput 분기가 `AttackSystem`
업데이트 루프 안) — 그 커버리지는 PlayMode `DefenderApplyStackOutputTest` 가 갖는다.

**머지 2 집중 실행**: `.*ModifierFrameworkTests.*` → `passed=12 failed=0 skipped=1`.
집중 실행을 따로 돌린 이유 = 그 12건이 **실행됐음을 카운트로 증명**하기 위해서다.

### 검증 방법의 구멍 1건 (2026-08-04 발견·수정)

머지 2 가 `BattleBridge.GetStackThresholds` 를 삭제했는데 **PlayMode 테스트 3파일이 아직 그것을
호출**해 프로젝트가 컴파일 에러 상태였다(`error CS0117` ×3, 에디터 로그 실측). 원인은 코드가 아니라
**검증 범위**다 — 당시 `dotnet build` 를 `Wassup.Runtime` + `Wassup.Tests.EditMode` 2개만 돌려
`Wassup.Tests.PlayMode` 를 빼먹었고, 표에는 "테스트 오류 0" 이라고 적혀 있었다.

- 보정: 3파일을 `StackThresholdRegistry.Get(kind)` 로 전환 —
  `DefenderApplyStackOutputTest:45` · `DreamcatcherOnHitTest:70` · `KindlerFireStackE2ETest:49`.
  가드의 의미는 오히려 강해진다(저작 SO 존재 → **Bridge 가 실제로 등록했는지**).
- **이후 규칙**: 공개면을 지우거나 이름을 바꾼 커밋은 `Wassup.Runtime` ·
  `Wassup.Tests.EditMode` · **`Wassup.Tests.PlayMode`** · `Assembly-CSharp-Editor`
  **4개를 모두** 빌드한다. 2개만 도는 검증은 "테스트 통과" 로 적지 않는다.

**머지 3 달성 게이트(실측)**: `Assets/_Project/Scripts/Battle/` 안 MonoBehaviour **0** ·
`using UnityEditor` **0** · `BattleBridge` 코드 참조 **0**(주석/Tooltip 문자열만 잔존).
→ 제약 1 의 후계인 asmdef 의존 방향이 grep 으로 검증 가능해졌고, CLAUDE.md 절대 제약에
그 상태를 되돌리지 못하도록 명문화했다.

> ⚠ EditMode/Play 미실행 사유: 사용자 Unity 에디터가 프로젝트 락을 보유해 batch 실행이 불가했다.
> `dotnet build` 는 **컴파일만** 증명하며 Unity Test Framework 를 실행하지 않는다(리뷰 HIGH 2).
> 락 해제 후 ① 머지 2 집중(`ModifierFrameworkTests`) ② 전체 EditMode ③ 머지 1 Play smoke
> 순서로 실행하고 이 표를 갱신한다.

### 락 하에서 테스트를 실행하는 경로 (`SimTestAutoRunner`)

락 해제를 기다리지 않는다. `Assets/_Project/Editor/SimTestAutoRunner.cs` 가 **살아 있는 에디터**에게
트리거 파일로 실행을 시키고 결과를 파일로 회수한다 — batch 두 번째 인스턴스가 막히는 제약의 우회.

- 요청: `Temp/sim-test-request.txt` — 1행 `EditMode`|`PlayMode`|`Golden`, 2행(선택) 그룹 정규식.
  1초 폴링으로 소비된다. 메뉴 `Tools/Sim/Run {EditMode,PlayMode} Tests` 도 같은 일을 한다.
  `Golden` 은 `LegacyTraceGoldenRunner.RegenerateGoldens()` 기동이며 **골든 7종을 덮어쓴다** —
  판정은 재생성 후 `git diff Assets/_Project/Tests/Golden/`, 커밋 권한은 unit 19 뿐이다.
- 결과: `Temp/sim-test-result.txt` — 상태·카운트·**실패 전건**(이름 + 메시지 + 스택 6줄).
- 콜백은 도메인 리로드마다 재등록되므로 **Test Runner 창에서 사용자가 직접 돌린 실행도 수확된다**.
- 전제: 에디터가 **리컴파일을 한 번 해야** 러너가 로드된다(포커스 시 자동). 즉 트리거가 소비되지
  않고 남아 있으면 아직 리컴파일 전이라는 뜻 — 실행 실패로 오독하지 말 것.
- **신분증 확인이 선행 조건**: 러너는 로드될 때마다 `Temp/sim-test-runner.version` 에 자기 버전을
  쓴다(현재 `2-golden`). 새 모드를 쓰기 전에 이 값을 확인한다 — 아니면 **구 바이너리가 트리거를
  물어간다**. 모드를 추가하면 `RunnerVersion` 을 올린다.
- **모르는 요청은 거절**한다(폴백 금지). 실측 사고: 리컴파일 전에 `Golden` 을 던졌더니 구 러너가
  "PlayMode 아님 → EditMode" 로 폴백해 **전체 EditMode 를 조용히 재실행**했고, 골든이 돈 것처럼
  보였다. 지금은 `Debug.LogError` 후 아무것도 실행하지 않는다.

**계측의 기본 규칙 (같은 실패를 세 번 겪고 명문화)**: ① Unity 배치가 락으로 조기 종료한 로그에서
`error CS` 0 을 보고 성공으로 읽음 → 실제 에러 8건. ② `dotnet build` 를 2어셈블리만 돌리고
"테스트 오류 0" 으로 기록 → PlayMode 에 실제 에러 3건. ③ 골든 대기 조건을 `staged >= 14` 로
두었으나 10시간 전 산출물이 이미 14개 → 즉시 참. 공통 원인은 **증거의 부재/노후를 성공으로 읽은
것**이다. 따라서 판정은 항상 **baseline 대비 변화**로 한다 — 파일의 존재가 아니라 mtime·카운트·
로그 라인번호가 기준선보다 나아갔는지를 본다.
- `Temp/` 는 gitignored · 에디터 재시작 시 소실 = 저장소 오염 0. 러너는 M1 잔여 unit(13~20)이
  전부 EditMode+PlayMode 실행을 요구하므로 스캐폴딩이 아니라 상시 도구로 둔다.
