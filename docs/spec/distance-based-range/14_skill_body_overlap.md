# 14 — 도형 스킬을 몸 걸침으로 (결정 4 폐기 이행)

> 외부 세션 확정 6: 도형 스킬 판정 = **SDF(대상 접지점, 도형) ≤ targetR** — 몸 걸침 = 히트,
> 자동 민코프스키 확장. 결정 4(2026-08-31)의 「스킬 광역 셀 유지」 절반을 폐기한다.

## 목적

드림캐쳐·액티브 스킬 광역이 대상의 몸을 본다. 오늘은 중심 대 중심(`EcsSkillContext.cs:445`)
이라 ① 몸이 범위에 절반 걸친 2×2 유닛이 안 걸리고 ② 다칸 유닛 중심의 셀 스냅
(`CellOfPosition`)이 불안정하다(unit 10 잔여 결함).

## 변경 대상

- `Scripts/Battle/Skills/EcsSkillContext.cs` — `Collect` 멤버십 재작성:
  원 도형(`Euclidean`) = `dist ≤ r + targetR` · 사각 도형(`Chebyshev`) = 사각 SDF ≤ targetR.
  SDF 는 `SkillMath` 에 순수 함수로(제약 10 — 링 셰이더의 `sdRoundedBox` 와 같은 식).
- `Scripts/Battle/Combat/TileAoe.cs` — 스킬 arm 소비처 처분(계약 6 rev).
- 계약 7 rev 이행 — 두 arm 모두 몸 포함. `Chebyshev` rename(`BodyDistance` 안) 재검토는 이때.

## 구현

- **targetR 는 unit 12·13 의 그 값이다** — 사거리와 스킬이 같은 몸을 본다(값 하나).
- 접지점 = 적은 시뮬 좌표 · 아군은 기하 중심(외부 세션 결정 5).
- 표기(칸 하이라이트)는 이번에 안 바꾼다 — 몸 걸침은 표시보다 **넓게** 맞는 방향이라 무통보
  관용과 동류. 도형 윤곽 표기는 후속 후보.
- ⚠ 저작 17종의 도형 자체(사각/원·반경 값)는 무변 — 멤버십 산식만 바뀐다.
- ⚠ 해저드 **존 틱**(`HazardShapeSampler` 셀 리스트)은 범위 밖 — 재설계급이라 후속(기본값
  결정 2026-09-01).

## 완료 기준

- [ ] 범위 경계에 몸이 반쯤 걸친 2×2 유닛이 버프를 받는다(EditMode 재현 단언).
- [ ] 콘텐츠 17종 발동 스모크 — 골든에 스킬 판이 있으면 diff 귀속.
- [ ] EditMode 전건 초록.
