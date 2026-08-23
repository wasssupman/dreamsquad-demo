# heart-stress-axis — 마음 스트레스 (판을 끝내는 축의 귀환)

> 상태: **작성됨 2026-08-23** · unit 0 착수 대기
> 선행: [`three-minute-kill-race`](../three-minute-kill-race/README.md)(완료 — **이 spec 이 그 계약 4개를 의도적으로 뒤집는다**),
> [`goal-stability`](../goal-stability/README.md)(마음 = 전투 대상), [`goal-tower-siege`](../goal-tower-siege/README.md)(공성 모델),
> [`battle-structures`](../battle-structures/README.md)(진영×종류 축)
> 정본 갱신 대상: `docs/reference/ingame-flow.md`(종료 조건 3경로)

## 목표

**마음이 다시 판을 끝낸다.** 적이 마음을 팰수록 스트레스가 차오르고, 100 이 되면 그 자리에서 판이
끝난다. 대신 악몽을 잡을수록 스트레스가 내려간다 — 마음은 「버티는 벽」이 아니라 **밀고 당기는 저울**이다.

승/패는 여전히 없다. 어떻게 끝나든 **그때까지의 처치 수가 곧 결과**이고 그대로 랭킹으로 간다.

### 검증 질문

> *"마음이 뚫리면 끝난다는 압박과, 잡을수록 숨통이 트인다는 보상이 맞물려 3분을 긴장으로 채우는가?"*

`three-minute-kill-race` 의 검증 질문은 *"질 수 없다는 걸 알고도 손이 바쁜가"* 였다. 이 spec 은 그
전제를 버린다 — **질 수는 없지만 끝날 수는 있다.**

## 사용자 확정 명제 (2026-08-23) — 이 spec 의 헌법

| # | 명제 |
|---|---|
| 1 | **누수(유출)가 없다. 마음이 파괴되면 게임이 종료된다.** |
| 2 | 종료는 **3경로**: 3분 만료 · 유저 제출(유지) · 스트레스 100 |
| 3 | 승/패 표기 없음. 그때까지의 **처치 수 = 결과 = 랭킹 환산값** |
| 4 | 스트레스는 마음 체력의 **0→100 정규화 · 차오르는** 구조 |
| 5 | 스트레스가 오를 때 화면이 빨갛게 튀고, **높을수록 강하고 빠르게 / 낮을수록 느리게** |
| 6 | 마음 체력은 **악몽을 처치할수록 회복** |
| 7 | 회복량 = **`awakeningReward` 재사용 + 스케일 배율만 튜닝** (새 저작 필드 신설 없음) |
| 8 | 게이지 자리 = **보드 위, 마음 머리 위 바** |
| 9 | 돌격형 자폭 피해 축(`stabilityDamage`)은 **당장 그대로 둔다** |
| 10 | **마음은 무조건 1개**임을 깔고 진행한다 |

## 핵심 구조 — 정본은 마음 `Health` 하나

```
정본 : 마음 Health(value, max)                   ← 지금 그대로. 새 수치 타입 없음
피해 : 공성 = 적 공격력 | 자폭 = stabilityDamage    ← 지금 그대로 (명제 9)
회복 : IncomingHeal ← 킬 드레인                    ← 신규는 이것 하나
표시 : 스트레스 = (1 − value/max) × 100            ← 순수 함수 (차오름)
종료 : value == 0                                 ← EndMatch 3번째 경로
```

**새 ECS 컴포넌트 0 · 새 이벤트 채널 0 · 새 맥락 0 · 새 SO 0.** 마음은 이미 `IncomingDamage` 를
받고 있고, 힐은 이미 같은 시스템의 **같은 줄**(`DamageApplicationSystem` 의
`newHp = min(max, value − dmg + heal)`)이 처리한다. 붙일 것은 **버퍼 하나**뿐이다.

> **「100」은 표시 정규화이지 HP 최대치가 아니다.** `Health.max` 를 진짜 100 으로 두면
> `Enemy_Basic` 공격력 20 이 5대에 판을 끝낸다. 정본 HP 는 지금 스케일(`goalStabilityMax`)을 유지한다.

## 실측 기준선 (2026-08-23 · 착수 전)

밸런스 판단의 출발점. **이 수치는 코드/에셋에서 잰 것이고 unit 5 에서 재측정한다.**

| 항목 | 값 | 출처 |
|---|---|---|
| 마음 HP | **1000** (라이브 덱 10종 전부) | `AttackDeck.goalStabilityMax` |
| `Enemy_Basic` | 공격력 20 · 쿨다운 **0.5s** = DPS 40 | `Enemy_Basic.asset` |
| ⇒ Basic 1기 단독 | **25초**에 스트레스 100 | 1000 ÷ 40 |
| ⇒ Basic 5기 | **5초** | |
| 고빈도 공성수 | `Skimmer` cd 0.2(DPS 100) · `WaypointAir` cd 0.2(DPS 50) | 각 asset |
| 자폭(`Runner`·`Swift`) | `stabilityDamage` 1 = **스트레스 0.1** | 명제 9 로 유보 |
| `awakeningReward` | 잡몹 2 / 엘리트·중간 3 / 보스 5 / **슬라임 분열체 0** | 적 SO 23종 |

**마음 HP 1000 은 지금도 이미 무르다** — 배치가 늦으면 1분 안에 판이 끝날 수 있다. unit 5 의 첫
후보는 첫 요청의 **「본능(1000)의 1.5배 = 1500」** 이다.

## 뒤집는 계약 — 넷 다 명시적으로 되돌린다

| # | 계약 | 출처 | 이 spec |
|---|---|---|---|
| 1 | 시스템은 판을 끝내지 않는다 (`EndMatch` 2곳 초과 금지) | three-minute-kill-race | **3번째 경로 추가** (`stress_full`) |
| 2 | 마음은 판정 권한이 0 이다 | 〃 | **마음이 판을 끝내는 유일한 시스템 축이 된다** |
| 3 | 마음을 게이지로 그리지 않는다 | 〃 unit 2 | **머리 위 스트레스 바가 화면의 주인공** |
| 4 | 골 엔티티에 `IncomingHeal` 을 붙이지 않는다 | `Battle/Units/GoalTowerTag.cs` 주석 | **붙인다** (unit 2) |

계약 4 는 critic 리뷰에서 발견됐다. 원 명분은 *"`MaxHealthScaleSystem` 이 `Health.max` 를 재계산하면
미러가 깨진다"* 인데 그건 **`ModifierStats` 에만 해당**한다 — `IncomingHeal` 단독은 `Health.max` 를
건드리지 않는다. 그래도 **계약이므로 선언하고 주석을 같은 커밋에서 갱신**한다.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 코어 + 브리지 | [`0_stress_axis_and_end.md`](0_stress_axis_and_end.md) | 스트레스 산식(순수 함수) + `EndMatch` 3번째 경로 + 마음 1개 가드 + 테스트 개정 3건 |
| 1 | 프레젠테이션 | [`1_heart_stress_bar.md`](1_heart_stress_bar.md) | 마음 머리 위 차오르는 바 (`OverheadBarSkin.Stress`) |
| 2 | ECS(Units) + 브리지 | [`2_kill_relief.md`](2_kill_relief.md) | 처치 회복 (`IncomingHeal` 버퍼 + 킬 드레인 힐) + 계약 4 선언 + 테스트 개정 1건 |
| 3 | 프레젠테이션 | [`3_stress_screen_feedback.md`](3_stress_screen_feedback.md) | 화면 연출 두 축 — 개별 튐 + 수위 맥박 |
| 4 | 코어 + UI | [`4_result_and_log.md`](4_result_and_log.md) | 결과 화면·로그 어휘 정합 (`stress_full` · 「남은 마음」 줄) |
| 5 | 밸런스 + 검증 | [`5_balance_and_golden.md`](5_balance_and_golden.md) | 회복 배율·마음 HP 튜닝 + **골든 코퍼스 재녹화** |
| 6 | 인계 | `6_handoff_summary.md` | 종료 시 작성 |

**의존**: `0 → 1`(바가 그릴 산식이 먼저) · `0 → 4`(종료 라벨이 생겨야 화면이 읽는다) ·
`2 → 5`(회복이 있어야 배율을 잰다) · `0,2 → 5`(골든은 마지막). `3` 은 `0` 뒤라면 언제든.

## Feature-wide 계약

- **정본은 마음 `Health` 하나다.** 스트레스는 별도 리소스가 아니라 그 값의 **표시 반전**이다.
  두 번째 정본(싱글턴·미러 카운터)을 만들지 않는다 — 만드는 순간 동기화 코드가 생긴다.
- **종료 경로는 정확히 3개다**: `complete`(3분 만료) · `submitted`(유저 제출) · `stress_full`(스트레스 100).
  **이 셋 말고 `EndMatch` 를 부르는 코드를 새로 만들지 않는다.** (three-minute-kill-race 의 계약을
  숫자만 2→3 으로 바꿔 승계한다 — 「시스템이 마음대로 판을 끝낸다」로 번지는 것을 막는 장치는 그대로 필요하다.)
- **승/패는 없다.** `stress_full` 은 결과 **라벨**이지 패배가 아니다. `MatchTally` 에 승패 필드를
  만들지 않는다. 서버 제출값은 무변경(`SubmissionScore => Kills`).
- **누수는 발생하지 않는다.** 붕괴 프레임에 판이 끝나므로 `_breachedCells` 가 영원히 비고,
  `_goalReachedCount` 증가 경로 2곳이 도달 불가가 된다. **이건 부작용이 아니라 명제 1 의 실체다.**
- **도달 불가가 된 코드를 이 spec 에서 지우지 않는다.** `OpenGoalCellAfterBreach` ·
  `LeakSiegingEnemy` · `MatchTally.Leaks` · `defeatGoalReachedCount` · 몽마의 계약 선불
  (`TryPayLeakAllowance`)은 **판정만 끊고 휴면**시킨다. 되돌릴 때의 비용과 diff 크기 때문이다 → 후속 후보.
- **마음은 1개다.** 라이브 `MapDocument` 15종 전부 `goals` 길이 1 임을 확인했다(2026-08-23). 다만
  **기계는 1~4 를 허용**하므로(`map-rework` 계약 3 = "콘텐츠만 은퇴, 멀티골 기계는 유지") unit 0 이
  저작 가드를 세운다. 가드 없이 2개가 저작되면 「첫 붕괴가 끝인가 마지막 붕괴가 끝인가」와
  「회복이 어느 마음에 들어가는가」가 미정의로 남는다.
- **회복량은 `awakeningReward` 를 재사용한다**(명제 7). 새 per-enemy 필드를 만들지 않는다.
  튜닝은 `AttackDeck` 의 **배율 하나**로 한다. ⚠ **SO 원값**(`killedType.awakeningReward`)을 쓴다 —
  `evt.awakeningReward` 는 「살찌운 제물」 배율이 곱해진 값이라, 그걸 쓰면 카드 하나가 각성과
  스트레스 회복 두 축을 겸한다.
- **초과 회복은 버린다.** `DamageApplicationSystem` 의 `min(maxHp, …)` clamp 가 이미 그렇게 한다.
  마음 만피 구간에서 킬의 회복 가치가 0 이 되는 것은 **인지된 수용**이다(저장하려면 별도 기계가 필요).
- **`GamePhase` enum 의 값을 건드리지 않는다.** `Data/Camera/CameraDirectionConfig.asset` 에
  **정수로 직렬화**되어 있다. 새 종료 경로도 기존 `Tally → Result` 전이를 그대로 탄다.

## 파이프라인 커버리지

**N/A — 신규 플레이 오브젝트가 없다.** 마음(골 타워)은 `goal-stability`/`battle-structures` 가
이미 세운 아키타입이고, 이 spec 은 그 **표시와 종료 판정만** 바꾼다. 머리 위 바는 본능·적 마음이
이미 쓰는 `UnitOverheadUiLayer` 경로를 그대로 재사용한다(스킨 1종 추가).
`docs/reference/object-pipeline-map.md` 갱신 대상 아님.

## 리뷰 매칭

- **unit 2** = 골 아키타입 컴포넌트 추가 + 킬 드레인 → **ecs-reviewer**
- **unit 0** = 브리지/코어 판정 + 테스트 → 일반 리뷰 (ECS 컴포넌트 무변경)
- **unit 1·3·4** = Mono 뷰/UI → 일반 리뷰

## 후속 후보

- **돌격형 자폭 피해 축 재저작** [S] — `stabilityDamage` 는 마음 HP 가 1~5 이던 시절의 유물이라
  지금은 1발 = 마음의 0.1% 다. `Runner`·`Swift` 는 마음에 사실상 무해하다. 명제 9 로 이번엔 유보.
  ⚠ **「공격력으로 통일」은 함정이다** — 그 둘에게 `AttackState` 를 주면 `canSiege=true` 가 되어
  골에서 안 죽고 「필드에 적 0기」 판정을 막아 웨이브가 안 넘어간다(`battle-structures` unit 0 이
  회귀로 규정해 제거한 이력). 재저작한다면 **값만** 1000급으로 올리는 쪽이다.
- **휴면 코드 정리** [M] — 위 계약의 도달 불가 6종. A/B 판단이 끝난 뒤에.
- **몽마의 계약 코스트 재지정** [M] — 유출이 구조적으로 불가능해져 `leakAllowanceCost` 가 영구
  무의미해진다. 기존 Follow-up Backlog 의 같은 항목과 **합류**한다.
- **마음 만피 구간의 킬 가치** [S] — 초과 회복이 버려져 「지금 잡을 이유」가 사라지는 구간.
  저장(오버힐 풀)·점수 보너스·회복 대신 다른 보상 중 택일.
- **마음 피격 데미지 넘버** [S] — 지금 마음은 맞아도 숫자가 안 뜬다(`AttackUnitTag` 적 전용 필터).
  `goal-stability` 후속 후보와 **같은 항목**.
- **스트레스 서사 회수(해몽)** [L] — 판 후 마음이 겪은 일을 서사로 돌려준다. 기존 backlog 항목.
