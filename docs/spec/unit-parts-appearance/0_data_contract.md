# Unit 0 — 파츠 외형 데이터 계약

## 목적

파츠 조합/색상을 담는 데이터 계약을 인터페이스와 구현체에 추가한다. 이 unit 은 계약만 추가하고 런타임 동작은 바꾸지 않는다 (빈 목록 = 현행 유지).

## 변경 대상

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — 멤버 2개 추가
- `Assets/_Project/Scripts/Data/SpineSlotColor.cs` — 신규 (직렬화 struct)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`, `AttackUnitData.cs` — 필드 + 프로퍼티 구현. **인터페이스 구현체는 이 2개가 전부** (critic 검증: PropData 는 ISpineUnitVisualData 를 구현하지 않고 유사 필드를 PropBillboard 가 직접 읽는 구조라 제외 — 죽은 필드 방지)

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
4. `[SpineSkin]` 은 List<string> 요소에 동작 확인됨 (SpineTreeItemDrawerBase 가 string 드로어 + `FindBaseOrSiblingProperty` 가 SO 루트의 skeletonDataAsset 을 찾음. Layer Lab PartsManager 자체가 동일 패턴 출하 중).

## 완료 기준

- [x] 컴파일 에러 0 (격리 리그 배치 검증)
- [x] 기존 렌더 무변화 (전 유닛 여전히 full_skins 단일 스킨) — 배치 스모크 PASS
- [ ] Defender 인스펙터에 partSkins/slotColors 필드 노출 확인 — 에디터 시각 항목, unit 2 에서 드롭다운 확인과 함께 수행

확인 2026-07-07 (배치 검증 기준). SpineSlotColor.cs.meta 는 리그 생성분 회수(guid ad59ba91...).
