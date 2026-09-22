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

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `RequireForUpdate` 겸직(피해 그릇 0 이면 재생 정지) | 단계 실행 조건 명시로 분리 | 보류(C24) |
| 캐스트 기계 일체 | 캐스터 제거 확정 | 제거 |
| `ThreatTable.Leader` | 소비자 0 — 위협 누적만 남긴다(보스 위협 귀속) | 제거(C25) |
| 요청 캐리어 엔티티·ECB 지연 | 요청 리스트 + 같은 phase 적용 | 보류 |
| `_aliveAttackersQuery` 공유 쿼리 불변식 | 쿼리 없음. 전멸 판정은 unit 4 가 자기 술어로 | 보류 |
| 선딜/공속 관계·폴백 사각 자·부채꼴 몸 0·슬램 형 데이터 | 현행 그대로, 플레이 후 재결정 | 보류(C19~C22) |

## 완료 기준

- [ ] 헤드리스 `dotnet build/test` 초록. 복사한 순수 테스트 전부 통과(적응 목록 기록).
- [ ] 골든 `kill_race_basic`: DebugSpawn 스케줄로 적 30기 vs 방어유닛 4기 3분 — 완주 · `UnitSlain` 수 > 0 · `UnitDestroyed` = 스폰 수 · 결정론 2회 동일.
- [ ] 옛 코퍼스 `basic` 과의 **거시 지표 대조표**(킬 수·유출 수·생존 방어유닛)를 이 파일에 기록 — 통과 조건 아님, 참고(계약 3).
- [ ] 규칙 분류표 전투 판정 「필수」 18 이 각각 테스트·골든·코드 포인터에 매핑(표를 하단에).
- [ ] 소멸 경로 전수 = `BattleWorld.Destroy` 1곳(grep) · `UnitSlain` 은 피해 사망에서만.
- [ ] `core-reviewer` APPROVE.
