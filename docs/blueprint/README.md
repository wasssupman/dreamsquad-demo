# 현재 설계 윤곽 (blueprint)

> **새 세션의 입구.** 이 게임이 지금 무엇이고, 각 부분이 어떻게 맞물리고, 무엇이 어디서 정본인지만 적는다.
> 수치는 적지 않는다 — 적는 날부터 낡는다. 값은 정본 위치(시트 · 에셋 · 코드)를 가리킨다.
> `docs/reference/` 가 이미 말하는 것은 옮기지 않고 링크한다. 게임 언어가 먼저, 코드 이름은 괄호.
>
> 코드 대조 기준 `478c8c6c0`(2026-10-01). 설계를 바꾸는 spec 은 끝날 때 이 문서의 해당 줄을 고친다(`docs/spec/README.md` 「진행 규칙」).

## 1. 한 줄

전원이 **같은 시드의 3분**을 완주하고, 그 안에서 **몇 마리를 처리했는지**로만 겨루는 비동기 스코어어택 디펜스.
개인 선택은 전부 판 밖(스쿼드 · 드림스톤 · 드림캐쳐 덱)에 있고, 판 안은 전술(배치 · 퇴근 · 드림캐쳐 · 웨이브 당김)뿐이다.
규칙 · 동사 · 설계 지향 7축의 정본은 [`../reference/ingame-flow.md`](../reference/ingame-flow.md).

## 2. 지금 단계

- **프로덕션 초기.** 지금 하는 일은 코드를 정식 설계에 맞추는 것이다(결정 ⑩ — `docs/spec/battle-core-rebuild/README.md`).
- **정본으로 다듬는 대상**: 전투 코어 · 정의표(`MatchDefinition`) · 커맨드와 사건 · 효과 표와 소유 줄 · 시트 스키마.
- **데모(정본 아님)**: 인게임 UI · 에셋. 바꿔도 되고, 설계 근거로 삼지 않는다. 아웃게임은 이 리포에 없다(`demo-diet` 2026-10-07 — somnia-client 가 담당).
- **방향**: 서버 권위 실시간 게임 서버. 서버가 매치 설정을 주고 핵심 로직을 돌리며, 클라는 표시 + 커맨드 전송을 맡는다. **커맨드가 곧 서버 로직의 키워드**다. CI 는 시기상조.
- **서버와 닿는 곳**: 이 리포엔 없다. 로그인 · 토너먼트 참가 · 결과 제출 · 랭킹은 somnia 아웃게임이 맡고, 전투는 입구 값(`MatchEntryInput`)과 출구 사건(`BattleDriver.MatchFinished(MatchOutcome)` 등)으로만 바깥과 통한다. 시트 읽기는 에디터 임포터(프록시 응답 파서 `Data/StatImport/ApiEnvelope`)만. 판 자체는 클라에서 돈다.

## 3. 한 판의 생애

1. **입구** — 바깥(somnia 로비)이 스쿼드(유닛 + 드림스톤) · 드림캐쳐 덱 · 시드를 값 `MatchEntryInput` 으로 넘긴다(편성 검사 · 프리셋 · 로그인은 아웃게임 소관). 입력이 없으면 `BattleDriver` 의 저작 필드가 기본값이다. (`BattleCoreUnity/MatchEntryInput` · `MatchEntryContext`)
2. **시드** — 토너먼트 시드는 `MatchEntryInput.MapSeed` 로 들어온다(참가 신청 · 시도 id 는 아웃게임 소관). 없으면 드라이버의 고정 시드.
3. **판 조립** — 모드 SO 와 저작 SO 를 정의표로 굽고(`MatchDefinitionBuilder`), 시드가 맵과 그 맵에 짝지어진 적 덱 · 웨이브 플랜을 고른다(전원 동일). (`BattleCoreUnity/BattleDriver` · `MatchEntry`)
4. **판** — 카운트다운 뒤 제한시간 동안 실시간. 코어(`BattleMatch`)가 고정 틱으로 돌고, 입력은 커맨드로 들어가고, 뷰는 사건을 받아 그린다.
5. **종료** — 판을 끝내는 통로는 시간 만료 · 마음 붕괴 둘이고, 유저 제출은 언제든 빠져나가는 절차 밖 탈출구다(코드상 종료 사유는 이 셋뿐). 어느 쪽이든 그때까지의 처치 수가 결과다. 결과는 사건 `MatchFinished(MatchOutcome)` 으로 나가고(덱 id 는 `DeckLocked`), 제출 · 랭킹은 구독자(somnia) 몫. 중도 이탈은 `Abandon()` → `MatchAbandoned`. ([`../reference/score-formula.md`](../reference/score-formula.md))
6. **판 뒤** — 이 리포엔 결과 화면이 없다. 판이 끝나면 HUD 가 마지막 점수를 보여 주고 멈춘다(`CoreMatchEndBeat` 의 붕괴 박자만). 복귀 · 미제출 시도 정리는 somnia 아웃게임 소관.

## 4. 시스템 지도

경로는 `Assets/_Project/` 기준. 「정본」은 값과 규칙이 결정되는 곳이다.

| 영역 | 무엇인가 | 정본 | 코드 입구 |
|---|---|---|---|
| 판(전투 코어) | 담당자들이 상태를 나눠 갖고, 틱 단계 목록이 순서를 정한다. 엔진을 모른다 | `../reference/battle-core-architecture.md` | `Scripts/BattleCore/Match/BattleMatch` · `TickPipeline` |
| 방어유닛 | 코스트를 내고 배치하는 고정 개체. 클래스 5. 몸 = footprint 가로 반폭 | 시트 `Defenders` → `Data/Defenders/` · 카탈로그 `Data/DefenderCatalog.asset` | `Scripts/Data/DefenderUnitData` |
| 적 · 보스 | 웨이브가 스폰해 마음으로 오는 개체. 클래스 × 등급. 몸 = 크기 티어(보스만 개별 저작) | 시트 `Enemies` → `Data/Enemies/` · `Data/EnemyCatalog.asset` | `Scripts/Data/AttackUnitData` |
| 웨이브 | 적 덱(편성 knob)이 시드로 웨이브를 생성한다. 같은 맵 = 같은 웨이브 | `Scripts/Data/Decks/` · `Data/WaveConcepts/` · [`map-wave-balancing.md`](../reference/map-wave-balancing.md) | 코어 `Scripts/BattleCore/Wave/WaveGenerator` |
| 맵 | 스테이지 프리팹이 맵의 정본이자 비주얼. 풀에서 시드로 고른다 | `Data/Maps/MapStagePool.asset` · [`map-stage-authoring.md`](../reference/map-stage-authoring.md) | `Scripts/Core/MapStage/` |
| 적 이동 | 목적지별 흐름장 + 어그로 · 감지 · 웨이포인트 우선순위 | [`enemy-movement-algorithm.md`](../reference/enemy-movement-algorithm.md) | 코어 `Scripts/BattleCore/Move/` |
| 드림캐쳐 | 카드가 유닛의 규칙을 바꾼다(스탯을 올리지 않는다). 큐 · 손패 · 각성 게이지 | 시트 `Cards` · `DcSkills` · `DcConfig` → `Data/Dreamcatcher/` · 스키마 `docs/spec/skill-data-table/tables.md` §7 | 코어 손패 `HandDeck` · 굽기 `CardDefinitionBuilder` |
| 스킬 · 효과 | 카드 · 방어유닛 · 적이 같은 소유 줄(트리거 → 효과 id)을 든다. 효과 표 하나 | 시트 `Skills` · `SkillOwners` → `Data/Effects/` · `docs/spec/skill-data-table/` | `Scripts/BattleCore/Trigger/` · `Scripts/Skills/` |
| 드림스톤 | 판 밖 스탯 배율(체급 공급원). 스쿼드 프리셋에 장착 | `Data/Dreamstones/DreamstoneCatalog.asset` | `Scripts/Data/Dreamstone/` |
| 경제 | 코스트(배치) · 각성(카드) · 당김 크레딧 · 쿨타임 | 시트 `CostConfig` → `Data/Config/DefaultCostConfig.asset` · `Data/Dreamcatcher/AwakeningConfig.asset` | 코어 `CostLedger` 등 담당자 |
| 마음 · 점수 | 마음 = 방어 거점의 체력(화면엔 스트레스). 점수 = 처치 수 | [`score-formula.md`](../reference/score-formula.md) | 코어 `HeartMeter` · `ScoreLedger` · 종료 `MatchClock.EndMatch` |
| 시즌 기믹 | 판 전체에 얹히는 규칙(과로 · 번아웃 · 사직서 · 온천). 현행 라이브 모드는 꺼 둠 | `Data/Gimmick/` | 코어 `GimmickHost` |
| 매치 모드 | 목표 종류(enum)를 고르는 SO. 현행 라이브는 하나(`KillScoreTimed`) | `Data/Modes/MatchMode_KillScore3Min.asset` · `docs/spec/battle-core-rebuild/match-mode-design.md` | `Scripts/BattleCore/Match/ModeDef` |
| 시간 | 정지 · 슬로모는 도메인별 lease. 전투는 틱 발행률로 반영 | — | `Scripts/Core/TimeControl/TimeManager` · `BattleDriver` |
| 뷰 · 연출 | 사건을 받아 그리는 뷰 풀들. 순서는 한 파일 | [`object-pipeline-map.md`](../reference/object-pipeline-map.md) · 무기 궤적 [`weapon-trail-authoring.md`](../reference/weapon-trail-authoring.md) | `Scripts/BattleCoreUnity/View/` · `ViewOrder` |
| 바깥과의 경계 | 입구 값 `MatchEntryInput`(스쿼드 · 드림스톤 · 덱 · 플랜 · 맵 · 시드) · 출구 사건 `MatchStarted` · `DeckLocked` · `MatchFinished(MatchOutcome)` · `MatchAbandoned`. 로그인 · 프로필 · 토너먼트는 somnia | [`../spec/demo-diet/0_seams.md`](../spec/demo-diet/0_seams.md) | `Scripts/BattleCoreUnity/MatchEntryInput` · `BattleDriver` |
| 테스트 · 골든 | 어셈블리 다섯 + 헤드리스 lane. 골든은 Unity 에서만 | [`test-procedure.md`](../reference/test-procedure.md) | `Tests/` · `Scripts/BattleCore/Harness/` |

## 5. 값이 흐르는 길

```
구글 시트(8탭) ──에디터 임포트(파일에 씀)──▶ ScriptableObject ──MatchDefinitionBuilder──▶ 정의표(plain) ──▶ 전투 코어
```

- 시트가 밸런스 값의 정본이고, SO 는 그 사본이다. 탭 · 헤더 · 업서트 키의 정본은 `docs/spec/skill-data-table/5_sheet_io.md`.
- 코어가 SO 를 직접 읽는 곳은 없다. 읽는 곳은 `MatchDefinitionBuilder`(+ 옆의 `CombatDefinitionBuilder` · `CardDefinitionBuilder` · `BindingDefinitionBuilder` · `BoardEffectDefinitionBuilder`) 한 군데다.
- 탄 · 패턴 · 장판 같은 모양 표는 아직 시트에 없다(에셋에서만 저작).

## 6. 알고 뺀 것 · 알고 넣은 것

몰라서 빠진 게 아니라 의식적으로 정한 것이다. 뒤집으려면 사용자 결정이 필요하다.

- **저지(블로킹) 없음** — 적은 방어유닛을 통과한다. 몸은 표적이지 벽이 아니다(「적은 네 유닛을 막아서지 않는다 — 지나가는 놈을 잡는 게임이다」). 훗날 저지를 들이면 저지 반경은 몸 반경과 같아야 한다 — 따로 만들면 아래 「그림자가 진실이다」가 깨진다.
- **판에서 지지 않는다**(라이브 모드) — 감점 없음 · 점수 = 처치 수 하나 · 마음은 판정에 관여하되 점수에 섞이지 않는다. 종료 통로를 넷째로 늘리면 패배 조건의 부활이다. UI 어휘는 「포기」가 아니라 「제출」.
- **드림캐쳐는 스탯을 올리지 않는다** — 규칙 · 행동만 바꾼다. 체급은 드림스톤이 판 밖에서 공급한다.
- **개인 유불리를 깎는다** — 맵 · 웨이브 편성 · 공용 액티브는 시드로 전원 동일. 랜덤 대신 구조적 결정론(index 기반 분산).
- **몸 = 그림자 = 판정** — 「닿아 보이면 맞는다」 · 「그림자가 진실이다」. 그림자는 코드가 몸 지름으로 그린다(`CoreSpriteUnitView` 등의 `BlobShadow`).
  아트 발주 규격: 피벗은 점유 박스 하단 중앙 · 루트모션 금지(런지 · 넉백은 시각 오프셋만) · 에셋에 그림자를 굽지 않는다 · 접지 실루엣이 그림자 원을 가로로 크게 벗어나지 않게. 새 적은 크기 티어를 먼저 정한다.
- **첫 판 안내(온보딩) 없음** — 전투 · 로비 안내를 전량 제거했다(결정 ④).
- **사거리는 원** — 「N칸 안」은 몸 사이 거리다. 남은 사각 판정 자리는 사용자 재결정 대기(아래 7).

## 7. 열린 것

상세와 전체 목록은 `docs/spec/README.md` 「Follow-up Backlog」. 여기는 방향을 바꿀 수 있는 것만.

- **서버 권위 spec**(아직 없음) — 열면 자연 해소되는 묶음: 토너먼트 맵 결정권(지금은 클라 우선순위 사슬) · 라이브 판 커맨드 기록과 리플레이. (서버 API · 프로필 저장은 somnia 소관.)
- **규칙 재결정 대기** — 분류표의 보류 행(질문 목록 — `docs/spec/battle-core-rebuild/ledgers/rules.md`) · 일반 공격 대상 선정에 남은 사각 자 · 기본값 박제.
- **스킬 데이터 표 unit 7 보류** — 방어유닛 · 적의 진영 버프 · 공격 변형 배선(`docs/spec/skill-data-table/README.md`).
- **보관 자료 처분** — 로컬 브랜치 `blueprint` · `prd.zip`(설계 전환 전 내용이라 근거로 쓰지 않는다).

## 8. 읽는 순서

1. `CLAUDE.md`(자동으로 들어온다) — 제약 · 함정 · 검증.
2. 이 문서.
3. 손댈 영역의 `docs/reference/` 문서(위 표의 「정본」 열).
4. 진행 중인 spec 의 README(`docs/spec/README.md` 「진행 중 spec」).
5. Unity 조작 · 에셋 · 커밋 전에는 `docs/reference/lessons/` 의 해당 파일.
