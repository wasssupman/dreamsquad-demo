# Enemy Unit Development Spec

**작성일**: 2026-04-30
**상태**: **완료 2026-04-30** — 구현/EditMode 통과. PlayMode 밸런스/시각 확인은 후속 필요. 인계는 `0_handoff_summary.md` 참조.
**목표**: 기존 적 3종(`Basic`/`Swift`/`Tanker`) 위에 신규 적 3종을 추가하고, 적 공격 유닛이 방어 유닛과 동일한 projectile/Spine 비주얼 인프라를 사용하도록 정합성을 회복한다. `AttackUnitData` 를 `DefenderUnitData` 와 같은 수준으로 끌어올리는 작업.

## 형식 참고

본 spec 은 retroactive 로 정리된 인계 문서다. `0_..N_` 작업 단위 파일은 따로 만들지 않았고, 구현 흐름과 검증/리스크는 모두 `0_handoff_summary.md` 에 모았다. 이후 본 spec 영역에 추가 작업이 생기면 `1_{topic}.md` 부터 정상 작업 단위로 잇는다.

## 구현 결과 요약

- 신규 적 3종 추가: `Rootcaster` (장거리 투사체 + 공격 후 1초 pause), `Needler` (이동 중 빠른 투사체 연사), `Runner` (초고속 이동, 공격 없음).
- 적 전용 projectile data 2종 (`Projectile_Enemy_RitualBolt`, `Projectile_Enemy_Needle`).
- `Enemy_Tanker` 는 BellKnight Spine 으로 전환.
- 방어/공격 유닛이 공통 Spine 렌더링 경로를 사용 — `SpineUnitView` / `SpineUnitPool` 로 통합 (legacy `SpineDefenderView/Pool`, `SpineAttackUnitView/Pool` 삭제).
- `AttackUnitData` 가 `ProjectileData` + `movePauseOnAttackSec` + Spine 필드 + `ISpineUnitVisualData` 구현 보유.
- `EnemyAttackMovePause` 컴포넌트로 적 공격 후 이동 정지 동작 구현 (이후 `modifier-legacy-migration` Unit 3 에서 Movement 맥락으로 ownership 이동, queue 경유로 갱신).
- `WaveA` 의 `attackUnitPool` 에 신규 3종 포함.

## 공통 원칙

- **방어/공격 비주얼 통합**: Spine 렌더링은 `SpineUnitView` / `SpineUnitPool` 한 갈래만 사용. defender/enemy 전용으로 다시 분리하지 말 것.
- **Projectile 인프라 공유**: `AttackUnitData.projectile` 은 `DefenderUnitData.projectile` 과 같은 `ProjectileRef` 인프라 (이후 `modifier-legacy-migration` Unit 0/1 에서 `outputs[]` 로 통일됨).
- **EnemyAttackMovePause 단일 책임**: 공격형 적의 공격 직후 이동 제어만 담당. 일반 CC (`CcEffect`) 와 섞지 말 것. 현재 write owner 는 Movement 맥락 (`MovementPauseRequestDrainSystem`).
- **DraftView missing script**: 본 spec 작업 범위 밖. 건드리지 않음.

## 비목표

- 적 공격 시스템의 시각 이펙트 신설 (현재는 defender projectile prefab 재활용 + tint/scale 만 조정).
- `WavePatternGenerator` 의 unit weight 지원.
- 적 Spine attack animation 트리거 연결 (`UnitAttackVisualEvent` 일반화 작업).

## 후속 후보

본 spec 의 후속 후보는 `docs/spec/README.md` 하단 **Follow-up Backlog** 의 "Enemy 콘텐츠 / 비주얼" 테마 서브그룹에 있다. PlayMode 검증 잔여는 본 spec 의 `0_handoff_summary.md` "남은 작업 / 리스크" 섹션 참조.
