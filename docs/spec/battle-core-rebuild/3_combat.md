# 3 — 전투 판정 (조각 A)

## 목적

유닛이 **때리고, 맞고, 죽는다.** 통합 공격 루프(START/RESOLVE) · 도달 산식(제약 13) · 타겟팅과 락 · 방향 도형 · 투사체(궤적 × 페이로드) · 발사 명세 · 피해 인박스 · 실드 흡수 · 킬 귀속 · 사망 2단계 · 넉백/넉업/수면 부여 · 도약(보스·궁극기) · 방어유닛/적 행동 상태(UnitAi). 규칙 정본 = `ledgers/rules.md` 전투 판정 절(필수 18) + `census-combat.md`(88행). 상태이상의 **적용·틱**(모디파이어·CC·DoT 슬롯 자체)은 unit 6 — 이 unit 은 「누가 무엇을 얼마나」를 정해 인박스에 넣는 데까지다. 이 unit 이 끝나면 조각 A 의 검증 질문(「헤드리스로 3분 판 완주」)에 답할 수 있다(웨이브는 DebugSpawn 스케줄로 대체, unit 4 가 진짜 웨이브).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 개체 부분 | `Unit.Attack`(`AttackState`: targetMask · targetLayers · interval · hitDelay · cooldown · committedTarget/Direction · lock · shape · patternSlots · outputs) · `Unit.Ai`(`AiStatus` — UnitAi 결정 저장) · `Unit.Shield`(`ShieldSlots`) · `Unit.Inbox`(`IncomingDamage[]`·`IncomingHeal[]`·`IncomingShield[]`) · `Unit.Progressive`(`UltimateLeap`·`LethalTimer`·`Charge` — 중단 정책 표 동봉) |
| 개체 신설 | `Projectile`(궤적 `MovementKind`×바인딩 3 · 페이로드 4 · 관통 예산 · 재타격 기록 · 재조준 반경 · 원점 몸 반경) |
| phase | `TickProjectilePhase`(투사체 이동/착탄 · 스탯 만료/집계 자리는 unit 6) · `CombatPhase`(공격 → **[Attack seam 자리]** → 피해 → **[Death seam 자리]** → 후처리(넉업·기상·발사기·배럴·CC 감쇠 자리) → 소멸 → **[Lifecycle seam 자리]** → 경계 → 궁극기 · 순간이동) — seam 은 unit 7 이 채운다, 이 unit 은 **훅 위치만** 뚫는다 |
| salvage(순수) | `Skills/SkillMath` · `Battle/Combat/{AttackReach, TargetPersistence, FrontmostTargeting, LowestHealthTargeting, NearestTargeting, AggroTargeting, KillAttribution, ProjectileTargeting(PatternTargeting), BounceRetarget, SweepHitMath, TileAoe, AttackShapeBake}.cs` · `Battle/Units/{ShieldMath, KillAttribution}.cs` · `UnitAi/{DefenderAi, EnemyAi}` |
| 이벤트 | `AttackResolved{a, b, SiteFired, SiteTarget, outputs}` · `ProjectileSpawned/Hit` · `DamageApplied{b, amount, hpRatioFinal, absorbed}` · `HealApplied` · `ShieldBroken{b}` · `UnitSlain{a=killer, b}`(피해로 죽음 — 분열·처치 보상의 사건, `UnitDestroyed` 와 다름) · `Knockup{b, height, sec}` · `LeapAscend/Descend` · `AggroAcquire` |
| 정의표 | `UnitDef`/`EnemyDef` 확장: 공격 출력 목록 · 사거리·간격·선딜 · 타겟 마스크/층 · 우선 클래스 · 방향 도형(bake 삼각비) · 패턴 슬롯 · 투사체/패턴 정의표 `ProjectileDef[]`·`PatternDef[]` |
| 테스트 | `Tests/EditModeCore/Combat*` — 옛 순수 테스트 복사·적응(`AttackReach*`·`TargetPersistence*`·`Frontmost*`·`KillAttribution*`·`ShieldMath*`·`SweepHit*`·`TileAoe*`·`Bounce*`·`PatternTargeting*`·`DefenderAi*`·`EnemyAi*`). 골든 `kill_race_basic`(옛 `basic` 의 후계 — 거시 지표 대조) |

## 구현

1. **통합 루프.** 방어유닛·적·도발받은 적·순찰병이 **한 루프**. 후보 = 전 진영 통합 풀(진영 판정은 공격자마다), 제외 3종(`Deploying`·`Dead`·궁극기 이탈)은 `Unit.IsTargetable()` **한 술어**(C16 — 우선순위 함수가 이 둘을 인자로 받는다). 순회는 `SimEntityId` 오름차순.
2. **START/RESOLVE.** 실주기 = max(간격, 선딜)(C19 — 현행 그대로 보류). 쿨다운은 CC 중에도 돈다. 행동 잠금(기절·수면 ‖ 도약)은 START 만 막는다. 커밋 대상/방향은 공격이 끝날 때까지 불변. strict lapse(대상 소멸 = 빗나감, 방향탄만 커밋 축으로 발사).
3. **타겟팅.** 획득/유지/정지 3술어 분리, 히스테리시스 0.1칸(코드 상수 — `configHash` 밖), 최전방·우선 클래스·최저체력, 동률 = `SimEntityId` 작은 쪽. 지속 락 제외 4종(facing·frontmost·힐러·가디언). 행동 불능 중 락 비우고 재잠금 건너뜀(C13). `PastGoal` 은 유효 대상(C14). 거점 특별 취급 없음(C15). 어그로 sticky 배타(C11). 가디언 대표 = 실제로 때린 적, 최전방 카드는 swap(C6).
4. **도달 산식.** 제약 13 진입점 4(`ReachFromUnit`·`ReachFromCell`·`ReachWithOrigin`·`ReachFromImpact`) + `InReachShaped`(부가 타격만 도형 AND). 칸 반폭·몸 상수는 `private`. **폭탄맨 폴백 사각 자(C20)·부채꼴 몸 0(C21)은 현행 그대로**(보류 — 플레이 후). 칸 자는 순찰 이동 전용(C10).
5. **아키타입 → 정책 값.** `Def.AttackPolicy{Target, Bomb, Summon}`. 폭탄맨(C1 던진 순간에만 쿨 리셋, 적 없으면 만료 대기)·소환사(C2 소환물 살아 있어도 쿨은 돈다, 스폰만 skip)는 **대상 선정 전에** 처리하고 루프를 빠져나간다(C3). `HasComponent` 7곳은 이 정책 값 하나로.
6. **투사체.** 궤적 9 × 바인딩 3(엔티티/셀/방향) · 페이로드 4(SingleSplash·TileAoe·PathHit·SpawnBlocker). 요청 = 값 스냅샷(사수 스탯 변해도 탄 불변). 원점 몸 반경은 경계 너머까지 실린다(0 = 자리형). 유효 피격 반경 = 탄 관대함 + 대상 몸(월드↔타일 환산). 재조준(방향 바인딩은 0), 관통 예산·재타격 창, 튕김 감쇠, 부메랑 발사 축 불변, 궤도 = 주인 종속. 한 탄에 조준은 하나. 착탄 넉백은 착탄까지 미룸.
7. **발사 명세.** 패턴 슬롯(durable `fireCountBase`) · 버스트 중 쿨다운 연장 · 탄막 난수 씨앗 = `hash(사수 SimEntityId, 발사 카운터)` · 순위 축 `SimEntityId` 오름차순 · 「한 칸에 몇 발」은 착탄의 성질.
8. **피해.** 인박스에 쌓고 `CombatPhase` 피해 단계가 한 번에: 받는 피해 배율 → 실드 흡수(같은 출처 max·다른 출처 합·FIFO 소모) → 체력. 완전 흡수 = 피격 아님. 실드 파열 = 합 >0→0 순간. 킬 귀속 = 그 틱 최대 피해 source(동점 = 버퍼 앞, source 없음 = 미귀속). 피해·회복은 **그 틱에 비운다**, 실드 부여만 **다음 틱**(C17 — 현행 비대칭 그대로). 데미지 숫자 비율 = 그 틱 최종값(C7). 재생은 피해 그릇 유무와 **무관**(C24 보류 → 분리). 궁극기 이탈·마음 방패는 피해 버퍼를 **비운다**(C12).
9. **사망 2단계.** `Dead` 표시 틱 ≠ 소멸 틱. 표시 → (후처리: 사직서 드랍·순찰 연쇄·시체 폭발 자리) → `BattleWorld.Destroy`(유일 경로, `UnitDestroyed`). 피해로 죽었을 때만 `UnitSlain`(분열·보상 사건). 죽은 채 배치 중이면 조용히 걷기만(「시체는 배치되지 않는다」). 몸 반경·진영은 파괴 직전 스냅샷을 이벤트에 싣는다.
10. **CC 부여 측.** 넉백 방향 = 적이 가던 방향의 반대(방향 없으면 안 밀림, C8). 넉업 = 짧은 기절 + `Knockup` 이벤트(뷰용). 수면은 주 대상 1체. **내 피해가 내 수면을 안 깨운다** — 한 틱 안에서 도는 새 코어는 명시 가드가 필요(C9): 「CC 적용은 피해 판정 뒤」로 phase 순서 고정 + 같은 틱 부여 슬롯은 기상 대상 제외. 보스 면역(기절·수면·넉백). 실제 슬롯 적용은 unit 6.
11. **도약.** 보스 도약 = 즉시 순간이동 + `LeapAscend`(뷰 아치, 피격 가능). 궁극기 = 이탈(피격 불가·잠금+무적 **원자 개시**) → 예고 → 강습 → 착지 슬램(자리형, 0 몸). 생존당 1회는 unit 7 바인딩 `fireCap`. 중단 정책 표: 사망(이탈 중 없음 — C12) · 퇴근(취소·위치 복귀) · CC(면역).
12. **행동 상태.** `DefenderAi.Resolve`(배치중 > 잠김 > 교전중 > 유지중 > 대기, 공격 시작은 대기·유지중) · `EnemyAi.Evaluate`(Marching/Engaging/Chasing/Standoff) — 공격 루프와 **같은 술어 함수**를 본다(데드락 방지). 결정은 UnitAi, 저장은 `Unit.Ai`.
13. **캐스터 제거.** `HazardCast*`·`CastEvents`·Cast seam·「캐스트 = 공격 사건」·`castCountedHosts` 는 만들지 않는다(README 계약 9).

## 이식 제외

**안 옮긴 것과 그 이유.** 플레이 중 이상하면 이 표부터 본다.

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 캐스트 기계 일체(`HazardCast*`·`CastEvents`·Cast seam·`castCountedHosts`) | 캐스터 제거 확정(README 계약 9). 「캐스트 = 공격 사건」도 같이 사라졌다 | 제거 |
| `ThreatTable` · `ThreatHitEvents` · `Leader` | 보스 위협 귀속의 소비자가 unit 7(보스)이라 그때 세운다. `Leader` 는 소비자 0 이라 부활시키지 않는다 | 제거(C25) |
| `RequireForUpdate` 겸직(피해 그릇 0 이면 재생 정지) | 단계 실행 조건을 명시로 썼다 — 재생은 `RegenPerSec > 0` 만 본다 | 분리(C24) |
| 요청 캐리어 엔티티 + ECB | `BattleWorld.ProjectileRequests` 리스트로 충분하다. 나르던 규칙(한 틱에 같은 주체가 독립 발사 여럿)은 그대로 산다 | 보류 |
| `_aliveAttackersQuery` 공유 쿼리 불변식 | 쿼리가 없다. 전멸 판정은 unit 4 가 자기 술어로 | 보류 |
| `ProjectileState` 의 슬롯 겸직표(주석 40줄) | unmanaged struct 크기를 아끼려는 ECS 제약의 산물이지 규칙이 아니다. 새 코어는 **이름이 뜻을 말한다** | 제거 |
| `BounceRetarget` 오버로드 3개(층 → 진영 → 몸) | 기존 producer 를 안 깨려고 층층이 쌓인 사슬이다. 새 코어는 producer 가 하나이고 셋을 전부 넘긴다 | 제거 |
| `ShotOrder`/`PatternLogic` 타입 | 안에 든 것이 전부 발사기의 지역 변수다. ECS 가 `Entity` 를 순수 함수에 못 나르던 제약의 산물 | 제거 |
| `FrontmostAttackLock`(별도 락 컴포넌트) | 그 락의 존재 이유는 **카드 배율 스냅샷**이었고 배율은 unit 7 의 것이다. 스윙 중 유지는 커밋이 같은 strict lapse 로 이미 한다 | 흡수 |
| `NextAttackDoubleFire` 컴포넌트 | `ProgressiveStates.Charge` 로 접었다(개시 의도 다섯이 한 자리에 모인다) | 흡수 |
| `DefenderDensity` 의 사각 자 | 보스 순간이동 착지 지점 선정 = unit 7. 그때 형부터 정하고 온다 | 보류 |
| 폭탄맨 폴백 사각 자 · 부채꼴 몸 0 | **현행 그대로**(플레이 후 재결정). 원 자로 바꾸면 착지 칸·콘 판정이 조용히 달라진다 = 밸런스 변경 | 보류(C20·C21) |
| 선딜/공속 관계(실주기 = max) | **현행 그대로.** 「실주기를 누가 소유하나」가 미정이고 수용 확정도 아니다 | 보류(C19) |
| 착지 슬램의 형을 데이터로 싣기 | 저작이 하나뿐이라 코드가 `OriginBodyRadius = 0` 으로 선언한다. **그 전에 필드를 만들지 말 것** | 보류(C22) |
| 스탯·스택 출력의 **적용** | 저작(`AttackOutputDef`)은 옮겼고 슬롯 적용은 unit 6 이다. 오늘은 인박스까지 | 이월(unit 6) |
| `PatternDef.FanOutToAllCandidates` / `FanOutStaggerSec` | 정의표와 `configHash` 에는 있는데 `EmitPatternShot` 에 **소비자가 없다**(옛 `ProjectileEmitterSystem.cs:221` 은 true 면 반경 안 전원에게 한 발씩). 라이브 SO 에서 켠 곳 0건이라 오늘 거동은 같다. 발사 명세를 트리거 레이어가 소비할 때 배선하거나, 그때도 켠 곳이 0 이면 은퇴 판정 | 보류(unit 7) |
| 군중 제어 슬롯 적용·감쇠·면역 병합 | 같은 이유. 이 unit 은 **부여 측**(누가 무엇을 얼마나)까지 | 이월(unit 6) |
| 모디파이어 배율(`damageMul`·`attackSpeedMul`·`dmgTakenMul`) | 소비처는 이미 살아 있다(`Unit.DamageTakenMul`·`AttackState.Period(mul)`). 값을 넣는 것이 unit 6 | 이월(unit 6) |

### unit 2 에서 **고친** 것

| 무엇 | 왜 |
|---|---|
| `AiMovePhase.HysteresisTiles` 0.5 → `TargetPersistence.HysteresisTiles`(0.1) | 옛 전투의 감지도 `TargetPersistence.KeepsLock` 을 **재사용**했으므로 0.1 이 정본이다. 같은 종류의 진동을 막는 데 두 개의 자를 두지 않는다 |
| `MoveState.Ai` → `Unit.Ai.Enemy` | spec 구현 12 —「결정은 UnitAi, 저장은 `Unit.Ai`」. 이동과 공격이 같은 자리를 봐야 「락은 있는데 Marching」 데드락이 안 난다 |
| `UnitDef.AttackShape`/`EnemyDef.AttackShape`(int) 제거 | `MatchDefinitionBuilder` 가 `(int)d.attackShape` 로 **struct 를 캐스트**하고 있어 Unity 어셈블리가 컴파일되지 않았다(헤드리스 lane 은 이 파일을 안 컴파일해 드러나지 않았다). bake 된 삼각비가 `AttackDef` 에 들어오면서 중복이기도 했다 |

### 나중에 **고친** 것

| 언제 | 무엇 | 왜 |
|---|---|---|
| 2026-09-24 (6a 감사) | 코어 `PatternSelectionRule` 번호를 **옛 저작과 같게** 되돌리고, 빌더의 통짜 캐스트를 **이름 기반 매핑**(`CombatDefinitionBuilder.ToCoreSelection`)으로 바꿨다 | unit 3 이 이 enum 을 「읽기 좋게」 재배열(`None = 0`)해 두고 주석에는 「번호는 옛과 같다」고 적었는데 **거짓**이었다. 저작 값은 이미 구워진 `ProjectilePatternData` 에셋에 들어 있어서 **12개 저작 중 11개가 다른 규칙으로 읽혔다** — 0(캐논·나이트메어 탄막 = 순회 폭격)이 「선택 안 함」이 되고, 2(샷거너·머신거너·관통·저격·마크스맨 = 방향 발사)가 「무작위 저격」이 됐다. ⚠ 골든은 안 바뀐다 — 코퍼스는 SO 를 안 읽어 발사 명세 줄이 **0개**다(그래서 이 결함이 골든에 안 잡혔다). 그물은 `PatternSelectionRulePinTests`(assets lane — 두 어휘를 동시에 보는 유일한 자리) |
| 2026-09-24 (드리프트 감사 H1) | 연발(발사 명세) 전탄의 피해 = **트리거 시점 공격 실효값**(산출물 피해 합 × 배율, 단발탄과 같은 `ShotDamage`). 패턴 저작 피해는 보스·스킬 경로 값으로 남는다(unit 7) | 옛 `AttackSystem` `spec.damage = projectileDamage`. 새 코어가 `pat.Damage`(라이브 머신거너 0)를 실어 **머신거너·샷거너 연발 전탄이 피해 0** 이었다 |
| 2026-09-24 (H2) | 패턴 슬롯이 있는 유닛은 **단발을 쏘지 않는다** — 패턴이 단발을 대체한다 | 옛 `pushedPattern` 게이트. 없으면 한 공격 = 단발 1 + 연발 10 |
| 2026-09-24 (H3) | 선정 규칙 없음(방향 발사) 패턴의 기준 방향 = **트리거 시점 조준 방향**(커밋 방향 또는 주 대상 쪽)을 버스트에 스냅샷 | 옛 `fireDir` → `template.direction`. 대상이 없을 때 「대상 쪽」을 재면 사수 자리가 나와 늘 +Z(북쪽)로 쐈다 |
| 2026-09-24 (H4) | `targetAllies` 가 저작 마스크를 이긴다 → 대상 진영 = 방어유닛 단독, 공격 대상 층 0(빌더) | 옛 `DefenderTargetDefaults.Resolve`. 라이브 힐러 `targetFactions 98` 을 raw 로 실어 **적을 회복시켰다** |
| 2026-09-24 (H5) | 해석된 대상 진영에 유닛 비트가 없는 적은 히트·도발 어그로를 **안 받는다**(`GrantAggro`) | 옛 `AggroStateSystem` 도발 범위 게이트. 마음사냥꾼이 가디언에게 끌려갔다 |
| 2026-09-24 (M6) | 보스 면역 = **기절·수면·넉백만**. `EffectEligibility.AcceptsCc(victim, kind)` — 인자 없는 오버로드 없음 | 옛 `IsBossImmune(kind) = IsLock ∨ Impulse`. 종류 축을 잃어 감속까지 막았다 |
| 2026-09-24 (M7) | 적의 공격 대상 층 = 0(무필터). 이동 정지 조건·감지 후보(`ReachProbe`)도 **공격 대상 층**을 본다 | 옛 적 `targetTraversalLayers` 미설정. 「자기 통행 층」을 대상 층으로 읽어 비행 적이 경로를 걷는 순찰병을 못 때리고 멈추지도 않았다 |
| 2026-09-24 (빌더 의미) | 적 `attackMethod None`/산출물 없음 = 걷기만(`AttackDef.Unarmed`, 사거리 0) · 탄은 `Projectile` 방식만 · 직업 필터는 **존재가 게이트**(`HasClassFilter`, 적은 늘 켬 — 마스크 0 = 아무도 못 때림) · 폭탄맨 = 능력 ∧ `travelSec > 0` · 광역 = `Splash` 토큰 ∧ 반경 · 패턴 거절·클램프(옛 `TryToSpec`) · 패턴을 먼저 굽고 탄 표를 나중에 굳힌다 | 전부 잠복(라이브 무영향). 새 두 칸은 기본값이면 canonical 줄을 안 써 골든 `configHash` 를 보존한다 |
| 2026-09-24 (enum 핀) | `MapTileType`·`EnemyTargetMode`·`EngageMovement` 이름 매핑 + 핀 · 도형 종류·층 비트 값 핀 · 스택 저작 종류도 `ToCoreStackKind` · `EnemySpawn` 의 교전 이동 clamp → 정의역 밖 loud | `PatternSelectionRule`(ec10619d)과 같은 모양의 나머지 쌍 |
| 2026-09-24 (모드 배선) | `ModeValidation.Validate` 를 빌더가 부른다(문제 전부 loud) · 모드 `deck`(있으면 이김)·`plan`(저작 플랜 모드) 소비 — 드라이버도 같은 `ResolveDeck/Plan` · `costConfig` 누락 = loud 오류 | 검증이 테스트에서만 불렸고, 모드 덱·플랜 소비자 0, 배치 창 폴백이 옛 30초 → 0초로 뒤집혀 있었다. 맵 풀 로테이션은 이 spec 이 귀속을 정한다(미배선) |

## 규칙 → 증언 매핑 (전투 판정 「필수」 18)

| # | 규칙 | 증언 |
|---|---|---|
| C1 | 폭탄맨은 던진 그 순간에만 쿨을 돌린다 | `CombatRulesTests.폭탄맨은_적이_없으면_쿨을_만료로_대기시킨다` · `CombatPhase.StepBomb` |
| C2 | 소환사는 소환물이 살아 있어도 쿨을 돌린다 | `CombatRulesTests.소환사는_소환물이_살아있어도_쿨을_돌린다` · `UnitAiRulesTests.공격_시작은_대기와_유지중_둘이다` |
| C3 | 폭탄맨·소환사는 대상을 고르기 전에 처리하고 빠져나간다 | `CombatRulesTests.폭탄맨은_근접_피해를_내지_않는다` · `CombatPhase.StepAttack` ⒟ |
| C4 | 규칙이 발동했는데 실행할 팔이 없으면 경고 | `CombatRulesTests.팔이_없으면_조용히_넘어가지_않는다` · `TickContext.Warn` |
| C5 | 바늘 캐리어 피해에는 공격력 배율이 안 붙는다 | **unit 7 이월** — 캐리어 자체가 트리거 레이어다(오늘 생산자 0) |
| C6 | 가디언은 「실제로 때린 적」을 대표로 세운다 | `CombatRulesTests.가디언_대표는_실제로_때린_적이다` · `CombatPhase.Resolve` |
| C7 | 피해 숫자의 체력 비율은 그 틱 최종값 | `CombatRulesTests.피해_사건은_그_틱_최종_체력_비율을_싣는다` |
| C8 | 방향을 모르는 대상은 밀리지 않는다 | `CombatRulesTests.방향을_모르는_대상은_안_밀린다` |
| C9 | 내 피해가 내 수면을 안 깨운다 | `CombatRulesTests.같은_틱에_건_수면은_내_피해가_안_깨운다` + `수면이_없는_피격은_기상_요청을_낸다` |
| C10 | 칸 자는 순찰 이동 전용 | `AttackReach.InCellRange` 헤더 + 소비처 0(공격 루프는 `InReach`/`InReachShaped` 만) |
| C11 | 어그로는 배타적이다 | `CombatRulesTests.끌려간_적은_가디언만_본다` · `UnitAiRulesTests.어그로는_사격_대상을_덮는다` |
| C12 | 공중에서 죽는 일은 없다 | `CombatRulesTests.궁극기_이탈은_피해를_버린다` + `배치중_사망_궁극기이탈은_표적이_아니다` |
| C13 | 행동 불능 중에는 락을 비우고 재잠금도 건너뛴다 | `CombatRulesTests.행동_불능이면_락을_비우고_다시_안_잠근다` |
| C14 | 골을 지난 적도 유효 대상 | `CombatRulesTests.골을_지난_적도_때린다` |
| C15 | 거점에 타입 기반 특별 취급이 없다 | `CombatRulesTests.거점은_거리로만_경쟁한다` |
| C16 | 우선순위 표의 맨 위 두 칸은 「죽었다」와 「배치 중」 | `CombatRulesTests.배치중_사망_궁극기이탈은_표적이_아니다` · `Unit.IsTargetable` |
| C17 | 피해·회복은 그 틱에 비우고 실드 부여만 다음 틱 | `CombatRulesTests.실드_부여는_다음_틱에_들어간다` + `피해와_회복은_그_틱에_비운다` |
| C18 | 순서 어트리뷰트 4건은 명시 단계 목록으로 산다 | `CombatPhase.Run` 하위 단계 8 + `SeamHooks`(Attack·Death·Lifecycle·Threshold) · 사망 2단계는 `사망_표시_틱과_소멸_틱은_다르다` |

**보류 4건의 자리**: C19 `CombatRulesTests.실주기는_간격과_선딜의_큰_쪽이다` · C20 `CombatPureMathTests.폭탄맨의_사각_자는_반경_0_이면_고르지_않는다` · C21 `SkillMath.SectorGate`(몸 0 그대로) · C22 `CombatPhase.StepLeap` 의 `OriginBodyRadius = 0` 선언 + `ProjectileBehaviorTests.궁극기_도약은_이탈_예고_강습_슬램이다`.
**제거 1건**: C25 `ThreatTable` 미이식(위 표).

## 거시 지표 대조 — 옛 `basic` ↔ 새 `kill_race_basic`

**통과 조건이 아니다**(계약 3 — 옛 골든은 참고다). 두 판은 길이도 저작도 다르므로 **비율과 방향**만 본다.

| 지표 | 옛 `basic` | 새 `kill_race_basic` |
|---|---|---|
| 판 길이 | 900틱(15초) | 10,800틱(180초) |
| 적 스폰 | 트레이스에 스폰 채널이 없어 **미상** | 30 |
| 처치 | 1 | 30 |
| 유출(골 도달) | 0 | 0 |
| 방어유닛 사망 | 0 | 0 |
| 사건 수 | 76 | 636 |

읽는 법: 옛 판은 15초 동안 적 한 기를 잡고 끝났고, 새 판은 3분 동안 30기를 전부 잡고 아무도 흘리지 않았다. **방향이 같다**(방어가 이기고 유출 0). 절대값 대조는 의미가 없다 — 옛 코퍼스에는 스폰 채널이 없어 분모를 모르고, 저작 값도 공유하지 않는다.

## 완료 기준

- [x] 헤드리스 `dotnet build/test` 초록. 복사한 순수 테스트 전부 통과(적응 목록은 「이식 제외」 위 표).
- [x] 골든 `kill_race_basic`: DebugSpawn 스케줄로 적 30기 vs 방어유닛 4기 3분 — 완주 · `UnitSlain` 30 · `UnitDestroyed` = 스폰 수 · 결정론 2회 동일(`DeterminismTests.킬_레이스는_두_실행이_같다` · `킬_레이스가_완주한다`).
- [x] 옛 코퍼스 `basic` 과의 거시 지표 대조표(위).
- [x] 규칙 분류표 전투 판정 「필수」 18 이 각각 테스트·골든·코드 포인터에 매핑(위).
- [x] 소멸 경로 전수 = `BattleWorld.Destroy` 1곳(grep: 호출부 4, 목록 제거는 그 함수 안 2줄뿐) · `UnitSlain` 은 피해 사망에서만(`처치_사건은_피해_사망에서만_난다` · `출처_없는_사망은_처치가_아니다`).
- [ ] `core-reviewer` APPROVE.

### 재기준선을 잡은 골든 5종

`empty_board` · `spawn_destroy` · `march_to_goal` · `detour_obstacle` · `detect_and_chase` 를 다시 구웠다. 근거:

1. **정의표가 커졌다** — `AttackDef`·`ProjectileDef`·`PatternDef` 가 들어오고 중복 `AttackShape` 가 빠져 `configHash` 가 전부 움직였다. 코드 회귀가 아니라 조건 드리프트다.
2. **사건 흐름은 먼저 대조했다.** 새 채널을 열기 **전**에 다섯 시나리오의 이벤트 줄을 옛 골든과 diff 해 **완전 일치**를 확인했다(unit 2 동작 무변 — 히스테리시스 0.5→0.1 도 이 다섯에서는 결과를 안 바꿨다).
3. 그 뒤 전투 채널 11개를 열자 `detour_obstacle`·`detect_and_chase` 에 **`AttackResolved` 만** 늘었다(피해 0 — 두 고정구는 공격 출력이 없다). 나머지 셋은 사건 수 무변.
