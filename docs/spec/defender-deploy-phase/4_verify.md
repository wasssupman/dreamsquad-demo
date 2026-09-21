# 4 · 검증

## EditMode (코어 26초 + 에셋 5초)

- unit 0·1·3 신규 테스트 + 기존 무회귀. 기지 실패 2건(bomb_man·boomerang 문안) 외 빨강 0.
- 실에셋 길이 표(`DeployMotionSecondsAssetTests`)가 곧 페이즈 길이 정본 — 값이 바뀌면 여기가 먼저 빨개진다.

## Play 계측 (MCP · 배치 단계 + 전투)

1. `PlaceDefenderAs` 로 실드셔틀 배치 → `StartBattle` → 사거리 안 적: 트랙 0 이 `skill(once)` 을 **완주**한 뒤 `attack4(once)`. 활성화 로그 `remaining=0.67`.
2. 스나이퍼: `drop` 1.67s 완주 후 첫 발사(선딜 `hitDelaySec` 1.0 은 그 뒤에 더해진다 — 두 값의 성격이 다르다는 것을 로그로 확인).
3. 캐논(Hit 0.97): 착지 후 0.97s 에 배치 미사일 발동(`JustDeployed` → OnPlace).
4. 순찰병(모션 0): 착지 다음 틱 활성화.
5. D&D 로 같은 유닛: 비행 `dropTotalSeconds`(unscaled, 현재 0.45) → 착지 → 위와 동일. 드래그 슬로모 중 배치 모션이 같이 느려짐.
7. **StartBattle 전** 배치: 착지 + 모션 길이 뒤 `Activated` 트레이스가 그 프레임에 찍힌다(드레인이 `_running` 앞).
8. 비행 중 컨트롤러 OnDisable → pending 잔존 0.
6. 되돌리기 버튼이 활성화 프레임에 사라진다.

## PlayMode lane (8분 · 사용자 승인 후)

- `PlacementAuraTest` · on-place 스킬 테스트(캐논·배스티온·실드셔틀·샷건맨) · `ActionLockTest` · `DropDismountTest` · `RelocationPlacementSessionTest`·`RelocationSmokeTest`(폴링 ≥ 모션 길이). 0.45 리터럴 단언은 unit 2 에서 교체됐어야 한다.

## 골든 코퍼스

- 골든이 방어유닛을 `PlaceDefenderAs` 로 놓는다면 활성화 타이밍이 모션 길이만큼 밀려 **전건 빨감이 정상**이다 — 재베이크. 단 「남의 WIP 드리프트」(map·wave)와 섞이지 않게 먼저 `git status` 로 격리 확인(메모리: 골든은 머신 상태를 상속한다).

## 완료 기준

- 위 6 계측 로그 + 스크린샷 1장(실드셔틀 skill 프레임에 적이 사거리 안).
- 사용자 Play 육안 확인 후 커밋 해시 기록.
