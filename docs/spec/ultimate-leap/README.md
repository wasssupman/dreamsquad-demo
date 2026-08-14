# ultimate-leap — 보스 궁극기 도약: 이탈 → 예고 → 강습

> 상태: **완료 2026-08-02 (사용자 Play 확인).**
> 선행 의존: `docs/spec/leap-flight-state/`(LeapFlight 태그) — 함께 구현됨.
> EditMode 1815 중 1813 통과·실패 0. 커밋·검증 범위·되돌리면 안 되는 의도는 `6_handoff_summary.md`.

## 목표

점프를 좋아하는 보스 컨셉의 정점 스킬. 발동하면 보스가 **화면 밖 공중으로 이탈**하고, 착지 지점
N타일이 **빨갛게 예고**되며, **2초 후 착지해 범위 피해**를 준다. 이탈 동안 보스는 공격 불가·이동
불가·**피격 불가**다. 생존당 1회만 발동하는 궁극기 개념.

기존 일반 도약(SelfBlink — 즉시 텔레포트 + 뷰 아치, 피격 가능)은 **현행 유지**. 이건 별개의 신규 스킬이다.

## 검증 질문

> 체력 30% 에서 보스가 판을 떠나고, 착지 예고 타일을 보고 **방어유닛을 빼는 회피 플레이가
> 성립**하는가? 예고된 타일과 실제 착지·피해 범위가 항상 일치하는가? 이탈 2초 동안 보스는
> 완전히 판 밖 존재(공격·피격·이동 전무)이고, 착지 후 정상 복귀하는가?

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_payload_and_state.md` | `DcPayloadKind.UltimateLeap` + `UltimateLeapState` + bake |
| 1 | 시뮬 | `1_trigger_and_sequence.md` | 발동 arm(착지셀 고정) + 시퀀스 시스템(타이머→착지) |
| 2 | 시뮬 | `2_capability_gates.md` | 피격·타겟팅 완전 차단 + `LeapFlight` 재사용(공격·이동) |
| 3 | 프레젠테이션 | `3_visual_channel.md` | 신규 채널 + 상승/숨김/강하 연출 |
| 4 | 프레젠테이션 | `4_telegraph_tiles.md` | 착지 예고 빨간 타일 (기존 tint 경로 재사용) |
| 5 | 배선 | `5_asset_wiring.md` | 짱쎈놈 슬롯(30%) + Play 검증 + CLAUDE.md 채널 등재 |
| 6 | 인계 | `6_handoff_summary.md` | 커밋·검증·되돌리면 안 되는 것 |

## Feature-wide 계약

1. **명명 `UltimateLeap` (사용자 결정 2026-08-02).** 역할(궁극기)이 이름에 포함된다 — 훗날 다른
   보스가 비궁극기로 재사용하게 되면 재명명을 검토한다(enum 은 int 직렬화라 C# rename 안전.
   단 컴포넌트/SO **필드명**은 에셋 생성 후 rename 금지 — `bossUnit` 교훈).
2. **스킬 = 데이터, 메커니즘 = 코드.** "생존당 1회"는 코드 어디에도 없다 — `fraction ≥ 0.5` 면
   두 번째 경계가 음수라 재발동이 수학적으로 불가(`HealthThresholdEval`). 다른 보스 재사용 =
   에셋에 슬롯 한 줄. 예고 시간은 payload `duration` 필드(제약 6 — 2초를 하드코딩하지 않는다).
3. **피격 완전 차단 + `IncomingDamage` 매 틱 드랍.** 이탈 중 타겟 후보에서 제외되고, 이미 날아온
   피해(잔여 투사체·DoT 포함)도 버린다. **적립 금지** — 버퍼를 스킵만 하면 착지 프레임에 2초치
   피해가 몰아서 터진다. 따름정리: **공중 사망이 없다 = 착지가 보장된다** — abandon 경로는
   teardown 뿐이다.
4. **착지점은 발동 프레임에 고정.** 예고는 약속이다 — 착지 직전 재계산하면 회피 플레이가 거짓이
   된다. sim 위치는 이탈 동안 출발지에 머물고(이동 잠금이라 goal 도달 사고 없음), 착지 프레임에
   기존 `BlinkRequestEventsSingleton` seam(Combat→Movement)으로 텔레포트 — 신규 이동 채널 0.
5. **시퀀스는 sim 소유, Battle 도메인 시계.** 2초는 회피 창이자 피해 게이트 = 게임 규칙이다.
   슬로모 중엔 예고도 함께 느려져야 시뮬과 어긋나지 않는다. (일반 도약의 창이 브리지 소유인 것과
   비대칭이 맞다 — 그쪽은 연출 정합, 이쪽은 게임플레이.)
6. **`LeapFlight` 재사용.** 공격·이동 잠금은 선행 spec 의 태그가 담당하고, 이 spec 은 피격·타겟팅
   축(`UltimateLeapState` 존재 = 차단)만 추가한다. 잠금은 두 스킬이 공유, 무적은 궁극기 전용 —
   레이어가 갈리는 것이 계약이다.
7. **이식성 4조항** (사용자 요구 2026-08-02): (a) 신규 규칙 함수 시그니처에 ECS 타입 금지,
   (b) 규칙은 전역을 읽지 않는다 — dt·현재값 주입, (c) 시스템 셸에서 순수 호출을 지우면 배관만
   남아야 한다(셸의 분기 = 이식 비용), (d) 신규 규칙마다 EditMode 동반. 이번 spec 이 재사용하는
   기존 "한 발 걸친" 체인(`DefenderDensity`/`BlinkMath` 의 NativeArray 시그니처,
   `TryResolveBlinkDest` 의 `FlowFieldSingleton` 의존)은 **이번에 고치지 않고** 여기 명시만
   한다 — 해당 체인을 실제 수정하는 별도 Demo spec에서 재평가할 기술부채다.
8. **신규 채널 1개** (`UltimateLeapVisualEventsSingleton`, Combat→Bridge). lifecycle 3점 세트
   (생성 Persistent / 싱글턴 파괴 / Dispose)는 브리지 소유. 종료 시 CLAUDE.md 채널 목록에 27번째로
   등재한다.
9. **잔여 투사체는 수용된 quirk.** 이탈 순간 날아오던 투사체는 sim 출발지(보스가 서 있던 자리)에
   착탄하되 피해는 계약 3 이 버린다. 뷰에서는 빈 땅에 착탄 — 빈도가 낮고(투사체 잔여 비행 <0.5s)
   "간발의 차로 피했다"로 읽혀 수용.
10. **경계 무겹침.** `fraction 0.7`(30%) 은 진동갑주 0.2 의 배수도, 일반 도약 0.5·0.9 와도 겹치지
    않는다 — 같은 프레임 동시 발동 없음(boss-jjangssen 계약 5 준수).

## 파이프라인 커버리지

대조: `docs/reference/object-pipeline-map.md` §적 + §투사체. 신설 오브젝트는 없고 기존 보스의
스킬 경로 추가다.

| 정거장 | 이 spec | 비고 |
|---|---|---|
| 데이터 SO | unit 0·5 | payload kind 추가 + `Enemy_Boss_Jjangssen.asset` 슬롯. 신규 SO 타입 0 |
| 스폰 진입점 | **N/A** | 기존 보스 — 생성 경로 무변경 |
| ECS 컴포넌트 | unit 0 | `UltimateLeapState`(Combat). `LeapFlight` 는 선행 spec |
| 시뮬 시스템 | unit 1 | `HealthThresholdSystem` arm 추가 + **신규 시스템 1**(`UltimateLeapSystem`, Combat, ISystem) |
| 이벤트 큐 | unit 3 | 신규 1(`UltimateLeapVisualEvents`) + 기존 `BlinkRequest`·`ProjectileSpawnRequest` 재사용 |
| View/Pool | unit 3 | 기존 `SpineUnitView` — 상승/숨김/강하 + lift 시각 규칙(flight-lift-feel) 재사용 |
| 체력 표시 | 자동 | 뷰 숨김 동안 오버헤드는 화면 밖 앵커 — unit 3 에서 숨김 확인 |
| 씬 wiring | unit 3·5 | 채널 lifecycle 3점 세트 + 예고 tilemap 참조(기존 것 재사용 시 0). `unity-feature-wiring` 스킬 대상 |
| 투사체(슬램) | 재사용 | SkyFall × TileAoe 기존 경로(`ResolveLanding` 슬램과 동일 규약) |

## 후속 후보 (범위 밖)

- **다른 보스 재사용** — 에셋 슬롯 값만으로 성립하는지가 이 설계의 검증. 실제 두 번째 소비처가
  생기면 payload 필드 부족분(예: 착지 셀 선택 정책)이 드러날 수 있다 — 그 spec 이 추가한다.
- **행동트리 재검토 트리거** — 보스 스킬이 "우선순위 경쟁·인터럽트·다페이즈" 를 요구하게 되면
  규칙 목록(trigger×gate×payload)의 한계다. 그 전까지 BT 도입은 제약 8 위반.
- **예고 UI 강화** — 경고음·점멸 가속(착지 임박)·`BossWarningView` 톤 연동.
- **`CanBeHit` 술어 집약** — 무적 소스가 2개 이상 생기면(`UltimateLeapState` 외) 순수 술어로 집약.
