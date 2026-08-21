# unit 10 — 카메라 상태 레시피와 프레이밍 계산

## 목적

카메라 포즈의 저작 단위를 "홈 포즈 기준 델타"에서 **상태별 독립 레시피**로 바꾸기 위한 토대.
이 유닛은 데이터 타입과 순수 계산 함수만 만든다 — 실제 배선은 unit 11.

기존 구조에서는 모든 페이즈 포즈가 홈 기준 델타라 **홈을 건드리면 전 페이즈가 딸려 온다**.

**사실 관계 주의**: 이 결함은 저장소 HEAD 에서는 *잠들어 있었다*. `enableNonDragEffects: 0`
때문에 페이즈 델타가 하나도 적용되지 않아(`CameraDirector.OnPhaseChanged` 조기 return +
LateUpdate 의 `_flightDelta = default`) **배치와 전투가 같은 포즈를 썼다**. 즉 에셋의
`pitchOffset -5`(배치) 는 죽은 값이었다. 2026-08-21 에 «전환이 블렌딩되게 해달라» 는 요청으로
그 게이트를 떼자 값이 살아났고, 그때 전투 각도를 60°→47° 로 내리니 배치가 55°→42° 로 함께
내려갔다. 되살리려 역산해 넣은 고정 오프셋은 맵 크기 비종속이 되는 2차 결함을 낳았다.

따라서 은퇴 대상(`phasePoses` · `boardFramePullback` · `boardFrameRaiseY`)은 **이관할 실사용
값이 없는 순삭제**다. 상태끼리 기준점을 공유하지 않으면 이 결함군 자체가 성립하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — `CameraState` enum + `CameraStateFraming` 신설
- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs` — 레시피 → 포즈 순수 함수
- `Assets/_Project/Tests/EditMode/CameraFramingMathTests.cs` — 테스트 추가

## 구현

`CameraState` = `Placement` / `Battle` (2개). 페이즈 enum(7종)과 별개다 — 상태는 페이즈보다
훨씬 적고, 「어느 페이즈에 어떤 그림을 보여줄까」는 연출 정책이지 게임 규칙이 아니다.

**유닛 상세는 상태가 아니다** (2026-08-21 사용자 결정). 상세 줌은 2026-08-19 에 이미 끈 기능이고
(`DcInspectController.InspectZoomEnabled = false`) 되살리지 않는다. 인스펙트 채널은 방향 지정
조준(`DirectionAimController`)이 계속 쓰므로 **채널 그대로 남는다** — 상태 포즈 위에 얹힌다.

`CameraStateFraming` (`[Serializable]`, config 에 배열로):

| 필드 | 뜻 |
|---|---|
| `state` | 어느 상태의 레시피인가 |
| `pitchDeg` | 대상 위 몇 도에서 내려다보는가 |
| `fitToBoard` | true = 판 전체가 들어오는 거리를 맵마다 계산, false = `fixedDistance` |
| `fitMargin` | fit 여백 배율 (1 = 코너가 화면 가장자리에 딱 닿음) |
| `fixedDistance` | fit 을 안 쓸 때의 대상까지 거리 |
| `screenY` | 대상이 놓일 화면 세로 위치 (0.5 = 정중앙, 클수록 위) |
| `fov` | 이 상태의 화각 |
| `flightSec` / `ease` | 이 상태로 **들어올 때**의 기본 전환 |

순수 함수 `CameraFramingMath.SolveStatePose(대상 월드좌표, 레시피, 보드 bounds, aspect, out 위치, out 회전, out fov)`:

1. 회전 = `Euler(pitchDeg, 0, 0)`. yaw/roll 없음 — 이 게임의 보드는 화면 정면 고정이다.
2. 거리 `d` = `fitToBoard` 면 기존 `FitDistance(코너, tanH, tanV, fitMargin)`, 아니면 `fixedDistance`.
3. 위치 = `대상 - forward·d`, 그 뒤 **카메라 로컬 up 축**으로 `u = -(screenY-0.5)·2·d·tanV` 만큼 평행이동.

DoF 필드는 **여기서 만들지 않는다.** unit 15 가 같은 클래스에 붙인다 — 같은 타입에 나중에
필드를 더하는 일에는 위험이 없고, 아무도 안 읽는 값 12개(3상태×4)를 미리 에셋에 박으면
unit 15 에서 스키마가 흔들릴 때 그게 마이그레이션 대상이 된다(제약 8 «나중을 위한» 금지).

3번을 월드 Y 로 밀면 안 된다 — 대상까지의 **시야 깊이**(카메라 정면 축 거리)가 같이 변해
판의 크기가 흔들리고, 화면비마다 다른 그림이 된다(절대 거리 저작이 무너진 unit 9 와 같은 함정).
로컬 up 은 정면 축과 직교하므로 깊이를 보존한다 — 대상은 자리만 옮기고 판의 크기는 그대로다.
(유클리드 거리는 √(d²+u²) 로 조금 늘어난다. 보존되는 것은 깊이이고, 화면 크기를 정하는 것도
그쪽이다 — 이 구분을 흐리면 테스트 단언이 틀린 것을 재게 된다.)

## 완료 기준

- `SolveStatePose` EditMode 테스트:
  - `screenY = 0.5` 면 대상이 뷰포트 정중앙(카메라 정조준)에 온다.
  - `screenY` 를 바꿔도 **대상까지 시야 깊이와 판의 화면 크기가 불변**이다.
  - 같은 레시피를 16:9 와 19.5:9 로 풀면 **대상의 뷰포트 y 가 같다**(구도 화면비 독립).
  - `fitToBoard` 일 때 보드 네 코너가 전부 프러스텀 안에 들어온다.
- 회귀 기준값: `pitch 47 / fitMargin 1.0058 / screenY 0.5543 / fov 35.9834`, 보드 21×12,
  16:9 → 카메라 `(0, 15.849, -15.860)`. 2026-08-21 사용자가 손으로 잡은 전투 포즈와 일치한다.
- **레시피가 없는 상태로는 전환하지 않는다** — 구 경로의 `FindPhasePose` null → hold 가
  은퇴하므로(unit 11) 그 자리를 대신할 규칙이 필요하다. 함수는 null 레시피에 실패를 반환하고
  호출부가 «현재 포즈 유지» 로 처리한다.
- 이 유닛만으로는 게임 동작이 바뀌지 않는다(타입·함수 추가뿐). 컴파일 + EditMode 초록.
