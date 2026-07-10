# 7 — Handoff Summary (완료 2026-07-10)

## Commit

spec `888d420d`(0~6 작성) → rev 2 정합 `7a752656`. 구현: `63507a10`(u0 enum) `507dc1e5`(u1 위협테이블) `d0a92c12`(u4 진영게이트) `a8b47381`(u5 스폰베이크) `710dc230`(u2 융단폭격) `194bbe8b`(u3 텔레포트) + u6 보스콘텐츠/rev3(이 커밋).

## Implemented

- **편입 구조**: 보스 능동 스킬 = 새 시스템이 아니라 드림캐쳐 `trigger×payload` enum+arm 확장. `nightmareMechanics` 선언한 적이 스폰 베이크 분기에서 보스가 됨(BossTag+ThreatEntry+DcTriggerSlot).
- **융단폭격** = `PeriodicTimer(10s)` × `AreaBarrage(r3,10dmg)`: 슬롯 accumulator 틱 → 결정론 진앙(row-major round-robin) → SkyFall×TileAoe 캐리어 발사. `targetFaction=Defender` 로 방어유닛 풀 타격(보스 자해 없음).
- **텔레포트** = `HealthThreshold(30%마다, 래치)` × `SelfBlink`: 위협 리더(원거리 포함 귀속)→최근접→skip 폴백. Combat 판정 → Movement 위치 대입(seam). 출발/도착 퍼프 연출.
- **위협 테이블**: 투사체 `owner` 귀속으로 근접+원거리 모두 집계. ThreatHit 채널 → BossHealthThresholdSystem 상단 드레인.
- **각성 경제 자동 편입**: 보스 처치 reward 5 / 폭격이 defender 죽이면 각성+4·카드 회수(신규 코드 0).

## Key Files

- `Battle/Combat/`: `ThreatTable.cs`(버퍼+채널+Leader/Accumulate/TryCredit) · `BossTag.cs` · `DcTrigger.cs`(PeriodicTick/HealthThresholdEval) · `BarrageEpicenter.cs` · `BlinkMath.cs` · `BossPeriodicTriggerSystem.cs` · `BossHealthThresholdSystem.cs`
- `Battle/Movement/`: `BlinkRequestEvents.cs` · `BlinkApplySystem.cs`(위치 쓰기 유일점)
- `DcTriggerSlot.cs`(periodic/threshold 상태 append) · `ProjectileState/SpawnRequest.cs`(owner+targetFaction) · `AttackUnitData.cs`(nightmareMechanics) · `BattleBridge.cs`(BakeNightmareMechanics + 채널 3 lifecycle)
- 에셋: `Enemy_Boss_Nightmare` · `Projectile_BlinkPuff` · `WavePlan_BossTest`

## Verified

- 컴파일 클린 · EditMode 640/642 그린(nightmare-catcher 신규 28: PeriodicTick4·Epicenter4·HealthThreshold6·BlinkMath7·ThreatTable7). 사전 skip 2는 무관.
- 렌즈 B 2회(1·4·5 / 2·3 arm) 전부 **PASS** — 맥락경계·큐 lifecycle·시스템순서·Burst·결정론·teardown.
- 사용자 Play e2e 2026-07-10: 폭격 데미지 정상·텔레포트·blink 연출 확인.

## Notes (되돌리면 안 되는 의도)

- **`ProjectileTargetFaction` zero=Enemy** 가 무회귀의 뿌리 — 기본값이 곧 기존 경로 byte-identical(N3). 보스 폭격 arm 만 Defender setter.
- **투사체 `owner`=Null(bridge 캐스트 스킬)** 은 위협 무귀속 의도 — 플레이어 Meteor 가 보스를 때려도 텔레포트 타겟 안 흔듦.
- **BlinkMath 폴백축 = world -Z 컴파일 상수** — 런타임 파생축 금지(NaN 재발). 링 순회 maxRing 상한.
- **GA hitPrefab = Muzzle 지목 함정** — blink 퍼프는 `vfx_Hit_*` 실물로 배정(오프스크린 픽셀검증). 재발 시 같은 방식.
- 슬롯 상태는 `DcTriggerSlot` 필드 append(병렬 slot 타입 아님) — 버퍼 존재 게이트가 편입 계약.

## Follow-up

- **`enemy-hunter-targeting` (신규 spec, 사용자 확정)** — 보스가 타겟 없으면 goal 로 누수. 방어유닛 존재 시 최근접 추격(BossTag 전용). 별도 진행 중.
- 보스 어그로 저항/면역(BossTag 게이트 1줄) · GA hitPrefab 전수 정비 · 폭격 피격 체감 연출(DamageNumber 진영 개방) · 라이브 웨이브 보스 편성 규칙 · 렌즈 B M1(투사체 1프레임 지연, 60fps 비가시).
- 튜닝 계수 실측(폭격 주기·텔레포트 빈도·보스 HP) — config/SO.
