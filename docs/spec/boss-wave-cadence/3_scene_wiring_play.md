# 3 — 씬 배선 + 덱 값 + Play 검증

## 목적

BossWarningView 를 BattleScene 에 배치·배선하고, 덱 asset 에 보스 편성 값을 넣어 실제 매치에서
보스@5웨이브 + "꿈결 위기!!" 배너가 뜨는지 검증한다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — BossWarningView GameObject + BattleBridge 참조
- 덱 asset(예 `WaveA.asset`) — 보스 편성 값

## 구현

**덱 asset**(UnityMCP/에디터):
- `bossUnit` = `Enemy_Boss_Nightmare`
- `bossWaveInterval` = 5, `bossEscortMin` = 3, `bossEscortMax` = 4
- `waveGeneratorVersion` = 2 (오프라인 분석 로그 라벨 — `BattleLogger` JSON 기록용, 런타임 enforce 없음)

**BattleScene**:
- 배틀 UI 계층에 GameObject `BossWarningView` 추가 → `BossWarningView` 컴포넌트.
- `warningFont`/`warningMaterial` = `ScoreHudView`가 쓰는 Kanit Bold Italic SDF 에셋과 동일하게 할당,
  `vignetteSprite` = 스코어 milestone 비네트와 동일 계열 할당.
- `BattleBridge._bossWarning` 참조 배선.
- 씬 저장은 **미저장 WIP 베이크 주의**(feedback: scene-save-bakes-wip / scene-checkout-discards-user-camera):
  저장 전 diff 로 사용자 카메라·미저장 컴포넌트가 박히지 않는지 확인, 필요 시 스냅샷→HEAD→delta 재적용 격리.

**Play 검증**(스크립트 e2e — TestModeContext.Set + StartBattle, project: scripted-battle-e2e-verify):
- 짧은 덱 또는 `WavePlan_BossTest` 로 보스 스폰 유도 → 보스 엔티티 `BossTag` 확인 + 배너 출현 스크린샷.
- 라이브 seed 경로도 5웨이브 도달 시 보스+배너 확인(장시간이면 interval 임시 축소로 스모크).

## 완료 기준

- Play smoke green: 보스 스폰 순간 크림슨 "꿈결 위기!!" 배너 출현, 잡몹 스폰엔 미출현.
- 보스 웨이브에 보스 1 + 잡몹 3~4 실제 스폰, `BossTag` 부착.
- 콘솔 클린. **사용자 Play 체감 확인**(배너 스타일/타이밍) 후 완료 기재.
