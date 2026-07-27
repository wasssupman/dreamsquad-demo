# 4 — 융단폭격 이관 (전용 arm 제거)

## 목적

기존 융단폭격(`PeriodicTimer` × `AreaBarrage`)을 패턴 명세로 재표현하고 전용 arm 을 없앤다. 소비자 2개(융단폭격 + unit 5 미사일)가 같은 emitter 를 타면 seam 이 옳은지 실증된다. **값 보존이 이 unit 의 유일한 성공 기준**이다.

## 변경 대상

- 신규 `Assets/_Project/Data/Projectiles/Pattern_NightmareBarrage.asset`
- `Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset` — mechanic 0 을 `EmitProjectilePattern` 으로 교체
- `Battle/Combat/BossPeriodicTriggerSystem.cs` — `AreaBarrage` arm 제거
- `Bridge/BattleBridge.cs` — bake 의 `AreaBarrage` 분기 → loud 거절
- 삭제 `Battle/Combat/BarrageEpicenter.cs` · `Tests/EditMode/BarrageEpicenterTests.cs`

## 구현

### 패턴 asset (현재 SO 값 그대로)

| 필드 | 값 | 출처 (현행) |
|---|---|---|
| `barrel` | `Projectile_Meteor` | mechanic 0 `payload.projectile` |
| `damage` | 150 | `payload.magnitude` |
| `selection` | `RoundRobin` | `BarrageEpicenter.Select(fireCount)` |
| `shotCount` | 1 | 현행 = 발화당 1발 |
| `shotIntervalSec` | 0 | — |
| `reselectPerShot` | false | — |
| `telegraphSec` | 1.5 | `payload.duration` → `flightTime` |

`impactTileRange`(3) 는 barrel(`Projectile_Meteor`)에서 온다 — 현행은 `payload.tileRange` 였으므로 **Meteor SO 의 `impactTileRange` 가 3 인지 확인 후** 다르면 패턴 도입 전에 barrel 값을 맞춘다(플레이어 Meteor 와 공유 asset 이면 전용 사본을 만든다 — 공유 asset 값을 바꿔 플레이어 스킬을 흔들지 않는다).

보스 SO mechanic 0: `trigger` 는 그대로(`PeriodicTimer` / `periodSeconds 10`), `payload.kind` → `EmitProjectilePattern`, `payload.pattern` → 위 asset. `magnitude`/`tileRange`/`duration`/`projectile` 은 패턴으로 이사했으니 0/null 로 비운다.

### arm 제거

`BossPeriodicTriggerSystem` 에서 `AreaBarrage` 분기를 삭제한다. 남는 payload arm 은 `AllyMoveSpeedAura`(채찍질)와 `EmitProjectilePattern` 이며, 그 외는 기존대로 경고 로그다.

bake 쪽 `AreaBarrage` 분기는 "arm 없음 — 패턴으로 이관됨" 경고 + skip 으로 바꾼다(조용한 no-op 금지, dc-trigger 선례).

### `BarrageEpicenter` 흡수

`PatternTargeting.Select(rule: RoundRobin)` 가 동일 동작이다. unit 0 에서 **두 함수의 결과 일치 테스트를 이미 작성했으므로**, 여기서 원본과 그 테스트를 삭제하고 이관 완료로 본다. 삭제 전 `grep -rn "BarrageEpicenter"` 로 잔여 참조 0 확인.

## 완료 기준

- 컴파일 클린. `grep -rn "AreaBarrage"` 결과가 enum 정의 + bake 거절 + 주석뿐(라이브 arm 0).
- **값 보존 Play 검증**: 10초 주기 · 방어유닛 진앙 round-robin 순회 · 1.5초 낙하 텔레그래프 · 150 데미지 · 반경 3 · 보스 자해 없음(`targetFaction=Defender`). 이관 전 스크린샷/로그와 대조한다.
- 방어유닛 0 마리 상황에서 발사가 소모되고 순회 위상이 보존된다(현행 `fireCount` 불변 규칙과 동일).
- EditMode: `BarrageEpicenterTests` 삭제분이 `PatternTargetingTests` 로 덮여 총 건수 감소 없음.
- 플레이어 Meteor 무회귀(같은 barrel 을 공유하는 경우 특히) — 캐스트 후 착탄·데미지 확인.
