# 0 — 도형 게이트 술어 (`SkillMath`) + `AttackReach` 도형 인자

## 목적

+X 고정 부채꼴·띠 게이트를 `Wassup.Skills.SkillMath` 에 신설하고, `AttackReach.InReach/InCellReach` 가
**도형을 인자로 요구**하게 바꾼다. 반경 판정(`Reach`)은 무변 — 게이트는 AND 항이다.
이 unit 은 소비처 11곳을 **Omni 로 호출부만 갱신**해 컴파일·판이 성립하게 한다(동작 무변). 실제 도형
전달은 unit 2.

## 변경 대상

- `Skills/SkillMath.cs` — `SectorGateX` · `BandGateX` 신설(이름은 구현 시 확정)
- `Combat/AttackReach.cs` — `InReach(..., in AttackShapeBaked shape, int side)` · `InCellReach(...)` 동형
- `Combat/AttackShapeBaked.cs` 신규 — unmanaged struct(`kind, sinHalf, cosHalf, halfWidth`), `Omni` static
- 소비처 11곳 — `AttackShapeBaked.Omni, side: 0` 으로 기계적 갱신(unit 2 가 실값으로 교체)
- `Tests/EditMode/AttackShapeGateTests.cs` 신규

## 구현

입력은 **타일 단위**. 도형은 항상 **+X 방향**이다 — 호출부가 `side` 로 dx 부호를 접어 넘긴다:
`along = side == 0 ? |dx| : side·dx` (0 = 양쪽 합집합 = 획득, ±1 = 한쪽 = 부가 타격). `across = dz`.

### 부채꼴 — `SectorGateX(along, across, sinHalf, cosHalf, tr)`

전체각 A ≤ 180° → 반각 θ ≤ 90° → 볼록 쐐기. `b = |across|`.

```
along·sinθ ≥ b·cosθ                       → 중심이 안                 → true
along·cosθ + b·sinθ ≥ 0 (가장자리에 투영)  → 거리 = b·cosθ − along·sinθ → ≤ tr
그 외(꼭짓점 뒤)                            → along² + b² ≤ tr²
```

= 쐐기 SDF ≤ tr. **꼭짓점 뒤를 가장자리 식으로 근사하지 말 것** — sinθ 배 관대해져 등 뒤 인접 적이 샌다.
θ = 90°(A = 180) 는 반평면 `along ≥ −tr` 로 퇴화 — 테스트로 고정.

### 띠 — `BandGateX(along, across, halfWidth, lengthTiles, tr)`

상자 = `along ∈ [0, L]` · `|across| ≤ halfWidth`, `L = 사거리 + 원점 몸`(축 위에서 `Reach` 와 일치).

```
va = max(|along − L/2| − L/2, 0) · vb = max(|across| − halfWidth, 0)
va² + vb² ≤ tr²
```

= `BodyOverlapsSquare` 를 반폭 둘 + 중심 오프셋으로 일반화. 회전 없음.

### 공통

- Omni(`kind == 0`)는 게이트 호출 자체를 건너뛴다 — 오늘 경로와 명령 수가 같다.
- `Unity.Mathematics` 안 씀(float 산술·삼항). `Unity.Burst` 참조는 이미 있다(4a 진행 기록 (1) — 없으면
  BC1055 로 런타임에 무너진다).
- `AttackReach.InReach` 의 새 인자에 **기본값을 주지 않는다**(계약 2). `selfBodyRadiusTiles` 와 같은 이유 —
  기본값이 있으면 새 호출부가 도형을 안 넘기고도 컴파일되고, 그 순간 소비처가 다른 답을 받는다.

## 완료 기준

- [x] 부채꼴: 축 위 in · θ 가장자리에서 몸 걸치면 in / 점이면 경계 in · θ+ε 밖 out · **등 뒤(−X) 거리 > tr 에서
      out** · `side = 0` 이 좌우 대칭(`(dx,dz)` 와 `(−dx,dz)` 같은 답) · A = 180 → 반평면 · `side = +1` 에서
      `dx < −tr` out.
- [x] 띠: `|dz| ≤ halfWidth + tr` 경계 · 뒤(`along < −tr`) out · 축 위 `along ≤ L + tr` 이 `Reach` 와 일치 ·
      `halfWidth = 0` 유효.
- [x] **Omni 항등**: 격자 점을 훑어 `InReach(…, Omni, 0)` == `SkillMath.ReachFromUnit`(구 본체) 전건 일치 — 1,521점.
- [x] NaN/무한 0. `ReachEntryPointGuardTests` 초록.
- [x] 소비처 11곳 Omni 갱신 후 EditMode 코어 lane 전건 초록 — **2648건 · 실패 0 · 신규 15건**(리그 배치 2026-09-12).
- [~] 골든 전건 초록 — **unit 2 로 이월.** `Verify Against Golden Corpus` 는 BattleScene Play 세션의 브리지를
      요구해 배치 리그로 못 돌린다(MCP 단절 상태). unit 0 은 전 소비처가 Omni 라 구조적 무변이고, 위 Omni 항등
      테스트가 그 증언을 대신한다. 동작이 실제로 바뀔 수 있는 unit 2 에서 돌린다.

---

### 진행 기록 — 구현 2026-09-12

- 신설: `SkillMath.SectorGateX/BandGateX` · `Combat/AttackShapeBaked`(kind 0 = Omni · 1 Sector · 2 Band) ·
  `AttackReach.InReach/InCellReach(…, in shape, side)` + private `ShapeGate`(두 진입점이 같은 본체).
  `TargetPersistence.KeepsLock` 도 도형을 받는다(side 0 · `h` 는 사거리에만).
- 소비처 갱신 11곳 + 테스트 6파일. `targetBodyRadiusTiles` 기본값도 함께 은퇴(뒤에 필수 인자가 오므로).
- ⚠ **`in AttackShapeBaked.Omni` 는 컴파일되지 않는다**(CS8156) — 프로퍼티 rvalue 에 명시 `in` 을 붙일 수 없다.
  호출부는 `in` 없이 넘긴다(컴파일러가 임시 사본을 만든다). 로컬 변수엔 `in` 유지. 12파일에서 한 번 넘어졌다.
- 리그: 공유 `wassup-testrig` 가 남의 WIP(`AttackState`·`AttackSystem`·브리지 — 이 spec 과 같은 파일)로 dirty 라
  `wassup-rig-das` 를 새로 팠다(APFS 클론). 끝나면 `git worktree remove --force`.
- `.meta` 2건은 메인 에디터가 살아 있어 직접 생성됐다(guid 고정, 커밋 포함).
