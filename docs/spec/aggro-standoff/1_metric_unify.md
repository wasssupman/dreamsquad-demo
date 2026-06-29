# 1 — standoff metric 통일 (M1)

## 목적

aggro 정지 판정을 `AttackSystem` 발사 판정과 **동일한 tile-Chebyshev 사거리**로 통일. (투트랙 리뷰 M1) Euclidean 정지 vs Chebyshev 발사 불일치로 `range<0.5·tile` 시 "정지하나 발사 못함"(frozen soft stall) 이던 것 제거.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — aggro 분기: Euclidean `dist > AttackState.range` → tile-Chebyshev `tileDist > RangeToTiles(range)`.

## 구현

- `tileDist = max(|Δx|,|Δy|)`(공격자 셀 ↔ guardian 셀), `tileRange = GridMath.RangeToTiles(AttackState.range)` — `AttackSystem`(`:117,140-141`)과 **동일 함수·식**.
- `tileDist ≤ tileRange` 면 정지 ⟺ AttackSystem 발사 조건 충족 → **정지 = 발사 가능** 일관.
- 그리디 이동/스냅(`step >= dist`)·cell-trim 은 유지(`dist` 는 이동 방향용으로 남김). `aggroCell` 은 tileDist·cell-trim 공용으로 한 번만 계산.

## 완료 기준

- compile 0 · EditMode 26 무회귀.
- range≥1(현 데이터): 이전 Euclidean(보수적)과 거동 동등 + 발사와 정확히 일치. range<0.5: 정지⟺발사 일관(둘 다 미충족 = 계속 접근, frozen idle 소멸).
- 라이브 거동은 에디터 포커스 시 측정 가능 — 무회귀는 metric 일치로 구조적.

---

확인: 2026-06-29 · compile 0 · EditMode 26/26. range≥1 거동 동등(Euclidean 도 보수적이었음) + 발사 metric 과 정확 일치. soft stall(range<0.5) 소멸.
