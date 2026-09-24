# 7a — 바인딩 코어 · 라우팅 표 · seam · concrete 33 (조각 D · 1/4)

> 조각 D 는 원래 unit 7 하나였다. 복사·적응 대상이 실측 **19,255줄**(카드 UI 7,608 · 스킬 도메인 3,338 · 브리지 파셜 4 2,262 · 옛 디스패처·어댑터 1,955 · 드림캐쳐 규칙 1,472 · 기믹/분열 955 · 저작 854 · 트리거 부속 811)이라 한 커밋이 될 수 없어 **7a·7b·7c·7d** 로 나눴다(5·6 의 선례). 번호 체계는 그대로다(unit 8~10 참조 무변).
> ⚠ 이 합계는 **파일을 한 번씩만** 센 것이다. 각 unit 문서가 자기 머리에 적은 규모(7b 의 손패 컨트롤러 551 · 7c 의 브리지 연출 1,429)는 **한 옛 파일이 두 unit 에 걸치는** 경우라 서로 겹친다 — 더하면 안 된다.

## rev 2 → rev 3 정정 9건 — **이 표가 이 조각의 잣대다**

정본은 [`04_trigger_layer_rev3.md`](../../plans/2026-09-22-battle-core-rebuild-census/04_trigger_layer_rev3.md)이고, 근거는 [`03_critic_rev2.md`](../../plans/2026-09-22-battle-core-rebuild-census/03_critic_rev2.md)의 코드 대조 12건이다. **rev 2 문면을 인용하지 말 것** — 아래 9행이 전부 뒤집혔다.

| # | rev 2 (틀림) | **rev 3 (현행 그대로)** | 근거 | 자리 |
|---|---|---|---|---|
| 1 | Squad 카드 owner = Match · 판 종료 | owner **host 유닛** · lifetime **소멸 ∪ 퇴근** · 회수 = 소급 중화 | C1 (`RecoverCardsHostedBy` 가 둘 다) | 7b |
| 2 | 기믹 주기 = Match 호스트 | **유닛 호스트 per-unit 타이머**(부착 시점 위상) · 필터는 기믹마다 다름 | C4 (둘 다 lazy-attach 타이머) | 7d |
| 3 | `PlacementAura` = 바인딩 1 | **바인딩 2**(ApplyStat `revokeOnExpire true` + ApplyCc `false`) | H6 (회수가 비대칭) | 7b |
| 4 | `SplitOnDeath` = `OnDeath` | **`OnSlain`**(피해로 죽음) — `OnDeath` 면 분열 조건이 넓어진다 | H7 | 7d |
| 5 | lifetime 한 필드에 「N회」 | **`fireCap` ≠ `lifetime`** — 표식은 1회 발동 + 소멸까지 부착 | M9 | 7b |
| 6 | 세대 BFS 를 연쇄 전부에 | **바인딩→바인딩 직접 재진입만**(오늘 0건). intent 경유는 각자 phase | H5 | 7a |
| 7 | `AreaBlast` 로 concrete 통합 | **철회** — 통행 층·예고 시간이 이벤트에 없다. concrete 둘 유지 | C2 | 7a |
| 8 | `skillId`·라우팅 표 은퇴 | **표는 남는다.** 은퇴는 「Burst 용 int 인코딩」뿐 — 프리뷰가 드래그 중(사건 없음)에 형을 물어야 한다 | C3 | 7a |
| 9 | 사망 seam 에서 시전자 재질의 | **값 스냅샷**(몸 반경·진영·통행층) — 드레인 시점엔 파괴됐다 | S18 · 제약 13 | 7a |

## 목적

「무슨 일이 일어나면 무엇이 터지나」의 **레일**을 코어에 세운다. 지금 코어에는 seam 훅이 뚫려 있고 **핸들러가 0** 이다(`SeamHooks.cs` 헤더 — 「내용만 unit 7 이 채운다」). 이 unit 이 그 훅에 붙는 유일한 핸들러(`TriggerDispatcher`)와, 규칙 하나를 담는 `Binding`, 실행자 `ISkill` concrete **33**, 그리고 그들이 세상을 만지는 창구(`CoreSkillContext` → `IntentApplier`)를 세운다.

카드가 그 레일에 무엇을 싣는가는 7b, 화면은 7c, 기믹·보스·분열은 7d 다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 바인딩 | `BattleCore/Trigger/{BindingDef, Binding, BindingRegistry}.cs` — `BindingDef{EventKind, Subject(Self·Any), Conditions[], SubjectFilter, Effect(ISkill), Params, fireCap, Lifetime, revokeOnExpire, Origin}` · `Binding{Def, Owner, InstanceId, Seq, fireCount, counters, remaining}` |
| 디스패처 | `Trigger/TriggerDispatcher.cs` · `Trigger/TriggerEvent.cs`(값 스냅샷 · `SiteFired↔CasterBody` · `SiteTarget↔EventBody` 짝) |
| 라우팅·형 | `Trigger/SkillRouting.cs`(← `Core/Dreamcatcher/DcSkillRouting.cs` **111줄**) · `Trigger/RangeCatalog.cs`(← `DcRangeCatalog.cs` **137줄**) · `Trigger/TriggerKinds.cs`(코어 미러 enum: 트리거 10 · 페이로드 33 · 게이트) |
| 창구 | `Trigger/CoreSkillContext.cs`(← `Battle/Skills/EcsSkillContext.cs` **1,215줄** — 어댑터는 버리고 질의·`Emit` 표면만) · `Trigger/IntentApplier.cs`(SimIntent 24 + MetaIntent 2) |
| 공격 수식자 | `Combat/AttackMod.cs` — 축 5(강공 · 튕김 부여 · 최전방 배율 · 수면 배율 · 충전 소비). **바인딩 밖**(rev 3 §1) |
| seam | `Phases/SeamHooks.cs` 에 `Immediate` **append**(`_Count` 앞) + **호출부** = `CommandPhase` 가 커맨드 적용 **콜스택 안에서** `Dispatcher.Drain(Seam.Immediate)` |
| 정의표 | `Match/MatchDefinition.cs` 에 `BindingDef[] Bindings` + canonicalize · `CombatDefs` 의 유닛 줄이 자기 바인딩 인덱스를 든다 |
| 사건 | `CoreEvent` **append**(`_Count` 앞 — 번호는 조각 C 가 쓴 다음부터): `TriggerFired`·`BindingAttached`·`BindingDetached` |
| salvage(무변) | `Wassup.Skills` 전량 **3,338줄** — asmdef 가 `noEngineReferences: true`·참조 `Unity.Mathematics`·`Unity.Burst` 뿐이라 **손대지 않고 그대로 참조**한다. `SkillMath.TryOriginRadius`·`SkillCone`·`SkillParams`·`SkillRegistry` 포함 |
| 테스트 | `Tests/EditModeCore/`: `BindingRegistryTests`·`TriggerDispatchTests`·`SkillRoutingTests`·`RangeCatalogTests`·`IntentApplierTests`·`AttackModTests`·`CoreTriggerEnumPinTests` |

## 구현

1. **어휘는 6개념으로 닫는다**(rev 3 §7 = rev 2 §1 잔존분): Command · Event · Binding · Condition · Effect · Owner/Lifetime. 유닛 스킬·카드·기믹·Squad·배치 오라·액티브가 **전부 `Binding` 하나**다. 어휘 밖은 **5**(강공 · 공격 수식자 3 · 충전 소비) — 전부 `AttackMod` 축이고 「하나」라고 적지 않는다.
2. **정적 라우팅 표는 은퇴하지 않는다**(정정 8 · C3). 은퇴하는 것은 **Burst 용 int `skillId` 를 unmanaged 슬롯에 굽는 인코딩**뿐이고, `BindingDef` 가 concrete **참조**를 직접 든다. 표가 남아야 하는 이유는 **부착 범위 프리뷰가 드래그 중에 형을 묻기 때문**이다 — 그 시점엔 이벤트도 `Site` 도 없다. 표의 모양은 옛 것 그대로다: 트리거별 분기(`OnKill`·`OnDeath`·`OnDamagedN`·`OnShieldBreak`·`OnRetire`·`None`·`HealthThreshold`) → 폴백 `ForPayload`. ⚠ **`NextAttackDoubleFire`·`SpawnHazard` 는 폴백 표에 둔다** — 트리거 블록에 넣으면 그 트리거 밖 조합이 라우팅 0 을 받아 조용히 죽는다(`OnPlace × 충전`이 실제로 그랬다 — 근거는 census-skills 의 `NotRouted = 0` 행「이전 도중 뜻이 뒤집혔다 … 조용히 죽어 있었고 EditMode 는 전부 초록이었다」와 `DcSkillRouting.cs:39-42` 의 경고 주석이다).
3. **`AreaBlast` 통합은 철회한다**(정정 7 · C2). 두 concrete 의 차이는 자리만이 아니다 — `SelfAreaBlastSkill` 은 `Duration = 0`(즉발) + 시전자 통행 층을 싣고, `DeathSiteBlastSkill` 은 저작 예고 시간 + **통행 층 0(무제한)** 이다. 합치면 어느 쪽으로 통일해도 **비행 적 상대 전투가 달라진다**.
4. **형은 이미 있는 어휘를 쓴다.** `RangeMetric`(`Skills/ISkillContext.cs:57`)의 `SelfArea`(몸에서 나오는 것) · `CellArea`(자리에 떨어지는 것) · `Euclidean`(원점 항 0) · `None`(fail-closed) 네 값이 제약 13 의 「효과의 형」이고, 원점 항 매핑은 `SkillMath.TryOriginRadius` **하나**다. 새 자를 만들지 않는다. concrete 별 형은 아래 표.
5. **seam 은 파이프라인이 도출한다** — 개수를 계약에 적지 않는다. 오늘의 수는 **6**: `Attack`·`Death`·`Lifecycle`·`Threshold`(unit 3) + `Periodic`(6b2) + `Immediate`(이 unit). `Cast` 는 캐스터 제거로 **없다**(계약 9). ⚠ `Periodic` 은 이름과 달리 **배치 엣지도 받는다**(S1) — `OnPlace` 가 같은 드레인을 탄다. 놓치면 배치 스킬이 조용히 죽는다.
6. **`Immediate` 는 자기 순서를 갖지 않는다.** 부착·액티브는 **동기 트랜잭션**이라 `CommandPhase` 가 커맨드를 적용하는 그 콜스택에서 드레인한다. 큐에 넣고 틱을 기다리면 **소모(차감·쿨다운) 뒤에 실행이 도착한다.**
7. **전순서와 연쇄.** 키 = `(seam 틱 순서, generation, productionSeq, owner SimEntityId, instanceId)`. 세대 BFS 는 **바인딩→바인딩 직접 재진입에만** 적용하고(오늘 0건), intent 경유 연쇄(시체폭발 → 처치 → 잿불)는 **각자의 phase 에서** 돈다 — 한 틱으로 접으면 반경 멤버십과 연출 리플이 바뀐다(정정 6 · H5). 깊이 예산 **4**, 초과는 `Report`(조용한 폐기 금지). 잔여 큐 = **후속 seam 이면 같은 틱, 지난 seam 이면 다음 틱**(현행 계약).
   ⚠ **「후속이냐 지난 seam 이냐」를 enum 값으로 판정하지 말 것.** 코어의 `Seam` 은 뚫린 순서대로 번호가 붙어 있고(`Attack = 0` · `Death = 1` · `Lifecycle = 2` · `Threshold = 3`, 6b2 의 `Periodic` 은 **4 로 append**) 그 번호가 **틱 안의 실행 순서와 다르다** — `Periodic` 은 `FieldPrepPhase` 끝이라 `Attack` 보다 **앞**에서 돈다. 번호로 비교하면 주기 사건이 「이미 지난 seam」으로 오판돼 **한 틱 밀린다.** 판정은 별도 표 `SeamTickOrder`(틱 파이프라인이 부르는 순서대로 매긴 index)로 하고, 그 표는 `TickPipeline` 을 읽어 한 곳에서만 만든다. enum 은 **append-only 의 몫**이고 순서의 몫이 아니다.
8. **이벤트는 값 스냅샷이고 자리↔몸은 짝으로 다닌다**(정정 9). `SiteFired↔CasterBodyRadius` · `SiteTarget↔EventBodyRadius` · 진영 · 통행 층 · 방향 · 피해량. **`0 = 그 자리는 칸`** 이 형 구분의 표현이고 새 필드를 만들지 않는다(제약 13 · 6c 구현 6 과 같은 규약). ⚠ **사망 seam 은 몸 반경을 발화 시점 값으로 싣는다** — 드레인 때 다시 읽으면 0 으로 새어 시체 폭발이 **조용히 좁아진다**. 진영도 값이다(안 실으면 적의 작별 선물이 적을 때린다).
9. **`CoreSkillContext` 는 질의와 `Emit` 둘뿐이다.** 질의 = 자리·정체·후보·격자 판단·조준 필요 + `UnitPredicate` 8. concrete 는 상태를 **안 바꾼다**(계약 3 계승). 후보 상한 64 는 「가까운 64」가 아니라 **목록 순서 선착 64**(S23 — 현행 박제, 넘는 판이 생기면 그때 잘림 규칙을 계약으로).
10. **쓰기는 `IntentApplier` 하나를 지난다**(S20 을 닫는다). 옛 전투는 asmdef 가 「쓰기는 발행으로만」을 컴파일러로 강제했고 예외 4건이 폐쇄 목록이었다. 새 코어는 concrete 가 여전히 `Wassup.Skills`(엔진 무참조)에 살지만 **코어 안에서는 아무것도 막지 않으므로**, 규율을 **표면 하나**로 옮긴다: `BattleWorld` 상태를 바꾸는 스킬 경로는 `IntentApplier.Apply` 뿐이고 그것을 `CoreArchitectureTests` 가 소스로 못박는다(5b 의 `CoreViewYardstickTests` 선례 — 막으려는 것이 값이 아니라 **형태**라 grep 을 테스트로 옮긴다). **원자 개시는 한 함수**다(S19 — 잠+감시자, 잠금+무적).
11. **`AttackMod` 5 는 바인딩 밖이다.** 「이번 공격의 출력 조립에 참여」가 판별 기준이고, 이들은 사건을 안 낸다. 실행 자리는 둘: 근접·즉시는 `CombatPhase` 공격 조립, 투사체는 **6a2 가 연 착탄 관문**(그 unit 이 「같은 슬롯의 다른 종류 · 실행은 unit 7」로 예약해 둔 자리). ⚠ 충전(`Charge`)은 **부여가 스킬 · 소비가 `AttackMod`** 다 — 경계가 여기 있다.
12. **bake 는 침묵보다 거절이다.** 게이트 3(감지자 없음 / 부착 전용 payload 를 트리거에 매닮 / 스킬인데 라우팅 없음)은 전부 loud skip. 「없음 = `-1`」 센티널 3축(탄·패턴·장판)도 명시 초기화다 — struct 기본값 0 은 **유효 index** 라 미배선 슬롯이 0번 탄을 쏜다(S4).
13. **저작 enum 은 값으로 미러한다.** `Wassup.Data.DcTriggerKind`(10)·`DcPayloadKind`(33)는 시트가 **enum 값으로 왕복**하므로 append-only 이고, 코어는 같은 번호의 미러를 든다. 어셈블리가 갈려 컴파일러가 못 잡으므로 `CoreTriggerEnumPinTests` 가 **값·개수 모두** 대조한다(6a 의 `CoreSkillEnumPinTests` 와 같은 그물). 변환은 `MatchDefinitionBuilder` 한 곳.
14. **유닛 스킬이 이 unit 에서 개통된다** — 카드가 아니라 **유닛이 저작으로 든 규칙**이다: 적 악몽(`nightmareMechanics`) · 방어유닛 능력 · **배치 스킬**(`OnPlace` → `Periodic` seam) · 퇴근 페이로드. 진영 중립 단일 bake 라 같은 표를 쓴다.
15. **「한 발이 반경 안 전원에게」를 배선한다**(unit 3 이월 — 조건부 은퇴 판정은 **성립하지 않는다**). `PatternDef.FanOutToAllCandidates` 가 켜져 있으면 발사 명세가 후보 **전원에게 한 발씩** 전개한다. 라이브 저작이 **하나 있고**(`Pattern_Cannon_Strike.asset:30`) 그것이 **캐논의 1:1 융단폭격**이다 — 「한 칸에 몇 발」이 발사가 아니라 **착탄의 성질**이라는 그 규칙의 표면이다. 전개는 **대상 바인딩이 개체일 때만**(옛 `ProjectileEmitterSystem.cs:221`) — 칸·방향 바인딩은 후보 집합이 없다.
16. **결정론.** 바인딩 순회는 `InstanceId` 오름차순, 등록부는 소유자별 목록 + Match 목록 하나. `System.Random` 금지. 사건 큐 ≤ 1024/틱 · 바인딩/유닛 ≤ 8 · Match 바인딩 ≤ 16(rev 3 §6).

### concrete 33 의 형 (제약 13) — 「형을 정하는 것은 감지자다」

> **33 은 오늘의 수가 아니다.** 라이브 레지스트리는 **34**(`Id` 1~34 연속, `BattleBridge.InstallSkillLayer` 의 `Register` 34회)이고, `CastHazard`**28** 이 **캐스터 제거로 사라져** 새 코어가 33 이다(rev 3 §5 · 계약 9). grep 해서 34 를 보고 이 표가 틀렸다고 읽지 말 것.

| 형 | 원점 항 | concrete (12 + 7 + 1 + 2 + 12 − 1 겸직 = **33**) |
|---|---|---|
| **몸에서 나오는 것**(`SelfArea`) | 그 몸의 `HitRadius` | `SelfAreaBlast`4 · `AreaSleep`1 · `AreaCc`14 · `AreaDot`15 · `AreaStack`13 · `AreaTaunt`8 · `AllySpeedAura`2 · `AllyStatAura`9 · `OpponentStatAura`10 · `GrantShield`3(반경 0 = 자기만 → None) · **`DeathSiteBlast`20 × `OnDeath`/`OnKill`**(시체가 터진다) · `ConeBreath`34 |
| **자리에 떨어지는 것**(`CellArea`) | 칸 반폭 0.5 (**몸이 아니라 도형 보정**) | `TileMeteor`33 · **`DeathSiteBlast`20 × `OnRetire`**(퇴근 운석) · `DeathSiteHazard`21 · `AllyBuffField`30 · `PullField`31 · `Portal`32 · `TileStatBurst`29 |
| **탄 비행 거리**(`Euclidean`) | 0 | `EmitPattern`7 |
| **개시만 한다 — 착지 슬램은 코어의 것** | 슬램은 **자리형**(몸 0) | `BlinkToCluster`5 · `UltimateLeap`6 — 스킬은 도달을 한 번도 안 묻는다. 착지 판정은 unit 3 의 `CombatPhase.StepLeap` 이 소유하고 거기서 `OriginBodyRadius = 0` 을 선언한다(C22) |
| **도달 판정을 안 한다** | — | `SelfStatBuff`18 · `ThresholdSelfBuff`22 · `SelfBuffLethal`25 · `GrantSelfCharge`23 · `DreamCocoon`26 · `TargetCc`16 · `TargetStack`17 · `TargetProjectile`19(대상이 이미 정해져 있다) · `OrbitProjectile`24(**궤도는 범위가 아니다** — 카탈로그가 `None` 을 준다) · `BountyMark`27 · `GainCost`11 · `ReduceSkillCooldown`12 |

⚠ **같은 concrete 가 두 형을 겸한다**(`DeathSiteBlast`) — 형을 정하는 것은 스킬이 아니라 **그 자리를 써 넣는 감지자**이고, 배선상 **「몸 반경 0 = 그 자리는 칸」**이 그 표현이다(실측: 스킬 자신은 어느 트리거가 불렀는지 **모른다**. 갈림은 감지자가 `EventBodyRadius` 에 무엇을 스냅샷하느냐에 전부 들어 있다). 새 필드를 만들지 말 것.
⚠ `ConeBreath` 는 **몸형인데 오늘 몸 0 으로 박제**돼 있다(unit 3 이식 제외 C21 — 고치면 콘 판정이 달라져 밸런스 변경).
⚠ **브레스의 그림은 이 스킬의 사건이 나른다**(6c 후속 3 이월). 옛 VFX 는 공격 도형이 아니라 이 슬롯의 콘(`coneHalfAngleDeg`·`tileRange`, 방향 = 시전자→대상)으로 그렸다(옛 `AttackSystem.cs:1954-1971` 의 `hasAreaBreath` 캐리어). 6c 가 `AttackResolved` 에 실은 도형은 **공격의** 도형이라 드래곤(전방위 · 2칸)에서 브레스(50° · 3칸)와 다르다 — 7a 가 `ConeBreath` 발화 사건에 콘 축·반각·사거리를 값으로 실어야 6c 의 브레스 뷰(보류)가 켜진다.
⚠ **착탄 예고 표식의 반경은 저작 필드가 아니라 스킬 intent 값이다**(6c 이월). 옛 `EcsSkillContext.cs:1121` `telegraphTileRange = intent.Telegraph ? intent.TileRange : 0` — 탄 정의표(`ProjectileDef`)에 옮길 저작이 없다. 그래서 7a 가 스킬 발사 경로(`TileMeteor`33 · 스킬 조준 탄)에서 **`ProjectileSpawned`(또는 탄 요청)에 예고 반경을 값으로 실어야** 6c 의 예고 표식 뷰(보류 — 6c 「아직 안 보이는 것」 착탄 예고 행)가 켜진다. 반경 없이 칠하면 뷰가 규칙을 지어낸다.

## 파이프라인 커버리지

이 unit 은 플레이 오브젝트를 신설하지 않는다 — 여는 것은 **사건과 실행 창구**다. 기존 정거장에 붙는 것만 적는다.

| 정거장 | 이 unit |
|---|---|
| 저작 | `DcMechanic`(무변 · append-only) · 유닛 SO 의 `mechanics` |
| 정의표 | `BindingDef[]` 신설 · `configHash` 에 실린다 |
| 생성 | N/A — 바인딩은 개체가 아니라 소유자의 등록부 항목이다 |
| 매 프레임 | seam 6 의 드레인(각 phase 의 훅 위치) |
| 소멸 | lifetime 만료 → `BindingDetached` |
| 뷰 | N/A — 7c |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `EcsSkillContext` 어댑터 통째(1,215줄) | foundation README 가 「버려지는 것은 이것뿐이고 그것이 포트 패턴의 비용」이라 선언했다. `ISkillContext`·concrete·seam 규칙은 그대로 산다 | 제거(선행 선언) |
| `SkillDispatch{Seam}System` 7 인스턴스 + `[UpdateBefore/After]` 그래프 | 시스템 순서가 곧 seam 계약이던 형태. 명시 호출로 바뀐다. ⚠ 그때 **순서 제약이 코드에 안 보이게 되는 위험**이 새로 생기므로 `SeamHooks` 호출부가 phase 안에 **주석 없이도 읽히는 자리**에 있어야 한다 | 제거(계약 1) |
| `skillId` int 인코딩 · `DcTriggerSlot` unmanaged 버퍼 | Burst 가 managed 레지스트리를 못 읽어 생긴 간접층. `tileRange` 한 칸이 **7~13가지 뜻을 겸직**한 근본 원인도 「평평한 형태」다 — payload 별 타입 분리로 푼다 | 제거 · S24·S27 |
| ECB 스테이징 vs 직접 쓰기 2갈래 · 폐쇄 목록 4건 | ECS 성질에서 나온 구분이라 개념째 사라진다. **원자성 요구만 남는다**(S19) | 제거 · S20 닫힘 |
| `CastHazardSkill`(28) · `HazardCastAbility` · `DcHostArchetype.HazardCast` · Cast seam | 캐스터 제거 확정(계약 9). concrete 34 → **33** | 제거(사용자 결정) |
| `AreaBlast` 로 concrete 통합 | 정정 7 — 통행 층·예고 시간이 이벤트에 없다 | 철회(C2) |
| 「같은 죽음 중복 억제」의 64비트 마스크 | `skillId ≥ 64` 면 억제가 조용히 꺼진다. 의도만 옮기고 키는 `InstanceId`(E2) | 제거 · E2 |
| 후보 상한 64 의 「가까운 순」 오해 | 현행은 **목록 순서 선착**이다. 바꾸지 않고 박제 | 보류 · S23 |
| ~~`PatternDef.FanOutToAllCandidates`~~ | **이식 제외가 아니다 — 이식 필수다.** unit 3 이 「켠 곳이 0 이면 은퇴 판정」으로 이월했는데 그 전제가 **거짓**이다: `Data/Projectiles/Pattern_Cannon_Strike.asset:30` 이 `fanOutToAllCandidates: 1` 이고 그것이 **캐논의 1:1 융단폭격**(미사일 1발 = 적 1기 — census-skills 「확정」)이다. 안 배선하면 캐논 배치 스킬이 **조용히 한 발만** 쏜다. 소비 지점은 옛 `ProjectileEmitterSystem.cs:221`(`binding == Entity` 일 때만 전개) | **이월 해소 = 이식** |
| `SelfWarmupBuff`(7) | 핸들러 0 · 사용 카드 0 — **죽은 값**. 시트 왕복 때문에 번호만 보존하고 코드 경로는 안 만든다 | 제거 · S17 |
| `AreaBarrage`(5) | 죽은 값이 **아니라 이관됨** — arm 이 철거되고 일이 발사 명세로 옮겨 갔다. 코드 경로는 안 만들되 bake 가 남기던 **안내 문구**(`BattleBridge.cs:10290`)는 같은 뜻으로 유지한다. 저작자가 이 번호를 고르면 「발사 명세를 쓰라」고 말해야 한다 | 이관(안내 유지) |
| 미개방 게이트 조합 5종 · 복수 게이트 ∧ · 아웃게임 상태 게이트 | `GateComboSupported` 2조합(궁지폭발 · 처형타)만 유지하고 나머지는 loud 거절. 「대상의 체력 × 피격 N회」는 **한 틱 다중 출처의 주체 선정 규칙**이 선행한다 | 보류 · S26 |

## 고친 것 (기존 코어·Unity 층 변경)

*(구현 중 채운다.)*

## 완료 기준

- [ ] **헤드리스 초록** · **EditMode 코어 lane 초록** + 새 테스트 7묶음: `BindingRegistryTests`(수명 5종 · `fireCap` ≠ `lifetime` · `revokeOnExpire` · `InstanceId` 단조 증가 F1) · `TriggerDispatchTests`(전순서 키 · 세대 BFS 직접 재진입만 · 깊이 4 초과 `Report` · 잔여 큐 후속/지난 seam) · `SkillRoutingTests`(트리거별 분기 7 + 폴백 표 · **`OnPlace × 충전`이 라우팅을 찾는다** · 미라우팅은 loud 거절) · `RangeCatalogTests`(형 표 전건 · `DeathSiteBlast × OnDeath/OnRetire` 가 **다른 형**) · `IntentApplierTests`(intent 24 + meta 2 · 원자 개시) · `AttackModTests`(5종 · 충전 부여/소비 경계) · `CoreTriggerEnumPinTests`.
- [ ] **증상 단언 3건**(규칙이 화면에서 보이는 형태로): ⑴ 적을 죽인 자리에서 시체 폭발이 터지고 **그 시체의 몸만큼 넓다** ⑵ 배치하면 배치 스킬이 **그 프레임에** 난다 ⑶ 부착한 카드가 붙은 유닛이 죽으면 **작별 선물이 그 자리에서** 터진다(시전자가 없어도).
- [ ] `SeamHooks.Run` 호출부 **6곳**(grep) · `Seam._Count` 앞 번호가 **안 밀렸다** · `Immediate` 는 `CommandPhase` 콜스택 안에서만 불린다.
- [ ] **`SeamTickOrder` 단언**: `Periodic` 의 틱 순서 index 가 `Attack` 보다 **작다**(enum 값은 더 크다). 「지난 seam 이면 다음 틱」 판정이 그 표를 보고, **enum 값을 비교하는 코드가 0** 이다(소스 단언 — 5b 의 `CoreViewYardstickTests` 선례).
- [ ] **캐논 융단폭격 단언**: `Pattern_Cannon_Strike` 저작으로 배치 스킬을 쏘면 **미사일 수 = 반경 안 적 수**(1:1), 그 손잡이가 꺼진 명세는 한 발. 안 배선하면 조용히 한 발이 되므로 값이 아니라 **개수를** 센다.
- [ ] 코어에 `Unity.Entities` 0 · `Wassup.Skills` 는 **한 줄도 안 고쳤다**(git diff 0줄 — 엔진 무참조가 이미 참이라는 증거).
- [ ] `ledgers/rules.md` **S1·S4·S8·S9·S18·S19·S20·S22·S23·S24·S25·S26·S27 · E2 · C5** 가 코드 포인터로 매핑. S20 은 「`IntentApplier` 단일 표면 + 아키텍처 테스트」로 **보류 → 결정**. ⚠ `rules.md` 의 `C5`(바늘 캐리어)와 `rule-holders.md` 의 `C5`(코스트)는 **다른 행**이다 — 장부를 섞지 말 것.
- [ ] `ledgers/bridge-methods.md` 미정 **44 → 36**(8행): `RoutingProbe/2` · `TryBuildPatternSlot/5` · `BuildPatternTemplate/4` · `ConeCosSq/1` · `SpawnProjectile/2` · `CanDefenderTargetMover/2` · `TryPickNearestEnemy/4` · `AddTraversalMask/2`.
- [ ] `core-reviewer` APPROVE — **매니저 0**(`SkillManager`·`TriggerManager` 같은 이름이 없다) · 하드코딩 0(깊이 4 와 상한 3종만 상수 + 근거 주석) · 틱 phase 수 무변.
- [ ] 7a 단독으로는 **카드가 아직 안 붙는다**(7b) — 라이브 확인은 유닛 저작 스킬(적 악몽 · 배치 스킬)로 한다.
