# 3. Handoff Summary

## Commit

- `2d6bdd19` docs(spec): runtime-stat-refresh spec
- `6e44868e` refactor(stat-import): 공유 로직 런타임 이동 (unit 0)
- `d307a540` feat(stat-refresh): EnemyCatalog + 런타임 갱신 서비스 (unit 1)
- `95ea1e92` feat(stat-refresh): 로비 스탯 갱신 버튼 + 씬 배선 (unit 2)
- (+ 직후 simplify 리팩토링 커밋 — 4각도 리뷰 반영, 병렬 fetch)

## Implemented

- 로비(OutgameScene) "스탯 갱신" 버튼: 시트 2탭 **병렬** GET → 메모리 내 카탈로그 SO 갱신 → 다음 전투부터 적용
- dev 게이트: `!Debug.isDebugBuild && !Application.isEditor` 이면 버튼 GO 비활성 (릴리즈 미노출)
- 공유 코어(런타임 `Wassup.Data.StatImport`): `SheetEnvelopeParser`(envelope+로그 정책) / `SheetFetcher`(fetch+병렬 join) / `UnitStatApplier`(BuildIndex/BuildPayload/Apply/투영) — 에디터 임포터와 단일 코드
- `EnemyCatalog`(신규, 9종 등록) — Defender 는 기존 `DefenderCatalog`
- 중복 id 정책 통일(전체 skip) + 기존 3개+ 중복 재등록 버그 수정 (테스트 고정)

## Key Files

- `Assets/_Project/Scripts/Data/StatImport/` — 공유 코어 4파일
- `Assets/_Project/Scripts/Core/UnitStatRuntimeRefresher.cs` — 씬 로컬 서비스 (`ApplyBodies` 가 테스트 가능한 순수 코어)
- `Assets/_Project/Scripts/UI/Outgame/StatRefreshButtonView.cs` + OutgameScene 배선 (StatRefreshButton/StatRefreshResult/UnitStatRefresher)
- `Assets/_Project/Editor/UnitStatImport/` — 에디터 창(임포트/익스포트 UI + AssetDatabase 스캔 + 디스크 저장)만 잔류

## Verified

- EditMode 523개 통과 (무관 상시 실패 1건 제외), compile 0 error
- Play 2회: 버튼 클릭 → 결과 라벨 `Matched 25, unmatched 0, fields applied 336, projected 19, skipped 0`, 콘솔 에러 0 (병렬 fetch 전/후 동일)
- 씬 YAML: 참조 6개 전부 non-zero fileID

## Notes

- **런타임 갱신은 메모리 한정** — Save 계열 호출 금지 계약. 에디터 Play 중 사용 시 로드된 asset 인스턴스에 값이 남는다(도메인 리로드로 복귀). 에디터 영구 반영은 `Window/Wassup/Unit Stat Import`.
- 에디터/런타임의 파싱·적용·로그 문자열은 전부 공유 코어에서 나온다 — 규칙 변경 시 그쪽만 수정.
- Play 검증은 임시 `[MenuItem]` + `execute_menu_item` 패턴 사용 (execute_code 고장 — lessons/01 참조), 검증 후 삭제됨.

## Follow-up

- 실기기 Development Build 1회 확인 + 릴리즈 빌드 버튼 미노출 확인 (unit 2 체크박스 잔여)
- README 후속 후보: 시작 시 자동 fetch / diff UI / 릴리즈 배포 채널 / 제네릭 카탈로그 베이스
