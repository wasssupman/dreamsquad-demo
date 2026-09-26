# unified-effect-layer — 전수 표 (unit 0)

> 실측 2026-09-26 · 브랜치 `unified-effect-layer` · 방법 = `Assets/_Project/Data/**` YAML 파싱(스크립트 GUID 로 SO 종류 판별) + 코드 정독. 빌더를 실제로 돌린 것이 아니라 **정적 대조**다 — 굽힌 결과의 동치는 unit 5 빌더 스냅샷이 잰다.
> 칸 표기: `?` = 모름(사유 병기). 파일:줄은 이 커밋 기준.

## 약어

| 약어 | 뜻 | 코드 |
|---|---|---|
| `SP-대상` | `IntentApplier.SpawnProjectile` 대상 갈래 — 궤적 = 의도 명시 > 탄 정의 · `TileRange` = 재조준/방향 사거리 | `Trigger/IntentApplier.cs:258-272` |
| `SP-자리` | 같은 함수 자리 갈래 — **탄 정의 궤적을 무시하고 SkyFall × TileAoe 강제** · 반경·비행·예고는 의도에서 | `IntentApplier.cs:274-287`(강제 `:277`) |
| `PAT` | 발사 명세 버스트 슬롯 — 슬롯이 **바인딩을 든 자** 목록에 든다 | `IntentApplier.cs:302-304` → `Phases/CombatPhase.cs:963-979` |
| `FAN` | 전원 손잡이(`fanOutToAllCandidates`) — **대상 결합 탄에서만** 돈다 | `CombatPhase.cs:1055-1056` |
| `ORB` | 궤도 탄(궤적 강제 `OrbitAroundPoint`) | `IntentApplier.cs:469-488` |
| `SLAM` | 착지 슬램 — `CombatPhase` 가 직접 요청(자리형 · `SpawnProjectile` 경유 아님) | `CombatPhase.cs:1509-1520 · 1555-1566` |
| `MOD` | 공격 수식자로 접힘(규칙 줄 아님) | `CardDefinitionBuilder.cs:279-286` · `BindingDefinitionBuilder.cs:114-135` |
| `—` | 투사체 없음 | |
| 결합 | `CombatDefinitionBuilder.Translate`(`:405`) → `MovementBinding.Of`(`Combat/Projectile/ProjectileAxes.cs:86`). flightMode int: 0 Homing·3 BezierHoming·6 SkyFallOnTarget = **Entity** / 1 BallisticToCell·4 SkyFall·7 BallisticBlocker = **Cell** / 2 Directional·5 Boomerang = **Direction** | |

「무변」 열 = 그 행을 무변 단언해야 하는 unit(5 는 전 행 — 빌더 스냅샷이라 생략).

## 표 1 — 라이브 조합

### 1a. 카드 — Unit 33장(메커닉 32 = 규칙 28(PlacementAura 는 줄 2) · MOD 3 · 손패 선언 1 · + attackMod 카드 3)

| # | 에셋 | 트리거 | 효과 → concrete | 탄 · 결합 | 전원 | 경로 | 무변 |
|---|---|---|---|---|---|---|---|
| 1 | Card_PokeNeedle | AttackN(1) | ProjectileToTarget → TargetProjectile | NeedleFlame · Homing → Entity | — | SP-대상 | 1·2 |
| 2 | Card_Boomerang | AttackN(1) | ProjectileToTarget → TargetProjectile | Boomerang · Boomerang → Direction | — | SP-대상(방향) | 1·2 |
| 3 | Card_FrostArrow | AttackN(3) | ApplyCcToTarget(Stun) → TargetCc | — | — | — | 2 |
| 4 | Card_GaleShove | AttackN(4) | ApplyCcToTarget(Impulse) → TargetCc | — | — | — | 2 |
| 5 | Card_LullabyDart | AttackN(5) | ApplyCcToTarget(Sleep) → TargetCc | — | — | — | 2 |
| 6 | Card_EmberBite | AttackN(3) | ApplyStackToTarget → TargetStack | — | — | — | — |
| 7 | Card_Frostbite | AttackN(1) | ApplyStackToTarget → TargetStack | — | — | — | — |
| 8 | Card_Frenzy | AttackN(1) | SelfStatBuff(상한 10) → SelfStatBuff | — | — | — | — |
| 9 | Card_ExecutionStrike | AttackN(1) · HpBelow/EventTarget | HeavyStrike | — | — | MOD | — |
| 10 | Card_HeavyStrike | AttackN(5) | HeavyStrike | — | — | MOD | — |
| 11 | Card_CalamityHeart | AttackN(3) | HeavyStrike | — | — | MOD | — |
| 12 | Card_CalamityHeart | None | SelfBuffLethal → SelfBuffLethal | — | — | — | — |
| 13 | Card_CalamityHeart | OnDeath | SelfTileAoe → **DeathSiteBlast** | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 14 | Card_Farewell | OnDeath | SelfTileAoe → DeathSiteBlast | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 15 | Card_CorpseBurst | OnKill | SelfTileAoe → DeathSiteBlast(자리·몸 = 죽은 적) | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 16 | Card_SeveranceMeteor | OnRetire | SelfTileAoe → DeathSiteBlast(비워진 칸 · 몸 0) | Meteor · Homing(무시) | — | SP-자리 · 비행 0.8 · 예고 끔 | 1·2 |
| 17 | Card_CorneredBurst | OnDamagedN(1) · HpBelow/Self 0.3 | SelfTileAoe → SelfAreaBlast | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 18 | Card_ShieldBurst | OnShieldBreak | SelfTileAoe → SelfAreaBlast | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 19 | Card_TremorPlate | HealthThreshold(0.7) | SelfTileAoe → SelfAreaBlast | Meteor · Homing(무시) | — | SP-자리 · 비행 0 | 1·2·4★ |
| 20 | Card_ShieldLull | OnShieldBreak | AreaSleep → AreaSleep | — | — | — | 2 |
| 21 | Card_Thornmail | OnDamagedN(5) | NextAttackDoubleFire → GrantSelfCharge | — | — | — | — |
| 22 | Card_LastStand | HealthThreshold(0.7) | SelfStatBuff → **ThresholdSelfBuff** | — | — | — | — |
| 23 | Card_DevouringCraving | OnKill | SelfStatBuff → SelfStatBuff | — | — | — | — |
| 24 | Card_NightmareAfterglow | OnKill | SelfStatBuff → SelfStatBuff | — | — | — | — |
| 25 | Card_EmberField | OnKill | SpawnHazard(Hazard_Ember) → DeathSiteHazard | — | — | — | 2 |
| 26 | Card_FlameSpinner | PeriodicTimer(5) | SelfOrbitProjectile(구슬 2) → OrbitProjectile | FlameOrb · (강제 궤도) | — | ORB | 2 |
| 27 | Card_MothSwarm | PeriodicTimer(3) | EmitProjectilePattern → EmitPattern | Pattern_MothSwarm ⟨Moth · BezierHoming → Entity⟩ | 0 | PAT | 3 |
| 28 | Card_LastFlame | None | SelfBuffLethal | — | — | — | — |
| 29 | Card_ButterflyDream | None | DreamCocoon | — | — | — | — |
| 30 | Card_FattenedOffering | None(적 표식) | BountyMark | — | — | — | — |
| 31 | Card_SlowAwakening | None → **OnPlace · Any** 규칙 2(공속 · 수면) | PlacementAura → SelfStatBuff + PlacementSleep(코어 로컬) | — | — | — | — |
| 32 | Card_Handover | OnRetire | RecallAttachedToFront — 규칙 0(퇴근 회수 **선언**) | — | — | 손패 | — |
| 33 | Card_BouncyBead · Card_EyeOnTheEnd · Card_NightmareHunt | — | attackMod(Bounce · Frontmost · DamageVsSleeping) | — | — | MOD | — |

★ = unit 4 에서 **그림이 달라질 수 있는 행**(아래 「발견」 1).

### 1b. 카드 — Squad 13장 · Active 6장

| # | 에셋 | 트리거 | 효과 → concrete | 탄 · 결합 | 경로 | 무변 |
|---|---|---|---|---|---|---|
| 34 | Squad 13장(effects 15 — Fortress·CrackedGrail 2개씩) | OnPlace · Any(축 필터) | SelfStatBuff | — | — | — |
| 35 | Active_Meteor(Skill_Meteor) | 커맨드(지정 칸) | → TileMeteor · 예고 켬 · 비행 = warningSec 1.5 | Meteor · Homing(무시) | SP-자리 | 1·2·4 |
| 36 | Active_SlowField | 커맨드 | → TileStatBurst(MoveSpeed) | — | — | 2 |
| 37 | Active_PowerSurge · Active_RapidFire | 커맨드 | → AllyBuffField(Damage · AttackSpeed) | — | — | 2 |
| 38 | Active_Tornado | 커맨드 | → PullField | — | — | 2 |
| 39 | Active_Portal | 커맨드(두 칸) | → Portal | — | — | 2 |

### 1c. 유닛 능력 — `UnitSkillAbility` 17 + `ShieldCastAbility` 1 (OnPlace 17 · 주기 1 — 전부 자기 사건)

| # | 에셋 | 트리거 | 효과 → concrete | 탄 · 결합 | 전원 | 경로 | 무변 |
|---|---|---|---|---|---|---|---|
| 40 | Ability_SkyStrike_Cannon | OnPlace | EmitProjectilePattern → EmitPattern | **Pattern_Cannon_Strike ⟨CannonStrike · SkyFallOnTarget → Entity⟩ · scope 3 · 예고 0.4** | **1** | **PAT · FAN ★결합 분기** | 3·5 |
| 41 | Ability_OnPlaceShot_Sniper · _Marksman · _MachineGunner · _Piercer | OnPlace | EmitProjectilePattern → EmitPattern(방향 조준) | Pattern_*_OnPlace ⟨*_OnPlaceShot · Directional → Direction⟩ | 0 | PAT | 3 |
| 42 | Ability_OnPlaceBlast_Shotgunner | OnPlace | EmitProjectilePattern → EmitPattern(방향 조준) | Pattern_Shotgunner_Blast ⟨ShotgunBlast · Directional → Direction⟩ | 0 | PAT | 3 |
| 43 | Ability_UnitSkill_BombMan | OnPlace | EmitProjectilePattern → EmitPattern | Pattern_BombMan_Barrel ⟨Barrel · BallisticBlocker → **Cell**⟩ · scope 2 | 0 | PAT | 3 |
| 44 | Ability_MeleeBurst_Bruiser | OnPlace | SelfTileAoe → SelfAreaBlast | BruiserShock · Homing(무시) · 프리팹 없음 | — | SP-자리 · 비행 0 | 1·2 |
| 45 | Ability_Quake_Malphite | OnPlace | AreaCc → AreaCc | — | — | — | 2 |
| 46 | Ability_OpeningBeam_Busters | OnPlace | AreaDot → AreaDot | — | — | — | 2 |
| 47 | Ability_BleedBurst_Slasher | OnPlace | AreaApplyStack → AreaStack | — | — | — | 2 |
| 48 | Ability_Taunt_Bastion | OnPlace | AreaTaunt → AreaTaunt | — | — | — | 2 |
| 49 | Ability_AllyDamageAura_Guardian | OnPlace | AllyStatAura → AllyStatAura | — | — | — | 2 |
| 50 | Ability_SlowAura_Archer | OnPlace | OpponentStatAura → OpponentStatAura | — | — | — | 2 |
| 51 | Ability_AreaShield_ShieldShuttle | OnPlace | GrantShield(반경 2) → GrantShield | — | — | — | 2 |
| 52 | Ability_GainCost_Scout · Ability_ReduceCooldown_Ranger | OnPlace | GainCost · ReduceSkillCooldown → Meta | — | — | — | — |
| 53 | Ability_Shield_ShieldShuttle(`ShieldCastAbility`) | PeriodicTimer(cooldown) — 코드 bake `BindingDefinitionBuilder.cs:66-83` | GrantShield(사거리 · 자기 포함) | — | — | — | 2 |

규칙 레일 밖 능력 8(`CombatDefinitionBuilder` 평타 경로 · 이 spec 무관): Bomb_BombMan · Volley_MachineGunner · Volley_Shotgunner · SummonPatrol_Summoner · Hazard_{Blocking,Fire,Ice,Poison}Caster(**코어 소비 0** — 「발견」 4).

### 1d. 악몽 — `nightmareMechanics` 비지 않은 적 6(규칙 13)

| # | 에셋 | 트리거 | 효과 → concrete | 탄 · 결합 | 전원 | 경로 | 무변 |
|---|---|---|---|---|---|---|---|
| 54 | Enemy_Boss_Jjangssen | HealthThreshold(0.2) | SelfTileAoe → SelfAreaBlast | JjangssenQuake · Homing(무시) · 프리팹 없음 | — | SP-자리 · 비행 0 | 1·2 |
| 55 | Enemy_Boss_Jjangssen | HealthThreshold(0.9 · 0.5) | SelfBlink → BlinkToCluster | JjangssenLeap(연출) | — | SLAM(도약) | 2 |
| 56 | Enemy_Boss_Jjangssen | HealthThreshold(0.8) | UltimateLeap → UltimateLeap(fireCap 1) | JjangssenLeap | — | SLAM | 2 |
| 57 | Enemy_Boss_Mamemo | PeriodicTimer(3.5) | AreaSleep | — | — | — | 2 |
| 58 | Enemy_Boss_Mamemo | HealthThreshold(0.34) | GrantShield(자기) | — | — | — | 2 |
| 59 | Enemy_Boss_Mamemo | PeriodicTimer(2.5) | GrantShield(반경 4) | — | — | — | 2 |
| 60 | Enemy_Boss_Nightmare | PeriodicTimer(10) | EmitProjectilePattern | Pattern_NightmareBarrage ⟨NightmareBarrage · SkyFall → **Cell**⟩ · 예고 1.5 | 0 | PAT | 3 |
| 61 | Enemy_Boss_Nightmare | PeriodicTimer(0.1) | EmitProjectilePattern | Pattern_NightmareMissile ⟨NightmareMissile · BezierHoming → Entity⟩ | 0 | PAT | 3 |
| 62 | Enemy_Boss_Nightmare | PeriodicTimer(0.5) | AllyMoveSpeedAura → AllySpeedAura | — | — | — | 2 |
| 63 | Enemy_Dragon | AttackN(3) | AreaBreath → ConeBreath | — | — | — | 2 |
| 64 | Enemy_Slime · Enemy_Slime_Mid | OnDeath | SplitOnDeath — **의도적 무항목**(`BindingDefinitionBuilder.cs:104-112`) | — | — | — | — |

### 1e. 규칙 레일을 타는 비-`DcMechanic` 출처

| # | 출처 | 트리거 | 효과 | 경로 | 무변 |
|---|---|---|---|---|---|
| 65 | 드림스톤 64개(`Stone_*`) | OnPlace · Any · Match | DreamstoneStat(코어 로컬) | — | — |
| 66 | 기믹 레드불 · 온천 · 번아웃(`GimmickBindings.cs`) | PeriodicTimer(판 / 유닛) | `ICoreEffect`(스킬 아님) | — | — |
| 67 | 기믹 퇴근 — 사직서(`GimmickBindings.cs:69-76`) | OnDeath · Any · PlacedDefender | `ICoreEffect` | — | — |
| 68 | 기믹 퇴근 — 임계 운석(`ResignationBarrage.cs:63-94` · `Gimmick_ClockOut.asset:32`) | 사건 `ResignationThreshold` 소비(바인딩 아님) | `SpawnProjectile` 의도 직접 | **SP-자리** · Meteor · Homing(무시) · 비행 1.2+시차 · 예고 끔 | **1** |

**집계**: 저작 `DcMechanic` 62건(카드 32 · 유닛 17 · 악몽 13) → 서로 다른 (트리거 × 효과) **42쌍**(초안 「41」 정정). + Squad · 드림스톤 · 액티브 6 · 실드 캐스트 · 기믹 4 · 기믹 운석.
**남의 사건에 반응하는 바인딩**(`Subject.Any`)은 #31 · #34 · #65 · #67 뿐 — 전부 버스트 없음 → unit 3 「라이브 0」 성립.
**`SP-자리` 에 기대는 행 전부가 탄 정의 궤적이 Homing** 이다(Meteor · BruiserShock · JjangssenQuake) — 강제(`:277`)를 걷으면 의도가 궤적을 명시해야 한다(unit 1).

## 표 2 — concrete × 원점 읽기 (`Scripts/Skills/Concrete/` 29 파일 · 클래스 33)

「후보 거리」 = 후보 선정·정렬용 위치 읽기 — **원점 읽기 아님**(unit 2 가 안 건드린다).

| 파일(클래스 · Id) | 발사 자리 | 효과 좌표 | 원점 항 | Source(귀속) | 후보 거리 | 원점 읽음 |
|---|---|---|---|---|---|---|
| AllyBuffFieldSkill(30) | — | `target.CellA` `:29` → 장 운반체 | ? — 장 판정은 장 담당자(이 파일 밖) | 미기입 | — | ○ |
| AreaCcSkill(14) | — | `ctx.Position(caster)` `:27` | SelfArea = `caster.BodyRadius` | caster | — | ○ |
| AreaDotSkill(15) | — | `ctx.Position(caster)` `:25` | SelfArea | caster | — | ○ |
| AreaSleepSkill(1) | — | `ctx.Position(caster)` `:32` | SelfArea | **미기입**(ApplyCc) | distSq `:54` · 건너뛰기 `ReachFromUnit` `:81` | ○ |
| AreaStackSkill(13) | — | `ctx.Position(caster)` `:26` | SelfArea | caster | — | ○ |
| AreaTauntSkill(8) | — | `ctx.Position(caster)` `:35` | SelfArea | caster(어그로 주인) | — | ○ |
| BlinkToClusterSkill(5) | `ctx.Position(caster)` `:28`(연출 출발) | 밀집 셀 → 착지 칸(파생 `:23-25`) | 없음(이동) | caster | 밀집 탐색(컨텍스트) | ○ |
| BountyMarkSkill(27) | — | `target.Unit`(좌표 없음) | — | target 자신 | — | ✕ |
| CastHazardSkill(28) | — | `p.EventPosition` `:30` | ? — 장 담당자 | caster | — | ○ **미등록(죽은 파일)** |
| ConeBreathSkill(34) | `ctx.Position(caster)` `:37` | 방향 `target.DirectionXZ` `:31` | 후보 SelfArea · **콘 거리 컷은 몸 항 없음**(`SkillCone.cs:46`) | caster | 콘 판정 `:58` | ○ |
| DeathSiteBlastSkill(20) | = 효과 좌표 | `p.EventPosition` `:36` | `p.EventBodyRadius` `:40`(감지자 스냅샷 · 퇴근 0) | caster(OnKill = 킬러 · OnDeath/OnRetire = **없음**) | — | ○ |
| DeathSiteHazardSkill(21) | — | `CellOfPosition(p.EventPosition)` `:31` | ? — 장 담당자 | caster | — | ○ |
| DreamCocoonSkill(26) | — | 자기 | — | caster | — | ✕ |
| EmitPatternSkill(7) | `ctx.Position(caster)` `:64`(조준 필요 시만 실음 · Preaimed `:49` 는 없음) | 방향(바라봄 / 후보 `SkillAim`) | Euclidean 0(조준) | caster — ⚠ 버스트 Owner 는 **슬롯 주인**(H3) | 후보 `:86` | ○ |
| GrantSelfChargeSkill(23) | — | 자기 | — | caster | — | ✕ |
| GrantShieldSkill(3) | — | `ctx.Position(caster)` `:54`(+연출 `:49`) | SelfArea | caster(병합 키) | distSq `:100` | ○ |
| MetaSkills(GainCost 11 · ReduceSkillCooldown 12) | — | 판 밖 | — | — | — | ✕ |
| OrbitProjectileSkill(24) | 궤도 중심 `ctx.Position(caster)` `:32` | 같음 | 스윕 굵기(`HitThreshold`) — 원점 항 해당 없음 | caster | — | ○ |
| PullFieldSkill.cs(PullField 31 · Portal 32) | — | `target.CellA` `:25` · 입구/출구 `:53-54` | ? — 장 담당자 | 미기입 | — | ○ |
| SelfAreaBlastSkill(4) | = 효과 좌표 | `ctx.Position(caster)` `:26` | `caster.BodyRadius` `:28`(= 주인 `HitRadius`, `TriggerDispatcher.cs:494`) | caster | — | ○ |
| SelfBuffLethalSkill(25) | — | 자기 | — | caster | — | ✕ |
| SelfStatBuffSkill.cs(SelfStatBuff 18 · ThresholdSelfBuff 22) | — | 자기 | — | caster | — | ✕ |
| StatAuraSkill.cs(AllySpeedAura 2 · AllyStatAura 9 · OpponentStatAura 10) | — | `ctx.Position(caster)` `:53` | SelfArea | caster | — | ○ |
| TargetCcSkill(16) | — | `target.Unit`(+`DirectionXZ` Impulse `:39-40`) | — | caster | — | ✕(대상 엔티티만) |
| TargetProjectileSkill(19) | `ctx.Position(caster)` `:29` | `target.Unit` `:27` + `DirectionXZ` `:31` | 대상 결합 = 재조준 반경 · 방향 = 사거리 · 칸 결합 = 빌더 거절 | caster | — | ○ |
| TargetStackSkill(17) | — | `target.Unit` | — | caster | — | ✕(대상 엔티티만) |
| TileMeteorSkill(33) | = 효과 좌표 | `CellCenter(target.CellA)` `:27` | 0(미기입 → `ReachFromImpact` 칸 반폭) | caster(플레이어 = 없음) | — | ○ |
| TileStatBurstSkill(29) | — | `CellCenter(target.CellA)` `:23` | CellArea(칸 반폭) | 대상 자신 | — | ○ |
| UltimateLeapSkill(6) | `ctx.Position(caster)` `:57`(연출) | 착지 칸 `:43-44`(밀집 탐색 파생) | 슬램 = 자리형 0(`CombatPhase` SLAM) | caster | 밀집 탐색 | ○ |
| *(코어 로컬)* PlacementSleep(101) · DreamstoneStat(102) — `BattleCore/Trigger/CardSkills.cs` | — | 자기(= 사건 주체) | — | caster | — | ✕ |

**원점 읽음 ○ = 21 파일 / 29**(README·unit 2 의 「21/29」 확인 · 그중 `CastHazardSkill` 은 레지스트리 미등록). 읽는 자리 셋: `ctx.Position(caster)` 14 · `target.CellA` 4 · `p.EventPosition` 3.
**원점 항의 출처**: `caster.BodyRadius`(= 드레인이 사건 주체 `HitRadius` 로 채움) · `p.EventBodyRadius`(= 감지자 `SiteBody`) · 0(자리형). 드레인이 이미 `site = e.HasSite ? e.Site : e.SubjectPos` · `siteBody` 를 만든다(`TriggerDispatcher.cs:504-506`).

## 표 3 — 출처 × 거절 규칙

사유 분류: **원점** = 원점(또는 궤적 결합)을 못 낸다 · **수명** = 붙는 순간 이미 지난 자기 사건 / 사건 자체가 없다(영영 안 터짐) · **관례** = 한 출처에만 있는 블랙리스트 · **공통** = 두 빌더가 같은 함수로 거절(출처 무관 — unit 5 에서도 유지) · **값** = 저작 값 가드(3분류 밖 — unit 5 가 그대로 옮긴다).

### 3a. `Scripts/BattleCoreUnity/CardDefinitionBuilder.cs` (카드)

| 줄 | 거절 | 분류 | 유닛·악몽 빌더에서는 |
|---|---|---|---|
| 121 | 표식 카드는 표식 메커닉만 | 관례 | 해당 없음 |
| 215 | PlacementAura 카드당 하나 | 관례 | 비규칙(161) |
| 242 | 트리거 None × {4종 외} | 관례(부착 즉시 어휘 4종만) | None 자체 거절(98) |
| 255 · 256 | 인수인계는 OnRetire 만 · 게이트 없음 | 관례(손패 동작) | 비규칙(161) |
| **261** | **OnPlace 불가** | **수명**(자기 배치는 부착 전 — 「남의 배치」 주체 축이 생기면 풀린다 · unit 5) | 허용 |
| 263 | OnRetire × SelfTileAoe 외 불가 | 관례 | 허용 |
| 268 | 게이트 조합 미배선(`SkillRouting.GateComboSupported`) | 공통 | 121 · 168 |
| 274 | OnDamagedN × {SelfTileAoe · NextAttackDoubleFire} 외 불가 | 관례 | 허용 |
| 281 | HeavyStrike 는 AttackN 전용 | 공통 | 116 |
| **313** | **셀 결합 탄 × ProjectileToTarget** | **원점**(대상 갈래가 칸 결합에 착탄 반경·비행·예고를 안 채움 — H2 · unit 1 이 해소) | **검증 없음**(ProjectileToTarget 탄 검사 자체가 없다) |
| 332 | SpawnHazard 는 OnKill 만 | 관례 | 허용 |
| 339 | SelfOrbitProjectile 는 PeriodicTimer 만 | 관례 | 전면 거절(228) |
| **369** | **EmitProjectilePattern 은 PeriodicTimer 만** | **관례**(H5 · AA 를 막는 줄) | 허용 |
| 370 → `BindingDefinitionBuilder.cs:310-334` | 발사 명세 검증 | 공통 | 공통 |
| 374 | 감지자 없음(`SkillRouting.HasDetector`) | 공통(수명) | 137 |
| 375 | 라우팅 없음 | 공통 | 156 |
| 값 | 133-135 · 180 · 190-191 · 202-203 · 213 · 216 · 247 · 262 · 269 · 273 · 276 · 282(⚠ 유닛 빌더는 거절 대신 1 로 둔다 `:130`) · 309 · 314-316 · 326 · 334 · 340-342 · 351 · 354 · 357 · 361 · 363 | 값 | — |
| 액티브 `BakeActive` 461-463 | 모르는 `SkillEffectType` | 값(어휘) | 해당 없음 |

### 3b. `Scripts/BattleCoreUnity/BindingDefinitionBuilder.cs` (유닛 능력 · 악몽 · 실드 캐스트)

| 줄 | 거절 | 분류 | 카드 빌더에서는 |
|---|---|---|---|
| 98-101 | 트리거 None | 수명(유닛·적 규칙엔 부착 사건이 없다) | 부착 즉시 4종 허용 |
| 104-112 | SplitOnDeath — 의도적 무항목(값 검증만) | 값 | 해당 없음 |
| 116 | HeavyStrike AttackN 전용 | 공통 | 281 |
| 121 · 168 | 게이트 조합 | 공통 | 268 |
| 137 | 감지자 없음 — 적 × {OnPlace · OnRetire} | 수명(사건 자체가 없다) | 374 |
| 143 | 부착 즉시 전용 payload 를 트리거에 | 수명 | 해당 없음 |
| 148 | AreaBarrage — 발사 명세로 이관 | 관례(은퇴 어휘) | 라우팅 없음(375) |
| 156 · 161 | 라우팅 없음 · 비규칙 payload(PlacementAura · Recall 등) | 공통 · 관례 | 375 · 카드 전용 분기 |
| **228** | **SelfOrbitProjectile 전면 거절** | **관례**(bake 가 탄 필드를 안 채운 구현 공백) | 허용(339 PeriodicTimer) |
| **286-292** | **GrantShield (HealthThreshold ∧ 반경>0) ∨ ((PeriodicTimer ∨ OnPlace) ∧ 반경 0)** | **관례**(concrete 는 둘 다 받는다) | **검증 없음** |
| **297** | **AreaTaunt 는 가디언만** | **관례**(카드는 부착 적용성에도 가디언 검사 없음 — concrete 가 조용히 무동작 `AreaTauntSkill.cs:32`) | 검사 없음 |
| 322 | 전원 손잡이 × 비-Entity 결합 | 원점(칸 결합은 조준이 둘) | 공통(370) |
| 값 | 106-110 · 232 · 244 · 261-262 · 275-281 · 284 · 296 · 301 · 313 · 317 · 321 · 328 | 값 | — |

### 3c. 기믹 · 액티브 · 기타

| 파일:줄 | 내용 | 분류 |
|---|---|---|
| `BattleCore/Trigger/GimmickBindings.cs` | 거절 분기 없음 — 코드가 규칙을 직접 만든다(`Row` `:120-127`) | 해당 없음 |
| `BattleCoreUnity/MatchDefinitionBuilder.cs:766-767` | 퇴근 기믹 운석 탄 없음 → Error(굽기는 진행) · 코어 `ResignationBarrage.cs:52` 가 런타임에 버린다 | 값 |
| `CardDefinitionBuilder.cs:450 · 459` | 메테오 탄 없음 Error · 포탈 두 칸 꺼짐 Warn(굽기는 진행) | 값 |

**라이브 조합 중 거절되는 것 = 0**(SplitOnDeath 의도적 무항목 제외 · 표 1 대조). unit 5 에서 「관례」 거절이 풀려도 새로 굽히는 라이브 행은 없다.

## 발견 (unit 1~5 에 걸리는 것)

1. **unit 4 가 라이브 그림을 바꿀 수 있다(추정).** 비행 0 × `SkyFall` 탄은 같은 틱에 착탄·소멸해 오늘 뷰가 월드 탄을 못 찾는다(`CoreProjectileViewPool.cs:95-97`). `Projectile_Meteor` 는 `projectilePrefab` 이 있어 표 1 ★ 6행(#13-15 · #17-19)은 unit 4 뒤 **운석 그림이 새로 뜰 수 있다**. BruiserShock · JjangssenQuake 는 프리팹이 없어 무변. → unit 4 완료 기준에 확인 항목 추가.
2. **`ResignationBarrage` 도 `SP-자리` 강제에 기댄다**(탄 = `Projectile_Meteor` · Homing). unit 1 변경 대상에 없었다 → 추가.
3. **브레스 콘 사거리가 인라인 자다** — `SkillCone.IsInCone` 거리 컷(`SkillCone.cs:46`)에 원점·대상 몸 항이 없다(후보 질의는 정본 `ReachWithOrigin`). 라이브 = 드래곤(#63). 고치면 규칙이 바뀐다(계약 6 위반) → README 후속 후보.
4. **`HazardCastAbility` 4개가 코어 소비 0**(`UnitKitSummary` 만 읽는다 · `CastHazardSkill` 레지스트리 미등록). 이 spec 범위 밖 — 기록만.
5. **OnDeath · OnRetire 자리 폭발은 귀속이 없다**(사건 주체가 사라져 `caster.Unit = None` — `TriggerDispatcher.cs:493-499`). 계약 4 「귀속 = 발동 주체」는 오늘도 그대로이며(주체가 이미 없다) 무변.
6. **「유닛 능력 26」은 능력 에셋 전체 수다.** 규칙 레일을 타는 것은 `UnitSkillAbility` 17 + `ShieldCastAbility` 1 = 18(나머지 8 은 `CombatDefinitionBuilder` 평타 경로) → unit 5 · README 수치 정정.
