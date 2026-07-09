# 4 — Handoff Summary

> dreamcatcher-content-1 (신규 드림캐쳐 3종) 종료 인계. 최신 계약은 README/번호 문서, 구현 상세는 코드/커밋 우선.

## Commit

- `54c159e6` / `25841d25` / `b69594b9` spec (설계 critic CRITICAL2+HIGH5 반영 + LethalTimer Units 재배치 + DamagedCounter 버퍼화)
- `8c3d1c12` unit 0 — 어휘(enum/필드/컴포넌트)
- `90cc4f56` unit 1 — ② 작별 선물 (OnDeath×SelfTileAoe)
- `ca27d42f` unit 2 — ③ 마지막 불꽃 (SelfBuffLethal 카미카제)
- `2a790a7a` unit 3 — ① 가시 갑옷 (OnDamagedN×NextAttackDoubleFire)

## Implemented

- **② 작별 선물**(`Card_Farewell`): 사망 시 2타일 100 폭발. UnitLifecycleSystem 이 파괴 전 OnDeath 슬롯 RO 읽어 `DefenderDeathEvent` 에 베이크 → bridge 가 `SpawnProjectile(TileAoe, Entity.Null)` 즉발(파괴 엔티티 미접근, critic C1).
- **③ 마지막 불꽃**(`Card_LastFlame`): 부착 즉시 공속+90% 5s + 종료 시 자폭. 부착 가드 재구조화(trigger=None 허용, C2). `LethalTimerSystem`(Units, UpdateBefore DamageApplication, WithNone<DeadTag>) 만료 시 DeadTag → 기존 사망 경로.
- **① 가시 갑옷**(`Card_Thornmail`): 5회 피격 시 다음 공격 2연발. `DamagedCounter`(Units buffer, DcTriggerSlot 아님=맥락경계 H1) 를 DamageApplicationSystem 이 tick → `NextAttackDoubleFire`(Combat 채널, IncomingDamage 역방향) 발화 → AttackSystem 이 **쿨다운0 자연 2연발**로 소비(RESOLVE 복제 없이 H3/H4 회피).
- **의도된 콤보**: ③ 자폭이 ②를 달아 자폭 위치 폭발(어휘 재조합, 코드 0).

## Key Files

- `Data/Dreamcatcher/DcMechanic.cs` — enum(OnDamagedN/OnDeath, SelfTileAoe/NextAttackDoubleFire/SelfBuffLethal) + DcPayloadSpec(tileRange/duration)
- `Battle/Units/DamagedCounter.cs`(buffer) · `LethalTimer.cs` · `LethalTimerSystem.cs` · `DefenderDeathEvent.cs`(확장) · `UnitLifecycleSystem.cs`(OnDeath 베이크) · `DamageApplicationSystem.cs`(피격 tick)
- `Battle/Combat/NextAttackDoubleFire.cs` · `DcTriggerSlot.cs`(tileRange) · `AttackSystem.cs`(더블파이어 소비)
- `Bridge/BattleBridge.cs` — 부착(SelfBuffLethal 즉발 / OnDamagedN→DamagedCounter / SelfTileAoe 슬롯) + DrainDefenderDeathEvents 폭발
- `Data/Dreamcatcher/Card_Farewell|LastFlame|Thornmail.asset`

## Verified

- 컴파일 클린. EditMode 588 그린(회귀 0). 3장 각 구조 Play 검증(부착 베이크·사망 폭발 무예외·자폭 5s 보드제거·DamagedCounter 랩어라운드+charge 소비).
- **impl ecs-review: CRITICAL 0, HIGH 0** — SpawnProjectile(Null) 안전·더블파이어 정확히 1발·LethalTimer 이중 DeadTag 불가·tick 위치·enum 무회귀·OnDeath 격리 전부 CONFIRMED.

## Notes (되돌리면 안 되는 의도)

- **DamagedCounter=Units 소유**(DcTriggerSlot 아님), **LethalTimer=Units**(Effects 아님) — 맥락 경계. 되돌리면 위반.
- **더블파이어=쿨다운0 방식** — RESOLVE 2회 복제 금지(H3 request 충돌/H4 CC·틱 중복). isDefenderStart 가드 필수.
- **OnDeath 페이로드는 파괴 전 이벤트 베이크** — bridge 드레인 시점 엔티티 파괴됨. drain 에서 슬롯 읽기 금지.
- NextAttackDoubleFire=Combat 채널(Units 생산/Combat 소비), IncomingDamage 역방향 선례.

## Follow-up (README 후속 후보)

바인딩/회수 UX · TRD IComponentData 핸드오프 명문화(M1) · SelfBuffLethal 중복 가드(M2) · 다중 DamagedCounter 누적(M3) · 신규 시스템 EditMode 테스트(M4) · non-Damage SelfTileAoe · OnDamagedN DoT 개별 카운트 · 카드 아트/전용 VFX. **적 피격 시각 e2e(폭발/2연발 육안)는 사용자 포커스 Play 대기.**
