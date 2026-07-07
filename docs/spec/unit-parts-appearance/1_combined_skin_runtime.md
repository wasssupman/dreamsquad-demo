# Unit 1 — combined skin 런타임 합성

## 목적

`SpineUnitView.Spawn` 의 스킨 적용 단계를 확장해, 파츠 목록이 있으면 combined skin 을 합성·적용하고 슬롯 색상을 틴트한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 스킨 적용 블록 확장
- `Assets/_Project/Scripts/Presentation/SpineCombinedSkinCache.cs` — 신규 (합성 + 캐시)
- `Assets/_Project/Editor/SpineUpgradeSmoke.cs` — 스모크에 합성 검증 추가

## 구현

1. `SpineCombinedSkinCache` (static):
   - 키: `(SkeletonDataAsset, comboKey)` — comboKey 는 파츠 목록 join 문자열
   - `GetOrBuild(SkeletonDataAsset, IReadOnlyList<string> parts)` → `new Skin(comboKey)` 에 `skeletonData.FindSkin(part)` 를 `AddSkin` 으로 누적. 존재하지 않는 스킨 경로는 경고 로그 + 스킵 (유닛은 나머지 파츠로 렌더).
   - 캐시 수명: SkeletonDataAsset 의 GetSkeletonData 캐시와 동일 취급, 정리 로직 없음 (상한 = 유닛 데이터 종수).
2. `SpineUnitView.Spawn`: `SpinePartSkins` 가 비어 있지 않으면 기존 단일 스킨 경로 대신
   `SetSkin(cache.GetOrBuild(...))` → `SetSlotsToSetupPose()`. 비어 있으면 현행 경로 그대로.
3. 슬롯 색상: `SpineSlotColors` 를 순회하며 `skeleton.FindSlot(name)?.SetColor(color)`
   — `SetSlotsToSetupPose()` **이후** 적용 (setup pose 가 색을 리셋하므로). 사망 페이드는 `Skeleton.A` 채널이라 곱연산 독립.
4. 스모크 확장: 파츠 2~3개를 가진 임시 조합으로 combined skin 을 합성해 어태치먼트 존재
   (`skin.GetAttachments()` 카운트 > 단일 파츠 카운트) 와 슬롯 색 적용을 검증.

## 완료 기준

- [ ] 파츠 목록 있는 유닛이 조합 외형으로 렌더 (에디터 확인)
- [ ] 빈 목록 유닛은 기존과 동일 (full_skins)
- [ ] 없는 스킨 경로는 경고 후 생존 (에러/크래시 없음)
- [ ] 같은 조합 유닛 N마리 스폰 시 Skin 합성 1회 (캐시 히트 로그 또는 카운터로 확인)
- [ ] 배치 스모크 PASS
