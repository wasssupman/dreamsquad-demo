# camera-direction units 10~14 — 인계 요약

## Commit

(미커밋 — 사용자 확인 대기)

## Implemented

- **카메라 포즈 저작이 «홈 기준 델타» 에서 «상태별 독립 레시피» 로 바뀌었다.** 상태는 배치·전투 2개.
  각 상태가 `대상 · 각도 · 거리 · 화면 세로 위치 · 화각 · 흐림` 을 통째로 소유하고, 포즈는 매 프레임
  절대값으로 계산된다. **상태끼리 공유하는 기준점이 없다** — 전투를 튜닝해도 배치가 미동도 안 한다.
- `SolveStatePose` 순수 함수 + EditMode 테스트 11개. 화면 세로 보정은 **카메라 로컬 up** 으로 민다
  (월드 Y 로 밀면 시야 깊이가 변해 판 크기가 흔들린다).
- 상태 해석은 매 프레임: 배치/기믹 → 배치, 나머지 전부 → 전투. 구 «미등록 페이즈 = hold» 은퇴.
- 전환은 도착 상태의 `flightSec`/`ease`. **양쪽 레시피를 매 프레임 풀어 결과를 섞는다**(포즈를 얼리지 않음).
- 배치 커서 추종: 기존 드래그 포커스 채널의 **델타 해석만** 교체(`PanDelta`). 새 채널·스프링·staleness 없음.
- 상태별 흐림: 배치는 끄고(전환 종료 후 `mode` Off), 전투는 현행 값. 포즈와 **같은 진행도**로 섞인다.
- 은퇴: `_homePos/_homeRot/_homeFov` 캡처 · `CameraPhasePose` · `boardFitMargin/Pullback/RaiseY` ·
  `dofBlurStartT/EndT` · `FrameBoard`(→ `SetBoardBounds`).

## Key Files

- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — `CameraState`, `CameraStateFraming`, `stateFramings`
- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs` — `SolveStatePose`
- `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — `PanDelta`
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — `UpdateStatePose` / `ResolveState` / DoF
- `Assets/_Project/Tests/EditMode/CameraStatePoseTests.cs`
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — 두 상태 값

## Verified

- EditMode 2529개 실행, 카메라 관련 전부 초록. 실패 1개(`UnitKitCatalogTests` malphite 설명문 줄길이)는
  **HEAD 부터 있던 것** — 해당 에셋 미변경이고 이 작업과 무관하다.
- Play(BattleScene, 2160×1080 = 화면비 2.0), **전투 pitch 47 시점**:
  - 전투 포즈 `(0, 14.4115, -14.4215) @47°`, **보드 중앙 화면 세로 0.5543** = 저작값 일치.
  - 같은 레시피를 16:9 로 풀면 `(0, 15.849, -15.860)` — 손으로 잡은 포즈와 일치.
  - 배치 포즈 `(0, 20.711, -8.371) @70°`, 화면 세로 0.554, 보드 점유 `x[0.10, 0.90]`.
  - 배치→전투 전환 0.6초 보간 관측(pitch 69.97 → 69.56 → … → 47.000), 도착 시 정확히 정착.
  - 흐림: 배치에서 `mode = Off`, 전환 시작 프레임에 Gaussian 으로 복귀. 콘솔 에러 0.
- **그 뒤 사용자 튜닝으로 전투 pitch 47 → 50, 흐림 0.6~0.88/r1.5 → 1.0~1.6/r1.0 로 바뀌었다.**
  튜닝 후 값으로 다시 계산한 프레이밍(코드 경로 동일, Play 재확인은 미실시):
  전투 21×12@16:9 `(0, 16.481, -14.850)` 보드 y[0.18,0.82] · 12×10 y[0.07,0.87] · 30×18 y[0.16,0.83].
- 맵 크기 4종(12×10 · 15×10 · 21×12 · 30×18) × 화면비 3종(16:9 · 2.0 · 19.5:9) 전수 — 두 상태 모두
  판 네 코너가 화면 안(튜닝 후 값으로 재검산 포함).

## Notes

- **행동 변화**: 상태 전환 0.6초 동안 드래그 포커스·인스펙트가 강제 페이드아웃된다(전환 우선).
  이전에는 `flying` 이 `enableNonDragEffects` 게이팅이라 영구 false 였다. 전환 순간에 드래그
  중이면 팬이 죽었다 살아난다 — 손맛은 Play 로 확인이 필요하다.

- **되먹임 금지가 이 설계의 뼈대다.** 커서 추종 입력은 스크린 NDC 이고 월드로 투영하지 않는다.
  투영하면 «카메라 이동 → 대상 이동 → 카메라 이동» 이 되고, 스프링은 진동을 숨길 뿐 없애지 못한다.
  `CameraComposeMath.PanDelta` 주석에 이유가 있다 — 되돌리지 말 것.
- **씬의 Main Camera 포즈는 런타임에 읽지 않는다.** 에디터에서 보이는 그림과 플레이가 다를 수 있다.
  런타임이 안 읽는 값 때문에 dirty 씬을 저장하지 않는다(이 프로젝트에서 반복 사고가 난 축).
- `DepthOfField.IsActive()` 는 반경을 보지 않는다 — 반경 0 으로는 안 꺼지고 풀스크린 패스가 계속 돈다.
  그래서 **전환이 끝난 뒤 `mode` 를 Off** 로 내린다. 끄는 타이밍을 전환 중으로 옮기면 팝이 난다.
- `gaussianMaxRadius` 저작 범위는 0.5~1.5 이고 실제 반경은 **해상도에 비례**한다(상한 2).
- 인스펙트 채널은 **살아 있다** — 유닛 상세 줌은 2026-08-19 결정대로 꺼져 있지만, 방향 지정 조준
  (`DirectionAimController`)이 같은 채널을 쓴다. 기준만 홈 포즈 → 상태 포즈로 바뀌었다.

## 변경 전 카메라를 새 구조로 되살리는 법

이 재설계 이전의 카메라를 다시 보고 싶을 때(비교·롤백·«그때가 나았다» 판단)를 위한 이관표다.
**구 값을 그대로 복원할 수는 없다** — 구조가 달라서 환산이 필요하고, 그 환산은 맵 크기·화면비마다
달라진다. 그 사실 자체가 이 재설계의 이유이므로, 아래 수치도 **기준 조건을 반드시 함께 읽어야 한다.**

### 변경 전에 실제로 화면에 있던 것

`enableNonDragEffects: 0` 이라 **페이즈 델타가 하나도 적용되지 않았다.** 배치·전투·집계·결과가
전부 같은 포즈(구 «홈»)를 썼고, 유닛 상세 줌도 2026-08-19 에 꺼져 있었다
(`DcInspectController.InspectZoomEnabled = false`). 즉 **실제로 존재한 카메라 상태는 하나**였다.
아래 (2)(3)은 «데이터에는 있었지만 화면에 나온 적 없는» 포즈다 — 되살릴 때 그 점을 감안할 것.

구 홈의 정의: 보드 fit(`boardFitMargin 1.12`) + `boardFramePullback 4`(추가 후퇴)
+ `boardFrameRaiseY 2`(카메라를 월드 -Y 로), 회전·화각은 씬 카메라 값(pitch 60 / fov 35.9834).

### (1) 구 홈 = 당시 전 페이즈 공통 포즈 → `CameraStateFraming`

| 기준 조건 | `pitchDeg` | `fitMargin` | `screenY` | `fov` |
|---|---|---|---|---|
| 21×12 · 16:9 | 60 | **1.2270** | **0.5592** | 35.9834 |
| 21×12 · 2.00 | 60 | 1.2383 | 0.5649 | 35.9834 |
| 12×10 · 16:9 | 60 | 1.2632 | 0.5770 | 35.9834 |

**환산값이 조건마다 다른 것이 핵심이다.** `pullback`/`raiseY` 가 절대 거리라 판이 작아질수록
상대적으로 크게 작용했다 — 같은 저작이 맵마다 다른 그림을 냈다는 뜻이고, 새 구조에서는 한 줄
(`fitMargin`)이 모든 맵에서 같은 비율을 낸다. 되살린다면 **주력 맵 조건 하나를 골라** 그 행을 넣는다.

### (2) 구 배치 델타(`localPos.z -0.8`, `pitchOffset -5`) → 얹은 결과

| 기준 조건 | `pitchDeg` | `fitMargin` | `screenY` | `fov` |
|---|---|---|---|---|
| 21×12 · 16:9 | 55 | 1.2383 | **0.4230** | 35.9834 |
| 12×10 · 16:9 | 55 | 1.3443 | 0.4396 | 35.9834 |

`screenY` 가 0.5 **아래**인 데 주의. 고정 위치에서 pitch 만 5° 세우면 시선이 위로 밀려 보드가
화면 아래쪽으로 내려간다 — 델타 구조의 부작용이고, 새 구조에서 같은 그림을 원하면 이 값을 쓴다.

### (3) 구 유닛 상세 채널 → 상태로 옮길 때

구 값: `inspectDolly 3`(전진) · `inspectFovDelta -6` · `inspectFrameBiasY 0.35` ·
`inspectLookWeight 0.5` · `inspectPitchDeg 0` · 페이드 in 0.22 / out 0.3 · `inspectFollowRate 12`.
보드 중앙의 유닛을 골랐을 때 합성 결과(21×12 · 16:9): **pos `(0, 19.263, -12.667)` · pitch 63.24 ·
fov 31.0 · 대상까지 거리 23.05**.

레시피로 옮기면 대략 `pitchDeg 63` / `fitToBoard false` / `fixedDistance 23` / `fov 31` /
`screenY` 는 `inspectFrameBiasY` 환산값. 다만 **정확히 같아지지는 않는다**:

- `inspectLookWeight 0.5` 는 «유닛 쪽으로 절반만 돌아보는» **부분 lookat** 인데, 새 레시피는 대상을
  항상 정조준한다. 부분 조준을 표현할 자리가 없다 — 필요하면 레시피에 축을 늘려야 한다.
- `fov 31` 은 `fovMin` 클램프에 걸린 값이다(35.98 − 6 = 29.98 → 31). 레시피에 31 을 직접 적으면
  클램프에 의존하지 않는다.
- 상태로 만들면 **배타적**이 된다 — 상세 중에는 배치/전투 포즈가 밀려난다. 채널이던 시절과 달리
  진입/이탈이 전환(0.6초)을 타므로 페이드 시간(0.22/0.3)은 전환 시간으로 옮겨 적는다.
- 되살리기 전에 **2026-08-19 「선택 줌을 끈다」 결정**부터 확인할 것. 그리고 같은 채널을 방향 지정
  조준(`DirectionAimController`)이 아직 쓰고 있어, 상태 승격 시 그쪽까지 전환을 타게 할지 먼저 정해야 한다.

### (4) 구 흐림

전역 `dofBlurStartT 0.6` / `dofBlurEndT 0.88`, 프로파일 반경 1.5, 모드 상시 Gaussian.
새 구조에서는 전투 레시피에 `dofStart 0.6` / `dofEnd 0.88` / `dofMaxRadius 1.5` / `dofEnabled true`,
배치도 같은 값으로 켜면 당시와 같다(당시엔 상태 구분이 없었다).

## Review

리뷰(격리 에이전트)에서 나온 지적을 반영했다. 치명 0. 되돌리면 안 되는 것:

- **DoF 반경 하한 0.5 를 apply 단계에 걸지 말 것.** 그 값은 페이드 중인 블렌드 결과라,
  0.5 에서 잘리면 켜지는 첫 프레임에 절반 세기가 들어오고 꺼질 때 툭 끊긴다. 저작값 범위
  0.5~1.5 는 `SolveDofFor` 가 입구에서 지킨다.
- **`aspect` 를 Director 에서 클램프하지 말 것.** `FrustumTangents` 의 «aspect<0.01 → 16:9»
  폴백이 안 걸려, 게임뷰 0 폭에서 fit 거리가 100 배로 튄다(전용 테스트가 있는 가드다).
- **`_dofDrivable` 프로브는 `!= Bokeh`.** `== Gaussian` 으로 두면 씬 프로파일을 Off 로 저작하는
  순간 DoF 가 로그 없이 영영 죽는다 — 모드를 이제 Director 가 소유하므로 Off 저작은 자연스럽다.
- **`_settled` 무효화는 `CommitStatePose` 한 곳.** 직전 base 포즈와 비교한다. 화면비·판 교체·
  Play 중 인스펙터 튜닝이 전부 여기서 커버된다(개별 감지를 다시 만들지 말 것).

## Follow-up

- **실기 확인**: 흐림 세기가 해상도 비례라 폰에서 다르게 보인다. 배치·전투 두 상태를 눈으로.
- **작은 맵의 하단 여백**: `margin 1.0058` 로 전투 프레이밍이 12×10 에서 보드 아래끝이 화면 y 0.07 까지
  내려온다(21×12 에서는 0.20). 하단 트레이에 물리면 전투 `fitMargin` 을 올린다.
- 유닛 상세 카메라 상태 · 전환 쌍 예외 표 · 씬 카메라 굽기 버튼 — README 후속 후보 참조.
