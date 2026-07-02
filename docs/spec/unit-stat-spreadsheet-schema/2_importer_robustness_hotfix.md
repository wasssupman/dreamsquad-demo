# 2. Importer Robustness Hotfix

## 목적

unit 1 구현에 대한 리뷰(2026-07-02, Fable 교차 리뷰)에서 확인된 결함 5건을 해소한다. 기능 추가 없음 — unit-stat-projection(투영) spec 착수 전 선행 커밋.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs`
- `Assets/_Project/Editor/UnitStatImport/DefenderClassFlagsJsonConverter.cs`
- `docs/spec/unit-stat-spreadsheet-schema/0_json_schema_contract.md` (계약 표현 정정)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` (신규 테스트)

## 구현

- **② 전역 SaveAssets 제거**: `ApplyPayload`의 `AssetDatabase.SaveAssets()`(전역 dirty flush — 사용자 WIP까지 저장) → 매칭·갱신된 SO에만 `AssetDatabase.SaveAssetIfDirty(so)` 개별 호출.
- **③ 도메인 리로드 고착 해제**: `_requestInFlight`가 EditorWindow 직렬화로 리로드를 살아남고 completed 콜백은 유실 → `OnEnable()`에서 `false` 리셋.
- **⑥ 중복 id 검출**: (a) asset 측 — import 시작 시 id→asset 사전 구축, 같은 id 에셋 2개+ 발견 시 해당 id 전체 skip + 로그 (모호한 타깃에 쓰기 금지). (b) payload 측 — 같은 id 행 2개+ 이면 첫 행만 적용, 이후 행 skip + 로그.
- **⑤ enum 파싱 일관화**: flags 컨버터 `Enum.Parse`를 case-insensitive로 완화(Json.NET 기본 enum 파싱과 동일 규칙). 미지 멤버명은 `JsonSerializationException`으로 거부(기존 유지, 메시지 개선). 계약 문서의 "대소문자 유지" → "멤버명 표기 권장은 C# 그대로, 수용은 case-insensitive" 로 정정.
- **④ displayName 계약 명확화**: 문서의 "참조용" → "표시명 — 제공 시 덮어씀(부분 갱신 규칙 동일 적용)" 로 정정. 코드 변경 없음(매퍼 동작이 정답).
- 부수 정리: 계약 문서의 "향후 importer가 검증" (Everything 혼용 금지) — 이미 구현됐으므로 현재형으로 정정.

## 완료 기준

- [ ] compile 오류 없음
- [ ] 기존 테스트 7종 + 신규 3종 통과: 중복 payload id 후행 skip / displayName 제공 시 덮어씀 / flags 소문자 멤버명 수용
- [ ] 수동: import 실행 후 무관 dirty asset이 디스크에 저장되지 않음 확인 (또는 SaveAssetIfDirty 전환 코드 리뷰로 갈음)
