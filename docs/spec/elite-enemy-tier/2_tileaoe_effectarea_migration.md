# 2 — `TileAoe` 페이로드를 `EffectArea{TileRadius}` 로 이관

## 목적

unit 1 의 `EffectArea` 를 **기존 동작에 먼저 대보고** 무회귀를 증명한다. 새 도형(콘)과 새 소비자를
동시에 켜면 실패 원인이 «도형 수학이 틀렸나 / 소비자 배선이 틀렸나» 로 갈라지지 않는다
(`traversal-layers` unit 5 의 교훈 — 순수 함수는 전부 초록인데 유닛이 얼어 있었다).

이 커밋도 **행동 변화 0** 이 목표다. 메테오·보스 barrage·폭탄이 같은 코드를 타므로 여기서 값이
한 톨이라도 바뀌면 라이브 밸런스가 움직인다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EffectAreaMath.cs` — `TileRadius` 분기 구현
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — `PayloadKind.TileAoe`
  피해자 순회의 멤버십 판정 1줄 (현행 `TileAoe.IsInTileRange(cell, centerCell, tileRange)`)

## 구현

### 계층을 나눈다 — `EffectAreaMath` 는 디스패치, `TileAoe` 는 수학

`TileAoe`(`Battle/Combat/TileAoe.cs`)는 **이미 순수·Burst 공유 프리미티브**이고 프로덕션 호출처가
5곳이다. 그것을 대체하지 않는다. `EffectAreaMath.Contains` 의 `TileRadius` 분기가 **그 함수를
그대로 호출**한다:

```csharp
case EffectAreaShape.TileRadius:
    return TileAoe.IsInTileRange(point, origin, area.tileRange);
```

이렇게 하면 식이 **글자 그대로 동일**해서 수치 drift 가 원리적으로 불가능하고, `TileAoeTests`
20여 개 단언이 그대로 이 경로의 회귀 방지가 된다.

### 이관하는 호출처는 **1곳뿐**

`ProjectileHitSystem` 의 `PayloadKind.TileAoe` 피해자 순회만 `EffectAreaMath.Contains` 를 타게
바꾼다. 이 자리가 «페이로드가 자기 도형을 선언한다» 를 표현하는 지점이기 때문이다.

**나머지 4개 호출처는 그대로 둔다** — `AggroTargeting`(어그로 후보) · `DefenderDensity`(밀집도
계수) · `BounceRetarget`(튕김 재조준) · `BattleBridge:3574`(브리지 미러). 이들은 «효과 영역» 이
아니라 타게팅·통계 질의라서 도형이 데이터가 될 이유가 없다(제약 8).

도형 값의 출처는 지금은 `ProjectileState`/요청의 `impactTileRange` 그대로다 — **`EffectArea` 를
컴포넌트에 저장하지 않는다.** 순회 직전에 지역 변수로 조립한다:

```csharp
var area = new EffectArea { shape = EffectAreaShape.TileRadius, tileRange = tileRange };
```

컴포넌트 필드를 늘리는 것은 소비자가 그것을 필요로 할 때(unit 4 의 콘)에 한다.

## 완료 기준

- [ ] compile 통과
- [ ] `TileAoeTests` 전체 통과 (건드리지 않았으므로 그대로여야 한다)
- [ ] EditMode 전체 — 신규 실패 0
- [ ] **PlayMode 무회귀 대조**: 이 단위 착수 직전 커밋을 baseline 으로 PlayMode 전체를 돌려
      pass/fail 집합이 **동일**함을 확인한다. 메테오·보스 AreaBarrage·폭탄이 이 경로다
- [ ] mutation 확인: `TileRadius` 분기를 `tileRange - 1` 로 바꾸면 TileAoe 관련 테스트가
      **실제로 빨개진다**(경로가 살아 있다는 증거 — 조용한 우회를 배제)
- [ ] `EffectArea` 를 저장하는 컴포넌트 필드가 늘지 않았다 (지역 조립만)
