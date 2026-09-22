# 트리거→발동 레이어 rev 2 — 백지 초안(타 세션) × 현행 실측(census) 합성

> 2026-09-22. 상대 초안의 개념 골격(5개념·소유자/수명·전순서·무할당·이벤트 스트림 검증)을 채택하고,
> census 가 실측한 계약(값 스냅샷·seam 창·동기 커맨드·수식자 분리·매치 규칙 고정)으로 그 골격을 «이 게임에 맞게» 좁힌다.
> 어느 한쪽의 복사가 아니다 — 두 문서 어디에도 없던 것은 ★ 로 표시.

---

## 1. 어휘 — 6개념으로 닫는다 (상대 5 + 커맨드)

| 개념 | 정의 | 출처 |
|---|---|---|
| **Command** ★ | 플레이어 입력(배치·퇴근·부착·액티브·착지·제출). 틱 phase 0 에서 **동기** 적용, `Receipt{accepted, reason}` 반환 | census 1-4(Immediate seam 동기 계약) |
| **Event** | 「일어난 사실」의 불변 값 struct. **필드 겸직 금지** — `Site{pos, originBody}` 두 짝 · 진영 · 통행층 · 방향 · 피해량을 값으로 싣는다 | 불변식 5 · unit 23b |
| **Binding** | `{event, subject ★, conditions[], effect, owner, lifetime, instanceId, seq}`. 유닛 스킬·카드·기믹·Squad·배치 오라·액티브 전부 이 타입 하나 | 상대 §4 + 본 세션 변형 ① |
| **Condition** | 닫힌 술어 집합(현행 `DcGateKind` × `DcGateSubject`) 의 **AND 배열**. 미개방 조합은 bake 에서 loud 거절 | 상대 §4 · trigger-gates 계약 |
| **Effect** | `ISkill` concrete(무상태, `ctx.Emit` 만). 34개 현행 계승 + `AreaBlast` 통합 등 정리 | foundation 계약 3·5 |
| **Owner / Lifetime** | 소유자 = `SimEntityId` 하나(유닛·거점·**Match 호스트 = 예약 id 0** ★). 수명 = `{영구 · N회 · 판 종료 · 소유자 소멸 · 퇴근}`, 만료는 `OnDetach` 사건 | 상대 §4 |

**어휘 밖 = 하나.** `HeavyStrike`(자기를 부른 공격의 출력 수정)는 사건 반응이 아니라 **공격 출력 수식자**다.
수식자는 `AttackMod`(AttackContext 위 순수 함수, 이벤트 없음) 축에 산다 — 상대가 «어휘 조사에서 가장 먼저 부딪힐 지점»으로
예측한 DamagePre/Post 경계가 바로 이것이고, census 는 그 경계를 이미 결정으로 갖고 있다(pre-scan 불변식).

**환원표(현행 전량)** — 상대 §6 의 blueprint 행을 현행 어휘로 교체:

| 현행 기능 | event | subject | condition | effect | owner | lifetime |
|---|---|---|---|---|---|---|
| Unit 카드(트리거 × 페이로드 26 살아 있는 조합) | 10종 | Self | 게이트 2조합 개방 | concrete | host 유닛 | 소유자 소멸 |
| `trigger == None` 카드 3 | `OnAttach` | Self | — | concrete | host | **N회=1** |
| 적 `nightmareMechanics` · 배치 스킬(`OnPlace`) · 가디언 캐스트 · 퇴근 페이로드 | 동일 | Self | 동일 | concrete | 유닛 | 소유자 소멸 |
| Squad 카드 | `OnPlace` | **Any(defender, 클래스 필터)** + 판 시작 1회 | — | ApplyStat | **Match** | 판 종료 |
| `PlacementAura`(어휘 밖이던 것) | `OnPlace` | **Any(defender)** | host 생존 | ApplyCc(EventTarget) | host 유닛 | 소유자 소멸 |
| `SplitOnDeath`(다른 배선이던 것) | `OnDeath` | Self | — | SpawnUnits ★(intent 신설) | 유닛 | 소유자 소멸 |
| 기믹 4 (번아웃·온천·레드불·사직서) | `PeriodicTimer` · `OnDeath(any defender)` · `OnPickupConsumed` · `OnResignationThreshold` | Any | 시즌 게이트 | 스택/힐/픽업 스폰/메테오 | **Match** | 판 종료 |
| 액티브 카드 | `OnPlayerCast` | — | 쿨다운·비용(Command receipt 가 판정) | concrete 5 | Match(시전자 없음) | 영구 |
| 인수인계·코스트 획득·쿨다운 단축 | 동일 | Self | — | **MetaIntent**(큐 미경유) | host | 소유자 소멸 |
| 궁극기 「생존당 1회」 | `HealthThreshold` | Self | — | BeginUltimateLeap | 보스 | **N회=1** ★(현행은 코드에 없어 경계 음수 hack) |

---

## 2. 해소 파이프라인 — 상대 규칙 1·2·3 을 census 계약으로 구체화

- **규칙 1 (큐 해소)**: 동의. 단 **커맨드는 예외**(phase 0 동기). 커맨드 적용 중 발화하는 Immediate seam 은 그 콜스택에서 끝난다.
- **규칙 2 (전순서 키)**: `(seam, generation, productionSeq, ownerSimEntityId, instanceId)`. 저작 priority 축은 두지 않는다(소비처 0). 동률이 남으면 결함.
- **규칙 3 (연쇄)**: 발동 중 생긴 사건은 `generation+1` 큐 → **같은 seam 창 안에서 세대 순으로 소진**. 깊이 8 초과는 `Report(ChainDepthExceeded)` 로 loud 폐기. 라이브 최장 연쇄는 3(시체폭발→OnKill→잿불).
- **잔여 큐 정책(상대 §8-B)** ★ 확정: 사건은 자기 seam 을 싣는다. **후속 seam 이면 같은 틱, 이미 지난 seam 이면 다음 틱.** 트레이드오프가 아니라 현행 load-bearing 계약(Cast→Attack · Periodic→Emitter · Death 는 파괴 앞).
- **seam 은 파이프라인이 도출**한다(현행 7: Periodic·Attack·Threshold·Death·Lifecycle·Immediate·Cast). 개수를 계약에 적지 않는다.
- **리스너 색인** ★: Self subject 는 host 의 자기 슬롯 리스트, Any subject 는 Match 리스트 하나 — 사건당 비용 = 자기 슬롯 + 전역 소수. 「감지는 분산, 실행은 단일」(계약 6) 유지, 전 유닛 재스캔 없음.

---

## 3. 저작 → 런타임 (상대 §8-E 답)

```
SO 저작 (DcMechanic: enum 값 append-only, 시트 왕복)
   │  Materializer (매치 시작 1회, Unity 층)
   ▼
plain BindingDef 표 (BattleSim asm) ── bake 게이트 3 유지(감지자 없음 / 부착 전용 / 라우팅 없음 → loud skip)
   │  스폰·부착 시
   ▼
Binding 인스턴스 (풀에서 대여 · instanceId 발급 · lifetime 시작)
```
- `skillId`·`DcSkillRouting` 간접층 은퇴(Burst 근거 소멸). payload → concrete 1:1. 트리거별 분기는 **이벤트의 Site** 가 흡수.
- 자기 죽음 중복 억제 키 = `instanceId`(현행 `skillId < 64` 비트마스크 대체).
- 어휘 커버리지 ★(상대 §7 채택): 라이브 콘텐츠가 쓰는 `(event, subject, payload)` 조합 목록을 EditMode 가 유지. 미사용 프리미티브 = 삭제 후보, 미검증 조합 = 라이브 경로 금지(trigger-gates 계약 계승).

---

## 4. 두 초안 모두에 없던 것 ★

1. **진행형 상태의 중단 정책 표.** `Begin*` 5종(궁극기·호접몽·시한부·충전·자기 대기)마다 「사망 / 퇴근 / CC / 소유자 소멸」에서 무엇이 되는지를 상태 정의에 동봉. 현행은 시스템마다 제각각 알고 취소 어휘가 0.
2. **Event 의 두 자리 짝 표준.** `Site(Fired)` 와 `Site(Target)` 이 항상 짝으로 다니고 `originBody = 0` 은 「그 자리는 칸」(제약 13 의 두 형). 감지자가 채우고 concrete 는 재질의하지 않는다.
3. **Match 호스트의 시즌 게이트 = lifetime 의 시작 조건.** 기믹 바인딩은 「판 시작 시 활성 시즌이면 부착」— `CreateGimmickConfigIfActive` 의 RequireForUpdate 게이팅이 바인딩 부착 여부로 접힌다.
4. **커맨드 Receipt 가 UI 계약.** 실패한 부착이 손패로 돌아오는 것, 코스트 부족·상한·쿨다운 사유가 트레이 표현과 일치하는 것(census: 구조 > 자원 순서)이 receipt 사유 enum 하나로 고정된다.

---

## 5. 상대 초안에서 가져온 것 / 좁힌 것 / 뺀 것

| 가져옴 | 좁힘 | 뺌 |
|---|---|---|
| 5개념 닫힘 · Lifetime 1급 · 소유자=EntityId(씬 포함) · 전순서 키 · 무할당 · 이벤트 스트림 검증 · 골든 러너 첫 소비자 · 어휘 커버리지 · 「이벤트 = 연출 훅」 | 이벤트 최소 필드 → 값 스냅샷 struct · 입력=이벤트 → 커맨드 분리 · 페이즈 8 → 감지자 창 도출 seam · DamagePre 체인 → AttackMod 순수 함수 · 「모드 = 바인딩 세트」 → 기믹까지만 | 서버 재전투 코어 전제 · 고정소수점 코어 · 저작 priority 축 · ECS 잔존 전제 · death-only recycle |
