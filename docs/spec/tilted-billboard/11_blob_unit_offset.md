# 11 — 블롭 유닛별 XZ 미세조정 노브

> **선행 조건**: unit 10 의 계측·투영 이후에도 개별 유닛에 남는 어긋남이 있을 것.
> 자동 해가 전부 맞으면 이 노브는 «나중을 위한» 추상이 된다(제약 8) — 그때는 착수하지 않는다.
> 요구사항 자체는 유효하다(사용자 2026-08-28). 미루는 것은 **순서**뿐이다.

## 목적

unit 7·10 의 자동 해가 리그 사정으로 어긋나는 유닛을 **데이터로** 교정할 길을 연다.
자동 해가 맞는 유닛은 아무것도 저작하지 않는다(기본 0 = 무회귀).

## 변경 대상

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs`
- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- `Assets/_Project/Scripts/Presentation/BlobShadow.cs`

## 구현

### 새 인터페이스 멤버

```csharp
// 블롭 그림자 XZ 미세조정(보드 공간, 타일 단위). 기본 0 = 자동 해에 맡김.
Vector2 BlobShadowOffset { get; }
```

**기존 `SpineVisualOffset` 을 재활용하지 않는 이유** — 그건 *캐릭터 비주얼*을 옮기는 노브이고,
`transform.position` 에 더해진다(`SpineUnitView.cs:217`). BlobShadow 는 그 자식이라
XZ 성분을 넣는 순간 **그림자가 셀에서 같이 밀린다**(현재 유닛 에셋이 전부 0 이라 잠복 상태).
성격이 다른 두 보정을 한 필드에 겸직시키면 그 결함이 살아난다.

### 두 구현체 모두 실제 필드로 저작

`AttackUnitData`·`DefenderUnitData` 양쪽에 `public Vector2 blobShadowOffset;` 을 두고 그대로 반환한다.
**한쪽을 `Vector2.zero` 하드코딩으로 막지 않는다** — `SpineVisualOffset` 이 `DefenderUnitData.cs:342`
에서 "본 spec 범위 밖"으로 막힌 결과 지금 디펜더엔 그 노브가 아예 없다. 같은 길을 다시 가지 않는다.

### 적용 지점

unit 10 의 `SolveGroundAnchor` 결과 **뒤에**(unit 10 이 폐기됐으면 unit 7 의 평면 투영 뒤에) 보드 XZ 로 가산한다.
투영 앞에 더하면 offset 이 카메라 각도에 따라 배율이 달라져 저작 직관이 깨진다
(«앞으로 0.1타일» 이 pitch 마다 다른 양이 된다).

Y 성분은 두지 않는다 — 계약 7. 평면이 Y 를 소유한다.

## 완료 기준

- [ ] 컴파일 통과. 기존 에셋 전부 `blobShadowOffset = (0,0)` 이라 **화면 변화 0**
- [ ] Assets lane(5초) 초록 — SO 필드 추가로 깨지는 에셋 단언 없음
- [ ] 임의의 유닛 1종에 `(0, 0.2)` 를 넣고 Play → 그림자만 보드 +Z 로 0.2타일 이동, **캐릭터는 제자리**
- [ ] 값을 0 으로 되돌리면 unit 10 결과와 픽셀 동일
