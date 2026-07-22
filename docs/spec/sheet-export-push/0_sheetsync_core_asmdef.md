# 0. Wassup.SheetSync 코어 asmdef 신설 (순수 추가)

## 목적

이식 가능한 **게임 무의존 sheet-sync 코어**를 자체 asmdef 로 세운다. push 에 필요한 POST transport + 응답 envelope 만 **신규 파일로** 담는다. **working import(`SheetFetcher`/`SheetEnvelopeParser`/`ApiEnvelope`)는 전혀 건드리지 않는다** — 순수 추가라 회귀 위험 0 (사용자 결정 2026-07-22). read 이관·중복 제거는 후속 후보.

## 변경 대상

- 신규: `Assets/_Project/Scripts/SheetSync/Wassup.SheetSync.asmdef` — refs 없음, 플랫폼 중립(런타임-capable), Newtonsoft 는 auto-ref.
- 신규: `Assets/_Project/Scripts/SheetSync/SheetEnvelope.cs` — 자체 최소 봉투 파서.
- 신규: `Assets/_Project/Scripts/SheetSync/SheetHttp.cs` — POST transport.
- 수정(1줄): `Assets/_Project/Editor/UnitStatImport/Wassup.Editor.UnitStatImport.asmdef` — `Wassup.SheetSync` 참조 추가(push 클라이언트가 유닛 3 에서 소비).
- **불변**: `Wassup.Runtime.asmdef`(참조 추가 불요 — push 는 에디터 전용), `Core/Api/ApiEnvelope.cs`, `Data/StatImport/*`.

## 구현

1. `Wassup.SheetSync` asmdef — `"references": []`, `includePlatforms:[]`(중립), `overrideReferences:false`(auto-ref Newtonsoft 픽업).
2. `SheetEnvelope.TryGetData(body, out JToken data, out string error)` — `{success,data,errorDetail}` 를 Newtonsoft 로 파싱. 빈 바디/비 JSON/`success!=true`/`data` 없음 → false + error 문구(`errorDetail.errorCode/errorMessage/detailMessage` 조합). `DateParseHandling.None`. `data` 는 JToken(배열·객체 무관, push 응답은 객체) 그대로 반환. `Core/Api` 참조 없이 독립 — 동일 wire shape 를 두 모듈이 독립적으로 읽는 의도된 경계.
3. `SheetHttp.Post(url, jsonBody, onDone)` — 기존 `SheetFetcher.Fetch` 의 에디터 검증된 비동기 스타일 미러(`SendWebRequest()` + `operation.completed` + `Dispose()`). `UnityWebRequest.kHttpVerbPOST` + `UploadHandlerRaw`(UTF-8) + `Content-Type: application/json` + `timeout=30`. HTTP 에러여도 바디 보존(Apps Script 는 에러도 JSON 반환). `Result{body, transportError}`. GET 은 소비처 없어 미포함.

## 완료 기준

- [ ] compile 성공 — 신규 asmdef 에서 Newtonsoft(`Newtonsoft.Json.Linq`) 해석됨. 안 되면 `precompiledReferences:["Newtonsoft.Json.dll"]`+`overrideReferences:true` 로 명시.
- [ ] read_console 에 신규 파일발 CS 에러 0. import 관련 파일 무변경(`git status` 로 `Data/StatImport/`·`Core/Api/` 미변경 확인).
- [ ] EditMode 기존 `UnitStatImport` 스위트 여전히 통과(회귀 없음 — 애초에 import 를 안 건드림).
- [ ] 확인 일자 + 커밋 해시 기록.
