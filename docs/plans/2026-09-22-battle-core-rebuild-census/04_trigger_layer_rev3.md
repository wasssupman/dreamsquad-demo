# 트리거→발동 레이어 rev 3 — 비판 리뷰(`03_critic_rev2.md`, 코드 대조 12건) 반영

> 2026-09-22. rev 2 의 6개념 골격은 유지한다. 환원표에서 실측과 어긋나 «기획 그대로»를 깨던 자리를 전부 현행으로 되돌리고,
> 골격이 그것을 담을 수 있도록 축을 넷 추가한다. 캐스터 4기 제거(사용자 결정)·소환사 유지 반영.

---

## 1. 골격 정정 — 축 4개 추가

| 축 | 왜 필요한가(리뷰 근거) | 정의 |
|---|---|---|
| **fireCap ≠ lifetime** (M9) | 표식 카드 = 발동 1회 + 표식된 적이 사라질 때까지 부착. 한 필드로 못 담는다 | `fireCap`(0 = 무제한) 과 `lifetime` 을 분리. `trigger==None` 3장 = `fireCap 1` + `lifetime 소유자 소멸`. 궁극기 「생존당 1회」 = 그 바인딩만 `fireCap 1`(다른 HealthThreshold 사용자는 다회 발동이 사양) |
| **만료 시 소급 회수 여부** (H6) | PlacementAura 의 스탯은 소급 중화, Sleep 은 등록부 제거만 | `revokeOnExpire: bool`. lifetime 과 직교 |
| **호스트 상태 필터 ≠ Condition** (M10·C4) | 카드 게이트 어휘(`HpBelow` × Self/EventTarget, 개방 2조합)는 그대로 두고, 기믹·픽업이 쓰던 `WithAll/WithNone` 은 다른 축 | `subjectFilter`(alive · notDeploying · faction · unitKind · hasLastRun …) — **저작 노출 없음**, 코어 내부 바인딩 전용. 카드 Condition 은 현행 2조합 유지·fail-closed |
| **정적 (트리거, 페이로드) → (concrete, 형) 표 유지** (C2·C3·H8) | 범위 프리뷰는 드래그 중(이벤트 없음)에 형을 알아야 하고, 시체폭발↔자기폭발은 통행층·예고 시간까지 다르며, 빈사폭주는 origin 이 다르다 | `DcSkillRouting` 은 **은퇴하지 않는다**. 은퇴하는 것은 «Burst 용 int skillId 를 unmanaged 슬롯에 굽는 인코딩»뿐 — 바인딩이 concrete 참조를 직접 든다. concrete 34 는 그대로(AreaBlast 통합 철회, SelfStatBuff 파생 둘 유지) |

어휘 밖 = **5**(HeavyStrike · AttackMod 3종 · charge 소비). 전부 `AttackMod` 축(공격 출력 조립, 이벤트 없음). 「하나」라고 적지 않는다.

---

## 2. 환원표 정정 (rev 2 → rev 3)

| 기능 | rev 2 (틀림) | **rev 3 (현행 그대로)** | 근거 |
|---|---|---|---|
| Squad 카드 | owner Match · 판 종료 · 시작 1회 | owner **host 유닛** · lifetime **소유자 소멸 ∪ 퇴근** · 부착 시점에 판 위 전원 + 이후 배치분 `OnPlace(any)` 상속 · 회수 = 소급 중화 | C1: `handle≥1` 회수, `RecoverCardsHostedBy` 가 사망·퇴근 둘 다 |
| `CostRate` Squad 효과 | ApplyStat | **메타 intent `SetCostRegenMul`**(판 자원) — 라이브 경로는 드림스톤뿐이나 어휘 자리 유지 | 표현 불가 1 |
| 기믹 번아웃·온천 | Match 호스트 PeriodicTimer × Any | **유닛 호스트 바인딩** — 판 시작·스폰 시 부착, 유닛별 타이머(부착 시점 위상)·유닛별 스택(온천). subjectFilter 는 **기믹마다 다르게 현행 그대로**(번아웃 = defender·사망 미제외 / 온천 = 전 유닛·사망·배치중 제외) | C4 |
| 레드불 스폰 cadence | — | **Match 호스트** 주기 바인딩(유일한 전역 주기) | C4 |
| 레드불 소비 | 사건 | 매 틱 공간 폴링(현행) → `OnPickupConsumed` 사건 생산. 재소비 락(`hasLastRun`)은 subjectFilter | M10 |
| PlacementAura | 바인딩 1(ApplyCc) | **바인딩 2**: ApplyStat(공속, `revokeOnExpire true`) + ApplyCc(Sleep, `revokeOnExpire false`). owner host, `OnPlace(any)`, lifetime 소유자 소멸 | H6 |
| SplitOnDeath | `OnDeath` × SpawnUnits | 사건 = **`OnSlain`(피해로 죽음 = 현행 EnemyKilled)**, `OnDeath`(모든 사망 경로) 아님. intent `SpawnUnits` 는 **부모 셀 중심 양자화 칸** + 인덱스 결정 배치 + 첫 슬롯만 + 상한 8 + 순환 차단 | H7 |
| 사직서 임계 | edge 사건 | level 폴링(엔티티 수 / threshold, 한 틱 다중 barrage 사양) → 사건 생산. 순서(드랍 vs 임계 상대 순서)는 **현행 캡처 순서 그대로** | L12 |
| 인수인계 | MetaIntent(Self) | 손패 상태의 **집합 연산**(host 의 부착 카드 중 선언 카드만 뒤, 나머지 앞). 손패 규칙이 코어로 들어오므로 `HandState.RecallOthersToFront(host)` 로 실행 — 바인딩 effect 가 아니라 **퇴근 회수 규칙**의 일부 | 표현 불가 2 |
| 궁극기 생존당 1회 | lifetime N회=1 | `fireCap 1`(그 바인딩만) | 옳았던 것 3 |

---

## 3. 연쇄 규칙 정정 (H5)

- 오늘 **드레인 안 재진입은 0건**이다(concrete 는 `Emit` 만, 사건 생산은 감지자뿐). 시체폭발→OnKill→잿불은 intent → 투사체 → 피해 → 감지로 **틱을 넘는 파이프라인**이다.
- 따라서 세대 BFS 는 **바인딩→바인딩 직접 재진입**(현재 0)에만 적용하고, intent 경유 연쇄는 **각자의 phase 에서** 적용한다(한 틱으로 접지 않는다 — 접으면 반경 멤버십과 연출 리플이 바뀐다).
- 깊이 예산: 재진입 자체가 0 이므로 값은 현행 동작에 무영향. **4** 로 두고 초과는 `Report`. 라이브 근거가 생기면 그때 조정.
- 잔여 큐 정책은 rev 2 그대로(후속 seam 같은 틱 / 지난 seam 다음 틱 = 현행).

---

## 4. 「즉시 반영」 결정의 실체 (M11)

- 적용 phase 는 현행처럼 이동·공격 **앞**(phase 1). 생산자 8곳은 phase 5 라 **여전히 다음 틱** — 전환으로 바뀌는 것은 사실상 없다.
- 실드는 **이미 같은 틱**이다(피해 적용 직전 merge). 「다음 프레임 드레인이 의도」 주석은 생산자 위치에 따라서만 참.
- 유일한 실변화 후보 = `FatigueAccrual` 의 박제된 1프레임 지연. **현행 순서 그대로 박제**한다(기획 그대로). → 사용자 결정 3-①은 「변경 없음」으로 귀결.

---

## 5. 캐스터 제거 범위 (사용자 결정 · 소환사 유지)

| 사라지는 것 | 목록 |
|---|---|
| 유닛 SO 4 + 능력 SO 4 | `Defender_{Fire,Ice,Poison,Blocking}Caster` · `Ability_Hazard_*Caster` |
| 기계 | `HazardCastState/System/Kind` · `CastEvents`(채널 31→30) · `SkillSeam.Cast`(seam 7→6) · `HazardCastAbility` · `CastHazardSkill`(concrete 34→33) · `DcHostArchetype.HazardCast` · `UnitKitSummary` 캐스트 arm |
| 부수 | `PickFallbackTarget` 사용자가 폭탄맨 하나로 줄어든다(체비셰프 잔여는 그 하나) · 「자는 캐스터」 미결 소멸 · 「캐스트 = 그 유닛의 공격 사건」 계약 소멸 |
| 남는 것 | 길막 장판 개체 자체(투사체 `SpawnBlocker` 페이로드·효과 타일이 생산) · `DefenderClass.Caster` enum 값(소환사가 쓴다) · 소환사·순찰병 전부 |
| 테스트 | EditMode 5 + EditModeAssets 1 이 죽는다 — 삭제 |

---

## 6. 비용·할당 (표현 불가 4·5)

- 기믹의 실비용은 리스너 조회가 아니라 **대상 열거**(전 유닛 순회)다. 바인딩화해도 남는다 — 「전 유닛 재스캔 없음」은 슬롯 스캔에만 참이라고 명시.
- 할당 상한을 먼저 정한다: 유닛 ≤ 256 · 바인딩/유닛 ≤ 8 · Match 바인딩 ≤ 16 · 후보 버퍼 64(현행) · 사건 큐 ≤ 1024/틱. 등록부·보고 문자열이 실제 할당 지점 — `Report` 는 코드만 싣고 문장은 뷰가 만든다(현행).

---

## 7. rev 2 에서 그대로 사는 것

6개념 · 커맨드 ≠ 이벤트 + receipt(`DcRejectReason` 8종이 그 enum) · 값 스냅샷 이벤트(자리↔몸 짝) · 소유자 = `SimEntityId`(Match 예약 id) · Lifetime 1급 · 전순서 키 `(seam, 세대, 생산 순번, owner, instanceId)` · seam 은 파이프라인 도출 · 진행형 상태 중단 정책 표(`LastRun` 추가) · 어휘 커버리지 추적 · `DcApplicability` 무변.

## 8. 확인 대기
- 마메모의 「웨이브 회전 정지」가 코드 어디에 있는지 미확인(spec README 는 escort 만 언급). 웨이브 규칙에 보스 훅이 있다면 phase 6 매치 규칙과 바인딩 사이의 경계 사례.
