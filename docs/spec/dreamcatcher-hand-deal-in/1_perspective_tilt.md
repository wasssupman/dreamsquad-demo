# 1 — 입체감 ① (원근 틸트)

## 목적

딜되는 카드에 원근 틸트를 얹어 "두께 있는 카드가 카메라 쪽으로 기울며 날아와 눕는"
입체감을 준다. 메시·셰이더 없이 `RectTransform` 3D 회전만 사용한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

1. **캔버스 원근 확인(선행, 라이브)**: 기존 `FlipRoutine` 이 X축 회전으로 카드를 edge-on
   소실시키는 게 이미 성립하므로 손패 캔버스는 rotated rect 를 foreshorten 한다(원근 존재).
   Play 에서 딜 시작 카드에 X 틸트를 임시로 크게 줘 원근이 도는지 육안 확인.
   - 만약 Overlay 라 납작하면(원근 부족): **캔버스 모드 교체는 하지 않는다**(드래그 raycast/sorting
     회귀 위험). 틸트각을 키우고 스케일/그림자로 입체감을 보강하는 선에서 마감. 모드 교체가
     정말 필요하다고 판단되면 정지하고 질문.

2. **딜 궤적 틸트**: unit 0 의 딜 시퀀스에 카드 시작 자세를 `localEulerAngles = (startTiltX, 0, homeRotZ±)`
   로 세팅하고, 안착 트윈에 `Tween.LocalEulerAngles`(또는 X 성분 별도 트윈)로 `startTiltX → 0` 복원.
   시작 시 위로 선 카드가 딜되며 보드로 눕는 인상.

3. **안착 오버슛 틸트**: OutBack 위치 안착과 동기로 X 틸트가 0 을 살짝 지나쳤다 복귀(미세)해
   "탁 눕는" 무게감. 값은 작게(≤6°) — 과하면 텍스트 가독성 저하.

4. **튜닝 SerializeField**: `dealStartTiltX=55f`, `dealSettleTiltOvershoot=4f`. 카드 있는 슬롯만 적용.

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play — 카드가 딜되며 세워진 상태에서 눕는 원근이 보이고, 안착 시 미세하게 탁 눕는 느낌.
- 안착 후 카드는 X 틸트 0(정면)으로 복귀해 이름/코스트 텍스트 가독성 유지.
- 캔버스 모드 미변경(드래그 타겟팅·sorting 무회귀 — 카드 드래그/타일 포커스 정상).
