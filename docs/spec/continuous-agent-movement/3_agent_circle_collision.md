# unit 3 — 원형 에이전트 충돌 + 벽 슬라이드

## 목적

**격자가 눈에 보이는 가장 큰 원인을 제거한다.**

현행 `MovementCellTrim.Apply` 는 유닛을 **점(point)**으로 보고, 다음 위치가 벽 셀이면 현재 셀 경계로 clamp 한다. 그래서 벽에 비스듬히 부딪히면 미끄러지지 않고 **그 축이 통째로 막히고**, 코너에서 걸린다.

이것을 **반지름 r 의 원 vs 벽 타일 AABB** 충돌 + **접선 슬라이드**로 바꾼다. 맵은 여전히 격자로 저작되지만 충돌은 연속이다 — 브롤스타즈류의 "벽을 따라 스르륵 미끄러지는" 감각의 실체가 이것이다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Movement/AgentCollision.cs`
- 신규: `Assets/_Project/Tests/EditMode/AgentCollisionTests.cs`
- 수정: `Assets/_Project/Scripts/Battle/Movement/PathFollowState.cs` — `radius` 추가
- 수정: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — `Apply` 5곳 → `Resolve`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 반지름 knob + 스폰 시 주입

## 구현

### `AgentCollision.Resolve(current, desired, radius, in NavGrid) → float3`

**축 분리 해결**이다. X 를 먼저 해결하고, 그 결과 위치에서 Z 를 해결한다.

```
x = ResolveAxis(current.x, desired.x, atZ: current.z, ...)
z = ResolveAxis(current.z, desired.z, atX: x, ...)
```

이 순서가 슬라이드를 공짜로 만든다 — 벽에 막힌 축만 멈추고 자유로운 축은 계속 간다. 한 축씩 보므로 "막혔다"의 판정도 단순하다.

각 축은 **전진 방향의 원 가장자리**(`to ± r`)가 들어가는 셀 열/행을 보고, 그 안에 막힌 셀이 있으면 타일 경계 바로 앞에 세운다. 원이 걸치는 반대축 범위(`at ± r`)를 모두 검사해야 모서리를 통과하지 않는다.

### 안전장치

- **되돌아가지 않는다**: 결과는 항상 `[from, to]` 구간 안으로 클램프한다. 이미 벽에 겹쳐 스폰된 유닛(외력·텔레포트)이 뒤로 튕겨 나가는 사고를 막는다. 그런 유닛은 제자리에 머문다.
- **터널링**: 이 해결은 "한 프레임에 최대 인접 셀" 을 전제한다. 기존 `MovementCellTrim.ClampDisplacement`(0.9타일 상한)가 그 전제를 계속 지킨다 — **호출 순서를 유지한다**.
- **skin**: 경계에 정확히 붙으면 다음 프레임에 `IsBlocked` 가 흔들린다. `kSkin` 만큼 띄운다(기존 `kBoundaryEpsilon` 과 같은 성격).

### 반지름 공급

`PathFollowState.radius` (Movement 소유, per-agent). 값은 `BattleBridge` 의 `[SerializeField] agentRadiusTiles = 0.35f` 에서 스폰 시 주입한다.

- **0.35 근거**: 지름 0.7 < 1.0 이라 1타일 복도를 통과하고, 벽까지 여유 0.15 가 남는다.
- 하드코딩이 아니다(제약 6) — `tileSize`·`spawnHeight`·`bossLeap*` 과 같은 층의 knob 이다.
- 컴포넌트에 필드를 둔 것은 나중에 유닛별로 달라질 여지를 **미리 만들지 않고도** 열어두기 때문이다. 지금은 전원 같은 값이 들어간다.
- `radius <= 0` 이면 **기존 점 충돌과 동일**하게 동작한다(폴백). 픽스처 보호 + 회귀 시 즉시 되돌릴 수 있는 스위치.

### `MovementCellTrim.Apply` 의 운명

`Apply` 는 남긴다 — `radius = 0` 경로가 곧 `Apply` 의 의미이고, `AgentCollision` 이 그 경우를 위임한다. 중복 술어가 되지 않도록 **구현을 한 곳에만** 둔다.

## 완료 기준

- [ ] compile 통과 (콘솔 에러 0)
- [ ] `AgentCollisionTests` — 정면 충돌 정지 / **비스듬한 충돌은 접선 방향으로 계속 이동(슬라이드)** / 코너 통과 / 벽 겹침 시 뒤로 안 튐 / `radius=0` 은 기존 clamp 와 동일
- [ ] EditMode 실패 0
- [ ] Play 육안: **벽에 비스듬히 부딪힌 적이 미끄러지는가**, 코너에서 걸리지 않는가, 1타일 복도를 통과하는가
- [ ] `ecs-reviewer` 통과

## 주의

이 unit 은 경로를 바꾸지 않는다. 적은 여전히 4방향 flow 를 따라간다 — **코너 품질만** 바뀐다. L 자가 사라지는 건 unit 4, 직선 이동은 unit 7 이다. Play 확인 시 그 이상을 기대하면 "효과 없다"고 오판하기 쉽다.

---

**완료 기준 확인**: (미확인)
