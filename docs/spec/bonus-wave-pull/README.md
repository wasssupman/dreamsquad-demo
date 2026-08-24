# Bonus Wave Pull — 보너스 당기기

> 상태: **완료 2026-08-24** — units 0~9 · 커밋 `4ecf4429` · 브랜치 **`heart-stress-axis`**
> (EditMode 2455 · 실패 0 / PlayMode 10/10 / 사용자 Play 확인 / 투트랙 리뷰 반영)
> 잔여: 골든 코퍼스 재생성(무관 dirty 격리 후 별도 커밋). handoff: `10_handoff_summary.md`
>
> 선행 읽기: `docs/spec/boss-defender-field/README.md`(사냥 필드 — 이 spec 이 그 후속 후보를
> 실행한다) · `docs/spec/wave-pull-revival/`(일반 당김의 규칙/기제 2층, 도크 자리 예산) ·
> `.claude/skills/enemy-wave-integration/`(신규 적 SO 의 편성 영향)

## 목표

일반 당기기 버튼 위에 **조건부로 등장하는 두 번째 버튼**을 만든다. 누르면 일반 웨이브 편성에
**포함되지 않는** 보너스 적 한 무리가, 스폰 레인이 아니라 **보드 안에 저작된 보너스 포탈**에서
나와, 보스처럼 **배치된 방어유닛을 찾아다니며** 싸우다 방어유닛이 다 죽으면 거점으로 향한다.
체력·공격력이 낮은 적이라 **킬카운트를 불리는 서브 컨텐츠**로 쓴다.

## 검증 질문

> 조건이 차면 두 번째 버튼이 뜨고, 누르면 1초 뒤 보드에 포탈 2개가 열려 2초 뒤부터 보너스 적
> 10기가 순차로 나오는가? 그 적들이 배치된 방어유닛을 찾아다니며 싸우고, 방어유닛이 다 죽으면
> 거점으로 향하는가? **일반 웨이브의 편성과 타이밍이 둘 다 무회귀인가?**

타이밍까지 무회귀인 것은 공짜가 아니라 **계약 10 이 만들어내는 결과**다. 아무것도 안 하면
보너스 적이 `AttackUnitTag` 를 갖는다는 사실만으로 일반 웨이브가 20초 상한 구동으로 강등되고
일반 당김 예산이 얼어붙는다(critic C2).

## 사용자 확정 사항 (2026-08-24)

- **트리거** = 누적 처치 N기마다. 소비하면 카운터가 리셋되고 다시 채우면 또 뜬다(**반복**).
- **버튼 자리** = 일반 알약 **바로 위**에 세로 스택. 가로는 예산이 없다(계약 11).
- **전 맵·전 트리거 공통** = 포탈 2개 · 보너스 적 10기.
- **기존 웨이브 생성 로직과 분리** = `WavePatternGenerator` 계열 무접촉.
- **보너스 적은 일반 판 진행을 멈추지 않는다** = 전멸 판정에서 제외(계약 10).
- **보너스 적 SO 는 시트 관리 대상** = `Data/Enemies/` 에 두고 밸런스는 시트에서(계약 14).
- **스트레스 창** = 마음 스트레스가 30 이하일 때만 버튼이 **등장**한다(계약 15, unit 9).

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_defender_hunter_flag.md` | 게이트 교체 | `AttackUnitData.huntsDefenders` + `DefenderHunterTag`(부착 = `CreateEnemyEntity` 본문) + `BossTag` 게이트 **3개 지점** 교체 + 보스 무회귀 EditMode |
| 1 | `1_bonus_spawn_data.md` | 저작 축(런타임) | `MapDocument.bonusSpawns` + `SetBonusSpawns` + `GeneratedMap` 투영/`Dispose` + `MapDocumentBuilder` + 왕복 EditMode. **소비처는 unit 4 에서 붙는다**(의도된 공백) |
| 2 | `2_bonus_spawn_painter.md` | 저작 도구(에디터) | 페인터 `Tool.BonusSpawn` + `OnValidate`/페인터가 **같은 순수 검증 함수** 사용 + EditModeAssets |
| 3 | `3_bonus_wave_data.md` | 데이터/에셋 | `BonusWaveData` SO + 보너스 적 `AttackUnitData` (덱 풀 **미삽입** 결정 기록) |
| 4 | `4_bonus_wave_scheduler.md` | 시뮬/브리지 | 순수 배분·타임라인 함수 + `BonusWaveTag` + 브리지 스케줄러(`TickBattleFrame`) + 전멸 판정 전용 쿼리 + `configHash` 등재 + EditMode |
| 5 | `5_trigger_and_api.md` | 규칙 층 | 킬 임계 **순수 술어** + `BonusPullAvailable` / `TryBonusPull` + `bonus_pull` 로그 + EditMode |
| 6 | `6_portal_view.md` | 프레젠테이션 | 전투 중 포탈 등장/퇴장 뷰 (`SpawnPortal_Red` 직접 Instantiate — `SpawnStructureViews` 선례) + teardown 등재 |
| 7 | `7_dock_second_pill.md` | UI | 도크 두 번째 알약(세로 스택) + 등장/퇴장 연출 |
| 8 | `8_duel_authoring_and_play.md` | 저작/검증 | Duel 에 포탈 2칸 저작 + Play e2e |
| 9 | `9_stress_gate.md` | 규칙 층 | 스트레스 창 — `maxStressToOffer` + 래치 + 크레딧 보존 (사용자 결정 2026-08-24) |
| 10 | `10_handoff_summary.md` | 인계 | — |

**순서 근거**: unit 0 을 맨 앞에 둔다 — **라이브 보스 동작을 건드리는 유일한 unit** 이므로 신규
데이터가 하나도 없을 때 착지시켜야 「보스 회귀」와 「보너스 웨이브 버그」가 커밋 단위로 갈린다.
1/2 는 Runtime asmdef ↔ Editor asmdef 로 갈리고 테스트 lane 도 갈려(코어 ↔ Assets) 한 커밋에
묶으면 되돌리기 단위가 커진다.

## Feature-wide 계약 (load-bearing)

1. **기존 웨이브 생성과 코드 경로가 갈린다.** `WavePatternGenerator` · `AttackDeck` ·
   `_wavePlan` · `_pending` 무접촉. 보너스 웨이브는 자기 리스트(`_bonusPending`)와 자기
   타임라인을 갖는다.
   **포탈 칸을 기존 `MapDocument.spawns` 로 표현하지 않는다** — `QueueWave` 가
   `laneCount = _generatedMap.spawns.Length`(`BattleBridge.cs:2371`)를 `ExpandWave` 의
   라운드로빈 분모로 쓰므로, 스폰 배열에 2칸을 더하면 **모든 일반 웨이브의 레인 분포가
   바뀐다**. `structures` 로도 안 된다 — 거점 개수가 맵 **모드 판정**에 들어간다
   (`StructureAuthoringRules.ValidateMode`). 그래서 별도 축이다.
2. **전 맵·전 트리거 공통 상수 — 포탈 2 · 적 10.** 맵별·트리거별 분기 없음.
   ⚠ **포탈 개수의 소유자는 `BonusWaveData` 가 아니라 «맵»이다.** 런타임 분모는 언제나
   `GeneratedMap.bonusSpawns.Length` 이고 저작 계약의 「2」는
   `BonusSpawnAuthoringRules.RequiredPortalCount` 하나다. SO 에 `portalCount` 를 두면 읽히지
   않는 값이 인스펙터에서 유효해 보인다(리뷰 H1 — 그래서 그 필드를 지웠다).
   나머지 값(마리수·타임라인·임계·스트레스 문턱)은 `BonusWaveData` 한 곳이 소유한다. **그 SO 와 맵의 포탈 칸은
   `CollectMatchConfig`(`BattleBridge.cs:3004`)에 등재한다** — 수집 기준이 「게임 결과에
   영향을 주는가」인데 등재하지 않으면 보너스 웨이브 저작을 바꿔도 `configHash` 가 안 움직여
   골든 코퍼스가 「조건 무변화」라고 거짓말한다.
3. **결정론은 seeded RNG 가 아니라 구조로.** 포탈 배분 = `i % portalCount`, 스폰 시각 =
   `첫스폰지연 + i × 간격`, 같은 포탈 내 위치 = 셀 중심 + 반경 `tileSize*0.25` · 각도
   `2π·i/count`(분열 레시피 복제, `BattleBridge.cs:9953-9958`). 어디에도 RNG 없음.
4. **보너스 적은 덱 풀에 들어가지 않는다.** 넣는 순간 그 덱의 웨이브가 웨이브 1부터 전부
   재추첨된다. `enemy-wave-integration` 이 인정하는 「풀에 넣지 않기로 결정」 경로이며,
   나중에 넣기로 하면 **그 커밋이 그 스킬을 다시 태운다.**
5. **사냥은 신설이 아니라 개방이다.** `boss-defender-field` 의 필드·이동 분기를 그대로 쓰고
   게이트만 바꾼다. **신규 시스템 0 · 신규 이벤트 채널 0 · 신규 FSM 상태 0.**
   교체 지점은 **정확히 3곳** — `MovementSystem.cs:216`(사냥 이동) ·
   `DefenderFieldSystem.cs:51`(재빌드 skip) · `:55`(R 산출).
   `BossTag` 를 읽는 나머지 5곳(넉업 면역 `AttackSystem:1695` · 어그로 면역
   `AggroStateSystem:175` · CC 면역 `CcApplySystem:27`·`EffectSpawner:35` · bake)은
   **보스 특권이지 사냥 성질이 아니므로 그대로 둔다.**
   ⚠ **부착 지점은 `CreateEnemyEntity` 본문이다.** `BakeNightmareMechanics`
   (`BattleBridge.cs:9079`)는 `nightmareMechanics` 가 비면 조기 반환하므로 `BossTag` 옆에
   두면 **메커닉 없는 보너스 적에게 태그가 안 붙는다** — 보스는 무회귀이고 테스트도 전부 초록인
   채 사냥만 죽는다. 조건은 `tier == Boss || huntsDefenders`.
6. **보너스 적의 `tier` 는 Normal.** Boss 로 두면 CC 면역·어그로 면역·등장 경보가 딸려와
   「저체력 잡몹 무리」가 성립하지 않는다.
7. **보너스 적은 다른 모든 면에서 일반 적이다** — 1킬 1점 · 각성치 · `killHealPerAwakening`
   마음 회복 · 골 도달 시 공성(`canSiege`) · 어그로 피격 · 유출 판정 전부 같은 경로.
   귀결 3가지를 밸런스 결정으로 명시한다: ⓐ 보너스 웨이브 1회의 실제 가치는 「킬 10」이 아니라
   **킬 10 + 마음 회복 + 각성치**다. ⓑ 도발·가디언 자석이 보너스 사냥꾼을 끌어당긴다(사양이다 —
   `boss-defender-field` 의 「aggro 우선」 현행과 일관). ⓒ 골에 도달하면 파괴되지 않고 공성한다
   — 사용자 목표문의 「거점을 패러 이동」이 이것이다. `stabilityDamage` 는 저작으로 정한다.
8. **미저작 맵은 버튼이 뜨지 않는다.** 「보드 중심 자동 도출」 금지 — 벽·거점·골 위에 포탈이
   뚫린다. 저작 검증은 금지 목록이 아니라 **양성 조건 3개**다: ⓐ 그 칸이 걸을 수 있는 칸
   ⓑ 그 칸에서 골까지 도달 가능(방어유닛 0기일 때의 폴백이 goal flow 다 —
   `boss-defender-field` 계약 5) ⓒ 두 칸이 서로 다른 칸. ⓑ 를 빼면 격리 칸의 보너스 적이 영영
   안 죽어 계약 10 의 예외 상태가 상시 켜진다. 검증은 `MapDocument.OnValidate` 와 페인터가
   **같은 순수 함수**를 부른다(`waypoint-routing` unit 5 선례).
9. **플레이어 경로는 `TryBonusPull` 하나.** 기제(무조건 실행)와 규칙(트리거 술어)을
   분리한다 — 일반 당김의 `ForceNextWave`/`TryPullNextWave` 2층 구조와 같은 형태.
10. **보너스 웨이브는 일반 판 진행을 멈추지 않는다** (사용자 결정 2026-08-24).
    `NoQueuedAttackersRemain()`(`BattleBridge.cs:6805`)의 전멸 판정에서 **보너스 적과
    `_bonusPending` 을 둘 다 제외**한다. 제외하지 않으면 보너스 적이 살아 있는 동안
    ⓐ 일반 웨이브가 전멸 구동 → 20초 상한 구동으로 강등되고 ⓑ `_pullsSinceClear` 가 회복되지
    않아 **일반 당김 알약이 잠긴 채 남는다**(계약 7ⓒ 의 공성 때문에 이 상태가 영구화된다).
    ⚠ **`_aliveAttackersQuery` 자체에 `WithNone` 을 붙이지 마라.** 그 쿼리는 11곳이 공유하고
    거기엔 슬로우·토네이도·메테오 사전집계와 **배치 스킬 대상 수집**
    (`CollectEnemiesInTileRange`)·전방 투사체·밀쳐냄이 들어 있다 — 필터를 걸면 보너스 적이
    광역기와 배치 스킬에서 통째로 사라진다. 전멸 판정 **전용 쿼리를 따로 세운다.**
11. **도크 자리 계약 불변** — 알약 리사이즈 금지 · `panelOffset.x + pillSize.x ≤ 300`.
    배치 트레이가 x 320~1600 을 항상 먹으므로 가로 확장은 불가능하고, 두 번째 알약은
    같은 x·같은 폭으로 **위에** 쌓는다.
12. **트리거 카운터는 「일반 적 처치 수」다 — 보너스 적 처치는 세지 않는다.**
    `_killCount`(`BattleBridge.cs:4739`)를 그대로 쓰면 보너스 킬이 다시 임계를 채워 실효 임계가
    `N − 10` 으로 내려가고, **N ≤ 10 에서는 발산한다**(보너스 웨이브가 자기 자신을 무한 재발화).
    ⚠ **판별은 태그가 아니라 SO 동일성이다** — 킬 드레인 시점엔 엔티티가 이미 파괴돼
    `BonusWaveTag` 를 읽을 수 없다. 그 동치는 계약 4(덱 풀 미삽입)가 보장하고,
    `BonusEnemyNotInDeckTests` 가 깨지면 빨개진다(리뷰 M2).
    불변식 `N > BonusWaveData.enemyCount` 는 `OnValidate` + 같은 테스트가 잡는다.
    **소비는 한 회분만** — `consumed += killThreshold`. `= normalKills` 로 두면 스트레스 창이
    닫혀 있는 동안 쌓인 초과 크레딧이 통째로 증발한다(unit 9).
    **점수(`_killCount`)는 계약 7 대로 모든 처치를 계속 센다** — 갈라지는 것은 트리거뿐이다.
13. **보너스 웨이브는 동시 1벌이다.** 진행 중(`_bonusPending` 이 비지 않았거나 포탈이 열려
    있음)에는 `BonusPullAvailable` 이 거짓이다. 카운터는 계속 쌓이고, 끝난 직후 임계를 이미
    넘겨 있으면 버튼이 바로 다시 뜬다. 막지 않으면 포탈 GameObject 가 두 벌 뜨거나 첫 벌이
    orphan 이 된다(teardown 등재와 다른 축 — 이건 수명 중복이다).
14. **보너스 적 SO 는 시트 관리 대상이다** (사용자 결정 2026-08-24). `Data/Enemies/` 에 둔다 —
    `UnitStatExporter` 가 그 폴더를 **전수 스캔**하므로 「시트 행을 안 만든다」는 다음 push
    한 번에 깨진다. 도구와 싸우지 않고 적 17종과 같은 규율을 쓴다. 귀결: **밸런스는 시트에서
    돌리고, SO 직접 편집은 로비 진입마다 덮인다.**

15. **스트레스 창 — 마음이 여유 있을 때만 등장한다** (사용자 결정 2026-08-24, unit 9).
    `BonusWaveData.maxStressToOffer`(기본 30) 이하일 때만 버튼이 **등장**한다.
    ★**등장 조건이지 유지 조건이 아니다** — 뜬 뒤 스트레스가 올라가도 유지된다(매 프레임
    재평가하면 문턱에서 깜빡인다). 진행 중에는 래치를 켜지 않는다.
    마음 없는 맵은 `StressMath` 가 0 을 주므로 창이 항상 열린다(fail-open).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 의 **적 (Enemy)** 아키타입 대조.

| 정거장 | 이 spec 에서 | 비고 |
|---|---|---|
| 데이터 SO | 신규 `AttackUnitData` 1종(`Data/Enemies/`) + `EnemyCatalog` 등록 + 시트 행 | 계약 14. ⚠ 기존 적 복제 시 `targetFactions` 를 **0 으로 되돌린다**(옛 에셋의 `13` 이 「의도된 좁힘」으로 읽혀 방어 본능을 못 때린다 — 2026-08-13 사고). 기본 마스크가 `DefenderCore` 를 포함해 공성이 켜지는 것은 **의도다**(계약 7ⓒ) |
| 등급 축 | `tier = Normal` 고정 | 계약 6 |
| 스폰 진입점 | 기존 `CreateEnemyEntity(so, worldPos, -1, -1)` **재사용** · 신규 wrapper = 보너스 스케줄러 | 레인·웨이포인트 없음(분열 경로와 같은 형태). ⚠ SO 의 `waypointPathIndex = -1` 필수 — 0 이상이면 없는 경유점을 밟다 경고 폴백. 겹침 오프셋은 래퍼(`SpawnUnit`)에만 있으므로 **분열 레시피를 복제**한다(계약 3) |
| ECS 컴포넌트 | 표준 세트 + `DefenderHunterTag`(Combat) + `BonusWaveTag`(Units) | 둘 다 스폰 시 1회 bake, 이후 RO·불변. 신규 버퍼 0 |
| 시뮬 시스템 | **신규 0** — 게이트 3개 지점 교체만 | 계약 5 |
| 이벤트 큐 | **신규 0** — `EnemyKilledEvents`·`GoalReachedEvents` 기존 경로 | CLAUDE.md 채널 목록 갱신 불요 |
| View/Pool | 기존 `SpineUnitPool` / `enemyViewPool` 재사용 | 신규 풀 0 |
| 체력 표시 | 기존 `UnitOverheadUiLayer` 폴링 | 무변경 |
| **포탈 뷰**(신규 정거장) | `SpawnPortal_Red` 프리팹을 브리지가 직접 Instantiate/Destroy | `SpawnStructureViews`/`ClearStructureViews`(`BattleBridge.cs:6256·6291`) 선례 복제. 프랍 파이프라인을 태우지 않아도 **잃을 것이 없다** — 그 `PropData` 는 `billboardMode: None` · `visualScale: 1` · `visualOffset: 0` 이다(확인함). teardown 등재 필수 |
| 씬 wiring | BattleBridge 신규 SerializeField 2(`bonusWaveData`·포탈 프리팹) + 도크 신규 필드 | ⚠ 「도크 색은 씬에 직렬화돼 있다」 경고는 **기존 필드 재사용 시**에만 해당한다. 신규 SerializeField 는 씬 YAML 에 없어 C# 기본값이 그대로 적용된다 |
| 매치 경계 정리 | `_bonusPending` · 보너스 카운터 · 포탈 뷰 리스트 | ⚠ `_pending.Clear()` 는 `BeginPlacement`(1353)·`StartBattle`(1435) **두 곳**이다. 양쪽에 co-locate. ECS 엔티티는 `AttackUnitTag` 로 자동 정리됨 |

## 테스트 계획

「무회귀」를 주장하는 것 중 **지금 자동 그물이 없는 것**을 채운다.

| 대상 | lane | 단언 |
|---|---|---|
| 계약 5 보스 무회귀 | EditMode | `DefenderFieldSystem` 테스트는 **리포 전체에 0건**이다. 합성 월드: `tier=Boss` 적 + 방어유닛 → 1틱 후 hunt-dist 유한 / 태그 없는 일반 적은 goal flow |
| 계약 5 부착 지점(H1) | EditMode | `nightmareMechanics` **비어 있는** SO 로 `CreateEnemyEntity` → `DefenderHunterTag` 존재. 이 한 줄이 H1 을 구조적으로 막는다 |
| 계약 12 자기증식 | EditMode | 트리거 술어를 순수 static 으로 두고 「보너스 킬은 카운터에 안 들어간다」를 값 수준에서 |
| 계약 3 결정론 | EditMode | 배분·타임라인 순수 함수 — 같은 입력 = 같은 출력, 2회 호출 동일 |
| 계약 10 진행 무간섭 | PlayMode | `WavePullCapTest` 형제(그 파일이 이미 `BattleBridgeTestAccess` 리플렉션 헬퍼를 갖는다). 보너스 적 생존 중에도 `cleared` 가 성립 |
| H2 로그 | EditMode | `BattleLogPullEventTests` 형제 — `bonus_pull` 이 스냅샷에 실린다 |
| 계약 1 왕복 | EditMode | `MapDocumentRoundTripTests` 에 `bonusSpawns` 항목 추가 |

**갱신이 필요한 기존 테스트**: `AuthoredTargetMaskTests`(전수 스캔 — 마스크를 좁히지 **않으므로**
통과하지만 이유를 주석으로) · `EnemyCatalogAuthoringTests`(등록 시 자동 편입) ·
`EnemyTierBakeTests`(같은 fixture 에 `DefenderHunterTag` 단언 추가가 자연스럽다) ·
`MapDocumentRoundTripTests`. 맵 문서를 편집하는 unit 1·8 커밋은 **Assets lane 추가 실행**.

## 후속 후보 (스코프 밖)

- **R-별 헌터 필드 분리** [M] — `DefenderFieldSystem` 의 소스 반경 R 은 헌터들 사거리의 **min**
  이라, 보스와 근접 보너스 적이 동시에 살아 있으면 R 이 1 로 내려간다. 스톨은 아니지만
  **보스가 사냥을 멈추고 골로 향하는 전략 퇴행**이 그 창 동안 일어난다(ECS 리뷰 M-3).
  `boss-defender-field` 가 같은 항목을 이미 적어뒀고, 이 spec 이 그 조건을 **실제로 만든다**.
- **보너스 웨이브 발화 횟수의 실측 튜닝** [S] — 임계 N 은 어림값으로 시작한다. 라이브 Duel 덱
  기준 판당 처치는 **50~130 추정**(`minUnitsPerWave 5` / `growth 1.12` / break 15·12 /
  실도달 10~16웨이브 — `docs/reference/map-wave-balancing.md:88`)이라 N=30 이면 판당 2~6회다.
  **잘하는 플레이어일수록 더 자주 발화**하므로 점수 분산이 넓어진다 — 「서브 컨텐츠」라는 위치
  설정과 맞는지 Play 후 판단.
- **맵별·트리거별 보너스 웨이브 차등** [M] — 지금은 전역 1벌(계약 2).
- **포탈 전용 비주얼** [S] — 지금은 레인 스폰과 **완전히 같은 그림**이다. unit 8 Play 검증에
  「레인 포탈과 혼동되지 않는가」를 넣어 판정을 강제한다.
- **보너스 웨이브 전용 점수·보상** [M] — 지금은 1킬 1점 그대로(계약 7). 배수·전용 각성은
  `three-minute-kill-race` 의 「1킬=1점, 예외 없음」을 건드리므로 별 결정.
- **Duel 외 맵 저작** [S] — 라이브 풀 entries 가 Duel 1장이라 지금 저작 대상도 1장.
