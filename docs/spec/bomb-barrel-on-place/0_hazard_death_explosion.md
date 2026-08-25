# 0 — 설치물이 부서지면 터진다 (축 개통)

## 목적

판 위 길막 설치물이 죽을 때 **그 자리에서 폭발**하게 한다. 지금 설치물의 죽음은
「사라짐 + 파괴 VFX」가 전부고, 파괴 알림 채널은 브리지가 연출로만 쓰고 버린다.
이 단위가 여는 축은 **「이 설치물이 부서질 때 X 한다」** 이며, 배럴은 그 첫 소비자다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — 폭발 피해·반경·**폭발 탄 SO**
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazard.cs` — 같은 값의 런타임 사본 + `explodeDataIndex`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — bake(파라미터 1개 추가)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnBlockingHazardWithVisual` 이 탄 index 해석
- `Assets/_Project/Scripts/Battle/Combat/BarrelExplosionSystem.cs` (신규)
- `Assets/_Project/Tests/EditMode/BarrelExplosionTests.cs` (신규)

## 구현

- **SO 필드**: `float explodeDamage` · `int explodeTileRange` · `int explodeTargetCap`
  (가까운 순 상한, 0 = 무제한) · `ProjectileData explodeProjectile`.
  `explodeDamage` 기본 0 = **폭발 없음** → 기존 길막 캐스터 무회귀(계약 5).
- **런타임 사본**: `BlockingHazard` 에 같은 세 수치 + **`int explodeDataIndex`**.
  sim 은 SO 를 못 읽으므로 스폰이 싣는다. index 해석은 브리지 몫이다 —
  `SpawnBlockingHazardWithVisual` 이 `GetOrCreateProjectileDataIndex(so.explodeProjectile)` 로
  풀어 `EffectSpawner.SpawnBlockingHazard` 에 넘긴다(기존 `RegisterBlockingHazardSO` 와 같은 자리).
  ⚠ **index 를 빼먹으면 기본 0 번 탄으로 폭발이 연출된다** — 피해는 맞고 VFX 만 엉뚱하다
  (조용한 오작동). `explodeDamage > 0` 인데 index < 0 이면 loud warn.
- **`BarrelExplosionSystem`**(Combat, `ISystem`): `BlockingHazard` + `DeadTag` 를 순회해
  `explodeDamage > 0` 이면 그 자리에 **즉발 광역 투사체**를 stage 한다 —
  `ProjectileSpawnRequest{ movement=SkyFall, payload=TileAoe, origin=impact=설치물 칸 중심,
  flightTime=0, impactTileRange=explodeTileRange, aoeTargetCap=explodeTargetCap,
  damage=explodeDamage, dataIndex=explodeDataIndex, targetFaction=Enemy }`.
  폭발 해결은 기존 착탄 시스템이 한다(계약 2).
- **⚠ 요청은 죽는 배럴이 아니라 전용 캐리어 엔티티에 건다**(계약 9). 배럴에 걸면 같은 프레임에
  `UnitLifecycleSystem` 이 엔티티를 파괴해 **브리지 드레인이 요청을 영영 못 본다**(드레인은
  MonoBehaviour Update 라 ECS 시스템 전부가 끝난 뒤 돈다). 기존 관용구를 그대로 쓴다 —
  `ecb.CreateEntity()` + `ProjectileRequestCarrier` + `ProjectileSpawnRequest`
  (`AttackSystem.SpawnNeedleCarrier`·`UltimateLeapSystem` 선례). 드레인이 캐리어를 **통째로
  파괴**하므로 잔여 엔티티도 안 남는다(`BattleBridge.cs` 드레인의 캐리어 분기).
- **맥락**: 읽는 것은 `BlockingHazard`(Effects)·`DeadTag`(Units)이고 **쓰기는 없다** —
  타 맥락 컴포넌트 읽기는 허용(CLAUDE.md 맥락 규칙). 만드는 것은 Combat 자기 것뿐이다.
- **⚠ 순서 못박기 — 어트리뷰트 3개 전부 필요하다.** 하나만 걸면 정렬기 tie-break 에 맡겨져
  「가끔·빌드마다 안 터진다」가 된다. `DeadTag` 를 붙이는 생산자가 둘이고 파괴자가 하나다:
  ```
  [UpdateAfter(typeof(Wassup.Battle.Units.DamageApplicationSystem))]   // 체력 사망 경로
  [UpdateAfter(typeof(Wassup.Battle.Effects.ObstacleLifetimeSystem))]  // 수명 만료 경로(unit 1)
  [UpdateBefore(typeof(Wassup.Battle.Units.UnitLifecycleSystem))]      // 파괴 전에 봐야 한다
  ```
  전자 둘 중 하나라도 뒤에 놓이면 그 프레임엔 `DeadTag` 가 없고, 다음 프레임엔 엔티티가
  이미 없다 — **폭발이 통째로 소실된다.**
- **중복 폭발 없음**: `UnitLifecycleSystem` 이 자기 `OnUpdate` 안에서 즉시 `ecb.Playback` 하므로
  `DeadTag` 배럴은 붙은 프레임에 파괴된다 = 이 시스템이 정확히 한 번 본다. 별도 «폭발함» 태그
  불필요. (이 전제가 깨지면 — 예: lifecycle 이 지연 ECB 로 바뀌면 — 중복이 생긴다.)

## 완료 기준

- [x] compile 0 에러.
- [x] `BarrelExplosionTests`: 폭발값 0 인 설치물은 요청을 안 낸다 · 값 있으면 정확히 1회 ·
      요청이 **`ProjectileRequestCarrier` 를 단 별도 엔티티**에 실린다 · 착탄 칸이 설치물 칸 ·
      반경/상한/피해/`dataIndex` 가 컴포넌트 값과 일치.
- [x] **시스템 순서 단언**: 위 세 어트리뷰트가 실제 유효 순서에 반영됐는지 EditMode 로 핀
      (정렬기 tie-break 는 빌드마다 다를 수 있어 Play 만으로는 간헐 재현이다 — 리뷰 지적).
- [x] 전체 EditMode 회귀 없음(기존 길막 설치물은 값 0 이라 무영향).

확인 2026-08-22 · `BarrelExplosionTests` 7건 green · 전체 EditMode 2548/1(기존 실패만).
**Play 실측**: 배럴을 세우고 치명 피해를 넣자 `+barrel f1729 → -barrel f1730 + CARRIER1 f1730` —
부서진 **그 프레임에** 폭발 요청이 나갔다. 반경 1 안 적 체력 `60 → 0`(폭발 120)으로 처치까지 확인.
