# 5 — Handoff Summary

## Commit

| 커밋 | 내용 |
|---|---|
| `bbfc06c1` | unit 0 — `bossPool` 로테이션 (기존 덱 무회귀) |
| `ee5fd831` | unit 1 — 보스 에셋 + 연출 SO 2종 + 카탈로그 |
| `527ea53d` | unit 2 — 진동갑주 (경계마다 자기중심 폭발) |
| `67875169` | unit 3 — 보스 어그로 + 행동정지·넉백 면역 |
| `21b9aaec` | unit 4 — 집단 도약 (밀집 지점 착지) |
| `384134a9` | 덱 7개 로테이션 투입 + handoff |
| `0b5edc8b` | `WavePlan_JjangssenTest` e2e 작성 플랜 |
| `7aa769ba` | unit 6 — 도약을 텔레포트에서 아치 도약으로 |
| `6ab41555` | unit 6 rev1 — 착지 슬램 50/반경1 + 비행 시간 +50% |
| `aa7537b3` | **fix** — 도약 첫 프레임 착지지점 팝(드레인을 `LateUpdate` 로) |
| `7dcb38b8` `698faf1a` `b4a5fc32` `0436b29d` `48f41cc0` `74cbbcf0` `5b467560` `828a89ed` | 실플레이 튜닝 (연출·무기·애니·경계·아치) |

설계·리뷰 이력: `docs/plans/2026-07-29-boss-jjangssen-design.md`

## Implemented

- **`AttackDeck.bossPool`** — 보스 2종+ 로테이션. `bossUnit` 은 **rename 없이 병행 유지**하고
  폴백은 생성기(`BuildBossPool`)가 소유한다. **보스 1종이면 rng 를 소비하지 않는다** → 기존 덱의
  웨이브 편성이 byte-identical.
- **`Enemy_Boss_Jjangssen`** — HP 950 / 이속 2.2 / cd 0.6 / cleave 3 / dmg 30 / hitDelay 0.25.
  나이트메어와 같은 Spine 스켈레톤 + `partSkins` 6종 교체 + 스케일 2.6(나이트메어 3.2).
- **진동갑주** `HealthThreshold(0.20) × SelfTileAoe(반경 2, 60)` — 760/570/380/190 에서 4회.
- **집단 도약** `HealthThreshold(0.20) × SelfBlink` — 방어유닛 밀집도 최대 셀로 착지.
- **보스 면역** — 어그로 부착 차단 + 직접 행동정지·넉백 거절. 스택 임계 CC·DoT·Slow 는 통과.
- 덱 7개(맵 6 + Endless)에 `[나이트메어, 짱쎈놈]` 로테이션 투입. `WaveA`/`WaveB` 는 미변경.
- **도약 아치 비행**(unit 6) — sim 은 텔레포트, 뷰만 `KeyringSim.DismountPoint` 궤적으로 0.83초 비행.
  착지 프레임에 자기중심 반경 1 / 50 **슬램 피해**. 출발 퍼프는 없다(인과상 거꾸로 읽혀 제거).
- **최종 튜닝값**: 도약 경계 50%·10% 2회 · 진동갑주 20%씩 4회(WindAoeVFX 2.08) ·
  무기 스파이크 곤봉 + 파리채 · `Attack2`/`Run_Gear` · 아치 factor 0.95 / min 8.5 / launch.y 1.25.

## Key Files

- `Assets/_Project/Scripts/Data/AttackDeck.cs` · `WavePatternGenerator.cs` — pool + `BuildBossPool`
- `Assets/_Project/Scripts/Battle/Combat/DefenderDensity.cs` — 밀집 앵커 순수 함수 (신규)
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — 두 능력의 arm + `DeadTag` 가드
- `Assets/_Project/Scripts/Battle/Effects/CcActionLock.cs` — `IsBossImmune` 술어 (면역 단일 소스)
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs` · `EnemyCcEvents.cs` — `CcSource` 출처 축
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics` 의 `SelfTileAoe` 분기
- `Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset`

## Verified

- **EditMode 1575 중 1573 통과 · 실패 0 · 스킵 2**(둘 다 기존 `[Ignore]`, 무관). 덱 투입 후 재확인.
- 신규 테스트 25개: `WavePatternGeneratorBossTests` +7 · `BossCcImmunityTests` 12 · `DefenderDensityTests` 6.
- 컴파일 에러 0 (Unity `refresh_unity` + 테스트 실행으로 검증).
- **사용자 Play 확인 완료 2026-07-29** — 스폰·외형·능력 구분·도약 아치·팝 수정까지. 실플레이에서
  잡힌 지적 3건(1프레임 팝 · 출발 퍼프 인과 · 능력 구분 불가)은 전부 반영됐다.
- **여전히 미검증**: PlayMode e2e 미작성. 면역 5항목(잡몹 부착 / `AggroCapacity.held` / 직접 Sleep 거절 /
  **Bleed 스택 → 보스 HP 감소** / 말파이트 넉업 억제)과 도약 3항목(밀집 쪽 대조 / `DeadTag` 억제 /
  슬로모 배율 동행)은 육안·자동 모두 확인되지 않았다. 각 unit 하단 "미검증(남김)" 에 동일 목록.

## Notes — 되돌리면 안 되는 것

1. **`bossUnit` 을 rename 하지 마라.** 라이브 덱 9개가 guid 를 직렬화하고 있고, 키를 바꾸면 YAML orphan
   → 생성기의 `null` graceful no-op → **에러도 경고도 없이 전 맵에서 보스가 사라진다.**
2. **보스 1종일 때 rng 미소비 가드를 지워도 안 된다.** 소비하면 `escortCount`/`escortType` 이 밀려
   보스 웨이브 구성이 전부 바뀐다. `SingleEntryBossPoolIsIdenticalToLegacyBossUnit` 가 가드다.
3. **`bossPool` 파라미터는 `Generate` 시그니처 맨 뒤에 있다.** 중간으로 옮기면 positional 로 호출하는
   테스트들이 조용히 다른 값을 받는다.
4. **`attackMethod = Melee` + `projectile = null` 이 cleave 3 의 전제.** `projectile` 을 채우면
   `AttackSystem` 이 투사체 분기를 타서 cleave 가 **조용히** 사라진다.
5. **`SelfTileAoe` 는 `payload.projectile` 이 필수다.** 없으면 `projectileDataIndex = -1` 이 남고
   드레인이 요청을 통째로 버려 **데미지까지 안 나간다**(연출만 빠지는 게 아니다). bake 가 loud 거절한다.
6. **넉업은 `AttackSystem` 생산 지점에서 막는다.** CC(Stun)와 `KnockupVisualEvent` 가 한 쌍이고
   연출은 `CcApplySystem` 을 거치지 않으므로, CC 만 막으면 보스가 떠오르는데 스턴은 안 걸린다.
   부여 거절 원칙("2곳")의 **유일한 예외**이며 이유가 이것이다.
7. **밀집 tie-break 는 row-major 셀 키다.** 청크 순회 순서에 의존하면 결정론이 깨진다.
8. **발동 순서 = 폭발 → 도약**, 시스템 순서(`BlinkApplySystem` 의 `[UpdateAfter]`)가 고정한다.
   슬롯 순서로 뒤집을 수 없다.
9. `SelfBlink` 의 구 착지 정책("위협 리더 근처")은 **은퇴**했다(`nightmare-catcher` 문서에 기재).
   `ThreatEntry`/threat drain 은 별 책임이라 살아 있다.

## Follow-up

**남은 Play 검증** — 아래는 사용자 확인 세션에서 다루지 않았다.

- 나이트메어와 짱쎈놈이 웨이브마다 번갈아 나오는지(작성 플랜은 짱쎈놈 고정이라 **seed 경로로 일반 맵**
  플레이가 필요). 맵당 보스 3회라 **판에 따라 짱쎈놈이 아예 안 나올 수도 있다**(1/8).
- 방어유닛 3기 인접 배치 → **가디언이 있어도** 3기 동시 피해(면역 회귀 가드).
- 두 무리로 나눠 배치 → **더 많이 모인 쪽**으로 뛰는지 대조.
- 말파이트가 때려도 보스가 떠오르지 않는지 / Bleed 스택은 보스 HP 를 깎는지.
- 비행 중 보스 사망 시 공중 정지 없음 / 슬로모(0.3x) 중 도약 배율 동행.

**출발 전용 연출** — 현 blink arm 은 `dataIndex` 가 하나라 출발/착지에 다른 연출을 줄 수 없다.
출발 먼지 같은 걸 넣으려면 인덱스 분리가 필요하다.

**경계 3개 이상** — `fraction` 이 균등 간격이라 임의 경계는 슬롯 1개씩 필요하다(현재 도약 2슬롯).
3개부터는 경계 리스트를 받는 트리거로 바꾸는 게 낫다.

**밸런스** — 전부 placeholder다. 특히 HP 950 근거는 **방어유닛 20종** 기준 추산인데 지금 24종이고
늘어난 4종이 전부 화력형이라 낮을 수 있다. 폭발 60/도약 반경(밀집 2·링 6)도 실측 조정 대상.

**PlayMode 테스트(미작성)** — spec 이 요구한 e2e 는 아직 없다. 우선순위 항목:
① 가디언이 보스를 때려도 `Aggroed` 미부착(잡몹엔 부착) ② Bleed 스택 → 보스 HP 실제 감소
③ 보스 HP 79% 세팅 → 밀집 셀 근처 blink + `DeadTag` 프레임엔 미발동.

**범위 밖(README 후속 후보 참조)** — 보스 트리거 개방(`AttackN` 대회전 / `OnKill` 학살 가속,
`OnKill × SelfStatBuff` bake 의 `buffStat` 미설정 버그 동반) · 면역으로 죽은 카드·유닛 재설계 ·
프리뷰/런타임 seed 불일치.
