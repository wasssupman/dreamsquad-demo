# 5 — Handoff Summary (dreamcatcher-content-2)

## Commit

- `a1ef1e23` unit 0 — Frontmost 정의 계층 (enum/lock/투사체 inert 필드) + README 개정
- `ee9d567a` unit 1 — 순수 FrontmostTargeting 랭커 + EditMode + Bridge per-kind bake
- `067f2743` unit 2 — AttackSystem frontmost 선택·잠금(START/RESOLVE, 엄격 lapse, ecs-review M1 반영)
- `f14f21af` unit 3 — priority 직접 피해 +20% + Threat 동기

## Implemented (units 0~3, 전부 검증·커밋)

- `DcAttackModKind.FrontmostTarget` append. `FrontmostAttackLock`(active/target/damageMulSnapshot/targetIsPriority) Combat 컴포넌트. `ProjectileSpawnRequest`/`ProjectileState`에 `priorityTarget`/`priorityDamageMul` inert 필드.
- 순수 `FrontmostTargeting`(flowDist↑→sqDist↑→entity idx/ver↑, unreachable 제외) + EditMode 6종.
- Bridge attackMod 검증 per-kind 분기: FrontmostTarget은 `damageMul>0`+양수 Damage output 요구(힐러/output없는 caster 거절), `FrontmostAttackLock` 최초 1회 add. 전역 `count>0` guard 제거.
- AttackSystem: 기존 후보 루프에 단일 패스 frontmost 추적, PastGoal·unreachable 제외. 공격 단위 START lock/RESOLVE, **엄격 lapse**(사망·despawn·범위밖·PastGoal 재선택 없음), 매 resolve 해제. 가디언 primary 강제(swap-to-primary, 중복 방지).
- priority 피해: melee primary만 ×snapshot, 투사체 direct/bounce/TileAoe victim==priority만 ×mul(splash secondary는 priority여도 base). IncomingDamage와 `ThreatTable.TryCredit`에 동일 dmg → threat desync 구조적 불가.
- **악몽의 여운은 production C# 0** — 기존 OnKill×SelfStatBuff arm + AttackDamage buffStat bake 재사용(kill-and-threshold 선례). unit 4에서 SO만 만들면 됨.

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs`, `FrontmostAttackLock.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (선택·잠금·melee priority)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (victim priority)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` (per-kind bake), `BattleBridge.cs`:2308 (drain 복사)
- 테스트: `FrontmostTargetingTests`, `FrontmostAttackLockTests`, `ProjectileSystemTests`(priority 4종)

## Verified

- EditMode(rig batchmode, wassup-testrig): **total 743 / passed 741 / failed 0 / skipped 2**(기존 ignored). 신규 frontmost/priority 20종 전부 green.
- compile green(units 0~2 라이브 에디터, unit 3 rig). ecs-review(unit 2): CRITICAL/HIGH 0, MEDIUM M1(가디언 중복) 반영.

## Notes (되돌리면 안 되는 의도)

- **엄격 lapse**(사용자 결정): 잠긴 대상 사망/despawn/범위밖/PastGoal → 재선택 없이 헛방. reselect 금지.
- **시트 = catalog-only**(사용자 결정): DcCards/DcMechanics/DcAttackMods 행 추가·roundtrip 안 함(kill-and-threshold 선례). 시트 baseline drift(`AwakeningConfig` 20/20/20/4 vs 스냅샷 30/15/5)는 이 spec 스코프 밖 → `dreamcatcher-sheet-sync` 후속.
- Threat는 IncomingDamage와 **동일 변수**로 적용(별도 계산 금지 — desync 방지).
- 가디언 primary 강제는 swap-to-primary(overwrite 아님) — SelectTargets 원 primary를 secondary로 보존.

## Follow-up (미완)

- **unit 4 (에디터+아트 필요)**: 카드 SO 2종(스펙=`4_card_assets.md`, enum 값 검증됨) + catalog 등록 + 아트 `dreamcatcher_card_21/22.png`(1024×1536, 2:3). 아트는 아티스트 산출물. **MCP(라이브 에디터) 끊긴 상태로 미착수** — 복구(사용자가 Unity 창 포커스) 후 저작+Play e2e 검증.
- **Play e2e**: 악몽의 여운 refresh/expiry, 끝을 보는 눈 flow-타겟 선택·+20%, 무카드 무회귀(모두 EditMode로 대리 검증됨, 최종은 실제 SO Play).
- 시트 완전 미러링(eye DcAttackMods 행, Afterglow buffStat 컬럼) → `dreamcatcher-sheet-sync` 확장.
