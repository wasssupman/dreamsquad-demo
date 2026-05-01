# 17c. Character Sort Order Alignment — DEPRECATED

**상태**: 철회 (2026-04-24)
**대체 spec**: `24_enemy_view_mono_migration.md`, `25_fallback_defender_view_mono.md`, `26_sort_order_unification.md`

## 철회 사유

본 spec 은 "Enemy / fallback defender presenter 에 같은 sortingOrder 공식을 삽입" 을 전제했으나, codex 리뷰에서 아래 사실이 확인됐다:

- Enemy 는 `BattleBridge.cs:1931` 에서 **ECS `RenderMeshUtility.AddComponents`** 로 렌더됨. GameObject view 가 없어 `GetComponentsInChildren<Renderer>()` 대상이 존재하지 않음.
- Non-Spine fallback defender 도 `BattleBridge.cs:1762` 에서 동일하게 ECS RenderMesh.
- SpriteRenderer(프랍) 과 ECS RenderMesh(캐릭터) 는 서로 다른 렌더 파이프라 공식 통일만으로 sort 되지 않음.

즉 원인은 "공식이 없어서" 가 아니라 "Enemy 가 Mono view 가 아니라서". 따라서 본 spec 대신 **Enemy / fallback defender 를 Mono view 로 이관한 뒤 공식을 통일**하는 순서로 재구성한다.

## 후속

- `24` Enemy view Mono 이관
- `25` fallback defender Mono 수렴
- `26` 프랍/캐릭터 공통 sortingOrder 유틸 도입

새 audit finding `V-010` 은 `26` 에서 해소된다.

## 주의

- Enemy Spine asset 이 아직 준비되지 않았다 (`Assets/_Project/Spine/` 에 player-main 만 존재). `24` 는 Spine 없이 placeholder quad Mono view 로 시작. Spine 교체는 별도 `27` spec 에서.
- 본 문서는 실행되지 않음. 커밋 히스토리 추적용으로만 보존.

확인 일자: 2026-04-24 / 커밋 해시: (deprecated, 실행 없음)
