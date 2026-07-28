# 4 — 융단폭격 이관 (전용 arm 제거)

## 목적

기존 융단폭격(`PeriodicTimer` × `AreaBarrage`)을 패턴 명세로 재표현하고 전용 arm 을 없앤다. 소비자 2개(융단폭격 + unit 5 미사일)가 같은 emitter 를 타면 seam 이 옳은지 실증된다. **값 보존이 이 unit 의 유일한 성공 기준**이다.

## 변경 대상

- ~~`Data/ProjectileData.cs` — `ProjectileFlightMode.SkyFall` **append**~~ · ~~`ResolveProjectileAxes` 케이스~~ → **unit 1 에서 선반영**(궤적 enum 을 한 커밋에 모아 축 어휘를 한 번에 열었다)
- `Core/Dreamcatcher/DcApplicability.cs` — `EmitProjectilePattern` 등록 (아래 참조)
- 신규 `Assets/_Project/Data/Projectiles/Projectile_NightmareBarrage.asset` — Meteor 사본, flightMode/tileRange 확정치
- 신규 `Assets/_Project/Data/Projectiles/Pattern_NightmareBarrage.asset`
- `Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset` — mechanic 0 을 `EmitProjectilePattern` 으로 교체
- `Battle/Combat/BossPeriodicTriggerSystem.cs` — `AreaBarrage` arm 제거
- `Bridge/BattleBridge.cs` — bake 의 `AreaBarrage` 분기 → loud 거절
- 삭제 `Battle/Combat/BarrageEpicenter.cs` · `Tests/EditMode/BarrageEpicenterTests.cs`

## 구현

### 축 매핑 개통 (spec-review C1 — 이 unit 의 선행 조건)

기존 arm 은 `movement=SkyFall, payload=TileAoe` 를 **하드코딩**했고, `ResolveProjectileAxes` 는 `{Homing, BallisticToCell, Directional}` 만 안다. 게다가 실측 `Projectile_Meteor.asset` 은 `flightMode: 0`(Homing)·`impactTileRange: 0` 이다 — 플레이어 Meteor 는 `ApplyMeteor` 가 축을 하드코딩해서 이 필드들을 읽지 않기 때문에 값이 방치돼 있었다. **이대로 패턴 경유로 이관하면 폭격은 홈잉 단발(r0, 텔레그래프 무시)이 된다.**

따라서:

1. `ProjectileFlightMode.SkyFall` append + `ResolveProjectileAxes` 케이스 1개. 기존 asset 은 아무도 이 값을 갖지 않으므로 무회귀.
2. **전용 barrel 사본 `Projectile_NightmareBarrage`** 를 만든다(GUID 신규): `flightMode = SkyFall` · `impactTileRange = 3` · 나머지(낙하 비주얼·dropHeight·fallPortion)는 Meteor 값 그대로. 공유 `Projectile_Meteor` 는 **건드리지 않는다** — 플레이어 스킬이 쓰는 asset 의 방치 필드에 의미를 부여하는 순간 소유가 모호해진다.

### 패턴 asset (현재 SO 값 그대로)

| 필드 | 값 | 출처 (현행) |
|---|---|---|
| `barrel` | `Projectile_NightmareBarrage` | mechanic 0 `payload.projectile`(Meteor) 의 확정치 사본 |
| `damage` | 150 | `payload.magnitude` |
| `selection` | `RoundRobin` | `BarrageEpicenter.Select(fireCount)` |
| `shotCount` | 1 | 현행 = 발화당 1발 |
| `shotIntervalSec` | 0 | — |
| `reselectPerShot` | false | — |
| `telegraphSec` | 1.5 | `payload.duration` → `flightTime` |

`impactTileRange`(3)·낙하 축은 barrel 사본이 소유한다(계약 3).

보스 SO mechanic 0: `trigger` 는 그대로(`PeriodicTimer` / `periodSeconds 10`), `payload.kind` → `EmitProjectilePattern`, `payload.pattern` → 위 asset. `magnitude`/`tileRange`/`duration`/`projectile` 은 패턴으로 이사했으니 0/null 로 비운다.

### arm 제거

`BossPeriodicTriggerSystem` 에서 `AreaBarrage` 분기를 삭제한다. 남는 payload arm 은 `AllyMoveSpeedAura`(채찍질)와 `EmitProjectilePattern` 이며, 그 외는 기존대로 경고 로그다.

bake 쪽 `AreaBarrage` 분기는 "arm 없음 — 패턴으로 이관됨" 경고 + skip 으로 바꾼다(조용한 no-op 금지, dc-trigger 선례).

### `DcApplicability` 등록 (구현 중 발견, 2026-07-28)

새 `DcPayloadKind` 는 `Core/Dreamcatcher/DcApplicability.EvaluateMechanic` 의 switch 에도 등록해야 한다. 등록하지 않으면 `DcRejectReason.Unclassified` 로 **fail-closed**(부착 거절)되고 `DcApplicabilityTests.EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs` 가 전수 검사로 실패한다 — 실제로 unit 3 커밋 후 이 테스트가 걸렸고, 그게 설계된 안전망이다.

`EmitProjectilePattern` 은 **host 의 공격 모델과 직교**하므로 무조건 `None`(허용) 그룹에 넣는다: 대상은 패턴의 selection 이 스스로 뽑고(host 가 대상을 줄 필요 없음), 진영은 host 진영의 반대로 도출되며(계약 7), 데미지도 패턴 소유다 → `targetsEnemies`·`HostProvidesTarget`·`hasDamageOutput` 어느 축도 게이트가 아니다. 명세 자체의 유효성(`pattern`/`barrel` null)은 bake 가 최종 판정한다.

### `BarrageEpicenter` 흡수

`PatternTargeting.Select(rule: RoundRobin)` 가 동일 동작이다. unit 0 에서 **두 함수의 결과 일치 테스트를 이미 작성했으므로**, 여기서 원본과 그 테스트를 삭제하고 이관 완료로 본다. 삭제 전 `grep -rn "BarrageEpicenter"` 로 잔여 참조 0 확인.

## 완료 기준

- 컴파일 클린. `grep -rn "AreaBarrage"` 결과가 enum 정의 + bake 거절 + 주석뿐(라이브 arm 0).
- **값 보존 Play 검증**: 10초 주기 · 방어유닛 진앙 round-robin **순회**(연속 발화가 다른 방어유닛으로 도는지 — C2 회귀 감시) · 1.5초 낙하 텔레그래프 · 150 데미지 · 반경 3(SkyFall×TileAoe 경유 확인 — C1 회귀 감시) · 보스 자해 없음(`targetFaction=Defender`). 이관 전 스크린샷/로그와 대조한다.
- 방어유닛 0 마리 상황에서 발사가 조용히 소모된다(경고/에러 0). **의도된 semantics 차이 1건**: 현행 arm 은 no-fire 시 `fireCount` 불변(위상 보존)이지만, 새 구조는 push 시 선증가라 빈 풀 발화도 위상을 전진시킨다 — 빈 풀에선 관측자가 없고 풀 자체가 계속 변해 순회 공정성에 실질 영향이 없으므로 수용한다(동시 인스턴스의 시드 충돌을 막는 단순성이 이득). 되돌리려면 완주 시 write-back 인데 겹침 버스트에서 시드 충돌한다 — 채택하지 않는다.
- EditMode: `BarrageEpicenterTests` 삭제분이 `PatternTargetingTests` 로 덮여 총 건수 감소 없음.
- 플레이어 Meteor 무회귀(같은 barrel 을 공유하는 경우 특히) — 캐스트 후 착탄·데미지 확인.
