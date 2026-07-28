# 1 — 유닛/능력/투사체 에셋 + 카탈로그 등록

## 목적

샷건너를 순수 데이터로 성립시킨다. 코드 변경 0.

## 변경 대상

- `Assets/_Project/Data/Projectiles/Projectile_ShotgunPellet.asset` (신규 — `Projectile_MachineGunBullet` 사본에서 시작)
- `Assets/_Project/Data/Abilities/Ability_Volley_Shotgunner.asset` (신규 `DirectionalVolleyAbility`)
- `Assets/_Project/Data/Defenders/Defender_Shotgunner.asset` (신규 `DefenderUnitData`)
- `Assets/_Project/Data/DefenderCatalog.asset` (units 배열에 등록)

## 구현

1. **펠릿**: MachineGunBullet 사본 → `flightMode Directional` 유지, `pierceCount 0`(탄당 1히트 — 근접 뭉침에서 단일 대상이 전탄을 받는 것이 샷건 정체성). 속도/수명은 사거리 2 기준으로 사본 값에서 조정. 비주얼은 사본 그대로(placeholder 허용).
2. **능력**: `shotCount 5` · `shotIntervalSec 0`(동프레임) · `spreadAngleDeg 90`. id 슬러그 `volley_shotgunner`.
3. **유닛**: id `shotgunner` · 표시명 "샷건너" · role Ranger · rarity Rare · cost 3 · HP 200 · attackRange 2 · attackCooldown 2.2 · hitDelaySec 0.3 · outputs `[Damage 12]`(탄당) · projectile = 펠릿 · abilities = [능력]. 배치 밀치기: `onPlacePushDistance 1.5` · `onPlacePushDuration 0.35` · `onPlacePushRadius 2`. `onPlaceEffect None`.
4. portrait/skeleton/deployVoice 는 기존 placeholder 재사용(guid 유지 교체 전제). `desc` 는 비워 `UnitKitSummary` 폴백 확인 후, 마음에 안 들면 한 줄 저작.
5. **카탈로그 등록** — 미등록 = 로스터 미노출.
6. `.meta` 짝 커밋 필수. 시트 `Defenders` 탭 행 추가는 커밋 후(임포터는 id 매칭 갱신만이라 시트에 없어도 SO 값 유지 — 무해).

## 완료 기준

- [ ] compile clean + `DcApplicabilityMatrixTests`/`UnitKitSummaryTests` green (Data/Defenders 전수 스캔에 자동 편입)
- [ ] 에디터 Play: 로스터에 샷건너 노출 → 배치 → 방향 지정 페이즈 진입(머신거너 문법) → 확정
- [ ] 정면 레인에 적 진입 시 **한 프레임에 부채꼴 5발** 발사 육안 확인 + 배치 순간 주변 적 밀쳐짐 확인
- [ ] 사용자 Play 확인: 부채꼴 발사가 샷건답게 읽히는지 (탄속/크기 튜닝 피드백 수집)
- [ ] 통과 시 `defender-directional-volley` README 후속 후보의 "스프레드 실증 유닛" 줄을 본 spec 링크로 대체
