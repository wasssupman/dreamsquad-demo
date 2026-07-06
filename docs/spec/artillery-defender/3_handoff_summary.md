# 3 — Handoff Summary

> 곡사포 유닛 완료 2026-07-06. projectile-trajectory-payload 리팩터의 **첫 실증 (Play OK)**.

## Commit

- (이 커밋) 곡사포 유닛 — `Projectile_ArtilleryShell` + `Defender_Artillery` + DefenderCatalog 등록.
- 선행 엔진: projectile-trajectory-payload `e5836bc`~`b84f2da`.

## Implemented

- **`Projectile_ArtilleryShell.asset`** — Rock 비주얼(`vfx_Projectile_Rock01`, GA `Projectile_Rock_GA` 의 prefab/hit/cast 재사용) + `flightMode=BallisticToCell`, arcHeight 3.5, impactTileRange 2(5×5 AOE), speed 6, minFlightTime 0.3.
- **`Defender_Artillery.asset`** — id=artillery, range 7·cooldown 3.5·health 350·cost 5, outputs=Damage 60, projectile=shell, **Cannon Spine/mat/placementVfx 재사용**.
- **DefenderCatalog.units** 에 등록(16개). 테스트용으로 플레이어 프로필 `ownedUnitIds` 에 수동 추가.

## Key Files

- `Assets/_Project/Data/Projectiles/Projectile_ArtilleryShell.asset`
- `Assets/_Project/Data/Defenders/Defender_Artillery.asset`
- `Assets/_Project/Data/DefenderCatalog.asset`
- 스펙: `docs/spec/artillery-defender/README.md` (결정/스탯 source of truth)

## Verified

- Play e2e (사용자 육안 2026-07-06): **arc 비행 + 5×5 반경 AOE + 셀 낙하** OK. 곡사 view-공간 arc 렌더(선행 spec b84f2da) 반영 확인. 스탯 느낌 OK.

## Notes

- **몸체 = Cannon Spine 재사용** — 전용 rig 은 후속(Healer 선례와 동일 패턴).
- DefenderUnitData 에 **아이콘 필드 없음** — 드래프트는 Spine/이름 표시, 아이콘 저작 불필요.
- 데미지는 outputs(Damage) 합산 → 엔진 TileAoe 가 flat AOE. 새 데미지 필드 없음.
- 스탯은 초기값(육안 통과) — 밸런싱은 별도.

## Follow-up

- **[결정 대기] ProfileStore 신규 유닛 reconcile** — `LoadOrCreate` 가 기존 저장 프로필을 그대로 로드해 카탈로그 신규 유닛을 안 보태므로, 새 유닛이 기존 플레이어에게 안 열린다. 지금은 프로필 수동 패치로 우회. 프로토 = 전체 보유면 로드 시 카탈로그 reconcile 한 줄이 정답(진행/획득 정책 결정). artillery 스펙 밖 → `docs/spec/README.md` 백로그.
- slow-곡사포(착탄 slow) · 임팩트 knockback · arcHeight 거리비례 · 전용 Spine rig.
