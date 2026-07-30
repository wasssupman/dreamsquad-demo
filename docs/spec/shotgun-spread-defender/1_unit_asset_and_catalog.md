# 1 — 유닛/능력/투사체 에셋 + 카탈로그 등록

> 상태: 완료 (`376eeba0`). 아래 5발·사거리 2 수치는 최초 저작 이력이며, 현재 수치는
> `docs/spec/projectile-shot-sequence/2_defender_attack_pattern_cutover.md`가 우선한다.

## 목적

샷건너를 순수 데이터로 성립시킨다. 코드 변경 0.

## 변경 대상

- `Assets/_Project/Data/Projectiles/Projectile_ShotgunPellet.asset` (신규 — `Projectile_MachineGunBullet` 사본에서 시작)
- `Assets/_Project/Data/Abilities/Ability_Volley_Shotgunner.asset` (신규 `DirectionalVolleyAbility`)
- `Assets/_Project/Data/Defenders/Defender_Shotgunner.asset` (신규 `DefenderUnitData`)
- `Assets/_Project/Data/DefenderCatalog.asset` (units 배열에 등록)

## 구현

1. **펠릿**: MachineGunBullet 사본 → `flightMode Directional` 유지, **`pierceCount 1`**(탄당 1히트 — 근접 뭉침에서 단일 대상이 전탄을 받는 것이 샷건 정체성). 비주얼은 사본 그대로(placeholder 허용).
   - ⚠ **`pierceCount` 는 1이 "관통 없음"이다.** bake 가 `max(1, pierceCount)` 로 클램프하므로(`BattleBridge.cs:3432`) 0 을 적어도 1로 읽히고, 저작 의도만 오독된다. 머신건 탄과 같은 값이 맞다.
   - `minFlightTime` 은 BallisticToCell 전용이라 Directional 펠릿에 영향 없음 — 사본 값 유지.
2. **능력**: `shotCount 5` · `shotIntervalSec 0`(동프레임) · `spreadAngleDeg 90`. id 슬러그 `volley_shotgunner`.
3. **유닛**: id `shotgunner` · 표시명 "샷건너" · role Ranger · rarity Rare · cost 3 · HP 200 · attackRange 2 · attackCooldown 2.2 · hitDelaySec 0.3 · outputs `[Damage 12]`(탄당) · projectile = 펠릿 · abilities = [능력]. 배치 밀치기: `onPlacePushDistance 1.5` · `onPlacePushDuration 0.35` · `onPlacePushRadius 2`. `onPlaceEffect None`.
4. portrait/skeleton/deployVoice 는 기존 placeholder 재사용(guid 유지 교체 전제). `desc` 는 비워 `UnitKitSummary` 폴백 확인 후, 마음에 안 들면 한 줄 저작.
5. **카탈로그 등록** — 미등록 = 로스터 미노출.
6. `.meta` 짝 커밋 필수. 시트 `Defenders` 탭 행 추가는 커밋 후(임포터는 id 매칭 갱신만이라 시트에 없어도 SO 값 유지 — 무해).

## 완료 기준

- [x] compile clean + `DcApplicabilityMatrixTests`/`UnitKitSummaryTests` green.
- [x] 샷건너·능력·펠릿·카탈로그 등록.
- [x] 초기 5발 스프레드 통합 검증.
- [x] 현재 10발·4타일 계약으로 이관 및 사용자 확인(`projectile-shot-sequence`).
- [x] `defender-directional-volley`의 “스프레드 실증 유닛” 후속 항목 정리.
