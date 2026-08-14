# Spec — Defender Clock-Out (배치 유닛 퇴근)

상태: **구현 완료 2026-08-14 — units 0~3 커밋.** 자동 검증 전부 통과(퇴근 5/5 · 회귀 23/23 ·
EditMode 8/8). 연출은 rev 2(퇴근 스냅) 확정 — rev 3 키링 회수는 시도 후 **기각**
(이유는 [`3_retire_exit_flight.md`](3_retire_exit_flight.md) 상단, 재시도 금지 근거 포함).
남은 것은 **퇴근 스냅 육안 확인**과 **`placementCooldown` 값 저작**뿐.
인계는 [`4_handoff_summary.md`](4_handoff_summary.md).

## 검증 질문

> 판 위 유닛을 선택해 "퇴근"을 누르면 그 유닛이 **전용 연출로** 판에서 내려오고, 트레이 셀이
> **쿨타임**으로 돌아왔다가 시간이 차면 다시 배치할 수 있는가? 이동 버튼·초상화 드래그는 사라졌는가?
>
> 그리고 **퇴근이 사망의 결과를 하나도 일으키지 않는가?** — 각성 0, 사직서 0, 작별 선물 0, 사망 애니 0.

## 상위 목표

`defender-relocation` 이 만든 **이동/재배치**를 팀 리뷰 결과 **퇴근 → 쿨타임 → 재배치**로 대체한다.
같은 문제("상한 1 이라 판이 굳는다 · 후반에 코스트가 남는다")를 다른 문법으로 푼다.

| | 이동(폐기) | 퇴근(신설) |
|---|---|---|
| 대가 | 코스트 전액 + 8초 이동모드 | **시간**(쿨타임) + 재배치 시 코스트 재지불 |
| 보상 | 배치 스킬 재발동 + HP 50% 회복 | 없음 — **새 유닛**이 온다(HP 만땅·스킬 1회) |
| 상태 | 엔티티 유지(부착·스택 보존) | 엔티티 소멸 → **재배치는 새 개체** |
| 판의 공백 | 없음(순간 이동) | **쿨타임 동안 그 자리가 빈다** ← 진짜 대가 |

이동의 대가는 "코스트"라는 **자원 축** 하나였다. 퇴근의 대가는 **시간과 판**이다 — 후반에 남아도는
코스트로는 못 사는 것이라 상한 1 의 긴장을 유지한다.

**이동 기능은 지우지 않는다.** 배선(버튼 / 초상화 드래그)만 끊고 코드·테스트는 그대로 둔다.

---

## ⚠ rev 1 — 사망 채널 재활용을 폐기한 이유

초안은 `RetiredTag` 를 `DeadTag` **위에 얹고** `DefenderDeathEvent.retired` 플래그로 갈랐다.
사용자 지적으로 폐기한다. 근거 셋:

1. **`DeadTag` 의 정의가 좁다** — "Tag indicating the unit's **health has reached zero**".
   퇴근한 유닛은 HP 가 0 이 아니다. 얹는 순간 **태그가 거짓말을 한다.**
2. **죽음은 이미 이 게임의 저작 어휘다** — `DcTriggerKind.OnDeath` 는 드림캐쳐 카드가 선언하는
   1급 트리거고(`OnKill`·`OnShieldBreak` 와 나란한 사건 축), `UnitLifecycleSystem` 이 그 슬롯을
   읽어 작별 선물을 굽는다. 퇴근을 그 채널에 플래그로 얹으면 **지금 있는 모든 `OnDeath` 카드와
   앞으로 만들 카드가 전부 "진짜 죽은 건가?"를 되물어야 한다.**
3. **코드베이스에 정답 선례가 있다** — 적은 두 가지로 판에서 내려간다: `DeadTag`(죽음)와
   `PastGoalTag`(유출). **형제 태그지 한 태그 + 플래그가 아니다.**

형제 태그로 가려다 **더 나은 길**을 찾았다: 퇴근은 sim 을 아예 타지 않는다(계약 1).

## ⚠ rev 2 — 리뷰 반영 (2026-08-13)

ECS 경계 리뷰 + 과설계 리뷰 2벌의 지적을 반영했다. **핵심 골격("ECS 변경 0")은 두 리뷰 모두
코드 근거로 유효 판정.** 고친 것은 전부 주변 절차다:

| 지적 | 반영 |
|---|---|
| "브리지가 이미 10곳에서 쓰는 관행" 이 **과장** — 실제 9건이고 유닛은 적 2건, **defender 직접 파괴 선례는 0건** | 계약 1 문구를 사실대로 고침 |
| `ReleaseDefenderTile` 에 뷰 반납을 넣으면 `bool playDeathAnim` 플래그 파라미터를 부르게 된다 | **뷰 반납을 공유 함수에서 뺐다** — 호출처 소유(계약 3) |
| 열린 항목 ② "뷰 반납 실측" 은 **실측 불요** — `deathAnimation` 기본값이 `"die"`(`DefenderUnitData.cs:215`) 라 퇴근이 사망 애니를 타는 게 코드로 확정 | 열린 항목 삭제 → **unit 3(퇴근 연출)** 로 승격 |
| unit 0 이 고치는 패널 버튼 2줄을 **unit 2 가 즉시 덮어쓴다**(순수 churn) + unit 0 완료 기준이 중간 상태(액션 버튼 없는 패널) 육안 검증을 요구 | unit 0 을 **접근자 차단 + 테스트 반전**으로 축소 |
| `SetMoveState` → `SetRetireState` **재특화**는 슬롯을 또 기능 이름에 묶어 계약 10("플래그 한 줄이면 부활")을 거짓으로 만든다 | **중립화**: `SetActionState(bool, string label)`(계약 9) |
| `relocationEnabled` 가 `[SerializeField]` 면 인스펙터에서 켜고 씬을 저장할 때 값이 조용히 리포에 박힌다 | **인스펙터 미노출**(계약 10) |
| PlayMode 단정 11개는 신규 런타임 ~40줄 대비 과함. 사직서·작별선물·각성 대조군은 비싼 셋업으로 "안 부른 코드가 안 돌았다"를 확인 | **`DeadTag` 미부착 단정 하나로 가족 전체를 덮고 5~6개로 축소** |
| `RecoverCardsHostedBy` — `OnDefenderDied` 에만 `RevokeDreamcatcherEffects` 3줄이 있어 "같은 루프"는 절반만 사실 | 계약 5 에 주의 명시 |

---

## feature-wide 계약

1. **퇴근은 사망 경로를 한 글자도 건드리지 않는다. ECS 변경 0.**
   브리지가 엔티티를 **직접 파괴**한다(`_em.DestroyEntity`). 신규 컴포넌트 0 · 신규 NativeQueue
   채널 0 · 시스템 수정 0. 갈라짐이 전부 **부작용 없이** 성립한다:

   | 사망의 결과 | 퇴근에서 왜 안 일어나나 |
   |---|---|
   | 사직서 드랍 | `ResignationDropSystem:38` 은 `WithAll<DeadTag, DefenderUnitTag>` — **`DeadTag` 를 안 단다** |
   | 작별 선물(OnDeath AoE) | 그 bake 가 `UnitLifecycleSystem:101` 의 `DeadTag` 분기 안에 있다 |
   | 각성 게이지 지급 | `DefenderDied` 를 **안 쏜다**(퇴근은 `DefenderRetired`) |
   | 사망 애니메이션 | 뷰 반납을 `NotifyDeath` 가 아니라 `Despawn` 으로 간다(계약 3·11) |

   > ⚠ **선례의 강도를 정직하게 적는다.** 브리지의 `DestroyEntity` 호출은 9건이고 그중 **유닛**은
   > 적 2건뿐(`BattleBridge.cs:5565`·`5805` — 공성 유출/골 붕괴), 나머지는 캐리어·필드·구조물 같은
   > 인프라다. **defender 엔티티를 브리지가 파괴한 선례는 0건**이다. 그래도 설계는 성립한다 —
   > 퇴근은 UI 기원 행위이고 브리지가 유일한 게이트웨이이며, **브리지가 배치한 것을 브리지가
   > 수거하는 대칭**이다. 구현 시 이 사실을 주석에 남긴다("첫 사례임을 알고 한다").

2. **순찰병 회수는 공짜다.** `PatrolLifecycleSystem:48` 의 소환사 생존 판정 첫 줄이
   `state.EntityManager.Exists(owner)` 다 — 엔티티가 사라지면 다음 sim 틱에 순찰병에 `DeadTag` 가
   붙고 `UnitLifecycleSystem` 이 파괴한다. **파괴 자체가 신호**라 별도 배선이 없다.
   (1 틱 지연은 무해 — 소환사가 없을 뿐 순찰병은 정상 유닛이다.)

3. **공유하는 것은 브리지의 판 정리 절차 1벌뿐이다.** `ReleaseDefenderTile(cell, out binding)` 로
   추출하고 호출처 2개(사망 드레인 / 퇴근)가 공유한다. 내용: 바인딩 제거 · 점유 해제 · 배치
   하이라이트 갱신 · 타일 게이지 제거 · 시너지 재계산 · 빔 종료.
   ⚠ **뷰 반납은 이 함수에 넣지 않는다.** 사망은 `NotifyDeath`(사망 애니), 퇴근은 `Despawn`(즉시)
   으로 갈리므로, 넣으면 `bool playDeathAnim` 같은 **플래그 파라미터**를 부르게 된다. 뷰 반납은
   처음부터 **호출처 소유**다.

4. **브리지 이벤트는 형제다** — `DefenderDied` / `DefenderRetired`.
   `DefenderDied` 의 런타임 구독자는 정확히 2개이고(`DefenderSelector:167` 트레이 리페인트,
   `DreamcatcherHandController:242` 각성+회수) **퇴근은 둘 다 다르게 굴어야 한다**(전자는 쿨타임
   시작 추가, 후자는 각성 제거). 플래그였다면 두 구독자 전부 `if (retired)` 를 써야 한다.

5. **부착 드림캐쳐 카드는 회수한다** — 사망과 같다. 회수 루프를 `RecoverCardsHostedBy(host)` 로
   뽑아 호출처 3개가 공유한다(사망 / 적 소멸 / 퇴근).
   ⚠ **"같은 루프"는 절반만 사실이다** — `OnDefenderDied` 에만 `handle > 0 → RevokeDreamcatcherEffects`
   3줄이 있다. 통합하면 `OnEnemyGone` 이 그 분기를 물려받는 **행동 확장**이므로, 유일한 writer 인
   `AttachAndSpend` 에서 **적 부착이 항상 handle 0 인지** 한 줄 확인하고 넘어간다.

6. **비용은 없다. 환급도 없다.** (사용자 결정) 대가는 (a) 재배치 시 코스트 전액 재지불
   (b) 쿨타임 (c) 그동안 판이 빈다, 셋으로 충분하다.

7. **쿨타임 값 = 기존 `DefenderUnitData.placementCooldown` 재사용.** (사용자 결정) 신규 필드 0.
   `PlacementCooldownRuntime` 도 손대지 않는다 — `StartCooldown` 을 퇴근 시점에 한 번 더 부를 뿐
   (overwrite 라 중복 안전). `0 = inert` 라 값 없는 유닛은 즉시 재배치 가능 — **쿨타임을 켜는 것은
   저작 행위**다.
   > ⚠ **`defender-board-limit` 계약 10 을 뒤집는다.** "상한 1 과 `placementCooldown` 을 함께 걸지
   > 않는다(죽은 값)" 였는데, **퇴근 직후가 정확히 "소진이 풀렸는데 아직 못 놓는" 구간**이다.
   > 계약 10 은 rev 로 기록한다.

8. **확인 절차 없음 — 누르면 즉시 퇴근.** (사용자 결정) 되돌릴 수 없다는 사실은 쿨타임이 흐르는
   것으로 사후에 읽힌다.

9. **패널의 액션 슬롯은 기능 이름을 갖지 않는다.** 이동 버튼이 쓰던 1칸을 그대로 쓰되
   `SetMoveState(bool, int cost)` → **`SetActionState(bool enabled, string label)`** 로 중립화하고
   라벨 소유를 컨트롤러로 옮긴다(`"퇴근"` / `$"이동  {cost}"`). 슬롯을 retire 로 **재특화하면**
   나중에 이동을 되살릴 때 라벨·시그니처·cost 파라미터를 전부 되돌려야 해 계약 10 이 거짓이 된다.
   진입 조건 = **전투 페이즈 + 살아 있는 대상 + 비-busy**. 미달 시 **흐림**(숨김 아님).

10. **이동 비활성화는 코드 상수 1개.** `DcInspectController` 의 `relocationEnabled`(기본 false).
    **`[SerializeField]` 로 노출하지 않는다** — 인스펙터에서 켜고 씬을 저장하면 값이 조용히 리포에
    박힌다(이 프로젝트에서 반복된 사고). 진실원은 코드 하나.
    이 상수가 막는 것은 최종적으로 **`Relocation` 접근자 하나**다(패널 버튼은 unit 2 가 통째로
    교체한다). 접근자가 null 이면 `DefenderDragSlot.TryBeginRelocationFromSlot` 이 **자기 폴백**으로
    종전 동작(판 위 그 유닛 선택)에 돌아간다 — 슬롯 파일은 건드리지 않는다.

11. **퇴근은 전용 연출로 사라진다** (사용자 결정 2026-08-13). 사망 애니(`deathAnimation`, 기본
    `"die"`)를 타지 않는다. 연출 = **배치 아치의 역재생** — 판에서 위로 아치를 그리며 자기 트레이
    슬롯으로 딸려 올라가 사라지고, 그 칸에서 쿨타임이 차오르기 시작한다. 상세 unit 3.

## 작업 단위 목록

| 파일번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 배선 해제 | `0_disable_relocation_entries.md` | 이동 진입구 차단 (상수 1개 + 테스트 반전) |
| 1 | 토대 | `1_retire_path.md` | `ReleaseDefenderTile` 추출 + `RetireDefender` + `DefenderRetired` |
| 2 | UI·보상 | `2_retire_button_and_cooldown.md` | 액션 슬롯 중립화 + 퇴근 버튼 + 쿨타임 + 카드 회수 |
| 3 | 연출 | `3_retire_exit_flight.md` | 사망 애니 배제 + 트레이로 딸려 올라가는 아치 |
| 4 | 인계 | `4_handoff_summary.md` | 커밋/검증/주의점 |

## 파이프라인 커버리지

**N/A** — 새 플레이 오브젝트를 신설하지 않고, 생성→렌더 경로를 바꾸지 않는다. 추가되는 것은
브리지 API 1 · 브리지 이벤트 1 · UI 버튼 1 · **기존 비행 헬퍼의 세 번째 소비자** 1 뿐(ECS 변경 0).

## ⚠ 열린 밸런스 항목

**사망에는 쿨타임이 없고 퇴근에는 있다.** `placementCooldown` 을 크게 잡으면 **"퇴근시키느니 죽게
두는 게 빠르다"** 가 성립한다. 지금 판단은 *그대로 두고 값으로 조절* 이다 — 죽게 두는 쪽은 그동안
유닛이 계속 맞아주고 퇴근은 즉시 자리를 비운다는 차이가 있어 자동으로 지배 전략이 되진 않는다.
다만 값을 넣을 때 이 비교를 의식하고, 실플레이에서 "방치가 최적"으로 굳으면 **사망에도 같은
쿨타임을 태우는 rev** 로 간다.

## 후속 후보 (현 스코프 밖)

- **사망에도 쿨타임** — 위 밸런스 항목이 실측으로 확인되면.
- **`DcTriggerKind.OnRetire`** — "퇴근하면 발동" 카드(예: 퇴근 유닛 자리에 핫식스 드랍).
  이 스펙이 주는 것은 **사건 신호**와, 죽음에 없는 성질 하나다: 브리지가 파괴 시점을 소유하므로
  **파괴 직전에 그 유닛의 `DcTriggerSlot` 버퍼를 직독할 수 있다**(죽음은 `UnitLifecycleSystem` 이
  ECB 로 파괴하고 브리지가 그 뒤에 드레인해서 payload 를 **미리 구워** 날라야 한다).
  채널을 섞었다면 이 카드가 **사망에서도 터졌을** 것이다.

  ⚠ 다만 **"enum 에 추가만 하면 된다"는 아니다.** 퇴장 지점에는 trigger×payload 디스패처가
  **없다** — `OnDeath` 는 `SelfTileAoe` 한 쌍에 하드코딩돼 있고(`break; // first OnDeath slot
  only (v1)`), 다른 `OnDeath` 용법(`SplitOnDeath`)은 슬롯을 쓰지도 않는다. 필요한 것: ① `OnRetire`
  enum + bake 화이트리스트 + `DcApplicability` + 카드 문안 formatter ② `DcPayloadKind.DropPickup`
  + 퇴근 경로의 payload 분기. 스폰 자체는 이미 있다 — `Pickup{cell, kind, remainingLife}` ·
  `PickupPresenter` · `PickupConsumeSystem` 라이브(핫식스 = `PickupKind.Redbull`), 생성 3줄.

  ⚠ **스코프가 진짜 갈림길이다.** DC 트리거는 전부 **host-scoped**. "이 유닛이 퇴근하면 그 자리에"
  는 지금 구조에 맞지만(카드 1장 ≈ spec 1 unit), "**아무** 아군이나 퇴근하면" 은 다른 모양이다 —
  squad-wide 는 modifier 로만 존재하고 트리거 축으로는 없다.
- **이동 기능 부활 판단** — 상수 하나로 되돌아온다. 상한 2+ 콘텐츠가 생길 때 재검토.
  ⚠ 다만 **"이동은 죽었다"가 절반만 참**이다: 퇴근의 `CanRetire` 가 쓰는 `TryGetDefenderAt(…, out
  busy)` 가 `BattleBridge.Relocation.cs:39` 에 있다. 나중에 relocation 을 지운다면 공짜가 아니다.
- **퇴근 코스트 부분 환급** — 지금은 0.
- **네이밍** — "퇴근"은 시즌 기믹 "집에 가도 되나요?"(clock-out)와 화면에서 겹친다. 코드 식별자는
  `Retire*` 로 갈라 뒀으니 **UI 문안만** 바꾸면 되는 문제다(후보: "철수" · "복귀" · "해산").
