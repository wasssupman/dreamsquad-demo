# 0 · 피규어 물리 순수 코어

## 목적

항아리 안에서 미니 피규어들이 떨어져 벽·바닥·서로에 부딪히며 **정착(settle)** 하는 물리를
아키텍처 중립 순수 함수로 구현한다(제약 10). `Time`/`EntityManager`/`Spine` 무관, plain 값
in/out → EditMode 회귀 대상. 게이지 값 자체는 이 시뮬이 결정하지 **않는다**(`Gauge` 가 source
of truth). 이 코어는 (a) 렌더용 피규어 위치와 (b) 정착 시 **단조·결정론적인 채움 높이**(2순위
판독)만 제공한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePhysics.cs` (namespace `Wassup.UI`,
  asmdef `Wassup.Runtime`)
- 신규 `Assets/_Project/Tests/EditMode/JarFigurePhysicsTests.cs`

## 구현

기법: **Verlet 적분 + 위치 제약**(벽/바닥/피규어 겹침). 비탄성 정착이라 드롭 임팩트 순간만
살아 움직이고 빠르게 멈춘다(spec "임팩트-only" 의도와 일치). 속도는 `(pos − prevPos)` 로 암시.

값 타입 3종 + static 코어:

- `struct JarFigure { float2 pos; float2 prevPos; float radius; }` — 항아리 로컬 좌표
  (x∈[-halfWidth, halfWidth], y=0 이 바닥, 위로 +). 암시 속도 = `pos − prevPos`.
- `struct JarBounds { float halfWidth; float height; }` — 좌우 벽·천장(넘침 판정용).
- `struct JarSimParams { gravity; damping; sleepMotionSq; }` + `Default`.
- `static class JarFigurePhysics`:
  - `Create(pos, vel, radius, dt)` — 초기 속도로 `prevPos` 역산(드롭 스폰용, unit 2).
  - `Step(JarFigure[], int count, in JarBounds, in JarSimParams, float dt, int iterations=6)`
    — 고정 dt 한 스텝: ① Verlet 적분(암시 속도×damping + 중력), ② 위치 제약 반복(피규어 원-원
    겹침 절반씩 밀어내기 + 벽/바닥 클램프), ③ 스텝당 이동 `sleepMotionSq` 미만이면 `prevPos=pos`
    스냅. 호출자 소유 버퍼 제자리 갱신. **RNG·Time 없음 → 결정론**.
  - `FillHeight(JarFigure[], int count)` — 가장 높은 피규어의 top(`pos.y + radius`). 빈 통 0.
  - `TotalMotionSq(JarFigure[], int count)` — ∑|pos−prevPos|². settle 감지·테스트용, 정착 시 →0.

물리 파라미터는 전부 `JarSimParams`(호출자가 SO/뷰에서 주입) — 하드코딩 금지(제약 6).
정착 채움 높이만 게이지 2순위 판독으로 노출하고, "몇 개인지 세기"는 판독 계약에서 제외한다.

## 완료 기준

- **compile**: `Wassup.Runtime` + `Wassup.Tests.EditMode` 컴파일 그린.
- **EditMode 테스트**(`JarFigurePhysicsTests`) 통과:
  1. **결정론** — 동일 초기 상태·스텝 수 → 동일 최종 위치(비트 동일).
  2. **격리(containment)** — 다수 스텝 후 모든 피규어가 벽·바닥 안(|x|≤halfWidth−r, y≥r, tol).
  3. **정착(settle)** — drop 없이 N 스텝 후 `TotalMotionSq` → ~0(< 1e-3).
  4. **단조 높이** — 좁은 통에서 k+1 개 정착 높이 ≥ k 개 정착 높이(피규어 추가가 수위를
     낮추지 않음).
  5. **비침투** — 정착 후 모든 쌍 거리 ≥ r₁+r₂ − tol.
- 순수 함수라 Play 검증 불필요. 뷰 배선은 unit 1·2 에서.

완료 확인 2026-07-23 — Unity EditMode 전체 1269개 실패 0(내 6개 포함), 오프스크린 시각검증
(fill 6/13/20 → 채움 높이 1.49/3.45/4.36 단조 상승·비침투·격리 육안 확인). Verlet 전환 근거:
임펄스 방식이 중력 재주입 한계진동으로 정착 실패(잔여 1.17→0.56) → Verlet+위치제약으로 ~0.
커밋 `1f97f564`(구현) · `df2df7ef`(meta).
