# 장부 — 퇴역 테스트 짝 지도 (unit 9 구현 2 · retire-set.md 묶음 5·6·7)

> `retire-set.md` 의 테스트 묶음(5·6·7)마다 「같은 규칙을 증언하는 코어 테스트」를 적은 짝 지도다(spec 9 구현 2). 목록이 길어 퇴역 목록과 파일을 나눴다.
> **「규칙 누락 의심」 행은 옛 테스트를 지우기 전에 코어 테스트로 옮긴다**(구현 2 「짝이 없으면 코어 테스트로 먼저 옮긴다」) — 옮긴 뒤 그 행의 분류를 `짝` 으로 바꾸고 코어 짝을 적는다.
> 조사 2026-09-25 · 워크트리 `wassup-core`(`rebuild/battle-core`) · 읽기 전용 대조(테스트 이름 + 필요한 곳은 본문 `파일:줄`).

## 읽는 법

- **코어 짝**은 `파일::테스트명`. 경로 접두 생략 = `Tests/EditModeCore/`. `Assets:` = `Tests/EditModeAssets/`(남는 것), `PlayCore:` = `Tests/PlayModeCore/`.
  **`(잔류)`** = `Tests/EditMode/` 안에서 **퇴역 목록에 없어 남는** 파일(스킬 도메인·UnitAi·아웃게임 등 137개 — unit 9 가 asmdef 에서 Entities 만 뗀다).
- **분류 넷.** `짝` / `짝 없음 — 옛 기계 전용` / `짝 없음 — A/B 비교` / `짝 없음 — 규칙 누락 의심`.
- `짝` 인데 **⚠ 부분 공백:** 이 붙은 줄은 파일의 주장 대부분은 코어가 증언하지만 일부 하위 규칙의 코어 테스트를 못 찾은 것이다(맨 아래 「부분 공백」 목록에 모았다).
- 「옛 기계 전용」에는 **제거 확정 기능**(README 계약 9 · rules.md `제거` 행 · 재배치 은퇴 README 고지 ⑴ · 튜토리얼 결정 ④)도 넣었다 — 새 코어에 대응 규칙이 없기 때문이다. 근거를 괄호에 적었다.

## 이식 결과 (2026-09-25 · `7482f7ba6`)

「규칙 누락 의심」 34 중 **33 을 코어 테스트로 옮겼다**(31 `TilemapMapViewTests` 는 대상 `TilemapMapView` 가 퇴역이라 제외). 새 파일: EditModeCore `Retired{CombatRule,TargetLock,EffectRule,DetectionMove,WaveRule,WaveForecast}PortTests` · EditModeAssets `Retired{WaveAuthoring,DeckFilter}PortTests` · EditMode `BoardSpaceAuthorityTests`·`CoreProjectileVariationTests` · PlayModeCore `Retired{Beam,SpriteBackend}PortTest`. 각 테스트 위 주석이 옛 `파일::테스트` 를 가리킨다.
**옛 규칙과 코어가 다른 4건**은 코어를 고치지 않고 `[Ignore("unit 9 — 옛 규칙과 다름: …")]` 로 남겼다 — 방향탄 관통 소진 뒤 호밍 튕김 · 감지 후보의 직업 필터 · 같은 입구 다른 종의 예고 병합 · 예보 경로 해석(사용자 결정 대기). **9c 실현**: 앞 둘은 옛 규칙으로 복원하고 `[Ignore]` 해제(`27297a0cb`·`125d002b0`) — 뒤 둘(표현)은 **사용자 결정 ⑧** 로 닫았다: 예보 경로 = 옛 방식 복원(9c 행 7 `e26b8c1ad` · `[Ignore]` 해제) · 예고선 병합 = 새 방식 유지(9c 행 8 `8e4b19c82` · 테스트를 새 문장으로 뒤집음). `[Ignore]` 0. 「부분 공백」(아래)은 README 후속 후보로 넘긴다.

## 총계

| 분류 | 5 (EditMode 160) | 6 (Assets 6) | 7 (PlayMode 84) | 합 |
|---|---:|---:|---:|---:|
| 짝 | 122 | 2 | 72 | **196** |
| 짝 없음 — 옛 기계 전용 | 10 | 0 | 8 | **18** |
| 짝 없음 — A/B 비교 | 1 | 1 | 0 | **2** |
| 짝 없음 — 규칙 누락 의심 | 27 | 3 | 4 | **34** |
| 계 | 160 | 6 | 84 | **250** |

(`짝` 196 중 **⚠ 부분 공백** 붙은 것 51 — 5: 30 · 6: 2 · 7: 19. 표 행 수는 `retire-set.md` 묶음 5·6·7 파일 목록과 1:1 대조 완료.)

---

## 5. `Tests/EditMode/*`

| 옛 테스트 파일 | 규칙 | 분류 | 코어 짝 |
|---|---|---|---|
| AgentCollisionTests | 원형 몸 충돌 · 벽 면 앞 정지 · 벽 따라 미끄럼 · 빠른 스텝도 벽을 못 건너뜀 · 1칸 복도 통과 | 짝 | AgentCollisionTests::벽에_정면으로_가면_면_앞에_선다 · 한_축이_막히면_다른_축으로_미끄러진다 · 스윕이_벽_한_칸을_건너뛰지_못하게_한다 · NavGridAndTrimTests::장애물은_정적_벽과_합쳐진다 · MovementRulesTests::군집_통과_검산_1칸_복도_20기_100초 |
| AggroAoeWidthTests | 어그로에 물린 광역 적도 광역 폭은 그대로(단일로 접지 않음) · 넓은 가디언 몸에 닿는 적을 때린다 | 짝 없음 — 규칙 누락 의심 | 몸 원 부분만 — AttackReachTests::도달은_사거리에_양쪽_몸을_더한_원이다. 「물린 광역 적이 이웃도 때린다」 단언 없음(CombatPhase 어그로 sticky `:607` 뒤 Resolve 부가 타격 — 테스트 0) |
| AggroChaseFreezeTests | 어그로된 적은 **쏠 수 있는 칸에서** 멈춘다(영구 동결 금지) · 접근 보정은 지배축 | 짝 | MovePureMathTests::추격판은_사격_칸을_소스로_굽는다 · 접근_보정은_지배축_cardinal_이다 · UnitAiRulesTests::어그로는_사격_대상을_덮는다 |
| AggroChaseMathTests | 유효 사거리 해석 · 추격판 수선 고착·코너 우회·고립 섬 | 짝 | MovePureMathTests::추격판은_사격_칸을_소스로_굽는다 · 소스가_하나도_안_서면_도달_불가로_채운다 · 공격_수단이_없으면_거부한다 · 더_가까운_이웃_쪽으로_내려간다 |
| AggroPolicyTests | 어그로 획득 수용량 · 해제 · 가디언 선정 = 안 물린 적 우선·몸 반영 | 짝 | CombatPureMathTests::가디언은_아직_안_물린_적부터_때린다 · 상한이_차면_겹친_팩을_정리한다 · 가디언_선정은_자기_몸을_센다 · DetectionRulesTests::어그로는_수용량을_넘지_않고_도발은_그것을_우회한다 |
| AggroStateSystemTests | 히트 구동 어그로 · 수용량 · 선점 · 가디언 사망 해제 · 도발 수용량 우회·만료·갈아타기 | 짝 | CombatRulesTests::가디언은_때린_적을_끌어온다 · 유닛을_노리지_않는_적은_가디언에게_끌려가지_않는다 · DetectionRulesTests::어그로는_수용량을_넘지_않고_도발은_그것을_우회한다 · 도발_시한이_지나면_풀린다 · 가디언이_사라지면_붙들린_적마다_풀림_사건이_한_번씩_난다. ⚠ 부분 공백: 히트 선점(먼저 문 가디언 유지, `AiMovePhase.cs:199`) · 도발 재부여는 긴 쪽 · 비행 적 추격판 공중층 · 도달 불가 거절 |
| AoeTargetCapTests | 광역 상한 = 가까운 순, 동률 앞 인덱스 | 짝 | CombatPureMathTests::광역_상한은_가까운_순이고_동률은_앞_인덱스다 |
| AttackCommitTests | 공격 1회 타겟 커밋 — 선딜 중 더 가까운 적이 와도 **겨눈 대상을 때린다** · 커밋 대상 소멸/이탈은 빗나감 | 짝 없음 — 규칙 누락 의심 | strict lapse 만 — CombatRulesTests::선딜_중_대상이_사라지면_빗나간다(`:583`). 「A 를 겨누고 B 를 때리지 않는다」(본 파일의 결함) · 골 통과는 이탈 사유 아님 · 사거리 이탈 lapse 단언 없음 |
| AttackReachTests | 도달 = 사거리 + 양쪽 몸 · 대각 인접 = 사거리 1 · 칸 체비셰프는 이동 계층 전용 · 타일 크기 환산 | 짝 | AttackReachTests::도달은_사거리에_양쪽_몸을_더한_원이다 · 대상_몸을_빼면_조용히_좁아진다 · 대각_인접도_사거리_1_이다 · 칸_체비셰프는_격자_계층_전용이다 · 타일_크기가_달라도_같은_칸수로_판정한다. ⚠ 부분 공백: 파생 몸 = 가로/2(행 배제) · 거점 몸 = footprint 절반 |
| AttackShapeGateTests | 부채꼴·띠 게이트 절대값(모서리·꼭짓점 뒤·몸 걸침) · Omni 는 원과 비트 동일 · 주 대상 방향 회전 | 짝 없음 — 규칙 누락 의심 | 일부만 — AttackReachTests::도형이_Omni_면_원_항만_본다 · CombatRulesTests::부가_타격은_주_대상_방향_도형_안에서만_고른다(부채꼴 1케이스) · AttackShapeBakeTests(잔류, bake 만). **띠(Band) 게이트 코어 테스트 0**, 부채꼴 경계·꼭짓점 뒤 거리 0 — 지금까지는 A/B `AttackReachParityTests` 가 대신 지켰다 |
| AttackShapeSelectionTests | 획득은 원 · 도형은 부가 타격에만 · 주 대상 방향을 따라 돈다 | 짝 | CombatRulesTests::부가_타격은_주_대상_방향_도형_안에서만_고른다 · 공격_성사_사건은_판정한_도형_축_사거리를_값으로_싣는다. ⚠ 부분 공백: 가디언(어그로) 경로도 같은 도형 규칙 |
| AttackSystemMaskTests | 진영 비트마스크 — 방어유닛은 길막을 안 친다 · 적은 길막을 친다 · 통행층 대상 선별 | 짝 | CombatRulesTests::직업_필터는_존재가_게이트다_마스크_0_은_아무도_못_때린다 · 비행_적은_지상을_걷는_아군을_멈춰_서서_때린다 · FactionRelationTests::BlockingHazard_IsNotAUnitSide(잔류) · Assets:LiveDefinitionSmokeTests::적의_공격은_통행_층을_거르지_않는다 |
| AttackSystemStateGateTests | 적은 **Engaging·Standoff 에서만** 발사 · Marching/Chasing 은 쿨만 돈다 | 짝 없음 — 규칙 누락 의심 | 전이표만 — UnitAiRulesTests::어그로가_없으면_사격_대상_유무로_갈린다 · EnemyAiStateTransitionTests(잔류). 발사 게이트(`CombatPhase.cs:462-466`) 자체의 판 단언 없음 |
| AttackSystemUnifiedLoopTests | 공격 루프 통합 — 탄은 요청으로 · 근접 광역 · 넉백 방향 없으면 없음 · 탄 넉백은 착탄 시 · 배율 · 자기/배치 중/사망 제외 · 폭탄맨 최근접 칸·대기 · 바늘 5타 | 짝 | ProjectileBehaviorTests::요청은_한_틱_뒤에_탄이_된다 · CombatRulesTests::방향을_모르는_대상은_안_밀린다 · 배치중_사망_궁극기이탈은_표적이_아니다 · 폭탄맨은_적이_없으면_쿨을_만료로_대기시킨다 · 폭탄맨은_근접_피해를_내지_않는다 · CombatPureMathTests::폭탄맨의_사각_자는_반경_0_이면_고르지_않는다. ⚠ 부분 공백: **바늘 캐리어(PokeNeedle 5타 · C5 피해 flat)** 코어 테스트 0 · 탄 넉백은 착탄 시점 · CastEvent 부분은 캐스터 제거(계약 9) |
| AuraPulseTests | 오라 펄스 대상 — 체비셰프 경계·같은 칸 포함·링(최소 반경) | 짝 | StatAuraSkillTests(잔류) · AreaCircleMembershipTests::SelfArea_WidensWithCasterBody_NotWithACellConstant(잔류) — 체비셰프 자는 제약 13 으로 원 자로 바뀌었고 링(도넛)은 마메모 설계에서 폐기 |
| BallisticArcTests | 포물선 양 끝·정점 · 비행 시간 = 수평거리/속도, 최소값 바닥 | 짝 | ProjectileMathTests::포물선은_양_끝에서_융기가_0_이다 · 비행_시간은_최소값_바닥을_갖는다 |
| BarrelExplosionTests | 부서진 설치물이 터진다 · 폭발 저작 없으면 안 터짐 · 배럴은 시간으로 안 죽는다(자기 피해 채널) | 짝 | BlockingHazardTests::폭발_저작이_있는_길막은_부서지는_틱에_그_칸에서_적만_때리는_즉발_광역을_낸다 · 폭발_저작이_없는_길막은_부서져도_안_터진다 · 문은_부서짐_하나다_노후화가_없으면_시간으로_안_사라진다 · 체력_나누기_초당_감소가_아무도_안_때렸을_때의_수명이다 |
| BattleBridgeDraftMapTests | `BattleBridge` 맵 프리빌드 수명(준비·재빌드·무재빌드) | 짝 없음 — 옛 기계 전용 | 브리지 수명. 연결성 하드 실패는 Assets:StagePoolBuildabilityTests::AllPoolStages_ScanAssembleAndConnect · MapRuntimeTests::슬롯이_없으면_시끄럽게_실패한다 가 잇는다 |
| BattleScaledRateManagerTests | 전투 시간 배율 — 0 이면 멈춤·절반이면 절반·정지 해제 복귀 | 짝 | PlayCore:CoreTickRateTests::BattleScaleThreeTenths_IssuesAboutEighteenTicksPerSixtyFrames · BattleScaleZero_IssuesNoTicks |
| Bezier3Tests | 베지어 궤적 — 양 끝 · 퇴화 입력 · 발 번호로 좌우 교대 | 짝 | ProjectileMathTests::베지어_퇴화_입력은_직선으로_붕괴한다. ⚠ 부분 공백: 제어점 좌우 교대·번호로 스윙 확대 |
| BlinkMathTests | 보스 자기 도약 목적지 — 리더 한 칸 너머 · 막히면 행우선 링 · 상한에서 종료 | 짝 | BossTests::증상_보스가_밀집한_곳으로_도약하고_비행이_끝나는_틱에_그_자리에_슬램이_떨어진다. ⚠ 부분 공백: 막힌 착지점 링 탐색·상한 종료·NaN 가드 |
| BoardSpaceTests | 셀↔월드 정합의 권위는 주입된 `GridLayout`(회전·비균일·오프셋) · 레이캐스트 평면 | 짝 없음 — 규칙 누락 의심 | 없음. `Scripts/Core/BoardSpace.cs` 는 **남고 새 층이 쓴다**(`CoreMapOverlay` 등). 파일째 퇴역 asmdef 에 있어 같이 지워진다 — 남는 lane 으로 옮길 후보 |
| BonusWaveScheduleTests | 보너스 포탈 번갈아 배분 · 첫 스폰 기준 등차 시각 · 결정론 | 짝 | PlayCore:CoreScreenTransferTests::보너스를_당기면_포탈이_판의_시계로_열리고_닫힌다(`BonusWaveSchedule.Build` 를 부른다 `:114`). ⚠ 부분 공백: 순수 배분·등차·잘못된 입력 = 빈 배열 |
| BoomerangBakeAndDrainTests | 부메랑 굽기·드레인이 궤적 필드를 다 채운다 · 퇴화 저작은 크게 거절 | 짝 | CardProbeTests::실행자가_없는_규칙은_굽기_실패다_그리고_원본_정의표는_무변이다 · Assets:CoreBuilderDriftTests::잘못된_발사_명세는_거절된다 · Assets:CardBakeTests::라이브_카드는_전부_무언가를_싣는다. ⚠ 부분 공백: 방향 바인딩 비행거리 0 거절 · 칸 바인딩 탄 거절(드레인 절반은 브리지 전용) |
| BoomerangTests | 왕복 궤적 — 반 시간에 최대 거리 · 발사점 뒤로 안 감 · 완주는 누적 시간 | 짝 | ProjectileMathTests::부메랑은_발사점_뒤로_가지_않는다 · 부메랑_완료는_누적_시간으로_본다 |
| BossCcImmunityTests | 보스 CC 면역 = 기절·수면·넉백(스택 임계 기절 포함) · 감속·도트 토큰은 받는다 | 짝 | CcStateTests::보스는_행동불능_면역이지만_버프_디버프는_받는다 · 보스_면역은_기절_수면_넉백만_막고_감속은_받는다 · CombatRulesTests::보스는_기절_수면_넉백에_면역이다 |
| BounceRetargetTests | 튕김 재조준 — 사거리 내 최근접 · 제외 · 동률 낮은 인덱스 · 반경 0 없음 | 짝 | ProjectileMathTests::튕김은_제외한_최근접을_고른다 · 튕김은_진영을_본다 · 튕김_반경_0_은_선정_없음이다 |
| CcActionLockTests | 행동 잠금 = 기절·수면뿐 | 짝 | CcStateTests::잠금은_기절과_수면뿐이고_넉백은_아니다 |
| CcApplySystemTests | CC 병합 — 종류당 슬롯·긴 시간·주기 변경 비례 환산 · 사라진 대상 무시 · 거점 면역 | 짝 | CcStateTests::종류당_슬롯_하나이고_시간은_긴_쪽이다 · 주기가_바뀌면_진행률을_새_주기로_비례_환산한다 · 거점은_상태이상과_모디파이어에_전면_면역이다 · 이미_사라진_대상에_거는_것은_요청이_아니라_사고다 |
| CcDecaySystemTests | CC 감쇠·만료 제거·나머지 보존 | 짝 | CcStateTests::감쇠는_만료된_종류를_비트로_알린다 · 무한_슬롯은_감쇠를_자연_통과한다 |
| CellLayersInstallTests | 셀 층 비트 → 통행 마스크(칸 층 ∩ 슬롯) · 비행은 장애물 무시 · 슬롯 설치 | 짝 | NavGridAndTrimTests::통행_마스크는_칸_층과_슬롯_마스크의_교집합이다 · 비행_슬롯은_지상_장애물을_벽으로_보지_않는다 · MapRuntimeTests::경유점과_거점이_슬롯이_된다 (설치/해제 Dispose 는 옛 기계) |
| CostRuntimeTests | 코스트 — 지불은 충분할 때만 · 상한 · 재생 배율 · 재생 시작 | 짝 | MatchCostTests::지불은_성공_판정_뒤에만_일어난다 · 상한에서_멈춘다 · 재생_배율은_반입이_정하고_판_안에서_안_바뀐다 · 재생은_배치_창이_닫히는_순간_시작한다 · 획득은_상한을_넘지_않는다 |
| CostWellMathTests | 물통 채움 산식(최대면 가득 · 소수부 되감김 epsilon) | 짝 없음 — 옛 기계 전용 | 옛 `CostDisplay`(묶음 3) 전용 — 새 `CoreCostDisplay` 는 `CostWellMath` 를 안 쓴다(`CostWellMath.cs` 는 퇴역 목록 밖 고아). 화면 수 내림은 MatchCostTests::화면_숫자는_내림이고_판정은_실수다 |
| DeadCasterFactionTests | 시전자가 죽은 뒤 도는 스킬의 진영 = 죽은 자의 진영(적의 작별 선물은 방어유닛을 친다) | 짝 | TriggerSymptomTests::부착한_규칙이_붙은_유닛이_죽으면_작별_선물이_그_자리에서_터진다_시전자가_없어도 (rules E3). ⚠ 부분 공백: **적** 시전자 쪽 진영 단언 |
| DefenderAiStateSystemTests | 방어유닛 AI 상태 — 배치 중 → 준비 · 스윙 = 교전 · 소환 유지 | 짝 | UnitAiRulesTests::배치중이_모든_것보다_위다 · 유지중은_소환_정책에만_있다 · DefenderAiTraceTests::상태가_변할_때만_한_건이고_이전과_이후를_값으로_싣는다 · DefenderAiTests(잔류) |
| DefenderBoardLimitTests | 판 위 동시 배치 상한 — 미저작 1 · 거부 사유 append | 짝 | MatchPlacementTests::판_상한은_유닛_저작과_모드_상한을_둘_다_본다 · PlacementSlotBlockTests::소진이_쿨타임보다_먼저_답한다 |
| DefenderDensityTests | 밀집 착지 앵커 — 군집 선택 · 동률 행우선 · 입력 순서 무관 | 짝 | BossTests::증상_보스가_밀집한_곳으로_도약하고_비행이_끝나는_틱에_그_자리에_슬램이_떨어진다. ⚠ 부분 공백: 동률·입력 순서 무관 결정론 |
| DefenderHunterGateTests | 사냥판은 헌터가 있을 때만 · 방어유닛 쪽 유한 dist | 짝 | DetectionRulesTests::무제한_감지는_유출_면제를_받는다 · 사냥판_반경은_가장_짧은_사거리로_내려간다 (`BossTag`/`DefenderHunterTag` 태그 게이트는 ECS 기계) |
| DefenderLockTests | 방어유닛 지속 락 — 더 가까운 적이 와도 유지 · 이탈/사망 해제 · 힐러·가디언·최전방 카드는 락 안 함 | 짝 없음 — 규칙 누락 의심 | 일부만 — CombatRulesTests::행동_불능이면_락을_비우고_다시_안_잠근다(C13) · CombatPureMathTests::히스테리시스는_유지만_넓힌다. 락 유지·해제·제외 4종(`CombatPhase.cs:565-597`) 판 단언 없음 |
| DeploymentActivationSystemTests | 배치 페이즈 시계 — 비행 중 활성화 없음 · 모션 길이 뒤 활성화 · 0 이면 1틱 · 사망 시 폐기 | 짝 | MatchPlacementTests::배치_모션이_있으면_착지_뒤에_활성화된다 · 배치_모션이_0이면_페이즈_자체가_없다 · 배치_중에_죽으면_활성화도_배치_스킬도_없다 · DeployPhaseClockTests(잔류) |
| DetectionChaseFieldTests | 감지 2단계 — 갈 수 있는 경로 · 우회가 짧은 쪽 · 우회 상한 초과 시 원래 길 · 비행은 벽 너머 · 도달 불가면 다음 후보 | 짝 없음 — 규칙 누락 의심 | 없음. 우회 상한(`AiMovePhase.cs:38`)·후보 넘김 구현은 있고 DetectionRulesTests 에 해당 단언 0 |
| DetectionLeakProofTests | 감지는 유출·공성 전환을 건드리지 않는다 — 유한 감지 적도 골에서 공성 전환 · 무제한만 면제 · 어그로가 감지를 이긴다 | 짝 없음 — 규칙 누락 의심 | 무제한 쪽만 — DetectionRulesTests::무제한_감지는_유출_면제를_받는다 · MovementRulesTests::공성형은_골에서_살아남는다는_것을_사건이_말한다. 「**유한** 감지 적이 사냥 중에도 골에서 공성 전환」(본 파일이 「가장 위험한 계약」이라 부른 것) 단언 없음 |
| DetectionSystemTests | 감지 — 반경 안/밖 · 무제한 · 마스크 밖 · 어그로된 적은 감지 안 함 · 동거리 낮은 simId · 대상 사망 1초 관성 · 2초 막힘 해제 · CC 중 막힘 무누적 · 발견 사건 전이 1회 | 짝 없음 — 규칙 누락 의심 | 일부만 — DetectionRulesTests::발견은_전이_1회다 · 감지_0_은_오늘과_같은_경로다 · 유한_감지는_방어유닛_앞에서_멈춘다. 관성(`AiMovePhase.cs:30,389-394`) · 막힘 해제(`:31,402-423`) · 어그로 중 감지 안 함 · simId 동률 단언 0 |
| DistContractTests | dist/flow 소비처 계약 — 골 0 · 도달 불가 센티넬 · 단조 · 최전방은 작은 dist | 짝 | FlowFieldBuilderTests::소스가_없으면_전_칸이_도달_불가다 · 직선에서_모든_칸이_목적지를_가리킨다 · CombatPureMathTests::최전방은_골까지_남은_거리로_정렬한다 · 최전방은_도달_불가를_건너뛴다 |
| DotApplySystemTests | 도트 — 연속은 DPS×dt · 틱은 즉시 1회·간격마다 · 큰 dt 다회 | 짝 | DotSetTests::연속_지속_피해는_값이_DPS_다 · 새로_걸린_지속_피해는_진입_즉시_1회_준다 · 안전_상한이_무한_루프를_막는다 |
| DotEffectMergeTests | 도트 병합 키 (origin, element) — 둘 중 하나만 달라도 슬롯 분리 · 주기 변경 비례 | 짝 | DotSetTests::출처와_원소_둘_중_하나만_달라도_슬롯이_갈린다 · 주기가_바뀌면_진행률이_비례로_넘어간다 · 새로_걸린_지속_피해는_진입_즉시_1회_준다 |
| DotTickTests | 틱 누적 산식 · 프레임당 상한 | 짝 | DotSetTests::안전_상한이_무한_루프를_막는다 · 새로_걸린_지속_피해는_진입_즉시_1회_준다 · 연속_지속_피해는_값이_DPS_다 |
| EffectIntegrationTests | 모디파이어 스탯이 **실제로** 이동 속도·피해·공속에 곱해진다 | 짝 없음 — 규칙 누락 의심 | 이동만 — EffectRulesTests::감속을_건_적은_같은_판에서_느리게_간다. `Effective.DamageMul`·`AttackSpeedMul` 이 판의 실제 피해·주기에 곱해지는 단언 0(코어 테스트는 슬롯/`Effective` 값까지만 — EffectTileTests `:10`, PickupTests `:77`) |
| EffectTickSystemTests | 회오리 장 만료 시 파괴 · 남으면 유지 | 짝 | FieldCarrierTests::수명은_이동_뒤에_깎이고_소멸_사건을_낸다 · 수명_0은_무기한이다 |
| EffectTileModifierTests | 효과 타일 모디파이어 — 부여·영구·다중 스탯 분리 슬롯 | 짝 | EffectTileTests::증상_효과_타일에_놓으면_공격력이_오르고_퇴근시키면_회수된다 · 효과는_활성화_엣지에_걸린다 · 증상_퇴근_뒤_같은_칸에_다시_놓으면_다시_받는다 · ModifierSetTests::칸_단위_회수는_그_칸의_슬롯을_전부_지운다 (시너지 스택은 은퇴) |
| EmitterTickTests | 발사 시퀀스 스케줄 — 시작 틱 첫 발 · 간격 0 일괄 · 잔여 이월 · 무작위 시드 재현 | 짝 | PatternEmissionTests::시작_틱에_첫_발이_나간다 · 간격_0_이_이어지면_같은_틱에_전부_나간다 · 잔여_이월로_드리프트가_0_이다 · 버스트_길이는_첫_간격을_무시한다 · 같은_씨앗에는_같은_N발이다 · 난수_저작이_아니면_저작값을_안_건드린다 |
| EnemyAiStateSystemTests | 적 FSM 전이 — 사거리 안 Engaging · 밖 Marching · 어그로 Standoff/Chasing | 짝 | UnitAiRulesTests::어그로가_없으면_사격_대상_유무로_갈린다 · 어그로는_사격_대상을_덮는다 · EnemyAiStateTransitionTests(잔류). ⚠ 부분 공백: Focus 락이 사거리 밖이면 풀고 교전 |
| EnemyBehaviorTests | FocusUntilDead — 물면 죽을 때까지 · 사거리 밖이면 해제 · Nearest 는 매번 최근접 | 짝 없음 — 규칙 누락 의심 | 없음. `TargetMode.FocusUntilDead`(`CombatParts.cs:38`) 을 쓰는 코어 테스트 0(고정구는 전부 `Nearest`). 저작 쪽만 Assets:DragonBreathAuthoringTests::Dragon_FocusesUntilDead_SoStacksCanReachThreshold |
| EnemyTargetPriorityTests | 직업 **우선순위** — 사수는 더 가까운 가디언보다 레인저를 먼저 · 우선 없으면 최근접 | 짝 없음 — 규칙 누락 의심 | 없음. `AttackState.PriorityClass`(`CombatPhase.cs:535`) 를 쓰는 코어 테스트 0(필터 `ClassMask` 테스트와는 다른 축) |
| EnemyTierBakeTests | 엘리트(보스 아닌 메커닉 보유 적) — 규칙 슬롯은 받되 보스 부속 없음 · 감지 저작 · 분열 저작 거절 | 짝 | Assets:CoreTriggerEnumPinTests::라이브_유닛_능력과_적_악몽이_전부_규칙이_된다 · SplitTests::자기_자신을_가리키는_분열은_건너뛰고_말한다 · SplitChainTests(잔류) · Assets:EnemyCatalogAuthoringTests::EveryCatalogEnemy_HasValidSplitChain · Assets:DetectionRangeAuthoringTests. ⚠ 부분 공백: 「엘리트는 보스 면역·어그로 면역을 안 받는다」 판 단언 |
| FillWalkMaskTests | 걷기 마스크 = 타일 ∩ 장애물(골·고립 칸은 걷기) | 짝 | NavGridAndTrimTests::통행_마스크는_칸_층과_슬롯_마스크의_교집합이다 · 장애물은_정적_벽과_합쳐진다 · 마스크_미생성은_평지로_본다 |
| FlowFieldBuilderTests | 흐름장 — 직선·우회·단절·다중 소스·대각 비용·코너컷 거절 | 짝 | FlowFieldBuilderTests::직선에서_모든_칸이_목적지를_가리킨다 · 장애물이_있으면_돌아간다 · 대각은_양쪽_직교가_열려_있을_때만_허용된다 · 대각_비용이_직교보다_비싸다 · 다중_소스는_최근접_소스를_향한다 · 소스_수집은_자기_칸을_빼고_디스크를_덮는다 |
| FlowFieldRebuildTests | 막으면 돌아간다 · 치우면 복구 · 완전 봉쇄는 도달 불가지 벽 아님 | 짝 | MovementRulesTests::길을_막으면_돌아간다 · MapRuntimeTests::막으면_돌아간다 · 장애물이_바뀐_틱에만_다시_굽는다 |
| FlowFieldSingletonTests | 다중 골 칸 판정 · 미생성 폴백 | 짝 | MapRuntimeTests::경유점과_거점이_슬롯이_된다 · MatchStructureTests::마음_타워는_골마다_하나이고_체력을_안_든다. ⚠ 부분 공백: 골이 여럿일 때 각 골 칸 도달 판정 |
| FlowRecoveryTests | zero-flow 복구 방향 = 최소 dist 이웃 · 없으면 0 · 모서리 안전 | 짝 | MovePureMathTests::더_가까운_이웃_쪽으로_내려간다 · 더_나은_이웃이_없으면_zero_다 · 자기_칸이_도달_불가여도_탈출한다 · 동률에서_직교_순서가_결정론을_준다 |
| FootprintPlacementCheckTests | 다칸 배치 판정 — 전 칸 통과 · 칸별 사유 · 종합 우선순위 | 짝 | MatchPlacementTests::다칸은_전_칸이_통과해야_한다 · 점유는_주인과_쌍으로_바뀐다 · MapRuntimeTests::다칸_점유를_통째로_막는다 · FootprintMathTests(잔류) (재배치 자기 겹침은 재배치 은퇴) |
| FrenzyStackingTests | 광란 — 최대 중첩 상한 환산 · 비버프 거절 · 공격 N회 자기 버프 | 짝 | ModifierSetTests::상한은_배율_빼기_1에_최대_중첩을_곱한_값이다 · 상한은_크기만_막고_남은_시간은_안_막는다 · 상한은_신규_슬롯_경로에도_걸린다 |
| FrontmostAttackLockTests | 최전방 카드 — 흐름 dist 최소 · 선딜 중 유지 · strict lapse · 골 지난 적 포함 · 주 대상만 배율 · 가디언 swap | 짝 | AttackModTests::최전방_수식자가_있으면_최전방을_물고_주_대상에_배율이_붙는다 · CombatRulesTests::골을_지난_적도_때린다 · 가디언_대표는_실제로_때린_적이다 · 선딜_중_대상이_사라지면_빗나간다 |
| FrontmostTargetingTests | 최전방 순위 — 흐름 dist > 거리² > simId · 도달 불가 제외 | 짝 | CombatPureMathTests::최전방은_골까지_남은_거리로_정렬한다 · 최전방은_도달_불가를_건너뛴다 · 동률은_먼저_스폰된_쪽이_이긴다 |
| GameManagerMatchCountTests | 판 수 카운트 — 결과·나가기 모두 1회 · 이중 신호 1회 | 짝 | PlayCore:CoreMatchEntryTests::나가기와_결과가_겹쳐도_한_판은_한_번만_센다 · 메뉴_나가기는_로비로_돌아가고_한_판을_센다 |
| GoalProjectileTests | 호밍 직격은 골도 맞는다 · 방어 광역은 골 포함 · 적 광역은 골 무시·적 거점 포함 · 길막은 어느 풀도 아님 | 짝 | MatchStructureTests::공성형은_마음_타워를_때리고_그만큼_마음이_깎인다 · ProjectileBehaviorTests::칸_광역은_반경_안_전원을_때린다. ~~⚠ 부분 공백: 광역 풀별 거점 포함/제외 · 길막 제외~~ **9c 실현**(`ea73d1ddd` 길막 제외) · 풀별 거점 = **사용자 결정 ⑦-2: 새 동작 유지**(부가 피해도 거점을 친다 — `0cbb0cd31` 철회 `fb0c9952c`) · ProjectileBehaviorTests 8 |
| GoalTargetingPriorityTests | 거점은 일반 후보 — 거리로 경쟁 · 힐러는 거점 안 봄 · Focus 가 거점도 문다 | 짝 | CombatRulesTests::거점은_거리로만_경쟁한다 · CombatPureMathTests::힐러는_가장_다친_아군을_고른다 · Assets:AuthoredTargetMaskTests 는 같은 묶음 퇴역 — Assets:CoreBuilderDriftTests::힐러는_다친_아군을_회복하고_적은_회복하지_않는다 |
| GoalTauntGrantTests | 무공격 골-grant 적의 도발 마스크 OR·원복 | 짝 없음 — 옛 기계 전용 | rules **C26 제거**(무장 해제 적 임시 도발 공격 · 골 grant 은퇴) |
| GoalTowerArchetypeTests | 브리지가 만든 골 타워 아키타입 = 공용 픽스처 | 짝 없음 — 옛 기계 전용 | ECS 아키타입 drift 방지선. 「거리로 경쟁」은 CombatRulesTests::거점은_거리로만_경쟁한다 |
| GridMathTests | 월드↔칸 반올림·클램프·원점 · 흐름 대각 스텝 | 짝 | MapGridMathTests::월드에서_칸은_반칸에서_위로_붙는다 · 격자_밖은_클램프되지만_무클램프는_밖을_말한다 · 흐름_단위벡터의_대각_성분을_버리면_스텝이_사라진다 · 칸_인덱스는_행우선이다 |
| HandViewSelectionSignalTests | 손패가 열린 채 선택 대상이 바뀌면 튜토리얼 신호 | 짝 없음 — 옛 기계 전용 | 튜토리얼 전량 제거(사용자 결정 ④ · 8d) |
| HazardCasterTests | 해저드 캐스터 — 사거리 내 적 칸에 시전·쿨·스냅샷 | 짝 없음 — 옛 기계 전용 | 캐스터 4기 + 캐스트 기계 **제거 확정**(README 계약 9 · rules F32) |
| HazardDestroyedEventTests | 길막 사망 사건은 파괴 전에 · 싱크 없어도 파괴 | 짝 | BlockingHazardTests::막는_칸은_저작_모양이고_부서지면_다음_틱에_길이_열린다 · DestroyEventTests::사라진_유닛은_전부_소멸_이벤트를_냈다 · PlayCore:CoreEffectViewTests::길막은_해저드_풀이_들고_부서지면_회수된다 |
| HazardShapeSamplerTests | 해저드 모양 샘플 — 1칸 · 3×3 · 반경 | 짝 | HazardZoneTests::모양이_반경을_정하고_음수는_존_효과가_없다 · BlockingHazardTests::막는_칸은_저작_모양이고_부서지면_다음_틱에_길이_열린다 |
| HealAppliedEventTests | 회복 사건 — 들어온 회복만(재생은 아님) · 사망/배치 중 제외 · 한 틱 다회 합산 1건 | 짝 | HeatFatigueTests::열기_회복은_넘치는_만큼_잘라낸다_만피면_아무것도_안_들어온다(`HealApplied` 청취 `:38`) · CombatRulesTests::피해와_회복은_그_틱에_비운다. ⚠ 부분 공백: 재생은 사건 없음 · 다회 합산 1건 |
| HealthRatioTests | 체력 비율 정의·클램프 · 최대체력 비율 피해 | 짝 | CombatRulesTests::피해_사건은_그_틱_최종_체력_비율을_싣는다 · PickupTests::먹으면_공속이_오르고_라스트런이_끝나면_최대체력의_비율만큼_피해를_입는다 |
| HealthScaleMaxTests | 최대체력 배율 — 축소 클램프 · 복원 무료 회복 없음 · 1HP 바닥 | 짝 | MaxHealthScaleTests::바닥은_1_HP_다 · 축소하면_현재값을_잘라내고_복원에_무료_회복이_없다 · 기준은_언제나_스폰_시점_원본이다 |
| HeatMathTests | 열기 반전 — ≤flip 회복 · >flip 손실 · 오버힐 클램프 · HP1 바닥 | 짝 | HeatFatigueTests::열기_회복은_넘치는_만큼_잘라낸다_만피면_아무것도_안_들어온다 · 열기가_반전_임계를_넘으면_손실이고_체력_1_밑으로는_안_내린다 · 열기는_상한에서_멈춘다 |
| HuntCloseInLockTests | 사냥 레인 접근 보정 게이트 — 사격 칸의 헌터는 붙는다 · 잠김·도약·무공격·비헌터는 안 붙는다 (C10 「칸은 통과·몸 거리 실패면 한 칸 더」) | 짝 없음 — 규칙 누락 의심 | 순수 방향만 — MovePureMathTests::접근_보정은_지배축_cardinal_이다. 게이트(누가 붙나) 판 단언 0 · C10 salvage(`PatrolAreaMath.cs:122`) 코어 테스트 0 |
| KillAttributionTests | 킬 귀속 = 최대 피해 출처 · 동점 먼저 접힌 쪽 · 출처 없음 무시 | 짝 | CombatPureMathTests::킬러는_그_틱_최대_피해_출처다 · 동점이면_먼저_접힌_쪽이_유지된다 · 출처_없는_피해는_미귀속이다 |
| LegacyTraceV0Tests | 옛 골든 포맷 왕복·손상 파일 거절 | 짝 없음 — 옛 기계 전용 | `LegacyTraceV0`. 새 포맷은 DeterminismTests::직렬화_왕복이_바이트로_같다 · 옛_계열_트레이스는_코어_리더가_거절한다 |
| LowestHealthTargetingTests | 가장 다친 아군 — 체력비 > 거리² > simId | 짝 | CombatPureMathTests::힐러는_가장_다친_아군을_고른다 · SecondaryTargetingTests::힐러의_2_3번째_회복_대상은_가까운_순이_아니라_가장_다친_순이다 |
| MatchTallyTests | 총점 = 처치 수 · 제출값 = 총점(안정도 무관) | 짝 | MatchHeartScoreTests::점수는_적_처치만_세고_생값이다 · MatchGoalTests::시간_점수_목표는_만료로_끝나고_점수는_킬_생값이다 |
| ModifierAuraClassifierTests | 드림캐쳐 출처 스탯 오라 판정 | 짝 | ModifierAuraClassifierTests(7 전부) |
| ModifierAuthoringTests | 올리면 가산 · 깎으면 곱셈 | 짝 | ModifierSetTests::저작_분류는_올리면_가산_깎으면_곱셈이다 |
| ModifierFrameworkTests | 같은 키 갱신 · 결합 · 사라진 대상 무시 · 거점 면역 · 다중 임계 · 산출물 4종 채널 · 만료 · 클램프 | 짝 | ModifierSetTests::네_축_중_하나라도_다르면_슬롯이_갈린다 · 결합식은_가산_합에_곱셈_곱이다 · 클램프_경계는_스탯마다_다르다 · 만료도_다시_접으라는_표시를_켠다 · CcStateTests::거점은_상태이상과_모디파이어에_전면_면역이다 · EffectRulesTests::공격_산출물이_스탯을_걸고_출처는_때린_자다 · StackRuleTests::임계는_올라가는_길에만_발화한다 |
| ModifierMathTests | 결합 + 클램프(바닥·천장·이동 바닥·Override) | 짝 | ModifierSetTests::결합식은_가산_합에_곱셈_곱이다 · 클램프_경계는_스탯마다_다르다. ⚠ 부분 공백: Override 가 가산·곱셈을 이긴다 |
| MovementCellTrimApplyTests | 벽 칸 진입은 현재 칸 경계로 접힌다 | 짝 | NavGridAndTrimTests::막힌_칸으로_넘어가면_현재_칸_경계로_접힌다 |
| MovementCellTrimTests | 막힘 술어 · 경계 클램프 · 변위 상한 < 1칸 | 짝 | NavGridAndTrimTests::경계_밖은_항상_막힘이다 · 변위_상한이_터널링을_막는다 · 막힌_칸으로_넘어가면_현재_칸_경계로_접힌다 |
| MovementCompositionTests | 최종 위치 = 흐름 스텝 × 속도배율 + 외력 변위 | 짝 | MovementRulesTests::당김은_이동을_대체하지_않고_더해진다 · EffectRulesTests::감속을_건_적은_같은_판에서_느리게_간다 · 넉백은_잠금이_아니라_외력이다 |
| MovementImpulseAcrossStatesTests | 넉백은 **상태와 무관**(대치·추격·교전 정지·순찰 대기에서도 밀린다) | 짝 | EffectRulesTests::넉백은_잠금이_아니라_외력이다(행진 적만) · CcStateTests::넉백은_초당_속도라_변위가_dt_에_비례한다. ⚠ 부분 공백: 비행진 상태(Standoff·Chasing·Engaging-Halt·순찰 대기)에서의 넉백 |
| MovementSystemTests | 흐름 따라 이동 · 골 표식 · 도달 불가 정지 · 상태별 이동 · 포탈·회오리 · 경유점 | 짝 | MovementRulesTests::적이_골까지_걸어간다 · 골_도달은_1회_고정이다 · 정지는_결과_관찰이다 · 포탈은_출구로_옮긴다 · 당김은_이동을_대체하지_않고_더해진다 · MovePureMathTests::인접_칸이면_지났다 · 도달_불가면_건너뛴다 |
| NavGridTests | 벽 질의 — 격자 밖 막힘 · 정적 마스크 · 고립 걷기 칸은 벽 아님 · 장애물 | 짝 | NavGridAndTrimTests::경계_밖은_항상_막힘이다 · 마스크_미생성은_평지로_본다 · 장애물은_정적_벽과_합쳐진다 |
| NearestLockTests | Nearest 적 락 — 더 가까운 방어유닛이 와도 유지(보스 포함) · CC 가 락을 비움 · CC 뒤 새로 고름 · CC 가 커밋을 못 뺏음 | 짝 없음 — 규칙 누락 의심 | C13 만 — CombatRulesTests::행동_불능이면_락을_비우고_다시_안_잠근다. 락 유지(「행동이 실제로 바뀌었다」의 증거라던 본체) 판 단언 0 |
| NearestTargetingTests | 반경 내 최근접 결정론 · simId 동률 | 짝 | PatternEmissionTests::최근접_동률은_낮은_id_가_이긴다 · SkillAimTests(잔류) |
| ObstacleLifetimeTests | 장애물 수명 · 막힌 칸 합집합(길막 포함, 죽은 길막 제외) | 짝 | MapRuntimeTests::장애물이_바뀐_틱에만_다시_굽는다 · MovementRulesTests::디버그_장애물이_흐름장을_다시_굽는다 · BlockingHazardTests::막는_칸은_저작_모양이고_부서지면_다음_틱에_길이_열린다 |
| ObstacleSignatureTests | 막힌 칸 시그니처 결정론 | 짝 | MapRuntimeTests::같은_집합이면_같은_시그니처다 · 개수가_다르면_반드시_다른_시그니처다 |
| OrbitTests | 궤도 궤적 — 반경 유지 · 접선 · 위상 균등 | 짝 | ProjectileMathTests::궤도_접선은_부호만_남긴다 · 궤도_위상은_균등_배치다 |
| PathHitRehitCooldownTests | 관통 재타격 창 · 0 은 영구 1회 · 궤도/부메랑 자기 시계 · 수명 종료 | 짝 | ProjectileMathTests::재타격_0_은_피해자당_영구_1회다 · 재타격_창은_시간으로_열린다 · ProjectileBehaviorTests::경로_스윕은_관통_예산까지_때린다 · 임자가_사라지면_탄도_사라진다. ⚠ 부분 공백: 부메랑 넉백(가는 길 밀고 오는 길 당김) |
| PathSmoothingTests | 평활화 — 먼 가시점 직행 · 몸통 걸리면 막힘 · 코너 조준 | 짝 | PathSmoothingTests(4) · AgentCollisionTests::코너_조준_오프셋은_충돌_여유와_같은_값을_쓴다 |
| PatrolAreaMathTests | 순찰 거점 박스 이동 — 안 적 추적 · 없으면 집 · 밀려나면 복귀 · 사격 칸이지만 몸이 멀면 계속 접근 | 짝 | PatrolAreaMathTests::구역_안_적을_향해_간다 · 구역_안에_적이_없으면_집으로_돌아간다 · 집에_서_있으면_정지한다 · 박스_밖으로_밀려나면_마스크_없는_필드로_복귀한다. ⚠ 부분 공백: OnFiringCell 3종(C10 salvage) · 벽이 가른 박스 · 도달 불가 최근접이 도달 가능한 적을 가리지 않음 |
| PatrolLayerRoutingTests | 순찰 **시스템 경로**가 유닛 층 마스크로 움직인다(지상은 배치 칸만, 길 유닛은 길만) | 짝 없음 — 규칙 누락 의심 | 헬퍼만 — NavGridAndTrimTests::통행_마스크는_칸_층과_슬롯_마스크의_교집합이다 · TraversalLayerAxisTests(잔류). 본 파일이 「헬퍼는 맞는데 경로를 안 본」 실패로 만든 판-경로 단언이 코어에 없다 |
| PatrolSystemIntegrationTests | 순찰병은 골 칸에서 골 표식 없음 · 주인 사망 연쇄 소멸 · 소환 1기·stale 재소환 · 첫 소환은 구역에 적 · 쿨 대기 | 짝 | SummonPatrolTests(5) · CombatRulesTests::소환사는_소환물이_살아있어도_쿨을_돌린다 · 소환사가_죽으면_순찰병도_소멸_사건을_낸다. ⚠ 부분 공백: 순찰병 골 칸 무시(`AiMovePhase.cs:591`) |
| PatternBakeTests | 발사 패턴 굽기 — 탄 불일치·탄 없음·잘못된 시퀀스 크게 거절 · 값 클램프 · 카드 경로 비주기 거절 | 짝 | Assets:CoreBuilderDriftTests::발사_명세의_탄은_정의표_안을_가리킨다 · 잘못된_발사_명세는_거절된다 · 발사_명세_값은_정의역으로_접힌다 · Assets:PatternSelectionRulePinTests |
| PatternDirectionTests | 패턴 방향 = 저작 각도 보간 · 기준 방향 회전 | 짝 | PatternEmissionTests::방향은_저작_각도_사이를_보간한다 |
| PatternScopeTests | 후보 반경 — 0 이하는 전량 통과·원본 순서 · 원본 인덱스 반환 · 양쪽 몸 | 짝 | PatternEmissionTests::반경_0_은_전량_통과다 · 반경_게이트는_원본_인덱스를_돌려준다 |
| PatternTargetingTests | 패턴 대상 선정 결정론 — simId 순위 · 순회 · 셔플 | 짝 | PatternEmissionTests::순회_선정의_순위축은_SimEntityId_다 · 발사_카운터가_0_에_고정되면_같은_순위만_고른다 · 최근접_동률은_낮은_id_가_이긴다 · 후보가_없으면_발사를_소비하고_건너뛴다 · 탄막_씨앗은_사수와_발사_카운터에서_나온다 |
| PlacementCooldownRuntimeTests | 재배치 대기 — 시작·틱·만료·유닛별 독립·교체 시 재시작 | 짝 | MatchPlacementTests::재배치_대기가_끝나기_전에는_못_놓는다 · 퇴근_대기는_사망_대기의_비율이고_뒤집힐_수_없다 · PlacementSlotBlockTests::쿨타임이_코스트보다_먼저_답한다 · DefenderExitCooldownTests(잔류). ⚠ 부분 공백: rules **E22**(같은 유닛 2기 = `max(remaining,new)`) 코어 테스트 0 |
| PlacementLayerTests | 배치 층 = (셀 층 ∩ 유닛 층) ≠ 0 · 미저작은 지상 · 효과 타일은 길 전용 칸 건너뜀 | 짝 | MatchPlacementTests::배치_층과_유닛_층의_교집합이_0이면_못_놓는다 · Assets:CatalogPlacementLayerTests · TraversalLayerAxisTests(잔류) · EffectTilePlacerTests::SelectCells_ReturnsOnlyPlaceCells(잔류) |
| PlacementMaskLivePathTests | 라이브 경로 — 열린 칸 층 · 차단 존은 배치만 닫음 · 스폰·골 칸 전 층 폐쇄 | 짝 | DioramaMapBuilderTests::Assemble_BlockZone_ClosesPlacement_KeepsTraversal(잔류) · MatchStructureTests::스폰_골_본능_자리에는_못_놓는다 · Assets:StagePoolBuildabilityTests |
| ProjectileEmitterIntegrationTests | 버스트 1발 1캐리어 · 죽은 숙주 새 버스트 없음 · 빈 풀 발 소비 · 방향 패턴 무대상 발사 · 적 숙주는 방어유닛을 | 짝 | PatternEmissionTests::후보가_없으면_발사를_소비하고_건너뛴다 · 간격_0_이_이어지면_같은_틱에_전부_나간다 · TriggerSymptomTests::캐논_융단폭격은_미사일_수가_반경_안_적_수다. ⚠ 부분 공백: 죽은 숙주는 새 버스트를 안 연다 |
| ProjectileOriginRadiusCarryTests | 원점 몸 반경이 요청→상태로 건너온다(자리형은 0) · 실으면 착탄 판정이 넓어진다 | 짝 | ProjectileBehaviorTests::자리형_탄은_원점_몸을_안_싣는다 · TriggerSymptomTests::적을_죽인_자리에서_시체_폭발이_터지고_그_시체의_몸만큼_넓다 · UnitSkillTests::짱쎈_경계_자폭은_층을_안_가리고_시전자_몸만큼_넓다 · ReachEntryPointGuardTests(잔류) |
| ProjectileRetargetAndBounceTests | 탄 재조준(대상 사망 시 근처 적으로·옵트인) · 끄면 옛 소멸 · 길 전용 재조준은 공중 무시 · 방향탄 관통 소진 → 호밍 튕김 | 짝 없음 — 규칙 누락 의심 · **9c 실현**(관통 소진 튕김) | 순수 선정만 — ProjectileMathTests::튕김은_제외한_최근접을_고른다 · ProjectileBehaviorTests::방향_바인딩은_재조준_반경을_0_으로_접는다. 재조준·관통 소진 튕김이 **탄 수명에 붙었는가**(본 파일의 글루) 단언 0 |
| ProjectileSystemTests | 탄 이동·소멸 · 착탄 피해 · 착탄 넉백(피해자 진행 반대) · 스플래시 · 길 전용 탄은 공중 무피해 · 포물선 · 칸 광역 · 우선 대상 보너스 | 짝 | ProjectileBehaviorTests::유도탄은_맞히고_사라진다 · 임자가_사라지면_탄도_사라진다 · 칸_광역은_반경_안_전원을_때린다 · 요청은_한_틱_뒤에_탄이_된다 · AttackModTests::최전방_수식자가_있으면_최전방을_물고_주_대상에_배율이_붙는다. ⚠ 부분 공백: **착탄 넉백**(`ProjectileState` Knockback 칸) · 스플래시가 직격 대상 제외 · 길 전용 탄의 공중 무피해 |
| ProjectileVariationTests | 탄 색 hue 변주 · 같은 시드 같은 순열 · 풀 재사용 누적 없음 | 짝 없음 — 규칙 누락 의심 | 없음(뷰). 새 `CoreProjectileViewPool.ApplyHueShift`(`:624`) 에 테스트 0 |
| RangeDisplayContractTests | 화면이 판정을 좁게 가르치지 않는다(표준 몸 · 사거리 1 = 8이웃) | 짝 | PlayCore:CoreViewRemainderTests::행8_배치_드래그의_사거리_칸은_판정과_같은_자로_세고_링_안을_한_겹으로_채운다 · PlayCore:CoreViewYardstickTests::오버레이의_도달_판정은_정본_진입점_하나다 |
| RangePredicateInvariantsTests | 사거리 술어 상대 불변식 — 단조·대칭·자기 포함·멀어지면 안 돌아옴 | 짝 | AttackReachTests::칸_판정과_월드_판정이_같은_본체를_지난다 · 도달은_사거리에_양쪽_몸을_더한_원이다. ⚠ 부분 공백: 단조·대칭 스윕 |
| RelocationCheckTests | 재배치 판정 | 짝 없음 — 옛 기계 전용 | 재배치 **은퇴**(README 고지 ⑴ · 퇴근이 대신) |
| SeparationTests | 겹침 해소 — 중심선 · 깊이 · 정확 겹침 0 · 전진 성분 거부 · 상한 | 짝 | SeparationTests(7 전부) |
| ShieldMathTests | 실드 병합(같은 출처 max·교차 합) · 오래된 것부터 흡수 · 완전 흡수는 피격 아님 | 짝 | ShieldMathTests::같은_출처는_max_다른_출처는_합산이다 · 소모는_오래된_것부터다 · 완전_흡수는_피격이_아니다 |
| SkillAdapterDirectWriteTests | 스킬 적용은 채널 쓰기뿐, 직접 쓰기는 닫힌 목록 | 짝 | CoreArchitectureTests::스킬_경로의_쓰기는_IntentApplier_한_표면을_지난다 |
| SkillEntityIdPinTests | 도메인 핸들 센티넬 = `SimEntityId` 센티넬 | 짝 | SimEntityIdTests::센티널이_unit0_결정과_같다 · None_은_오름차순에서_범위_밖이다 |
| SkillLayerEndpointTests | 같은 스킬이 부른 쪽의 상대를 고른다 · 전 concrete 등록 · id 유일 | 짝 | SkillRoutingTests::레지스트리는_concrete_33_이고_캐스트는_없다 · AreaSleepSkillTests::SameSkill_SleepsTheOtherSide_WhenADefenderCastsIt(잔류) (dedup 마스크는 E2 로 은퇴) |
| SkillLoadoutControllerTests | 액티브 굴림 결정론 · **숨긴 카드의 스킬을 풀에서 뺀다**(`FilterHiddenSkills` 6) | 짝 없음 — 규칙 누락 의심 | 굴림만 — Assets:CardViewAssetTests::액티브_굴림은_판_시드로_재현된다. `CoreDeckComposition.FilterHiddenSkills`(8c 가 이사, `CoreDeckComposition.cs:43`) 를 부르는 테스트 **0** — 단언 6개가 이사 안 왔다 |
| SkillMathParityTests | 도메인 순수 코어와 Runtime 순수 코어가 같은 답 | 짝 없음 — A/B 비교 | 비교가 본질(Runtime 소비자 0 이면 은퇴한다고 파일이 명시) |
| SkillModifierKindPinTests | 도메인 ↔ Runtime 모디파이어 enum 값 일치 | 짝 | CoreSkillEnumPinTests::스탯_종류는_값도_개수도_같다 · 결합_연산자는_값이_같고_도메인에만_저작_배율_칸이_하나_더_있다 · 출처_꼬리표는_미러한_값만_고정한다 |
| SkillRoutingCoverageTests | 라우팅 전수 — 스킬 payload 는 라우팅이 있고 아니면 이름 붙은 비스킬 | 짝 | SkillRoutingTests::스킬인_payload_는_어느_트리거든_라우팅이_있다_아니면_부착_전용이다 · 스킬이_아닌_payload_는_라우팅이_없다 · OnPlace_x_충전이_라우팅을_찾는다 |
| SkyFallTests | 하늘낙하 진행·도착 · 예고 0 즉시 · 대기 창/낙하 창 | 짝 | ProjectileMathTests::예고_0_은_첫_틱에_도착이다 · 하늘낙하는_칸과_적이_다른_바인딩이다 · PlayCore:CorePlayThreeSymptomTests::운석을_시전하면_하늘에서_떨어지는_운석이_화면에_보이고_착탄_연출이_터진다. ⚠ 부분 공백: 낙하 비율(FallProgress) 경계 |
| SpatialPlacementCheckTests | 공간 배치 술어 — 경계 밖·비배치·점유·마스크 | 짝 | MatchPlacementTests::칸의_상태는_지형과_유닛_점유를_가른다 · 배치_층과_유닛_층의_교집합이_0이면_못_놓는다 · PlacementSlotBlockTests::드롭_거절과_칸_도색은_같은_답이다 |
| SpawnAlertForecastTests | 스폰 예고 창 — 모든 웨이브(1·당김 포함)가 창을 얻는다 · 입구별 시각 = 실스폰 · 마지막 스폰 뒤 사라짐 | 짝 | RetiredWaveForecastPortTests(26 예고 창 — unit 9 이식) · 종별 병합은 **결정 ⑧-1 새 방식 유지**(9c 행 8): `같은_입구_같은_경로면_종이_달라도_한_줄이고_경로가_다르면_따로다` |
| SpawnBlockingHazardTests | 길막 설치 — 격자 밖·골·기존 막힘·방어유닛 자리 거절 | 짝 | BlockingHazardTests::골_칸과_이미_막힌_칸에는_못_세운다. ⚠ 부분 공백: 방어유닛 점유 칸 거절 |
| SpawnSpreadTests | 스폰 측면 분산 — 대칭·상단 압축·레인 라운드로빈·셀 안 | 짝 | MovePureMathTests::오프셋은_반_칸을_절대_못_넘는다 · 같은_순번이면_같은_레인이다 · 레인이_하나면_중앙이다 · 음수_순번도_안전하다 · 오프셋은_진행방향_수직이다 · MovementTuningTests |
| StackingModifierMergeTests | 누적 상한 — 같은 키 누적 · 상한 정지 · 상한에서도 지속 갱신 · 회수는 한 번에 | 짝 | ModifierSetTests::상한은_배율_빼기_1에_최대_중첩을_곱한_값이다 · 상한은_크기만_막고_남은_시간은_안_막는다 · 회수는_항등값_재발행이_아니라_슬롯_삭제다 |
| StagePoolDevEntriesTests | dev 슬롯 인덱스(Count+i) 해석 · 에디터 등록 dev 스테이지는 라이브 덱 상속 | 짝 | Assets:MatchEntryBuildTests::네_갈래_서열_dev_강제_디버그_토너먼트_0번 (브리지 해석 절반은 옛 기계) |
| StructureDestinationTests | 거점 목적지 — 최근접 · 마음도 경쟁 · 방패 마음 제외 · 중심 불통행이어도 footprint 소스 | 짝 | MovePureMathTests::팰_수_있는_것_중_최근접을_고른다 · 후보가_없으면_없다고_말한다 · MatchStructureTests::방패_걸린_마음은_더_가까워도_목적지가_아니다 · 무너진_본능은_더_이상_목적지가_아니다 |
| StructureFixtures | 거점 픽스처 빌더(헬퍼) | 짝 없음 — 옛 기계 전용 | 헬퍼 |
| SweepHitMathTests | 선분 스윕 히트 — 끝점 클램프 · 길이 0 은 점 | 짝 | ProjectileMathTests::스윕은_길이_0_선분을_점으로_퇴화시킨다 · 스윕은_지나간_선분을_훑는다 |
| TargetPersistenceTests | 락 유지 술어(히스테리시스) + Focus 적이 락 대상 이탈 시 다른 사거리 내로 전환·행진 복귀 | 짝 없음 — 규칙 누락 의심 | 술어만 — CombatPureMathTests::히스테리시스는_유지만_넓힌다. Focus 적 판 동작 5건 단언 0(EnemyBehaviorTests 와 같은 구멍) |
| ThreatTableTests | 위협 누적 · 최대 누적 1위 · 동률 먼저 · 죽은 공격자 제외 | 짝 없음 — 옛 기계 전용 | rules **C25 제거**(1위는 아무도 안 읽는다). 위협 의도 배달은 IntentApplierTests::실드_도발_위협_순간이동 |
| TileAoeTests | 칸 광역 소속 — 반경 1 은 대각 포함 · 콘 · 몸이 넓히는 폭발 · 착지 예고 링 = 반경+칸 반폭 | 짝 | CombatPureMathTests::반경_1_광역은_대각을_잃지_않는다 · 큰_몸은_폭발에_더_잘_걸린다 · HazardZoneTests::판정은_칸_반폭_자다_깐_자의_몸은_안_붙는다 · ConeBreathSkillTests(잔류) · 형태 그물(잔류 4) ReachEntryPointGuardTests::SkillDomain_NeverReadsTheDisplayOnlyShapePadding · ThePredicateBody_StaysPrivate_SoOnlyTheTwoEntryPointsExist · OriginBodyRadiusWiringTests::SelfSiteBlasts_CarryTheirOwnerBody_ThroughTheIntentBoundary · PublicReachEntryPoints_AreExactlyTheKnownSet + 코어판 신설 CoreArchitectureTests::전투_코어는_표기_전용_도형_보정항을_읽지_않는다(unit 9 감사 B). ⚠ `CenteredRingRadius_IsTheSingleSource_AndCarriesNoBody` 는 unit 9 가 삭제했다 |
| TileRangeTests | 체비셰프 · 사거리→칸 변환 | 짝 | MapGridMathTests::사거리를_칸으로_바꿀_때는_올림이다 · AttackReachTests::칸_체비셰프는_격자_계층_전용이다 |
| TilemapMapViewTests | 타일맵 페인트 자리 = `BoardSpace` 정합 · 바닥 타일 은퇴 · 격자 정렬 · 골 마커 앵커 | 짝 없음 — 규칙 누락 의심 | 없음(뷰). `Scripts/Core/TilemapMapView.cs` 는 남는다 — BoardSpaceTests 와 같이 남는 lane 으로 옮길 후보 |
| UnitLifecycleSystemTests | 골 도달 사건 · 무공격 적 산화 · 공성 적 생존 1회 표식 · 받을 곳 없으면 표식 안 찍음(E18) | 짝 | MovementRulesTests::골_도달은_1회_고정이다 · 공성형은_골에서_살아남는다는_것을_사건이_말한다 · MatchHeartScoreTests::돌격형은_마음을_치고_산화한다. ⚠ 부분 공백: E18 |
| WaveConceptBossTests | 보스 재케이던스 9 = 블록 마지막 · 호위는 블록 컨셉 필터·예산 · 보스는 첫 슬롯 입구 · 컨셉 라벨 | 짝 없음 — 규칙 누락 의심 | 컨셉 무관 보스만 — WaveGeneratorTests::보스_웨이브는_선봉이_보스고_표식이_붙는다 · Assets:LiveDeckBossAuthoringTests::LiveDecks_UseIntervalNine. 보스 × 컨셉 블록 상호작용 단언 0 |
| WaveConceptGenerationTests | 생성기가 컨셉을 편성으로 — 3웨이브 유지 · 경계 전환·연속 금지 · 입구 불변식 · 풀 비면 레거시 · 고도/직업 필터 · 게이트·상한 · 배율 · 결정론 | 짝 없음 — 규칙 누락 의심 | 없음. 코어 `WaveGenerator` 에 컨셉 경로(`WaveGenerator.cs:89-150`)가 있는데 코어 테스트 고정구는 `Concepts = Empty`(`CoreMatchFixtures.cs:83`) — 컨셉 경로를 타는 테스트 0 |
| WaveConceptMathTests | 컨셉 순수 함수 — 분배 · 입구 배정 · 컨셉 선택(게이트·가중치·직전 제외) | 짝 없음 — 규칙 누락 의심 | 분배·입구만 — WaveGeneratorTests::슬롯_분배는_잔여를_앞_슬롯부터_준다 · 입구_배정은_같은_위상이면_같은_입구다. **컨셉 선택(Pick) 12건** 단언 0 |
| WaveConceptVariantTests | 묶음 가운데 변주 — 미저작이면 완전 동일 · 두 번째 웨이브에만 삽입 · 입구 유지 · 클라이맥스 상시 · 보스 자리 보존 | 짝 없음 — 규칙 누락 의심 | 없음(변주 경로 `WaveGenerator.cs:110-131` 테스트 0) |
| WaveCountRampTests | 수량 곡선 — 첫 웨이브 = 기저 · 단조·포화 · 2단계 곡선 · 지터 범위 | 짝 | WaveGeneratorTests::두_단계_곡선은_break_저작이_없으면_기존_지수다 · AssertMatchesOracle(`ExponentialWaveTotal` 사용). ⚠ 부분 공백: 단조·포화·지터 범위 |
| WaveEligibilityGateTests | 등장 게이트(minWaveNumber) — 그 전엔 안 나옴 · 두 그룹 유지 · 전부 게이트면 fail-open | 짝 | WaveGeneratorTests::등장_게이트는_rng_를_소비하지_않는다. ⚠ 부분 공백: 전부 게이트 fail-open |
| WaveForceRescheduleTests | 당김이 남은 스케줄을 앞당긴다 | 짝 | MatchWaveTests::케이던스는_전멸_또는_상한_경과다 · 당김은_상한이_있고_전멸로만_회복된다 (새 코어는 사건 구동 케이던스) |
| WaveGroupsMatchSpawnTests | 선언한 편성 = 실제 펼침(유닛별 수량) | 짝 | WaveGeneratorTests::라운드로빈_펼침은_소진된_그룹을_건너뛴다 |
| WaveNominalIntervalTests | 명목 트리거 그리드 = i × 상한 간격 · 0 이면 duration/count | 짝 없음 — 규칙 누락 의심 | 없음(브리핑·로그 표시용). `WaveGenerator.cs:73` 의 간격 선택을 단언하는 테스트 0 — 경미 |
| WavePatternGeneratorBossTests | N번째 웨이브 보스+호위 · 보스 풀 회전 · 잡몹 누출 없음 · 결정론 | 짝 | WaveGeneratorTests::보스_웨이브는_선봉이_보스고_표식이_붙는다 · 보스는_잡몹_풀에서_방어적으로_제외된다 · 보스가_1종이면_선택_rng_를_소비하지_않는다 · MatchWaveTests::보스_웨이브는_생성기가_판별하고_경보는_스폰에서_한_번_난다 · Assets:LiveDeckBossAuthoringTests::MapDecks_SpreadTheThreeBossesEvenly |
| WavePatternGeneratorTests | 같은 시드 같은 요약·펼침 · 범위 안 · 라운드로빈 · 플랜 에셋 타임라인 | 짝 | WaveGeneratorTests::같은_시드는_같은_플랜을_낸다 · 웨이브_수와_수량은_저작_범위_안이다 · 라운드로빈_펼침은_소진된_그룹을_건너뛴다 · 저작_플랜은_타임라인이고_리드인이_0이다 · 레거시_2종_경로의_rng_소비_순서가_그대로다 |
| WavePerTypeCapTests | 종류별 상한 — 미저작 무변 · 잘린 몫은 상한 없는 쪽으로 · 총량 보존 | 짝 | WaveGeneratorTests::동시_등장_상한은_rng_를_소비하지_않는다 · AssertMatchesOracle(`ClampGroupCounts` 사용). ⚠ 부분 공백: 나머지 이전·총량 보존 |
| WaveSpawnForecastTests | 입구별 첫 스폰 시각 예보 · 레인 회전 규약 · 보스 선봉 · 경로 해석 | 짝 | RetiredWaveForecastPortTests(27 입구별 예보 — unit 9 이식) · 경로 해석은 **9c 실현 / 결정 ⑧-2 옛 방식 복원**(9c 행 7): `예보의_경로는_스폰과_같은_해석을_따른다` · `예보_경로는_적_저작_레인_기본_최단_순으로_이긴다` |
| WaypointFlowFieldSlotTests | 경유점 슬롯 설치 · 장애물 재빌드는 슬롯 목적지 · 예고선은 경유점 순서 | 짝 | MapRuntimeTests::경유점과_거점이_슬롯이_된다 · 장애물이_바뀐_틱에만_다시_굽는다 · PlayCore:CoreViewYardstickTests::예고선의_경로는_이동과_같은_함수에서_나온다 |
| WaypointProgressTests | 경유점 진행 — 인접 통과 · 도달 불가 건너뜀 · 경로 선택 서열 | 짝 | MovePureMathTests::인접_칸이면_지났다 · 아직_멀면_안_넘어간다 · 도달_불가면_건너뛴다 · 마지막을_지나면_끝난다 · 경로_선택은_좁은_쪽이_이긴다 |
| WhirlpotEngageRepro | 광역 근접 적이 인접 방어유닛에서 교전 · 시전자·동료 제외 · 반경 전원 | 짝 | SecondaryTargetingTests::적의_부가_타격은_가까운_순이다 · Assets:WhirlpotAuthoringTests::Whirl_IsTheBaseAttack_MeleeAoe_WithNoSeparateSingleHit |
| ZoneApplyFactionGateTests | 장판 진영 게이트 — 적에게만 · 길 전용 장판은 공중 무시 · 사각 존 원 경계 | 짝 | HazardZoneTests::진영은_저작_축이고_오늘의_저작은_옛_게이트와_같다 · 대상_통행층은_런타임_스냅샷이고_0은_필터_없음이다 · 판정은_칸_반폭_자다_깐_자의_몸은_안_붙는다 · Assets:BoardEffectAuthoringTests::라이브_장판_저작은_옛_게이트와_같이_적만_노린다 |

## 6. `Tests/EditModeAssets/*`

| 옛 테스트 파일 | 규칙 | 분류 | 코어 짝 |
|---|---|---|---|
| AttackReachParityTests | 옛 `AttackReach` 와 코어 `AttackReach` 의 같은 입력 같은 답 | 짝 없음 — A/B 비교 | 근거 계약 3(옛 러너와 병존하는 동안만 A/B). ⚠ 은퇴하면 **띠·부채꼴 게이트 절대값의 유일한 그물이 사라진다** — AttackShapeGateTests 행 참조 |
| AuthoredTargetMaskTests | 저작 타겟 마스크 — 미저작 = 기본 · 저작 존중 · 아군 겨냥이 이긴다 · 에셋 전량 비0 · 힐러는 적 거점 안 봄 · 배치 제외 = footprint | 짝 | Assets:LiveDefinitionSmokeTests::아군을_겨누는_유닛은_아군만_본다 · 유닛을_노리지_않는_적은_저작_그대로_유닛_비트가_없다 · Assets:CoreBuilderDriftTests::직업_필터를_전부_끈_적은_방어유닛을_못_때린다 · CombatRulesTests::거점은_거리로만_경쟁한다 · MatchStructureTests::스폰_골_본능_자리에는_못_놓는다. ⚠ 부분 공백: `TargetDefaults.ResolveEnemy/ResolveDefender` 폴백 순수 단언 |
| DirectionalVolleyIntegrationTests | 연발 — 샷건 10발 무작위 확산·4칸 · 머신건 0.1초 간격·**다음 트리거 연기** · **행동 잠금 중에도 시퀀스 완주** · 선딜 중 대상 잃어도 샷건 발사 · 트리거당 인스턴스 1 | 짝 없음 — 규칙 누락 의심 | 일부만 — PatternEmissionTests(스케줄) · PatternAttackTests::연발탄의_피해는_패턴_저작값이_아니라_공격_피해다 · 연발_유닛은_평타_단발을_따로_쏘지_않는다 · 방향_발사_연발의_기준은_조준_방향이다 · Assets:LiveDefinitionSmokeTests::연발_유닛의_탄_피해는_실효_공격_피해다. 연발 중 다음 트리거 연기 · CC 중 완주 · 선딜 중 대상 상실 발사 판 단언 0 |
| WaveConceptAuthoringTests | 저작 컨셉 5종 · 라이브 덱이 전부 참조 · **라이브 덱을 생성기로 굴린 결과**(첫 블록 산개 · 공습 블록만 공중 · 3웨이브 유지 · 맵마다 결정론·다른 서열) | 짝 없음 — 규칙 누락 의심 | 없음. 옛 `WavePatternGenerator` 로 굴린다 — 코어 생성기(`MatchDefinitionBuilder` 경유)로 라이브 덱을 굴리는 테스트 0. 덱 보스 쪽만 Assets:LiveDeckBossAuthoringTests · Assets:BonusEnemyNotInDeckTests |
| WaveKillBudgetPinTests | 덱 시드 고정 · 같은 시드 결정론 · 보스 간격 · 킬 예산은 실제 스폰에서 · 스폰 창 ≤ 상한 간격 | 짝 없음 — 규칙 누락 의심 | 일부만 — Assets:LiveDeckBossAuthoringTests::LiveDecks_UseIntervalNine(저작값) · WaveGeneratorTests::같은_시드는_같은_플랜을_낸다(고정구). **라이브 덱 × 코어 생성기** 구조 불변식 단언 0(스폰 창 경고는 `WaveGenerator.cs:79-84` 로그뿐) |
| WaveSpawnLeadInTests | 리드인 — 덱 값이 플랜으로 · 음수 0 · 트리거 그리드 독립 · 플랜 에셋 0 · 라이브 덱 전부 보유 · 첫 스폰은 리드인 뒤 · 당김이 shift 를 안 더럽힘 | 짝 | MatchWaveTests::리드인은_스폰_기준시각에만_더해진다 · 웨이브_1은_전투가_열리자마자_예약된다 · WaveGeneratorTests::저작_플랜은_타임라인이고_리드인이_0이다. ⚠ 부분 공백: 음수 클램프 · 라이브 덱 전량 리드인 보유 |

## 7. `Tests/PlayMode/*`

| 옛 테스트 파일 | 규칙 | 분류 | 코어 짝 |
|---|---|---|---|
| AbilityAreaShieldTest | 실드셔틀 배치 실드 — 반경 내 같은 진영 · 자신 제외 · 흡수 | 짝 | UnitSkillTests::실드셔틀_배치_보호막은_주변_아군에게_건다 · GrantShieldSkillTests::PositiveRadius_ShieldsAlliesExceptSelf(잔류) · ShieldMathTests::소모는_오래된_것부터다 |
| AbilityBombManBarrelTest | 폭탄맨 배치 — 최근접 적 칸에 피해 0 길막 · 적 없으면 없음 | 짝 | ProjectileBehaviorTests::길막_페이로드는_설치물을_세운다 · BlockingHazardTests::탄이_세우는_길막은_그_탄의_줄을_지난다 · EmitPatternSkillTests::NeedsAim_NoFacing_NoCandidate_FiresNothing(잔류) |
| AbilityOnPlaceBlastTest | 샷건맨 배치 — 최근접 방향 부채꼴·사거리 · 후보 없으면 없음 | 짝 | EmitPatternSkillTests::NeedsAim_NoFacing_PicksNearestOpponent · NeedsAim_CandidateOutOfRange_FiresNothing(잔류) · PatternEmissionTests::방향은_저작_각도_사이를_보간한다 |
| ActionLockTest | 수면은 피격에 깨고 기절은 유지 · 무한 수면 유지 | 짝 | EffectRulesTests::피격_기상은_수면만_풀고_기절은_안_깬다 · CombatRulesTests::지난_틱에_걸린_잠은_피격이_깨운다 · CcStateTests::무한_슬롯은_감쇠를_자연_통과한다 |
| ActiveAllyZoneTest | 아군 버프 장판 — 안에 있는 동안만 · 빈 칸 시전 성공 · 만료·이탈 시 소멸 · 배치 오라와 합산 · 겹침 비중첩 | 짝 | FieldCarrierTests::재발행_지속은_틱_델타보다_길고_나가면_곧_풀린다 · 겹친_아군_장은_깐_순서와_무관하게_가장_강한_값이다 · 수명은_이동_뒤에_깎이고_소멸_사건을_낸다. ⚠ 부분 공백: 배치 오라와 합산(별 슬롯) · 빈 칸 시전 성공 |
| ActiveMeteorTest | 운석 — 예고 뒤 반경 피해 · 탄 없으면 해결 없이 시전만 성공 | 짝 | ActiveCastTests::시전하면_이_커맨드_안에서_조준_칸의_적에게_걸린다 · ProjectileMathTests::하늘낙하는_칸과_적이_다른_바인딩이다 · PlayCore:CorePlayThreeSymptomTests::운석을_시전하면_하늘에서_떨어지는_운석이_화면에_보이고_착탄_연출이_터진다. ⚠ 부분 공백: 탄 미배선 시 동작 |
| ActiveSlowFieldTest | 감속장은 **시전 순간 스냅샷** — 나중에 들어온 적은 안 걸림 · 지속 뒤 풀림 (rules F35 결정) | 짝 없음 — 규칙 누락 의심 | 즉시 적용만 — ActiveCastTests::시전하면_이_커맨드_안에서_조준_칸의_적에게_걸린다. F35 의 본체(늦게 온 적 제외)·만료 단언 0 |
| ActiveTileCastTest | 포탈 입구=출구 거절 | 짝 | ActiveCastTests::성사가_안_되면_차감도_대기도_재활용도_없다_포탈_같은_칸 |
| ActiveTornadoTest | 회오리 — 중심으로 당겨 붙잡고 만료 시 풀기 · 시전 뒤 들어온 적도 당긴다(연속 장) | 짝 | MovementRulesTests::당김은_이동을_대체하지_않고_더해진다 · FieldCarrierTests::수명은_이동_뒤에_깎이고_소멸_사건을_낸다 |
| AttachRangePreviewTest | 부착 프리뷰 링 — 숙주 중심·카탈로그 반경 · 비공간 카드 무시 · 배치가 소유하면 양보 | 짝 | PlayCore:CoreCardViewTests::끌어서_부착하면_범위_링이_host_몸으로_재진다 · DcRangeCatalogTests(잔류) |
| BattleBridgeTestAccess | 브리지 private 접근(헬퍼) | 짝 없음 — 옛 기계 전용 | 헬퍼 |
| BeamPresentationTest | 빔 몸통이 총구↔대상으로 늘어난다 · 배치 일제 조사는 대상당 빔 1 · 드래그 경로도 · **일제 조사 중 평타 억제** | 짝 없음 — 규칙 누락 의심 | 없음. `CoreBeamPresenter` 에 PlayModeCore 테스트 0 · 뷰 표만 Assets:CardViewAssetTests::메커닉이_선언한_빔과_오라는_뷰_표에_실린다 |
| BoardLimitPlacementTest | 판 상한 — 닿으면 거부 · 죽으면 리셋 훅 없이 다시 배치 | 짝 | MatchPlacementTests::판_상한은_유닛_저작과_모드_상한을_둘_다_본다 · PlacementSlotBlockTests::소진이_쿨타임보다_먼저_답한다 (튜토리얼 저체력 신호는 결정 ④ 로 제거) |
| BoardLimitTrayStateTest | 트레이 소진 표현 — 테두리 · 소진 > 쿨타임 | 짝 | PlacementSlotBlockTests::소진이_쿨타임보다_먼저_답한다 · PlayCore:CoreCooldownDisplayTests::트레이는_남은_초를_숫자로_보여_준다 · PlayCore:CoreSelectionPanelTests::트레이_소진_칸을_누르면_그_유닛의_상세가_열린다 |
| BonusWavePullTest | 보너스 — 트리거 · 포탈 수만큼 순차 · 특수 적 · 일반 진행 무방해 · 재진입 차단 · 스트레스 게이트 | 짝 | MatchWaveTests::보너스는_일반_처치로만_쌓이고_래치된다 · 스트레스가_높으면_보너스가_안_뜬다 · 전멸_판정은_보너스_적을_세지_않는다 · BonusPullTriggerTests(잔류) · PlayCore:CoreScreenTransferTests::보너스를_당기면_포탈이_판의_시계로_열리고_닫힌다 (억제 3건은 X6 제거·결정 ④) |
| BossLullabyTest | 자장가 — 공격 사거리 밖 방어유닛만 재움 · 보스는 수면 면역 | 짝 | UnitSkillTests::마메모_자장가는_주기마다_가까운_상대를_재운다 · AreaSleepSkillTests::SkipsTargetsItWouldAttackAnyway_ButOnlyInsideItsRange(잔류) · CcStateTests::보스는_행동불능_면역이지만_버프_디버프는_받는다 |
| BossSelfBlinkTest | 경계 도약 — 방어유닛 밀집 착지 링으로 실제 순간이동 | 짝 | BossTests::증상_보스가_밀집한_곳으로_도약하고_비행이_끝나는_틱에_그_자리에_슬램이_떨어진다 |
| BossShieldTest | 꿈의 장막(경계 자기 실드) · 악몽의 가호(주기 호위 실드, 보스 제외) | 짝 | GrantShieldSkillTests::ZeroRadius_ShieldsSelfOnly · PositiveRadius_ShieldsAlliesExceptSelf(잔류) · ShieldMathTests::같은_출처는_max_다른_출처는_합산이다 |
| BossThresholdSelfAoeTest | 경계 자폭 — 경계마다 주변 방어유닛 피해 1회 | 짝 | UnitSkillTests::짱쎈_경계_자폭은_층을_안_가리고_시전자_몸만큼_넓다 · BossTests::빈사폭주류_경계_규칙은_발동_상한이_없어_경계마다_난다 · TriggerDispatchTests::체력_경계는_한_방에_여러_경계를_뚫어도_1회다 |
| BossUltimateLeapTest | 궁극기 — 이탈 중 무적 · 고정 착지점 강습 · 예고 범위 슬램 | 짝 | ProjectileBehaviorTests::궁극기_도약은_이탈_예고_강습_슬램이다 · CombatRulesTests::궁극기_이탈은_피해를_버린다 · IntentApplierTests::원자_개시_궁극기는_잠금과_무적이_함께_선다 |
| BossWhipAuraTest | 채찍 — 반경 내 같은 진영 이동 속도 증가(자신 제외) | 짝 | StatAuraSkillTests::GuardianIncludesItself_WhipDoesNot · WhipIgnoresAuthoredStat_BecauseItsNameSaysTheStat(잔류) · EffectRulesTests::감속을_건_적은_같은_판에서_느리게_간다(이속 소비 증인) |
| BountyMarkTest | 살찌운 제물 — 보상 배율·받는 피해 감소·이중 거절 · 처치/유출 분기 | 짝 | BountyMarkTests(5 전부) |
| DefenderApplyStackOutputTest | 유닛 공격 산출물 ApplyStack → 출혈 스택 → 도트 | 짝 | EffectRulesTests::스택이_쌓이면_임계가_한_번_터지고_소비형은_기준을_다시_맞춘다 · UnitSkillTests::궁수_감속_오라와_난도질꾼_출혈은_상대에게_건다 |
| DefenderRetireTest | 퇴근 — 칸 비움·재사용 · 사망 경로 안 탐 · 배치 중 거절 · 퇴근 쿨 < 사망 쿨 · 카드 회수·각성 없음 · 인수인계 · 퇴근 운석 · 작별 선물 없음 | 짝 | MatchPlacementTests::퇴근은_사망이_아니다 · 퇴근_대기는_사망_대기의_비율이고_뒤집힐_수_없다 · DestroyEventTests::퇴근한_칸은_다시_쓸_수_있다 · MatchHandDeckTests::퇴근은_각성을_주지_않는다 · 인수인계는_퇴근에서만_앞으로_당긴다 · RetireRecallTests(2) · CardRuleTests::퇴근_운석은_비워진_칸_중심에_몸_없이_떨어진다 · PlayCore:CoreScreenTransferTests::퇴근하면_유닛이_사라지지_않고_뽑혀_날아간다 |
| DioramaStagePlayTests | 스테이지 라이브 — 적이 셀을 옮기며 footprint 회피·골 전진 · 본능 4기 · 아군 본능이 적을 친다 · 마커 포탈 프랍 | 짝 | MovementRulesTests::적이_골까지_걸어간다 · MatchStructureTests::본능은_통행을_막지_않는다 · Assets:MarkerPropStyleAssetTests::Style_HasVerticalPortalProps_ForSpawnAndGoal · PlayCore:CoreSceneBootTests::Boot_NoErrors_ViewsTrackUnits_AndMatchRunsToEnd · 골든 `siege_instinct_fall`. ⚠ 부분 공백: 아군 본능이 걸어오는 적을 때린다 |
| DotAuraFromElementTest | 도트 오라는 도트의 원소를 따른다(스택 슬롯 아님) | 짝 | DotSetTests::스택_종류는_원소로_접히고_기믹_스택은_그림이_없다 · 사건이_나르는_묶음은_출처와_원소를_둘_다_되돌린다 · PlayCore:CoreEffectViewTests::상태_표식은_걸림에_켜지고_풀림에_꺼지며_숙주가_사라지면_전부_회수된다 |
| DotCoexistenceTest | 파이프라인이 다른 도트는 각자 요율·수명 | 짝 | EffectRulesTests::출혈_중인_적이_장판을_나가면_불_피해가_멈춘다 · DotSetTests::출처와_원소_둘_중_하나만_달라도_슬롯이_갈린다 |
| DraftFlowSmokeTest | 드래프트 버리기 3회 → 7장 확정 | 짝 없음 — 옛 기계 전용 | 뽑기 폴백 진입 **제거**(계약 9 · rules X17). `DraftSessionTests` 는 잔류 |
| DragCancelZoneTest | 트레이 위에서 떼면 무차감 취소 · 판정은 가상 포인터 | 짝 | PlayCore:CoreDragPreviewTests::드래그하면_실루엣이_하나_서고_손끝을_따라가며_판_밖에서_사라진다 · PlayCore:CoreArmedPlacementTests::판_밖_탭은_커맨드_없이_선택만_푼다 · PlacementPointerOffsetTests(잔류) |
| DragPlacementReachTest | 최상단 배치 행 도달 · 커밋은 배치 포인터를 따른다 | 짝 | PlayCore:CoreDragPreviewTests::판_위에서_떼면_실루엣은_사라지고_유닛은_트레이_칸에서_난다(포인터 오프셋 `:96`) · PlayCore:CorePlacementAnchorTests · PlacementPointerOffsetTests(잔류) |
| DragonBreathE2ETest | 드래곤 = 보스 태그 없는 엘리트·공중층 · 브레스 VFX 배선 | 짝 | Assets:DragonBreathAuthoringTests::Dragon_IsEliteFlyer_WithLift · UnitSkillTests::드래곤_브레스는_N타마다_콘_안의_적만_태우고_발동_사건이_콘을_싣는다 · ConeBreathSkillTests(잔류) |
| DreamCocoonTest | 호접몽 — 완주 영구 버프 · 잠 중 피격 파탄 · 이중 부착 무차감 거절 | 짝 | DreamCocoonTests::중간에_맞으면_잠이_깨는_그_틱에_고치도_깨지고_보상이_없다 · 끝까지_자면_영구_보상이다 · CardAttachTests::실패한_부착은_무차감_무순환이다_이중_상태 |
| DreamcatcherAttachRequirementE2ETest | 부착 제한(직업·유닛 id) 게이트 · 거절은 각성 무차감·손패 유지 · UI 판정 = 커밋 | 짝 | CardAttachTests::이_숙주에서_한_줄도_안_도는_카드는_거절되고_값을_안_치른다 · ApplicabilityTests::부착_제한의_무효_저작은_어디에도_안_붙는다 · 조준_preflight_와_커밋은_같은_답을_낸다 · DreamcatcherAttachEvalTests(잔류) |
| DreamcatcherCombatDamageTest | 공격력 버프가 **실제 피해**를 늘린다 · 대 CC 배율 | 짝 없음 — 규칙 누락 의심 | 대 CC 만 — CardRuleTests::파쇄의_찬가_피해_배율은_군중_제어나_지속_피해_중인_대상에게만_붙는다. `DamageMul` 버프 → 실제 피해 단언 0(EffectIntegrationTests 와 같은 구멍) |
| DreamcatcherCursedRelicTest | 마지막 불꽃은 부착 반환 시 이미 탄다 · 기존 치명 타이머면 복합 카드 통째 거절 | 짝 | CardRuleTests::마지막_불꽃은_시간이_끝나면_죽고_처치로_세지_않는다 · CardAttachTests::부착_즉시_규칙은_커맨드_콜스택_안에서_실행되고_그_뒤에_값을_치른다 · DreamcatcherAttachEvalTests::DupLethalTimer_RejectsWholeCard(잔류) |
| DreamcatcherDamagedTriggerTest | N번째 피격에 이중 발사 충전 부여 | 짝 | CardRuleTests::궁지_폭발은_체력이_게이트_아래로_내려간_피격만_센다(OnDamagedN 카운트) · AttackModTests::충전은_스킬이_부여하고_수식자가_소비한다_한_번에_전부 |
| DreamcatcherEffectTest | 카드 버프가 현재·미래 해당 유닛에 · 축 존중 · 회수 중립화 | 짝 | SquadCardTests::이후_배치되는_유닛은_배치_사건으로_상속한다_한_번만 · 축은_직업과_코스트를_가른다 · 회수는_항등_재발행이_아니라_슬롯_삭제다 |
| DreamcatcherGateE2ETest | 궁지 폭발은 게이트 안 피격만 · 처형타는 25% 이하에서만 약 2배 | 짝 | CardRuleTests::궁지_폭발은_체력이_게이트_아래로_내려간_피격만_센다 · TriggerDispatchTests::공격_N회는_대표_대상이_있는_RESOLVE_만_세고_게이트_실패는_카운트를_안_올린다(처행타 게이트 `:193`). ⚠ 부분 공백: 처형타의 피해 배율 자체 |
| DreamcatcherKillThresholdTest | 빈사 버프 · 처치 시 공속 버프(킬 귀속) · 시체 폭발·잿불 장판은 피해자 자리 · 작별 선물은 파괴 뒤 | 짝 | TriggerSymptomTests::적을_죽인_자리에서_시체_폭발이_터지고_그_시체의_몸만큼_넓다 · 부착한_규칙이_붙은_유닛이_죽으면_작별_선물이_그_자리에서_터진다_시전자가_없어도 · CardAttachTests(OnKill × SelfStatBuff) · DcSkillRoutingTests::OnKill_SpawnHazard_RoutesToDeathSiteHazard · SelfStatBuff_SplitsOnHealthThreshold(잔류) |
| DreamcatcherOnHitTest | 3타마다 기절 · 밀치기 · 출혈 스택+도트 규칙 | 짝 | TriggerDispatchTests::공격_N회는_대표_대상이_있는_RESOLVE_만_세고_게이트_실패는_카운트를_안_올린다 · Assets:CardEffectWitnessTests::카드는_붙이거나_시전하고_발동하면_그_종류의_효과가_걸린다 · StackRuleTests::같은_종류라도_저작_자산마다_줄이_갈린다 |
| DreamcatcherSleepDamageTest | 잠든 대상만 2배 · 파쇄의 찬가와 곱으로 중첩 | 짝 | AttackModTests::수면_배율은_잠든_대상에게만_붙는다 · CardRuleTests::파쇄의_찬가_피해_배율은_군중_제어나_지속_피해_중인_대상에게만_붙는다. ⚠ 부분 공백: 두 배율의 곱 중첩 |
| DropDismountTest | 하마 비행 — 핸드오프 무팝 · 활성화 = 착지 + 모션 · 배치 3종 전부 · 세션 독립 | 짝 | PlayCore:CoreDeployFlightTests::배치한_유닛은_공중을_날아와_착지한다 · 비행이_끊겨도_착지_신호는_반드시_나간다 · PlayCore:CoreFirstPlacementFlightTests · MatchPlacementTests::배치_모션이_있으면_착지_뒤에_활성화된다 |
| EffectTileBuffApplyTest | 버프 타일 3종이 저작값 그대로 점유 유닛에 | 짝 | EffectTileTests::증상_효과_타일에_놓으면_공격력이_오르고_퇴근시키면_회수된다 · Assets:BoardEffectAuthoringTests::효과_타일_개수와_종류가_시즌_맵_테마에서_실린다 · Assets:LiveDefinitionSmokeTests::효과_타일_개수는_활성_시즌_테마를_따른다. ⚠ 부분 공백: SO 효과 → 스탯 종류·연산자 매핑의 라이브 에셋 대조 |
| EnemyShieldTest | 적 스폰도 실드 그릇 쌍 · 체력 전에 흡수 · 적 머리 위 게이지 | 짝 | ShieldMathTests::소모는_오래된_것부터다 · 완전_흡수는_피격이_아니다 · IntentApplierTests::실드_도발_위협_순간이동. ⚠ 부분 공백: rules E19(적도 실드 그릇) 판 단언 · 적 오버헤드 실드 게이지 |
| GoalStabilityTest | 유출에 안정도 감소 · 0 이면 판 종료 | 짝 | MatchHeartScoreTests::돌격형은_마음을_치고_산화한다 · 첫_붕괴가_곧_판의_끝이다 · MatchStructureTests::마음이_다_깎이면_그_판이_끝난다 |
| HitscanDefenderTest | 투사체 없는 원거리 유닛은 사거리에서 직접 피해 | 짝 | CombatRulesTests::끌려간_적은_가디언만_본다(사거리 3·무탄 직접 피해 `:444`) · Assets:CoreBuilderDriftTests::근접_적은_탄이_저작돼_있어도_탄을_쏘지_않는다. ⚠ 부분 공백: 방어유닛 쪽 무탄 원거리 |
| IncubusPactTest | 몽마의 계약 — 유출 허용치 선불 | 짝 없음 — 옛 기계 전용 | 몽마의 계약 **제거 확정**(계약 9 · rules X22) |
| KindlerFireStackE2ETest | 킨들러는 레인저만 조준 · 화염 스택 5 임계 도트 | 짝 | ProjectileImbueTests::킨들러형_유닛의_탄이_스택을_건다 · SecondaryTargetingTests::직업_필터는_주_대상에서는_여전히_거른다 · StackRuleTests::임계는_올라가는_길에만_발화한다 |
| KnockupOnHitTest | 넉업은 때린 전원에게 · 배치 기절은 반경 안만 | 짝 | CombatRulesTests::넉업은_때린_전원에게_걸린다 · Assets:MalphiteKnockupAuthoringTests::KnockupHopIsShorterThanTheStun. ⚠ 부분 공백: 배치 StunNearby 반경 판 단언 |
| LegacyBattleScene | 옛 씬 로더(헬퍼) | 짝 없음 — 옛 기계 전용 | 헬퍼 |
| MovementIntegritySmokeTest | 실전투 동안 적이 걷기 타일 위 · 어그로 적이 가디언을 친다 | 짝 | NavGridAndTrimTests::막힌_칸으로_넘어가면_현재_칸_경계로_접힌다 · MovementRulesTests::군집_통과_검산_1칸_복도_20기_100초 · CombatRulesTests::끌려간_적은_가디언만_본다 |
| OnPlaceApplyStackNearbyTest | 난도질꾼 등장 — 반경 내 적에게 스택 | 짝 | UnitSkillTests::궁수_감속_오라와_난도질꾼_출혈은_상대에게_건다 |
| OnPlaceBindNearbyTest | 아처 배치 — 반경 내 적 감속, 풀리면 다시 움직임 | 짝 | UnitSkillTests::궁수_감속_오라와_난도질꾼_출혈은_상대에게_건다 · EffectRulesTests::감속을_건_적은_같은_판에서_느리게_간다 |
| OnPlaceBoostNearbyTest | 가디언 배치 — 반경 내·자신 실효 공격력 증가 | 짝 | StatAuraSkillTests::AllyAura_BuffsAllies_NotOpponents · GuardianIncludesItself_WhipDoesNot(잔류). ⚠ 부분 공백: 「실효 공격력」(DamageMul → 실제 피해 — EffectIntegrationTests 구멍) |
| OnPlaceDotNearbyTest | 버스터즈 일제 조사 — 틱당 피해 계약(총 = 크기 × 지속/간격) · 반경 안만 | 짝 | DotSetTests::연속_지속_피해는_값이_DPS_다 · 새로_걸린_지속_피해는_진입_즉시_1회_준다. ⚠ 부분 공백: 틱 간격 > 0 일 때 scalar = 틱당 피해 |
| OnPlaceForwardProjectileTest | 전방 관통 — 조준 없으면 최근접 적(남쪽 고정 금지) · 판 밖 적이 총구를 못 뺏음 · 사거리 밖 무피해 | 짝 | EmitPatternSkillTests::NeedsAim_NoFacing_PicksNearestOpponent · NeedsAim_CandidateOutOfRange_FiresNothing · NeedsAim_DeadCandidate_IsNotAimedAt(잔류) · SkillAimTests(잔류) |
| OnPlaceGainCostTest | 스카우트 — 저작량만큼 코스트 · 가득이면 안 넘침 | 짝 | MetaSkillsTests::GainCost_EmitsExactlyTheAuthoredAmount(잔류) · IntentApplierTests::메타_의도_둘은_판_자원_담당자로_간다 · MatchCostTests::획득은_상한을_넘지_않는다 |
| OnPlaceMeleeBurstTest | 브루저 배치 폭발 — 반경 내·도달 가능 층만 정확히 1회 · 퇴화 저작 무동작 | 짝 | SkillRoutingTests::몸에서_나오는_것_열은_SelfArea_다 · UnitSkillTests::짱쎈_경계_자폭은_층을_안_가리고_시전자_몸만큼_넓다(같은 concrete). ⚠ 부분 공백: **층 제한(도달 가능 층만)** 변형 — 코어 증인은 층을 안 가리는 짱쎈뿐 |
| OnPlaceReduceSkillCooldownTest | 레인저 — 진행 중 쿨다운 단축 · 준비에서 바닥 | 짝 | MetaSkillsTests::ReduceCooldown_EmitsExactlyTheAuthoredAmount(잔류) · IntentApplierTests::메타_의도_둘은_판_자원_담당자로_간다. ⚠ 부분 공백: 준비 바닥 |
| OnPlaceRuleTriggerTest | 탭·드래그 두 배치 경로 모두 배치 규칙 발화 · 규칙 없는 유닛 무슬롯 · 엣지 1틱 소비 | 짝 | TriggerSymptomTests::배치하면_배치_스킬이_다음_틱의_주기_seam_에서_곧바로_난다 · PlayCore:CorePlacementFlowTests::배치_커맨드는_receipt_와_뷰_스폰으로_이어진다 · PlayCore:CoreArmedPlacementTests::트레이_탭은_집어_들고_판_탭은_그_칸에_놓는다 (두 경로가 한 커맨드로 수렴) |
| OnPlaceSkyStrikeTest | 캐논 — 반경 안 적마다 1발 · 저작 피해 · 같은 칸 둘도 각 1발 · 시차 · 예고 중 이동해도 맞음 | 짝 | TriggerSymptomTests::캐논_융단폭격은_미사일_수가_반경_안_적_수다 · 융단폭격의_시차는_칸마다_예고_시간에_얹힌다 · 손잡이가_꺼진_명세는_한_발이다 · Assets:CoreTriggerEnumPinTests::캐논_배치_스킬은_한_발이_반경_안_전원에게인_명세를_가리킨다 · ProjectileMathTests::하늘낙하는_칸과_적이_다른_바인딩이다 |
| OnPlaceStunNearbyTest | 말파이트 배치 — 반경 안 적 실제 정지 · 풀리면 움직임 · 반경 안 피해 | 짝 | EffectRulesTests::행동_잠금은_START_만_막고_쿨다운은_계속_돈다 · IntentApplierTests::군중_제어는_면역_관문을_지나고_감속_토큰은_말한다 · PlayCore:CoreShapeGuideTests::지상_전용_말파이트를_끌면_비행_적에_마크가_안_켜지고_가이드도_그쪽을_안_본다. ⚠ 부분 공백: 배치 기절 반경·정지·만료 판 단언 |
| OnPlaceTauntNearbyTest | 배스티온 — 수용량 넘어 반경 전원 도발 · 도발된 적이 걸어온다 · 만료 해제 · 비행은 안 끌림 | 짝 | AreaTauntSkillTests::CallsEveryOpponentInRange · UnreachableLayer_IsNotCalled · BastionBody_WidensTauntShapeTo3_5(잔류) · DetectionRulesTests::어그로는_수용량을_넘지_않고_도발은_그것을_우회한다 · 도발_시한이_지나면_풀린다 · CombatRulesTests::끌려간_적은_가디언만_본다 |
| PatrolDefenderPlayTest | 소환사 1기 순찰·재소환 · 소환사 사망/퇴근 시 순찰병·뷰 제거 | 짝 | SummonPatrolTests::소환물이_죽으면_즉시가_아니라_남은_쿨이_다_돈_뒤에_다시_나온다 · CombatRulesTests::소환사가_죽으면_순찰병도_소멸_사건을_낸다 · PlayCore:CoreViewRemainderTests::행6_소환물이_살아있는_동안_소환사_뷰에_유지중을_밀고_유지_루프를_튼다. ⚠ 부분 공백: 소환사 **퇴근** 시 순찰병 제거 |
| PlacementAuraTest | 배치 오라 — 신규 배치만 · 축 존중 · 숙주 회수/사망 시 원복 | 짝 | PlacementAuraTests::이미_있는_유닛과_숙주는_안_받고_새로_배치된_유닛만_받는다 · 숙주가_죽으면_공속은_소급_회수되고_수면은_안_걷힌다 · SquadCardTests::축은_직업과_코스트를_가른다 |
| ProjectileApplyStackAccumulatesTest | 탄이 건 스택이 한 슬롯에 누적(출처 = 사수) | 짝 | ProjectileImbueTests::킨들러형_유닛의_탄이_스택을_건다 · StackRuleTests::스택은_출처와_종류_2축으로_갈린다 |
| ProjectileVisualSmokeTest | 탄 뷰 — 착탄 재생 후 풀 반납 · 발사 앵커 · 앵커 없으면 투영 위치 | 짝 | PlayCore:CorePlayThreeSymptomTests::운석을_시전하면_하늘에서_떨어지는_운석이_화면에_보이고_착탄_연출이_터진다(`CoreProjectileViewPool` `:67`). ⚠ 부분 공백: 풀 반납 · 발사 앵커 |
| RangePredicateMirrorTest | 교착 카나리아 — 멈춘 적은 결국 쏜다 | 짝 | CombatRulesTests::비행_적은_지상을_걷는_아군을_멈춰_서서_때린다 · DetectionRulesTests::유한_감지는_방어유닛_앞에서_멈춘다 · AttackReachTests 헤더(정지와 공격이 같은 자). ⚠ 부분 공백: 위치 스윕 카나리아 |
| RelocationMoveModeTest | 재배치 이동모드 | 짝 없음 — 옛 기계 전용 | 재배치 **은퇴**(README 고지 ⑴) |
| RelocationPlacementSessionTest | 재배치 세션 | 짝 없음 — 옛 기계 전용 | 재배치 **은퇴** |
| RelocationSmokeTest | 재배치 시뮬 토대 | 짝 없음 — 옛 기계 전용 | 재배치 **은퇴** |
| ShieldBreakSkillLayerTest | 실드 파열 — 폭발·수면 두 payload 가 스킬 레이어로 | 짝 | CardRuleTests::실드가_깨지는_그_피격에서만_파열_규칙이_한_번_터진다 · SkillRoutingTests::트리거별_분기_일곱은_폴백과_다른_concrete_로_간다 · AreaSleepSkillTests(잔류) |
| SkillLayerRemainingPayloadsTest | 불꽃 회전체 궤도탄 · 진동갑주 임계 폭발 | 짝 | PlayThreeSymptomTests::진동갑주를_단_유닛이_적에게_맞아_체력_30퍼센트_밑으로_가면_주변이_터진다 · IntentApplierTests::탄_두_갈래_대상탄과_자리_폭발 · Assets:CardEffectWitnessTests::카드는_붙이거나_시전하고_발동하면_그_종류의_효과가_걸린다 |
| SkillLayerRoutingTest | 이전된 스킬이 실제로 concrete 를 탄다 | 짝 | SkillRoutingTests::트리거별_분기_일곱은_폴백과_다른_concrete_로_간다 · CoreArchitectureTests::스킬_경로의_쓰기는_IntentApplier_한_표면을_지난다 |
| SlimeSplitE2ETest | 슬라임 두 번 분열·사슬 종료 · 마지막 슬라임 처치가 웨이브를 안 넘긴다 | 짝 | SplitTests::증상_슬라임을_잡으면_그_칸에서_자식이_퍼진다 · 부모만_죽은_틱에도_필드는_비지_않는다_자식이_전멸_판정_앞에_태어난다 · Assets:SlimeSplitAuthoringTests::SplitChain_Terminates_WithoutCycle |
| SpawnGuideMatchesWalkTest | 예고선 = 실제 이동선 · 레인 기본 경로 | 짝 | PlayCore:CoreViewYardstickTests::예고선의_경로는_이동과_같은_함수에서_나온다 · 예고선은_거점을_스스로_고르지_않는다 |
| SpriteUnitBackendPlayTest | 스프라이트 세트 유닛이 Spine 과 같은 판에서 뜨고·픽킹·공격 모션·사망 시 풀 제거(fake-null 가드) | 짝 없음 — 규칙 누락 의심 | 없음(뷰). `CoreSpriteUnitView`(`CoreUnitViewPool.cs:208`) 에 PlayModeCore 테스트 0 · 잔류 FlipbookCharacterViewTests·SpriteFlipbookPlayerTests 는 부품만 |
| StructureLivePlayTest | 거점 라이브 — 점유·통행 비차단·연결성 · 본능이 이웃 배치 안 막음 · 공성 맵 적 거점 | 짝 | MatchStructureTests::본능은_통행을_막지_않는다 · 스폰_골_본능_자리에는_못_놓는다 · 본능의_죽음은_보통_유닛의_죽음이다 · 골든 `siege_instinct_fall` (적 마음 파괴 = 승리는 X23 제거) |
| TallyFlowTest | 전투 종료 → 결과 화면 · 총점 보존(하드락 방지) | 짝 | PlayCore:CoreMatchOutcomeTests::판이_끝나면_결과_화면이_한_번_뜬다 · MatchGoalTests::성적의_조립_지점은_하나다 · PlayCore:CoreGamePhaseTests |
| TestPlacement | 즉시 배치 헬퍼 | 짝 없음 — 옛 기계 전용 | 헬퍼 |
| UnitOverheadUiLifecycleTest | 머리 위 UI — 스프라이트 공유·바만 페이드·소멸 뷰 정리 · 레거시/통합 모드 배타 | 짝 | PlayCore:CoreCardViewTests::탭_부착이면_카드_줄이_서고_퇴근하면_거둔다 · Assets:CoreViewRemainderAuthoringTests::틴트_통합_머리위_모드와_스타일_없음은_흰색이다 · UnitOverheadLayoutTests(잔류). ⚠ 부분 공백: 소멸 유닛 바 정리 |
| WavePullCapTest | 당김 상한 — 넘으면 거부 · 기제는 뚫림 · 전멸로 회복 · 타임아웃으론 회복 안 됨 | 짝 | MatchWaveTests::당김은_상한이_있고_전멸로만_회복된다 · 당김_상한을_다_쓰면_거절된다 · 기제층_당김은_상한을_무시한다 · 당김_상한_0은_금지가_아니라_폴백이다 (판 재시작 리셋은 X20 — 판을 새로 조립) |
| WaypointRoutingLiveTest | 경유점 순서 통과 · 레인 기본 경로 · dev 슬롯 생성 웨이브 | 짝 | MovePureMathTests::인접_칸이면_지났다 · 경로_선택은_좁은_쪽이_이긴다 · MapRuntimeTests::경유점과_거점이_슬롯이_된다 · Assets:MatchEntryBuildTests::저작_플랜은_제_시계로_돈다_0_은_끝없음 (튜토리얼 dev 슬롯은 결정 ④) |
| WhirlpotLiveRepro | 회오리 — 베이크 · 인접 피해 · 걸어와 교전 · 공격당 VFX 폭 = 판정 반경 · 저작 DPS 유지 | 짝 | Assets:WhirlpotAuthoringTests::Whirl_IsTheBaseAttack_MeleeAoe_WithNoSeparateSingleHit · AttackVfxPrefab_IsWiredOnWhirlpot_AndOnNoOtherEnemy · SecondaryTargetingTests::적의_부가_타격은_가까운_순이다. ⚠ 부분 공백: VFX 폭 = 판정 반경 |

---

## 규칙 누락 의심 — 모아 보기 (34)

**전투 판정·타겟팅**
1. EditMode/AggroAoeWidthTests — 물린 광역 적도 이웃을 친다
2. EditMode/AttackCommitTests — 선딜 중 겨눈 대상을 때린다(A 겨누고 B 때리기 금지)
3. EditMode/AttackShapeGateTests (+ Assets/AttackReachParityTests 은퇴로 커짐) — 띠 게이트 0 · 부채꼴 경계
4. EditMode/AttackSystemStateGateTests — 적은 Engaging·Standoff 에서만 쏜다
5. EditMode/DefenderLockTests — 방어유닛 지속 락
6. EditMode/NearestLockTests — Nearest 적 락
7. EditMode/EnemyBehaviorTests — FocusUntilDead
8. EditMode/TargetPersistenceTests — Focus 적 이탈 전환(판)
9. EditMode/EnemyTargetPriorityTests — `PriorityClass`
10. EditMode/ProjectileRetargetAndBounceTests — 탄 재조준·관통 소진 튕김(수명 글루)
11. Assets/DirectionalVolleyIntegrationTests — 연발 중 다음 트리거 연기 · CC 중 완주 · 선딜 중 대상 상실 발사

**효과·스탯**
12. EditMode/EffectIntegrationTests — `DamageMul`·`AttackSpeedMul` 이 실제 피해·주기에 곱해진다
13. PlayMode/DreamcatcherCombatDamageTest — 같은 구멍(공격력 버프 → 실제 피해)
14. PlayMode/ActiveSlowFieldTest — F35 감속장 스냅샷(늦게 온 적 제외)·만료

**감지·이동**
15. EditMode/DetectionSystemTests — 관성 1초 · 막힘 2초 해제 · 어그로 중 감지 안 함 · simId 동률
16. EditMode/DetectionChaseFieldTests — 우회 선택·우회 상한·비행 벽 너머·다음 후보
17. EditMode/DetectionLeakProofTests — 유한 감지 적의 공성 전환
18. EditMode/HuntCloseInLockTests — 사냥 접근 보정 게이트 · C10 salvage
19. EditMode/PatrolLayerRoutingTests — 순찰 판-경로가 층 마스크를 쓴다

**웨이브**
20. EditMode/WaveConceptGenerationTests — 컨셉 경로 전체
21. EditMode/WaveConceptMathTests — 컨셉 선택(Pick)
22. EditMode/WaveConceptVariantTests — 변주
23. EditMode/WaveConceptBossTests — 보스 × 컨셉
24. Assets/WaveConceptAuthoringTests — 라이브 덱 × 코어 생성기
25. Assets/WaveKillBudgetPinTests — 라이브 덱 × 코어 생성기 구조 불변식
26. EditMode/SpawnAlertForecastTests — `WaveScheduler.CollectForecast`
27. EditMode/WaveSpawnForecastTests — 같은 구멍(입구별 예보·레인 회전)
28. EditMode/WaveNominalIntervalTests — 명목 트리거 간격(표시용·경미)

**덱**
29. EditMode/SkillLoadoutControllerTests — `CoreDeckComposition.FilterHiddenSkills` 단언 6 이사 누락

**뷰(남는 코드의 테스트가 같이 지워짐)**
30. EditMode/BoardSpaceTests — `BoardSpace` 셀↔월드 권위
31. EditMode/TilemapMapViewTests — `TilemapMapView` 페인트 정합
32. EditMode/ProjectileVariationTests — `CoreProjectileViewPool.ApplyHueShift`
33. PlayMode/BeamPresentationTest — `CoreBeamPresenter` 빔 · 일제 조사 중 평타 억제
34. PlayMode/SpriteUnitBackendPlayTest — `CoreSpriteUnitView` e2e · fake-null 가드

## 부분 공백 — `짝` 이지만 하위 규칙 테스트를 못 찾은 것 (unit 9b 입력)

> unit 9b 입력 — 규칙 문장을 코어 하네스(`BattleMatch`·정의표·`EffectWitness`/`CardProbe`)로 다시 쓴다. 옛 ECS 배선을 옮기지 않는다.
> **76 행**(51 파일 — 한 파일에 공백이 여럿이면 하위 규칙마다 한 행으로 풀어 51 을 넘는다: 5 묶음 30 파일 → 51 행 · 6 묶음 2 → 3 · 7 묶음 19 → 22) · **규칙 누락 1** · **규칙 다름 의심 1** · **해소 4**(지도 뒤 이식 3 · 기존 테스트 재확인 1) · 이식 대상 아님 3. 나머지 67 행은 규칙이 코어에 있고 테스트만 없다.
> 조사 2026-09-25 · 워크트리 `wassup-core`. 위치 경로 접두 생략 = `Scripts/BattleCore/`, `Unity:` = `Scripts/BattleCoreUnity/`, `Data:` = `Scripts/Data/`. 옛 테스트 본문은 `git show 7482f7ba6:Assets/_Project/Tests/<lane>/<파일>.cs`.

| # | 옛 테스트(파일::테스트명) | 증언하는 규칙(한 줄, 게임 언어) | 코어의 규칙 위치(파일:줄) | 비고 |
|---:|---|---|---|---|
| 1 | AggroStateSystemTests::Preemption_SameTick_FirstGuardianWins · Preemption_AcrossTicks_KeepsFirstGuardian | 한 가디언에게 이미 물린 적은 다른 가디언이 때려도 넘어가지 않는다(먼저 문 쪽 유지) | Phases/AiMovePhase.cs:200 | |
| 2 | AggroStateSystemTests::Taunt_Refresh_KeepsTheLongerRemainder | 같은 적에게 도발을 다시 걸면 남은 시간은 긴 쪽이 남는다 | Phases/AiMovePhase.cs:235 | ~~**규칙 누락**~~ **9c 실현**(`356596355`) — `AiMovePhase.cs:239` `max` · DetectionRulesTests::도발을_다시_걸면_남은_시간은_긴_쪽이_남는다 |
| 3 | AggroStateSystemTests::AirEnemy_ChaseFieldUsesAirLayer_AcrossGroundWalls | 끌려가는 비행 적은 지상 벽을 넘어 공중 길로 간다 | Phases/AiMovePhase.cs:941 (`BuildChase` 가 자기 통행 층 nav 로 굽는다) | |
| 4 | AggroStateSystemTests::ChaseField_UnreachableEnemy_Refused | 가디언까지 갈 길이 없는 적에게는 어그로가 붙지 않는다(도발도) | Phases/AiMovePhase.cs:224 · :950 | |
| 5 | AttackReachTests::DerivedBody_IsHalfWidth_ColumnsOnly | 방어유닛 몸 반경 = 가로 칸 수 / 2(세로는 무관) | Data:DefenderUnitData.cs:80 → Unity:MatchDefinitionBuilder.cs:379 | 코어 밖 저작 파생 — 코어는 정의표 값을 받기만 한다 |
| 6 | AttackReachTests::StructureBody_IsHalfFootprint_AndDiameterMatchesFootprint | 거점 몸 = 점유 칸 수 / 2(마음 0.5 · 본능 1.5) | Map/MapSnapshot.cs:250 · World/BattleWorld.cs:384 | |
| 7 | AttackShapeSelectionTests::Guardian_AggroPath_UsesTheSameShapeRule | 가디언이 어그로로 대상을 고를 때도 부가 타격은 주 대상 방향 도형 안에서만 | Combat/AggroTargeting.cs:84 | |
| 8 | AttackSystemUnifiedLoopTests::Melee_PokeNeedle_Fires_Needle_Carrier_On_Fifth_Attack · BombThrower_PokeNeedle_FiresOnFifthBombWithSelfChosenTarget · BombThrower_PokeNeedle_DoesNotCountWhenBombCannotLaunch | 비수 카드: 5번째 공격마다 비수 1발 · 못 쏜 공격은 안 센다 · 비수 피해는 공격력 버프와 무관한 고정값(C5) | Trigger/TriggerDispatcher.cs:307 (N회 카운터) · Trigger/IntentApplier.cs:256 (flat) | `CardProbe` 로 쓸 대상 |
| 9 | AttackSystemUnifiedLoopTests::U3c_ProjectileDefender_DefersKnockbackToImpact | 탄을 쏘는 유닛의 넉백은 쏠 때가 아니라 탄이 맞을 때 일어난다 | Phases/CombatPhase.cs:889 → Phases/TickProjectilePhase.cs:856 | |
| 10 | AttackSystemUnifiedLoopTests::CastEvent_PokeNeedle_FiresOnFifthCastWithNearestTarget · CastEvent_DropsStaleCasterWithoutThrowing | 캐스터의 장판 시전이 공격 1회로 센다 | — | 이식 대상 아님 — README 계약 9 제거 확정(캐스터 4기 + 캐스트 기계) |
| 11 | Bezier3Tests::ControlPoints_AlternateSides_ByShotIndex · ControlPoints_SwingWidensWithIndex | 휘는 탄은 발 번호대로 좌우를 번갈아 돌고, 뒤 번호일수록 크게 벌어진다 | Combat/Projectile/Trajectories.cs:158 · :159 | |
| 12 | BlinkMathTests::Landing_DesiredBlocked_PicksFirstRowMajorRingNeighbor · Landing_OutOfBoundsDesired_StillFindsInGridCell | 도약 착지 칸이 막혔으면 가까운 고리부터 행 우선으로 갈 수 있는 칸에 내린다 | Trigger/CoreSkillContext.cs:295 (`LandingMath.TryLandingCell`) | |
| 13 | BlinkMathTests::Landing_AllBlockedWithinCap_ReturnsFalse_Terminates · Landing_RingZeroCap_OnlyChecksDesired | 상한 고리 안에 내릴 칸이 없으면 도약을 건너뛴다 | Trigger/CoreSkillContext.cs:302 | |
| 14 | BlinkMathTests::OffsetDest_NormalCase_LandsOneTileBeyondLeader · OffsetDest_DegenerateDirection_FallsBackToConstantAxis_NoNaN | 리더 한 칸 너머 목적지 · 방향이 없어도 NaN 없음 | — | 이식 대상 아님 — 도약 목적지가 「위협 리더 너머」에서 「상대 밀집 칸」으로 바뀌어 오프셋 계산이 없다(Trigger/IntentApplier.cs:89 주석) |
| 15 | BonusWaveScheduleTests::포탈은_순번대로_번갈아_배분된다 · 포탈별_총수는_전체를_나눠_가진다 · 링_인덱스는_포탈_안에서_0부터_증가한다 | 보너스 적은 포탈에 순번대로 번갈아 나오고, 안 나눠떨어지면 앞 포탈이 하나 더 | Wave/BonusWaveSchedule.cs:32 · :39 · :47 | |
| 16 | BonusWaveScheduleTests::스폰_시각은_첫스폰_기준_등차다 · 같은_입력은_두_번_불러도_같은_결과다 | 스폰 시각 = 첫 스폰 + 전체 순번 × 간격(같은 입력 = 같은 결과) | Wave/BonusWaveSchedule.cs:38 | |
| 17 | BonusWaveScheduleTests::잘못된_입력은_빈_배열이다 | 포탈·적 수가 0 이하면 아무도 안 나온다(던지지 않는다) | Wave/BonusWaveSchedule.cs:27 | |
| 18 | BoomerangBakeAndDrainTests::Bake_DirectionBinding_RejectsZeroFlightDistance · Bake_DirectionBinding_RejectsSilentlyUselessAuthoring | 방향으로 쏘는 탄 카드의 비행거리·굵기·속도가 0 이면 굽기에서 거절 | Unity:CardDefinitionBuilder.cs:315 | Unity 층 굽기 — EditModeAssets 몫 |
| 19 | BoomerangBakeAndDrainTests::Bake_CellBindingProjectile_IsRejected_NotSilentlyDroppedAtOrigin | 칸에 떨어지는 탄은 대상 조준 카드에 못 싣는다(보드 원점 낙하 방지) | Unity:CardDefinitionBuilder.cs:313 | Unity 층 굽기 |
| 20 | DeadCasterFactionTests::LifecycleSeam_DeadEnemyCaster_TargetsDefenders | 죽은 적이 남긴 스킬은 방어유닛을 친다(시전자 진영 스냅샷) | Trigger/TriggerDispatcher.cs:497 → Trigger/IntentApplier.cs:233 | 방어유닛 쪽만 증언됨 |
| 21 | DefenderDensityTests::TiesResolveToLowestRowMajorKey | 밀집도가 같으면 행 우선 칸 키가 작은 칸 | Trigger/CoreSkillContext.cs:279 | |
| 22 | DefenderDensityTests::ResultIsIndependentOfInputOrder | 입력 순서가 바뀌어도 같은 칸 | Trigger/CoreSkillContext.cs:278 (칸 키 비교 — 순회 순서 불의존) | |
| 23 | EnemyAiStateSystemTests::Focus_LockOutOfRange_OtherNear_ReleasesAndEngages | 문 대상이 사거리를 벗어나고 다른 방어유닛이 가까우면 놓고 교전한다 | Phases/CombatPhase.cs:597 · Phases/AiMovePhase.cs:174 | **해소** — RetiredTargetLockPortTests::집중_적은_문_대상이_이탈해도_대체가_있으면_교전_상태다 · 집중_적은_문_대상이_이탈하면_사거리_안의_다른_방어유닛을_때린다 |
| 24 | EnemyTierBakeTests::EliteWithMechanic_GetsSlot_ButNoBossAttachments | 엘리트는 규칙 슬롯은 받되 보스 면역(군중 제어·어그로)은 안 받는다 | Unity:CombatDefinitionBuilder.cs:290 → Phases/AiMovePhase.cs:196 | |
| 25 | FlowFieldSingletonTests::IsGoalCell_GoalsSet_TrueForEachGoal_FalseOtherwise · IsGoalCell_DuplicateGoals_Harmless | 골이 여럿이면 어느 골 칸에 닿아도 도착이다 | Map/MapSnapshot.cs:128 | 미생성·빈 골 폴백 2종은 옛 싱글턴 기계 — 이식 제외 |
| 26 | GoalProjectileTests::TileAoe_DefenderFaction_IncludesGoal · TileAoe_EnemyFaction_IgnoresGoal · TileAoe_EnemyFaction_IncludesEnemyStructures · TileAoe_DefenderFaction_ExcludesEnemyStructures | 광역 탄은 쏜 쪽의 상대 진영만(거점 포함) 맞힌다 — 적 광역은 우리 마음을, 우리 운석은 적 마음·본능을 | Combat/TargetDefaults.cs:19 · :21 → Phases/TickProjectilePhase.cs:1016 | |
| 27 | GoalProjectileTests::TileAoe_BlockingHazard_IsVictimOfNeitherPool | 길막(방벽)은 어느 쪽 광역에도 안 맞는다 | Combat/TargetDefaults.cs:19 | ~~**규칙 다름 의심**~~ **9c 실현**(`ea73d1ddd`) — `TickProjectilePhase.cs:1067` `IsAreaLegal`(칸 광역·스플래시·스윕·튕김 후보에서 방벽 제외, 직격 유지) · ProjectileBehaviorTests 3 |
| 28 | HealAppliedEventTests::RegenOnly_DoesNot_Enqueue_HealApplied | 초당 재생은 회복 연출 사건을 안 낸다 | Phases/CombatPhase.cs:1286 | |
| 29 | HealAppliedEventTests::MultiPulse_IncomingHeal_Sums_Into_Single_Event | 한 틱에 회복이 여러 번 들어와도 사건은 합산 1건 | Phases/CombatPhase.cs:1257 | |
| 30 | ModifierMathTests::Override_WinsOverAddAndMul_ButStillClamped | 강제 고정 버프는 가산·곱셈을 무시하되 스탯 경계로 잘린다 | Effects/ModifierMath.cs:45 | 부분 — ModifierSetTests::회수는_항등값_재발행이_아니라_슬롯_삭제다 가 단독 Override 만(동거·클램프 0) |
| 31 | MovementImpulseAcrossStatesTests::Standoff_StillTakesKnockback · ChasingLocked_StillTakesKnockback · Chasing_StillTakesKnockback · EngagingHalt_StillTakesKnockback · EngagingHalt_Authored_StillTakesKnockback · PatrolIdle_StillTakesKnockback | 대치·추격·교전 정지·순찰 대기 중인 적도 넉백에 밀린다 | Phases/AiMovePhase.cs:544 (외력 합성 1곳) → :549 이하 상태 분기 | |
| 32 | PathHitRehitCooldownTests::Boomerang_Knockback_PushesOutboundThenPullsBack | 부메랑은 가는 길에 밀고 오는 길에 당긴다 | Phases/TickProjectilePhase.cs:965 | |
| 33 | PatrolAreaMathTests::OnFiringCell_ButPhysicallyTooFar_KeepsClosing · OnFiringCell_AndPhysicallyClose_Stops · OnFiringCell_TargetBodyClosesTheGap_Stops · OnFiringCell_TooFarDiagonally_ClosesOnDominantAxis | 순찰병은 사격 칸에 서도 몸 거리가 멀면 계속 다가간다(C10) | Move/PatrolAreaMath.cs:78 | **해소** — RetiredDetectionMovePortTests::순찰병은_사격_칸이어도_몸_거리가_멀면_계속_다가간다 외 3 |
| 34 | PatrolAreaMathTests::Wall_Split_Box_Falls_Back_To_Anchor_Instead_Of_Sticking | 벽이 가른 구역에서 갈 수 없는 적이면 붙지 않고 집으로 간다 | Move/PatrolAreaMath.cs:71 | |
| 35 | PatrolAreaMathTests::Unreachable_Nearest_Enemy_Does_Not_Hide_A_Reachable_One | 가장 가까운 적이 못 가는 곳에 있어도 갈 수 있는 다른 적은 쫓는다 | Move/PatrolAreaMath.cs:93 | |
| 36 | PatrolSystemIntegrationTests::Patrol_On_Goal_Cell_Does_Not_Get_PastGoalTag | 순찰병은 골 칸에 서도 골 도착으로 치지 않는다 | Phases/AiMovePhase.cs:592 | |
| 37 | PlacementCooldownRuntimeTests(rules E22 — 옛 파일에 전용 단언 없음 · 가까운 것 StartCooldown_Restarts_To_Full_On_Replace) | 같은 유닛 2기를 놓았을 때 재배치 대기는 긴 쪽이 남는다 | Owners/PlacementService.cs:610 | |
| 38 | ProjectileEmitterIntegrationTests::DeadHost_DoesNotStartNewBurst | 죽은 유닛은 새 연발을 시작하지 않는다 | Phases/CombatPhase.cs:161 (→ `FirePatterns` :917) | |
| 39 | ProjectileSystemTests::Hit_EmitsOwnerKnockback_OppositeVictimTravel | 탄에 맞은 적은 자기가 가던 방향의 반대로 밀린다 | Phases/TickProjectilePhase.cs:1055 | |
| 40 | ProjectileSystemTests::Hit_Splash_Damages_Neighbors_Excluding_Direct_Target_And_Non_AttackUnit | 스플래시는 직격 대상을 한 번 더 때리지 않는다 | Phases/TickProjectilePhase.cs:867 | |
| 41 | ProjectileSystemTests::PathOnly_Projectile_DirectAndSplash_DoNotDamageAir · PathOnly_TileAoe_DoesNotDamageAirInImpactRange | 길 전용 탄은 직격·스플래시·칸 광역 어느 것으로도 공중 적을 못 맞힌다 | Phases/TickProjectilePhase.cs:1018 | 부분 — RetiredCombatRulePortTests::길_전용_방향탄은_공중_적을_지나쳐_길_적만_맞힌다 · 길_전용_탄의_재조준은_더_가까운_공중_적을_무시한다(방향탄·재조준만) |
| 42 | RangePredicateInvariantsTests::Range_IsMonotone_LongerNeverLosesTargets · IsSymmetric_AcrossTheGrid · SelfIsAlwaysInReach · FartherAlongAnAxis_NeverComesBackIntoReach | 사거리가 길면 잃는 대상이 없고, 판정은 양방향 같으며, 멀어지면 다시 들어오지 않는다 | Combat/AttackReach.cs:45 | 스윕 단언 |
| 43 | SkyFallTests::FallProgress_PortionOne_IsIdentity · FallProgress_WaitWindow_IsZero · FallProgress_FallWindow_Ramps · FallProgress_AtImpact_IsOne · FallProgress_ZeroPortion_GuardsDivide · FallProgress_MinAuthoredPortion_ReachesOneAtImpact | 운석은 대기 창 동안 하늘에 머물다 낙하 창에 떨어져 착탄 순간 땅에 닿는다 | Combat/Projectile/Trajectories.cs:49 | 뷰 전용 재매핑(착탄 시각은 불변) |
| 44 | SpawnBlockingHazardTests::Spawn_Rejects_DefenderFootprint_Overlap | 방어유닛이 선 칸에는 길막을 못 세운다 | World/BlockerSpawn.cs:103 | |
| 45 | UnitLifecycleSystemTests::Does_Not_Enqueue_When_Singleton_Absent(rules E18) | 받아 줄 곳이 없으면 골 도착 표식을 찍지 않는다(유령 적 방지) | Phases/AiMovePhase.cs:598 → Owners/HeartMeter.cs:210 | 구조로 성립 — 받는 쪽(`HeartMeter`)이 `BattleMatch` 조립에 항상 있다. 단언할 「받는 자 없음」 상태가 코어에 없다 |
| 46 | WaveCountRampTests::NoJitter_IsMonotonicNonDecreasing_AndSaturatesAtCap(단조) · ReachableBand_ActuallyGrows | 웨이브가 지날수록 적 수는 줄지 않는다 | Wave/WaveGenerator.cs:400 | |
| 47 | WaveCountRampTests::NoJitter_IsMonotonicNonDecreasing_AndSaturatesAtCap(포화) · TwoPhase_Climax_GrowsExponentiallyFromBreakUnits_AndSaturates | 적 수는 상한에서 멈춘다 | Wave/WaveGenerator.cs:415 | |
| 48 | WaveCountRampTests::StaysWithinBounds_ForAnyJitter · JitterShiftsAroundCenter · JitterSurvivesAtCap | 흔들림은 중심 ±폭이고 상한 근처에서도 살아 있으며 범위를 넘지 않는다 | Wave/WaveGenerator.cs:416 | 부분 — WaveGeneratorTests::웨이브_수와_수량은_저작_범위_안이다(한 시드) |
| 49 | WaveEligibilityGateTests::AllGatedPoolFailsOpenInsteadOfEmptyWave | 풀의 적이 전부 등장 게이트에 걸리면 빈 웨이브 대신 게이트를 연다 | Wave/WaveGenerator.cs:486 | |
| 50 | WavePerTypeCapTests::CapOnA_TrimsAndGivesRemainderToUncappedB · CapOnB_TrimsAndGivesRemainderToUncappedA · RemainderFillsPartialRoomOnTheOtherCappedSide | 상한에 잘린 몫은 여유 있는 쪽으로 넘어간다 | Wave/WaveGenerator.cs:431 | |
| 51 | WavePerTypeCapTests::BothCapped_TotalShrinks_RatherThanOverflowing · CountsBelowCap_AreNotInflatedToTheCap | 둘 다 상한이면 총량이 줄고, 상한 아래는 부풀리지 않는다 | Wave/WaveGenerator.cs:429 | |
| 52 | AuthoredTargetMaskTests::Resolve_Unauthored_FallsBackToDefaultMask · Resolve_Authored_IsRespectedVerbatim · DefenderResolve_Unauthored_FallsBackToEnemyUnit · DefenderResolve_Authored_IsRespectedVerbatim | 대상 진영 미저작 = 상대 진영 전부, 저작하면 그대로 | Combat/TargetDefaults.cs:25 · :28 | 순수 단언 |
| 53 | WaveSpawnLeadInTests::Generate_ClampsNegativeLeadInToZero | 음수 리드인은 0 으로 접힌다 | Wave/WaveGenerator.cs:249 | |
| 54 | WaveSpawnLeadInTests::EveryShippedDeck_CarriesALeadIn | 라이브 덱은 전부 리드인을 저작한다 | Unity:MatchDefinitionBuilder.cs:549 (물질화만) | 저작 단언 — EditModeAssets 몫 |
| 55 | ActiveAllyZoneTest::Zone_StacksOnTopOfPlacementAura_AndOverlapDoesNotStack(합산) | 아군 버프 장판은 배치 오라와 다른 칸이라 둘이 더해진다 | Phases/FieldPrepPhase.cs:327 (`SlotKind.AllyField`) | 겹침 비중첩은 FieldCarrierTests 가 증언 |
| 56 | ActiveAllyZoneTest::Zone_EmptyTileCastSucceeds_AndBuffsLateArrival · EnemyField_EmptyTileCast_StillSucceeds | 빈 칸에 시전해도 성공하고 나중에 들어온 아군도 버프를 받는다 | Trigger/IntentApplier.cs:345 → Phases/FieldPrepPhase.cs:327 (매 틱 재발행) | |
| 57 | ActiveMeteorTest::Meteor_MissingProjectile_DropsResolution_ButCastStillSucceeds | 운석 탄이 미배선이면 피해 없이 시전만 성공한다(경고) | Trigger/IntentApplier.cs:237 | |
| 58 | DioramaStagePlayTests::Duel_AllyInstinct_DamagesEnemyWalkingToGoal_WithoutAnyDefender | 방어유닛이 없어도 아군 본능은 골로 걸어가는 적을 때린다 | Phases/CombatPhase.cs:1675 (거점 공격 상태) · :161 (공격 루프) | |
| 59 | DreamcatcherGateE2ETest::ExecutionStrike_DoublesDamage_OnlyBelowQuarterHp | 처형타는 대상 체력이 25% 이하일 때만 피해가 약 2배 | Combat/AttackMod.cs:120 (게이트) · :112 (배율) → Phases/CombatPhase.cs:647 | 부분 — AttackModTests::강공은_N번째_공격만_배율이_붙는다(배율)와 TriggerDispatchTests::공격_N회는_대표_대상이_있는_RESOLVE_만_세고_게이트_실패는_카운트를_안_올린다(게이트)가 따로; 「게이트 아래에서만 배율」 결합 0 |
| 60 | DreamcatcherSleepDamageTest::DamageVsSleeping_StacksMultiplicatively_WithShatterHymn | 수면 배율과 파쇄의 찬가 배율은 곱으로 겹친다 | Phases/CombatPhase.cs:821 | |
| 61 | EffectTileBuffApplyTest::BuffTiles_ApplyAuthoredStatsToOccupant | 버프 타일 3종은 저작한 스탯·연산 그대로 점유 유닛에 든다 | Unity:BoardEffectDefinitionBuilder.cs:255 | Unity 층 매핑 — 라이브 에셋 대조는 EditModeAssets 몫 |
| 62 | EnemyShieldTest::EnemySpawn_HasShieldBufferPair_AndAbsorbsBeforeHealth(rules E19) | 적도 스폰부터 실드 그릇을 가져 가호를 받을 수 있다 | World/Unit.cs:138 (모든 유닛의 고정 부분) | 구조로 성립 — 판 단언 0 |
| 63 | EnemyShieldTest::EnemySpawn_HasShieldBufferPair_AndAbsorbsBeforeHealth(게이지) | 적 머리 위에도 실드 게이지가 그려진다 | Unity:View/CoreUnitOverheadUiLayer.cs:160 | |
| 64 | HitscanDefenderTest::ProjectilelessRangedDefender_DealsDirectDamage_AtRange | 탄이 없는 원거리 방어유닛은 사거리에서 바로 피해를 준다 | Phases/CombatPhase.cs:684 (탄 분기 :650 밖 = 즉시 해결) | 적 쪽만 증언됨 |
| 65 | KnockupOnHitTest::StunNearby_OnPlace_StunsEnemiesInRange_AndSkipsThoseOutside | 배치 기절은 반경 안 적만 | Trigger/CoreSkillContext.cs:185 | |
| 66 | OnPlaceBoostNearbyTest::Boost_RaisesEffectiveDamage_InRangeAndSelf_ButNotOutside | 가디언 배치 버프가 실제로 내는 피해를 올린다 | Phases/CombatPhase.cs:821 · Effects/EffectApply.cs:28 | **해소** — RetiredEffectRulePortTests::공격력_배율이_실제_피해에_곱해진다 · 카드로_건_공격력_버프가_창_동안_실제_피해를_늘린다 |
| 67 | OnPlaceDotNearbyTest::DotNearby_DealsPerTickDamage_InRangeOnly | 틱 간격이 있는 지속 피해의 값은 틱당 피해다 | Effects/DotSet.cs:127 | **해소(기존)** — DotSetTests::새로_걸린_지속_피해는_진입_즉시_1회_준다(주기 1 · 값 7 → 틱당 7) |
| 68 | OnPlaceMeleeBurstTest::MeleeBurst_DealsMagnitudeOnce_InRangeAndReachableLayerOnly | 브루저 배치 폭발은 닿을 수 있는 층의 적만 한 번 때린다 | Trigger/CoreSkillContext.cs:181 (`MatchTraversalLayers`) | 코어 증인은 층을 안 가리는 짱쎈뿐 |
| 69 | OnPlaceReduceSkillCooldownTest::ReduceCooldown_ShortensRunningCooldowns_AndFloorsAtReady | 레인저 배치 단축이 남은 쿨보다 크면 준비 상태(0)에서 멈춘다 | Owners/HandDeck.cs:409 | |
| 70 | OnPlaceStunNearbyTest::Stun_FreezesEnemiesInRange_ButNotOutside · Stun_WearsOff_AndTheEnemyMovesAgain · Stun_AlsoDealsDamage_InRangeOnly | 말파이트 배치: 반경 안 적은 멈추고, 풀리면 다시 걷고, 피해도 반경 안만 | Trigger/CoreSkillContext.cs:185 (반경) · Effects/CcState.cs:116 (만료) | |
| 71 | PatrolDefenderPlayTest::RetiredSummoner_AlsoRemovesPatrol | 소환사를 퇴근시켜도 순찰병이 사라진다 | Phases/FieldPrepPhase.cs:556 | |
| 72 | ProjectileVisualSmokeTest::HitPlayback_ReturnsToPool | 착탄 연출이 끝나면 풀로 돌아간다 | Unity:View/CoreProjectileViewPool.cs:645 | |
| 73 | ProjectileVisualSmokeTest::LaunchAnchor_IsKeptForSpawnAndFirstSync_ThenFollowsSimPath | 탄은 무기 끝에서 나와 첫 동기화 뒤부터 경로를 따른다 | Unity:View/CoreProjectileViewPool.cs:304 · :339 | |
| 74 | RangePredicateMirrorTest::StoppedEnemy_AlwaysEventuallyFires | 멈춰 선 적은 결국 쏜다(멈춤과 발사를 같은 자가 판정) | Phases/AiMovePhase.cs:174 (← Move/ReachProbe.cs:19) | 위치 스윕 카나리아 |
| 75 | UnitOverheadUiLifecycleTest::Reconcile_SharesSprites_FadesOnlyBar_AndClearsDespawnedViews(소멸 정리) | 사라진 유닛의 머리 위 바는 거둔다 | Unity:View/CoreUnitOverheadUiLayer.cs:237 | |
| 76 | WhirlpotLiveRepro::Whirlpot_Attack_SpawnsWhirlVfx(폭) | 회오리 연출 폭 = 사거리 × 칸당 배율 | Unity:View/CoreVfxSpawner.cs:240 | 이식 대상 아님 — 옛 파일이 폭 단언을 스스로 삭제(2026-08-17 · 기계로 못 잰다 → 육안). 저작 배율 > 0 은 Assets:WhirlpotAuthoringTests::AttackVfxPrefab_IsWiredOnWhirlpot_AndOnNoOtherEnemy |
