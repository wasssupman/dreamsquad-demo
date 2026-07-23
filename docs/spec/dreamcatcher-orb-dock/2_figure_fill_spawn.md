# 2 · 피규어 풀 / 스폰 (게이지 구동 물리 채움)

## 목적

항아리 안에 **게이지 비례 미니 피규어를 물리로 쌓는다**(unit 0 순수 시뮬 + unit 1 항아리 결합).
게이지가 오르면 통 위에서 피규어가 떨어져 쌓이고, 소비 시 줄어든다. unit 1 의 단색 채움을
피규어 더미가 대체(뒤에 옅은 액체 backing 만 유지).

**단계화**(2026-07-23 사용자 결정): 적 처치 이벤트가 스켈레톤 정체성을 안 실어보내고(디펜더만
`DefenderDied(DefenderUnitData)`로 완비) BattleScene 배선이 선행돼야 하므로 2 단계로 나눈다.
- **2a (본 단위)**: 절차적 피규어 스프라이트로 풀·물리·게이지 구동 구현. 배선 불요, 오프스크린 검증.
- **2b (후속)**: `SkeletonGraphic` 프리즌 스킨(대표 스켈레톤 + 디펜더 정확 재현)으로 비주얼 교체.
  BattleScene 로드+배선, Play 검증. (경로 A = UGUI SkeletonGraphic, 탐색 근거 `SquadCharacterPage`.)

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePile.cs` (namespace `Wassup.UI`).
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — 항아리 내부에 `JarFigurePile`
  생성, `Refresh` 에서 `pile.SetTargetLevel(normalized)`. 단색 채움은 옅은 backing 으로 강등.

## 구현

- **`JarFigurePile`**: 피규어 풀(상한 20) + `JarFigurePhysics` 매 프레임 고정 스텝(1/60 누적)
  + RectTransform 매핑.
  - `Configure(max, radius, JarSimParams, Sprite, Color[] tints)` — 풀 선생성(비활성).
  - `SetTargetLevel(float normalized)` — 목표 개수 = `round(normalized * max)`.
  - `Tick(float dt)` — 목표 향해 스폰(통 위 결정론적 x 분산 + 하향속도)/제거, 물리 스텝, 위치 반영.
    Update 가 `unscaledDeltaTime` 을 고정 스텝으로 누적해 호출(Verlet 안정성 = 고정 dt).
  - 좌표: pile RectTransform 이 항아리 인테리어를 채우고 pivot 하단중앙 → `JarFigurePhysics`
    로컬좌표(x∈[-halfWidth,halfWidth], y=0 바닥)를 anchoredPosition 에 직접 매핑.
- **피규어 스프라이트**: 절차적 원형 바디(rim) + tint 배열(보라/청록/파랑). 2b 에서 스켈레톤 교체.
- **물리 파라미터**: px 단위 튜닝(gravity ~1500, damping 0.9, sleepMotionSq 소). SO 아님 —
  항아리 기하 종속 상수라 뷰 소유(하드코딩 아님: 레이아웃 값). 순수 산식은 unit 0 이 소유.
- **AwakeningGaugeView 통합**: 레이어 순서 fill(옅은 backing) → ticks → **pile** → number → rim.
  `Refresh` 가 `_normalized` 로 `pile.SetTargetLevel` 호출. 게이지 리셋/소비 시 자동 감소.

## 완료 기준

- **compile**: `Wassup.Runtime` 그린. 씬 배선 무변경(절차적, 배선 불요).
- **오프스크린 검증**: 게이지 25/60/100 → 피규어 ~5/12/20 개가 항아리에 자연 정착, 채움 높이가
  개수에 따라 상승, 겹침·이탈 없음. unit 1 의 숫자·틱·림과 공존.
- 회귀: unit 1 계약(Toggled/탭 토글/phase 표시) 유지. EditMode 회귀 없음.
- **후속(2b)**: SkeletonGraphic 스킨 교체 + BattleScene 배선 + Play. 적 스켈레톤 정체성은
  별도 bridge 이벤트 확장 필요(ECS 경계 — 별도 판단).
