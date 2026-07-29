# 1 — 라이브 로스터 전수 베이크 + portrait 교체

## 목적

unit 0 도구로 라이브 방어유닛 전원의 Spine 상체 Sprite를 만들고,
`DefenderUnitData.portrait`를 id가 같은 출력으로 교체한다. 이 unit에서는 기존 AI
원본을 아직 삭제하지 않아 시각 승인 전 롤백 비용을 낮춘다.

## 변경 대상

- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.asset` — 프레이밍 보정
- `Assets/_Project/Art/DefenderPortraits/spine/defender_portrait_*.png` — 카탈로그 수만큼 신규
- `Assets/_Project/Art/DefenderPortraits/spine/defender_portrait_*.png.meta` — Sprite import
- `Assets/_Project/Data/Defenders/Defender_*.asset` — `portrait` 참조만 교체

## 구현

1. `DefenderCatalog.units` 전수를 `Bake All`한다. 현재 기준 20종이지만 코드·문서는
   20을 성공 조건으로 하드코딩하지 않고 `유효 catalog 수 == 출력 수`로 검증한다.
2. contact sheet에서 README의 시각 기준으로 검수한다. 공통 crop을 먼저 조정하고,
   예외만 id별 override를 둔다. 얼굴이나 머리장식이 잘린 경우는 실패다.
   긴 창·총·곡괭이는 프레임 밖으로 잘릴 수 있으며, 장비 전체를 담으려고 전신 크기로
   축소하지 않는다.
3. 도구가 id로 대상 SO를 찾아 같은 id 출력 Sprite를 `portrait`에 할당한다.
   asset/displayName/카탈로그 배열 순서 추측으로 매핑하지 않는다.
4. SO에서는 `portrait` 이외 필드가 변하지 않았는지 diff로 확인한다.
   특히 `partSkins`, `slotColors`, 스탯, 능력, 카탈로그 순서는 불변이다.
   베이커는 dirty SO를 거절하고 대상 SO를 디스크에서 강제 reimport한 뒤
   `SaveAssetIfDirty`로 개별 저장해 런타임 시트 동기화 값이 함께 직렬화되지 않게 한다.
5. 전수 검증:
   - 모든 유효 catalog unit의 portrait가 `Art/DefenderPortraits/spine/` 아래다.
   - portrait null 0, Sprite 중복 참조 0, 파일명 id 불일치 0.
   - 512×512 RGBA/straight alpha/importer 계약 전수 통과.
   - PNG에는 배경·프레임·라벨 픽셀이 없다.

기존 UI 코드는 수정하지 않는다. 모든 소비처가 이미 `DefenderUnitData.portrait`를 읽고
`preserveAspect`로 표시하므로 이 unit은 파생 에셋과 SO cut-over에 한정한다.

## 완료 기준

- Unity 컴파일 및 Console error 0, 베이커 `Validate` 전수 통과.
- id 라벨 contact sheet에서 전 유닛이 서로 구분되고 실제 Spine 파츠/색과 일치한다.
- 흰색·검정·게임 UI 톤 배경 위에서 halo와 불투명 사각형이 없다.
- `git diff`상 각 Defender SO는 `portrait` 참조 외 변경이 없다.
- 사용자 확인: 20종 contact sheet의 상체 crop/캐릭터 정합성 통과.

완료 확인: 2026-07-28 · 사용자 전수 진행 승인 · 구현 커밋 `4dc1244c` ·
catalog/output/고유 Sprite 20/20/20, 현재 Spine 재렌더와 PNG 바이트 일치 20/20.
