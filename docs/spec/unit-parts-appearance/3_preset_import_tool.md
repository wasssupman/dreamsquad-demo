# Unit 3 — Layer Lab 조립 결과 임포트 도구

> rev 2026-07-07: critic 리뷰 반영 — 1차 입력을 프리셋에서 **export 프리팹(CharacterPrefabData)** 으로 승격(F3/F5), helmet/hair 배타 resolve(F1·블로커), 논리 색→슬롯 확장(F2·블로커), 매핑 소스 교정(F4).

## 목적

Layer Lab 데모 씬에서 눈으로 조립한 결과를 버튼 한 번으로 유닛 데이터에 복사한다. 데모 씬 = 조립 도구, 우리 데이터 = 저장소.

## 변경 대상

- `Assets/_Project/Editor/LayerLabPresetImporter.cs` — 신규. Assembly-CSharp-Editor 소속이라 LayerLab.ArtMaker 타입 접근 가능 (주의: asmdef 있는 `Editor/UnitStatImport/` 폴더 안에 두면 안 됨)

## 구현

1. **1차 입력 = export 프리팹의 `CharacterPrefabData`** (skinParts: PartsType+index+isHidden, slotColors).
   근거(critic): 데모 UI 의 SavePrefab 버튼이 `SetDirty+SaveAssets` 로 디스크 영속 + 썸네일 생성 — VCS 에 남고 재시작에도 생존.
   프리셋(`PresetData.asset`)은 보조 입력: 런타임 저장이라 **dirty 플래그가 없어 에디터 세션 한정** — 임포터가 읽을 때 `SetDirty+SaveAssets` 로 영속화해 주고, 조회는 리스트 위치가 아닌 `PresetItem.index` 기준 (asset 실측: 비정렬 저장).
2. **인덱스 → 스킨 경로 매핑**: 씬/프리팹의 직렬화 리스트를 파싱하지 않는다 (critic: PartsManager 는 프리팹이 아니라 데모 씬 인스턴스뿐이고, 리스트는 런타임 Init 이 스켈레톤 스캔으로 통째로 덮는 스냅샷). 대신 에디터 타임에 `SkeletonDataAsset.GetSkeletonData(true)` 로 **동일 스캔을 재현**: 스킨 목록을 `PartsType.ToString()` prefix StartsWith(OrdinalIgnoreCase) 필터 — Layer Lab `GetCategorySkins` 와 같은 규칙이라 어긋나지 않는다.
3. **resolve 규칙 (블로커 수정)**:
   - **helmet/hair 배타**: helmet index ≥ 0 (isHidden 아님) → `hair_hat` 포함 + `hair_short` 제외, 아니면 `hair_short` 포함 + `hair_hat` 제외. (Layer Lab 은 hair_hat 을 hair_short 와 항상 미러링 저장하므로 단순 flat 변환 시 이중 머리가 된다)
   - index < 0 또는 isHidden 카테고리 → 스킵 (파츠 미착용)
4. **색상 확장 (블로커 수정)**: 저장된 논리 색 4종을 슬롯 prefix 스캔으로 bake —
   `skin → body/leg_l/leg_r/arm_l/arm_r/head`, `hair → hair/hair_long/helmet_hair`, `beard → beard`, `brow → brow` (총 11슬롯).
   하드코딩 대신 스켈레톤 슬롯을 StartsWith 스캔 (Layer Lab `GetSlotsWithPrefix` 와 동일 규칙). 이 안에 `eye` 는 없으므로 unit 1 의 애니 키잉 제약과 충돌하지 않는다.
5. **출력**: 선택한 유닛 데이터의 `partSkins`/`slotColors` 에 기록 + Undo 지원.
6. **UI**: 유닛 데이터 인스펙터에 "Layer Lab 프리팹/프리셋 가져오기" 버튼 — 프리팹 선택(썸네일) 또는 프리셋 인덱스+파츠 요약 표시(인덱스 혼동 방지).

## 조립 절차 (아트팀용)

1. `Demo_Casual.unity` Play → UI 로 파츠/색상 조립 (랜덤 버튼 활용 가능)
2. **SavePrefab 버튼(좌클릭)** 으로 저장 → `Assets/CharacterPrefabs/` 에 프리팹+썸네일 생성 (프리셋 슬롯은 **우클릭=저장, 좌클릭=적용** — 보조용)
3. Play 종료 → 대상 Defender 데이터 선택 → "가져오기" → 프리팹 선택
4. BattleScene Play 로 확인. 파츠 제거는 hide 토글이 아니라 **미착용(선택 해제)** 으로 — 프리셋 경로는 hide 상태를 저장하지 않는다 (프리팹 경로는 isHidden 저장됨)

## 완료 기준

- [ ] 데모에서 만든 조합이 버튼 클릭으로 Defender 데이터에 복사되고 게임에서 **동일 외형**으로 렌더 — **헬멧 착용/미착용 프리팹 각 1개** 왕복 검증 (배타 규칙 검증)
- [ ] 색상 왕복: 데모에서 바꾼 피부/머리색이 게임에서 11슬롯 전체에 재현 (부분 틴트 아님)
- [ ] Wassup.Runtime 은 여전히 Layer Lab 무의존 (에디터 어셈블리만 접근)
