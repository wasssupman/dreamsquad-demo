# 5. 테스트 + Handoff

## 목적

전환 완료 후 회귀 방지 EditMode 테스트 추가 및 SO 점검.

## 변경 대상

- `Assets/_Project/Tests/EditMode/TileRangeTests.cs` (신규)

테스트 프레임워크: NUnit `[Test]`, 위치 `Assets/_Project/Tests/EditMode/`, 네임스페이스 소스 미러.

## EditMode 테스트 목록

### GridMath.ChebyshevDistance

| 입력 | 기대값 | 설명 |
|---|---|---|
| (0,0)→(1,0) | 1 | 직교 |
| (0,0)→(1,1) | 1 | 대각 |
| (0,0)→(2,0) | 2 | 직교 2칸 |
| (0,0)→(2,1) | 2 | 혼합 |
| (3,3)→(1,2) | 2 | 음수 방향 |

### GridMath.RangeToTiles (half-away-from-zero 경계값)

| 입력 | 기대값 |
|---|---|
| 0.5f | 1 |
| 1.0f | 1 |
| 1.5f | 2 |
| 4.5f | 5 |
| 5.5f | 6 |
| 3.0f | 3 |

### 범위 경계

| range | target | 기대 |
|---|---|---|
| 1 | (1,1) | in range |
| 1 | (2,0) | out of range |
| 2 | (2,2) | in range |
| 2 | (3,0) | out of range |

### 시너지 8방향

- 중심 타일 기준 8개 이웃 모두 같은 유닛 → `neighbors == 8`
- 직교 4개만 배치 → `neighbors == 4`
- 대각 4개만 배치 → `neighbors == 4` (기존 0이었던 케이스)

## SO 값 점검 (커밋 전)

float 비정수 `attackRange` / `range` 를 가진 SO 를 확인하고, `RangeToTiles` 결과가 의도와 맞는지 검토. 다르면 정수로 수정.

주요 대상:
- `Enemy_Rootcaster.asset` — `attackRange: 5.5` → `RangeToTiles` → 6
- 스킬 SO (`Skill_Tornado`, `Skill_Meteor`, `Skill_Slow` 등) — `range` 점검

## 완료 기준

- [ ] 신규 EditMode 테스트 전체 통과
- [ ] 기존 EditMode 전체 통과 (170+ 테스트)
- [ ] PlayMode 스모크: range=1 defender 대각 공격 확인
- [ ] PlayMode 스모크: Tornado / Meteor 정사각형 AoE 확인
- [ ] PlayMode 스모크: 시너지 대각 defender 버프 적용 확인
- [ ] SO 비정수 range 값 정수화 완료 또는 round 결과 의도 확인
- [ ] console error / warning 0
