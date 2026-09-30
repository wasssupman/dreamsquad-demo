# 7 — 브레스 콘 도달을 정본 자로 (코어 · 제약 13)

## 목적
드래곤 화염 브레스(`ConeBreathSkill`)만 도달을 자기 자로 잰다. 후보는 이미 정본(`Opponents` × `RangeMetric.SelfArea` = 사거리 + 드래곤 몸 + 대상 몸)으로 뽑는데, `SkillCone.IsInCone` 이 그 결과를 **몸 없는 중심 거리**로 다시 자르고 각도도 대상 **중심점**으로만 본다. 다른 방향 도형(브루저·말파이트 부채꼴 = `AttackReach` → 원 AND `SkillMath.SectorGate`)과 규칙을 맞춘다. **사용자 결정 2026-09-28**(「다른 모든 것들과 규칙을 맞춰」) — 도달이 넓어지는 규칙 변경을 승인.

## 변경 대상
- `Scripts/Skills/Concrete/ConeBreathSkill.cs` — 길이 컷 제거(후보 질의가 정본 원) · 각도는 `SkillMath.SectorGate`(대상 몸 걸침 · 칸 단위 · 발사 자리 기준).
- 반각 표현: `SectorGate` 는 `(sin, cos)` 를 받는다. 저작 → bake 1회 변환(`AttackShapeBake` 선례)으로 싣는다. `ConeCosSq` 를 대체할지 병행할지는 구현 판단 — 소비처 0 이 되는 필드·함수(`SkillCone.IsInCone` 등)는 지운다(제약 8). `SkillCone.SameSpotEpsSq` 는 `AttackReach` 가 쓴다.
- 반각 ≥ 90° 거절(bake) 은 유지. 반각 ≤ 0° 도 같은 자리에서 거절한다(아래 완료 기록의 후속 — `SectorGate` 의 정의역 밖).
- 테스트: `Tests/EditMode/` 의 브레스 스킬 테스트 · 코어 테스트 · 탐침/위트니스 중 브레스 행.

## 구현
- 형 = **몸에서 나오는 것**(브레스는 드래곤 몸에서 뻗는다) → 원점 항 = 드래곤 몸, 대상 몸 항 포함.
- 같은 자리(겹침) = 포함 — `SectorGate` 가 중심 안이면 참.

## 완료 기준
- 새 단언: 몸 큰 적이 사거리 끝에 몸만 걸치면 맞는다 · 부채꼴 가장자리 밖에 중심이 있어도 몸이 걸치면 맞는다 · 등 뒤는 몸 반경을 넘으면 안 맞는다.
- 헤드리스 `verify-fresh-skills.sh` · Unity EditMode 3 어셈블리(선행 외 0 — 아트 1건 포함 3) · 골든 11(드래곤이 코퍼스에 있으면 바뀔 수 있다 — 바뀌면 트레이스로 원인을 이 변경으로 확인 후 재베이크) · `BindingBakeSnapshotTests`(드래곤 줄이 바뀌면 의도 확인 후 갱신).

- 완료 2026-09-28 · `bc8c4f61b`(구현) · `31179517c`(굽기 스냅샷 2종 — 반각 표현만 1:1 변경) — 헤드리스 981/0 · Unity EditMode 3 어셈블리 2620 중 실패 = 선행 2 + 카드 아트 1(개사기·별똥 타격 전용 아트 대기) · 골든 11 일치. 후속: 반각 0° 저작 시 `SectorGate` 가 축 위 등 뒤를 참으로 읽는다(라이브 0) → 굽기가 반각 ≤ 0° 를 ≥ 90° 처럼 거절한다.
