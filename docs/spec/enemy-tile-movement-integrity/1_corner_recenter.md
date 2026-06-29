# 1 — 코너 복원 (②)

## 목적

코너에서 적이 셀 안쪽 엣지(측정 |perp| 0.29~0.49)에 고정돼 이동타일 밖처럼 보이는 것을, `target=0 + dead-band` 측면 복원으로 해소한다. 직진 구간의 스폰 분산은 보존한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/LateralRecenter.cs` (신규) — 순수 헬퍼.
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — flow branch 에 통합. zero-flow recovery 스킵 플래그.
- `Assets/_Project/Tests/EditMode/LateralRecenterTests.cs` (신규) — 6종.

## 구현

- perp = 진행방향(stepDir) 수직 성분(셀 중심선 대비). `|perp| > deadband` 일 때만 `min(rate·dt, |perp|−deadband)` 만큼 0 쪽으로(밴드 가장자리까지). **안쪽 이동만** → 벽 침투 없음, 기존 cell-trim 무해.
- `deadband = DeadbandFraction(0.25)·tile`, `rate = RateK(0.4)·유효속도`. 내부 이동-품질 상수(게임플레이 값 아님 → `MovementCellTrim.kBoundaryEpsilon` 선례. 싱글톤/serialized 불필요). 코너 정착점 = deadband.
- **zero-flow recovery 분기는 스킵**(이미 교정 이동 중). **임펄스 측면성분은 이 프레임 보존**(recenter 는 current 기준 standing 오프셋만 당김) → 넉백은 이후 프레임에 점진 복귀(원하는 회복).
- cell-trim **전에** desired 에 합산 → 기존 벽 clamp 경로 그대로.

## 완료 기준

- compile 0 에러.
- EditMode `LateralRecenterTests` 6종 + `SpawnSpreadTests` 무회귀 green.
- Play 코너 통과 시 perp 가 deadband(≈0.25·tile) 이하로 수렴, 직진 분산 보존(unit 3 통합 검증). deadband 가 시각적으로 여전히 엣지처럼 보이면 unit 3 에서 상수 하향 검토(분산 약간 압축 감수).

---

확인: 2026-06-29 · 커밋 `be1d950` · compile 0 · EditMode 21/21.
Play 측정(20×10 맵, 코너 4셀): 적 perp **최대 0.250(=deadband)**, 엣지-허깅 밴드(0.29~0.49) **0건**. 코너 통과 적이 셀 엣지(0.49) 대신 deadband(0.25)에 정착 — 스폰 분산(≤0.2)은 보존(밴드 안 무손). 정착점 0.25 가 여전히 엣지스러우면 unit 3 에서 `DeadbandFraction` 하향 검토.
