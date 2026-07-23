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

## 완료 기준

- [ ] 컴파일 클린
- [ ] 에디터 Play: 짧은 탭 시 기존 인스펙트 동작이 spec 도입 전과 동일(회귀 0), 홀드 시 이동모드,
      이동모드 중 인스펙트 미발동
- [ ] 실측 결과(DcInspect 의 제스처·조건)가 이 문서에 3~5줄로 기록됨
