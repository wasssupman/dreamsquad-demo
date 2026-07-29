# 4 — 집단 도약 (밀집 지점 착지)

## 목적

최대체력 **50%** 를 하향 돌파할 때 **방어유닛 밀집도가 가장 높은 지점**으로 순간이동한다.
**rev 2 (2026-07-29) — 50% + 10% 2회.** 경계 = `maxHp × (1 − k × fraction)`, k 는 1부터 단조 전진이라
한 슬롯은 **균등 간격**만 표현한다(0.5 → 50%·0%). 50%·10% 는 균등이 아니므로 **슬롯 2개**를 쓴다:

| `fraction` | 발동 경계 | 비고 |
|---|---|---|
| `0.50` | 475 (50%) | k=2 는 0% — `DeadTag` 가드가 막는다 |
| `0.90` | 95 (10%) | k=2 는 −80% → 영구 미발동 |

**코드 변경 0 · 데이터만.** 임의 경계 집합을 늘리려면 슬롯을 하나씩 더해야 하므로 3개 이상부터는
경계 리스트를 받는 트리거로 바꾸는 게 낫다(후속 후보).
벽 세우기를 무력화하고 밀집 응징을 보스가 능동적으로 만들러 오는 형태로 만든다.

`HealthThreshold × SelfBlink` arm 은 이미 존재한다. **신규는 착지 지점 선택 정책 하나다.**
이 spec 이 `SelfBlink` 의 **첫 라이브 사용처**다 — 나이트메어 asset 의 mechanic 3개는 전부
`PeriodicTimer` 라 `SelfBlink` 는 authoring 사용처가 0이었다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — 쿼리에 `DeadTag` 제외 + 착지 정책 교체
- 밀집도 선택 순수 함수 (신규 파일, `Battle/Combat/` 아래 — `BlinkMath.cs` 인접)
- `Assets/_Project/Tests/EditMode/` — 밀집도 선택 테스트 (신규)
- `Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset` — `nightmareMechanics[1]`
- `docs/spec/nightmare-catcher/` — "위협 리더 근처" 계약 문구 갱신(같은 커밋)

## 구현

### 죽는 프레임 가드 (선행)

`HealthThresholdSystem` 의 쿼리에 **`WithNone<DeadTag>` 가 없다.** `DamageApplicationSystem` 이 자기
`OnUpdate` 끝에 playback 하므로 죽는 프레임에 `DeadTag` 는 이미 붙어 있고, 오버킬로 여러 경계를
한 번에 관통하면 **시체가 마지막 경계에서 blink 한다.** `BossPeriodicTriggerSystem` 은 이미 명시적으로
제외하고 있으니 그 표현을 따른다. `SelfBlink` 첫 라이브 사용처이므로 여기서 막는다.

### 착지 정책 — 필드 추가가 아니라 교체

현재 `TryResolveBlinkDest` 는 "위협 리더 근처 + 방어유닛 폴백" 이다. 라이브 authoring 사용처가 0이므로
**정책 셀렉터 필드를 새로 만들지 않고 그냥 교체한다**(제약 8 — "나중을 위한" 추상 레이어 금지).
회귀 리스크는 사실상 0이다. `ThreatEntry` / threat drain 은 별 책임이니 그대로 남긴다.

`HealthThresholdSystem` 의 관련 주석과 `nightmare-catcher` 의 "위협 리더 근처" 계약 문구를
**같은 커밋에서** 갱신한다.

### 밀집도 선택은 순수 함수

제약 10 의 (a)비자명 · (c)sim-critical 에 해당한다. 같은 카테고리가 이미 분리돼 있고
(`PatternTargeting.Select` 헤더 = "순수 수학 + EditMode 고정(제약 10 — sim-critical 타겟팅)")
`TryResolveBlinkDest` 도 정책 체인만 갖고 수학을 전부 위임하는 구조이므로, **인라인이 관례 이탈이다.**

계약 두 개:

- **tie-break 는 row-major 셀 키 rank.** 청크 순서에 의존하면 결정론이 깨진다(`PatternTargeting` 이
  같은 이유로 같은 규칙을 쓴다).
- **밀집 최대 셀도 `BlinkMath.TryFindLandingCell` 을 통과시킨다** — walkable ∧ connected 보장.
  밀집 최대 지점이 벽이면 링 탐색 폴백으로 내려간다.

착지 실패(방어유닛 전멸 / 링 상한 초과)면 skip 하고 경계는 소모한다(기존 동작 유지 — 발동 소모는
남고 재발동은 없다).

### mechanic 데이터

`nightmareMechanics[1]` = `trigger { kind = HealthThreshold, fraction = 0.50 }` ×
`payload { kind = SelfBlink, tileRange = <착지 탐색 반경>, projectile = <출발/착지 퍼프 SO 또는 null> }`.

`projectile` 이 null 이면 무연출 blink 다(기존 계약). 순간이동이 눈에 안 보이면 플레이어가 무슨 일이
일어났는지 모르므로 반드시 넣는다.

**도약 VFX = `Assets/PixPlays/ElementalAOE/EarthAOE/Version_URP/EarthSlamSpikesAoeVFX.prefab`**
(사용자 지정 2026-07-29). URP 버전을 쓴다 — 프로젝트가 URP 17.4 이고 `Version_BuiltIn` 은 머티리얼이
Built-in 파이프라인용이다. 흙 슬램 스파이크라 **착지 임팩트**에 맞는다.

기존 blink arm 은 출발지·착지 **양쪽**에 같은 `projectileDataIndex` 로 히트 이벤트를 쏜다. 슬램 스파이크는
착지에 어울리고 출발지에는 어색할 수 있으니, Play 확인 후 어색하면 출발지 연출 분리를 후속 후보로 올린다
(현 arm 은 단일 인덱스라 분리에 필드 추가가 필요하다 — 이번 범위 아님).

### 동시 발동 순서

**2026-07-29 튜닝으로 두 능력의 경계가 분리됐다** — 진동갑주 `0.20`(760/570/380/190, 4회) vs
도약 `0.50`(475, 1회). 0.5 는 0.2 의 배수가 아니라 **경계가 겹치지 않으므로 같은 프레임 동시 발동이
일어나지 않는다.** 사용자 피드백("도약 말고는 구분이 안 된다")에 대한 가독성 조치이기도 하다 —
폭발과 도약이 별개 사건으로 읽힌다.

경계가 다시 겹치게 값을 바꾸면 순서는 **폭발 → 도약**으로 시스템 순서가 고정한다
(`BlinkApplySystem` 의 `[UpdateAfter(HealthThresholdSystem)]`). 슬롯 순서로는 뒤집을 수 없다.

## 완료 기준

- 컴파일 통과.
- **EditMode 신규** (밀집도 선택 순수 함수):
  - 방어유닛 다수가 한 곳에 모인 배치 → 그 셀이 선택된다.
  - 동점 다수 → row-major 셀 키 rank 로 **결정론적**으로 하나가 선택된다(같은 입력 2회 = 같은 출력).
  - 후보 0(방어유닛 전멸) → 실패 반환.
  - 밀집 최대 셀이 walkable 하지 않음 → 링 폴백으로 인접 walkable 셀이 나온다.
- **PlayMode**: 보스 HP 를 79% 로 직접 세팅 → 다음 프레임에 밀집 셀 근처로 blink 하고,
  **`DeadTag` 가 붙은 프레임에는 blink 하지 않는다**(오버킬 케이스).
- **Play 육안**: 방어유닛을 두 무리로 나눠 배치 → 보스가 경계마다 **더 많이 모인 쪽으로** 뛴다.
  가디언으로 막아도 뛴다(unit 3 의 어그로 면역과 함께 동작).
- 폭발이 **도약 전 자리**에서 터지는 것을 눈으로 확인(계약 5 검증).
- `nightmare-catcher` spec 의 "위협 리더 근처" 문구가 갱신됐다.

---

**확인 2026-07-29 · 커밋 `21b9aaec`**(+ `5b467560` 50%·10% 2회) — 사용자 Play 확인.
"도약 이동은 잘 되는 것 같다" → 밀집 지점 착지가 동작한다. EditMode 신규 6건(밀집 선택 결정론) 통과.

**미검증(남김)**: 방어유닛을 두 무리로 나눠 **더 많이 모인 쪽**으로 뛰는지 대조 확인,
오버킬 프레임에 `DeadTag` 로 blink 가 억제되는지, 슬로모(0.3x) 중 배율 동행.
