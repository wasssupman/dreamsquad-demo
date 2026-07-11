# Dreamcatcher Sheet Sync — 드림캐쳐 카드/효과/설정 스프레드시트 관리

> 상태: **완료 2026-07-11** — 실 구글 시트 왕복 검증 포함 (no-op IDENTICAL + farewell 밸런스 반영). units 0~1 `1eed2ee0` · 2 `e338c7da` · 3 `2608c179` · 4 `a3f4c9a9`. 인계는 `5_handoff_summary.md`.
> 선행: `unit-stat-spreadsheet-schema` (완료) — API 계약·fetch/parse/apply 코어를 그대로 재사용한다.

## 목표

기획파트가 드림캐쳐 카드의 밸런스 수치·텍스트를 Unity Inspector 대신 스프레드시트에서 관리한다. 유닛 스탯과 달리 카드는 **중첩 배열(effects/mechanics/attackMods)** 과 **에셋 참조(projectile/skill/art)** 를 가지므로, 업계 표준(마스터데이터 정규화)대로 **본체 탭 + 효과별 자식 탭(FK 조인)** 으로 분해한다.

## 설계 근거 (2026-07-10 리서치)

- **정규화 자식 테이블 + id FK**: 배열 항목 1개 = 자식 탭 1행, `(cardId, slot)` 복합 키. Unreal DataTable RowHandle·모바일 마스터데이터 표준 패턴.
- **EAV/제네릭 param1..N 컬럼 배제**: 문서화된 안티패턴. 기존 `DcPayloadSpec` 처럼 의미명 고정 컬럼(magnitude/tileRange/duration) 유지 — SO 구조와 1:1이라 구조 파괴 없음.
- **에셋 참조는 시트 계약 밖**: 기존 유닛 계약 동일. `_skillId`/`_projectileId` 는 `_` 접두 정보성 컬럼(export 기록, import 무시).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs only) | `0_json_schema_contract.md` | 탭 6종 스키마 + 컨벤션 + 배열 싱크 시맨틱 확정 |
| 1 | 데이터+docs | `1_seed_json_and_sheet_guide.md` | 시드 JSON(`1_seed_dreamcatcher.json`) — 기획 시트 초기 입력 원본 |
| 2 | 구현 | `2_dto_and_child_appliers.md` | config SO `id` 필드 + DTO 6종 + (cardId,slot) 매칭 applier + EditMode 테스트 |
| 3 | 구현 | `3_import_window_and_export.md` | Import 창 Dreamcatcher 섹션 (6탭 fetch→apply) + SO→JSON export + 실 왕복 검증 |
| 4 | 구현 | `4_unit_sheet_awakening_reward.md` | 기존 Defenders/Enemies 탭에 `awakeningReward` 컬럼 (하드코딩 감사 결과 반영) |
| 5 | 인계 | `5_handoff_summary.md` | (종료 시) |
| 6 | 구현 | `6_review_fixes.md` | 3관점 리뷰(아키텍트/코드/프로세스) 반영 — H1/H2/M2 버그 + 무음 실패 방어 |

## Feature-wide 계약

- **탭 6종**: `DcCards` / `DcCardEffects` / `DcMechanics` / `DcAttackMods` / `DcSkills` / `DcConfig`. 기존 API (`GET {base}/{sheetName}`, envelope `{success,data,errorDetail}`) 그대로, 탭당 1콜.
- **배열 SoT 는 에셋 참조 유무로 이원화** (2026-07-11 확장성 검증): 순수 스칼라 자식 탭(`DcCardEffects`/`DcAttackMods`)은 **시트가 배열 SoT** — 탭에 등장한 cardId 의 배열을 행들로 전체 재구성(행 추가 = 효과 추가), 미등장 카드는 유지 + 길이 변화 리포트. `DcMechanics` 는 projectile 에셋 참조 때문에 **Unity 가 구조 SoT** — `(cardId, slot)` 매칭 값 갱신만. 에셋 참조(projectile/skill/art)는 Inspector 관리 유지.
- **갱신 전용**: 신규 .asset 생성 없음 (유닛 파이프라인 동일). 카드 신설은 Unity에서.
- **부분 갱신 컨벤션 동일**: 빈 셀 = 키 생략 = 기존값 유지 · enum 은 C# 멤버명 case-insensitive · `_` 접두 컬럼 = 계약 밖(임포터 무시) · DTO 필드명 = SO 필드명 = 시트 헤더 (중첩 필드만 접두 평탄화: `trigger.kind`→`triggerKind`).
- **싱글턴 config 도 시트 관리**: `AwakeningConfig`/`DeckRuleConfig` 에 `id` 필드 신설(append-only), `DcConfig` union 탭에서 행별 부분 갱신.
- **텍스트도 시트 관리**: displayName/description 은 기획 조작 대상 (현 에셋의 한글 오타도 시트에서 수정).
- **코어 재사용**: `SheetFetcher`/`SheetEnvelopeParser`/`UnitStatApplier.BuildIndex` 공유. 규칙 분기 금지.

## 운영 규칙 (2026-07-11 리뷰 확정)

1. **Import 직전에 대상 Data 폴더를 커밋해 둔다** — import 결과가 리뷰 가능한 diff 가 되고, 사고 시 `git restore` 한 방으로 복구된다 (staleness 사고의 실제 복구 경로).
2. **Import 결과는 Data 4폴더(Defenders/Enemies/Dreamcatcher/Skills) 전부 포함해 단일 커밋** — 절반 커밋은 정합성 깨진 스냅샷을 남긴다.
3. **Unity 에서 SO 를 고치면 즉시 Export 로 시트에 되올린다** — 시트가 낡으면 다음 import 가 조용한 롤백이 된다.
4. import 로그의 "headers not in contract" 경고는 컬럼 rename 사고 신호다 — 보이면 시트 헤더를 먼저 확인.

## 하드코딩 감사 결과 (2026-07-10)

- **현행 awakening hand 경로는 클린** — 밸런스 수치 전부 `AwakeningConfig`/카드 SO 소재. 리팩토링 불요.
- `awakeningReward` (DefenderUnitData=4, AttackUnitData=1) 가 **유닛 시트 계약에 누락** → unit 4 에서 컬럼 추가.
- dormant 3중1 경로(`DreamcatcherController` 의 `%5`/`Draw3`)는 scene-dormant 레거시 — 손대지 않음.
- `BattleBridge.DcDuration=1e9f`(매치영구 센티널), `/100f`(% 변환) 는 구조 상수 — 시트화 대상 아님.

## 후속 후보

- **Import dry-run diff 프리뷰** [M] · 적용 전 "변경될 에셋/필드 목록" 표시 후 확인. staleness·무음실패의 구조적 마감 — 3관점 리뷰 공통 최우선 권고 (2026-07-11).
- **탭 배선 매핑 테이블화** [S] · `ApplyDcFetched` 의 위치기반 `r[0..5]` → 탭명↔DTO 매핑. 7번째 탭 추가 시점에.
- **`RebuildEffects`/`RebuildAttackMods` 병합** [M] · 90% 중복 — 3번째 자식배열 도메인이 생기기 직전에 (추상화 규칙 준수).
- **import 직전 git-dirty 검사** [S] · 대상 폴더에 미커밋 변경 있으면 경고 — dry-run 전까지의 값싼 방어.
- **Dreamstones 탭** [S] · `DreamstoneData`(13종, CardEffect 재사용) 평탄 탭 — 유닛 패턴 그대로.
- **런타임 리프레시(로비 버튼) 드림캐쳐 확장** [M] · `DreamcatcherCardCatalog` 경유. 5번째 카탈로그 발생 시 `ScriptableCatalog<T>` 승격 조건(runtime-stat-refresh 백로그) 함께 검토.
- **드림캐쳐 외 하드코딩 밸런스** [S] · `BattleBridge.SynergyPerNeighbor=0.1f`, `EnemyKillScoreDelta=10`, 점수 산식(`durationSec*10 - goal*50`) — 별도 spec 으로 SO/시트화 검토.
- **기본 덱 구성(카드 id 리스트) 시트 노출** [S] · `DreamcatcherDeck_Default` — 에셋 참조 리스트라 id 해석기 필요.
- **시트發 신규 카드 upsert** [M] · 현 계약은 update-only. 시트 행으로 .asset 신설(기본값 생성 후 art/projectile 만 Unity 보충) — 카드 신설 빈도가 높아지면 검토.
