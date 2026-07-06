# 1. EnemyCatalog + 런타임 갱신 서비스

## 목적

빌드에서 시트를 fetch 해 카탈로그의 SO 인스턴스에 반영하는 런타임 서비스를 만든다. Defender 는 기존 `DefenderCatalog`, Enemy 는 이 unit 에서 신설하는 `EnemyCatalog` 로 id 매칭한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/EnemyCatalog.cs` (신규) — `DefenderCatalog` 와 동일 패턴 (`AttackUnitData[] units`, `ById`, `AllIds`)
- `Assets/_Project/Data/EnemyCatalog.asset` (신규) — 9종 enemy asset 등록
- `Assets/_Project/Scripts/Services/UnitStatRuntimeRefresher.cs` (신규) — MonoBehaviour 서비스
- EditMode 테스트 (apply 로직)

## 구현

- **서비스 형태**: MonoBehaviour + SerializeField (`DefenderCatalog`, `EnemyCatalog`, base URL/시트명 2개 — 기본값은 dev API/`Defenders`/`Enemies`). Manager 싱글톤 금지 규칙 준수 — 로비 씬 로컬 컴포넌트.
- **흐름**: `Refresh(Action<string> onDone)` → UnityWebRequest GET 2회 (시트별 독립) → `SheetEnvelopeParser.ParseSheetRows<T>` → 카탈로그 id 매칭 → `UnitStatFieldMapper.ApplyNonNullFields` + `AttackOutputStats` atk/heal 투영. **SetDirty/Save 계열 호출 없음** (메모리 한정).
- **결과 문자열**: 에디터 임포터와 동일 포맷 (`Matched N, unmatched N, fields applied N, projected N, skipped N`) + 시트별 실패 사유. UI 가 그대로 표시.
- **apply 코어 분리**: 카탈로그 기반 apply 는 정적 순수 함수로 (`ApplyToCatalogs(payload, defenderCatalog, enemyCatalog) → log`) — EditMode 테스트 대상. 네트워크 부분은 테스트 제외.
- 중복 id 방어: 에디터 임포터의 asset 스캔 dedup 은 AssetDatabase 의존이므로, 카탈로그 배열 내 중복 id 는 첫 항목만 사용 + 로그.

## 완료 기준

- [ ] compile 오류 없음
- [ ] EditMode 테스트: 카탈로그 매칭 apply(갱신/미매칭 무시/중복 id 첫 항목), atk/heal 투영, 시트 한쪽 실패 시 부분 적용
- [ ] 에디터 Play 에서 `Refresh` 1회 → 로그 카운트 정상 (`Matched 25, unmatched 0`)
