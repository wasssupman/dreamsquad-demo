# 4 — Handoff Summary (최종, units 0~3)

## Commit
- `6f17120` feat(spawn) unit 0 · `ee6f65d` docs
- `be1d950` feat(movement) unit 1 · `11374d1` docs
- `cfe04ec` feat(movement) unit 2 · `f619e46` docs
- unit 3 = 검증/문서(이 커밋). main 직접 커밋(프로젝트 관행). 무관 dirty 보존.

## Implemented (units 0~3)
- **리프레임**: `movement-lane-centering` 초안 → `enemy-tile-movement-integrity`. "레인 대형 시스템"은 명시적으로 폐기(후속 후보 II). **적 타일 이동 결함 3종 픽스**만.
- **① 결정론 스폰(unit 0, rev)**: `SpawnSpread.LaneFraction`(폭 중앙 대칭 이산 N-레인 round-robin, `spawnSubLaneCount` 기본 3) 로 `_spawnSpreadRng` 대체. RNG 0. (초기 golden-ratio 연속 → 사용자 의도대로 이산 N줄로 rev.)
- **② 코너 복원(unit 1)**: `LateralRecenter`(target=0 + dead-band 0.25·tile, rate 0.4·speed). flow 수직 성분이 밴드 밖일 때만 중심 쪽으로(가장자리까지). 직진 스폰 분산은 밴드 안이라 보존, 코너 엣지-허깅만 교정. zero-flow recovery 스킵, 임펄스 측면성분 보존.
- **③ aggro 타일 제약(unit 2)**: cell-trim 을 `MovementCellTrim.Apply` 로 추출 → flow·aggro 두 분기 공유. aggro 는 이동목표 변경(goal→guardian)뿐, cell-trim bypass 제거 → walk 타일 위에 머묾. 별도 사거리정지/stuck/return 코드 없음.
- **핵심 통찰**: 세 문제는 "target 으로의 이동이 유효 타일 경로 위에 머문다"의 단면. aggro-종료 복귀는 **unit 2 가 흡수**(타일 안 벗어났으니 flow 재개 = 복귀). unit 1+2 가 aggro 생애주기 전부 커버.

## Key Files
- `Battle/Movement/SpawnSpread.cs`(DeterministicFraction), `LateralRecenter.cs`(신규), `MovementCellTrim.cs`(Apply), `MovementSystem.cs`(flow recenter + aggro Apply 통합)
- `Bridge/BattleBridge.cs`(`_spawnSpreadCounter`)
- Tests: `Tests/EditMode/{SpawnSpread,LateralRecenter,MovementCellTrimApply}Tests.cs`

## Verified
- compile 0 · EditMode **25/25**.
- Play(2026-06-29): 타일이탈 0/12 · aggro 적 walk 셀 정착(Place 미진입) · 코너 perp ≤0.25(엣지-허깅 0). aggro-종료 복귀=합성, 결정론=구조적. 상세 `3_verify.md`.

## Notes (되돌리면 안 되는 의도)
- deadband/rate 는 **헬퍼 내부 상수**(게임플레이 값 아님; `kBoundaryEpsilon` 선례). 싱글톤/serialized 의도적 회피(최소 스코프). Play 튜닝 필요 시 승격.
- `MovementCellTrim.Apply` 는 flow·aggro **공유 단일 지점** — 한쪽만 고치면 불변식 깨짐.
- 코너 정착점은 deadband(0.25)지 0 아님(분산 보존 트레이드오프). 더 중앙으로 원하면 `DeadbandFraction` 하향(분산 약간 압축).
- aggro = 이동목표 변경뿐. 별도 로코모션/return 만들지 말 것.

## Follow-up
- **QuadUnit 뷰 누수** [S] — `QuadUnitViewPool` 가 엔티티 사망 시 Quad 뷰 미해제 추정(`3_verify.md` 측정). presentation 누수, sim 무관.
- **(II) 결정론 레인 대형 시스템** [L] — 폐기된 기능. product 가치 확인 후 별도 spec(README 후속 후보).
- **aggro 정식 경로탐색** [M] — 현재 greedy+cell-trim 근사. guardian 이 벽 뒤로 멀면 거칠 수 있음.
- **aggro-종료 라이브 데모** [S] — 합성 검증으로 갈음. 원하면 더미 guardian 으로 라이프사이클 재현 가능.
