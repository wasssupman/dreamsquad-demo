# 5 — Handoff Summary

## Commit

| 커밋 | 내용 |
|---|---|
| `bbfc06c1` | unit 0 — `bossPool` 로테이션 (기존 덱 무회귀) |
| `ee5fd831` | unit 1 — 보스 에셋 + 연출 SO 2종 + 카탈로그 |
| `527ea53d` | unit 2 — 진동갑주 (경계마다 자기중심 폭발) |
| `67875169` | unit 3 — 보스 어그로 + 행동정지·넉백 면역 |
| `21b9aaec` | unit 4 — 집단 도약 (밀집 지점 착지) |
| (본 커밋) | 덱 7개 로테이션 투입 + handoff |

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
- **Play 육안 검증은 아직 안 했다.** 아래 Follow-up 참조.

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

**Play 검증(미완)** — 코드/테스트는 끝났지만 육안 확인이 남았다.

- 나이트메어와 짱쎈놈이 웨이브마다 번갈아 나오는지, "꿈결 위기!!" 배너가 둘 다 정상인지.
- 실루엣이 육안으로 구분되는지(`partSkins` 조합 + 스케일 2.6). 안 되면 `partSkins` 재선택.
- 방어유닛 3기 인접 배치 → **가디언이 있어도** 3기가 동시에 피해를 받는지(면역 회귀 가드).
- HP 경계마다 폭발이 터지고 **AOE VFX 가 보이는지**, 그 다음 밀집 지점으로 뛰는지.
- 폭발이 **도약 전 자리**에서 터지는지(계약 8 육안 검증).
- 말파이트가 보스를 때려도 **보스가 떠오르지 않는지**(연출 desync 가드).
- `Projectile_JjangssenLeap` 의 슬램 스파이크가 **출발지**에도 재생되는데(arm 이 단일 인덱스)
  어색하면 출발/착지 연출 분리를 후속 후보로.

**밸런스** — 전부 placeholder다. 특히 HP 950 근거는 **방어유닛 20종** 기준 추산인데 지금 24종이고
늘어난 4종이 전부 화력형이라 낮을 수 있다. 폭발 60/도약 반경(밀집 2·링 6)도 실측 조정 대상.

**PlayMode 테스트(미작성)** — spec 이 요구한 e2e 는 아직 없다. 우선순위 항목:
① 가디언이 보스를 때려도 `Aggroed` 미부착(잡몹엔 부착) ② Bleed 스택 → 보스 HP 실제 감소
③ 보스 HP 79% 세팅 → 밀집 셀 근처 blink + `DeadTag` 프레임엔 미발동.

**범위 밖(README 후속 후보 참조)** — 보스 트리거 개방(`AttackN` 대회전 / `OnKill` 학살 가속,
`OnKill × SelfStatBuff` bake 의 `buffStat` 미설정 버그 동반) · 면역으로 죽은 카드·유닛 재설계 ·
프리뷰/런타임 seed 불일치.
