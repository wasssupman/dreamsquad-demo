# 9 — 최종 인계 요약

## Commit

- `12f5b644` unit 7 rev — 부착 제한 필드 3개를 `attachType` + `attachValue`로 수렴
- `(이 커밋)` unit 8 — 제한 카드 0장에서도 Push 신규 컬럼 부트스트랩

## Implemented

- 카드 부착 제한은 `None` / 클래스 / 특정 유닛을 2필드로 표현한다.
- UI 판정과 실제 커밋 preflight가 같은 순수 판정을 사용한다.
- 거절 시 카드를 소비하지 않고 각성 게이지도 차감하지 않는다.
- `DcCards` import/export가 `attachType`과 `attachValue`를 왕복한다.
- 제한 없는 카드의 일반 export는 두 키를 생략해 `None` 노이즈를 만들지 않는다.
- Push payload에만 `id` 없는 헤더 시드를 넣어 첫 Push에서 두 컬럼을 자동 생성한다.
- 배포된 Apps Script는 시드의 키로 헤더를 만든 뒤 `id` 결측으로 데이터 행을 건너뛴다.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
- `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs`
- `Assets/_Project/Editor/UnitStatImport/DcAttachRequirementValidator.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetAttachRequireExportTests.cs`

## Verified

- 최종 기능 회귀: EditMode 1343건 중 1341 pass / 0 fail / 기존 Ignore 2.
- 부착 게이트와 무차감 보장 PlayMode 신규 2건 pass.
- validator 실사: 카드 44장, 부착 제한 위반 0건.
- unit 8: Unity compile 에러 0, Push payload 회귀 2/2 pass.
- 헤더 시드는 정확히 1개이며 `attachType` / `attachValue` 외 데이터를 갖지 않는다.
- 실제 카드 행 수는 시드 때문에 증가하지 않는다.

## Notes

- 일반 Dreamcatcher export에는 헤더 시드를 넣지 않는다. Push 병합 payload 전용이다.
- 제한 해제는 `attachType=None` 명시가 유일한 수단이며 빈 셀은 기존 값 유지다.
- 기존 카드 44장은 모두 제한 없음이라 런타임 동작 변화가 없다.
- 첫 import에서 새 직렬화 필드 때문에 카드 에셋 YAML 대량 diff가 생길 수 있다.

## Follow-up

- 최초 운영 `Push to Sheet`에서 `DcCards` 오른쪽에 두 컬럼이 생기고 가짜 카드 행이
  생기지 않는지 확인한다. 외부 데이터 변경이라 이번 커밋에서는 실행하지 않았다.
- 첫 운영 import payload에서 빈 텍스트 셀이 키 생략인지 빈 문자열인지 확인한다.
- 실제 제한 카드가 저작되면 손패와 덱빌더의 "○○ 전용" 문안을 육안 확인한다.
