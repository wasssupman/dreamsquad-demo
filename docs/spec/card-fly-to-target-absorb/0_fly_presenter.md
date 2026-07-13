# 0 — 카드 비행 presenter

## 목적

부착 확정 순간, 손패 카드(UGUI)가 타겟 스크린 좌표로 가속 비행해 찰싹 스쿼시 후 즉시 dissolve.
(유닛 묵직 반응은 unit 1, 타일 일반화는 unit 2.)

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Dreamcatcher/CardFlyPresenter.cs`(가칭) — UGUI 고스트 카드 fly.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 커밋 성공 시 발사 호출.

## 구현

1. **트리거**: `CommitNow(commit)` 의 `ok == true` 분기(성공)에서, Refresh/Close 전에 발사. 실패/취소는 발사 안 함.
   발사에 필요한 정보: 카드 손패 rect(시작), 타겟 host 엔티티(유닛) 또는 셀(타일), 카드 art(고스트 비주얼).
2. **고스트 카드**: 손패 카드 위치·크기로 UGUI 고스트 생성(오버레이 최상단). 비주얼 = `card.art`(또는 `UiCardFaceMesh`
   페이스 스냅샷 — 열린 결정). raycastTarget=false.
3. **타겟 스크린 좌표(추적)**: 유닛 = `bridge.TryGetUnitViewAnchor(host)` 월드 앵커(+머리 offset) → 카메라 project.
   **매프레임 재투영**(유닛 행진). 앵커 소실(사망) 시 현재 위치에서 즉시 dissolve.
4. **비행 트윈**(PrimeTween): 시작→타겟 `Ease.InBack`(가속), duration ~0.28s. 접근 중 회전 살짝 + 임팩트 직전
   scale 살짝 확대(anticipation). 곡선 아크는 선택(2-스텝 or 제어점).
5. **찰싹 splat**: 도착 프레임에 scaleX↑ scaleY↓(splat) 1~2프레임 → 즉시 dissolve(축소+페이드 ~0.08s) → 파괴.
   도착 시점에 unit 1 의 임팩트 반응 콜백 호출.
6. **정리**: 페이즈 이탈/teardown 에 진행 중 고스트 정리(leak 방지, PrimeTween Sequence stop).

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 유닛에 스와이프 확정 시 카드가 손패에서 그 유닛으로 가속 비행 → 찰싹 → 즉시 사라짐(머무름 없음).
- 유닛이 움직여도 정확히 그 유닛에 안착(추적).
- 취소/실패 커밋은 비행 없음(카드 손패 복귀).
- 페이즈 이탈 시 고스트 잔류 0.
