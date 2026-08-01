# 0 — 비행 시간 재매핑 순수 함수

## 목적

등속으로 흐르던 아치 비행에 리듬을 준다: **초반 급상승 → 정점 체공 → 후반 급하강**. 기하는 손대지
않고 시간만 재분배한다. 제약 10 근거: (a) 분기 있는 비자명 계산, (b) 호출처 2곳(드롭·도약),
(c) 회귀 테스트 가치 — `power=1` 항등이 무회귀의 근거라 테스트로 못 박아야 한다.

`0_dismount_arc_math.md`(`DismountPoint`)와 같은 자리·같은 형태다. 그 함수가 **기하만** 담당하고
"시간 이징은 호출측 책임" 이라고 계약해 둔 자리에 들어가는 짝이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — static 메서드 1개 추가
- `Assets/_Project/Tests/EditMode/KeyringSimTests.cs` — 테스트 추가(기존 파일)

## 구현

```csharp
// flight-lift-feel unit 0 — 비행 구간 시간 재매핑(ease-out-in). 양 끝이 빠르고 중간이 느리다:
// 초반 급상승 → 정점 체공 → 후반 급하강. 기하는 불변, 시간만 재분배한다.
//   power = 1  → 항등 (현행 선형과 byte-identical)
//   power < 1  → 리듬 강화 (0.7 근처가 기본 대역)
// ⚠ **비행 구간에만** 적용한다. 호출측 규약:
//     u    = (t01 − recoilFrac) / (1 − recoilFrac)
//     t01' = recoilFrac + (1 − recoilFrac) × FlightTimeRemap(u, power)
//   반동(웅크림) 구간까지 왜곡하면 힘 모으는 타이밍이 흔들린다.
// 총 시간은 바뀌지 않는다 — 드롭의 "비행 창 ⊆ pending 창" 계약이 그대로 산다.
public static float FlightTimeRemap(float u, float power)
{
    if (power >= 0.999f) return u;      // 항등 조기 반환 — 기본값 경로에 pow 비용·오차 0
    u = Mathf.Clamp01(u);
    float p = Mathf.Max(0.05f, power);  // 0 은 계단 함수라 금지
    return u < 0.5f
        ? 0.5f * Mathf.Pow(2f * u, p)
        : 1f - 0.5f * Mathf.Pow(2f - 2f * u, p);
}
```

- **왜 `Out*` 이 아닌가**: `DismountPoint` 의 "시간 이징 없음(선형)" 은 `Out*` 이징이 끝속도를 0 으로
  죽여 스틱 착지를 물러지게 하기 때문에 세운 계약이다. ease-out-in 은 **끝속도를 오히려 키우므로**
  그 계약과 충돌하지 않는다 — 오히려 착지 임팩트를 강화한다.
- 이 유닛은 함수 정의와 테스트까지다. **적용은 unit 3.** 여기서 호출처를 건드리지 않으므로 커밋
  시점의 런타임 동작은 완전 무변경이다.

## 완료 기준

- EditMode 테스트(신규 5):
  - **항등**: `power=1` 에서 `u ∈ {0, 0.25, 0.5, 0.75, 1}` 이 그대로 반환 (오차 0 — 조기 반환 경로)
  - **끝점**: 임의 `power` 에서 `Remap(0)=0`, `Remap(1)=1` (오차 < 1e-5)
  - **단조 증가**: `power=0.7` 에서 100 분할 샘플이 순증가
  - **대칭**: `Remap(u) + Remap(1−u) ≈ 1` (오차 < 1e-5) — 상승/하강 리듬이 대칭이라는 계약
  - **체공 성립**: `power=0.7` 에서 중앙 기울기 < 끝 기울기 (차분 비교). `power=1` 에서는 둘이 같음
- compile 클린 · 기존 `KeyringSimTests` 무회귀

## 검증 기록

- 2026-08-01 · EditMode 1790 중 1788 통과·실패 0 · compile 클린 · 독립 코드 리뷰 반영(`c6f6405e`).
- **사용자 Play 감각 확인은 미완** — 통과 시 이 줄 아래에 확인 일자를 추가한다.
