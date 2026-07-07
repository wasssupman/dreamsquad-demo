# Unit 2 — 인스펙터 조립 UX

## 목적

기획/아트가 유닛 데이터 인스펙터에서 파츠를 드롭다운으로 직접 조립할 수 있게 한다. 스킨 이름 타이핑 금지가 목표.

## 변경 대상

- unit 0 의 `[SpineSkin]` 어트리뷰트가 List<string> 요소에 동작하는지 확인, 미동작 시:
  `Assets/_Project/Editor/SpinePartSkinDrawer.cs` — 신규 PropertyDrawer
- `Assets/_Project/Editor/UnitVisualDataValidator.cs` — 신규 (유효성 검사)

## 구현

1. **드롭다운**: `[SpineSkin(dataField:"skeletonDataAsset")]` 은 리스트 요소에서 동작 확인됨
   (critic 검증: SpineTreeItemDrawerBase + FindBaseOrSiblingProperty 의 SO 루트 조회, `/` 경로가
   GenericMenu 서브메뉴로 자동 그룹핑 — 480개도 카테고리별로 접힘. Layer Lab 자체가 동일 패턴 사용).
   에디터에서 1회 확인만 하고, 만에 하나 깨질 때만 PropertyDrawer 대체.
2. **유효성 검사** (에디터 전용): 유닛 데이터 저장/선택 시
   - 존재하지 않는 스킨 경로 → 경고
   - **필수 카테고리 누락**: `skin/` 파츠(본체, 1종뿐) 없으면 경고 — default 스킨이 없는 스켈레톤이라 누락 시 몸통 없는 유닛이 된다. eyes/mouth 권장 수준 안내
   - 같은 카테고리 파츠 중복 → 경고. **"뒤가 이김" 이 아니라 슬롯 단위 프랑켄슈타인 병합** (critic 실측: helmet_c_1 은 helmet+helmet_back 2슬롯, c_2 는 1슬롯 — 순차 합성 시 c_1 잔재가 남음). 의도 확인용이 아닌 실결함 방지
   - **helmet ↔ hair_short/hair_hat 배타 규칙 위반** → 경고 (helmet 있으면 hair_hat, 없으면 hair_short — Layer Lab 데모와 동일 규칙)
   - slotColors: 슬롯 이름 오타 경고 + **애니메이션이 색을 키잉하는 슬롯(`eye`) 경고** (틴트가 소리 없이 덮임)
3. 검증은 로그가 아니라 인스펙터 HelpBox 로 — 아트팀이 콘솔을 안 봐도 되게.

## 완료 기준

- [ ] Defender 데이터에서 파츠를 드롭다운만으로 추가/교체 가능
- [ ] 잘못된 스킨 경로/중복 카테고리가 인스펙터에서 즉시 경고 표시
- [ ] 스킨 이름 수동 타이핑이 필요한 경로가 없음
