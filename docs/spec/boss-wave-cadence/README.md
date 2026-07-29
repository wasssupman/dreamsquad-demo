# boss-wave-cadence — 5웨이브마다 보스 + 꿈결 위기 워닝

> 상태: **완료 2026-07-18** (코드·덱·씬배선 전부 커밋, 사용자 Play 확인 — 5·10웨이브 배너 정상). BattleScene 배선 커밋 f1e7877d(사용자 드래그-프리뷰 WIP 은 unstaged 보존).

## 목표

라이브 생성 웨이브에서 매 `bossWaveInterval`(=5)번째 웨이브를 **보스 1기 + 잡몹 3~4마리**로 편성하고,
보스가 스폰되는 순간 **"꿈결 위기!!"** 크림슨 워닝 배너로 등장을 알린다.

**검증 질문**: "5·10웨이브에 보스가 결정론적으로 등장하고, 보스 스폰 순간 위기 배너가 뜨는가?"

## 공통 원칙 / feature-wide 계약

- **보스 출처** = `AttackDeck.bossUnit`(단일 `AttackUnitData`). `attackUnitPool`과 **분리** → 랜덤 생성기가
  보스를 잡몹으로 뿌리지 않는다. `null` → 보스 웨이브 없음(현행과 동일, graceful).
  - **authoring 계약 + 방어**: `bossUnit`은 `attackUnitPool`에 넣지 않는다. 생성기는 pool 빌드 시 `bossUnit`을
    방어적으로 **제외**한다(있으면 경고 로그). 없으면 no-op → 비-보스 웨이브 불변. 이로써 "분리"가 규약이 아니라
    생성기 불변식으로 강제된다(비-보스 웨이브 보스 오발화·escort 보스 중복 차단).
- **신규 `AttackDeck` 필드**(하드코딩 금지 원칙): `bossUnit`, `bossWaveInterval`(기본 5),
  `bossEscortMin`(3), `bossEscortMax`(4).
- **편성은 `WavePatternGenerator.Generate`(seed 경로)에서만**. `FromPlanAsset`(authored 테스트) 미변경.
- **주입 방식 = 치환**: 기존 랜덤 루프로 전 웨이브를 만든 뒤, `(i+1) % bossWaveInterval == 0` 웨이브의
  groups 를 **보스×1(선봉) + 잡몹×[min,max]**(같은 seed rng)로 덮어쓴다. → 비-보스 웨이브는 현행 생성기와
  **동일**(같은 seed). 보스는 그룹 맨 앞(RoundRobin round 0 = 보스 먼저 스폰).
- **`waveGeneratorVersion` 1→2**: 보스 웨이브로 seed 리플레이가 달라짐을 남기는 **오프라인 분석 로그 라벨**
  (`BattleLogger` JSON 기록용). 런타임은 이 값을 rng 에 투입하지도, stale 플랜을 거부하지도 않는다(순수 라벨).
- **미리보기 노출은 의도적 수용**(2026-07-18 사용자 결정): `WavePatternStripView`가 드래프트/스쿼드 준비/일시정지
  메뉴에서 `Generate(deck)`를 호출하므로 보스 웨이브가 미리보기 카드에 그대로 표시된다. 보스 배치가 index 기반이라
  seed 가 달라도 "몇 번째 웨이브에 보스"는 드러난다 — 이를 **수용**한다(스쿼드 대비 이점). 워닝 배너는 "깜짝"이 아니라
  **인-배틀 극적 경보** 역할. 미리보기 마스킹은 하지 않는다.
- **워닝 트리거 = 보스 스폰 순간**. `BattleBridge.SpawnUnit`의 보스 판별 지점(`nightmareMechanics` 비어있지
  않음 — 기존 `BakeNightmareMechanics` 조건)에서 `BossWarningView.Show()`를 호출. 웨이브/생성기 무관 →
  seed·authored 어느 경로든 자동. **단일 진실 = nightmareMechanics**.
- **`BossWarningView`** = 런타임 절차 UI(`UiCanvasSetup` + `UiRoundedSprite` + PrimeTween), **Kanit Bold
  Italic SDF**(스코어와 동일 폰트/머티리얼 serialized), 크림슨→화이트핫 플래시 + 붉은 비네트 펄스.
  슬램인→홀드→페이드 ~2.5s(전부 SerializeField). sortingOrder > 스코어 HUD. 재진입 코얼레스(`_showing` 가드).
  unscaled time(timeScale=0 모달 중에도 재생, ScoreHud 선례).
- **ECS 경계**: 워닝 트리거는 Mono(BattleBridge) 내부, 크로스 컨텍스트 쓰기 없음. 생성기는 순수 데이터(ECS 무참조).

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 생성기 | `0_generator_boss_injection.md` | AttackDeck 보스 필드 + `Generate` 주입 + EditMode 테스트 |
| 1 | UI | `1_boss_warning_view.md` | `BossWarningView` 런타임 크림슨 배너 컴포넌트 |
| 2 | 브리지 | `2_spawn_warning_hook.md` | `SpawnUnit` 보스 판별 지점 → `BossWarningView.Show()` + serialized 참조 |
| 3 | 배선 | `3_scene_wiring_play.md` | BattleScene 배선 + 폰트/비네트 할당 + 덱 asset 값 + Play 검증 |

> 순서 근거: UI(1)를 스폰 훅(2)보다 먼저 → `BattleBridge`의 `BossWarningView` 참조가 컴파일되려면 타입이 선재해야 함.

## 파이프라인 커버리지

**N/A** — 신규 플레이오브젝트/생성→렌더 경로 없음. 보스 아키타입은 기존(`AttackUnitData` + `nightmareMechanics`,
스폰·베이크 경로 불변), 워닝은 HUD(플레이오브젝트 아님). 본 spec 은 **웨이브 편성 + HUD** 만 다룬다.
편성 데이터의 소비처는 스폰(BattleBridge)과 **미리보기(`WavePatternStripView`)** 둘이며, 후자의 보스 노출은
위 "미리보기 노출은 의도적 수용" 계약대로 그대로 둔다(렌더 파이프라인 변경 아님).

## 후속 후보

- ~~**보스 로테이션**(`bossPool`)~~ → **`docs/spec/boss-jjangssen/0_boss_pool_field.md` 로 승격(2026-07-29)**. 두 번째 보스 "짱쎈놈" 추가와 함께. `bossUnit` 은 **rename 하지 않고 유지** + `bossPool` 추가 + 폴백 — 라이브 덱 9개가 guid 를 들고 있어 rename 하면 보스가 조용히 사라진다.
- **엄밀한 리드**: "배너 먼저 → N초 후 보스 워크인"(보스 스폰 지연). 현재 기본은 "스폰 순간 배너".
- **보스 웨이브 파워/밸런스 스케일링** — 파워 예산 시스템(사용자 보류)과 함께.
- **최종 웨이브 보스 클라이맥스** 특수 처리(현재는 순수 매 N번째).
- **잡몹 다타입 호위**(현재 1타입 3~4) / **워닝 사운드**(SoundManager 스팅).
