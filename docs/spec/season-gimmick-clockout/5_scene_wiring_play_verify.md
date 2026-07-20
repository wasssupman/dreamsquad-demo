# 5. wiring + Play 통합검증

## 목적

`Gimmick_ClockOut` asset 생성 + `gimmickPool` 등록으로 기믹을 매치 라이프사이클에 편입하고, 전체 체인(퇴근→사직서→메테오)을 Play 로 실측한다. 검증 질문 4개에 답한다.

## 변경 대상

- `Assets/_Project/Data/Gimmick/Gimmick_ClockOut.asset` (신규) — `ClockOutGimmickData` SO
- `Assets/_Project/Data/Config/BattleConfig.asset` — `gimmickPool` 에 ClockOut 3번째 등록

## 구현

1. **`Gimmick_ClockOut.asset`**(MCP `manage_scriptable_object`): `gimmickId=G3_ClockOut`, displayName "집에 가도 되나요?", 수치 SO 기본값(clockOut 10 / threshold 5 / meteorCount 3 / dmg 40 / tileRange 1 / warn 1.2 / stagger 0.4), `meteorProjectile=Projectile_Meteor`(기존 재사용).
2. **`gimmickPool` = [Burnout, RedBull, ClockOut]** 3개(옵션 A: 공유 pool 정식 등록).
3. 씬 배선 불요 — `resignationViewPrefab` null → 절차적 흰 종이 플레이스홀더. (정식 아트는 후속.)

## 완료 기준 (검증 질문)

- [ ] ClockOut 배정 매치(로그 `gimmick=G3_ClockOut` + `ClockOutGimmickConfig 주입`)에서 전투 시작 후 배치 유닛이 10초에 퇴근(소멸) + 타일에 사직서(흰 종이).
- [ ] 사직서 5장 → 5장 소멸 + Walk 타일 3곳 메테오 순차 낙하 → 적만 피해.
- [ ] `gimmickEnabled=false` 또는 다른 기믹 → 이 시스템 전무(무변화).
- [ ] 결정론: 같은 `debugFixedMatchSeed` → 같은 기믹 배정 + 같은 착탄 셀 시퀀스.

> Play 실측: `debugFixedMatchSeed` 로 ClockOut 배정 시드 고정(`[GameManager] gimmick=G3_ClockOut` 확인) 후 배치→전투. 밸런스 노브(meteorDamage 40 vs 보스 150)는 실측 후 튜닝.
