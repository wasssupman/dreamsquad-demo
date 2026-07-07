# Unit 0 — 파츠 외형 데이터 계약

## 목적

파츠 조합/색상을 담는 데이터 계약을 인터페이스와 구현체에 추가한다. 이 unit 은 계약만 추가하고 런타임 동작은 바꾸지 않는다 (빈 목록 = 현행 유지).

## 변경 대상

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — 멤버 2개 추가
- `Assets/_Project/Scripts/Data/SpineSlotColor.cs` — 신규 (직렬화 struct)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`, `AttackUnitData.cs`, `PropData.cs` — 필드 + 프로퍼티 구현 (인터페이스 구현체 전부)

## 구현

1. 신규 직렬화 타입:
   ```csharp
   [Serializable]
   public struct SpineSlotColor { public string slotName; public Color color; }
   ```
2. `ISpineUnitVisualData` 에 추가:
   ```csharp
   IReadOnlyList<string> SpinePartSkins { get; }      // 파츠 스킨 경로 목록. 비면 SpineSkinName 단일 스킨 사용
   IReadOnlyList<SpineSlotColor> SpineSlotColors { get; } // 슬롯 틴트. 비면 미적용
   ```
3. 각 구현체에 필드 추가 (기본값 빈 목록):
   ```csharp
   [SpineSkin(dataField: "skeletonDataAsset")] public List<string> partSkins = new();
   public List<SpineSlotColor> slotColors = new();
   ```
   `[SpineSkin]` 은 spine-unity 어트리뷰트 — 인스펙터에서 해당 SkeletonData 의 스킨 드롭다운을 띄운다 (unit 2 에서 검증/보완).
4. PropData 는 파츠를 쓰지 않지만 인터페이스 구현체라 동일 필드를 갖는다 (빈 목록 고정, 별도 UI 노출 불필요).

## 완료 기준

- [ ] 컴파일 에러 0
- [ ] 기존 렌더 무변화 (전 유닛 여전히 full_skins 단일 스킨) — 배치 스모크 그린
- [ ] Defender 인스펙터에 partSkins/slotColors 필드 노출 확인
