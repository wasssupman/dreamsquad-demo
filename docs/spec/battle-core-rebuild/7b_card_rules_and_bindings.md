# 7b — 카드의 규칙 (조각 D · 2/4)

> 7a 가 **레일**을 세웠다면 여기는 **카드가 그 레일에 무엇을 싣는가**다. 화면은 7c, 기믹·보스·분열은 7d.

## 목적

unit 4 의 `HandDeck` 은 자원만 움직이고 효과 자리를 **진단 통로로 말하고 지나갔다**(그 문서 이식 제외 표 — 「조용한 무동작 금지」). 이 unit 이 그 구멍을 닫는다: 카드를 붙이면 규칙이 **실제로 붙고**, 액티브를 쓰면 **실제로 터지고**, 숙주가 떠나면 **되돌아온다**.

복사·적응 대상 실측: `DreamcatcherHandController` **551줄**(자원 몫은 unit 4 가 가져갔고 남은 것은 부착·회수·적용) · `DcApplicability` **291줄** · `DreamcatcherAttachEval` **137줄** · `BattleBridge.Dreamcatcher.cs` **1,429줄**(이 중 22 선언) · `Data/Dreamcatcher/` **854줄**.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 카드 → 바인딩 | `BattleCore/Trigger/CardBindings.cs` — 카드 한 장이 굽는 바인딩 목록(`mechanics[]` → `BindingDef[]`) · `attackMods[]` → `AttackMod` 슬롯 · `effects[]`(Squad) → 바인딩 |
| 부착·해지 | `Owners/HandDeck.cs` 확장 — `TryAttach`/`TryCast` 가 **자원 + 바인딩**을 한 콜스택에서. `Recover` 가 `BindingRegistry.Detach` 를 부른다 |
| 부착 가능성 | `Trigger/Applicability.cs`(← `Core/Dreamcatcher/DcApplicability.cs` **291줄**) — host 종속 조건만. **UI preflight 와 커밋 bake 가 같은 함수**(옛 선례 계승) |
| 정의표 | `Match/CardDef.cs` 확장: `BindingRange`(이 카드가 굽는 `BindingDef` 구간) · `AttackModRange` · `SquadEffect[]` · `ActiveSkill` · `AttachRequirement`(host 종속) · `TargetsEnemies` · canonicalize |
| 메타 | `Trigger/IntentApplier` 의 `MetaIntent` 3: `GainCost`·`ReduceSkillCooldown`(기존 2) + **`SetCostRegenMul`**(신설 — `CardBuffKind.CostRate` 는 유닛 스탯이 아니라 **판 자원**이다, rev 3 §2) |
| 배치 오라 | `Owners/PlacementService` 가 `OnPlace` 사건을 낼 때 `Any` subject 바인딩이 받는다 — 서비스는 오라를 **모른다**(등록부가 안다) |
| 사건 | `CoreEvent` append: `CardAttached`·`CardDetached`·`CardCast`(뷰·트레이스 — 7c 가 구독) |
| 테스트 | `Tests/EditModeCore/`: `CardAttachTests`·`SquadCardTests`·`PlacementAuraTests`·`BountyMarkTests`·`RetireRecallTests`·`ActiveCastTests`·`ApplicabilityTests` |

## 구현

1. **부착은 동기 트랜잭션이고 순서가 규칙이다.** ① 적용(바인딩 부착 + `Immediate` seam 드레인) → ② 차감 → ③ 순환(풀에서 이탈). **실패한 부착은 무차감·무순환**(D11·D12). 큐에 넣고 틱을 기다리면 소모 뒤에 실행이 도착한다. 실패 사유는 `Receipt.RejectReason` 하나로 나가고 그 enum 은 이미 코어에 있다(`Command.cs` — 옛 `DcRejectReason` 8종이 그 자리에 합류해 있다).
2. **Squad 카드의 주인은 host 유닛이다**(정정 1 · C1). owner = 부착한 방어유닛, lifetime = **소멸 ∪ 퇴근**. 부착 시점에 판 위 전원에게 걸고, **이후 배치되는 유닛은 `OnPlace(any, 클래스 필터)` 바인딩이 상속**시킨다. ⚠ 옛 전투는 Squad 카드에 ECS 엔티티·슬롯이 **0** 이었고 브리지 사전이 그 역할을 했다 — 그 사전이 여기서 사라진다.
3. **회수는 슬롯 삭제다** — 6a 가 연 축(`ModifierSet.Revoke(key)`)을 그대로 쓴다. 옛 회수는 「배율 1.0 재발행으로 중화」라 ⑴ 강제 고정에 항등이 없고 ⑵ 상한을 실으면 조용히 실패했다. 이 전환의 **부수 효과 하나**: 「출처 사망 시 모디파이어 회수」가 생겼으므로 `PlacementAura` 의 **등록 영수증이 불필요해진다**(census 후속 후보 해소).
4. **`CostRate` 는 유닛 스탯이 아니다.** `CardBuffKind.CostRate` 하나만 `StatModifier` 매핑이 없고 `CostLedger` 가 통째로 소비한다 — 그래서 `SetCostRegenMul` 메타 intent 다(표현 불가 1). 라이브 경로는 드림스톤뿐이지만 **어휘 자리는 유지**한다.
5. **`PlacementAura` 는 바인딩 둘이다**(정정 3 · H6). `OnPlace(any)` × ApplyStat(공속, `revokeOnExpire true` — 소급 중화) + `OnPlace(any)` × ApplyCc(Sleep, `revokeOnExpire false` — 등록부 제거만). owner = host, lifetime = 소유자 소멸. **한 바인딩으로 접으면 둘 중 하나가 틀린다.** 이 payload 가 「어휘 밖(시제)」이던 이유가 `Any` subject 축으로 접혔다.
6. **표식 카드는 `fireCap` 1 + `lifetime` 소유자 소멸이다**(정정 5 · M9). 한 필드로는 못 담는다 — 발동은 1회인데 **표식된 적이 사라질 때까지 부착**이기 때문이고, lifetime 을 「N회=1」로 두면 카드가 즉시 손패로 돌아온다. ⚠ 두 효과(각성 배율 + 받는 피해 감소)는 **원자**다 — 하나만 걸리면 버그. 그리고 **각성 배율은 baked 값 덮어쓰기, 마음 회복은 SO 원값**이다(두 축 겸직 금지). 적을 겨냥하는 유일한 카드라 **부착 상한 3 밖**이다(D14 — 상한은 방어유닛 손패 규칙이다).
7. **`trigger == None` 3장은 슬롯을 안 굽는다.** 마지막 불꽃 · 호접몽 · 살찌운 제물 — 부착 지점이 발화시키고 `fireCap 1`. 트리거에 매달면 bake 가 거절한다(**면제가 아니라 거절 사유**다).
8. **인수인계는 바인딩 effect 가 아니라 퇴근 회수 규칙의 일부다**(표현 불가 2). 주어가 유닛이 아니라 **「그 host 에 붙은 카드 집합」**이라 `HandDeck.RecallOthersToFront(host)` 가 실행한다: 선언 카드 자신은 맨 뒤, **나머지는 부착 순서 그대로 큐 앞**. 판정은 저작 한 칸(`CardDef.DeclaresRetireRecall`)이고 그것이 곧 규칙이다(D10 — 두 곳에 두면 「붙는데 무효」가 돌아온다). ⚠ **플레이어가 누른 퇴근에만** 붙는다 — 사망 경로에 얹으면 조준 중 비동기 재정렬이 손패 멤버십 가드를 깬다.
8-1. **퇴근 페이로드의 seam 이 옮겨진다 — 선언해 둔다.** 옛 전투에서 퇴근은 브리지가 유닛을 파괴한 뒤 **`Lifecycle` seam** 으로 밀어 넣었다(`BattleBridge.cs:4389`). 새 코어에서 퇴근은 **커맨드**이고 파괴하는 주체가 `CommandPhase` 라, 그 틱의 `Lifecycle` 훅(`CombatPhase` 안 소멸 직후)은 **퇴근이 지나가지 않는 자리**다 — 거기 붙이면 퇴근 운석이 영영 안 떨어진다. 그래서 `Immediate` seam 으로 옮기고, 값 스냅샷(**비워진 칸 중심 · 몸 0 = 자리형**)은 커맨드가 **파괴 직전에** 읽어 싣는다. ⚠ **낙하 시각은 안 밀린다** — 옛 경로도 입력 처리 안에서 같은 프레임에 넣었고, 예고 시간은 탄이 자기 수명으로 세기 때문이다. seam 이 바뀌었다고 예고 초를 다시 저작하지 말 것.
9. **액티브는 시전자가 없다.** 칸(또는 칸 둘)을 조준하고 `CasterRef.Player` 로 진영이 접힌다. 저작 효과 6갈래 → concrete **5**(스탯 버스트 · 당김장 · 메테오 · 아군 장판[2갈래 공유] · 포탈 2셀). 성사 → 차감 → 쿨다운 재충전이 **한 함수**다(K2). ⚠ **쿨다운은 판의 시계로 옮겨진다**(사용자 확정 2026-09-23 · unit 4 가 이미 틱으로 센다) — 옛 `SkillRuntime` 은 `Time.deltaTime` 을 그대로 빼서 **슬로모에 안 느려졌다**(실측). 그래서 이것은 이식이 아니라 **규칙 변경**이고, 새 코어에서는 느려진다.
9-1. **부착 상한 3 은 Unit 카드와 Squad 카드가 «같이» 센다**(실측 — `AtAttachCap` 가 종류를 안 가린다). 적에게 붙는 표식만 그 밖이다(D14). ⚠ **값은 라이브 에셋이 정본이다** — C# 필드 기본값(손패 5 · 코스트 30/15/20)과 라이브 저작(손패 **4** · 게이지 20/100 · 부착 3)이 **다르다**(unit 4 이식 제외 「코드 기본값을 기획으로 읽기」). 정의표가 읽는 것은 에셋이다.
10. **`Applicability` 는 host 종속 조건만 판정한다.** 「magnitude ≤ 0」처럼 어느 host 에서나 답이 같은 것은 이 층 **밖**이다. host 아키타입(Standard·FacingVolley·BombThrow — `HazardCast` 는 캐스터 제거로 사라짐) × 투사체 경로(None·Homing·Ballistic·Directional·Grenade)로 판정하고, ⚠ **경로는 탄 SO 선언이 아니라 그 host 가 실제로 타는 길**이다(폭탄맨의 탄은 flightMode 0 인데 경로는 수류탄).
11. **카드 순환 4규칙**(unit 4 가 자원으로 이미 갖고 있고 이 unit 이 **계기**를 준다): host 소멸 → 큐 뒤 / 액티브 사용 → 즉시 뒤 / 인수인계 → 그 유닛의 **다른** 카드가 앞 / 실패한 부착 → 무변.
12. **한 사건이 같은 바인딩을 두 번 발동시키지 않는다**(E2). 억제 키 = **`InstanceId`** 이고, 옛 64비트 `skillId` 마스크는 안 옮긴다(`skillId ≥ 64` 에서 억제가 **조용히 꺼졌다**). ⚠ **키를 바꾸면 규칙이 바뀐다** — 같은 카드 두 장은 `InstanceId` 가 둘이라 **둘 다 터진다**. 그것이 결정 ① 의 기본값이고 `rules.md` E2 의 「의도만 옮기고 키는 `instanceId` 로」가 같은 문장이다.
    ⚠ **억제 루프는 하나가 아니라 둘이다** — 자기 죽음(`UnitLifecycleSystem.cs:237`)과 **처치**(`DamageApplicationSystem.cs:465`)가 각자 `firedMask` 를 든다. 그래서 키를 바꾸면 **시체 폭발(내가 적을 죽였을 때)도** 붙인 장수만큼 터진다 — 결정 ① 은 두 문을 같이 움직인다. 나머지 트리거(`OnDamagedN`·`OnShieldBreak`·`OnRetire`)는 **오늘도 억제가 없고** 장수만큼 터지므로 이 축에서 안 바뀐다.
13. **결정론.** 부착 번호(`_attachSeq`)는 **판마다 리셋**, 바인딩 `InstanceId`(= 카드 묶음 핸들의 번호판)는 **판 수명 내내 단조 증가**(F1 — 의도적 비대칭. 재사용하면 낡은 핸들이 새 대상을 가리킨다. ⚠ 초판 「앱 수명」은 **정정** — 7a 「고친 것」: 코어는 판 밖 상태를 안 들고, 앱 전역 카운터는 같은 판을 다시 돌리면 트레이스 `i` 가 갈라 결정론을 깬다). 부착 목록 순회는 부착 번호 오름차순(D20), 바인딩 순회는 `InstanceId` 오름차순.

## 파이프라인 커버리지

카드는 플레이 오브젝트가 아니다(개체가 없다 — 소유자의 등록부 항목이다). 이 unit 이 여는 정거장만 적는다.

| 정거장 | 부착 카드 | 액티브 카드 | Squad 카드 |
|---|---|---|---|
| 저작 | `DreamcatcherCard.mechanics[]`(무변) | `.skill`(`SkillData`) | `.effects[]` |
| 정의표 | `CardDef.BindingRange` | `CardDef.ActiveSkill` | `CardDef.SquadEffect[]` |
| 생성 | `HandDeck.TryAttach` → `BindingRegistry.Attach` → `CardAttached` | `TryCast` → `Immediate` seam → `CardCast` | 부착 1 + `OnPlace(any)` 상속 바인딩 1 |
| 매 프레임 | 자기 seam 드레인 | N/A(1회) | 상속 바인딩만 |
| 소멸 | 숙주 소멸·퇴근 → `Detach` + `CardDetached` | 즉시 재활용 | 같은 lifetime |
| 뷰 | 7c | 7c | 7c |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| Squad 효과의 브리지 사전(`_activeDcEffects`) + 지속 1e9 트릭 | Squad 카드가 sim 밖에 살던 형태. 바인딩 owner = host 로 접힌다(정정 1) | 제거 |
| 회수 = 항등 재발행 중화(op 재도출 포함) | 6a 가 슬롯 삭제로 바꿨다. 이 unit 은 그 축을 **쓰기만** 한다 | 제거(선행 · F27·F28) |
| `PlacementAura` 등록 영수증 | 회수가 슬롯 삭제가 되면서 근거가 소멸(구현 3) | 제거 |
| `RecallAttachedToFront` 의 `magnitude` | 소비자 **0** — 컨트롤러도 덱도 안 읽고 카드 문안이 「저작 손잡이 없음」을 단언한다. 정의표에서 삭제 | 제거 · S12 |
| 카드 화이트리스트 2벌(자기 진영 타격 방지 술어 2 · 「방어유닛 전용」 하드코딩 3) | migration unit 8 이 이미 걷었다. 되살리지 않는다 | 제거(선행) |
| 실드 파열 브리지 arm(`if (!routedToSkillLayer)`) | 죽은 코드. 남기면 「파열은 브리지가 실행한다」로 오해된다 | 제거 · S13 |
| `CardCategory`(Normal/Unique/Subconscious) | **순수 시각 라벨** — 덱 규칙 소비처 0. 정의표에 안 싣고 뷰 데이터로 둔다(7c) | 소유 이전 |
| `DeckRuleConfig.squadCardMax` 를 모드가 복제하기 | 덱 규칙이 이미 소유하고 라이브가 **무제한**이다. 두 곳이 갈린다 | 소유 이전(unit 4 결정 계승) |
| 부착 상한을 적 표식에도 적용 | D14 — 상한은 방어유닛 손패 규칙이다. 표식은 그 밖 | 현행 유지 |
| **사망은 같은 스킬을 죽음당 한 번만 발동**(퇴근은 전 매칭 슬롯) | 실측: 사망 루프는 `firedMask`(64비트)로 **같은 `skillId` 를 억제**하고, 퇴근 루프는 버퍼를 직독해 **전부** 발동한다 — 그 코드가 *「사망 쪽 «첫 매칭 슬롯만» 은 사망 이벤트 struct 가 payload 필드를 한 벌만 실어서 생긴 제약이었고, 여기는 해당 없다」* 고 적어 뒀다. 종류가 다른 카드는 사망에서도 각각 발동하므로 갈리는 것은 **같은 카드 여러 장**뿐이다. 새 코어엔 그 한계가 없다 → **사망·처치도 전부 발동**(사용자 결정 ① 「같은 카드 2장 = 장수만큼」 — 이행 `CardAttachTests.같은_카드_두_장은_사망에서도_장수만큼_터진다`·`…처치에서도…`) | 규칙 변경(사용자 결정) · E4 |
| 옛 `SkillRuntime` 의 **벽시계 쿨다운**(`SkillRuntime.cs:73` 이 `Time.deltaTime` 을 그대로 뺀다) | 판의 시계로 옮긴다(사용자 확정 2026-09-23 · unit 4 가 이미 틱으로 센다). **슬로모·정지에 같이 느려진다** — 옛 동작과 다르므로 이식이 아니라 규칙 변경으로 적는다 | 규칙 변경(사용자 확정) |
| 미개방 게이트 조합 | 7a 이식 제외 표 · S26 | 보류 |

### 이식 제외 — 구현에서 더한 행

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `SetCostRegenMul` **메타 의도 신설**(구현 4 · 변경 대상 표 「메타」 행) | `MetaIntentKind` 는 `Wassup.Skills` 의 enum 이라 추가하면 **diff 0 계약**(7a)이 깨진다. 그리고 생산자가 **0** 이다 — 카드의 `CostRate` 효과는 옛 전투에서도 무동작이었고(`BattleBridge.Dreamcatcher.cs:1385` `MapDcBuff` default 분기 — 「방어적 no-op」 주석), 라이브 경로는 드림스톤뿐인데 그것은 판 진입 배율(`MatchDefinition.CostRateMultiplier`)이다. 카드 bake 가 `CostRate` 를 **loud skip** 하고, 드림스톤 코스트 돌은 `CardDefinitionBuilder.CostRateOf` 로 간다 | 제거(생산자 0 · 계약 충돌) |
| 카드 발사 명세가 숙주의 발사 명세 버퍼를 **공유**하던 것 — 그 부작용 둘: ⑴ 카드 한 장이 숙주 분류를 `FacingVolley` 로 바꿨다 ⑵ 「패턴 없는 방향 단발」 숙주에서 카드 패턴이 0번 슬롯을 차지해 기본 공격을 바꿔쳤다(그래서 옛 bake 가 거절했다, `BattleBridge.Dreamcatcher.cs:949-959`) | 버퍼 공유라는 **기계**에서 나온 결함과 그 땜빵이다. 새 코어에서 카드 패턴은 규칙의 버스트(`Binding.Emitters`)라 숙주 슬롯과 섞이지 않는다 — 거절도 필요 없다 | 제거(기계) |
| 부착 검증 경고가 **부착할 때마다** 뜨던 것 | 어느 숙주에서나 답이 같은 검증(magnitude·탄·주기·트리거 축 가드)은 판 밖 bake 에서 **한 번**(`CardDefinitionBuilder`). 숙주 종속만 부착 때(`Applicability`) | 소유 이전 |
| 몽마의 계약 **선불**(유출 허용치 지불 · `CommitAttach` 의 선행 게이트) | README 계약 9 제거 확정(유출 한도 소멸, D19·X22). 카드는 자기 효과(공격력 +25%)만 싣는다 | 제거(선행) |
| `DcHostArchetype.HazardCast` | 캐스터 제거(계약 9) | 제거(선행) |
| 표식 등록부(`_bountyMarked` HashSet)와 `EnemyGone` 사건 | 표식 = **적에게 붙은 카드 규칙**이다(`CardBindings.IsMarked`). 적 소멸(처치·유출)은 이미 `UnitDestroyed` 가 알린다 — 두 번째 알림이 필요 없다 | 제거(구조로 접힘) |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 전 → 후 | 근거 |
|---|---|---|
| `HandDeck.TryAttach` 순서 | unit 4: ①(진단 통로) → 풀 이탈 → 지불 → **① 적용 → ② 차감 → ③ 순환**. ① = `CardBindings.Plan`(판정) → `BindingRegistry.AttachCard` → `CardBindings.FireOnAttach`(Immediate 줄 세움). 드레인은 이 커맨드의 콜스택(`CommandPhase.Execute` 가 `Dispatch` 직후 — **호출부 하나 유지**)이다 | 구현 1 · D11·D12 · 옛 `CommitAttach` 가 성사 여부를 **실행 전 판정**(`attached == 0`)으로 정했다 — 새 코어도 판정이 성사를 정하고 실행은 같은 커맨드 안에서 끝난다 |
| `HandDeck.TryCast` | 칸 없음 · 국면 무관 → **칸 둘(포탈)** · **전투 국면에서만** · 준비·성사·차감·대기·재활용 한 함수 | 옛 `CastSkillAtTile`/`CastPortal` 의 `!_running` 거절(`BattleBridge.cs:2641`·`:2729`) — unit 4 는 배치 창에서도 받았다(옛과 다름 → 옛으로) · 입구 == 출구 거절(`:2741`) |
| `Command` | `CellB`·`HasCellB` · `CastActivePair` · 디버그 22 `DebugAttachCard` · 23 `DebugCastCard` · `RejectReason` +5(`NotAnEnemy`·`NotADefender`·`AttachRequirementUnmet`·`NoContribution`·`NeedsSecondCell`) | 두 칸 조준 · 손패 UI 없이 라이브 확인(6b2 규율) |
| `CoreEvent` | **60** `CardAttached` · **61** `CardDetached` · **62** `CardCast`(`Arg` = 손패 항목 · `DefIndex` = 카드 줄 · `Amount` = 묶음 핸들) + `UnitSlain` 전용 `RewardMul` 칸 · 트레이스 **57~59** | 뷰(7c)가 규칙 사건을 모아 카드를 역산하지 않게 · 표식 배율은 값 스냅샷(계약 7) |
| `BindingDef.SubjectCost` · `TriggerDispatcher.SubjectPasses`(정적) | 직업 비트만 → 직업 ∧ 코스트 · 한 함수 | 카드 축 `Cost1`(옛 `MatchesDcAxis`) · 상속과 부착 즉시 전개가 같은 답 |
| 카드 묶음(`CardAttachment`) | 없음 → 규칙 몇 줄 + 공격 수식자(`OwnerInstanceId` = 핸들) · 핸들 = `InstanceId` 번호판 | 떼는 단위가 카드 |
| 카드 주기 규칙 | 스폰 위상 → **붙자마자 첫 발동**(`Elapsed = PeriodSeconds`) | 사용자 결정 2026-08-16(옛 `BattleBridge.Dreamcatcher.cs:622` `elapsed = periodSeconds`) |
| 호접몽 완주 버프의 칸 | `SlotTag(OnPlace, stackId)` → **`SlotTag.OfCard(InstanceId)`**(`CombatPhase.StepCocoon`) | 옛 `_dcStackCounter++`(100~ — 배치 칸과 번호판 분리). 배치 칸에 두면 유닛 저작 스택 id 와 같은 번호판을 써 서로를 덮는다 |
| `IntentApplier.ToCoreOrigin` | 이름 없는 값 → `Unspecified` → **번호로 옮긴다**(정의된 값만) | 드림스톤 출처(5)가 스킬 어휘에 없다 — 두 어휘는 번호가 정렬돼 있다(`SkillModifierOrigin` 헤더 「어댑터가 캐스트한다」) |
| 정의표 | `CardDef` +7칸(`Bindings`·`SquadBindings`·`AttackMods`·`ActiveBinding`·`NeedsTwoCells`·`TargetsEnemies`·`Requirement`) · `MatchDefinition.MatchBindings`(드림스톤) · `ClockOutSpec.MeteorProjectileDefIndex` — **기본값이면 한 줄도 안 쓴다**(퇴근 운석 줄만 기믹 절 안에 항상) | 골든 코퍼스에 카드·기믹 0 → 해시 무변 |
| `ResignationBarrage` | 7d 계획 → **7b**(리드 배정) | `ResignationThreshold` 소비자 · 7d 문서 행 이동 |
| 빌더 | `CombatDefinitionBuilder.Fill(…, extraProjectiles, cards)` · `MatchDefinitionBuilder.Build(…, cards, dreamstones)` · `GimmickProjectilesOf` · `BindingDefinitionBuilder.BindPattern` internal · `BattleDriver._cards`·`_dreamstones` | 카드 탄·패턴·장판·운석 탄이 **표를 굳히기 전에** 편입돼야 한다 |
| `MatchHandDeckTests` 고정구 | 규칙 없는 카드 → 무해한 규칙 한 줄 | 규칙 0 줄 카드는 이제 **거절**이다(옛 `attached == 0 → -1`) |

### 사용자 결정 필요 (7b) — **1건**

**배치 오라(느린 각성)의 수면은 언제부터 세나?** 옛 전투는 수면을 **스폰 순간**(배치 비행 시작)에 걸었고, 수면 감쇠가 배치 중에도 돌아(`CcDecaySystem` 이 `PendingDeployment` 를 안 거른다) **배치 모션 길이만큼 수면이 먹혔다** — 활성화 뒤 실제로 자는 시간 = 저작 초 − 배치 모션. 새 코어는 spec 대로 **활성화 사건**(`OnPlace`)에서 건다 → 저작 초 **전부**를 잔다. 라이브 저작은 `Card_SlowAwakening` 하나(수면 2초)이고 배치 모션은 유닛마다 다르다. **기본값 = 활성화 기준**(저작 문면 「배치된 유닛은 N초 잔다」에 맞음). 옛 체감을 원하면 「스폰 사건」 규칙이 하나 더 필요하다. 공속·Squad 상속은 배치 중에 공격하지 않아 시점 차가 안 보인다.

## 완료 기준

- [x] **헤드리스 초록**(Unity EditMode 는 MCP 세션 복구 뒤 — 아래 기록) + 새 테스트 7묶음: `CardAttachTests`(순서 ①②③ · 실패 무차감 · 상한 3 · `Immediate` 콜스택) · `SquadCardTests`(**퇴근해도 회수된다** · 이후 배치분 상속 · 회수 = 슬롯 삭제) · `PlacementAuraTests`(바인딩 2 · `revokeOnExpire` 비대칭) · `BountyMarkTests`(`fireCap 1` + 소멸까지 부착 · 두 효과 원자 · 상한 밖) · `RetireRecallTests`(선언 카드는 뒤 · 나머지는 부착 순서로 앞 · **사망 경로엔 안 붙는다**) · `ActiveCastTests`(쿨다운 = 판의 시계 · 성사와 차감이 한 함수 · 슬로모에서 느려진다) · `ApplicabilityTests`(preflight 와 bake 가 같은 답).
- [x] **증상 단언 3건**: ⑴ Squad 카드를 붙인 유닛을 **퇴근시키면 판 전체 버프가 사라진다** ⑵ 표식 붙인 적을 잡으면 각성이 **배로** 들어오고 카드가 손패로 돌아온다 ⑶ 인수인계 카드를 든 유닛을 퇴근시키면 **그 유닛의 다른 카드가 손패 맨 앞**에 온다.
- [x] **부여 생산자를 여는 커밋은 그 키의 `ImbueCapConfig` 줄을 같이 저작한다**(6a2 리뷰 F1). 상한 줄이 없으면 관문이 그 부여를 거절하므로(빌더는 어떤 키가 쓰일지 모른다) **카드가 조용히 안 걸린다.** 「생산자마다 상한 줄이 있다」를 테스트로 건다 — 카드 저작이 여는 `ImbueKey` 전부가 `MatchDefinition.ImbueCaps` 에 줄을 갖는지 훑는 한 건이면 된다.
- [x] `HandDeck` 안에 **효과가 한 줄도 없다**(unit 4 헤더의 약속) — 효과는 `BindingRegistry` 호출로만 나간다(grep).
- [x] **장부 두 장을 가른다.** `ledgers/rule-holders.md` 의 **D8·D9·D10·D11·D12·D14·D17·D20·D24 · K2**(unit 4 가 `4_match_owners_and_mode.md:87-104` 에 표로 든 그 행들)와 `ledgers/rules.md` 의 **F1 · S11 · S12 · S13 · S16 · E2 · E4** 가 각각 코드 포인터로 매핑된다. ⚠ 두 장부에 **같은 기호가 다른 뜻**으로 있다(`rules.md` 의 `C5` = 바늘 캐리어 · `rule-holders.md` 의 `C5` = 코스트) — 기호만 보고 옮기지 말 것.
- [x] `rules.md` **E4 문면 정정**: 「사망은 첫 하나만 발동한다」는 부정확하다 — 실측은 **같은 `skillId` 만 억제**하고 종류가 다른 규칙은 사망에서도 각각 발동한다. 결정 ① 의 답과 함께 문면을 고친다.
- [x] `ledgers/bridge-methods.md` 미정 **36 → 28**(8행): `NotifyEnemyGoneIfMarked/1` · `IsEnemyMarked/1` · `ApplyBountyMark/2` · `BuildHostProfile/1` · `TargetsEnemies/1` · `HasPositiveDamageOutput/1` · `SetDreamstones/1` · `ApplyPendingDreamstones/0`. 더해서 **주인 확정 1행**(잔량 무변): `HostBodyRadiusOf/1` 은 6c 가 「뷰」로 적었지만 유일 소비처가 **실드 파열 대상 수집**(`BattleBridge.cs:4620`·`:4635`)이라 판정이다 — 주인은 이 unit 의 파열 바인딩이다.
- [x] `core-reviewer` APPROVE(2026-09-24 · finding 0 — 매니저 0 · `HandDeck` 효과 코드 0줄(아키텍처 테스트) · 사건 60~62/트레이스 57~59 append-only · 결정론(Dictionary 순회 3곳 순서 무관·`RngStreams.Meteor`) · 옛 규칙 인용 전건 확인). 리드 export 재검증(`3fb35b8ff`): build 0 · test 629 · Check 0 · 미정 28. Unity lane 은 MCP 세션 복구 뒤(대기).
- [x] 7b 뒤에도 **카드는 화면에 없다**(손패 UI 가 7c). 라이브 확인은 디버그 커맨드(`DebugAttachCard`·`DebugCastCard`)로 한다 — **메뉴 UI 없이** 커맨드만으로 검증된다(6b2 의 규율).

> **이행 메모.** 7묶음 = `CardAttachTests`(9) · `SquadCardTests`(5) · `PlacementAuraTests`(2) · `BountyMarkTests`(5) · `RetireRecallTests`(2) · `ActiveCastTests`(5) · `ApplicabilityTests`(5) + `ResignationBarrageTests`(4) + `CoreArchitectureTests` 2건(손패 효과 0줄 · 탄 부여 생산자 그물). 증상 ⑴ `SquadCardTests.증상_Squad_카드를_붙인_유닛을_퇴근시키면_판_전체_버프가_사라진다` ⑵ `BountyMarkTests.증상_표식_붙인_적을_잡으면_각성이_배로_들어오고_카드가_손패로_돌아온다` ⑶ `RetireRecallTests.증상_인수인계_카드를_든_유닛을_퇴근시키면_그_유닛의_다른_카드가_손패_맨_앞에_온다`.
> **부여 상한 줄**: 7b 는 탄 부여(`ImbueGate.Grant`) 생산자를 **열지 않았다** — 카드의 착탄 효과(출혈 부리·서리 화살·자장가 다트)는 옛 RESOLVE 시점 그대로 공격 seam 에서 대상에 직접 걸고, 카드 탄(비수·부메랑)은 관문이 **시전자 저작 출력을 자동으로 접는다**(6a2 결정 ① — `IntentApplier.SpawnProjectile` 이 `Owner` 를 싣는다). 그래서 저작할 상한 줄이 0 이고, 그물(`탄_부여_생산자는_디버그_커맨드뿐이다…`)이 다음 생산자를 잡는다.
> **장부**: 「고친 것」 표 + 아래 매핑. `rule-holders` 와 `rules` 의 같은 기호(C5 등)는 섞지 않았다.

### `rule-holders.md` 귀속 행 → 코드 포인터

| 행 | 새 자리 |
|---|---|
| D8 숙주가 떠나면 전부 큐 맨 뒤 | `HandDeck.Recover(retired: false)` → `BindingRegistry.DetachCard` · 적 표식 숙주도 같은 문(`UnitDestroyed`) |
| D9 퇴근 + 인수인계 → 나머지가 부착 순서로 앞 | `HandDeck.Recover(retired: true)` · `RetireRecallTests` |
| D10 인수인계 판정은 한 곳 | `CardDef.DeclaresRetireRecall` — bake(`CardDefinitionBuilder.BakeMechanic` 의 `RecallAttachedToFront` 분기)가 **유일한 판정**이고 손패는 그 칸만 읽는다 |
| D11 부착 결과 규약 | `Receipt`(성사 / `RejectReason`) + 묶음 핸들(`CardAttachment`) — 「회수 불필요」는 숙주 소멸이 규칙을 자동으로 뗀다 |
| D12 적용 먼저, 값은 나중 | `HandDeck.TryAttach` ①②③ · `CardAttachTests.부착_즉시_규칙은_커맨드_콜스택_안에서_실행되고_그_뒤에_값을_치른다` · `실패한_부착은_무차감_무순환이다_이중_상태` |
| D14 표식은 상한 밖 | `HandDeck.TryAttach` 의 `!card.TargetsEnemies` 가드 · 적당 하나 = `CardBindings.IsMarked` → `DuplicateState` · `BountyMarkTests.표식은_부착_상한_밖이다` |
| D17 액티브 재활용 · 부착 이탈 | `HandDeck.TryCast`(뒤로) / `TryAttach`(풀 이탈) |
| D20 부착 목록은 부착 번호 오름차순 | `HandDeck.CompareByAttachSeq`(회수 순서) · 규칙 순회는 `InstanceId` 오름차순 |
| D24 부착 순서 자체가 기능 | `HandDeck._attachSeq` → `Recover` 정렬 |
| K2 성사 → 쿨다운, 확인과 커밋이 한 함수 | `HandDeck.TryCast`(준비 · 성사 · 차감 · 대기 · 재활용) · `ActiveCastTests.성사가_안_되면_차감도_대기도_재활용도_없다_포탈_같은_칸` |

### 구현 기록(2026-09-24)

`dd23e4578`(규칙 코어) · `3d03046e5`(사직서 barrage · 운석 탄 편입) · `7eea580b0`(카드 bake) + 이 문서 커밋.
헤드리스 export 3종(커밋마다 — 수치는 인계 보고): build 0 · test 590 → **625 → 629** · Check 0. `check_ledgers.py` exit 0 · 미정 **28** · rules 보류 **32**.
골든: 코퍼스에 카드·기믹 **0** 이라 정의표 해시·사건 줄 무변 **예상** — Unity EditMode 골든 11종 확인 대기(MCP 세션 끊김).
Assets lane 새 테스트(`CardBakeTests` 5 · `GimmickAuthoringTests` 운석 줄 1)는 헤드리스 **컴파일만** 확인했다(스크래치 csproj — UnityEditor·nunit 참조) — 실행은 Unity 대기.

