# 1 — 콘(부채꼴) 판정 순수 함수

## 목적

«대상 방향 부채꼴 안인가» 를 판정하는 **순수 static 함수 하나**를 추가한다. 드래곤 브레스(unit 4)가
쓸 유일한 신규 수학이다. 이 커밋은 **소비자가 0** 이라 행동 변화가 없다.

## 이 단위가 원래 무엇이었나 (되돌리지 말 것)

초판 스펙은 여기서 `EffectArea` struct + `EffectAreaShape` enum + `EffectAreaMath` 를 신설하고,
unit 2 에서 기존 `TileAoe` 페이로드를 그 위로 이관해 «소비자 2개» 를 만들려 했다.
**2026-08-12 리뷰(critic + ECS 경계 + 자체 검토)가 셋 다 독립적으로 과설계로 판정해 접었다.**

근거:

- **어떤 소비자도 도형에 다형적이지 않다.** 두 소비자 모두 shape 을 지역 변수로 조립해 몇 줄 뒤
  소비했다(컴포넌트 저장 금지가 스펙 명시). 태그가 **어떤 경계도 건너지 않고** 저작 축에도 없다
  (`AreaBreath` 는 항상 Cone, TileAoe 페이로드는 항상 반경).
- **TRD 5.2 금지 패턴 2개 동시 위반** — 「enum + switch 떡칠(다형성은 Tag Component 로)」 +
  「나중을 위한 추상화·확장 포인트」.
- **소비자 2개 중 1개를 이 spec 이 만들려 했다.** 그 이관은 `TileAoe.IsInTileRange` →
  `EffectAreaMath.Contains` → `TileAoe.IsInTileRange` 인 **행동 변화 0 의 순수 인디렉션**이고,
  대가는 메테오·보스 barrage·폭탄이 지나는 라이브 데미지 경로 수정이었다. 목적과 수단이 뒤바뀐
  자기충족적 정당화.
- 제약 10 의 「과잉 추상화 경고」 적용 결과 **정당화되는 것은 콘 판정 하나뿐**이다 — 원 판정은
  `TileAoe.cs` 에 이미 있다.

도형 어휘 통합이 정말 필요해지는 시점은 `HazardShapeSampler`(managed `List<int2>`)·오라·존·splash·
어그로 반경이 실제로 한 축을 공유해야 할 때다 → README 후속 후보.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/TileAoe.cs` — `IsInCone` 추가 (같은 파일, 같은 계층)
- `Assets/_Project/Tests/EditMode/TileAoeTests.cs` — 콘 단언 추가

**신규 타입 0 · 신규 파일 0.** `TileAoe` 는 이미 «공유 광역 멤버십 프리미티브» 로 선언된 순수·Burst
static 클래스이고 콘은 그 옆에 놓일 같은 성격의 술어다.

## 구현

```csharp
// from/to = 월드 XZ. dir = 정규화된 조준 방향. cosSq = cos²(반각), rangeWorld = 사거리(월드).
// 반각 정의역은 (0°, 90°) — 호출처(bake)가 보장한다. 아래 «부호 가드» 참조.
public static bool IsInCone(float2 from, float2 to, float2 dir, float cosSq, float rangeWorld)
{
    float2 d = to - from;
    float d2 = math.lengthsq(d);
    if (d2 <= SameSpotEpsSq) return true;              // 같은 자리 = 포함
    if (d2 > rangeWorld * rangeWorld) return false;    // 사거리
    float dp = math.dot(d, dir);
    return dp > 0f && dp * dp >= cosSq * d2;           // 부호 가드 + 제곱 비교
}
```

- **`dp > 0` 부호 가드는 필수다.** 빠뜨리면 제곱이 부호를 잃어 **등 뒤에 대칭 콘**이 생긴다.
- **그 가드가 정의역을 반각 90° 로 자른다.** `cos²θ = cos²(180−θ)` 라 저작 120° 는 조용히 60° 로
  동작하고 180° 는 «전방위» 가 아니라 «정면 한 줄» 이 된다 → **bake 가 `>= 90` 을 loud 거절**한다
  (unit 4). 이 함수는 정의역을 문서로만 방어하고 클램프하지 않는다.
- **같은 자리는 포함**이다. 드래곤은 lift 로 떠 있지만 sim 좌표는 평면이라 바로 아래 방어유닛과
  XZ 가 거의 같아지는 것이 상시 상황이고, 그때 방향이 무의미해져 `dp > 0` 이 조용히 제외한다.
- **월드 좌표로 판정한다**(셀 아님). `TileAoe` 의 반경 판정이 셀인 것은 착탄이 **셀에 락돼** 있어서
  이지만, 브레스는 연속 이동하는 비행 유닛에서 나가고 `dir` 도 월드에서 만들어진다. 멤버십만 이산
  으로 하면 반경 1~2타일에서 셀 중심 양자화가 방향 판정을 최대 ~45° 흔든다. 사거리만
  `tileRange × tileSize` 로 환산한다. 선례: `AttackReach.InReach`(사거리는 타일 · 미세 판정은 월드).
- `math.*` 만 쓴다 — 호출처 `AttackSystem` 이 `[BurstCompile]` 이다.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode 신규 (`TileAoeTests` 에 추가):
      - 정면 포함 / 반각 밖 제외 / **뒤쪽 제외**(부호 가드 회귀 방지) / 사거리 밖 제외
      - **같은 자리 포함**
      - 대각 방향이 반각 40° 에서 제외 · 50° 에서 포함
        (대각 `dp²/d² = 0.5`, `cos²40° ≈ 0.587` → 제외, `cos²50° ≈ 0.413` → 포함)
      - `from`/`to` 를 **뒤집어** 넣으면 결과가 뒤집힌다(비대칭 술어의 인자 순서 고정)
- [ ] 기존 `TileAoeTests` 단언 전부 그대로 통과 (`IsInTileRange` 는 건드리지 않았다)
- [ ] **행동 변화 0** — 소비자가 없으므로 PlayMode 결과가 baseline 과 동일
