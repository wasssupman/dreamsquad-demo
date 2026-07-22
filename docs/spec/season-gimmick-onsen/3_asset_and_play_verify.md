# 3. 에셋 + gimmickPool 등록 + Play 검증 [ECS]

## 목적

`OnsenGimmickData` SO 에셋을 만들고 `BattleConfig.gimmickPool` 에 등록해 기믹을 매치 라이프사이클에 편입하고, 열기 회복↔손실 반전 체인을 Play 로 실측한다. 검증 질문에 답한다.

## 변경 대상

- **신규**: `Assets/_Project/Data/Gimmick/Gimmick_Onsen.asset` — `OnsenGimmickData` SO
- `Assets/_Project/Data/Config/BattleConfig.asset` — `gimmickPool` 에 Onsen 4번째 등록

## 구현

1. **`Gimmick_Onsen.asset`**(MCP `manage_scriptable_object`): `gimmickId=G4_Onsen`, `displayName="뜨끈하니 좋네요오오.. 뜨겁네?"`, description(회복→과열 반전 설명), 수치 SO 기본값(heatInterval 5 / flipThreshold 5 / healPercent 0.1 / lossPercent 0.1 / heatMaxStack 6).
2. **`gimmickPool` = [Burnout, RedBull, ClockOut, Onsen]** 4개(공유 pool 정식 등록).
3. 씬 배선 불요 — 열기는 기존 힐/데미지 숫자 팝업으로 노출(플레이스홀더). 전용 상태FX/게이지는 후속.

## Play 강제 방법 (테스트 편의)

기믹은 `matchSeed` 결정론 배정이라 특정 기믹 강제는 시드 의존. RNG 재현 위험을 피해, **테스트 동안 `gimmickPool` 을 `[Onsen]` 단독**으로 두면 `PickIndex%1=0` 으로 시드 무관 항상 Onsen. 검증 통과 후 4개로 복원 후 커밋. (`debugFixedMatchSeed` 로도 가능하나 pool 크기 바뀌면 매핑 변동.)

## 완료 기준 (검증 질문)

- [ ] Onsen 배정 매치(로그 `gimmick=G4_Onsen` + `OnsenGimmickConfig 주입`)에서 5초마다 유닛에 초록(회복) 숫자, 6번째 틱부터 빨강(손실)으로 반전.
- [ ] 열기 손실만으로는 **아무 유닛도 죽지 않고 HP 1 바닥**(전투 데미지로는 정상 사망).
- [ ] 적에게도 적용 — 초반 적 질겨지고(회복) 후반 적 녹음(손실).
- [ ] `gimmickEnabled=false` 또는 다른 기믹 → 이 시스템 전무(무변화).
- [ ] 결정론: 같은 `debugFixedMatchSeed` → 같은 기믹 배정.

> Play 실측 후 healPercent/lossPercent/heatInterval 밸런스는 SO 노브로 조정(플레이 후 튜닝).
