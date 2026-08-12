# 1 — `EffectArea` 값 타입 + 순수 판정 함수

## 목적

«광역 효과의 도형» 을 **데이터로 표현하고 순수 함수로 판정**한다. 지금은 소비처마다 거리 비교가
인라인돼 있어(Chebyshev·유클리드 각각) 부채꼴 같은 새 도형을 넣을 자리가 없다.

**이 단위는 소비자를 만들지 않는다** — 타입과 함수와 테스트만 넣는다. 소비자는 unit 2(TileAoe
이관)와 unit 4(브레스)가 각각 붙인다. 그래서 이 커밋은 행동 변화 0 이다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EffectArea.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/EffectAreaMath.cs` (신규)
- EditMode 테스트 (신규)

맥락 = **Combat**. 근거: 첫 두 소비자가 `ProjectileHitSystem`(Combat)과 `AttackSystem`(Combat)
이고, 이 값이 결정하는 것은 «누가 피해를 받나» 다.

## 구현

### 인터페이스가 아니라 태그된 struct

사용자 제안은 «EffectArea 인터페이스» 였으나 C# `interface` 는 이 코드베이스에서 쓸 수 없다 —
구현체를 unmanaged 컴포넌트/슬롯에 담을 수 없고 Burst 에서 깨진다. 같은 의도의 ECS 대응물이
아래 형태이며, 제약 8(인터페이스는 구현체 2개 이상)에 애초에 걸리지 않는다.

```csharp
public enum EffectAreaShape : byte
{
    TileRadius = 0,   // Chebyshev 타일 반경 — unit 2 가 기존 TileAoe 를 여기로 이관
    Cone = 1,         // 방향 + 반각 + 사거리 — unit 4 브레스
}

public struct EffectArea
{
    public EffectAreaShape shape;
    public int   tileRange;      // 두 도형 공용 — 반경(TileRadius) / 사거리(Cone)
    public float2 direction;     // Cone 전용. 정규화 전제(생성처 책임)
    public float halfAngleDeg;   // Cone 전용
}
```

**v1 도형은 2종뿐이다**(계약 7). 유클리드 반경·라인·셀목록은 소비자가 없으므로 넣지 않는다.

### 순수 판정

```csharp
public static class EffectAreaMath
{
    // origin/point 는 **셀 좌표**. 반환 = point 가 area 안인가.
    public static bool Contains(in EffectArea area, int2 origin, int2 point);
}
```

- `TileRadius`: `GridMath.ChebyshevDistance(point, origin) <= tileRange` — 기존 인라인 판정과
  **글자 그대로 같은 식**이어야 한다(unit 2 의 무회귀가 여기 달려 있다).
- `Cone`: ① 원점 일치면 `true`(자기 셀은 항상 포함) ② Chebyshev 거리 > `tileRange` 면 `false`
  ③ `dot(normalize(point-origin), direction) >= cos(halfAngleDeg)` 면 `true`.
  `halfAngleDeg >= 180` 이면 전방위 = 원과 같아진다(클램프하지 않고 자연 성립).

Burst 호환을 지킨다 — `math.*` 만 쓰고 managed 할당·`Mathf` 를 쓰지 않는다.
셀 좌표 입력인 이유: 두 소비자가 이미 셀을 손에 들고 있고, 월드 좌표를 받으면 `tileSize`·`origin`
같은 맵 파라미터가 순수 함수 안으로 새어들어온다.

## 완료 기준

- [ ] compile 통과 · 신규 파일이 `.csproj` 에 들어갔는지 확인(파일 명시 나열 방식)
- [ ] EditMode 신규:
      - `TileRadius` 가 반경 경계에서 포함/제외를 정확히 가른다 (r=1 → 3×3 = 9칸 포함)
      - `Cone` 반각 45° 정면 셀 포함 / 90° 옆 셀 제외 / 뒤 셀 제외
      - `Cone` 사거리 밖은 각이 맞아도 제외
      - `Cone` 원점 = 포함
      - `halfAngleDeg = 180` 은 `TileRadius` 와 같은 집합을 낸다
- [ ] **행동 변화 0** — 소비자가 없으므로 PlayMode 결과가 baseline 과 동일
- [ ] Burst 컴파일 경고 0 (`[BurstCompile]` 을 붙인 호출처가 아직 없으므로 관찰만)
