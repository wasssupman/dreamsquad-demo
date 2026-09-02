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

- [x] `InBodyReachWithHalfExtent`·`BodyShape`·`BodyExtent` 참조 0 (grep).
- [x] 적 → 2×2 방어유닛 판정에 내접원(1.0)이 들어간다 — 파생식 테스트 + long_boss 귀속으로 실측.
- [x] EditMode 전건 초록(선행 실패 2건 제외).
- [x] 골든 재베이크 + **귀속 기록** — 아래 진행 기록 표.

---

### 진행 기록 — 구현·검증 2026-09-01

units 12·13·14 가 한 번의 재베이크로 검증됐다(구현 순서상 분리 측정 불가 — 대신 카나리아로 가른다).

- **EditMode 2694건 중 선행 실패 2건(bomb_man·boomerang 문안, 시트 소관) 외 전건 초록.**
  잡힌 회귀 1건: 스킬 멤버십의 `/_tileSize` 가 흐름장 없는 월드에서 0/0=NaN → 광역 전멸
  (`DeadCasterFactionTests` 3건). 역수 가드 관용구로 수정(`9372b713`).
- **골든 8/8 재생성 + 귀속** (기준선 = unit 10 재베이크 `c587e15b`):

| 시나리오 | 킬 전→후 | 읽기 |
|---|---|---|
| `no_defense` | 0→0, **이벤트 바이트 동일**(configHash 만) | 방어유닛 무개입 축 무회귀 카나리아. 적 몸 티어 스냅(unit 13)이 값 무변임의 실측 증거 |
| `basic` | 4→**5** | 실효 도달 +0.25 — 더 일찍·넓게 때린다 |
| `force_wave` | 9→**14**, 유출 1→**0** | 〃 (화력 증가로 유출 소멸) |
| `long_boss` | 25→**21**, 이벤트 급감 | 반대편 귀결 — 방어유닛도 더 잘 맞는다(대상 몸 0.75 둥근사각 → 1.0 원). 장기전에서 라인이 일찍 무너진다. 계약 5 rev 의 예고 그대로 |
| `seed_b`·`seed_c` | 9→8 | 소폭. 두 시드 동형 유지 |
| `summoner`·`restart` | 무변 | — |

- configHash 전건 갈림 = 스키마 확장/축소(방어유닛 `bodyRadius` 은퇴 + 적 `bodySize` 추가) —
  unit 3 과 같은 세 번째 범주. `force_wave.finalStateHash` 플레이키는 선재(unit 10 기록) 그대로.
- **밸런스 방향이 계약 5 rev 와 일치**하는 것까지가 이 unit 의 검증이고, 수치 조정(보스전
  라인 붕괴 완화 등)은 unit 6(웨이브 knob 재튜닝) 소관으로 넘긴다.
