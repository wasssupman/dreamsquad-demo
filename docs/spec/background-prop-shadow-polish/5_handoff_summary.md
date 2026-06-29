# 5 — Handoff Summary

## Commit

- `ee10b86` feat(presentation): 배경 프랍 그림자 폴리시 — 블롭 소프트닝 + 원경 블롭 + 밀도↓ (units 0~2)
- `4704d4f` feat(presentation): 블롭 그림자 모델 단순화 + 정적 스폰 (units 3~4)
- + docs 완료 표기 커밋(8e322ba 및 본 커밋).

## Implemented

- 근경 프랍 블롭 소프트닝(α 0.45→0.30). footprint 는 unit 3 에서 제거됨.
- 원경(외곽 링) 프랍에 접지 블롭 부착 — 이전 "그림자 OFF" 계약을 ON 으로 갱신(`tilemap-world-surround/4`).
- 원경 밀도 `ringPropDensity` 0.55→0.35 (`forest.asset`).
- 블롭 크기 모델 단순화: footprint 타원 제거 → **원형**, `지름 = BlobShadowSize(1타일,1.0) × visualScale`.
- 블롭 **정적 스폰**: 프랍은 Attach 시 1회 세팅(`live:false`), 매 프레임 강제 제거. 유닛만 `live:true` 따라가기.
- 방향 offset 없음 — 발밑 정중앙(좌우/깊이/페이즈 무관 일관).

## Key Files

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `live` 플래그, `ApplyTransform()` 1회/매프레임 분기, 원형 스케일.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 블롭 serialized/static (size·color·groundY). footprint/screenLift/offset 제거됨.
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `AttachPropBlob`(live:false) + `InstantiateRingProps` 블롭 호출.
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`, `QuadUnitView.cs` — 유닛 블롭 `live:true`.
- `Assets/_Project/Map/Theme/forest/forest.asset` — `ringPropDensity`.

## Verified

- compile 0, runtime console 0. Play 스크린샷 다수(`Assets/Screenshots/prop_shadow_*`).
- 측정: 링 나무 94/94 블롭 부착, deltaZ=0.000(발밑), worldScale 원형(1.50/0.68), ring 193→~120.

## Notes

- **씬 미커밋**: `BattleScene.unity` 워킹트리에 사용자 WIP(전역 Volume, 카메라 pitch 40°로 이동, GO 비활성, spawnSpread/propDistanceTilt 기본값 직렬화)가 남아 있다 — **내 작업 아님, 커밋 안 함**. blob 값(color α0.3 등)은 ee10b86 에서 커밋됨.
- 씬의 `blobShadowFootprint` 직렬화 줄은 코드에서 필드 제거 후 **orphan**(Unity 무시, 무해). 다음 정식 씬 저장 때 정리됨.
- 캐릭터는 데스크톱 real-shadow, 블롭은 모바일 폴백만(`live:true`). 프랍은 항상 블롭.
- 부모 lossyScale 나눗셈은 load-bearing(틸트 visualRoot 자식 한정이라 블롭 부모는 정적이지만, 90° 바닥 루트/placement.scale/jitter 상쇄에 필요). 유지.

## Follow-up

→ `docs/spec/README.md` Follow-up Backlog / 본 README 후속 후보: 방향성 그림자("해 전방", 매프레임 불가피라 보류), 원경 블롭 거리 dimming, 소프트 sprite 재오서링.
