# 0 — 도형 게이트 술어 (`SkillMath`)

## 목적

부채꼴·직사각형 **게이트** 진입점 2개를 `Wassup.Skills.SkillMath` 에 신설한다. 반경 판정(`Reach`)은
건드리지 않는다 — 게이트는 그 **뒤에 AND 로 곱해지는 항**이다. 이 unit 만으로 컴파일·판이 성립해야
하고(소비처 0), 라이브 동작은 무변이다.

## 변경 대상

- `Skills/SkillMath.cs` — `SectorGate` · `LaneGate` 신설 (이름은 구현 시 확정, 형태만 계약)
- `Combat/AttackReach.cs` — 타일 변환 래퍼 2개 (`InSector` · `InLane`). `float3`→타일 나눗셈만
- `Tests/EditMode/AttackShapeGateTests.cs` 신규

## 구현

입력은 전부 **타일 단위**: `Δ = (dx, dz)` 대상−원점 · `u = (ux, uz)` 주 대상 방향(**정규화**) ·
`tr` 대상 몸 반경.

### 부채꼴 — `SectorGate(dx, dz, ux, uz, sinHalf, cosHalf, tr)`

전체각 A ≤ 180° 이면 반각 θ ≤ 90° 이고 부채꼴의 각 영역은 **두 반평면의 교집합**(볼록)이다.

```
eL = rot(u, +θ) · eR = rot(u, −θ)              ← 가장자리 광선. rot 은 (sinθ, cosθ) 곱셈 4번
nL = rot(u, θ+90°) = (−ux·sinθ − uz·cosθ,  ux·cosθ − uz·sinθ)   ← 바깥 법선
nR = rot(u, −θ−90°) = (−ux·sinθ + uz·cosθ, −ux·cosθ − uz·sinθ)
sL = dot(Δ, nL) · sR = dot(Δ, nR)

sL ≤ 0 && sR ≤ 0                → 중심이 안                → true
sL > 0 && dot(Δ, eL) ≥ 0        → 왼 가장자리에 투영        → sL ≤ tr
sR > 0 && dot(Δ, eR) ≥ 0        → 오른 가장자리에 투영      → sR ≤ tr
그 외(꼭짓점 뒤 영역)           → |Δ|² ≤ tr²
```

= 볼록 쐐기의 SDF ≤ tr. **꼭짓점 뒤 영역을 `max(sL, sR)` 로 근사하지 말 것** — sinθ 배만큼
관대해져 등 뒤 인접 적이 새어 든다(θ=45°·tr=0.5 에서 0.7 뒤의 적이 통과). 반경 항은 `Reach` 가
AND 로 막으므로 여기엔 없다.

### 직사각형 — `LaneGate(dx, dz, ux, uz, halfWidth, lengthTiles, tr)`

`u` 를 축으로 한 국소 좌표: 전방 `l = dot(Δ, u)` · 측면 `s = |ux·dz − uz·dx|`.
상자 = `l ∈ [0, L]` · `s ≤ halfWidth`, `L = 사거리 + 원점 몸`(축 위에서 `Reach` 와 정확히 일치).

```
vl = max(|l − L/2| − L/2, 0) · vs = max(s − halfWidth, 0)
vl² + vs² ≤ tr²
```

= `BodyOverlapsSquare` 와 같은 상자 SDF 를 **회전한 프레임**에서, 반폭 둘로. 축을 벗어난 먼
모서리는 `Reach` 의 원이 추가로 깎는다(README 「의도」).

### 공통

- `u` 가 영벡터(`lengthsq < SameSpotEpsSq`) 이면 **호출부가 게이트를 건너뛴다** — 술어는 정규화 `u` 를
  요구하고 검사하지 않는다(`SkillCone` 과 같은 책임 분할).
- `Unity.Mathematics` 를 안 쓴다 — 이 어셈블리는 `float` 산술과 삼항으로 끝난다(4a 선례).
  `Unity.Burst` 참조는 이미 있다(4a 진행 기록 (1) — 없으면 BC1055 로 런타임에 무너진다).

## 완료 기준

- [ ] **주 대상 항등**: `Reach` 안의 임의 Δ 에 대해 `u = Δ/|Δ|` 이면 두 게이트 모두 true (README 계약 2).
      각도·폭·몸 반경을 격자로 훑는 property 스타일 단언 1건.
- [ ] 부채꼴: 축 위 in · 가장자리 각 정확히 θ 에서 몸이 걸치면 in(tr>0)/점이면 경계 in · θ+ε 밖에서
      몸이 안 닿으면 out · **등 뒤(−u) 거리 > tr 에서 out**(꼭짓점 뒤 근사 금지의 회귀) · 회전 불변
      (Δ,u 를 같이 돌려도 답 동일) · θ = 90°(A=180) 에서 반평면과 일치.
- [ ] 직사각형: 측면 `halfWidth + tr` 경계 · 뒤(`l < −tr`) out · 축 위 `l ≤ L + tr` 이 `Reach` 와 일치 ·
      `halfWidth = 0` 도 유효(몸이 축에 걸치면 in).
- [ ] NaN/무한 0 — 입력에 0 벡터·0 반경 조합을 넣어도 bool 만 나온다.
- [ ] `ReachEntryPointGuardTests` 초록 유지(새 함수가 `CellShapePaddingTiles` 를 안 읽는다).
- [ ] EditMode 코어 lane 전건 초록(선행 실패 2건 제외). 라이브 sim 무변(소비처 0).
