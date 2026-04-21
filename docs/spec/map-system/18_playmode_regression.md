# PlayMode Regression

**작업 구분**: Phase 10B (Phase 10 종료 전 최종 검증)

## 목적

Phase 10 전체 결과 (10A data 모델 + 10B procedural + 테마 오브젝트) 가 실제 PlayMode 에서 동작하는지 사용자 검증.

## 검증 시나리오

사용자 Unity Editor 에서 다음 3 시나리오 수행:

### 1. 동일 seed 재현성

- `MapGenerationSettings.defaultSeed = 12345` 고정
- Play 진입 → 맵 A 생성
- 재시작 (Restart 버튼)
- 같은 맵 A 다시 생성 — Walk/Place/Deco 배치 완전 동일, spawn/goal 위치 동일

### 2. 다른 seed 맵 variation

- `MapGenerationSettings.defaultSeed = 0` (매 판 랜덤)
- Play 진입 → 맵 A
- 재시작 → 맵 B (A 와 다름)
- 재시작 → 맵 C (A, B 와 다름)
- 3개 맵 모두:
  - Walk 타일이 spawn → goal 까지 연속 연결
  - defender 배치 가능한 Place 타일 여러 개 존재
  - 배경 오브젝트 (Deco) prefab 시각 확인

### 3. Fallback 맵 진입 (강제)

- `MapGenerationSettings.defaultSeed` 를 여러 값으로 바꿔가며 시도
- **의도적 실패 유도**: `ProceduralMapGenerator.MaxAttempts = 1` + path carve 의 MaxSteps 를 극단적으로 작게 조정 (코드 수정 필요)
- Fallback 하드코딩 직선 맵 진입 확인:
  - 가운데 행이 Walk, 나머지 Place
  - spawn = 좌측, goal = 우측
  - 콘솔에 "Falling back to linear" 경고
- 원 코드 복구

### 4. Phase 9 기능 회귀

- Procedural 맵에서 Phase 9 주요 기능 전부 동작:
  - Portal: exit 타일이 Walk 위면 자율 복귀 / Walk 밖이면 freeze (Phase 9 동일 제약)
  - Tornado: 해제 후 Walk 방향 복귀
  - Meteor: AoE damage 정상
  - Defender 배치 = Place 타일만
  - 적이 Walk 타일만 통과

### 5. 기록 (증거 확보)

각 시나리오 Unity 창 스크린샷 1~2 장 또는 짧은 영상. `docs/plans/recordings/phase10-playmode/` (신규 폴더) 에 저장.

## 판정 기준

- 시나리오 1: 두 맵이 완전히 동일 → 결정성 통과
- 시나리오 2: 3 맵이 시각적으로 뚜렷이 다름 + 모두 플레이 가능 → variation 통과
- 시나리오 3: fallback 진입 시 게임 crash/freeze 없이 플레이 가능 → UX 안전망 통과
- 시나리오 4: Phase 9 기능 0 regression → 회귀 없음

## 실패 처리

- 시나리오 1 실패 (동일 seed 다른 맵): RNG 출처 재확인 (`Unity.Mathematics.Random` + `HashSeed` 함수 결정성)
- 시나리오 2 실패 (맵 모두 동일): `HashSeed` 의 attempt/version 결합이 잘 작동하는지, System.DateTime.Ticks seed 가 판마다 갱신되는지 확인
- 시나리오 3 실패 (fallback 진입 안 됨): `MapConnectivity.AllSpawnsReachGoal` 반환값 검증 로직 확인
- 시나리오 4 실패: Phase 9 회귀 — 해당 기능 debugger 분석

## 완료 기준

- 4개 시나리오 모두 통과.
- 녹화 파일 저장 + commit.
- `docs/residual-issues.md` 에 P10 관련 잔존 항목 정리.
- `docs/PHASE10.md` 작성 (Phase 10A + 10B 통합 종료 스펙).
- `CLAUDE.md` 하단 상태: Phase 10 → Phase 11 prep 으로 갱신.
