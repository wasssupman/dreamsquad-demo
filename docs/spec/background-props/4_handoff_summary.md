# Background Props — Handoff Summary

**작성일**: 2026-04-22  
**세션**: Codex 가 v0 scaffolding + 샘플 자산을 먼저 올렸고, 이 세션에서 spec 을 per-unit 구조로 정리한 뒤 커밋 3개로 묶었다.

## Commits

- `88122b1 docs(background-props): add per-unit spec for PropData pipeline`
- `61e59d3 feat(background-props): add PropData pipeline with 1x1 prototype`
- `5262f09 fix(background-props): harden PropBillboard runtime guards`

## Implemented

- `PropData` ScriptableObject (`Wassup/PropData` 메뉴). id / displayName / footprint / visualOffset / visualScale / sprite / sourceTexture / spriteColor / sortingOrder / skeletonDataAsset / spineSkinName / idleAnimation / billboardMode
- `PropBillboard` 런타임 컴포넌트: FullCamera / YAxis / None 3 모드, Awake 에서 `ApplyData()` 호출로 PropData = source of truth 계약 이행
- `PropDataEditor` Inspector 에 `Generate Billboard Prefab` 버튼. Sprite sibling PNG → Sprite import 전환 + write-back, Spine `SkeletonDataAsset` 경로도 동일 버튼에서 처리, prefab 을 `Assets/_Project/Prefabs/Props/{PropData.name}.prefab` 에 overwrite 저장
- 샘플: `prop_prototype_1_1.asset` + 동명 prefab (Sprite_Diamond 기반, 1x1, visualOffset.y=0.55)
- Spec 구조: README + `0_prop_data` / `1_prop_billboard` / `2_prop_data_editor` / `3_prototype_sample` / (본 파일)
- map-system README 에 background-props 교차 링크

## Key Files

- 코드
  - `Assets/_Project/Scripts/Data/PropData.cs`
  - `Assets/_Project/Scripts/Presentation/PropBillboard.cs`
  - `Assets/_Project/Editor/PropDataEditor.cs`
- 자산
  - `Assets/_Project/Data/Props/prop_prototype_1_1.asset`
  - `Assets/_Project/Prefabs/Props/prop_prototype_1_1.prefab`
  - `Assets/_Project/Data/Sprites/Sprite_Diamond.png.meta` (Sprite import 전환)
- 문서
  - `docs/spec/background-props/` (이 폴더 전체)

## Verified

- `PropData.cs` / `PropBillboard.cs` / `PropDataEditor.cs` UnityMCP validate_script 깨끗 (0 warn / 0 err)
- Unity console 에 프로젝트 코드 관련 error/warning 없음 (Persistent allocator leak 경고 1건은 background-props 무관)
- Sprite 경로 샘플 (`prop_prototype_1_1`) prefab 이 실제로 생성되어 리포지토리에 들어감
- Play 모드 실기 확인 (Scene drop + billboard 3 mode 토글) 은 아직 안 함 — README "완료 확인" 2번 체크박스 대기

## Notes

- Footprint 기준점은 **좌하단 셀 중심** 고정. multi-tile 배치 시 world position 계산식 README 의 "공통 원칙" 4번 참조
- PropData.name = prefab 파일명, PropData.id = runtime lookup 키 (비면 name fallback)
- Generator 가 `ResolveSprite` 에서 `data.sprite` 에 write-back 하는 건 의도된 계약. Sprite_Diamond.meta diff 는 부수효과로 커밋에 포함됨
- `Editor/` 폴더에 asmdef 없음. `Wassup.Runtime.asmdef.autoReferenced = true` 로 현재 동작하지만 전체 asmdef 전환 시 깨질 수 있음 → 후속 후보
- PropBillboard 의 Spine Initialize/SetAnimation 이 중복 호출로 상태 리셋되지 않도록 `5262f09` 에서 가드 추가. 수동으로 `ApplyData` 반복 호출해도 idle animation 이 재시작되지 않는다

## Follow-up

- Play 모드 실기 확인: prop_prototype_1_1 drop + FullCamera/YAxis/None 전환 → README 완료 확인 2번 체크
- Spine 경로 샘플 prefab 1개 추가 검증 (현재 Sprite 샘플만 있음) — 선택
- v1 진입 포인트: README 의 "후속 후보" 순서 참고. 다음 후보는 Theme 이관 또는 Footprint placement 중 우선순위 선택
- 실기기 (Android) silhouette 수정 follow-up (c7ea469) 은 사용자 검증 완료
