# 3 — canonical MatchConfig blob + configHash

## 목적

골든의 "같은 조건" 보장은 스탯 SO 스냅샷만으로 부족하다 — 씬 상주 gameplay knob(스폰 spread, `enableAdjacencySynergy` 등)도 결과를 바꾼다. 한 판의 조건 전체(맵·웨이브플랜 **생성 결과**·덱·seed·유닛/스킬/투사체/해저드/기믹 스탯·점수 룰·씬 knob)를 **불변 blob으로 물질화**하고 canonical 직렬화의 `configHash`를 만든다. 골든 diff 발생 시 "시트 드리프트 vs 코드 회귀"를 해시로 먼저 가르는 1차 판독 장치이자, 이후 AMR·커맨드로그의 공통 필드다. 셋업 난수(`UnityEngine.Random` — 웨이브 생성·기믹 선택)는 생성 **결과**가 blob에 실리므로 sim 상류로 격리된다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MatchConfigSnapshot.cs` (또는 Battle 하위) — 수집·canonical 직렬화·SHA 해시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StartBattle` 직전 수집 지점 (씬 knob 필드 목록화 포함)
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs` — 테스트/하네스 모드 차단 가드 (시트 임포트가 SO를 덮어 골든을 오염시키는 기존 함정 방어)

## 구현

수집 범위는 "게임 결과에 영향을 주는가"로 판정 — 뷰 전용 값(비주얼 스케일·그림자 등)은 제외. canonical 직렬화는 필드 순서 고정·문화권 불변 포맷(invariant)·부동소수 R 포맷으로 재현성 확보. 씬 knob 전수는 Bridge SerializeField 88개를 gameplay/presentation으로 분류해 목록을 이 unit에 기록(M1 salvage 판정의 입력 재활용). 해시는 골든 덤프(unit 4) 헤더에 동봉.


## unit 3 에서 실제로 한 것 (2026-08-22)

### 물질화

`Wassup.Core.MatchConfigSnapshot` + `MatchConfigWriter`(신규). 포맷 규칙은 셋뿐이다 —
줄 단위 `key=value`(섹션 `[name]`) · 문화권 불변 + 부동소수 `"R"` · `null` 은 `~`
(빈 문자열과 구분: 「참조가 없다」와 「이름이 빈 문자열」은 다른 조건이다).
해시는 SHA-256 앞 8바이트(16 hex) — 로그·헤더에 실을 길이.

수집 지점은 하나다: `BattleBridge.StartBattle` 의 `_running = true` **직전**. 맵·웨이브
플랜·거점이 확정됐고 sim 이 아직 한 틱도 돌지 않은 유일한 순간이다.

### 스탯 SO 는 손으로 나열하지 않는다

`PutAsset` 이 리플렉션으로 SO 한 장을 통째로 접는다. 필드 목록을 손으로 쓰면 반드시
낡고, 낡은 목록은 **「스탯 하나를 바꿨는데 해시가 그대로」라는 조용한 실패**를 만든다 —
완료 기준이 정확히 그것을 금지한다. 규칙 셋:

- `public` + `[SerializeField] private` 를 **이름순**으로 접는다(선언 순서는 런타임 계약이
  아니다 — 정렬해 두면 「같은 값인데 해시가 다르다」가 원천적으로 불가능해진다).
- **데이터 SO 참조는 이름까지만.** 파고들면 SO 그래프에서 순환한다.
- **아트 참조는 아예 담지 않는다**(Sprite·Texture·Material·Shader·GameObject·Component·
  AudioClip·Font·AnimationClip·AnimatorController). 담으면 스킨 교체가 「조건이 바뀌었다」로
  읽혀 판독 장치가 거짓말을 한다.

⚠ 아트 판정은 **null 검사보다 앞**이어야 한다. 뒤에 두면 「아트가 비어 있을 때만 줄이
생기고 채우면 사라지는」 비대칭이 되어 머티리얼 하나 꽂는 것만으로 해시가 바뀐다 —
`MatchConfigWriterTests.Asset_ArtReference_DoesNotChangeHash` 가 이 형태로 잡았다.

### 웨이브는 **생성 결과**를 담는다

생성기 seed 만 담으면 안 된다. 웨이브 생성은 셋업 난수(`UnityEngine.Random`)를 쓰므로,
결과(웨이브별 트리거 시각·펼침 모드·그룹의 `유닛×수량@오프셋/레인/경로`)를 물질화해야
그 난수가 sim 상류로 격리된다. 적 로스터도 **이번 판 플랜에 실제로 등장하는 것만** 접는다
— 덱 전체를 담으면 이 판이 쓰지도 않는 적의 스탯 변경이 해시를 흔들어 신호가 무뎌진다.

### 씬 knob 분류표 (SerializeField 86개)

**gameplay = 15개.** 전부 스냅샷에 들어간다(직접 값 또는 **물질화된 결과**로).

| 필드 | 담기는 방식 |
|---|---|
| `deck` | `[deck]` 자산 다이제스트 |
| `defenderPool` | `[defenders]` 자산 다이제스트 ×N |
| `stackModifierAuthoring` | `[stackModifiers]` 자산 다이제스트 ×N |
| `mapPool` | **결과**로 — `[map]` (tiles·placeMask·spawns·goals·waypoints·structureCount) |
| `seasonRegistry` | **결과**로 — `[gimmick] assigned` 자산 다이제스트 |
| `fixedMapSeed` | `[sceneKnobs]` |
| `tileSize` · `spawnHeight` · `agentRadiusTiles` | `[sceneKnobs]` |
| `spawnSpreadEnabled` · `spawnSpreadFraction` · `spawnSpreadTopScale` · `spawnSubLaneCount` | `[sceneKnobs]` |
| `enableAdjacencySynergy` | `[sceneKnobs]` |
| `dcProcImpactMinIntervalSec` | `[sceneKnobs]` |

**presentation/wiring = 71개.** 담지 않는다. 그룹으로:
뷰 풀·스포너·UI 참조 24 (`spineUnitPool`·`vfxSpawner`·`scoreHud`·`resultScreen`·
`placementInput`·`tilemapMapView`·`draftController`·`skillRuntime` 등) ·
그림자/lift 11 (`blobShadow*`·`lift*`·`useRealShadows`) ·
타일맵·프랍 룩 9 (`tilemapCharacterScale`·`tilemapBillboardTilt`·`propDistanceTilt*`·
`mobilePropBudgetScale`·`tilemapHiddenEnvironment`·`tileSet`) ·
픽업/사직서/오버헤드 뷰 8 · 적 dim·오프셋 3 · 체력 표시 스타일 3 ·
도약 연출 16(`bossLeap*` 11 + `ultimateLeap*` 5 — 심은 즉시 텔레포트하고 뷰만 아치로 난다).

판정이 갈릴 뻔한 둘을 기록해 둔다:
- `tileSet` → **presentation**. 통행·배치 판정의 정본은 `GeneratedMap.tiles`/`placeMask`(둘 다
  스냅샷에 있다)이고 이 SO 는 그 위에 얹는 그림이다.
- `scoreRules` → **은퇴**. `ScoreRulesData` 는 필드 없는 빈 SO 다(점수는 적 SO 의
  `killScore` 에서 직접 나온다). 담을 값이 없다.

### 골든 오염 방어

`LoginAutoImport.TriggerOnce` 가 하네스 구동 중 임포트를 건너뛴다. `_done`(one-shot)을
**소비하지 않는다** — 소비하면 하네스가 끝난 뒤 라이브 세션의 값 갱신이 조용히 사라진다.

## 완료 기준

- [x] 같은 조건 2회 실행 → `configHash` 동일 — 하네스 보고서가 매 실행 이 두 값을 싣는다
      (실측 `24ed312970824526` 동일). 갈리면 보고서가 **「코드 회귀가 아니라 조건 드리프트」**
      라고 먼저 말한다.
- [x] 스탯 SO 값 1개 변경 → 해시 변경 — `MatchConfigWriterTests` 7건이 고정
      (public 필드·`[SerializeField] private`·null vs 빈 문자열·문화권 불변·아트 제외·
      null 참조도 기록). 라이브 실측 텍스트는 3560줄(방어유닛 821 · 적 1586 · 웨이브 760 ·
      knob 11)로 커버리지가 비어 있지 않음을 확인.
- [x] 하네스 모드에서 LoginAutoImport 미실행 —
      `LoginAutoImportTests.HarnessActive_SkipsImport_AndKeepsTheOneShotUnspent`.
- [x] gameplay/presentation knob 분류표 기록(위).

확인 2026-08-22 · 조건 지문은 `harness-determinism.md` 헤더에 실린다.
