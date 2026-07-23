# 3. 멀티-골 연결성 검증

## 목적

authoring/런타임 검증을 "각 스폰이 **아무 골이든** 도달"로 확장. 멀티-소스 BFS 로 각 스폰 도달성 보장.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapConnectivity.cs` — `AllSpawnsReachGoal(map)` 내부를 멀티골로. **시그니처명은 유지 권장**(rename 시 호출처 갱신 부담 — 리뷰 m1)
- 호출처(리뷰 m1 정정 — 실제 비-테스트 2곳): `BattleBridge.cs:953` connectivity 가드, `ProceduralMapGenerator.cs:31`(legacy, cleanup 삭제 대상이나 존속 중엔 갱신 필요). **`MapGridBattleAdapter.IsUsableDocument` 는 이 함수를 호출하지 않음**(Width/Tiles 만 검사) — 목록에서 제외. + `MapPainterWindow` 검증(유닛 4)
- 테스트: `Tests/EditMode/MapConnectivityTests.cs` (멀티-소스 케이스 추가)

## 구현

1. 기존: 단일 `map.goal` 에서 BFS → reachable 마킹 → 각 spawn 도달 확인. 각 골이 Walk 여야.
2. 변경: **goals 전체를 BFS 시드**(dist 0 동시)로 flood → reachable set. **goals 폴백**(리뷰 B1): `map.goals.IsCreated && Length>0 ? goals : [map.goal]` — 이 함수도 goals 안 채운 생산자에서 호출될 수 있으므로 소비 지점 폴백. 조건:
   - goals(폴백 포함) ≥1, 각 goal in-bounds + `TileAt==Walk`
   - `map.spawns.Length` ≥2(기존 유지 — 최소 2스폰 사용자 결정), 각 spawn in-bounds + Walk + reachable
3. 순수 함수 유지(값 입력→bool). Burst/EditMode 테스트 대상.

## 계약

- 단일 골: 시드 1개 → 기존 `AllSpawnsReachGoal` 과 동일 판정(회귀).
- **분리 복도 × 자기 골**: 각 복도가 자기 골만 포함해도 그 복도 스폰은 그 골로 reachable → 통과. (복도가 어떤 골에도 안 닿으면 실패 = 의도.)

## 완료 기준

- [ ] 멀티-소스 BFS 로 각 스폰 도달성 검증, 각 골 Walk 확인
- [ ] 단일골 맵 판정 기존과 동일(회귀)
- [ ] 2골 분리복도 맵 통과 / 고립 스폰 맵 실패(EditMode 4케이스)
- [ ] compile 0 error, EditMode green
