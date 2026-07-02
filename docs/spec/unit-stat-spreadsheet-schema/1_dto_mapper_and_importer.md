# 1. AttackUnitData.id + DTO/Mapper/Importer

## 목적

`0_json_schema_contract.md` 계약을 실제로 소비하는 코드를 구현한다. 기획파트의 스프레드시트 컬럼 구성이 향후 바뀔 수 있다는 전제 하에, 필드 추가/삭제가 최소 diff로 흡수되도록 설계한다 (사용자 확인: "스키마 구조가 달라질 수 있음을 감안하고 작업").

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `id` 필드 추가 (Defender와 동일 패턴)
- `Assets/_Project/Data/Enemies/*.asset` (9종) — `id` 값 채움 (파일명 접미사 lowercase, Defender 컨벤션과 동일)
- `Assets/_Project/Editor/UnitStatImport/` (신규)
  - `UnitStatImportDto.cs` — `UnitStatImportPayload`/`DefenderStatDto`/`EnemyStatDto`
  - `DefenderClassFlagsJsonConverter.cs` — `targetClassMask` 문자열 배열 ↔ `[Flags]` enum
  - `UnitStatFieldMapper.cs` — 리플렉션 기반 부분 갱신 매퍼
  - `UnitStatImportWindow.cs` — `Window/Wassup/Unit Stat Import` EditorWindow
  - `Wassup.Editor.UnitStatImport.asmdef` — Editor 전용, `UnitStatImport/` 서브폴더에만 적용 (`Editor/MapGrid/`는 영향 없음)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` (신규, 7 테스트)
- `Assets/_Project/Tests/EditMode/Wassup.Tests.EditMode.asmdef` — `Wassup.Editor.UnitStatImport` + `Newtonsoft.Json.dll` 참조 추가
- `Packages/manifest.json` — `com.unity.nuget.newtonsoft-json` 3.2.2 추가 (JsonUtility는 문자열 enum을 역직렬화하지 못해 채택)

## 구현

- **스키마 변경 내성**: DTO 필드명은 SO 필드명과 항상 동일하게 짓는다(계약). `UnitStatFieldMapper.ApplyNonNullFields`는 이름 매칭 리플렉션으로 non-null DTO 필드만 SO에 복사한다 — 새 스탯 컬럼이 추가되면 DTO에 같은 이름 필드 하나만 추가하면 되고, 매퍼/임포터는 무수정.
- **부분 갱신**: DTO 필드는 전부 nullable. JSON에 키가 없으면 null → 매퍼가 건너뜀 → 기존 SO 값 유지. `id`는 매칭키이므로 복사 대상에서 항상 제외.
- **targetClassMask**: `DefenderClassFlagsJsonConverter`가 문자열 배열을 비트 OR로 조합. `["Everything"]`과 다른 클래스명을 섞으면 `JsonSerializationException`으로 명시적 실패(계약 문서의 "혼용 금지" 실제 강제).
- **HTTP 호출**: `UnityWebRequest.Get` + `SendWebRequest().completed` 콜백. async/await(Awaitable) 대신 이벤트 콜백을 쓴 이유 — Editor(비-Play) 컨텍스트에서의 Awaitable 동작이 불확실해 보수적인 패턴 채택.
- **매칭/저장**: `id` 로 `Assets/_Project/Data/{Defenders,Enemies}/`를 스캔해 매칭. 미매칭 id는 스킵하고 결과 로그에 남김(신규 asset 자동 생성 없음). `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`로 반영.

## 완료 기준

- [x] compile 오류 없음 (`read_console`, 2026-07-02)
- [x] EditMode 테스트 7종 통과 — 문자열 enum 역직렬화, 필드 부재→null, flags 배열 조합(개별/Everything/빈배열/혼용 예외), 부분 갱신(제공 필드만 덮어씀), enum 필드 갱신, id 불변
- [x] 기존 EditMode 스위트 회귀 없음 — 411개 중 무관한 기존 실패 1건(`ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`, 이번 변경과 파일 겹침 없음) 외 전부 통과
- [ ] 실제 REST 엔드포인트 왕복 확인 — 이번 세션은 실 엔드포인트 미제공. 사용자가 `Window/Wassup/Unit Stat Import`에서 실제 URL로 확인 필요
