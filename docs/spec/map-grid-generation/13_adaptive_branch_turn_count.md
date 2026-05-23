# Unit 13 — Branch Turn Count 도 맵 크기 비례 (adaptive)

## 목적

기존에 `minBranchTurnCount` 는 SO 고정값(default 3)이라 그리드 크기에 무관했다. 큰 맵에서도 "최소 3번 꺾임" 만 보장 → 화면이 비어 보일 수 있음. `EffectiveMinBranchTurnCount(grid) = max(SO, min(W,H)/3)` 로 cell count 와 동일한 스케일 정책 적용.

## 변경 대상

- 수정: `MapGridGenerationSettings.cs` — `EffectiveMinBranchTurnCount(int2)` 메서드 신설.
- 수정: `MapGridValidator.cs` — `settings.MinBranchTurnCount` 대신 `settings.EffectiveMinBranchTurnCount(gridSize)` 사용.
- 수정: `MapGridValidatorTests.cs` — `Validate_LongLPath_PassesOk` 가 새 threshold 통과하도록 Z-path 로 교체.
- 수정: `README.md` — "최소 지류 꺾임" bullet 갱신.

## 정책 (적용 후 기댓값)

| Grid | min(W,H)/3 | Effective minTurns (SO default 3) |
|---|---|---|
| 30×15 | 5 | **5** |
| 20×20 | 6 | **6** |
| 20×10 | 3 | **3** |
| 10×20 | 3 | **3** |

## 리스크 / 완화

- 큰 맵에서 turn 요구치가 올라가 outer attempt 실패율이 증가 가능.
- 완화: builder 가 이미 Z(3-turn) → U(2-turn) → L(1-turn) 순서로 시도하므로 첫 라우팅은 보통 3-turn. 합류 후 두 번째 spawn 도 Z 우선. 큰 맵에선 BFS 결과 path 전체 turn 이 5~7 정도 나오므로 통과 가능성 높음.
- sweep 테스트로 검증. 실패율 ≥ 5% 면 SO default 를 낮추거나 max attempts 를 늘려 대응.

## 완료 기준

- [ ] 컴파일 0 ERROR.
- [ ] EditMode 회귀 0 (단, validator turn-pass 테스트는 Z-path 로 갱신).
- [ ] 3 preset × 50 seed sweep 통과 (성공률 ≥ 90%).
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
