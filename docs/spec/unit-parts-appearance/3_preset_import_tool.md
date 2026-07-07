# Unit 3 — Layer Lab 프리셋 임포트 도구

## 목적

Layer Lab 데모 씬에서 눈으로 조립한 결과를 버튼 한 번으로 유닛 데이터에 복사한다. 데모 씬 = 조립 도구, 우리 데이터 = 저장소.

## 변경 대상

- `Assets/_Project/Editor/LayerLabPresetImporter.cs` — 신규 (Assembly-CSharp-Editor 소속이라 LayerLab.ArtMaker 타입 접근 가능)
- 아트팀용 절차 문서: 이 파일 하단 "조립 절차" 섹션이 소스

## 구현

1. **입력**: Layer Lab `PresetData.asset` 의 `PresetItem` (`Dictionary<PartsType,int>` + 슬롯 색상)
   또는 데모 씬에서 export 한 프리팹의 `CharacterPrefabData` (`skinParts`/`slotColors`). 둘 다 지원.
2. **매핑**: `PartsType + index` → 스킨 경로. 데모 Player 프리팹의 `PartsManager` 직렬화 리스트
   (`[SpineSkin] List<string> back/top/...`) 를 읽어 인덱스→경로 변환 (Layer Lab 과 동일 소스라 어긋나지 않음).
3. **출력**: 선택한 유닛 데이터(ScriptableObject)의 `partSkins`/`slotColors` 에 기록 + Undo 지원.
4. **UI**: 유닛 데이터 인스펙터에 "Layer Lab 프리셋 가져오기" 버튼 (프리셋 인덱스 선택 팝업).

## 조립 절차 (아트팀용)

1. `Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/Scenes/Demo_Casual.unity` Play
2. UI 로 파츠/색상 조립 → 프리셋 슬롯에 저장
3. Play 종료 → 대상 Defender 데이터 선택 → "Layer Lab 프리셋 가져오기" → 프리셋 번호 선택
4. BattleScene Play 로 확인

## 완료 기준

- [ ] 데모 씬에서 만든 프리셋이 버튼 클릭으로 Defender 데이터에 복사되고 게임에서 동일 외형으로 렌더
- [ ] 색상 포함 왕복 (데모에서 바꾼 머리색이 게임에서 재현)
- [ ] Wassup.Runtime 은 여전히 Layer Lab 무의존 (에디터 어셈블리만 접근)
