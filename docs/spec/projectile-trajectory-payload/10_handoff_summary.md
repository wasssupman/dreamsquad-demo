# 10 — Handoff Summary (Meteor 라운드, units 7~9)

## Commit

- `76550e0` spec units 7~9 (critic rev2) · `3d69664` unit 7 수렴 · `d619b99` unit 8 레거시 삭제 · `48b2cb6` 스킬 aim 격자(range-preview unit 3, 이 라운드에서 파생) · (unit 9) GA 비주얼 — 이 커밋

## Implemented

- Meteor = 단일 투사체 라이프사이클(SkyFall×TileAoe). 전용 시스템/큐/캐리어 삭제, NativeQueue 채널 15→14.
- 스폰 seam = `BattleBridge.ApplyMeteor` 직접(`SpawnProjectile(req, Entity.Null)` → Entity 반환). 데미지 = `skill.magnitude` 스냅샷(계약 6 skill 조항).
- 스킬 aim/텔레그래프 = 배치와 동일 격자(`SetPlacementRange` 재사용, owner 게이트: Placement/SkillAim/SkillTelegraph). 빨간 쿼드 삭제.
- 텔레그래프 해제 = `ProjectileHitEvent.source` 엔티티 매칭(버전 안전, hitPrefab 유무 무관).
- 비주얼: 낙하 `Rock02`(scale 1.3, dropHeight 9 — 화면 밖 등장, fallPortion 0.35 — 후반 압축+대기 숨김) · 임팩트 vendor `Hit_Rock03`(hitVfxScale 2.5). `MeteorFall`/`SpawnMeteorFall`/`meteorFallPrefab` 은퇴.

## Key Files

- `Battle/Combat/Projectile/{SkyFall,MovementKind,ProjectileHitEvent,ProjectileHitSystem,ProjectileSpawnRequest,ProjectileState}.cs`
- `Bridge/BattleBridge.cs` (ApplyMeteor·SpawnProjectile·DrainProjectileHitEvents·range owner 게이트)
- `Presentation/ProjectileViewPool.cs` (SkyFall 낙하 렌더+숨김/reveal ResetVfx) · `UI/SkillBar.cs` (aim 추종)
- `Data/Projectiles/Projectile_Meteor.asset` · `Data/Skills/Skill_Meteor.asset`

## Verified

- 리그 EditMode 510/513(실패 1 = 알려진 무관 ObstaclePlacer) · SkyFallTests 15 (Progress/Arrived/FallProgress 경계+가드).
- MCP Play: 데미지 120→80(=40 정확)·MeteorPending 0·투사체 소멸·텔레그래프 owner 게이트/source 해제 실측·콘솔 클린.
- 투트랙 리뷰 3회(unit 7 APPROVE / unit 9 양측 APPROVE, M1 ResetVfx 반영). 사용자 육안 확정(낙하 느낌 튜닝 2회 반영).

## Notes (되돌리면 안 되는 것 / 경계)

- **SkyFall 의 arcHeight state 슬롯 = 낙하 시작 높이**(semantic overload, movement 로 dispatch — 주석 유지).
- **낙하/속도감은 뷰 전용**(`dropHeight`/`fallPortion`) — warningSec(게임플레이 예고·데미지 타이밍)와 독립. 균형 조정은 warningSec, 연출 조정은 SO 두 필드.
- **단일 텔레그래프 슬롯 가정**: meteor cooldown 18s ≫ flight 1.5s. 낙하 스킬 다중화 시 Dictionary 확장 필요.
- 임팩트 라우팅: hitPrefab 우선, prefab-less TileAoe 는 legacy `SpawnMeteorBurst` 폴백(현재 소비자 0 — 의도적 유지).
- 평면 보드에서 "하늘" = view-Y(화면 위쪽) — dropHeight 가 작으면 등장 팝인이 화면 안에 보인다(9 로 은폐). lessons/03 sim-Y 참조.

## Follow-up

- 낙하 직전 착탄 셀 프리플래시(예열 연출) [S] — 후보로만.
- `Homing+TileAoe` / non-Damage payload / Bezier — 기존 backlog 항목 유지.
- GA 투사체 최종화(디펜더별 변종 선택) 시 Rock02/Hit_Rock03 은 meteor 예약.
