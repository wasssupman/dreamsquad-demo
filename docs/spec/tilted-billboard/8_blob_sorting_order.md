# 8 — 블롭 정렬 대역 되찾기

## 목적

블롭이 자기 정렬을 못 지킨다. unit 3 이 정한 «블롭은 ground 위 · 캐릭터 아래(`ShadowOrder = -5`)»
계약이 **코드에서 이미 깨져 있다** — 유닛 정렬 스윕이 매 프레임 덮어쓴다.

## 재현 (정적 확인 2026-08-28)

```
SpineUnitView.cs:299-309   var renderers = GetComponentsInChildren<Renderer>(true);
                           if (rigRoot != null && renderers[i].transform.IsChildOf(rigRoot)) continue;
                           renderers[i].sortingOrder = order;
QuadUnitView.cs:208-213     동일. 예외 없음
```

- `UpdateSortingOrder` 는 BattleBridge 가 **매 프레임** 호출한다(`BattleBridge.cs:3788`·`3796`·`3856`·`3863`·`3921`·`3927`).
- BlobShadow 는 `Attach` 에서 `SetParent(target, false)` 로 붙은 **자식**이라 이 스윕에 걸린다.
- `Attach` 가 세운 `ShadowOrder(-5)` 는 **첫 프레임에 캐릭터 order 로 덮이고 복원되지 않는다**
  (`BlobShadow` 는 `sortingOrder` 를 `Attach` 에서 1회만 쓴다).
- 무기 궤적 리그는 같은 이유로 이미 예외 처리돼 있다 — 블롭만 그 예외에서 빠졌다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs`

## 구현

스윕에서 블롭을 제외한다. 궤적 리그와 **같은 형태** — 대역을 소유한 자식은 스윕이 건드리지 않는다.

```csharp
if (renderers[i].GetComponentInParent<BlobShadow>() != null) continue;
```

`QuadUnitView.SetSortingOrder` 는 궤적 예외조차 없으므로 같은 가드를 새로 넣는다.

**대안(채택 안 함)**: 블롭이 매 LateUpdate 에 자기 order 를 다시 쓰는 방식 — 두 writer 가 매 프레임
경쟁하는 모양이 되고, 「대역을 소유한 자식은 스윕 제외」라는 기존 규약과 어긋난다.

## 완료 기준

- [x] 컴파일 통과. 코어 lane 2494 초록
- [x] Play 실측: `UpdateSortingOrder` 호출 후에도 블롭 = **−5**, 같은 유닛의 캐릭터 메시 = 379
- [ ] Play 육안: 인접한 두 유닛이 겹칠 때 그림자가 상대 캐릭터 **위로 올라오지 않는다**
- [ ] 무기 궤적을 가진 유닛의 궤적 정렬이 변하지 않는다(기존 예외 회귀)
