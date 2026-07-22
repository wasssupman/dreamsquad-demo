# Apps Script 배포 가이드 — Push to Sheet 반영 엔드포인트

`Code.gs` 는 Unity "Push to Sheet" 가 POST 하는 `{ "<탭명>": [행], ... }` 를 받아 각 탭에
키 기준 업서트하는 generic 엔진이다. 스프레드시트에 바인딩해 웹앱으로 배포한다.

## 배포 6스텝

1. 대상 **구글 스프레드시트**를 연다. (import 가 읽는 그 시트 — 탭명이 계약과 일치해야 함:
   `Defenders`, `Enemies`, `DcCards`, `DcCardEffects`, `DcMechanics`, `DcAttackMods`, `DcSkills`, `DcConfig`.)
2. **확장 프로그램 → Apps Script** 로 스크립트 에디터를 연다(시트에 **바인딩**됨 = `getActiveSpreadsheet()` 로 시트 ID 불요).
3. 기본 `Code.gs` 내용을 지우고 이 폴더의 `Code.gs` 를 **통째로 붙여넣고 저장**.
4. **배포 → 새 배포 → 유형: 웹 앱**.
   - 실행: **나(내 계정)**.
   - 액세스 권한: **모든 사용자**(= URL 소지자. `Anyone with Google account` 아님 — 그러면 UnityWebRequest 가 인증 토큰이 없어 실패).
5. **배포**를 누르고 권한 승인(최초 1회). 완료 후 나오는 **웹 앱 URL(`.../exec` 로 끝남)** 을 복사.
6. Unity `Window/Wassup/Unit Stat Import` → **Apps Script URL** 필드에 붙여넣기(EditorPrefs 로컬 저장, 커밋 안 됨).

> 코드를 고칠 때마다 **배포 관리 → 편집 → 새 버전**으로 재배포해야 반영된다(저장만으론 `/exec` 가 안 바뀜).

## 계약 요약 (Code.gs 가 강제)

- 키: `id`(Defenders/Enemies/DcCards/DcSkills/DcConfig) · `(cardId,slot)`(DcCardEffects/DcMechanics/DcAttackMods) — `KEY_CONFIG` 상수.
- 업서트 · blank=keep(없는 키 셀 안 건드림) · 헤더 순서 유지+새 열 우측 · 고아 삭제 안 함(리포트만).
- 응답 `{success, data:{results:{"<탭>":{updated,added,orphans:[키]}}}, errorDetail}`. list-replace 탭은 `{replaced:N}`.
- **list-replace 모드** (`LIST_REPLACE_TABS`, 현재 `Presets`): 키 없는 list-SoT 탭은 `clearContents`+전체 재작성(삭제·재정렬을 미러). 빈 payload 는 스킵(전체 비우기 사고 방지). keyed 8탭의 비파괴 업서트와 **별개** — Unity 가 리스트 전체를 소유하는 탭에만 등록.
- **동기화 컬럼에 수식 금지** — 매트릭스 재기록 시 값으로 대체됨. 메모/수식은 계약 밖 별도 열.

## 실 왕복 검증 (유닛 5 완료 기준 — 실 시트 또는 사본에서)

1. **값 무변 push** → 시트 **IDENTICAL**(업서트 no-op, blank=keep 실증). 응답 `updated=N, added=0, orphans=[]`.
2. **SO 값 1개 변경 후 push** → 해당 셀만 갱신, 나머지·시트전용 열 무변.
3. **SO 행 1개 제거 후 push** → 시트 행 **잔존** + 응답 `orphans` 에 그 키 + Unity Result 로그에 고아 경고.

## 함정 — 탭 키 컬럼 라벨 (실사고 2026-07-22)

Defenders 탭 첫 컬럼 헤더가 `id` 가 아니라 **`공`** 으로 잘못 라벨돼 있었다(식별자 슬러그는 `공` 컬럼에 있었고 `id` 컬럼 자체가 없었음). 첫 push 가 이를 못 알아채고 `id` 컬럼을 새로 만든 뒤 20행을 **중복 추가**(37행 = 17 옛 + 20 새). Enemies 는 첫 컬럼이 `id` 라 정상 매칭됐다. dreamcatcher `DcCards` mojibake(`D����`) 사건과 동류.

→ **각 탭 1행 헤더의 키 컬럼명이 정확히 `id` / `cardId` / `slot` 인지 배포 전 확인.** `Code.gs` 는 이후 "기존 행이 있는데 키 컬럼이 헤더에 없으면 그 탭 스킵 + 에러 리포트"로 방어하므로, 잘못된 탭은 이제 조용히 중복되지 않고 Unity Result 에 `⚠ SKIPPED` 로 뜬다.

## 이식 (다른 프로젝트)

`Code.gs` 를 복사하고 `KEY_CONFIG` 만 그 프로젝트의 탭↔키로 교체. Unity 측은 `Wassup.SheetSync`
asmdef 복사 + adapter(payload 빌더) 재작성.
