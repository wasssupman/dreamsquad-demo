# 2 — 레거시 `OnPlaceEffectType` 9종

## 목적

**두 번째 어휘를 죽인다.** `BattleBridge.cs:5393~5590` 의 200줄 if/else 체인과
`OnPlaceEffectType` enum 11종, `DefenderUnitData` 의 flat 필드 7개가 여기서 사라진다.

`on-place-skill-rework` 계약 2 가 예약해 둔 「레거시 전량 이관」이 바로 이 작업이다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:5393~5590` — 체인 철거
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — enum + flat 필드
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 문안 case 10개
- 에셋 12개 재저작

## 구현

1. ~~**그물이 절대 선행이다.** PlayMode 커버리지가 **9종 중 3종**뿐이다.~~
   **stale — 2026-08-26 실측으로 정정.** 그 사이에 그물이 붙었다:

   | 값 | 종류 | PlayMode 그물 | 라이브 저작 |
   |---|---|---|---|
   | 1 | `SlowPulse` | **없음** | **0개** |
   | 2 | `BoostNearbyDefenders` | `OnPlaceBoostNearbyTest` | 1 |
   | 3 | `BindNearby` | `OnPlaceBindNearbyTest` | 1 |
   | 4 | `MeleeBurst` | `OnPlaceRuleTriggerTest`(간접) | 1 |
   | 5 | `ForwardProjectile` | `OnPlaceForwardProjectileTest` | **4** |
   | 6 | `GainCost` | `OnPlaceGainCostTest` | 1 |
   | 7 | `ReduceSkillCooldown` | `OnPlaceReduceSkillCooldownTest` | 1 |
   | 8 | `ApplyStackNearby` | `OnPlaceApplyStackNearbyTest` | 1 |
   | 9 | `StunNearby` | `OnPlaceStunNearbyTest` | 1 |
   | 10 | `DotNearby` | `OnPlaceDotNearbyTest` | 1 |

   **선행 작업이 「그물 6개 신설」에서 「`MeleeBurst` 의 직접 그물 1개」로 줄었다.**
   그리고 **`SlowPulse` 는 이전 대상이 아니라 삭제 대상**이다 — 그물도 없고 라이브
   저작자도 0이라 옮길 것 자체가 없다(옮기면 아무도 안 쓰는 concrete 가 생긴다, 제약 8).
2. ~~**신규로 필요한 payload kind 는 `AreaCc{DcCcKind}` 하나 정도**다.~~
   **stale — 2026-08-26 체인 전수 대조로 정정.** 하나가 아니라 셋이고, 하나는 성격이 다르다.

   | 값 | 유닛 | 오늘 하는 일 | 목표 | 신규 어휘 |
   |---|---|---|---|---|
   | 1 `SlowPulse` | — | (3과 **같은 분기**) | **삭제** | — |
   | 4 `MeleeBurst` | bruiser | 적에 즉발 피해 | `SelfTileAoe` | 없음 ✅ |
   | 5 `ForwardProjectile` | 4기 | **통로 스윕 피해** | `EmitProjectilePattern` | 없음, 단 ⚠아래 |
   | 6 `GainCost` | scout | 코스트 획득 | MetaIntent(이미 있음) | payload 1 |
   | 7 `ReduceSkillCooldown` | ranger | 쿨다운 감소 | MetaIntent(이미 있음) | payload 1 |
   | 2 `BoostNearbyDefenders` | guardian | **아군**에 DamageMul(TTL) | 스탯 오라 | ↓ |
   | 3 `BindNearby` | archer | **적**에 MoveSpeedMul(TTL) | 스탯 오라 | ↓ |
   | 8 `ApplyStackNearby` | slasher | 적에 스택 도포 | 광역 스택 | payload 1 |
   | 9 `StunNearby` | malphite | 적에 Stun + 피해 + **넉업 연출** | 광역 CC | payload 1 |
   | 10 `DotNearby` | busters | 적에 DoT + **빔 연출** + 쿨다운 밀기 | 광역 DoT | payload 1 |

   **오라 둘은 기존 concrete 를 일반화하면 공짜다.** `AllySpeedAuraSkill`(Id 2)이 이미
   `ApplyStatModifier` 를 `Selector = MoveSpeedMul` 로 방출한다 — **selector 축이 이미 있고
   concrete 가 그것과 「아군」을 하드코딩했을 뿐**이다. 스탯 축과 대상 진영을 params 로
   올리면 한 concrete 가 셋을 덮는다(보스 채찍 + 가디언 + 궁수).

## 결정 (사용자, 2026-08-26)

**① `ForwardProjectile` → 진짜 투사체로 재저작한다.** 라이브 4기의 체감이 바뀌는 것을
받아들인다. 통로 스윕 보존용 어휘는 만들지 않는다 — 샷건맨과 같은 형태로 수렴시킨다.
⚠ 이 슬라이스(2f)는 **밸런스 변경**이므로 커밋 메시지와 handoff 에 그렇게 적는다.

**② 「자기 공격 대기」를 스킬 어휘에 추가한다.** 채널링은 계속 나올 개념이고,
버스터즈 하나가 아니라 **어휘의 구멍**이라 메운다.

⚠ **새 컴포넌트를 만들지 않는다** — 그 필드는 이미 있다. 레거시 arm 이 쓰는
`AttackState.cooldownRemaining` 이 곧 「공격 대기」이고, 어댑터가 `max(현재, 요청)` 으로
민다(이미 걸린 쿨다운을 줄이지 않는다는 레거시 규칙 그대로). 도메인은 「나는 N초
묶인다」만 말하고 그것이 쿨다운인지 락 컴포넌트인지 모른다.

## ⚠ (해결됨) 결정이 필요했던 둘

**① `ForwardProjectile` 은 기계적 이전이 아니다.** 오늘 이 arm 은 탄을 안 쏜다 —
`ApplyForwardOnPlaceProjectile` 이 폭 1.2타일 통로를 훑어 **즉발 피해**를 준다.
`EmitProjectilePattern` 으로 옮기면 **진짜 투사체가 날아가는 다른 메커니즘**이 되고,
라이브 4기(머신거너·마크스맨·피어서·스나이퍼)의 체감이 바뀐다. 샷건맨이 이미 그 형태다.

- (a) 패턴으로 재저작 — 스펙의 「에셋 12개 재저작」이 뜻하는 바로 그것. **밸런스 변경**이다.
- (b) 통로 스윕을 그대로 보존하는 payload 를 신설 — 어휘가 하나 늘지만 무회귀.

**② 뷰 부작용 둘을 어디에 두나.** `StunNearby` 의 넉업 홉과 `DotNearby` 의 빔은
브리지가 **직접** 재생한다(큐 안 거침). 규칙 경로로 옮기면 `PlayVisual` 의도를 거쳐야 하고,
`DotNearby` 는 추가로 **host 공격 쿨다운을 밀어** 조사 중 평타를 막는다 — 그건 연출이 아니라
규칙이라 의도 어휘에 없다(`SimIntentKind` 에 「자기 공격 잠금」이 없다).

## 작업 분할 (한 unit 이 너무 크다)

| 슬라이스 | 내용 | 신규 어휘 | 상태 |
|---|---|---|---|
| **2a** | `MeleeBurst` → `SelfTileAoe` | 없음 | **완료** (2026-08-26) |
| **2b** | 오라 일반화 → `BoostNearbyDefenders` · `BindNearby` | payload 2 | **완료** (2026-08-26) |
| **2c** | `GainCost` · `ReduceSkillCooldown` (ECS 무관) | payload 2 | **완료** (2026-08-26) |
| **2d** | `ApplyStackNearby` | payload 1 | **완료** (2026-08-26) |
| **2e** | `StunNearby` · `DotNearby` | payload 2 + 「자기 공격 대기」 | **완료** (2026-08-26) |
| **2f** | `ForwardProjectile` | 없음(패턴 재저작) | 착수 가능 · **밸런스 변경** |
| **2g** | 철거(enum · flat 필드 · 체인 · 문안 · **`SlowPulse` 삭제**) | — | 위 전부 뒤 |

⚠ **`SlowPulse` 삭제는 2g 로 옮겼다.** enum **중간 값**을 지우면 뒤 값이 전부 한 칸씩
밀리고, 저작은 YAML 에 **정수**로 박혀 있다 — 라이브 12개 에셋이 조용히 다른 효과가 된다
(`BoostNearbyDefenders` 2→1 …). enum 전체가 사라지는 철거 시점에만 안전하다.

## 2a 에서 나온 것 (2026-08-26)

**`MeleeBurst` → `SelfTileAoe` 는 「기존 어휘 재사용」이 아니었다.** 셋이 달랐고 그물이 전부 잡았다:

1. **`SelfTileAoe` 는 폭발 VFX 가 필수다.** 없으면 bake 가 loud 거절하고, 통과시켜도
   드레인이 `dataIndex<0` 요청을 통째로 버려 **피해까지 안 나간다**. 브루저는 연출이
   없었으므로 `Projectile_BruiserShock`(짱쎈 진동 파형 재사용, 스케일만 축소)을 새로 저작했다.
   → **브루저가 배치 시 충격파를 갖게 된다**(콘텐츠 변경, 작지만 변경이다).
2. **투사체 요청 드레인은 `_running` 아래다.** 배치 페이즈에 머무는 하네스에서는 캐리어가
   만들어지고 **영원히 안 풀린다**(프레임 계측: carrier 1 · projectile 0 이 8프레임 유지).
   레거시는 `IncomingDamage` 직접 주입이라 이 경로가 필요 없었다 — **이전이 그물의 전제를
   바꾼 자리**다. 그물이 `StartBattle()` 을 부르게 고쳤다.
3. **광역 탄이 시전자의 공격 층을 안 들고 갔다.** 레거시는 후보를 모으는 단계에서 걸렀고
   광역 탄 경로는 후보를 안 모으므로 **탄이 대신 들고 가야 한다.** 안 실으면 0 = 무제한이라
   근접 유닛의 폭발이 하늘의 적을 때린다. 보스 자폭이 여태 이 구멍을 안 밟은 건 보스가
   전 층을 때려서다 — `EcsSkillContext` 의 `SpawnProjectile` 이 이제 실어 보낸다.

**즉발성은 지켰다.** 캐리어를 한 번 거치므로 한 프레임 더 들지만, 그물이 창을 4프레임으로
넓히면서 **총량 단언(「저작값 정확히 한 번」)은 그대로** 뒀다 — 넓힌 것은 창이지 계약이 아니다.
3. **Mono 도메인 arm 2개를 판정대로 처리한다** — `GainCost`(`:5564`, `CostRuntime`) ·
   `ReduceSkillCooldown`(`:5571`, `skillRuntime`)은 **ECS 를 전혀 안 만진다.**
   토대 unit 0 이 「Mono 계열 intent」 또는 「예외」로 판정한 것을 따른다.
4. **`onPlacePush*` 3필드 + `ApplyOnPlacePush` 를 같이 뗀다** — **에셋 소비자 0**이 확인됐다
   (`Assets/_Project/Data` 전수 grep, nonzero 0건). 샷건맨이 마지막 소비자였고 규칙 경로로
   갈아탔다. 무비용 철거다.
5. **시트는 안전하다** — `Data/StatImport/UnitStatImportDto.cs` 에 onPlace 필드가 **0건**이라
   flat 필드 7개 삭제가 시트 왕복을 깨지 않는다.
   ⚠ 단 에셋 12개를 재저작하는 동안 **로그인 시트 임포트가 끼어들어 무관한 유닛 스탯이
   딸려 커밋될 수 있다.** 커밋 전 `git diff --stat` 로 확인한다.

## 2b 에서 나온 것 (2026-08-26)

**한 구현이 셋을 덮는다.** `StatAuraSkill` 하나에 얇은 파생 셋(보스 채찍 · 가디언 · 궁수).
파생이 선언하는 것은 **네 축**뿐이다 — 누구에게(아군/상대) · 무슨 스탯(고정/저작) ·
자기 포함 · 병합 출처. 그 넷이 갈리는 것을 `StatAuraSkillTests` 가 각각 한 줄로 못박는다.

**진영은 payload 가, 스탯은 저작이 정한다.** 진영을 저작 필드로 두면 「아군을 감속시키는」
저작이 표현 가능해진다 — 그건 어떤 콘텐츠의 사양도 아니다. 그래서 payload kind 를 둘로 갈랐다.

**보스 채찍은 저작을 안 읽는다.** 그 슬롯들은 `buffStat` 을 안 채워 왔고, 읽기 시작하면
기본값 0(공격력)이 되어 **채찍이 조용히 다른 오라가 된다.** payload 이름이 이미 스탯을
말하므로 파생이 고정한다.

**병합 출처를 파생 축으로 뒀다.** 하나로 묶으면 보스 채찍과 배치 오라가 같은 키를 공유해
서로를 덮는다.

**저작이 배율에서 퍼센트로 바뀌었다**(1.3 → 30, 0.1 → −90). 레거시 arm 은 배율을 그대로
넘겼고 오라 어휘는 퍼센트다. 값은 동일하고 **읽기가 나아졌다**(+30% / −90%).

**세 그물이 새 어휘를 잡았다** — 전부 「조용히 잊는 것」을 막는 전수성 장치다:
`DcApplicability` 미분류 · `UnitKitSummary` 문안 누락 · `buffStat` 축이 유닛 bake 에
없어서 궁수가 공격력 오라가 될 뻔한 것. 마지막은 `MapDcBuff` 단일 번역 지점을 쓰게 했고,
`EffectiveHealth` 는 **산식이 역수**라 concrete 의 퍼센트→배율과 갈리므로 loud 거절했다.

## 2c 에서 나온 것 (2026-08-26)

**어댑터가 `GameManager` 를 직접 부르지 않는다.** 코스트·쿨다운은 Mono 쪽 자원이라
어댑터가 그리로 손을 뻗으면 제약 1(브리지 유일 창구)이 조용히 무너진다. 대신 **브리지가
델리게이트를 넣어 준다**(`BindMetaSink`) — 큐 싱크와 같은 패턴이고, 그 델리게이트가
스킬 레이어와 Mono 자원 사이의 유일한 통로다.

**「즉시 반영」이 계약이라 큐를 안 탄다.** 그래서 `MetaIntent` 는 `SimIntent` 와 별도
어휘이고, 그물이 「판 밖 스킬이 시뮬 의도를 하나도 안 낸다」를 단언한다.

**음수를 「뺏기」로 열지 않았다.** 통과시키면 오타 하나가 조용히 플레이어를 벌준다.
필요해지면 별도 사양으로 연다.

⚠ **두 효과가 한 틱 늦어졌다.** 레거시는 `PlaceDefenderAs` **호출 안**에서 동기 적용이라
그물 둘이 프레임을 안 흘리고 읽었다(하나는 「yield 금지」를 주석으로 못박고 있었다).
규칙 경로는 다음 틱이므로 프레임을 흘려야 하고, 그러면 **자연 회복·자연 감소가 측정에
섞인다.** 코스트는 `StopRegen()` 으로 격리했고, 쿨다운은 격리가 불가능해 **오차로 명시**했다
(흘린 프레임의 감소는 수십 ms 라 저작량 2초와 자릿수가 다르다).
체감상 한 프레임은 무시할 만하지만 **계약이 바뀐 것은 사실**이라 여기 적는다.

## 2d 에서 나온 것 (2026-08-26)

**상한은 스킬의 것이 아니다.** 「출혈은 몇 겹까지 쌓이나」는 스택 종류의 성질이지
시전자의 저작이 아니다 — 유닛마다 다른 상한을 적을 수 있게 두면 같은 출혈이
누구에게 걸렸느냐로 다르게 쌓인다. concrete 는 상한을 아예 모르고, 어댑터가
저작 SO 목록에서 푼 표(`BindStackCaps`)로 스택 종류에서 꺼낸다.

⚠ 카드 경로의 `ApplyStackToTarget` 은 **`tileRange` 를 상한으로 겸직**시킨다.
광역 스택은 반경과 상한이 둘 다 필요해 그 겸직을 물려받을 수 없었고, 그래서
별도 payload 로 갈랐다. **그 겸직은 unit 8 이 정리할 목록에 있다.**

**축이 셋이 됐다.** `Selector`(cc) · `StatSelector` · `StackSelector` — 한 슬롯이
셋을 다 쓰는 스킬이 나오는 순간 겸직은 조용히 갈리므로 전용 축으로 열었다.
유닛 bake 가 스탯·스택 둘 다 안 옮기고 있어서 각각 「기본값이 진짜처럼 보이는」
함정이었다(스탯 0 = 공격력 · 스택 0 = None).

**그물이 실제 경로를 안 보고 있었다.** `OnPlaceApplyStackNearbyTest` 는 가디언
사본에 레거시 필드를 꽂아 썼다 — 그대로 두면 이전 뒤에도 **은퇴 예정인 arm 만**
계속 재고 실제 저작을 안 본다. 실제 난도질꾼을 쓰게 고쳤다.

## 2e 에서 나온 것 (2026-08-26)

**「자기 공격 대기」에 새 컴포넌트가 필요 없었다.** 그 필드는 이미 있다 —
`AttackState.cooldownRemaining` 이고 레거시가 이미 그걸 민다. 어휘(`DelaySelfAttack`)만
열면 되고, 도메인은 그것이 쿨다운인지 락인지 모른다. `max` 인 이유도 레거시 그대로다:
줄이면 채널링이 오히려 공격을 앞당기는 자리가 된다.

**뷰 부작용 둘은 채널로 갈렸다.** 넉업 홉은 기존 `KnockupVisualEvents` 를 그대로 타고
(심에서 넉업의 실체는 짧은 스턴이라 뷰가 `CcEffect` 로는 구분 못 한다 — 띄운 쪽이 대상을
직접 신호하는 계약), 빔은 큐가 없어 코스트·쿨다운과 같은 **델리게이트**로 넘긴다.
어댑터는 프리팹을 모르고 **index 만** 넘긴다(투사체 dataIndex 와 같은 규약).

**「얼마나 높이 띄우나」는 스킬 저작이 아니라 유닛의 성질**이다(평타든 배치든 같다).
그래서 params 가 아니라 질의(`UnitStat.KnockupVisualHeight/HopSeconds`)이고,
어댑터가 이미 있던 `DefenderCcData` 에서 읽는다.

**아무도 없으면 안 묶인다.** 조사가 대상 0인데 공격 대기만 걸면 「아무 일도 안 했는데
공격만 못 하는」 순수 손해가 된다.

## ⚠ 유닛 bake 가 저작 선택자 **셋을 전부** 안 옮기고 있었다

`ccKind` · `buffStat` · `stackKind`. 셋 다 **기본값이 진짜처럼 보이는** 함정이라
「안 붙는다」가 아니라 **「다른 게 붙는다」**이고 로그도 안 난다:

| 축 | 기본값 0 의 뜻 | 증상 |
|---|---|---|
| `CcKind` | **감속** | 「기절」 저작이 조용히 감속이 된다 |
| `StatKind` | **공격력** | 「이동속도 감쇠」가 조용히 공격력 오라가 된다 |
| `StackKind` | None | 「출혈 도포」가 조용히 아무것도 안 건다 |

셋을 한 자리에 모아 옮기고 이유를 코드에 박았다. **카드 bake 는 셋 다 옮기고 있었다** —
유닛 경로만 비어 있었고, 그 payload 들이 유닛 저작으로 처음 오면서 드러났다.

## ⚠ `.meta` guid 함정

새 에셋의 `.meta` 를 손으로 쓰면 **Unity 가 import 하며 자기 guid 로 덮는다.** 그러면
그 guid 를 참조한 쪽이 조용히 끊긴다(부착이 `null` 이 되고 스킬이 안 붙는다).
새 에셋을 만들면 **import 뒤에 실제 meta guid 를 읽어 참조를 맞출 것.**

## 완료 기준

- [ ] `MeleeBurst` 직접 그물 1개 신설 (나머지 8종은 이미 있다 — 위 표)
- [ ] `SlowPulse` 는 **삭제**한다(라이브 저작 0 · concrete 신설 금지)
- [ ] `OnPlaceEffectType` enum 과 `BattleBridge.cs:5393~5590` 체인이 **삭제**됐다
- [ ] `DefenderUnitData` flat 필드 7개 + `onPlacePush*` 3필드 + `ApplyOnPlacePush` 삭제
- [ ] `UnitKitSummary` 문안이 저작 SO 를 읽는다
- [ ] 에셋 12개 재저작 커밋에 **무관한 유닛 스탯 변경이 섞이지 않았다**
- [ ] Assets lane + PlayMode 배치 스킬 테스트 초록 + Play 육안(9종)
