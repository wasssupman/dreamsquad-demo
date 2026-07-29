# 0 — Spine 포트레이트 베이크 프로필 + Editor 도구

## 목적

현재 유닛 외형을 고정 포즈로 렌더하고 상체 프레이밍을 조정해 투명 PNG로 저장한다.
Outgame/BattleScene은 캡처용으로 변형하지 않는다.

## 변경 대상

- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.cs` — 신규
- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakerWindow.cs` — 신규
- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.asset` — 신규
- 출력 폴더: `Assets/_Project/Art/DefenderPortraits/spine/`

## 구현

1. Profile은 catalog, 출력 폴더, 해상도(512), supersample, 포즈/시점, 공통 camera와
   id별 offset/zoom override만 소유한다. 런타임 `DefenderUnitData`에는 넣지 않는다.
2. Window는 카탈로그 순서로 `Bake Selected / Bake All / Validate`를 제공하고,
   개별 미리보기와 id가 표시된 전수 contact sheet를 보여준다. 출력 파일명은
   `defender_portrait_{id}.png`로 고정한다.
3. `EditorSceneManager.NewPreviewScene()`에 Camera/Directional Light와
   `SkeletonAnimation.NewSkeletonAnimationGameObject` 대상 리그를 만든다.
   `SpineCombinedSkinCache.Apply` 후 지정 애니메이션을 고정 시점까지 적용하고 mesh를 갱신한다.
   `finally`에서 임시 오브젝트와 preview scene을 항상 정리한다.
4. 프레임 기준은 `Head`/`Pelvis` bone이다. 장비 전체 bounds를 fit하지 않으며
   얼굴·머리장식·상의를 우선하고 예외만 override한다.
5. 투명 RenderTexture를 supersample로 렌더한 뒤 512로 축소한다. 현 Spine material은
   PMA 입력(`_StraightAlphaInput=0`)이므로 readback RGB를 alpha로 나눠 straight alpha로
   변환하고, alpha 0 픽셀 RGB는 0으로 정리한 뒤 PNG를 쓴다.
6. 재import 후 `TextureImporter`를 README 출력 계약으로 강제한다. 모바일은 Compressed
   + Android/iPhone ASTC 6×6을 기본으로 하되 92px 실기기 품질에 문제가 있으면 4×4로 올린다.
7. `Validate`는 null/중복 id·skeleton, 없는 포즈/anchor bone, 중복 출력 경로,
   importer 오류, 씬 dirty 증가와 preview scene 누수를 보고한다.

새 인터페이스·런타임 매니저·SpriteAtlas는 만들지 않는다.

## 완료 기준

- Unity 컴파일 및 Console error 0.
- 라이브 카탈로그 20종을 하드코딩 없이 열거하고 개별/전수 preview를 렌더한다.
- preview 전후 active scene 경로·dirty 상태와 `previewSceneCount`가 동일하다.
- 같은 profile의 2회 베이크 결과 크기·포즈·프레이밍이 동일하다.
- 투명 테스트 배경(흰색/검정/컬러) 모두에서 외곽 halo가 없다.
- `Validate`가 정상 profile은 통과하고 duplicate id/null skeleton/없는 애니메이션을 검출한다.

완료 확인: 2026-07-28 · 사용자 20종 프레이밍 승인("모든 유닛 진행") · 구현 커밋 `8c4b5427`.
