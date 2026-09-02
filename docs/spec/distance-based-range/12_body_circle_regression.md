# 12 — 몸을 원으로 회귀 (외부 세션 확정 이행 · sim)

> **외부 세션 확정 2026-09-01**: 몸 = 원, 사각 몸 안(案) 폐기. 판정 = edge-to-edge
> `d² ≤ (사거리 + selfR + targetR)²`. 아군 selfR = **min(W,H)/2 내접원 파생식**(저작 없음).
> README 계약 1 rev 3 · unit 9/10 rev 참조.

## 목적

unit 10 PR2 가 세운 box⊕disc 몸을 **원 하나**로 되돌리고, 아군 몸을 저작(b=0.25)에서
footprint 파생식으로 바꾼다. 판정·그림자·링·아트가 반경 하나에서 유도되는 기반이다.

## 변경 대상

- `Scripts/Skills/SkillMath.cs` — `InBodyReachWithHalfExtent`·`SelfHalfExtentTiles` 은퇴.
  소비처는 `InBodyReach`(원 전용, **기존 함수**)로. `StandardBodyRadiusTiles = 0.25` 는 존치
  (링·프리뷰의 「표준 소형 상대」 = 티어 소).
- `Scripts/Battle/Combat/AttackReach.cs` — `BodyShape` 은퇴, 반경 인자 시그니처로 회귀
  (1×1 특수화 오버로드가 이미 그 모양이다).
- `Scripts/Battle/Units/BodyExtent.cs` — 컴포넌트 은퇴(bake 2곳 포함). `HitRadius` 만 남는다.
- `Scripts/Data/DefenderUnitData.cs` — `bodyRadius` 저작 필드 은퇴 →
  `BodyRadiusTiles => min(W,H)/2` 파생 프로퍼티. `StructureData` 도 같은 식(3×3 거점 = 1.5).
- `Scripts/Battle/Combat/AttackSystem.cs` — `ShapeOf` → 반경 조회.
  **랭킹(`DistanceSqToTarget`)의 최근접-점유-칸 경로 은퇴** — 게이트와 랭킹이 같은 몸
  (중심 거리)을 본다. 방어유닛 `OccupiedCellsBuffer` 소비처가 0 이 되면 bake 도 제거.
- `Scripts/Bridge/BattleBridge.cs` — 방어유닛 `HitRadius` bake = 파생식. 링 공급값
  halfExtent = 0(셰이더는 코드 무변 — 0 이면 원).
- `Shaders/PlacementRangeRing.shader` — 주석 이력만 갱신(「원 셰이더 금지」 폐기 기록).
- 테스트 — `AttackReachTests` 원 기준 개정.

## 구현

- 술어는 이미 있다: `SkillMath.InBodyReach(dx, dz, range, selfR, targetR)`. 사각 오버로드
  소비처를 이것으로 바꾸는 것이 본체다.
- ⚠ **밸런스가 의도적으로 움직인다**(계약 5 rev): 1×1↔소형 적 도달 R+0.5 → R+0.75(전방향
  +0.25) · 2×2 축방향 +0.25·대각 +0.04 · 2×3 단축 +0.25·장축 −0.25. 리베이스 없음.
- ⚠ BC1055 우회(`float2` 반폭 인자)는 사각 항과 함께 자연 소멸한다 — 원 술어는 스칼라뿐이다.

## 완료 기준

- [ ] `InBodyReachWithHalfExtent`·`BodyShape`·`BodyExtent` 참조 0 (grep).
- [ ] 적 → 2×2 방어유닛 판정에 내접원(1.0)이 들어간다 — unit 10 완료 기준의 원 재정의분.
- [ ] EditMode 전건 초록(선행 실패 2건 제외).
- [ ] 골든 재베이크 + **귀속 기록** — 킬 변화가 「아군 몸 확대 +0.25」로 설명되는지 diff 를 읽는다.
