# Preset Sheet Import — 스프레드시트 → SquadPresetCollection 임포트 매퍼

> 상태: **구현 완료 2026-07-22 (units 0~4) · 라이브 시트 검증 대기** — 커밋 `e66be309`(1)·`06eb866d`(2)·`8a922165`(3)·`0ae9a2c7`(4). EditMode 8/8, export/import round-trip 구조 검증(units 14/0/0·cards 20/0). 남은 것: `Presets` 시트 탭 신설 후 라이브 왕복(에디터 import diff + 로그인 자동 import + 페이지 반영). 인계는 `5_handoff_summary.md`.
> 선행/재사용: `unit-stat-spreadsheet-schema`(완료) · `dreamcatcher-sheet-sync`(완료) — 읽기 transport(`SheetFetcher`/`SheetEnvelopeParser`) · `UnitAssetScan` · `BuildIndex` 를 그대로 재사용.
> 관계: `sheet-export-push`(진행 중; unit 0 `e17ab435` 착지) 와 **방향 반대** — 저건 SO→시트 push, 우리는 시트→SO import. `Wassup.SheetSync` 는 **POST 전용**이라(코어 주석: "GET 은 소비처 없음, import 는 레거시 SheetFetcher 유지") 우리 읽기 경로는 레거시 위에 짓고 SheetSync 는 건드리지 않는다.

## 목표

현재 프리셋 데이터(`SquadPresetCollection`)는 유니티 인스펙터에서만 authoring 된다. 이를 **스프레드시트를 SoT 로** 관리할 수 있게, 시트의 프리셋 행(이름 + 스쿼드 + 드림캐쳐)을 읽어 컬렉션을 재구성하는 **임포트 매퍼**를 만든다. 스쿼드/드림캐쳐는 각각 한 컬럼에 `,` 로 구분된 id 목록으로 입력하고, 임포터가 id → SO 참조로 해석한다.

### 검증 질문

기획자가 `Presets` 탭에 `presetName / squad(csv id) / dreamcatcher(csv id)` 행들을 채우고 임포트 버튼을 누르면, 그 행들로 `SquadPresetCollection.presets` 가 통째 재구성되고(참조 해석 포함) 프리셋 페이지에 그대로 뜨는가? dev 빌드에서는 로그인 시 자동 반영되는가?

## 작업 단위 목록

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs) | `0_sheet_schema_contract.md` | `Presets` 탭 스키마 + 위치 기반 리스트 재구성 + id 해석 + 미해결 처리 확정 |
| 1 | 구현 | `1_dto_and_applier.md` | `PresetDto` + `PresetSheetApplier` 순수 코어 + EditMode 테스트 |
| 2 | 구현 (에디터) | `2_editor_import_export.md` | Import 창 Preset 섹션 — import + seed export(SO→JSON) |
| 3 | 구현 (런타임) | `3_runtime_refresher.md` | `PresetSheetRuntimeRefresher`(로그인 자동 반영) + 페이지 재빌드 |
| 4 | 배선/검증 | `4_scene_wiring_and_verify.md` | `AllRuntimeRefresher` 배선 + Play 왕복 검증 |
| 5 | 인계 | `5_handoff_summary.md` | (종료 시) |

## Feature-wide 계약

1. **시트 탭 = `Presets`**, 컬럼 3개: `presetName`(표시명) / `squad`(≤7 DefenderUnitData id, `,` 구분, 순서=슬롯) / `dreamcatcher`(≤ 라이브 deckSize DreamcatcherCard id, `,` 구분). 행 순서 = `presets` 리스트 순서. id/slot 컬럼 없음.
2. **시트 = 리스트 전체 SoT.** 임포트는 `collection.presets` 를 시트 행들로 **통째 재구성**(행 추가/삭제/재정렬 = 프리셋 추가/삭제/재정렬). **가드**: 파싱 실패/빈 응답(rows=null)이면 **no-op** — 기존 리스트를 절대 날리지 않는다(기존 applier 동일 정책).
3. **id → SO 참조 해석.** unit id→`DefenderUnitData`, card id→`DreamcatcherCard`. 미해결(오타 등) = **unmatched 리포트 + 유닛은 그 슬롯 null(빈슬롯), 카드는 스킵**(best-effort; 부분이라도 반영). 유닛은 `maxUnits` 초과분 drop + 리포트.
4. **asset 포맷 무변경.** `SquadPresetCollection` 은 계속 SO 직접 참조(`DefenderUnitData[]`/`DreamcatcherCard[]`)를 담는다. 임포터는 **인스펙터를 대체하는 authoring 경로**일 뿐, 프리셋 페이지·`PresetApply` 적용 로직은 무손(unit 3 의 페이지 재빌드 트리거만 예외).
5. **레이어 경계.** `PresetDto`+`PresetSheetApplier` = **adapter**(`Wassup.Data.PresetImport`, 게임 타입 참조 O). 읽기 transport(`SheetFetcher`/`SheetEnvelopeParser`) = 레거시 재사용. `Wassup.SheetSync`(POST 전용, 게임 무의존) 는 import 가 쓰지 않는다.
6. **keyed-upsert 모델 밖.** 프리셋은 **위치 기반 list-SoT + 참조 해석**이라, 8탭의 keyed-upsert(`id`/`(cardId,slot)`, blank=keep, 고아 리포트) 와 **본질적으로 다르다**. 프리셋을 그 generic adapter/push 경로에 끼워넣지 않는다 — push 는 후속(§후속 후보).
7. **덱 규칙 검증 없음(v1).** 프리셋은 유효하게 authoring 된다는 전제. 무효 덱은 기존 START 로드아웃 게이트(`LoadoutGate.Check`)가 잡는다(loadout-preset-page 계약 승계).
8. **DTO = 양방향 컬럼 계약.** 필드명 = 시트 헤더. import 파싱과 export 직렬화가 같은 `PresetDto` 를 통과 → 후속 프리셋 push 시 매핑 재사용.
9. **런타임 = in-memory dev/QA only.** 로그인 후 `Presets` 탭을 fetch 해 컬렉션을 **메모리에서만** 갱신(에셋 저장 없음, 재시작 시 원복). `LoginAutoImport → AllRuntimeRefresher` 경유. 릴리스 빌드는 dev API 미호출(기존 게이트 승계).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트(유닛/적/투사체/해저드/VFX)나 생성→렌더 경로 변경이 없다. 아웃게임 authoring 데이터 + 에디터/런타임 임포트 파이프라인이라 `object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보 (현 스코프 밖)

- **프리셋 push (list-replace)** — 컬렉션 → 시트 자동 반영. `SheetHttp.Post` 재사용하되 8탭의 keyed-upsert 가 아니라 리스트 통째 교체 방식. sheet-export-push 스코프 밖의 별도 어댑터.
- **import dry-run diff 프리뷰** — 적용 전 "바뀔 프리셋/슬롯" 표시(sheet-sync 후속과 대칭).
- **적용됨 하이라이트 · 런타임 덱 규칙 검증 · 드림스톤 4슬롯 포함** — loadout-preset-page 후속 승계.
- **카드 수 ≠ 라이브 deckSize 경고 강화** — 현재는 리포트만, 게이트화는 후속.
