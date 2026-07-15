# Runtime Stat Refresh — 로비 시트→SO 갱신 버튼

상태: **구현 완료 2026-07-06** (units 0~2) · **드림캐쳐 확장 완료 2026-07-13** (units 3~4) · **전체 임포트 확장 완료 2026-07-15** (units 6~7). 인계 `5_handoff_summary.md`(0~4) · `8_handoff_summary.md`(6~7). 잔여: 실기기 Development Build 1회 확인 + 릴리즈 빌드 버튼 미노출 확인 (unit 2 완료 기준 참조)

## 목표

빌드된 앱(실기기 포함)의 로비에서 버튼 한 번으로 구글 시트 최신 밸런스를 내려받아 **메모리 내 SO 인스턴스에 즉시 반영**한다. 다음 전투부터 새 수치가 적용된다 (전투 스폰이 SO를 읽는 시점 = 전투 시작). **내부 개발/QA 전용** — 릴리즈 빌드에서는 숨김.

선행: `docs/spec/unit-stat-spreadsheet-schema/` (완료) — API 계약·DTO·매퍼·컨버터를 그대로 재사용한다.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 리팩토링 | `0_shared_logic_to_runtime.md` | import 공유 로직(DTO/매퍼/컨버터/envelope 파싱)을 Editor asmdef → 런타임으로 이동. 동작 무변경 |
| 1 | 구현 | `1_enemy_catalog_and_refresh_service.md` | `EnemyCatalog` 신설 + 런타임 갱신 서비스 (fetch→parse→카탈로그 id 매칭 apply) |
| 2 | 구현+wiring | `2_lobby_button_wiring.md` | 로비(Outgame) 버튼 + 결과 표시 + 씬 배선 + Play/실기기 검증 |
| 3 | 구현 | `3_dreamcatcher_runtime_refresher.md` | (2026-07-13) 드림캐쳐 확장 — `DcSheetRuntimeRefresher` (6탭 fetch→in-memory apply, 카탈로그+Active/스킬/config 참조) + EditMode 테스트 |
| 4 | 구현+wiring | `4_dreamcatcher_lobby_button.md` | (2026-07-13) `IRuntimeRefresher` 인터페이스로 버튼 일반화 + "IMPORT UNIT"/"IMPORT DREAMCATCHER" 2버튼 씬 배선 |
| 6 | 구현+wiring | `6_import_all_button.md` | (2026-07-15) `AllRuntimeRefresher` composite (8탭 fan-out+조인) + "IMPORT ALL" 3번째 버튼 |
| 7 | 구현+wiring | `7_login_auto_import.md` | (2026-07-15) 로그인 성공(`onSignedIn`) 시 전체 임포트 1회 자동 실행. 선행 unit 6 |
| 8 | 인계 | `8_handoff_summary.md` | (2026-07-15) units 6~7 인계 — 계약·함정·잔여 |

## Feature-wide 계약

- **런타임 적용은 메모리 한정**: `AssetDatabase`/`SetDirty`/`SaveAssetIfDirty` 호출 금지 (에디터 전용 API). 앱 세션 동안 유지, 재시작 시 빌드값 복귀 — 최신값이 필요하면 다시 버튼.
- **id 매칭 소스는 카탈로그**: Defender = 기존 `DefenderCatalog`, Enemy = 신설 `EnemyCatalog` (동일 패턴, 9종 asset 등록). AssetDatabase 스캔은 에디터 임포터 전용으로 남는다.
- **노출 게이트**: `Debug.isDebugBuild || Application.isEditor` 일 때만 버튼 표시. 릴리즈 빌드에서 완전 숨김 (dev API 주소 노출 최소화).
- **부분 갱신·투영 규칙은 에디터 임포터와 동일**: 같은 매퍼/컨버터/`AttackOutputStats` 투영 코드 공유. 규칙이 갈라지면 안 된다.
- **에디터 Play 중 사용 주의**: 메모리 SO 갱신이 Play 종료 후에도 로드된 asset 인스턴스에 남는다(디스크 미저장, 도메인 리로드 시 복귀). 에디터에서의 영구 반영은 기존 `Window/Wassup/Unit Stat Import` 를 쓴다.
- **실패 처리**: 시트별 독립 — 한쪽 실패 시 성공한 쪽만 적용하고 실패 사유(errorDetail)를 UI에 표시. 네트워크 불가 시 기존 값 유지 (게임 진행 차단 금지).

## 후속 후보

- ~~앱 시작 시 자동 fetch (버튼 없이 항상 최신)~~ → unit 7 로 승격 (2026-07-15). 트리거는 "앱 시작" 이 아니라 로그인 성공 seam (`LoginPanelView.onSignedIn`) — `outgame-login-gate` 가 로비 진입 앞에 인증을 두고 있어 그 지점이 "부팅 직후 1회" 의 실제 표현이다.
- 갱신 결과 상세(변경 diff) UI 표시 — unit-stat spec 백로그의 dry-run 프리뷰와 통합 가능
- 릴리즈용 밸런스 배포 채널 (버전 관리/서명) — 라이브 기능으로 승격 시 별도 spec
- **제네릭 `ScriptableCatalog<T>` 베이스** [S] · Defender/Enemy/Dreamstone/DreamcatcherCard 카탈로그 4곳이 units/ById 패턴 중복 (simplify 리뷰 2026-07-06). id 접근 인터페이스가 필요해 현 규칙(과도한 추상화 금지)상 보류 — 5번째 카탈로그가 생기거나 카탈로그 공통 기능이 늘면 승격
