# 0. Layout Math — GiftPhaseLayout 순수 함수

## 목적

7 스테이지 안무가 쓰는 좌표·순서·타이밍 계산을 아키텍처-blind 순수 함수로 토대부터 깐다. 이후 unit 2 의 시퀀스 코드는 이 함수들의 결과를 소비만 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseLayout.cs` (신규, static class)
- `Assets/_Project/Tests/EditMode/GiftPhaseLayoutTests.cs` (신규)

## 구현

전부 plain 값 입력 → plain 값 출력. `UnityEngine.Vector2` 는 허용(수학 타입), RectTransform/Time 등 아키텍처 타입 금지.

1. `GridSlot(int k, int cols, Vector2 cell) → Vector2` — k번째 카드의 5×2 그리드 로컬 좌표(중앙 정렬, 행 우선). 10장 기준이지만 cols/개수 일반화.
2. `FanSlot(int f, int n, float radius, float arcDeg, float baseY) → (Vector2 pos, float rotDeg)` — 부채꼴 f번째 위치+회전. 좌→우 = 0→n-1, 중앙 솟은 아치(DreamcatcherHandView 아치와 같은 시각 언어, 값은 독립).
3. `RiffleOrder(int n) → int[]` — 리플 셔플 연출용 지퍼 인터리브 순서. 스택을 좌(0..n/2-1)/우(n/2..n-1) 두 뭉치로 갈라 좌우좌우 교차로 재적층되는 **시각적** 순서. 결과 덱 순서와 무관한 가짜 연출이므로 계약은 "n개 전부 정확히 1회 등장" 뿐.
4. `AbsorbDelay(int i, float first, float min, float decay) → float` — i번째 흡수 시작 지연(가속 케이던스). 누적합: `delay(i) = Σ max(min, first × decay^j)`. 단조 증가·간격 단조 감소.
5. `StackJitter(int k) → (float rotDeg, Vector2 offset)` — 스택 적층 시 index 결정론 미세 회전/오프셋(seeded RNG 금지 — 구조적 결정론 원칙).

## 완료 기준

- EditMode 테스트: 그리드 중앙 대칭·행 전환, 부채꼴 좌우 대칭·회전 부호, RiffleOrder 순열 검증(n=12 전원소 1회), AbsorbDelay 단조성·min 클램프, StackJitter 결정론(같은 k → 같은 값).
- Unity 컴파일 클린. 기존 테스트 무회귀.

확인: 2026-07-14 — EditMode 12/12 (총 755 무회귀). 커밋 `cb3d99d9`.
