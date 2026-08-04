> unit 8 부속 — 게이트·부재-상태·쓰기 지도 전수 인벤토리(기계 추출 2026-08-04). 번역 규칙은
> [m1_blueprint_data_mapping.md](m1_blueprint_data_mapping.md) 본문이 소유한다.

# battle-sim-extraction unit 8 ?먯옄猷???寃뚯씠??쨌 遺???곹깭 쨌 ?곌린 吏??
??? `Assets/_Project/Scripts/Battle/` ??ISystem 44媛?(Combat 9 / Effects 26 / Movement 2 / Units 7).
異붿텧 諛⑹떇: ???뚯씪 吏곸젒 ?먮룆 (`RequireForUpdate` / `RequireAnyForUpdate` / `WithNone` / `RefRW` / `isReadOnly:false` lookup / ECB ?몄텧 / ?깃?????enqueue).

吏묎퀎 ?붿빟:

| ??ぉ | ??|
|---|---|
| ISystem 珥앷퀎 | 44 |
| `RequireForUpdate<T>()` 蹂댁쑀 | **35** (湲곕?移??쇱튂) |
| `RequireAnyForUpdate(query??` 蹂댁쑀 | 4 |
| 寃뚯씠??蹂댁쑀 ?뚭퀎 (35 + 4, 寃몄슜 0) | 39 |
| 臾닿쾶?댄듃 (留?tick ?ㅽ뻾) | 5 |
| `WithNone<>` ?ъ슜 ?쒖뒪??/ ?몄텧 ?ъ씠??| 26 / 48 |
| `EntityCommandBuffer` ?ъ슜 | 28 (?꾨? `Allocator.Temp` + 媛숈? OnUpdate ??Playback) |
| ECB 誘몄궗??(吏곸젒 ?곌린/?먮쭔) | 16 |

---

## A. RequireForUpdate 留ㅽ듃由?뒪

寃뚯씠?몃뒗 **AND** ?????섏뿴?????以??섎굹?쇰룄 ?뷀떚??0 ?대㈃ `OnUpdate` 媛 ?듭㎏濡??ㅽ궢?쒕떎. `RequireAnyForUpdate` 留?OR.

### A-1. Combat (9/9 ?꾩썝 寃뚯씠??蹂댁쑀)

| ?쒖뒪??| 寃뚯씠??| ?됰룞 ?⑥쓽 |
|---|---|---|
| `AttackSystem` | `AttackState` | 怨듦꺽??AttackState 蹂댁쑀) 0 ?대㈃ ??猷⑦봽 誘몄떎?? **遺???뺤?**: 媛숈? OnUpdate 癒몃━???덈뒗 `CastEventsSingleton` ?쒕젅???댁???罹먯뒪??= host ??怨듦꺽 ?ш굔)???④퍡 硫덉떠 ?먭? ?곸옱?쒕떎. |
| `BossPeriodicTriggerSystem` | `DcTriggerSlot` 쨌 `FlowFieldSingleton` | 移대뱶 ?щ’ 蹂댁쑀 ?좊떅 0 ?먮뒗 flow field 誘몃퉴??留?濡쒕뵫 ?? ??二쇨린 ?몃━嫄?`elapsed` ?꾩궛 ?먯껜媛 ?뺤?. 留??놁씠 ?щ’留??덉뼱?????덈떎. |
| `EnemyAiStateSystem` | `EnemyAiState` | FSM 蹂댁쑀 ??0 ???곹깭 ?꾩씠 ?놁쓬. Movement ??`aiStateLookup` 遺????`Marching` ?대갚?대씪 ?대룞? 怨꾩냽?쒕떎. |
| `HealthThresholdSystem` | `FlowFieldSingleton` **留?* | `ThreatEntry` 寃뚯씠?몃? ?섎룄?곸쑝濡??쒓굅(unit 1 二쇱꽍) ??蹂댁뒪 ?놁씠 ?뷀렂?붾쭔 ?덉뼱??`last_stand` 媛 ?뚯븘???섎?濡? threat drain ? `TryGetSingletonRW` + `HasBuffer` 濡??낅┰ 媛?? |
| `ProjectileEmitterSystem` | `EmitterInstance` 쨌 `FlowFieldSingleton` | 吏꾪뻾 以?諛쒖궗 ?몄뒪?댁뒪 0 ??誘몄떎?? ?몄뒪?댁뒪 踰꾪띁媛 鍮꾩뼱 ?덉뼱??湲몄씠 0) **踰꾪띁 蹂댁쑀** ?먯껜濡?寃뚯씠?몃뒗 ?듦낵?섎?濡?猷⑦봽 ??`instances.Length == 0` continue 媛 ?ㅼ젣 ?꾪꽣?? |
| `ProjectileHitSystem` | `ProjectileTag` | ?ъ궗泥?0 ??誘몄떎?? 李⑺깂 ?닿껐쨌splash쨌TileAoe쨌PathHit ?꾨? ??寃뚯씠???꾨옒. |
| `ProjectileMoveSystem` | `ProjectileTag` | ?ъ궗泥?0 ??誘몄떎?? |
| `TauntAttackGrantSystem` | **Any**(`Aggroed`, `TauntAttackGranted`) | ?닿렇濡??곸씠 ?놁뼱??**strip ?⑥뒪媛 ?댁븘 ?덉뼱??* ?섎?濡?OR. 遺?щ텇 ?뚯닔 ?꾨씫 諛⑹?. |
| `UltimateLeapSystem` | `UltimateLeapState` | ?댄깉 以??좊떅 0 ??誘몄떎?? ?곹깭媛 怨?吏꾪뻾 以??쒗?ㅻ씪 ?먭린-寃뚯씠?? |

### A-2. Effects (22/26 寃뚯씠??蹂댁쑀)

| ?쒖뒪??| 寃뚯씠??| ?됰룞 ?⑥쓽 |
|---|---|---|
| `AggroStateSystem` | **Any**(`AggroCapacity`, `Aggroed`) | 留덉?留?媛?붿뼵 ?뚮㈇ ?꾩뿉??orphan ?댁젣 ?⑥뒪媛 ?뚯븘???댁꽌 OR (二쇱꽍: 援?HIGH1 蹂댁〈). |
| `AllyBuffFieldSystem` | `AllyBuffField` 쨌 `StatModifierApplyEventsSingleton` | ?ν뙋 罹먮━??0 ???щ컻???뺤? = 踰꾪봽媛 `AllyBuffApplySec` ?덉뿉 ?먯뿰 ?뚮㈇(?뚯닔 硫붿빱?덉쬁??寃뚯씠??洹??먯껜). |
| `CcApplySystem` | `EnemyCcEventsSingleton` | ???깃???遺??釉뚮━吏 ?뗭뾽 ?? ??CC 遺???꾨㈃ ?뺤?. 紐⑤뱺 CC ?앹궛?먭? ???먮줈 ?섎졃?섎?濡??⑥씪 ?ㅽ뙣?? |
| `CcClearSystem` | `CcClearRequestsSingleton` | wake-on-hit(Sleep ?댁젣) ?꾩슜 ?뚮퉬?? 遺?????쇨꺽?대룄 ??源⑥뼱?? |
| `DefenderFieldSystem` | `DefenderFieldSingleton` | ?꾨뱶 誘명븷????誘몄떎?? 異붽?濡??고??꾩뿉 `bossQuery.IsEmpty` 硫??щ퉴??skip(?뚮퉬??蹂댁뒪肉?. |
| `DreamCocoonSystem` | `DreamCocoon` | ???꾩＜ 媛먯떆 ???0 ??誘몄떎?? |
| `EffectTickSystem` | **Any**(`TornadoField`, `PortalLink`, `AllyBuffField`) | ??罹먮━??以??섎굹?쇰룄 ?덉쑝硫??ㅽ뻾. ?????놁쑝硫??섎챸 tick ?먯껜媛 遺덊븘?? |
| `FatigueAccrualSystem` | `BurnoutGimmickConfig` 쨌 `StackModifierApplyEventsSingleton` | **config 議댁옱 = 湲곕? ?쒖꽦** 愿?⑷뎄(self-gate, `BurnoutGimmickConfig.cs` 二쇱꽍??紐낆떆). 鍮꾪솢???쒖쫵??lazy-attach ?⑥뒪源뚯? 0 鍮꾩슜. |
| `HazardCastSystem` | `HazardCastState` 쨌 `FlowFieldSingleton` 쨌 `HazardSpawnRequestsSingleton` | 3以?AND. 釉뚮━吏媛 spawn ?먮? 留뚮뱾湲??꾩뿏 罹먯뒪?곗쓽 **荑⑤떎??tick ?????덈떎**(荑⑤떎??媛먯궛??猷⑦봽 ?덉뿉 ?덉쓬). |
| `HazardLifetimeSystem` | `HazardSingleton` | 誘몄떎????`cellToEffects` 媛 **Clear ?섏? ?딆븘** 吏곸쟾 ?꾨젅??留듭씠 洹몃?濡??⑤뒗????ZoneApply 媛 stale ????쎌쓣 ???덈뒗 援ъ“(??ZoneApply ??媛숈? ?깃??댁쓣 寃뚯씠?몃줈 ?붽뎄?섎?濡??숈떆 ?뺤?). |
| `HeatAccrualSystem` | `OnsenGimmickConfig` | ?⑥쿇 湲곕? self-gate. |
| `LastRunSystem` | `RedBullGimmickConfig` | ?덈뱶遺?湲곕? self-gate. 鍮꾪솢?????대? 遺숈? `LastRun` ??crash ???곴뎄 蹂대쪟. |
| `ModifierApplySystem` | `StatModifierApplyEventsSingleton` 쨌 `StackModifierApplyEventsSingleton` | **AND ?쇱꽌** ?ㅽ깮 ?먮쭔 ?놁뼱??stat ?곸슜源뚯? ?④퍡 硫덉텣?? 紐⑤뵒?뚯씠???뚯씠?꾨씪???꾩껜???⑥씪 ?ㅽ뙣?? |
| `ObstacleLifetimeSystem` | `ObstacleSingleton` | 誘몄떎????`blockedCells` 誘멸갚?????대룞 trim ??stale 李⑤떒 ???蹂몃떎. |
| `PatrolFieldSystem` | `PatrolAnchor` 쨌 `FlowFieldSingleton` | ?쒖같蹂?0 ??`PatrolStep` 誘멸갚?? Movement ??`PatrolStep` **蹂댁쑀 ?щ?**濡??쒖같 ?꾪궎??낆쓣 ?먮퀎?섎?濡?寃뚯씠?멸? ?ロ엳硫?湲곗〈 dir 濡?怨꾩냽 嫄룸뒗??媛믪씠 ?⑥븘 ?덉쓬). |
| `PickupConsumeSystem` | `RedBullGimmickConfig` 쨌 `FlowFieldSingleton` 쨌 `StatModifierApplyEventsSingleton` | 3以?AND. |
| `PickupSpawnSystem` | `RedBullGimmickConfig` 쨌 `PickupSpawnState` | ?꾨낫 ? ?곹깭(留?鍮뚮뱶 ?곕Ъ) 遺?????ㅽ룿쨌留뚮즺 tick 紐⑤몢 ?뺤? ???대? ?볦씤 ?쎌뾽??留뚮즺?섏? ?딅뒗?? |
| `ResignationDropSystem` | `ClockOutGimmickConfig` | ?닿렐 湲곕? self-gate. |
| `ResignationThresholdSystem` | `ClockOutGimmickConfig` 쨌 `MeteorBarrageRequestsSingleton` | barrage ??遺?????ъ쭅?쒓? ?뚮え?섏? ?딄퀬 臾댄븳 ?곸옱. |
| `ShieldCastSystem` | `ShieldCastState` 쨌 `FlowFieldSingleton` | ?ㅻ뱶 罹먯뒪??0 ??誘몄떎??荑⑤떎??tick ?ы븿). |
| `StackModifierTickSystem` | `EnemyCcEventsSingleton` 쨌 `DotApplyEventsSingleton` 쨌 `StatModifierApplyEventsSingleton` | **3以?AND???뚭? ?꾪뿕**: ?섎굹留??놁뼱???ㅽ깮 tick ?꾩껜媛 硫덉떠 `header.remaining` ??媛먯궛?섏? ?딅뒗??= ?ㅽ깮???곴뎄 ?붿〈. `StatModifierTickSystem`(臾닿쾶?댄듃)怨?鍮꾨?移? |
| `ZoneApplySystem` | `HazardSingleton` 쨌 `EnemyCcEventsSingleton` 쨌 `FlowFieldSingleton` | 3以?AND. 異붽?濡??고??꾩뿉 `cellToEffects.Count()==0` early-return. |

### A-3. Movement (2/2)

| ?쒖뒪??| 寃뚯씠??| ?됰룞 ?⑥쓽 |
|---|---|---|
| `BlinkApplySystem` | `BlinkRequestEventsSingleton` | ?붾젅?ы듃 ?붿껌 梨꾨꼸 遺?????꾩튂 ?곸슜 ?놁쓬. `UltimateLeapSystem` ??李⑹? ?붾젅?ы듃????梨꾨꼸濡??섍?誘濡?梨꾨꼸 遺????沅곴레湲?李⑹?媛 ?쒖옄由??곗텧留??대룞). |
| `MovementSystem` | `PathFollowState` 쨌 `FlowFieldSingleton` | ?대룞泥?0 ?먮뒗 flow field 誘몃퉴?????대룞쨌?ы깉쨌?좊꽕?대룄쨌goal ?먯젙쨌`PastGoalTag` 遺???꾨? ?뺤?. |

### A-4. Units (6/7)

| ?쒖뒪??| 寃뚯씠??| ?됰룞 ?⑥쓽 |
|---|---|---|
| `DamageApplicationSystem` | `IncomingDamage` | **踰꾪띁 蹂댁쑀 ?뷀떚??0 ?대㈃ ?쒖뒪???꾩껜 誘몄떎??* ??媛숈? 猷⑦봽???뱁엺 Regen ?먃?IncomingHeal` ?쒕젅?맞룹떎??蹂묓빀/?≪닔쨌`DamagedCounter` tick쨌??洹?띉?DeadTag` 遺?ш퉴吏 ?숇컲 ?뺤?. 寃뚯씠?몃뒗 踰꾪띁 **鍮꾩뼱 ?덉쓬**???꾨땲??**遺??*留?蹂몃떎. |
| `HealthDeathSystem` | `Health` | ?ъ떎??臾댁“嫄??듦낵(?좊떅???덉쑝硫??깅┰). ?덉쟾留??깃꺽. |
| `HitFlashSystem` | `HitFlashTag` | ?뚮옒??以??좊떅 0 ??誘몄떎?? ?쒓렇媛 怨?吏꾪뻾 ?곹깭. |
| `LethalTimerSystem` | `LethalTimer` | ?먰룺 ??대㉧ 蹂댁쑀 0 ??誘몄떎?? |
| `PatrolLifecycleSystem` | `SummonedBy` | ?뚰솚???쒖같蹂?0 ??誘몄떎???뚰솚???щ쭩 ?곕룞 ?뺤?). |
| `UnitLifecycleSystem` | **Any**(`_pastGoalQuery`=PastGoalTag+AttackUnitTag, `_deadQuery`=DeadTag) | OR. ?좎텧 ?먮뒗 ?щ쭩 以??섎굹?쇰룄 ?덉쑝硫??ㅽ뻾. ?댁????뚭눼/?뷀렂???щ쭩 猷⑦봽????OR ?꾨옒???뱁? ?덈떎(????DeadTag 瑜??붽뎄?섎?濡?而ㅻ쾭??. |

### A-5. 臾닿쾶?댄듃 ??留?tick ?ㅽ뻾 (5媛?

| ?쒖뒪??| 鍮꾧퀬 |
|---|---|
| `CcDecaySystem` (Effects) | `IJobEntity.Run()` ?쇰줈 `CcEffect` 踰꾪띁 ?꾩닔 媛먯뇿. 寃뚯씠?멸? ?놁뼱??留뚮즺媛 ??긽 吏꾪뻾?쒕떎. |
| `DotApplySystem` (Effects) | 遺??`DotApplyEventsSingleton`)??`TryGetSingleton` ?듭뀛?? ??媛먯뇿??臾댁“嫄? 濡쒓렇 ???좊Т濡?job 2醫?遺꾧린. |
| `ModifierStatsAggregateSystem` (Effects) | dirty-only 荑쇰━(`EnabledRefRW<ModifierStatsDirty>`)媛 ?ъ떎???꾪꽣 ??븷. |
| `StatModifierTickSystem` (Effects) | 臾닿쾶?댄듃 = 梨꾨꼸 遺?ъ? 臾닿??섍쾶 stat ?щ’????긽 留뚮즺?쒕떎. `StackModifierTickSystem`(3以?寃뚯씠??怨쇱쓽 鍮꾨?移?씠 ?ш린??諛쒖깮. |
| `MaxHealthScaleSystem` (Units) | 臾닿쾶?댄듃. pass1(lazy attach) ??以묎컙 Playback ??pass2(?ш퀎??. |

### A-6. 寃뚯씠???좏삎 遺꾪룷 (李멸퀬)

- **肄섑뀗痢?議댁옱 寃뚯씠??*(洹?湲곕뒫????곸씠 ?덉쓣 ?뚮쭔): `AttackState`, `ProjectileTag`, `DreamCocoon`, `LethalTimer`, `HitFlashTag`, `UltimateLeapState`, `EmitterInstance`, `SummonedBy`, `AllyBuffField`, `PatrolAnchor`, `DcTriggerSlot`, `HazardCastState`, `ShieldCastState`, `EnemyAiState`, `IncomingDamage`, `Health`, `PathFollowState`, `PickupSpawnState`
- **湲곕? ?쒖꽦 ?뚮옒洹?*(config ?깃???議댁옱 = 耳쒖쭚): `BurnoutGimmickConfig`, `OnsenGimmickConfig`, `RedBullGimmickConfig`(3 ?쒖뒪??, `ClockOutGimmickConfig`(2 ?쒖뒪??
- **?명봽???깃???*(留?釉뚮━吏 ?쇱씠?꾩궗?댄겢 而ㅽ뵆留?: `FlowFieldSingleton`(8 ?쒖뒪??, `HazardSingleton`, `ObstacleSingleton`, `DefenderFieldSingleton`
- **?대깽??梨꾨꼸 ?깃???*(遺??= ?뚯씠?꾨씪???뺤?): `EnemyCcEventsSingleton`, `StatModifierApplyEventsSingleton`, `StackModifierApplyEventsSingleton`, `DotApplyEventsSingleton`, `CcClearRequestsSingleton`, `BlinkRequestEventsSingleton`, `HazardSpawnRequestsSingleton`, `MeteorBarrageRequestsSingleton`

---

## B. 遺???곹깭 (WithNone / tag) 紐⑸줉

### B-1. `WithNone<>` 荑쇰━ (26 ?쒖뒪??쨌 48 ?ъ씠??

| ?쒖뒪??| 荑쇰━ | ?쒖쇅 ???| ?섎? |
|---|---|---|---|
| `AttackSystem` | ?寃??꾨낫 ?ㅻ깄??| `PendingDeployment` | 諛곗튂 ?湲??좊떅? 留욎? ?딅뒗???꾩쭅 ?먯뿉 ?놁쓬) |
| `AttackSystem` | ?寃??꾨낫 ?ㅻ깄??| `DeadTag` | ?쒖껜 議곗? 湲덉? |
| `AttackSystem` | ?寃??꾨낫 ?ㅻ깄??| `UltimateLeapState` | ?댄깉(??諛? 以?= 議곗? 遺덇?. `LeapFlight` ??**?섎룄?곸쑝濡??쒖쇅 ????*(?쇰컲 ?꾩빟? 鍮꾪뻾 以묒뿉??留욌뒗?? |
| `AttackSystem` | 怨듦꺽??硫붿씤 猷⑦봽 | `PendingDeployment` | 諛곗튂 ?湲??좊떅? 怨듦꺽 ???? `DeadTag`/`LeapFlight` ??荑쇰━?먯꽌 鍮쇱? ?딄퀬 猷⑦봽 ??`actionLocked` ?좎뼱濡?泥섎━ ??荑⑤떎??tick 怨?吏꾪뻾 以??ㅼ쐷 RESOLVE 瑜??대젮???섎?濡?|
| `BossPeriodicTriggerSystem` | ?щ’ 猷⑦봽 | `DeadTag` | ?쒖껜媛 ?ㅽ궗????踰????곕뒗 寃?李⑤떒(?쒖꽌 ???洹쒖튃?쇰줈 ?쒗쁽) |
| `EnemyAiStateSystem` | ?寃??꾨낫 | `PendingDeployment`, `DeadTag` | AttackSystem 怨??숈씪 ?꾨낫 ? ?좎? |
| `HealthThresholdSystem` | ?щ’ 猷⑦봽 | `DeadTag` | ?ㅻ쾭?щ줈 寃쎄퀎 ?ㅼ쨷 愿?????쒖껜媛 ??컻/?꾩빟?섎뒗 寃?李⑤떒 |
| `ProjectileEmitterSystem` | ?몄뒪?댁뒪 host 猷⑦봽 | `DeadTag` | 二쎌? host ????諛??????대? ?쒖옉??踰꾩뒪?몃뒗 ?꾩＜) |
| `ProjectileEmitterSystem` | ???ъ“以 ? | `DeadTag`, `PastGoalTag`, `UltimateLeapState` | 二쎌?쨌?좎텧?쑣룻뙋 諛??곸? 議곗? ?꾨낫 ?꾨떂(鍮?????ш꺽 諛⑹?) |
| `ProjectileHitSystem` | AOE ?쇳빐??? | `UltimateLeapState` | ?댄깉 以??곸? splash/TileAoe ?쇳빐?먮룄 bounce ?꾨낫???꾨떂 |
| `ProjectileMoveSystem` | ?ъ“以 ?꾨낫 ? | `DeadTag`, `PastGoalTag`, `UltimateLeapState` | 媛숈? 洹쒖빟. ?ъ궗泥??먯껜 猷⑦봽??臾댄븘??|
| `TauntAttackGrantSystem` | grant | `AttackState`, `TauntAttackGranted` | **遺??= ?먯껜 怨듦꺽?섎떒 ?놁쓬 = ?꾨컻 怨듦꺽 遺?????*. ?대? 遺?щ맖(`TauntAttackGranted`) ?щ???李⑤떒 |
| `TauntAttackGrantSystem` | strip | `Aggroed` | ?닿렇濡??댁젣??= 遺?щ텇 ?뚯닔 ???|
| `UltimateLeapSystem` | ?댄깉 猷⑦봽 | `DeadTag` | 諛⑹뼱??媛??怨꾩빟??怨듭쨷 ?щ쭩 ?놁쓬). 二쎌뿀?쇰㈃ 李⑹? ?놁씠 ?곹깭留?嫄룹뼱 "?좉릿 ?쒖껜" 諛⑹? |
| `AllyBuffFieldSystem` | 硫ㅻ쾭??猷⑦봽 | `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??щ쭩 ?좊떅? ?ν뙋 踰꾪봽 ????꾨떂 |
| `DefenderFieldSystem` | 諛⑹뼱?좊떅 ?ㅻ깄??| `PendingDeployment`, `DeadTag` | BFS ?뚯뒪?먯꽌 ?쒖쇅 = 蹂댁뒪媛 諛곗튂 ?湲??좊떅???щ깷?섏? ?딆쓬 |
| `DreamCocoonSystem` | ?꾩＜ 媛먯떆 | `DeadTag` | 二쎌쑝硫??먯젙 以묐떒 |
| `FatigueAccrualSystem` | pass1 lazy attach | `FatigueAccrual` | **遺??= ?꾩쭅 ??대㉧ 誘몃?李?* (idempotent attach 愿?⑷뎄) |
| `HazardCastSystem` | ?寃??꾨낫 | `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??쒖껜??罹먯뒪???쒖쟻 ?꾨떂 |
| `HazardCastSystem` | 罹먯뒪??猷⑦봽 | `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??쒖껜??罹먯뒪??????荑⑤떎??tick ???뺤?) |
| `HeatAccrualSystem` | pass1 lazy attach | `HeatAccrual` + `DeadTag`, `PendingDeployment` | 誘몃?李??좊떅?먮쭔 ??대㉧ 遺李?|
| `HeatAccrualSystem` | pass2 ?꾩궛 | `DeadTag`, `PendingDeployment` | ?쒖껜/諛곗튂 ?湲곗뿏 ?닿린 ?꾩쟻 ?놁쓬 |
| `ObstacleLifetimeSystem` | ?섎챸 tick | `BlockingHazardCellsBuffer` | **遺??= ?⑥씪 ? ?μ븷臾??꾪궎???*. ?ㅼ쨷 ?(?뚭눼 媛???댁???? ??踰덉㎏ 猷⑦봽媛 ?대떦 = 遺?щ줈 ?꾪궎??낆쓣 媛瑜몃떎 |
| `ObstacleLifetimeSystem` | ?ㅼ쨷 ? 猷⑦봽 | `DeadTag` | 二쎌? ?댁????? 李⑤떒?먯꽌 鍮좎쭚 |
| `PatrolFieldSystem` | ??? ?ㅻ깄??| `DeadTag`, `PastGoalTag` | ?좎텧 ?湲??곸? 已볦쓣 ?댁쑀 ?놁쓬 |
| `PatrolFieldSystem` | ?쒖같蹂?猷⑦봽 | `DeadTag` | 二쎌? ?쒖같蹂?dir 誘멸갚??|
| `PickupConsumeSystem` | defender ?뚮퉬 | `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??쒖껜???쎌뾽 紐?癒뱀쓬 |
| `PickupConsumeSystem` | enemy ?뚮퉬 | `PendingDeployment`, `DeadTag` | ?숈씪 |
| `ShieldCastSystem` | ?꾨낫 ?ㅻ깄??| `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??쒖껜???ㅻ뱶 ????꾨떂(?먯떊 ?ы븿 洹쒖튃怨?蹂꾧컻) |
| `ShieldCastSystem` | 罹먯뒪??猷⑦봽 | `PendingDeployment`, `DeadTag` | 諛곗튂 ?湲??쒖껜??罹먯뒪??????|
| `MovementSystem` | ?대룞 猷⑦봽 | `PastGoalTag` | **?좎텧 ?뺤젙 ?좊떅? ?대룞 ?숆껐**. `UnitLifecycleSystem` ??媛숈? ?꾨젅?꾩뿉 ?뚭눼?섎뒗 ?꾩젣 ???뚭눼 猷⑦봽媛 `AttackUnitTag` 瑜??붽뎄?댁꽌 ?쒖같蹂묒뿉 ?쒓렇媛 遺숈쑝硫??곴뎄 ?숆껐?쒕떎(洹몃옒??goal ?먯젙??patrol 寃뚯씠?멸? ?덈떎) |
| `DamageApplicationSystem` | ?쒕젅??猷⑦봽 | `DeadTag` | ?대? 二쎌? ?좊떅? ?ъ쟻??????|
| `DamageApplicationSystem` | ?쒕젅??猷⑦봽 | `PendingDeployment` | 諛곗튂 ?湲??좊떅? ?쇳빐 ?섏떊 ????|
| `HealthDeathSystem` | HP<=0 ?ㅼ틪 | `DeadTag` | 以묐났 ?쒓퉭 諛⑹? |
| `LethalTimerSystem` | ??대㉧ 猷⑦봽 | `DeadTag` | ?대쾲 ?꾨젅???쇳빐濡??대? 二쎌? ?좊떅 double-tag 諛⑹? (critic H5) |
| `MaxHealthScaleSystem` | pass1 lazy attach | `MaxHealthScaleState` | **遺??= baseMax ?꾩쭅 誘몄벙泥?* (諛곗쑉??1 ?먯꽌 踰쀬뼱??泥??꾨젅?꾩뿉留?遺李? |
| `PatrolLifecycleSystem` | ?쒖같蹂?猷⑦봽 | `DeadTag` | ?대? 二쎌? ?쒖같蹂??ы깭源?諛⑹? |
| `UnitLifecycleSystem` | general dead 猷⑦봽 | `DefenderTile` | ?꾩そ ?뷀렂???щ쭩 猷⑦봽???**double-destroy 諛⑹?** |
| `UnitLifecycleSystem` | general dead 猷⑦봽 | `BlockingHazard` | ?댁????대깽??enqueue 猷⑦봽???double-destroy 諛⑹? |

### B-2. `HasComponent` / `HasBuffer` 遺꾧린 ??load-bearing ??"遺??= ?곹깭"

| 吏??| ?좎뼱 | ?섎? |
|---|---|---|
| `DamageApplicationSystem:99` | `UltimateLeapState` **蹂댁쑀** ??`damageBuffer.Clear(); continue` | ?댄깉 以?臾댁쟻. **荑쇰━ `WithNone` ?쇰줈 鍮쇰㈃ ???쒕떎** ??洹몃윭硫?2珥덇컙 ?쇳빐媛 踰꾪띁???곷┰??李⑹? ?꾨젅?꾩뿉 ?듭㎏濡??곗쭊??臾댁쟻???꾨땲??吏????깂). 肄붾뱶 二쇱꽍??紐낆떆???섎룄??鍮?WithNone |
| `MovementSystem:72` | `PatrolStep` 蹂댁쑀 = **?쒖같 ?꾪궎????먮퀎** | 遺??= ?쇰컲 ?대룞泥? goal ?먯젙쨌flow step ?뚯뒪瑜?????媛덉븘?꾨떎 |
| `MovementSystem:68` | `EnemyAiState` 遺????`AiState.Marching` ?대갚 | FSM 誘몃낫???뷀렂???쒖같蹂???????긽 ?꾩쭊 痍④툒 |
| `MovementSystem:80` / `AttackSystem:254` | `LeapFlight` 蹂댁쑀 ??`locked` (CC ? 媛숈? ?좎뼱??OR) | ?먭린二쇰룄 ?대룞/怨듦꺽 START 留??뺤?, ?몃젰쨌荑⑤떎??tick쨌吏꾪뻾 以??ㅼ쐷 RESOLVE ???좎? |
| `MovementSystem:135` | `DefenderFieldSingleton` 遺????`hunting=false` ?꾩썝 goal 寃쎈줈 | ?꾨뱶媛 ?덉뼱??`dist[idx]==int.MaxValue` 硫??꾨떖 遺덇? ??留덉묶 ?대갚(諛⑹뼱?좊떅 ?꾨㈇ = ??? MaxValue) |
| `CcApplySystem:33` | `BossTag` 蹂댁쑀 + `IsBossImmune(kind)` ??CC 嫄곗젅 | **遺???쒖젏 1怨?* 李⑤떒. IsLocked ?먯젙 履쎌뿉 ?ｌ쑝硫?臾댁떆 吏?먯씠 6怨??댁긽 |
| `AggroStateSystem:118` | `BossTag` 蹂댁쑀 ??`Aggroed` 遺李?嫄곗젅 | 遺李?1怨?李⑤떒. 遺숈? ??臾댁떆???뚮퉬 吏??6怨녹씠??鍮꾩떥??|
| `AggroStateSystem:104` | `AggroCapacity` 遺????鍮?媛?붿뼵 = ?덊듃 ?대깽??臾댁떆 | 媛?붿뼵 ?먭꺽??而댄룷?뚰듃 蹂댁쑀濡??뺤쓽??|
| `AggroStateSystem:57` / `PatrolLifecycleSystem:47` | ?щ쭩 3以??먯젙: `Exists` && !`DeadTag` && `Health.value>0` | ECB ?뚭눼遺?+ death ?꾨젅???쒓렇 + HP ?뚯쭊 ????李쎌쓣 紐⑤몢 ??뒗?? `Entity` 媛 version ???ы븿???ы솢??id 諛⑹뼱 |
| `AttackSystem` / `MovementSystem` / `DamageApplicationSystem` | `ModifierStats` 遺????諛곗쑉 `1f`, regen `0f` | 紐⑤뵒?뚯씠??誘몃낫?좉? 湲곕낯媛믨낵 媛숈? ?섎? (遺???덉쟾 湲곕낯媛? |
| `HealthThresholdSystem:67` / `ProjectileHitSystem:61` / `AttackSystem:140` | `ThreatEntry` **踰꾪띁 蹂댁쑀** = 蹂댁뒪 踰좎씠??| ?꾪삊 洹?띿씠 蹂댁뒪?먮쭔 ?곷┰?섎룄濡??섎뒗 ?좎씪???꾪꽣 |
| `ProjectileHitSystem` | `DefenderUnitTag`(owner) 蹂댁쑀 | ?꾪삊 洹?띿? defender 諛?李⑺깂留????ㅽ궗 ?ъ궗泥?owner=Null)??臾댁쁺??|
| `PickupConsumeSystem:90` | `LastRun` 蹂댁쑀 ???뚮퉬 嫄곗젅(**?뚮퉬 ??*) | ?ъ냼鍮꾨줈 ??대㉧ 由ъ뀑??crash 臾댄븳 ?뚰뵾?섎뜕 臾몄젣 李⑤떒. ?쎌뾽? 蹂대뱶???붿〈 |
| `DefenderFieldSystem:40` | `bossQuery.IsEmpty` ???щ퉴??skip | ?꾨뱶 ?뚮퉬?먭? 蹂댁뒪肉?|
| `LastRunSystem:41` | `Health` && `IncomingDamage` 踰꾪띁 ????蹂댁쑀?댁빞 crash ?곸슜 | 遺????議곗슜??而댄룷?뚰듃留??쒓굅 |
| `HazardCastSystem:126` | 罹먯뒪?곌? `DcTriggerSlot` 踰꾪띁 蹂댁쑀?댁빞 `CastEvent` enqueue | ?앹궛??寃뚯씠?????놁쑝硫?4珥덈쭏???대깽?몃쭔 ?곸옱 |
| `ShieldCastSystem:110` | 湲곗〈 ?щ’???대? amount ?댁긽?대㈃ append/VFX skip | Merge(max) no-op ?덉륫 = ?쏅텋苑?諛⑹? |
| `UnitLifecycleSystem` / `DamageApplicationSystem` | ?깃???荑쇰━ `CalculateEntityCount()==1` / `TryGetSingletonRW` | **fail-open**: 梨꾨꼸 ?놁쑝硫??대깽?몃쭔 鍮좎?怨??뚭눼/?쇳빐 濡쒖쭅? 怨꾩냽 |
| `DreamCocoonSystem:49` | `CcEffect` ??`Sleep` 遺??&& `remaining>0` ???뚰깂 | 遺?ш? "?쇨꺽?쇰줈 源⑥뼱?????좏샇. `remaining>0` 媛?쒓? ?뚰깂/?꾩＜???ㅼ젣 disambiguator |
| `EnemyAiStateSystem` / `AttackSystem` | `EnemyTargetFilter` 遺????`classMask=-1`(?꾩껜 ?덉슜) | 遺??= 臾댁젣???꾪꽣 |

---

## C. ?곌린 吏??
?뺤떇: `?곌린 ??곷뱾` = 而댄룷?뚰듃/踰꾪띁 吏곸젒 ?곌린(RefRW, RW lookup, DynamicBuffer 蹂??. `???? = NativeQueue enqueue(留λ씫 媛?梨꾨꼸). ECB ?댁? ?꾨? `Allocator.Temp` 濡쒖뺄 + 媛숈? OnUpdate ??`Playback(state.EntityManager)` + `Dispose()`.

### C-1. Combat

| ?쒖뒪??| ?곌린 ??곷뱾 | ECB |
|---|---|---|
| `AttackSystem` | `AttackState`(RefRW: cooldown/hitDelay) 쨌 `FrontmostAttackLock`(RW lookup) 쨌 `FocusTarget`(RW lookup) 쨌 `BombLauncherState`(RW lookup, rng ?꾩쭊) 쨌 `DcTriggerSlot`(RW buffer: counter/elapsed ?섏벐湲? 쨌 `PatternSlot`(RW buffer: fireCountBase) 쨌 `EmitterInstance`(RW buffer: Add) ??`UnitAttackVisualEvents` 쨌 `EnemyCcEvents` 쨌 `AggroHitEvents` 쨌 `AttackOutputLogEvents` 쨌 `ThreatHitEvents`(ThreatTable.TryCredit) 쨌 `KnockupVisualEvents` 쨌 `StatModifierApplyEvents` 쨌 `StackModifierApplyEvents` 쨌 `DcTriggerFiredEvents` | **O** ??Temp 1媛? `AddComponent<ProjectileSpawnRequest>`(attacker in-place + 罹먮━?? 쨌 `AddBuffer<ProjectileSpawnOutputElement>` 쨌 `RemoveComponent<NextAttackDoubleFire>` 쨌 `AppendToBuffer<IncomingDamage>`/`<IncomingHeal>` 쨌 `CreateEntity` 罹먮━??`ProjectileRequestCarrier`, `PatrolRequestCarrier`) |
| `BossPeriodicTriggerSystem` | `DcTriggerSlot`(RW buffer: elapsed) 쨌 `PatternSlot`(RW buffer: fireCountBase) 쨌 `EmitterInstance`(RW buffer: Add) ??`StatModifierApplyEvents` 쨌 `ProjectileHitEvents` | **X** ??援ъ“ 蹂寃??놁쓬(踰꾪띁 ?댁슜 蹂?대쭔) |
| `EnemyAiStateSystem` | `EnemyAiState`(RefRW) ???좎씪??writer | **X** |
| `HealthThresholdSystem` | `ThreatEntry`(RW buffer, `ThreatTable.Accumulate`) 쨌 `DcTriggerSlot`(RW buffer: nextBoundaryIndex) ??`StatModifierApplyEvents` 쨌 `BlinkRequestEvents` 쨌 `BossLeapVisualEvents` 쨌 `UltimateLeapVisualEvents` | **O** ??`AddComponent<UltimateLeapState>` 쨌 `AddComponent<LeapFlight>` 쨌 `CreateEntity` SelfTileAoe 罹먮━??|
| `ProjectileEmitterSystem` | `EmitterInstance`(RW buffer: runtime ?섏벐湲?/ ?꾩＜ ??`RemoveAtSwapBack`) | **O** ??`CreateEntity` + `AddComponent<ProjectileSpawnRequest>` + `ProjectileRequestCarrier` (諛??섎쭔?? |
| `ProjectileHitSystem` | `IncomingDamage`(RW buffer lookup ?뺣낫) 쨌 `IncomingHeal`(RW) 쨌 `AttackOutputElement`(RW buffer: bounce 媛먯뇿 in-place) ??`ProjectileHitEvents` 쨌 `EnemyCcEvents` 쨌 `ThreatHitEvents` 쨌 `StatModifierApplyEvents` 쨌 `StackModifierApplyEvents` | **O** ??`AppendToBuffer<IncomingDamage>`/`<IncomingHeal>`/`<PathHitRecord>` 쨌 `SetComponent`/`AddComponent<HitFlashTag>` 쨌 `SetComponent<ProjectileState>`(bounce next) 쨌 `RemoveComponent<AttackOutputElement>` 쨌 `DestroyEntity`(?ъ궗泥? |
| `ProjectileMoveSystem` | `LocalTransform`(RefRW: ?ъ궗泥??꾩튂) 쨌 `ProjectileState`(RefRW: elapsed/target/impactReached) | **O** ??`DestroyEntity`(?寃??뚮㈇쨌?섎챸 醫낅즺) |
| `TauntAttackGrantSystem` | (吏곸젒 ?곌린 ?놁쓬 ???꾨? ECB) | **O** ??grant: `AddComponent<AttackState>` + `AddBuffer<AttackOutputElement>` + `AddComponent<TauntAttackGranted>` / strip: 3媛?`RemoveComponent` |
| `UltimateLeapSystem` | `UltimateLeapState`(RefRW: remaining) ??`BlinkRequestEvents` 쨌 `UltimateLeapVisualEvents` | **O** ??`CreateEntity` ?щ옩 罹먮━??쨌 `RemoveComponent<UltimateLeapState>` 쨌 `RemoveComponent<LeapFlight>` |

### C-2. Effects

| ?쒖뒪??| ?곌린 ??곷뱾 | ECB |
|---|---|---|
| `AggroStateSystem` | `AggroCapacity`(RefRW: held full recompute) ??`Aggroed`/`AggroCapacity` ?⑤룆 writer | **O** ??`AddComponent<Aggroed>` 쨌 `AddBuffer<AggroChaseCell>` 쨌 `RemoveComponent<Aggroed>` 쨌 `RemoveComponent<AggroChaseCell>` |
| `AllyBuffFieldSystem` | (而댄룷?뚰듃 ?곌린 0) ??`StatModifierApplyEvents` (留??꾨젅???щ컻?? | **X** |
| `CcApplySystem` | `CcEffect`(EntityManager.GetBuffer ??`CcEffectMerge.Apply`) | **X** ??non-Burst OnUpdate |
| `CcClearSystem` | `CcEffect`(GetBuffer ??`RemoveAtSwapBack`) | **X** |
| `CcDecaySystem` | `CcEffect`(IJobEntity `ref DynamicBuffer`: remainingTime 媛먯궛 + 留뚮즺 ?쒓굅) | **X** |
| `DefenderFieldSystem` | `DefenderFieldSingleton.flow`/`.dist`(?깃????대? NativeArray in-place ?щ퉴?? | **X** |
| `DotApplySystem` | `DotEffect`(遺??merge + tick/媛먯뇿 ?섏벐湲? 쨌 `IncomingDamage`(job ??`ref DynamicBuffer.Add`) ??`HazardRuntimeEvents` | **X** |
| `DreamCocoonSystem` | `DreamCocoon`(RefRW: remaining) ??`StatModifierApplyEvents` | **O** ??`RemoveComponent<DreamCocoon>`(?뚰깂/?꾩＜) |
| `EffectTickSystem` | `TornadoField` 쨌 `AllyBuffField` 쨌 `PortalLink` (媛?RefRW: remaining) | **O** ??留뚮즺 ??`DestroyEntity`(罹먮━???뷀떚???듭㎏) |
| `FatigueAccrualSystem` | `FatigueAccrual`(RefRW: elapsed) ??`StackModifierApplyEvents` | **O** ??pass1 `AddComponent<FatigueAccrual>` + **以묎컙 Playback** ??pass2 |
| `HazardCastSystem` | `HazardCastState`(RefRW: cooldownRemaining) ??`HazardSpawnRequests` 쨌 `UnitAttackVisualEvents` 쨌 `CastEvents` | **X** |
| `HazardLifetimeSystem` | `HazardSingleton.cellToEffects`(Clear + ?ъ쟻?? 쨌 `Hazard`(RefRW: remainingLife) | **O** ??留뚮즺 `DestroyEntity` |
| `HeatAccrualSystem` | `HeatAccrual`(RefRW: elapsed/stacks) 쨌 `IncomingHeal`(RW buffer lookup `.Add`) 쨌 `IncomingDamage`(RW buffer lookup `.Add`) | **O** ??pass1 `AddComponent<HeatAccrual>` + `AddBuffer<IncomingHeal>` + **以묎컙 Playback**, ?댄썑 ??lookup ??Update`(援ъ“ 蹂寃쎌쑝濡?臾댄슚?? |
| `LastRunSystem` | `LastRun`(RefRW: remaining) 쨌 `IncomingDamage`(`SystemAPI.GetBuffer(...).Add`, crash ?쇳빐) | **O** ??`RemoveComponent<LastRun>` |
| `ModifierApplySystem` | `StatModifierSlot`(GetBuffer 蹂묓빀/異붽?) 쨌 `StackModifierSlot`(?숈씪) 쨌 `ModifierStatsDirty`(**EntityManager 利됱떆** `AddComponent` + `SetComponentEnabled`) | **O(?쇱슜)** ??ECB ??`AddBuffer` 留? 踰꾪띁 ?좎꽕쨌MarkDirty ??**?섎룄?곸쑝濡?EntityManager 利됱떆** ??媛숈? ?쒕젅??猷⑦봽?먯꽌 媛숈? ?源껋뿉 ??踰덉㎏ ?대깽?멸? ?ㅻ㈃ ECB ??AddBuffer 瑜???踰?湲곕줉??泥??щ’????씤??|
| `ModifierStatsAggregateSystem` | `ModifierStats`(RefRW ??**?좎씪??writer**) 쨌 `ModifierStatsDirty`(EnabledRefRW ??false) | **X** |
| `ObstacleLifetimeSystem` | `ObstacleSingleton.blockedCells`(Clear + ?ъ쟻?? 쨌 `Obstacle`(RefRW: remainingLife) | **O** ??留뚮즺 `DestroyEntity` |
| `PatrolFieldSystem` | `PatrolStep`(RefRW: dir ???좎씪??writer) | **X** |
| `PickupConsumeSystem` | (而댄룷?뚰듃 吏곸젒 ?곌린 0) ??`StatModifierApplyEvents` | **O** ??`DestroyEntity`(?쎌뾽) 쨌 `AddComponent<LastRun>`. `EntityManager.HasComponent<LastRun>` 濡??뚮퉬 ??利됱떆 ?먯젙 |
| `PickupSpawnSystem` | `Pickup`(RefRW: remainingLife) 쨌 `PickupSpawnState`(RW ?깃??? elapsed, **rng ?곹깭 ?섏벐湲???寃곗젙濡?*) | **O** ??留뚮즺 `DestroyEntity` 쨌 `CreateEntity` + `AddComponent<Pickup>` |
| `ResignationDropSystem` | (吏곸젒 ?곌린 0) | **O** ??`CreateEntity` + `AddComponent<Resignation>`(?щ쭩 defender ??쇰쭏?? |
| `ResignationThresholdSystem` | (吏곸젒 ?곌린 0) ??`MeteorBarrageRequests` | **O** ??`DestroyEntity`(?꾧퀎 諛곗닔留뚰겮 ?ъ쭅???뚮え) |
| `ShieldCastSystem` | `ShieldCastState`(RefRW: cooldownRemaining) 쨌 `IncomingShield`(RW buffer lookup `.Add`) ??`ShieldGrantedEvents` | **X** |
| `StackModifierTickSystem` | `StackModifierSlot`(GetBuffer: remaining 媛먯궛 쨌 stackCount consume 쨌 lastTriggeredStack 쨌 留뚮즺 ?쒓굅) ??`EnemyCcEvents` 쨌 `DotApplyEvents` 쨌 `StatModifierApplyEvents` | **X** ??non-Burst(BattleBridge 愿由?Dictionary SO 議고쉶) |
| `StatModifierTickSystem` | `StatModifierSlot`(GetBuffer: remaining 媛먯궛 + 留뚮즺 ?쒓굅) 쨌 `ModifierStatsDirty`(`SystemAPI.SetComponentEnabled` 利됱떆) | **X** |
| `ZoneApplySystem` | **而댄룷?뚰듃 ?곌린 0** ??`StatModifierApplyEvents` 쨌 `DotApplyEvents` 쨌 `EnemyCcEvents` 쨌 `HazardRuntimeEvents` | **X** ???쒖닔 ?앹궛??|

### C-3. Movement

| ?쒖뒪??| ?곌린 ??곷뱾 | ECB |
|---|---|---|
| `BlinkApplySystem` | `LocalTransform`(RW lookup, x/z 留???y ??mover ?먭린 媛??좎?) | **X** |
| `MovementSystem` | `LocalTransform`(RefRW: ?ы깉 ?붾젅?ы듃 쨌 chase step 쨌 flow step 쨌 pull 쨌 recenter) | **O** ??`AddComponent<PastGoalTag>`(goal ? ?꾨떖) |

### C-4. Units

| ?쒖뒪??| ?곌린 ??곷뱾 | ECB |
|---|---|---|
| `DamageApplicationSystem` | `Health`(RefRW ??Units ?뚯쑀) 쨌 `IncomingDamage`(Clear) 쨌 `IncomingHeal`(RW lookup, Clear) 쨌 `ShieldSlot`(RW lookup: Merge/Absorb) 쨌 `IncomingShield`(RW lookup, Clear) 쨌 `DamagedCounter`(RW lookup: counter tick) ??`HealAppliedEvents` 쨌 `DamageNumberEvents` 쨌 `EnemyKilledEvents` 쨌 `CcClearRequests` 쨌 `StatModifierApplyEvents` 쨌 `ShieldBreakEvents` | **O** ??`AddComponent<DeadTag>` 쨌 `AddComponent<NextAttackDoubleFire>` |
| `HealthDeathSystem` | (吏곸젒 ?곌린 0 ??Health ??RefRO) | **O** ??`AddComponent<DeadTag>` |
| `HitFlashSystem` | `LocalTransform`(RefRW: Scale 留? 쨌 `HitFlashTag`(RefRW: remaining) | **O** ??`RemoveComponent<HitFlashTag>` |
| `LethalTimerSystem` | `LethalTimer`(RefRW: remaining) | **O** ??`AddComponent<DeadTag>` + `RemoveComponent<LethalTimer>` |
| `MaxHealthScaleSystem` | `Health`(RefRW: value+max, `Health.ScaleMax` ?쒖닔?⑥닔) 쨌 `MaxHealthScaleState`(RefRW: appliedMul) | **O** ??pass1 `AddComponent<MaxHealthScaleState>` + **以묎컙 Playback** ??pass2 |
| `PatrolLifecycleSystem` | (吏곸젒 ?곌린 0) | **O** ??`AddComponent<DeadTag>`(?뚰솚???щ쭩 ???쒖같蹂? |
| `UnitLifecycleSystem` | (吏곸젒 ?곌린 0) ??`GoalReachedEvents` 쨌 `DefenderDeathEvents` 쨌 `HazardDestroyedEvents` | **O** ??4媛?猷⑦봽 ?꾨? `DestroyEntity`. enqueue 瑜??뚭눼 **??*???먯뼱 釉뚮━吏媛 tile/cell ??蹂닿린 ???뚮㈇?섏? ?딄쾶 ??|

### C-5. ECB ?⑦꽩 愿李?
- 28媛??꾨? `new EntityCommandBuffer(Allocator.Temp)` ??媛숈? `OnUpdate` ??`Playback(state.EntityManager)` ??`Dispose()`. **怨듭쑀 ECB / SystemGroup EntityCommandBufferSystem ?ъ슜 0**.
- **OnUpdate ??以묎컙 Playback 3嫄?* (lazy-attach 2-pass 愿?⑷뎄): `MaxHealthScaleSystem`, `FatigueAccrualSystem`, `HeatAccrualSystem`. `HeatAccrualSystem` ? 以묎컙 Playback ??BufferLookup ??臾댄슚?뷀븯誘濡?`_healLookup`/`_damageLookup` ??**??踰?* `Update` ?쒕떎(媛숈? ?꾨젅???⑥씠釉??ㅽ룿 + 湲곗〈 ?좊떅 ?곕?吏 append 寃쏀빀 諛⑹뼱).
- **ECB ? EntityManager 利됱떆 ?곌린 ?쇱슜 1嫄?*: `ModifierApplySystem` ??媛숈? ?쒕젅??猷⑦봽???숈씪 ?源?2???대깽?몄뿉??ECB `AddBuffer` 以묐났 湲곕줉??泥??щ’????뒗 臾몄젣瑜??쇳븯??踰꾪띁 ?좎꽕쨌MarkDirty 瑜?利됱떆 ?섑뻾.
- ECB 誘몄궗??16媛쒕뒗 (a) ?쒖닔 ?앹궛??`ZoneApplySystem`, `AllyBuffFieldSystem`), (b) 踰꾪띁 ?댁슜留?蹂??`Cc*`/`Dot*`/`*ModifierTick*`), (c) ?깃????대? 諛곗뿴 ?щ퉴??`DefenderFieldSystem`), (d) 而댄룷?뚰듃 in-place 留?`EnemyAiStateSystem`, `PatrolFieldSystem`, `HazardCastSystem`, `ShieldCastSystem`, `BlinkApplySystem`, `BossPeriodicTriggerSystem`, `ModifierStatsAggregateSystem`).

### C-6. ?⑤룆 writer ?좎뼵??肄붾뱶/二쇱꽍??紐낆떆??而댄룷?뚰듃

| 而댄룷?뚰듃 | ?좎씪 writer |
|---|---|
| `ModifierStats` | `ModifierStatsAggregateSystem` |
| `EnemyAiState` | `EnemyAiStateSystem` |
| `PatrolStep` | `PatrolFieldSystem` |
| `Aggroed` 쨌 `AggroCapacity` | `AggroStateSystem` (Movement/Attack ? RO) |
| `Health` | Units 留λ씫 (`DamageApplicationSystem`, `MaxHealthScaleSystem`) |
| `LocalTransform`(?좊떅 ?꾩튂) | Movement 留λ씫 (`MovementSystem`, `BlinkApplySystem`) ????`HitFlashSystem` ??**Scale 留?*, `ProjectileMoveSystem` ??**?ъ궗泥??꾩튂**瑜??대떎 |
| `ShieldSlot` | `DamageApplicationSystem` (`ShieldCastSystem` ? `IncomingShield` append 留? |
| `DamagedCounter` | `DamageApplicationSystem` (Combat ? charge 留?read) |

