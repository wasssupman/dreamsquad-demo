# 13 — Billboard / PropBillboard 회전 수학 통합

## 목적

코드 리뷰(2026-06-26) follow-up `[M]`. `Billboard`(유닛)와 `PropBillboard`(프랍)가 같은
빌보드 회전 수학(정적 X 틸트 `Euler(tilt,0,0)`, Y축 LookRotation, 카메라 페이싱)을 **두 곳에
중복 구현**한다. 카메라 정책을 바꾸면 두 파일을 손봐야 함. 회전 수학만 단일 순수 함수로 추출한다.

**동작·직렬화 불변**: `BillboardMode`/`PropBillboardMode` enum은 직렬화 대상(컴포넌트
SerializeField / `PropData` 필드)이라 값·순서를 건드리지 않는다. 클래스 병합도 하지 않는다
(`PropBillboard`는 `ApplyData` 프랍 프레젠터 책임 + 자식 `visualRoot` 회전 대상이 다름).

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BillboardRotation.cs` — 신설. 순수 static 헬퍼.
- `Assets/_Project/Scripts/Presentation/Billboard.cs` — `LateUpdate` 가 헬퍼 호출로 축약.
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs` — `LateUpdate` 가 헬퍼 호출로 축약.
- `Assets/_Project/Tests/EditMode/BillboardRotationTests.cs` — 신설. 헬퍼 순수 함수 회귀 테스트.

## 구현

- **`BillboardRotation.Compute(Facing, tiltAngle, camera, worldPos, flip180) → Quaternion?`**
  - `Facing { None, Tilted, YAxis, Camera }` — 중립 표현. enum→Facing 매핑은 호출측 책임.
  - 반환 `null` = "이번 프레임 회전 갱신 안 함"(None / 카메라 없음 / YAxis 퇴화 방향). 기존 두
    컴포넌트의 "조건 미충족 시 rotation 미설정" 동작을 보존.
  - `Tilted`=카메라 무관 `Euler(tilt,0,0)`. `Camera`=`camera.rotation`. `YAxis`=수평 LookRotation.
    `flip180` 시 마지막에 Y 180° 곱.
- **호출측**: 회전 대상 선택(`Billboard`=self / `PropBillboard`=`visualRoot ?? transform`),
  카메라 lazy fetch/캐싱, tilt 출처(`Billboard`=주입 필드 / `PropBillboard`=`data.tiltAngle`),
  `flip180`(`Billboard`만 true 가능) 는 각 컴포넌트에 그대로 둔다. 헬퍼는 회전값만 만든다.

## 완료 기준

- compile 0 에러.
- EditMode `BillboardRotationTests` green (Tilted 각/None·null카메라 null 반환/flip/카메라 페이싱/YAxis 수평).
- Play(tilemap): 프랍·유닛 빌보드 회전 시각적 동일(리팩터 전후 무변화). 직렬화 에셋 무변경.

완료 확인 2026-06-26 — compile 0 에러 / EditMode `BillboardRotationTests` 8/8 pass / Play 시각 동일 사용자 확인("문제없다"). 직렬화 에셋·enum 무변경.
