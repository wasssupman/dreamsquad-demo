# Background Props Spec

**작성일**: 2026-04-22  
**상태**: 0~3 작업 단위 대기 (PropData + billboard + generator + prototype 샘플)  
**목표**: 맵 타일 위 또는 외곽 장식 영역에 footprint 기반 배경 프랍을 배치할 수 있는 데이터/프리팹/생성 파이프라인을 만든다. v0 는 1x1 prototype 으로 pipeline 만 검증하고, footprint placement / theme 연동은 후속 spec 으로 분리한다.

## 구현 문서 목록

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | SO 계약 | `0_prop_data.md` | `PropData` 필드, naming, 기준점 결정 |
| 1 | Runtime | `1_prop_billboard.md` | billboard 3 모드 + ApplyData 책임 경계 |
| 2 | Editor | `2_prop_data_editor.md` | Generate 버튼 + ResolveSprite 파이프라인 |
| 3 | Sample | `3_prototype_sample.md` | `prop_prototype_1_1` 샘플 + v0 완료 기준 |

## 공통 원칙

- PropData SO 가 source of truth. 생성된 prefab 은 런타임 `ApplyData()` 로 data 값을 반영한다.
- Prefab 파일명은 `PropData.name` (asset file name). runtime lookup 키가 필요하면 `PropData.id` 를 쓰되, 비어 있으면 `name` 으로 fallback.
- Footprint 기준점은 **좌하단 셀 중심** 으로 고정한다. `footprintX * footprintY` 셀이 좌하단 기준으로 +X/+Y 방향으로 확장한다.
- Billboard prefab 은 root 1점 anchor 만 가진다. multi-tile footprint 의 world position = 좌하단 셀 중심 + `(footprintX-1)/2, 0, (footprintY-1)/2` * tileSize.
- Visual 은 항상 `root/Visual` 자식에 둔다. `visualOffset` / `visualScale` 은 Visual 의 local transform.
- `Assets/Resources/RuntimeMaterials` 패턴과 달리 프랍은 Sprite/Spine 기본 머티리얼을 그대로 쓴다. material override 는 후속 후보.
- Spine 경로 default: skin `"default"`, idle animation `"idle"`. 존재하지 않으면 무시.
- Prefab 저장 경로는 v0 에서 `Assets/_Project/Prefabs/Props/{PropData.name}.prefab` 고정. theme 이관은 후속.

## 후속 후보

- Theme 이관: `Assets/_Project/Data/Theme/{themeName}/Props/` 경로 지원 + `MapThemeData.tileProps / decorProps` 추가
- Footprint placement: seed 기반 후보 셀 선택 + allowedTiles / occupancy 검증 + `PropPlacement` record
- Designer tools: batch generator, footprint gizmo, prop_{name}_{x}_{y} 파일명 검증
- PropData v1 필드 (`weight`, `allowRotation`, `PropPlacementSurface`, `allowedTiles`, `blocksPlacement`, `pivot`, `randomOffsetRange`, `randomScaleRange`, sorting preset)
- MapView instantiate 책임 재정의 (현 MapView 확장 vs 별도 `PropSpawner`)
- Phase 10 `MapThemeData.obstaclePrefabs` 호환/교체
- Spine 프랍의 material/shader override 및 MapView 타일과의 sorting 통합

## 완료 확인

- [ ] 0~3 작업 단위 커밋
- [ ] `prop_prototype_1_1.prefab` 을 씬에 drop 시 타일 위에 빌보드 스프라이트로 표시, 카메라 회전 시 방향 유지
