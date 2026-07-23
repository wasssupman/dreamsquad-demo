# 4 — 짧은 탭 · DcInspect 경합 정리

## 목적

"유닛 터치 = 유닛 상태 화면" 요구를 **기존 인스펙트(`DcInspectController`)와 충돌 없이** 성립시킨다.
이 spec 은 상태 화면을 새로 만들지 않는다 — 입력 소유권 정리가 전부다(풀 상태 화면은 후속 후보).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (탭 양보 확인)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` (필요 시 최소 접촉)

## 구현

1. **현행 파악 선행**: `DcInspectController` 가 배치 유닛의 어떤 제스처(탭? 홀드?)를 어떤 조건에서
   소비하는지 실측 → 이 문서 하단에 확인 결과 기록 후 진행.
2. **소유권 규칙** (README 계약 10 구체화):
   - 짧은 탭(홀드 임계 전 릴리즈) → relocation 컨트롤러는 **불소비** → DcInspect 기존 동작이 그대로 발화.
   - 홀드 1초 → 이동모드가 소비. DcInspect 가 홀드 계열 제스처를 쓰고 있다면 임계·우선순위를 조정해
     두 기능이 겹치지 않게 (조정 방식은 실측 후 결정, 원칙: 기존 UX 후퇴 금지).
   - 이동모드 중에는 DcInspect 진입 차단 (모드 배타).
3. **DcInspect 가 탭을 안 쓰는 경우**: 짧은 탭은 no-op 유지. 간이 정보 표시가 필요하다는 판단이 서면
   이 spec 에서 만들지 않고 후속 후보(풀 유닛 상태 화면)로 보낸다 — 스코프 엄수.

## 실측 결과 (2026-07-24)

- `DcInspectController` 는 **press 다운 프레임**(`wasPressedThisFrame`)에 즉시 유닛을 픽킹해
  인스펙트(패널+줌+슬로모 lease)를 연다 — release 규약이면 카드 드래그 커밋 제스처가 패널을
  열어버려서(계약 3). 픽킹은 2단(스프라이트 스크린 렉트 → 발밑 셀).
- 양보 파트너(`Blocked()` → `Close()`): `IsAiming` · 손패 오픈 · 드래그/arm/방향지정
  (`DefenderSelector.DragController` 경유). 실행 순서 -50.
- **결론: 유저 원 명세("터치=상태 화면, 1초 홀드=이동모드")가 기존 인스펙트+신규 홀드의 자연
  합성으로 그대로 성립** — 다운 즉시 인스펙트가 열리고, 홀드가 1초에 도달하면 이동모드 진입,
  그 순간 인스펙트는 `Blocked()` 신규 조건(`relocationController.InMoveMode`)으로 자동 Close.
  필요 조정은 이 가드 1줄 + SerializeField 1개 + 씬 배선뿐이었다.

## 완료 기준

- [x] 컴파일 클린
- [x] 짧은 탭 = 기존 인스펙트 동작 그대로(재배치 컨트롤러는 홀드 전 릴리즈 불소비 — unit 1 테스트),
      홀드 = 이동모드(unit 1 테스트), 이동모드 중 인스펙트 미발동 = `Blocked()` 가드(기존 파트너와
      동일 패턴 — 코드 경로, 시각 확인은 unit 3 사용자 Play 게이트에 합류)
- [x] 실측 결과 기록(위 섹션)
- [x] 씬 배선: `DcInspect.relocationController` fileID 비영 확인

2026-07-24 자동 검증 통과 (relocation PlayMode 스위트 4/4 회귀 없음). 부수: 스모크의 시너지
어서션을 총합 `damageMul` → **origin=Synergy 슬롯 직독**으로 견고화(랜덤 기믹의 데미지 배율이
총합을 오염시키는 간헐 실패 관측·수정).
