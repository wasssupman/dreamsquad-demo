# 2 — 미세 커브 ② (안착 flex)

## 목적

카드가 부채꼴에 안착하는 순간 살짝 휘었다 펴지는 flex 를 주어 탄성·종이 질감을 더한다.
③ 꼬깃꼬깃(별도 spec)의 축소판 — 여기선 "부드러운 한 번의 굽힘→펴짐"까지만.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- (버텍스 커브 채택 시) 신규 경량 컴포넌트 `UiCardBend`(같은 폴더).

## 구현 (택1 — 안착 후 판단)

**A. 진짜 버텍스 커브(우선 시도)**: `IMeshModifier` 컴포넌트 `UiCardBend` 로 카드 `Image` 쿼드를
세로 N분할 격자로 재테셀레이션하고 `Bend` 0..1 로 중앙 버텍스를 곡선 변위(UV 보존). 딜 안착 시
`Bend` 를 짧게 `0→peak→0` 트윈(index 결정론, PrimeTween `Tween.Custom`). 메시 에셋·셰이더 불요.

**B. squash-stretch 폴백**: A 가 `preserveAspect`/레이아웃과 싸우거나 ROI 낮으면, 커브를
포기하고 안착 순간 `scaleY` squash→stretch 오버슛 + 미세 skew 로 flex 감을 낸다(4버텍스로 충분).
이 경우 **진짜 기하 커브는 ③ 스펙으로 이관**(서브디바이드 토대를 그쪽과 공유).

- 어느 쪽이든 flex 는 **안착 타이밍 1회**, 카드당 index 스태거. 텍스트/코스트는 flex 대상에서 제외
  하거나 부모만 굽혀 라벨은 평면 유지(가독성).

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play — 카드가 안착하며 한 번 탄력적으로 flex 후 평평해짐. 정지 상태에선 왜곡 0.
- 카드 5장 동시 안착에도 프레임 드랍 체감 없음(모바일 리스크 없는 A/B 중 택).
- 채택안(A/B)과 사유를 handoff 에 1줄 기록. B 선택 시 ③ 이관 확인.
