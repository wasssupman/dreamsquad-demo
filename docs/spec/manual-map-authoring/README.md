# Manual Map Authoring — 수동 고정 맵 (MapDocument) 가동

**상태: 완료 2026-07-19**
**작성일**: 2026-07-19 (구현 완료 후 회고 작성 — 코드/커밋이 선행, 문서가 후행)
**커밋**: `acff0abc` → `ba4ed7e3` → `12a9518d`

## 목표

매판 프로시저럴 랜덤이던 플레이 맵을, 명일방주식 **손으로 설계한 고정 레이아웃**으로 제공한다. 신규 시스템 없이 — map-grid spec 때 예약만 돼 있던 `MapDocument`(수동 맵 데이터) 경로를 처음 실데이터로 가동해 배선한다. 랜덤 생성 기능은 제거하지 않고 보존한다.

## 구현 문서 목록

| 번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Fix+Code | `0_map_source_sync_and_fixed_seed.md` | 패널이 씬 mapSource 를 덮어쓰던 버그 수정 + fixedMapSeed 스위치 |
| 1 | Asset | `1_mapdocument_authoring.md` | ArkFunnel 수동 맵 데이터 제작·검증·씬 배선 |
| 2 | Review-fix | `2_review_followup_fixes.md` | document 연결성 가드 · 로거 실시드 · 패널 무푸시 hydrate |
| 3 | Tuning | `3_switchback_15x10_tuning.md` | 15×10 스위치백 리레이아웃 + 원경/유닛 스케일/드래그 줌아웃 |
| 4 | Handoff | `4_handoff_summary.md` | 인계 요약 |
| 5 | Tuning | `5_three_spawn_corner_goal.md` | 3스폰 + 모서리 골 + 스폰별 최소 이동거리(≥20칸) |

## Feature-wide 계약

- **맵 소스 우선순위**: `BattleBridge.mapDocument`(수동, 최우선) > `fixedMapSeed`(비0 = 프로시저럴 고정) > `MatchSeed.DeriveMapSeed(matchSeed)`(매판 랜덤). document 배선 중엔 fixedMapSeed 가 **완전 무효**(어댑터가 seed 를 읽지 않음).
- **document 소비 조건은 한 곳**: `MapGridBattleAdapter.IsUsableDocument`. BattleBridge 의 connectivity-guard 판단도 같은 술어를 쓴다 — 두 곳이 어긋나면 안 된다.
- **수동 맵은 Validator 미경유** → 런타임 `MapConnectivity.AllSpawnsReachGoal` 검사를 반드시 거친다(실패 시 fallback 직선 맵). 프로시저럴 MapGrid 만 Validator 보장으로 검사를 스킵한다.
- **로그 mapSeed = 실제 빌드 시드**: 맵 빌드 직후 `BattleLogger.SetActualMapSeed(_generatedMap.seed)` 로 덮어쓴다. `SetMatchSeeds` 의 파생값은 빌드 전 추정치다. 수동 document 는 seed=-1 로 기록된다.
- **맵 설정 패널은 init 에 push 하지 않는다**: `Initialize` 는 `DraftController.SyncMapStateFromBridge()` 로 씬 authoring 값을 흡수(hydrate)만 하고, bridge 반영은 사용자가 패널을 실제 조작한 순간에만. (패널 코드 기본값이 씬 의도를 덮어쓰던 버그의 일반화 픽스 — mapSource 한 필드만 고치면 형제 필드에서 같은 버그가 재발한다.)
- **덮어쓰기 = GUID 유지**: 맵 레이아웃 교체는 기존 `MapDocument_ArkFunnel.asset` 에 `MapDocumentBuilder.WriteToDocument` 로 다시 굽는다. 씬 배선 불변.
- **수동 authoring 관례**: `authoringSeed = -1`, `generatorVersion = 0`. mergeDegree = 4방향 인접 path 수, chokepoint = degree≥3 (CellClassifier 정의와 동일하게 계산해 넣는다 — 로드 시 재계산 없음, 후속 후보 참조).

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설 없음. `MapDocument` 는 `GeneratedMap` 의 **소스만 대체**하며, 이후 정거장(flow field → 타일맵 페인트 → 프랍/효과 타일)은 기존 맵 파이프라인 그대로 소비한다. `docs/reference/object-pipeline-map.md` 구조 변경 없음.

## 후속 후보

`docs/spec/README.md` Follow-up Backlog 의 `(manual-map-authoring)` 항목 참조 (code-review 잠복 이슈에서 이관).
