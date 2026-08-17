# 0 — 적의 실제 진행 방향을 기록한다 (Movement)

## 목적

넉백 방향의 입력을 만든다. 「적이 지금 어느 쪽으로 가고 있나」를 **관측해서 기록**한다 —
다른 맥락이 흐름장을 뒤져 **재유도하지 않게** 하는 것이 요점이다.

재유도가 왜 안 되는지: 이동 방향을 결정하는 경로가 하나가 아니다.

| 적 | 방향의 출처 |
|---|---|
| 일반 지상 적 | 흐름장(목적지 × 통행층 슬롯) + 평활화(string pulling) |
| 비행 적 | 웨이포인트 |
| 어그로된 적 | 추격장(`chase` dist 하강) |
| 고립 셀의 적 | 복구 방향(`FlowRecovery.RecoveryDir`) |

넉백은 「그 칸의 기본 흐름(PrimarySlot)」을 읽고 있었다. 위 표의 아래 세 줄에서 전부 틀린다.
**대공 유닛의 주 표적이 비행 적**이라 이 어긋남은 기능의 핵심에서 나는 형태였다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/PathFollowState.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현

`PathFollowState` 에 `float2 lastMoveDir` 추가. Movement 소유 쓰기 · 다른 맥락은 RO.

**새 컴포넌트를 만들지 않는다** — `PathFollowState` 는 이미 움직이는 모든 주체에 붙어 있고,
바로 위 `holdingGround` 가 같은 종류의 관측 기록이라 값이 살 자리가 여기로 정확하다.

기록 지점은 `holdingGround = 0` 을 쓰는 **그 두 곳 그대로**다:

1. 추격 분기 — `math.normalize(chaseDir)` (게이트가 길이 > 1e-6 을 이미 보장)
2. 주 분기 — `math.normalize(flowStep.xz)`, `lengthsq(flowStep) > 1e-12` 안에서

`flowStep` 은 웨이포인트·흐름장·평활화·복구를 **이미 거친 최종 자기주도 변위**다. 그래서 이
한 줄이 어느 경로로 결정됐든 실제 진행 방향을 잡는다.

### 규율 — 케이스를 열거하지 않는다

`holdingGround` 주석이 세워둔 규율을 그대로 따른다: 「자기주도 변위를 **실제로 적용하는
지점에서만**」 쓴다. 새 `continue` 경로나 새 이동 분기가 생겨도 자동으로 편입된다.
분기를 열거하는 방식이면 분기가 늘 때마다 조용히 샌다.

### 멈춘 프레임에는 갱신하지 않는다

의도적이다. 교전 중 정지한 적(Standoff)도 **직전 진행 방향을 유지**해야 뒤로 밀 수 있다.
0 = 한 번도 움직인 적 없음(스폰 직후 한 프레임 · 합성 픽스처 · 고정 구조물)이고,
소비자는 0 을 「방향 없음 = 밀지 않음」으로 읽는다.

## 완료 기준

- [x] compile 에러 0
- [x] EditMode 코어 회귀 0 (이동/분리/경로 테스트 전부 유지)
- [x] 소비자(unit 1)가 비행 적·추격 중인 적에서도 옳은 방향을 받는다

확인: 2026-08-17 · EditMode 2318 통과 / 0 실패
