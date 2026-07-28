# 3 — Handoff Summary

## Commit

- `8c4b5427` — `feat(defender-spine-portraits): add deterministic portrait baker`
- `8cd70ac5` — `docs(defender-spine-portraits): record unit 0 approval`
- `4dc1244c` — `feat(defender-spine-portraits): bake and assign roster portraits`
- `4c0f2c2a` — `docs(defender-spine-portraits): record unit 1 validation`
- `5c4a3bdb` — `chore(defender-spine-portraits): retire legacy AI portraits`

## Implemented

- 현재 Spine 외형에서 512×512 투명 상체 Sprite를 만드는 Editor baker를 추가했다.
- 전장과 같은 `SpineCombinedSkinCache.Apply`로 `partSkins`와 `slotColors`를 합성한다.
- 고정 Idle 포즈, Head/Pelvis 기준 공통 프레이밍과 유닛별 override를 profile이 소유한다.
- preview scene 격리, 2× supersampling, PMA→straight alpha 변환을 적용했다.
- 유효 DefenderCatalog 20종을 id 기반 파일로 베이크하고 각 SO의 `portrait`를 교체했다.
- 개별 SO만 저장하도록 베이커를 제한해 열린 Inspector의 무관한 dirty 값을 보존한다.
- 기존 AI PNG 38장과 하위 폴더 meta를 삭제하고 현재 문서 포인터를 교체했다.

## Key Files

- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakerWindow.cs`
- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.cs`
- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.asset`
- `Assets/_Project/Art/DefenderPortraits/spine/`
- `Assets/_Project/Data/Defenders/`
- `docs/spec/defender-spine-portraits/README.md`

## Verified

- Catalog 20종, 고유 Sprite 20개, 출력 PNG 20개가 일치한다.
- fresh rerender와 저장 PNG의 픽셀 바이트가 20종 모두 일치했다.
- 동일 유닛 2회 렌더가 byte-identical이고 preview scene 누수가 없다.
- 투명 픽셀의 RGB 잔존 0이며 검정/흰색/게임톤 배경에서 halo가 없다.
- Battle tray 7슬롯: 140.5×126, preserveAspect, dim/비용/이름 밴드 확인.
- Outgame roster 20셀: 126×126, header 7슬롯: 80×80 확인.
- 임시 PresetUnitCell portrait: 76×76, preserveAspect 확인.
- DreamcatcherFocusPresenter의 `data.portrait` 바인딩과 preserveAspect 계약을 확인했다.
- 삭제된 38개 PNG GUID의 Assets/Packages/ProjectSettings/docs 참조는 0건이다.
- 최종 render probe 0, catalog validation 0, legacy asset 생존 0, Console Error 0이다.

## Notes

- PNG는 파생 산출물이며 source of truth는 Spine 데이터와 appearance override다.
- SO가 이미 dirty면 baker가 중단한다. 저장 전 디스크 재import 후 대상 SO만 저장한다.
- crop 조정은 UI별 보정이 아니라 bake profile override로 해결한다.
- 삭제된 AI 원본은 필요하면 git 이력에서 복구할 수 있다.

## Follow-up

- 연결된 Android/iOS 실기기에서 92px/header와 Battle tray 가독성을 최종 QA한다.
- 범위 밖 후보는 적 포트레이트, stale signature CI, 일부 화면의 SkeletonGraphic 전환이다.
