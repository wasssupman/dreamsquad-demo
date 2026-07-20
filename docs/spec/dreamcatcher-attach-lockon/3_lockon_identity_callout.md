# 3 · Lock-On Identity Callout (레이어 D · 핵심)

## 목적

pick 이 손가락 밑이라(README 설계 배경) 유닛 위 신호는 전부 가린다. **락온 유닛 위 손끝 반경 밖 오프셋**에 **아이콘 + 이름 + 부착수(X/3)** 를 띄워, "지금 어느 유닛에, 붙일 수 있는지"를 손가락에 안 가리게 확정한다(불편 ②·③). 이 spec 의 최우선 전제("확연히 인지")를 성립시키는 주신호.

## 변경 대상

- **신규** `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusCallout.cs` — 콜아웃 프레젠터(애셋-프리 UI: 배경 쿼드 + Image 아이콘 + TMP 이름/수치)
- `DreamcatcherHandView.cs` — 콜아웃 `Create`·소유, 폰트(battle-ui 한글 컨벤션) 배선
- `DreamcatcherCardDragSlot.cs` — 락온 entity 확정 시 콜아웃 `Show(iconInfo)`/`Hide` (리티클과 같은 entity·같은 히스테리시스 결과 공유)

## 구현

- **내용**: 타겟 유닛 아이콘(defender SO 아이콘 재사용 — 유닛 스트립/DcIconStrip 소스) + 이름 + **부착수 배지 X/3**(`maxAttachPerUnit`). 유효 = `calloutValidTextColor`, full(3/3) = `calloutFullTextColor` 강조(색+수치로 무효 이중 표기, 계약 #7).
- **위치**: 락온 유닛 렉트 상단 + `calloutScreenOffset`(손끝 반경 초과). 콜아웃은 리티클보다 **위 레인**(계약 #3). 화면 밖이면 `calloutEdgeClampPad` 로 clamp(가로 화면 가장자리 유닛 대응).
- **정체 소스**: 리티클(unit 2)과 **동일 entity**(같은 히스테리시스 통과 결과)를 소비 — 콜아웃과 리티클이 다른 유닛을 가리키지 않게. 유닛 아이콘/이름은 bridge 에서 defender 의 SO 참조로 조회(읽기 전용).
- **전환·페이드**: 정체가 바뀌면 `calloutFadeSec` 로 교체(래칭 덕에 자주 안 바뀜). 락온 해제 시 `Hide`.
- **확정 훅(unit 6 연동)**: 커밋 성공 시 콜아웃에서 "찰칵" 펀치(체크/스케일 팝) — 확정 가시성의 주 초점(손가락 밖).

## 완료 기준

- 밀집·손가락 가림 상태에서 **콜아웃만으로 "어느 유닛인지 + 붙일 수 있는지(X/3)"가 즉시 읽힘**(손끝 밖 오프셋 확인).
- 리티클과 항상 **같은 유닛**을 가리킴(정체 불일치 없음). full 유닛은 3/3 강조.
- 화면 가장자리 유닛에서 콜아웃이 화면 안으로 clamp. 정체 전환 시 부드러운 페이드.
- `Close`/`ForceClose`/`OnDisable`/`OnPhaseChanged` 하드 클리어(계약 #10). 콘솔 클린.
