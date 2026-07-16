# 무의식 저주 확장 3종 (subconscious-curse-expansion)

> 상태: 초안 — critic 리뷰(REVISE) 반영 완료, 사용자 승인 대기 (2026-07-16)
> 선행: `subconscious-cursed-relics`, `gift-phase`, `dreamcatcher-awakening-hand`, `unit-status-fx`

## 목표

무의식(저주) 풀에 신규 카드 3종을 추가한다. 설계 근거는 `docs/reference/드림캐쳐_각성안_최종스펙_v1.md` §6:
**EV 등가·분산만 크게 / 리스크는 선불·즉발, 리턴은 후불·지속(세탁 차단) / 덱에 넣고 싶게 만드는 크기**.

세 장은 서로 다른 자원을 담보로 잡는다 — 유닛 가동시간 / 점수(유출 허용치) / 전장 안전.
기존 2장(재앙의 심장=유닛 목숨, 금이 간 성배=전군 내구)과 담보가 겹치지 않는다.

## 카드 스펙 (제안 수치 — 전부 SO 튜닝 노브)

| 카드 | id | 타입 | 리스크 (선불·즉발) | 리턴 (후불·지속) |
|---|---|---|---|---|
| **호접몽** | `sub_butterfly_dream` | Unit | 부착 즉시 4초 잠(공격 정지). **피격 시 꿈이 깨져 리턴 소멸** (기존 wake-on-hit 재사용) | 잠 완주 시 자신 공격력 +35% (매치 영구) |
| **몽마의 계약** | `sub_incubus_pact` | Squad | 부착 순간 **유출 허용치 −1** (환불 없음, 잔여 허용치 ≥2 필요) | 호스트 생존 중 전 아군 공격력 +25% |
| **살찌운 제물** | `sub_fattened_offering` | Unit | 필드의 악몽 1체에 표식 — 즉시 **받는 피해 −30%** (더 튼튼해짐) | 표식 악몽 처치 시 각성치 **×3**. 유출 시 무보상 회수 |

세탁 차단 검증: 셋 다 리스크가 부착 순간 세상에 존재하고, 리턴은 완주/생존/처치를 요구한다.
호스트/표식 대상이 죽어도 카드 고유의 추가 각성 수익은 없다(표식 처치 보상은 "잡아야만" 나오는 리턴 그 자체).
단, 호접몽·살찌운 제물의 **리스크 크기는 대상/배치 선택에 종속**된다(후방 유닛 재우기 = 다운타임만 지불, 저보상 몹 표식 = net 손해) — 몽마·재앙의 무조건 선불과 달리 회피/실패 여지가 있는 것은 함정이 아니라 **표적 선택 스킬 표현**이다 (critic m5 명문화).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_butterfly_dream.md` | 코드·데이터·PlayMode | 호접몽 — DreamCocoon payload + Effects 감시 시스템 |
| 1 | `1_incubus_pact.md` | 코드·데이터·테스트 | 몽마의 계약 — 유출 허용치 선불 지불 |
| 2 | `2_bounty_mark_mechanism.md` | 코드·데이터·PlayMode | 살찌운 제물 — 표식/보상/회수 메커니즘 (API 직호출 검증) |
| 3 | `3_bounty_targeting_and_indicator.md` | 코드·씬·Play | 적 타겟 드래그 커밋 + 표식 인디케이터 |
| 4 | `4_gift_pool_integration.md` | 통합 | 카탈로그 등록 · 림 풀 6장 · 덱빌더 제외 · Play smoke |
| 5 | `5_card_art.md` | 아트 | 신규 3장 타로 스타일 카드 아트 |
| 6 | `6_handoff_summary.md` | 인계 | 종료 시 작성 |

## 공통 계약

1. **§6 규율이 카드 구조로 강제된다**: 리스크는 부착 커밋 순간 적용, 리턴은 조건 충족 시에만. 어떤 경로로도 리스크 환불 없음.
2. **신규 NativeQueue 채널 0.** 기존 StatModifier/CcEffect 채널과 기존 드레인(EnemyKilled/GoalReached)을 재사용한다. 호접몽만 Effects 소유 컴포넌트+ISystem 1쌍 신설(채널 아님).
3. **enum append-only**: `DcPayloadKind.DreamCocoon=14`, `BountyMark=15`. `EnemyKilledEvent` 에 `Entity entity` 필드 append (awakeningReward 선례).
4. 모든 수치는 카드 SO 가 소유. 코드 하드코딩 금지.
5. 무의식 풀 = 기존 3(재앙의 심장·금이 간 성배·느린 각성) + 신규 3 = **6장**, 림의 선물은 서로 다른 2장(기존 `PickRim` 무변경). `category=Subconscious` → 덱빌더 자동 제외 유지.
6. 부착/회수/사이클 수명주기 유지. 살찌운 제물만 회수 트리거를 확장한다: 표식 악몽 **사망 또는 유출** 시 큐 맨 뒤 복귀(신규 bridge 이벤트 `EnemyGone(Entity)` — 기존 두 드레인에서 발화).
7. 이중 부착 사전검증(LethalTimer preflight 선례): 같은 호스트에 두 번째 DreamCocoon, 같은 악몽에 두 번째 표식은 **아무 것도 적용하기 전에** 거절(무차감).
8. 표식의 스탯 적용은 기존 `EnqueueStatModifier`(DmgTakenMul, origin=Dreamcatcher)를 적 엔티티에 사용. 강화 오라(empower aura)는 defender 한정 reconcile 이라 적에겐 켜지지 않는다(의도).

## 파이프라인 커버리지

신규 플레이 오브젝트/렌더 경로 없음 → `object-pipeline-map` 대조 N/A.
표식 인디케이터는 기존 **StatusFx 아키타입**(StatusFxKind append + registry 프리팹)을 재사용하며 신규 정거장을 만들지 않는다. 호접몽 완주 버프의 시각 피드백은 기존 empower aura 가 자동 커버(Dreamcatcher origin 모디파이어).

## 비목표

- 재앙의 심장 규율 위반(리턴 선불) 재설계 — 별도 논의
- 느린 각성의 무의식 풀 제외/교체 — 풀 6장 유지, 거취는 후속
- 표식/코쿤 전용 고급 VFX·SFX (MVP 인디케이터만)
- 저주 전용 등급·드랍·보유 시스템, 스쿼드 저주 "덱당 1장" 덱규칙 (무의식은 덱빌딩 밖 — 림 배정이라 현 구조상 무의미)

## 후속 후보

- 표식 전용 프리팹 연출(현상금 문양), 코쿤 잠 전용 연출
- 시트 재export (`dreamcatcher-sheet-sync` 7번 JSON 이 cursed-relics 이후 stale — 본 spec 3장 포함해 일괄 갱신)
- 몽마의 계약 잔여 유출 허용치 HUD 연동 (backlog "남은 허용 유출 HUD" 와 합류)
