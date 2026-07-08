# 0 · KeyringSim 추출 (로직 통합, 동작 무변경)

## 목적

인게임/아웃게임에 중복된 키링 수학 3종(스프링 스텝·기울임각·낙하 스텝)을 순수 static 클래스 `KeyringSim` 으로 추출한다. **동작 무변경 리팩토링** — 수치 스냅샷 테스트로 등가를 고정.

## 변경 대상

- 신설: `Assets/_Project/Scripts/UI/KeyringSim.cs` (namespace `Wassup.UI` — 양 컨트롤러와 동일 어셈블리)
- 수정: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (Update 의 스프링/기울임 인라인 수학 → 호출)
- 수정: `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` (TickDrag 스프링/기울임 + FallStep 이동)
- 수정: `Assets/_Project/Tests/EditMode/LobbyKeyringFallStepTests.cs` → `KeyringSimTests.cs` 로 개명·재조준 + 스냅샷 테스트 추가

## 구현

```csharp
public static class KeyringSim
{
    // 무게추 스프링+감쇠+속도상한 적분. dt clamp 는 호출측 책임(현행 Mathf.Max(dt,1e-4f) 유지).
    public static void SpringStep(ref Vector3 pos, ref Vector3 vel, Vector3 target,
        float spring, float damping, float maxSpeed, float dt);
    // Vector2 포워딩 오버로드(아웃게임) — Vector3 본체 위임, z=0 왕복 bit-exact.
    // 호출측 마샬링의 copy-back 누락 풋건 흡수 (unit 0 리뷰 반영).
    public static void SpringStep(ref Vector2 pos, ref Vector2 vel, Vector2 target,
        float spring, float damping, float maxSpeed, float dt);
    // 줄 방향 → 기울임각(deg). 내부 정규화 금지 — 입력은 호출측 그대로
    // (인게임: 단위벡터의 camRight/camUp 투영 = 비단위 2D, 아웃게임: 단위 2D).
    public static float LeanAngle(float x, float y, float maxAngle);
    // LobbyKeyringDrag.FallStep 시그니처 그대로 이동.
    public static bool FallStep(ref float y, ref float velY, float floorY, float dt,
        float gravity, float bounceDamping, float bounceMinSpeed);
}
```

- `SpringStep` 본문 = 현행 두 곳과 동일 연산 순서: `accel = (target-pos)*spring - vel*damping` → `vel += accel*dt` → `maxSpeed > 0` 이면 magnitude 클램프 → `pos += vel*dt`. (DefenderDragPlacementController.cs:94-101 ≡ LobbyKeyringDrag.cs:111-118, Vector2 ⊂ Vector3 z=0 bit-exact — critic 검증 완료.)
- `LeanAngle` 본문 = `Mathf.Clamp(-Mathf.Atan2(x, Mathf.Max(y, 1e-3f)) * Mathf.Rad2Deg, -maxAngle, maxAngle)`.
- 아웃게임 호출부는 Vector2 오버로드를 직접 호출(마샬링은 KeyringSim 내부). 초기화(`_posInit` 스냅 vs 낙하속도 승계 재잡기)는 각 호출측 잔류.
- 테스트: FallStep 기존 케이스 재조준 + SpringStep/LeanAngle 을 **현행 에셋 상수**(spring 100 / damping 2.5 / maxSpeed 12·2400 / maxAngle 8)로 수 스텝 돌린 수치 스냅샷(기대값은 추출 전 코드로 산출) — bit-exact 회귀 방지.

## 완료 기준

- compile 클린, EditMode 전체 통과 (재조준 FallStep + 신규 스냅샷).
- 두 컨트롤러의 diff 가 수학 인라인 → `KeyringSim` 호출 대체뿐임을 리뷰로 확인 (동작 무변경).
- 에디터 Play 스모크: 인게임 드래그 스윙 / 아웃게임 스와이프·낙하가 리팩토링 전과 체감 동일.

확인 2026-07-08 — compile 클린 · EditMode 통과(키링 7개, 무관 사전실패 2 제외) · 8앵글
코드리뷰 후 CONFIRMED 1(Vector2 오버로드)·PLAUSIBLE 1(헤더) 반영. 사용자 진행 승인.
Play 스모크 체감 확인은 unit 3 시각 검증에서 재확인 예정. 커밋 `76bcb69f`.
