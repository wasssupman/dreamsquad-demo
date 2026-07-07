# Unit 2 — 인스펙터 조립 UX

## 목적

기획/아트가 유닛 데이터 인스펙터에서 파츠를 드롭다운으로 직접 조립할 수 있게 한다. 스킨 이름 타이핑 금지가 목표.

## 변경 대상

- unit 0 의 `[SpineSkin]` 어트리뷰트가 List<string> 요소에 동작하는지 확인, 미동작 시:
  `Assets/_Project/Editor/SpinePartSkinDrawer.cs` — 신규 PropertyDrawer
- `Assets/_Project/Editor/UnitVisualDataValidator.cs` — 신규 (유효성 검사)

## 구현

1. **드롭다운**: `[SpineSkin(dataField:"skeletonDataAsset")]` 이 리스트 요소에서 동작하면 그대로 사용.
   안 되면 PropertyDrawer 로 대체 — 대상 SkeletonDataAsset 의 스킨 목록을 카테고리 prefix
   (`helmet/`, `top/` 등) 로 그룹핑한 드롭다운 제공.
2. **유효성 검사** (에디터 전용): 유닛 데이터 저장/선택 시
   - 존재하지 않는 스킨 경로 → 인스펙터 경고 박스
   - 같은 카테고리 파츠 중복 (예: helmet 2개) → 경고 (뒤 항목이 이기지만 의도 확인용)
   - slotColors 의 슬롯 이름 오타 → 경고
3. 검증은 로그가 아니라 인스펙터 HelpBox 로 — 아트팀이 콘솔을 안 봐도 되게.

## 완료 기준

- [ ] Defender 데이터에서 파츠를 드롭다운만으로 추가/교체 가능
- [ ] 잘못된 스킨 경로/중복 카테고리가 인스펙터에서 즉시 경고 표시
- [ ] 스킨 이름 수동 타이핑이 필요한 경로가 없음
