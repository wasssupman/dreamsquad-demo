# 10 — 축 개통: 하늘낙하 × 적 조준

> 선행: unit 9(빨간 재현). 후행: unit 11(특수 케이스 철거).

## 목적

**근본 원인 = 탄 하나에 조준이 둘이다.**

이 파이프라인의 불변식은 「`flightMode` 하나 = 조준 하나 = 착탄 규칙 하나」다:

```
ProjectileFlightMode → (MovementKind, PayloadKind)  1:1   (BattleBridge.ResolveProjectileAxes)
MovementKind         → BindingClass                 순수  (MovementBinding.Of)
```

`SkyFall` = `(SkyFall, TileAoe)` = **Cell 조준** — 「발사 시점의 칸을 겨눠, 착탄 시점에 그 칸에
있는 것을 때린다」. 위치는 스폰 때 고정되고 **다시 조준하지 않는다**(예고는 움직이면 안 되므로
의도된 설계다).

캐논의 요구는 **적 단위 조준**이다("미사일 한개는 타겟1개를 피격" — 사용자 2026-08-16).
그런데 파이프라인엔 **「하늘에서 떨어지지만 적을 겨누는 탄」이라는 짝이 없다.** 그래서:

- unit 1 은 칸 조준을 지키려고 **칸당 1발로 접었다** → 발수가 적 수와 어긋났다.
- unit 8 은 요청에 `target` 을 실어 **`TileAoe` 팔이 임자만 고르게** 했다 → 궤적은 **칸**(발사
  시점 스냅샷)을 고정하고 페이로드는 **적**(착탄 시점)을 본다. **두 조준이 예고 시간만큼
  어긋나고 그 어긋남이 곧 헛방이다.**

두 조준을 남긴 채 어긋남만 덮는 수정(임자 추적 예외 · `impactTileRange` 확대 · 예고 축소)은
전부 땜빵이다 — 조준이 여전히 둘이라 다음에 어느 한쪽을 건드리면 같은 결함이 다시 열린다.
**빠진 축을 정식으로 연다.**

## 변경 대상

- `Assets/_Project/Scripts/Data/ProjectileData.cs` — `ProjectileFlightMode.SkyFallOnTarget`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/MovementKind.cs` — `SkyFallOnEntity`
- `.../Projectile/Emission/MovementBinding.cs` — 분류 + `KnownKindCount` 8→9
- `.../Projectile/ProjectileMoveSystem.cs` — 새 arm
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ResolveProjectileAxes` 짝 + `SpawnProjectile` 스폰 위치 arm
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` — 낙하 view arm 이 새 kind 도 받게
- `.../Projectile/Emission/ProjectileEmitterSystem.cs` — Entity fan-out 에 시차·결정 순서
- `Assets/_Project/Data/Projectiles/Projectile_CannonStrike.asset` — `flightMode` 4→6
- `Assets/_Project/Tests/EditMode/PatternTargetingTests.cs` — 전수 핀(`MovementBinding_ClassifiesEveryKnownKind`)

## 구현

1. **짝**: `SkyFallOnTarget → (SkyFallOnEntity, SingleSplash)`. 조준은 **엔티티 하나**,
   착탄은 기존 `SingleSplash` 팔(대상의 **현재** 위치에 피해, tile 판정 없음, 게이트 없음).
2. **바인딩**: `Of(SkyFallOnEntity) = Entity`. `KnownKindCount` 를 올리지 않으면
   `MovementBindingTests` 가 실패해 분류 누락을 잡는다 — 그게 이 파일의 설계된 확장점이다.
3. **Move arm**: 위치가 임자의 현재 위치를 따른다. 이건 예외가 아니라 **Entity 바인딩의 정의**다
   (`HomingToEntity` 도 대상의 살아 있는 위치를 향한다). 도착은 `SkyFall.Arrived(elapsed,
   flightTime)` — 시간 도착이 곧 **예고**의 정의다. 대상 소멸 시엔 기존 Entity 팔의 관례를 따른다.
4. **스폰/뷰**: `SpawnProjectile` 의 SkyFall 스폰 높이 arm과 `ProjectileViewPool` 의 낙하·
   `fallPortion` 은닉 arm이 새 kind 도 받게 한다(수학 신설 0 — 기존 `SkyFall.Progress` 재사용).
5. **Entity fan-out 시차**: `flightTime = order.telegraphSec + c * fanOutStaggerSec`,
   순서는 셀 rank(row-major) 로 결정론 고정(청크 순서가 새면 연출 순서가 프레임마다 바뀐다).
   이 분기의 **현재 소비자는 0** 이라(오늘 `fanOutToAllCandidates` 는 `Pattern_Cannon_Strike`
   하나뿐이고 그건 Cell 분기를 탄다) 무회귀다.

## 완료 기준

- unit 9 의 새 테스트 2개가 **초록**. 기존 5개 + `PatternTargetingTests` + `ProjectileSystemTests`
  + `PatternScopeTests` 초록.
- ⚠ unit 9 의 「같은 칸 착탄점」 테스트는 **더미를 칸 안에서 ±0.3타일 갈라 놓아야** 의미가 있다.
  `SpawnDummy` 는 둘을 정확히 같은 좌표에 놓고, 실제 판에서 그 둘을 갈라주는 분리 반경 시스템이
  더미에는 없다 — 안 갈라 놓으면 착탄점이 같은 것이 당연해져 아무것도 재지 못한다.
- 기존 SkyFall 소비자(메테오 barrage · 보스 AreaBarrage · 진동갑주 · `BallisticToCell` 평타)는
  `flightMode` 무변경 → **동작 바이트 동일**.
- Play 육안: 미사일이 적 위에서 터진다 · 뭉친 적에게도 발수가 적 수를 따라간다.

확인 2026-08-17 · 커밋 `8995140e`
