# boss-mamemo — 세 번째 보스 「마메모」

> 상태: **완료 2026-08-11** (units 0~5 · 사용자 Play 확인 · 투트랙 코드리뷰 반영 ·
> 실플레이 버그("재우기가 안 보인다") 계측→수정→재확인까지). 인계: `5_handoff_summary.md`.
> 설계 근거·접은 대안: `docs/plans/2026-08-11-boss-mamemo-design.md`.
> 잔여는 밸런스 관측 축뿐이며 `docs/spec/README.md` Follow-up Backlog 로 이관됨.
>
> **구현 중 잡힌 실버그 2건 (둘 다 자장가 대상 선정)**: ① 도넛 안쪽 경계가 사거리 링을
> 포함(`+1` 누락) — 코드리뷰가 발견. ② 그 수정(사거리 링 전체 제외) 자체가 과잉 —
> 붙는 보스는 조우 대부분을 사거리 안에서 보내 도넛 후보가 말라 **조우당 1회**로 퇴화했다.
> 실플레이 보고 후 프레임 계측으로 확정, «때릴 대상 1기만 rank 제외»로 교체(`a6ef2c38`).
> 전말은 unit 1 문서.
>
> **rev 2 = 투트랙 스펙 리뷰 반영.** 초판은 `three-minute-survival`(08-07) 이전의 게임 규칙을
> 전제로 써서 검증 질문이 현재 규칙 위에서 참·거짓을 못 가렸다. 위협의 표현을 "골이 뚫린다"에서
> **"웨이브 회전을 멈춘다"** 로 교체했다. 그 외 거짓 계약 1건·잘못된 진영 축 1건·자기모순 1건 정정.

> **선행 토대 — 착수 전에 읽는다.**
> 게임 규칙: `three-minute-survival`(점수=처치 단일 출처 · 전멸 즉시 진행 / 20초 상한 ·
> 안정도) · `goal-tower-siege`(골 = 공성 대상, 도달한 적이 죽지 않는다) ·
> `battle-structures`(`Faction` = 진영 × 종류 교차 비트, 거점 신설).
> 보스 저작: `boss-jjangssen`(bossPool 로테이션 · 보스 CC 면역 · 경계 무겹침 · **보스 생존 4~7초**) ·
> `nightmare-catcher`(trigger×payload 적 편입).
> 재사용 부품: `dreamcatcher-shield-break`(AreaSleep) · `shield-guardian-defender`(ShieldSlot·
> IncomingShield · 캐스트는 action-lock 게이트 밖).

## 목표

세 번째 보스를 추가하고 기존 2종과 `bossPool` 로테이션으로 공존시킨다.

**마메모는 플레이어의 시간을 뺏는 보스다.** 이 게임의 점수는 처치 단일 출처이고 웨이브는
전멸 즉시 넘어간다 — 즉 **빨리 미는 것이 곧 점수**다. 마메모의 세 능력은 전부 그 회전을 멈춘다:
방어유닛을 재워 못 밀게 하고, 자기 실드로 자기가 안 죽고, 호위에게 실드를 줘 호위가 골에
눌러앉게 만든다(공성 적은 살아 있으므로 **전멸 조건이 채워지지 않아 웨이브가 20초 상한에 고착**된다).

기존 두 보스와 다른 점은 **손해의 종류**다. 나이트메어·짱쎈놈은 유닛을 죽여 재배치 비용을
물리고, 마메모는 아무도 안 죽이면서 **점수와 시간을 가져간다.**

## 검증 질문

> 마메모 웨이브가 **다른 웨이브보다 눈에 띄게 오래 걸리고**(전멸이 늦거나 20초 상한에 닿고),
> 그것이 판 전체의 웨이브 수 = 최종 점수에 드러나는가?
> 그리고 **화력을 집중하거나 캐스터를 편성한 플레이어는 그 손해를 회수**하는가?
> 기존 덱의 **잡몹 편성은 무회귀**인가?

## 3개 패턴

| 패턴 | 트리거 × 페이로드 | 하는 일 | 플레이어 대응 |
|---|---|---|---|
| ① 자장가 | `PeriodicTimer` × `AreaSleep`(적→방어유닛) | 반경 내 방어유닛 몇을 잠시 재운다 — **공격 채널만** 멈춘다 | **캐스터/가디언 편성**(계약 3) · 분산 배치 |
| ② 꿈의 장막 | `HealthThreshold` × `GrantShield`(자신) | 경계마다 자기 실드 — 보스 처치가 늦어진다 | 순간 화력 집중 |
| ③ 악몽의 가호 | `PeriodicTimer` × `GrantShield`(주변 호위) | 호위가 안 죽고 골에 눌러앉는다 → 전멸 지연 | 광역·지속 화력 |

**수치는 SO 값이라 이 표에 박지 않는다**(제약 6). 초안값은 그 패턴을 저작하는 작업 단위에서
정한다(자장가 = unit 1 · 실드 2종 = unit 3).

## 수용하는 대가 (product 판정 — 리뷰 C2)

마메모는 **점수·웨이브 회전·각성 회전을 동시에 누른다.** 점수가 처치 단일 출처이므로
"안 죽는 보스 + 안 죽는 호위"는 그 웨이브의 획득 점수를 직접 깎고, `awakeningReward` 도 처치
지급이라 카드 회전까지 마른다 — **딜 버스트가 필요한 순간에 그 수단이 같이 준다.**

**이것을 의도로 수용한다.** 그게 이 보스의 압박 그 자체이기 때문이다. 대신 튜닝 계약을 둔다:
③의 실드량은 **"호위가 더 오래 버티되 웨이브가 20초 상한에 고착되지는 않는"** 선에서 저작한다.
unit 5 의 관측 항목 1번이 **마메모 웨이브의 소요 시간**이다.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_boss_asset_and_pool.md` | `Enemy_Boss_Mamemo.asset` + `EnemyCatalog` + `bossPool` 3종 + `score-formula.md` 갱신 — **잡몹 편성 무회귀를 먼저 증명** |
| 1 | 시뮬/데이터 | `1_lullaby_sleep.md` | 자장가 — 주기 arm 에 `AreaSleep` 적→방어유닛 분기 (**신규 페이로드 0**) + 에셋 슬롯 + EditMode |
| 2 | 계약/브리지 | `2_enemy_shield_payload.md` | `GrantShield` 신설 + 적 스폰 버퍼 쌍 부착 + bake 가드 + **적 오버헤드 실드 게이지 3줄** |
| 3 | 시뮬/데이터 | `3_shield_arms_and_authoring.md` | 경계 arm(자기) · 주기 arm(주변 호위) 배선 + 에셋 슬롯 2개 저작 |
| 4 | 프레젠테이션 | `4_sleep_and_shield_visuals.md` | 방어유닛 수면 표식 앵커/오프셋 + 실드 부여 VFX 판정 |
| 5 | 인계 | `5_handoff_summary.md` | 커밋 · 검증 · 되돌리면 안 되는 것 |

> 계획의 «unit 5 = Play 검증·튜닝 루프» 는 별 문서 없이 수행됐다 — 사용자 실플레이 다회 +
> `BossLullabyLiveTest`(발동 계측 상시 로그) + 실플레이 버그 수정 루프(`a6ef2c38`)가 그 실체다.
> 남은 밸런스 관측(웨이브 소요 시간·가호의 골 앞 유지)은 Backlog 로 이관.

**순서 근거 (짱쎈놈 선례)**: unit 0 은 mechanics 를 비운 채 에셋만 넣어 **기존 덱 무회귀와
스폰·이동·외형을 먼저 증명**한다. 단 `BakeNightmareMechanics` 는 mechanics 가 비면 early
return 이라 `BossTag`·워닝·방어유닛 사냥 이동이 **하나도 안 붙는다**(`BattleBridge.cs:7519-7521`)
— unit 0 의 검증 범위는 거기까지다.

unit 2 가 unit 3 보다 앞인 이유는 컴파일 의존이다(enum·bake·버퍼가 없으면 arm 이 참조할 대상이
없다). **적 오버헤드 게이지를 unit 2 에 둔 이유**는 버퍼 부착과 같은 커밋이어야 "붙었는데
화면에 안 보인다"를 눈으로 가릴 수 있어서다.

**unit 5(검증·튜닝)와 unit 6(인계)을 나눈 이유**: 이 보스의 검증 질문이 판 단위 관측이고
수치가 전부 미정이라 튜닝 루프가 붙는다. 짱쎈놈은 이 둘을 묶었다가 README 머리에
"PlayMode e2e 미작성"이 박혔다 — 나이트메어(`6_boss_play_validation.md` 독립)를 따른다.

## Feature-wide 계약

1. **신규 맥락 0 · 신규 시스템 0 · 신규 이벤트 채널 0.** 추가되는 것은 페이로드 1종
   (`GrantShield`)과 기존 두 arm(`BossPeriodicTriggerSystem` · `HealthThresholdSystem`)의
   분기뿐이다. 새 채널이 필요해 보이면 정지하고 질문한다.

2. **자장가는 기존 CC 파이프라인만 쓴다 — 브리지 실드파열 경로가 아니다.**
   방어유닛은 이미 `CcEffect` 버퍼를 갖고(`BattleBridge.cs:6270`), `AttackSystem.cs:249-252` 가
   공격자의 `CcActionLock.IsLocked` 를 확인하며, `CcApplySystem` 에는 대상 진영 게이트가 없다.
   → arm 이 `EnemyCcEventsSingleton` 에 enqueue 하면 `CcApplySystem` 이 적용한다.
   **`AreaSleep` 의 기존 실행기(`BattleBridge.DrainShieldBreakEvents` → `CollectShieldBreakTargets`)는
   대상 풀이 `AttackUnitTag` 하드코딩이라 재사용 대상이 아니다** — 그쪽에 방어유닛 쿼리를
   섞으면 기존 실드파열 카드가 깨진다. 페이로드 kind 만 공유하고 실행 경로는 별개다.
   `EnemyCcEventsSingleton` 은 **이름만** 적 지향이다 — 이름 때문에 병렬 채널을 만들지 않는다.

3. **자장가는 공격 채널만 멈춘다 — 그리고 그게 플레이어의 대응책이다.**
   `HazardCastSystem` · `ShieldCastSystem` 은 CC 를 **참조하지 않는다**(grep 결과 0건).
   `shield-guardian-defender` 계약 7 이 "캐스트는 action-lock 게이트 밖"으로 일부러 정한 결과다.
   → 장판 캐스터·가디언은 **자면서도 일한다.** 이걸 버그로 고치지 말고 ①의 대응책으로 쓴다:
   *마메모는 화력형 편성을 벌하고 서포트 편성에 약하다.* 코드 0줄로 생기는 플레이어 주도 답이다.

4. **자장가는 «내가 때릴 대상» 하나만 제외한다 — 링이 아니다.** (실측으로 2회 개정)
   wake-on-hit(`DamageApplicationSystem.cs:221-230`)에는 진영 게이트가 없어 방어유닛도 맞으면
   깬다. 마메모는 `BossTag` 이라 방어유닛을 사냥해 붙어서 때리므로 **자기가 재운 유닛을 자기
   평타로 깨운다.** 그런데 `attackTargetCount` 는 1 이라 **한 번에 1기만** 깬다.
   → 제외는 거리 오름차순 앞에서 `attackTargetCount` 기, **사거리 안일 때만**.
   ~~도넛(안쪽 = 사거리)~~ **폐기 2026-08-11 — 실측**: 붙는 보스는 조우의 대부분을 사거리
   안에서 보내 도넛 후보가 마르고, 12초 조우에 자장가가 **1회**밖에 안 터졌다(rank 제외는 2회,
   누적 수면 2.3초 → 4.8초). 자기무효화(1/3 낭비)를 막으려다 발동 자체를 없앤 과잉이었다.
   ~~`AttackState.committedTarget` 제외~~ 초판 기준. 그 필드는 START→RESOLVE 1회 수명이라
   안정된 기준이 못 된다.
   따름정리(수용): 실드를 두른 방어유닛은 **완전 흡수 히트가 피격이 아니므로**(guardian 계약 3)
   자장가에 더 오래 잔다. 이건 계약 3 과 방향이 반대인 상호작용이며 튜닝 관찰 항목이다.

5. **후보 풀의 축은 유닛 태그이지 `FactionTag` 이 아니다.**
   `AttackUnitTag`(적) / `DefenderUnitTag`(방어유닛)로 모은다 — 선례는
   `BossPeriodicTriggerSystem.cs:109-131`(whip 오라)이고 반대 진영 풀은 그 거울이다.
   **`FactionTag` 을 쓰면 안 된다**: `battle-structures` 이후 `Factions.AnyEnemy` 는
   **적 마음·본능(거점)** 을, `AnyDefender` 는 **골 타워**를 포함하는데 거점은 `CcEffect`·
   `ShieldSlot`·`IncomingShield` 버퍼를 **갖지 않는다**. 버퍼 없는 엔티티에 append 하는 것은
   `object-pipeline-map.md` §거점 이 "ECB playback 에서 던진다"고 경고한 경로다.
   (`boss-jjangssen` 의 진영 도출은 **투사체 `targetFaction`** 이라 별개 메커니즘이다 — 섞지 않는다.)

6. **`GrantShield` 하나가 패턴 2·3 을 표현하되, `tileRange > 0` 은 host 를 제외한다.**
   `tileRange 0` = 자신만 · `>0` = 반경 내 같은 진영 **유닛(host 제외)**.
   **host 를 포함하면 ②③이 붕괴한다** — `ShieldMath` 는 `source` 를 병합 키로 쓰므로 둘 다
   마메모가 출처라 **한 슬롯을 공유**하고, ③이 매 주기 ②의 잔량을 재충전해 "경계마다 생기는 벽"이
   "상시 실드"가 된다. host 제외가 출처 축을 쪼개는 것보다 훨씬 싸다(제약 8).
   `magnitude` = 실드량. **`duration` 은 무시된다 — 이 엔진의 실드에 TTL 이 없다**
   (`ShieldMath` 에 시간 축 없음). bake 가 `duration > 0` 저작을 **loud 경고**한다(unit 2).

7. **재부여는 누적이 아니라 갱신이고, 그게 의도다.** `ShieldMath.Merge` 가 같은 출처 슬롯을
   `max` 로 덮으므로 반복 부여는 **깎인 만큼만 다시 채운다.** arm 은 `ShieldMath.ValueFromSource`
   로 만충 대상을 건너뛴다(가디언 unit 4 선례 — 안 그러면 매 주기 헛 VFX 가 튄다).
   **마메모 사망 후에도 호위의 실드는 잔류한다**(`ShieldSlot.cs:8` — source 는 수명 링크가 아님).
   이걸 의도된 유산으로 수용한다. 사후 소멸이 필요하면 별건이다.

8. **`ShieldCastSystem` 을 손대지 않고, 버퍼는 항상 쌍으로 붙인다.**
   `ShieldCastSystem` 은 caster·후보 양쪽에 `DefenderUnitTag` 하드 게이트가 있는 가디언 전용
   생산자다. 이 spec 은 **`IncomingShield` append 라는 아래층**을 쓴다.
   `IncomingShield` 드레인이 `ShieldSlot` 존재로 게이팅돼 있으므로(`DamageApplicationSystem.cs:134`)
   **한쪽만 붙이면 조용히 무한 성장**한다. 부착 대상은 `SpawnUnit` 의 **적 유닛뿐** — 거점
   (`SpawnStructureEntities`)에는 붙이지 않는다(`battle-structures` 계약 8).
   ③의 수혜자가 호위 잡몹이므로 **보스만이 아니라 적 전원**에 붙인다(조건부 부착은 arm 의 대상
   선정을 스폰 시점에 왜곡한다).

9. **적 오버헤드 실드 게이지는 공짜가 아니다 — 3줄을 채워야 한다.**
   하위 레이어(`UnitOverheadUiLayer` · `UnitOverheadView` · enemy skin 의 shield 색)는 이미
   진영 무관이지만, **적 분기의 `shieldRatio` 인자가 리터럴 `0f`** 다(`BattleBridge.cs:3073`).
   방어유닛(`:3069`)·순찰병(`:3169`) 분기에 같은 폴링 3줄이 복붙돼 있으므로 **헬퍼로 추출**해
   3곳이 공유하게 한다. unit 2 의 작업이다.
   *(초판 계약 9 는 이걸 "공짜"라 적었고 인용 줄이 순찰병 분기였다 — 리뷰 정정.)*

10. **주기는 한 자리 초 이하로 저작하고, 두 주기를 배수 관계로 두지 않는다.**
    `boss-jjangssen` 계약 4 가 실측으로 남긴 기준선은 **보스 생존 4~7초**이고, 그래서 그 보스는
    주기 능력을 아예 안 썼다. 마메모는 ①③ 둘 다 주기라 이 제약을 정면으로 받는다.
    ②(자기 실드)가 생존을 늘려 ①③을 굴리는 것이 설계 의도이지만, **실드가 붙기 전 구간은
    여전히 4~7초**다(경계 트리거는 피해를 받아야 발동한다). 나이트메어의 라이브 주기가
    0.5s / 0.1s 인 것이 참고선이다.
    배수 관계 금지는 짱쎈놈 계약 5 와 같은 이유다 — **같은 프레임 동시 발동이 없어야 두 능력이
    별개 사건으로 읽힌다.**
    unit 1 완료 기준에 **"1회 조우에서 자장가 N회 이상 발동"** 을 넣는다. 없으면 "구현은 됐는데
    게임에서 안 보인다"가 재현된다.

11. **`bossPool` 2종 → 3종은 잡몹 편성을 바꾸지 않는다.** rng 소비 **횟수**가 그대로이므로
    `escortCount`/`escortType` 이 밀리지 않는다(`WavePatternGenerator.cs:167` — `Count>=2` 면
    `NextInt` 1회, range 무관 `NextState()` 1회). `waveGeneratorVersion` 은 올리지 않는다.
    unit 0 이 EditMode 로 고정한다(현재 `WavePatternGeneratorBossTests` 에 «Count 2 vs 3» 케이스 없음).
    **검증 절차 주의**: 능력 검증용으로 `bossPool` 을 마메모 단독 고정하면 `Count==1` **rng 미소비
    경로**로 갈라져 웨이브 편성이 라이브와 달라진다 → **무회귀 검증과 능력 검증을 같은 판에서
    하지 않는다.** (3종 균등이면 판당 보스 2~3회라 마메모를 한 번도 못 볼 확률이 30~44%다.)

12. **기존 보스 계약 상속** — 아래는 이 spec 이 바꾸지 않는다.
    `bossUnit` **rename 금지**(짱쎈놈 계약 1: 라이브 덱 9개가 guid 를 물고 있어 rename 하면
    에러 없이 전 맵에서 보스가 사라진다) · **보스는 방어유닛을 사냥한다**(`boss-defender-field`
    계약 1, `BossTag` 전체) · **보스 CC 면역**(`CcActionLock.IsBossImmune` — 마메모도 수면·스턴·
    넉백 면역. "재우는 보스가 자기는 안 잔다"는 의도된 비대칭이다).
    수치는 전부 SO 에서 온다(제약 6) — **시트에 `boss_mamemo` 행을 추가하는 순간 시트가 권위가
    되어 SO 직접 튜닝을 덮는다.**

## 파이프라인 커버리지

대조: `docs/reference/object-pipeline-map.md` §적(Enemy). 신설 플레이 오브젝트는 없고 기존 보스
아키타입에 스킬 경로가 붙는다.

| 정거장 | 마메모 | 비고 |
|---|---|---|
| 데이터 SO | unit 0·1·3 | `Enemy_Boss_Mamemo.asset` + `EnemyCatalog` 등록 + `bossPool`. **신규 SO 타입 0**. unit 0 이 `killScore`/`stabilityDamage`/`awakeningReward` 를 명시 저작한다(기본값 1/1/0 으로 조용히 틀리는 자리) |
| 스폰 진입점 | 기존 `SpawnUnit` | 보스 선택은 생성기 안. unit 2 가 실드 버퍼 쌍 부착 |
| ECS 컴포넌트 | 기존 적 경로 상속 | `BossTag`/`ThreatEntry`/`DcTriggerSlot` 은 기존 베이크. 추가는 `ShieldSlot`/`IncomingShield` **버퍼 2개**(신규 타입 0) |
| 시뮬 시스템 | 기존 2곳 수정 | `BossPeriodicTriggerSystem`(unit 1·3 — `defEntities` lazy-load 를 hostIsEnemy 분기로 확장) · `HealthThresholdSystem`(unit 3). **신규 시스템 0** |
| 이벤트 큐 | 기존 재사용 | `EnemyCcEvents`(수면) · `ShieldGrantedEvents`(부여 VFX). **신규 채널 0** |
| View/Pool | `SpineUnitPool` 공유 | 기존 보스와 같은 스켈레톤, `partSkins`/스케일만 다름 |
| 체력·실드 표시 | **unit 2** | 적 분기 `shieldRatio` 가 `0f` 하드코딩 — 폴링 3줄 헬퍼 추출(계약 9) |
| 상태 표식 | unit 4 | 수면 표식은 **이미 진영 무관**(`StatusFxKind.Sleep` 주석 "적·아군 공통", `_ccEffectQuery` 에 진영 컴포넌트 0). 남은 일은 방어유닛 앵커/오프셋 육안 + `StatusFxRegistry` 의 빈 프리팹 슬롯 판정 |
| 씬 wiring | **N/A** | 수면 표식은 `StatusFxRegistry` 배선, 실드 VFX 는 `ShieldGrantedEventsSingleton` → `VfxSpawner.SpawnShieldGranted` 가 **이미 배선돼 있다**(guardian unit 4). 신규 SerializeField 0, 프리팹 교체는 guid 스왑 |
| 참조 문서 | unit 0 | `docs/reference/score-formula.md` 보스 티어 표에 행 추가 |

## 알려진 주의점 (unit 문서로 옮길 것)

- **실드 1프레임 지연** — `HealthThresholdSystem` 은 `[UpdateAfter(DamageApplicationSystem)]` 인데
  `IncomingShield` 드레인은 그 안에 있다. 경계 관통 프레임에 append 한 실드는 **다음 프레임**부터
  흡수한다. 60fps 기준 무시 가능하지만 "경계에서 즉시 무적"으로 읽히지 않게 unit 3 에 명시.
- **선별 수학을 새로 쓰지 않는다** — 반경 필터는 `AuraPulse`(Combat, 순수·EditMode 고정),
  N명 cap 은 `AoeTargetCap.SelectNearest`(결정론 tiebreak). 둘 다 이미 있다(제약 10).
- **`ShieldGrantedEvent` 는 `float3 position` 하나만 싣는다** — 진영·host 가 없어 아군/적 실드
  연출이 같아진다. 다르게 하려면 필드 1개 추가(신규 채널 0 은 유지되나 "채널 무변경"은 아니다).
  현재 연출이 placeholder 라 unit 4 에서 판단만 하면 된다.
- **적 실드 파열은 지금 아무것도 안 한다** — bake 화이트리스트가 `PeriodicTimer`/`HealthThreshold`
  만 허용해(`BattleBridge.cs:7566-7571`) `OnShieldBreak` 슬롯이 적에 안 붙는다. 이 안전이 우연이
  아니게 unit 2 에서 EditMode 로 고정한다.

## 후속 후보 (범위 밖)

- **악몽의 늪(자리에 남는 장판)** — 배치 공간 박탈 축. 장판 효과가 `ZoneApplySystem` 의
  `PathFollowState` + 적 진영 게이트라 **타일 고정인 방어유닛에게 구조적으로 안 닿는다**.
  방어유닛용 존 소비 경로 신설은 `summon-patrol-defender` unit 0 이 일부러 닫은 아군 오폭
  게이트를 반대로 여는 작업이라 별 spec 이다. **다음 보스에서 딥하게 논의**(사용자 결정 2026-08-11).
- **네 번째 보스: 소환형** — 잡몹을 직접 뱉는 물량 축.
- **보스 트리거 개방 — `OnShieldBreak`** — 마메모와 궁합이 가장 좋다(실드가 깨지는 순간 반격).
  단 견적이 "게이트 완화 한 줄"보다 크다: 적 SO bake 화이트리스트 + **실행기가 Mono
  (`DrainShieldBreakEvents`)이고 대상 풀이 `AttackUnitTag` 하드코딩**이라, 보스가 파열 폭발을 쓰면
  자기 진영을 때린다(`targetFaction` 미지정 = `Enemy` 기본). **실행기 진영 파라미터화가 게이트
  완화보다 선행**이다. 짱쎈놈 README 의 같은 항목과 묶어 처리한다.
- **면역으로 죽은 카드·유닛** — 마메모전에서 체감이 최고조가 된다: 수면 카드 `Card_LullabyDart`,
  수면 방어유닛 `Defender_TooMuchTalker`(`sleepOnHitSec 3.5`)가 **"재우는 보스에게 내 재우기가
  안 통한다"** 는 가장 눈에 띄는 조합을 만든다. 짱쎈놈 README 의 동일 항목과 병합.
- **실드 회복/재생** — 지금은 부여 후 깎이면 끝이다.
- **⚠ 자는 캐스터가 계속 시전한다 — 사용자 판정 대기 (2026-08-11 Play 관측)**
  계약 3 이 서술한 그대로의 현상이다(`HazardCastSystem`·`ShieldCastSystem` 은 CC 참조 0,
  `shield-guardian-defender` 계약 7 이 일부러 그렇게 정했다). **다만 사용자가 이걸 "버그"로
  읽었다** — 계약 3 의 "의도된 대응책(캐스터 편성이 자장가에 강하다)" 프레이밍이 실제 체감과
  어긋난다는 신호다. 잠든 유닛이 장판을 계속 까는 그림은 규칙보다 먼저 **눈에 거슬린다**.
  선택지는 둘이고 **product 결정이다**: (a) 프레이밍 유지 — 캐스터를 자장가의 답으로 남긴다,
  (b) 캐스트를 action-lock 게이트 안으로 넣는다 — 그러면 `shield-guardian-defender` 계약 7 을
  뒤집는 것이라 가디언·해저드 캐스터 전원의 동작이 바뀌므로 **이 spec 밖 별건**이다.
  **당장 고치지 않는다(사용자 지시).** 결정 전까지 계약 3 은 "현상 서술"로만 읽는다.
- **잠든 유닛을 깨우는 능동 수단** — 현재 유일한 해제는 피격이다. 위 항목이 (a) 로 결정되면
  우선순위가 낮아지고, (b) 로 결정되면 자장가가 강해지므로 다시 본다.
- **참조 문서 stale** (마메모 책임 아님, 다만 이 spec 이 읽는 문서다):
  `score-formula.md` 가 "안정도 최대치 20"·"0 = 유일한 패배 조건"으로 남아 있고
  (실제 1000 · `stress-after-breach` 이후 아님), `map-wave-balancing.md` 는 보스를
  `bossUnit`/Nightmare 단일로 적고 있다. 별건 정비.
