# 0 — 결정론 스폰 분산 (③)

> rev 2026-06-29: 연속(golden-ratio) → **이산 N-레인 round-robin** (사용자 의도: "또렷한 N줄"). 분배 로직은 동일(스폰 순서→폭 안 측면 위치), granularity·순서만 이산·주기로 변경.

## 목적

스폰 측면 오프셋의 RNG(`_spawnSpreadRng`)를 제거하고 **결정론 이산 N-레인**(폭 중앙 기준 대칭, 스폰 순서 round-robin)으로 대체한다. 구조적 결정성(같은 입력 → byte-identical)을 확보하고, 또렷한 N줄 대형으로 분산한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs` — `LaneFraction(int index, int laneCount, float spreadFraction, float topScale)`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_spawnSpreadRng`(Random) 제거 → `_spawnSpreadCounter`(int, 맵 빌드 시 0 리셋). serialized `spawnSubLaneCount`(기본 3). `ComputeSpawnLateralOffset` 가 `LaneFraction(_spawnSpreadCounter++, spawnSubLaneCount, …)` 호출.
- `Assets/_Project/Tests/EditMode/SpawnSpreadTests.cs` — `LaneFraction` 회귀 6종.

## 구현

- 레인 위치: 폭 중앙(0) 기준 대칭, `[−fraction, +fraction·topScale]` 안에 N개 균등. `lane = index % N`(round-robin), `s = lane/(N−1)·2−1` (−1…+1), `perp = s≥0 ? s·half·topScale : s·half`.
  - 예) N=3, topScale=1 → {−0.2, 0, +0.2}. topScale=0.5 → {−0.2, 0, +0.1}(상단만 압축, 중앙 레인은 정확히 0).
  - N=1 → 중앙 단일 레인(0).
- RNG 없음 → 같은 index → 같은 레인(주기 N). 연속 스폰은 다른 레인(anti-stack). 같은 레인은 N 간격(시간차 스폰).
- `index`는 스폰 순번(맵 빌드마다 0 리셋). 셀은 perp 축 방향만 결정.
- `|offset| < 0.5·tile` 불변식: `half = clamp(fraction, 0, 0.49)` + `LateralOffset` clamp.
- **unit 1 deadband 와의 관계**: 레인 오프셋 ≤ deadband(0.25)면 직진에서 recenter 미적용(보존). 0.25 초과 오프셋은 직진에서도 0.25 로 캡됨 → 레인 폭 상한 ≈ ±0.25·tile(기본 0.2 는 여유).

## 완료 기준

- compile 0 에러.
- EditMode `SpawnSpreadTests` 전체 green (`LaneFraction` 6종: N=1 중앙 / N=3 대칭 / N=3 topScale 상단압축 / round-robin 주기 / 연속 index 다른 레인 / 셀 불변식).
- 기존 `FractionRange`/`Perpendicular`/`LateralOffset` 무회귀.
- Play 스폰 시 적이 또렷한 N줄로 분산, 한 점 적층 없음(육안).

---

확인: 2026-06-29 · 커밋 `6f17120`(초기 연속) · EditMode 15/15.
rev 확인: 2026-06-29 · 연속 golden-ratio → 이산 N-레인 round-robin · compile 0 · EditMode 26/26 (이 rev 커밋).
