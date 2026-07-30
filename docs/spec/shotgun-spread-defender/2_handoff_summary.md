# 2 — handoff summary

> 초기 5발 구현 인계 기록. 현재 계약과 후속 작업은
> `docs/spec/projectile-shot-sequence/README.md` 및 `4_handoff_summary.md`를 우선한다.

## Commit

- `bfbc8387` test(shotgun-spread-defender): unit 0 — 스프레드 통합 테스트 2케이스 추가
- `376eeba0` feat(shotgun-spread-defender): unit 1 — 샷건너 유닛/능력/펠릿 에셋 + 카탈로그 등록

## Implemented

- 샷건너(`shotgunner`) — Ranger·Rare·코스트 3, 사거리 2 · 쿨다운 2.2 · 탄당 12(풀히트 60)
- `Ability_Volley_Shotgunner`: 5발 / `shotIntervalSec 0`(동프레임) / 90° 부채꼴
- `Projectile_ShotgunPellet`: MachineGunBullet 사본, Directional · pierce 1 · visualScale 0.7
- 배치 밀치기 = `onPlacePush*` 재사용(1.5 / 0.35 / r2) — 코드 0
- 통합 테스트 2건: 5발×90° 부호각 전수(−45/−22.5/0/+22.5/+45), 동프레임 볼리 쿨다운 무연장
- **신규 시뮬 코드 0** — 스프레드 엔진은 directional-volley 가 이미 완성해 둔 것을 처음 소비

## Key Files

- `Assets/_Project/Data/Defenders/Defender_Shotgunner.asset`
- `Assets/_Project/Data/Abilities/Ability_Volley_Shotgunner.asset`
- `Assets/_Project/Data/Projectiles/Projectile_ShotgunPellet.asset`
- `Assets/_Project/Tests/EditMode/DirectionalVolleyIntegrationTests.cs` (케이스 2건 추가)
- 현재 스케줄러: `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/`
  (`EmitterTick`·`ShotOrder`; 초기 `VolleyMath`는 후속 spec에서 제거됨)

## Verified

- 리그 EditMode `DirectionalVolleyIntegrationTests` 10/10 (기존 8건 무회귀)
- 신규 유닛 전수 스캔 28/28 (`DcApplicabilityMatrixTests` · `UnitKitSummaryTests`)
- 에디터 execute_code: `RequiresFacing=True`, ability 5/0/90, projectile Directional·pierce 1 확인

## Notes (되돌리지 말 것)

- **`pierceCount` 는 1이 "관통 없음"이다.** bake 가 `max(1, pierceCount)` 로 클램프하므로(`BattleBridge.cs`) 0 을 적으면 1로 읽히고 저작 의도만 오독된다.
- `shotIntervalSec 0` = 동프레임 전탄 계약(`VolleyMath.TickBurst` 의 `intervalSec <= 0` arm). 쿨다운 연장도 0.
- 레인 게이트는 확산각과 무관하게 정면 폭 1타일 유지 — 콘은 발사 후 탄의 진행일 뿐이다.
- `desc` 를 authored 로 둔 이유: `UnitKitSummary` 폴백이 `shotCount>1` 을 무조건 "N연발"로 써서 동프레임 부채꼴을 시간차 연발로 오독한다(README 후속 후보).

## Follow-up

- 초기 5발 체감 확인은 10발·4타일 개편과 사용자 확인으로 대체됐다.
- 남은 콘텐츠 후보는 README의 거리감쇠·전용 아트 패스 참조.
