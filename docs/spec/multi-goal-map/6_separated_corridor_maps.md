# 6. 분리 복도 × 멀티골 맵 제작

## 목적

멀티골 시스템(유닛 0~5) 위에서 **명일방주식 분리 복도** 맵을 제작해 풀을 교체한다. 각 스폰이 자기 전용 복도로 **자기 골까지** 완전 독립(골에서도 합류 안 함). feature 의 e2e 검증.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_*.asset` — 기존 풀 5맵을 **GUID 유지 덮어쓰기**(풀 배선 불변)
- (필요 시) `MapDocumentPool.asset` — 엔트리 수 바뀌면만

## 구현

1. 규칙: 스폰 N개 = **완전 분리 복도 N개**, 각 복도 끝에 **자기 골**(스폰 수 = 골 수, 최근접-골 라우팅이 자동으로 자기 복도→자기 골). 이동 ≥20, Walk 1링=Place, 나머지 Deco, 그리드 ≤20×12, 2×2 walk 금지.
2. `scratchpad/akmaps.py` 검증기를 **멀티골**로 확장(골 목록·각 복도가 자기 골 도달·복도 완전 분리=골 공유 없음까지 체크) 후, 검증 통과한 맵만 굽는다.
3. Bake: 페인터(유닛 4) 또는 execute_code(CodeDom `in`→`ref`, delegate 파라미터명 충돌 주의). authoringSeed=-1, goals[] 기록.
4. 덱 페어링: 기존 waveSeed 고정 유지(같은 맵=같은 웨이브). 예산 동일.

## 계약

- 총량 스폰수 무관 보존·예산 동일·same-map-same-wave(random-map-pool 계약 승계).
- 테스트 참조 맵은 안 건드림(풀 5맵은 test-free 확인됨 — 사용자 지시).
- 파이프라인 커버리지: 골 정거장 N개(유닛 5 반영분).
- **라이브 스모크 의존(리뷰 M1)**: `MovementIntegritySmokeTest` 가 Battle 씬을 실제 로드해 이 풀을 태운다 → 유닛 2 의 proxy dist==0 전환이 **선행**돼야 멀티골 풀 교체 후 green. 유닛 6 은 유닛 2 이후에.

## 제작된 맵 (풀 5장, asset 파일명 유지 = GUID 안정)

| asset (기존명) | 새 모양 | 스폰/골 | 실루엣 |
|---|---|---|---|
| Serpent | TwoRivers | 2/2 | 상하 평행 S 복도 |
| Coil | Trident | 3/3 | 좌측 빗살 3 복도 |
| Twin | Ladder | 2/2 | 수직 serpentine 2 |
| Spiral | Zig3 | 3/3 | 밴드 지그재그 3 |
| Zig | Corners | 2/2 | 반대코너 대형 L 2 |

전부 **분리 복도 = 각 스폰 자기 골**(서로 한 칸도 안 만남), 이동 ≥20, 2×2 없음, ≤20×12. (파일명↔모양 불일치는 GUID 유지 위한 것 — 배선/덱 페어링 불변.)

## 완료 기준

- [x] 분리복도 멀티골 맵 5장(검증기 통과: 복도 완전 분리·자기 골 도달·2×2 없음·≥20)
- [x] 풀 GUID 유지 덮어쓰기, 배선/덱 페어링 불변(execute_code WriteToDocument)
- [x] 데이터 검증: 5맵 전부 docGoals=genGoals, connectivity=True. 실 sim: Coil 3골 flow field 각 골 dist=0·각 스폰 자기 복도로 dist=19 도달(최근접-골 라우팅 실증)
- [ ] (사용자) Play — 각 스폰이 자기 골로 독립 진행, 모든 골 누수 작동, 골 마커/구조물 N개 렌더 정상
- [x] EditMode green(1278), 콘솔 bake 에러 0

확인 2026-07-23 — 검증기(scratchpad/akmaps_mg.py)로 5맵 설계·검증 후 execute_code 로 풀 asset GUID 유지 덮어쓰기. 멀티골 데이터·connectivity·flow field 실증. 사용자 Play 육안만 남음.
