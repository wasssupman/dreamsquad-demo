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

| 머지 | 커밋 | 컴파일 | EditMode | Play smoke |
|---|---|---|---|---|
| 1 비주얼 statics | `b564e768` (+writer 제한 후속) | ✅ 에디터 자체 컴파일 오류 0 | ⏳ 미실행 | ⏳ 미실행 |
| 2 임계 조회 역전 | `c0a361cb` | ✅ `dotnet build` 런타임+테스트 오류 0 | ⏳ **신규/해제 3건 포함 미실행** | N/A |
| 3 비-sim 코드 퇴거 | — | — | — | — |

> ⚠ EditMode/Play 미실행 사유: 사용자 Unity 에디터가 프로젝트 락을 보유해 batch 실행이 불가했다.
> `dotnet build` 는 **컴파일만** 증명하며 Unity Test Framework 를 실행하지 않는다(리뷰 HIGH 2).
> 락 해제 후 ① 머지 2 집중(`ModifierFrameworkTests`) ② 전체 EditMode ③ 머지 1 Play smoke
> 순서로 실행하고 이 표를 갱신한다.
