# Sheet Export Push — Unity→구글 시트 자동 반영 + 이식 가능한 SheetSync 코어

> 상태: **초안 (승인 대기) 2026-07-22**
> 선행: `unit-stat-spreadsheet-schema` (완료) · `dreamcatcher-sheet-sync` (완료) — API 계약·DTO·exporter 를 그대로 재사용한다.
> SoT 전환(시트=진실)은 **현 스코프 밖** — 별도 spec 초안으로 대기.

## 목표

Unity SO → 구글 시트 **자동 push** 를 만든다. 현재 export 는 JSON 파일(+챗봇 프롬프트)까지만 만들고 사람이 시트에 수동 반영하는데, 데이터가 다변화하며 이 수작업이 병목이 됐다. Apps Script 웹앱 `doPost` 를 반영 엔드포인트로 두고, Unity 에디터에서 버튼 한 번으로 전 8탭을 upsert 한다.

동시에, 이 sheet-sync 기능이 **다른 프로젝트로 이식 가능한 독립 구조**가 되도록 generic transport/envelope 를 게임 무의존 `Wassup.SheetSync` asmdef 로 분리한다. 이식 = "asmdef 복사 + 얇은 adapter 작성 + Apps Script 복붙".

## 접근 결정 (2026-07-22 브레인스토밍)

- **쓰기 경로 = Google Apps Script 웹앱** (백엔드 무관·OAuth 불필요·시트에서 직접 배포). 읽기(import)는 기존 REST 프록시(`GET {base}/{tab}`) 유지.
- **삭제 규칙 = 업서트 + 고아 리포트** (비파괴). SO 에 없는 시트 행은 지우지 않고 목록만 리포트해 사람이 판단.
- **범위 = 전 8탭** (Defenders/Enemies + DC 6탭).
- **모듈화 = 자체 asmdef core + adapter** (UPM 패키징은 안 함).
- **런타임 export 는 제외** — push 프론트는 에디터 전용. import 의 런타임 refresher 경로는 그대로.
- **무위험 유닛 0** (2026-07-22 사용자 결정): SheetSync 는 **POST+envelope 신규 파일만** 담고, working import(`SheetFetcher`/`SheetEnvelopeParser`/`ApiEnvelope`)는 **건드리지 않는다**. 순수 추가라 회귀 위험 0. 대가는 일시적 중복(GET 헬퍼가 레거시에 하나 더) — read 를 SheetSync 로 이관해 중복 제거하는 건 **후속 후보**.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_sheetsync_core_asmdef.md` | `Wassup.SheetSync` asmdef 신설 — 자체 envelope + POST transport (**신규 파일만, import 불변**) |
| 1 | (흡수→2) | `1_adapter_registry.md` | 별도 registry 는 과잉 추상화(소비자 1개, 제약 8) — 유닛 2 로 흡수. 탭명은 창에서 전달, 병합은 탭명 키 |
| 2 | 구현 | `2_combined_payload_builder.md` | 8탭 병합 payload 빌더 — 검증된 exporter 를 임시 폴더에 돌려 재읽기·병합(**exporter 미변경**) |
| 3 | 구현 | `3_push_client_and_response.md` | `SheetPushClient` — payload→POST→응답 파싱(탭별 updated/added/orphans) + EditMode |
| 4 | 구현 | `4_editor_push_button.md` | Import 창에 Script URL 필드(EditorPrefs)+Push 버튼+확인 다이얼로그+결과 로그 |
| 5 | 서버+검증 | `5_apps_script_dopost.md` | `apps-script/Code.gs` generic 업서트 엔진 커밋 + 배포 가이드 + 실 test 탭 1회 push 왕복 검증 |
| 6 | 인계 | `6_handoff_summary.md` | (종료 시) |

## Feature-wide 계약

- **모듈 경계**: `Wassup.SheetSync` = POST transport + 응답 envelope 파싱. **게임 타입 0 참조** (UnityEngine + Newtonsoft 만) · 플랫폼 중립(런타임-capable, 이 프로젝트는 에디터에서만 구동). 공유 `Core/Api/ApiEnvelope` 는 **참조 금지** — 그건 Firebase/Tournament/UserLookup 이 쓰는 인프라라 끌어오면 모듈이 게임 전체에 묶인다. SheetSync 는 동일 wire shape(`{success,data,errorDetail}`)를 읽는 **자체 최소 envelope** 를 가진다. GET 은 소비처가 없어(=import 는 레거시 유지) 넣지 않는다.
- **adapter (`Wassup.Runtime` 잔류)**: DTO 8종·탭/키 config·atk/heal 투영·DC 배열/overlay 규칙·AssetDatabase scan 소스. 이식 시 프로젝트마다 새로 작성하는 부분.
- **push 는 에디터 전용** (`Wassup.Editor.UnitStatImport`): AssetDatabase scan → DTO 직렬화(8탭 병합) → SheetSync POST `/exec` → 응답 로그.
- **Apps Script 계약**: 업서트 by 키(`id` / `(cardId,slot)`) · **blank=keep**(JSON 에 없는 키는 그 셀 안 건드림, import 의 "빈 셀=유지" 와 대칭) · 헤더 순서 유지 + JSON 에만 있는 새 키는 오른쪽 새 열 · **고아 행(시트엔 있고 JSON 엔 없는 키) 삭제 안 함, 리포트만** · 값 원문(enum=문자열, 숫자, 한글).
- **응답 봉투 재사용 shape**: `{success, data:{results:{<탭>:{updated,added,orphans:[키]}}}, errorDetail}`. SheetSync envelope 로 검증, push client 가 `data.results` 해석.
- **키 계약** (import applier 와 동일): `id` = Defenders/Enemies/DcCards/DcSkills/DcConfig · `(cardId,slot)` = DcCardEffects/DcMechanics/DcAttackMods.
- **URL=secret**: Apps Script `/exec` URL 은 쓰기 권한이라 EditorPrefs 에만 저장(미커밋).
- **읽기 프록시 캐시 주의**: push 는 시트에 직접 쓰지만 import 는 `dev-api-somnia` 프록시로 읽는다 → push 직후 프록시가 stale 값을 줄 수 있다(별개 계층). 시트가 진실, 프록시는 지연 가능.

## 파이프라인 커버리지

**N/A** — 이 spec 은 플레이 오브젝트(유닛/적/투사체/해저드/VFX)를 신설하거나 생성→렌더 경로를 바꾸지 않는다. 에디터 툴 + 데이터 왕복 파이프라인이라 `object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보

- **import 를 SheetSync 코어로 완전 이관** [M] · 현재 유닛 0 은 generic transport/envelope 를 옮기되 위험 최소화가 목표. import apply 로직까지 모듈의 generic apply 로 승격하면 진짜 양방향 단일 코어가 된다(회귀 위험 큼 — 별도 유닛).
- **런타임 pusher (카탈로그→POST)** [M] · dev 빌드에서 in-memory 카탈로그를 시트로 push. import refresher 와 대칭. 실기기 push 수요 생기면.
- **SoT=시트 제약 설계** [L] · 시트를 진실로 두고 Unity 편집을 어떻게 제약/병합할지. 별도 spec.
- **push dry-run diff 프리뷰** [S] · POST 전 "바뀔 셀/추가될 행" 미리보기. import dry-run 후보와 대칭.
- **고아 행 반자동 정리** [S] · 리포트된 고아를 사용자 확인 후 일괄 삭제하는 옵션(비파괴 기본 유지).
- **[경계 메모] `Presets` 탭은 이 push 모델 밖** · 프리셋(`SquadPresetCollection`)은 위치 기반 list-SoT + id→SO 참조 해석이라 8탭의 keyed-upsert(`id`/`(cardId,slot)`·blank=keep·고아 리포트)와 본질적으로 다르다. 여기 9번째 adapter 로 끼워넣지 말 것. 프리셋 push 가 필요하면 list-replace 별도 어댑터 — `preset-sheet-import` spec 참조.
