# 3. Push 클라이언트 + 응답 파싱

## 목적

병합 payload(유닛 2)를 Apps Script `/exec` 로 POST 하고, 반영 결과 봉투를 파싱해 사람이 읽는 요약으로 만든다. 전송·봉투 검증은 SheetSync 코어(유닛 0), 결과 해석은 여기.

## 변경 대상

- 신규: `Assets/_Project/Editor/UnitStatImport/SheetPushClient.cs`.

## 구현

- `SheetPushClient.Push(scriptUrl, payloadJson, onDone)`:
  1. `SheetHttp.Post(scriptUrl, payloadJson, ...)` (유닛 0).
  2. transport 에러 → 그 문구 그대로 실패 리포트(무음 금지, import 창 규칙과 동일).
  3. `SheetEnvelope.Parse(body)` → `data` JToken. `success!=true` → errorDetail 리포트.
  4. `data.results` = `{ "<탭>": {updated:int, added:int, orphans:[키]} }` 를 순회해 로그 조립:
     - 탭별 `updated/added` 카운트.
     - `orphans` 비어있지 않으면 **경고 강조** + 키 목록(시트엔 있고 이번 payload 엔 없는 행 — SO 에서 지웠거나 시트에만 있는 것. 삭제 안 됨, 사람이 판단).
- 응답 shape 계약(Feature-wide): `{success, data:{results}, errorDetail}`. Apps Script(유닛 5)가 이 shape 로 반환.
- **비파괴 불변식**: 클라이언트는 절대 삭제를 요청하지 않는다. 고아는 리포트 전용. Apps Script 도 삭제 안 함(유닛 5).

## 완료 기준

- [ ] EditMode: 응답 파서가 (a) 정상(updated/added/빈 orphans), (b) 고아 있는 응답, (c) `success:false`+errorDetail, (d) transport 에러 4케이스를 올바른 요약/실패 문구로 처리.
- [ ] compile + 확인 일자·커밋 해시 기록.

> 참고: 실제 네트워크 왕복 검증은 유닛 5(Apps Script 배포 후). 이 유닛은 파서 단위 테스트까지.
