# unit 0 — 적 SO 신설 + 편입 + 라이브 계측

## 목적

`Enemy_Heartseeker`(마음사냥꾼)를 저작하고 판에 올린다. **코드 변경 0 이 이 unit 의 검증선**이다 — 원하는 동작은 `battle-structures` units 1·2 가 이미 구현했으므로, 코드를 고쳐야 한다면 그 전제가 틀린 것이고 정지해서 «왜 저작으로 안 되나»를 먼저 답한다(계약 1).

계측을 별도 unit 으로 떼지 않는다. 「저작만 하고 안 확인한」 커밋을 만들지 않기 위해서다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Heartseeker.asset` — 신규 (+`.meta`)
- `Assets/_Project/Data/EnemyCatalog.asset` — `units` 배열에 추가
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — 라이브 덱의 `attackUnitPool` 에 추가
- **코드 파일 0개**

## 구현

### 저작값

| 필드 | 값 | 이유 |
|---|---|---|
| `id` / `displayName` | `heartseeker` / 마음사냥꾼 | |
| `targetFactions` | `DefenderCore \| BlockingHazard` (= 8 \| 4 = **12**) | 마음 + 방벽. 방벽을 빼면 완전 봉쇄에서 영구 교착(README §마스크). 방벽은 `Factions.AnyUnit` 에 없어 **도발 면역은 유지**된다 |
| `targetMode` | `Nearest`(1) | 골이 여럿일 때 가까운 것. `FocusUntilDead` 와 해제 사유가 같아졌으므로(enum 주석) 차이는 선정 규칙뿐이고, 「집요함」은 이 적의 저작 의도가 아니다 |
| `engageMovement` | `Halt`(0) | 골 사거리에 닿으면 **멈춰서 때린다.** `Advance` 로 두면 골 셀에 진입해 `PastGoalTag`(누수)가 붙어 **때리기 전에 사라진다** |
| `attackMethod` | `Melee`(1) · `attackRange` 1 | 결정 1 — 원거리면 「죽여야만 막힌다」가 「사거리 밖에서 자유롭게 때린다」로 바뀐다 |
| `targetClassMask` | -1 | 거점은 `DefenderClassTag` 가 없어 이 마스크를 우회한다. 무해하지만 값을 좁힐 이유도 없다 |
| `health` | 높게 | 막을 수 없으니 **HP 로 시간을 산다**. 정확한 값은 unit 1 |
| `minWaveNumber` | **계측 중엔 1, 커밋 전 중반값으로** | 결정 2. 계측 편의로 낮춰 두고 완료 기준 통과 후 올린다 — 낮춘 채로 커밋하지 않는다 |

**시각은 최소 구분만 한다** — Spine 은 기존 스켈레톤 재사용, 슬롯 컬러만 다르게. **정식 구분은 unit 1** 이다(동작이 확인된 뒤에 아트를 붙인다).

### 시트는 건드리지 않는다

`UnitStatApplier` 는 **시트 행을 돌며 매칭되는 SO 만 쓴다** — 행이 없는 id 는 지워지지도 덮이지도 않는다(코드 대조 완료). 따라서 미등재 상태에서 SO 가 정본이고, 로그인 임포트가 이 적의 튜닝을 되돌리지 않는다. 밸런싱이 반복되면 그때 등재한다(D2).

## 완료 기준

**계측 4축 — 장면 목격이 아니라 반례를 «센다»**(계약 6):

- [x] **도발 부착 0회** — 마음사냥꾼 관측 38,457 프레임에서 `Aggroed` 부착 **0**
- [x] **방어유닛 피격 0회** — 마음사냥꾼의 `AttackState.committedTarget` 이 `DefenderUnitTag` 를 가리킨 프레임 **0**
- [x] **방벽 타격 발생** — 경로에 세운 방벽 HP **500 → 175**, 마음사냥꾼 `ai=Engaging`(계약 4의 라이브 증거)
- [x] **골 도달 및 피해 발생** — 골 타워를 겨눈 프레임 **5,605**, 타워 HP 1000→190→파괴, 골 셀 **앞에서 정지**(`ai=Engaging`, `Halt` 검증)

**⚠ 음성 대조군이 이 계측의 핵심이다.** 같은 판의 **일반 적**은 도발 **9,001** · 방어유닛 겨눔 **24,453** 을 기록했다. 이 대비가 없으면 «마음사냥꾼 0» 은 «도발이 아예 안 일어난 판» 과 구분되지 않는다 — 실제로 대조군을 세우기 전 3회차까지가 그 상태였고(배치한 가디언이 전부 `PendingDeployment` 에 갇혀 아무도 교전하지 않았다), 그때의 «0» 은 증거가 아니었다.

**계측 하네스 메모**(다음 세션이 반복하지 않도록): `TryBeginDefenderDeployment` 는 **시작만** 한다 — `ActivateDeployedDefender(cell, entity)` 를 이어 부르지 않으면 유닛이 `PendingDeployment` 로 남아 전투에 참여하지 않는다. 그리고 **골 타워 엔티티는 `StartBattle` 이후에 생긴다**(Placement 단계에서 조회하면 0).

**그 외**:

- [x] **코드 변경 0** — `git status` 에 `.cs` 파일 0개. 저작·에셋만 바뀌었다
- [x] 콘솔 신규 에러 0
- [x] EditMode **2111개 / 실패 0 / 의도적 스킵 3** (기준선 대비 실패 증가 0)
- [x] 골 락 금지 가드 — 마음사냥꾼이 골을 락한 채 굳는 사례 0(`past=11` 은 골 **붕괴 후** 통과분)
- [x] `minWaveNumber` 를 계측값(1)에서 **8**(중반)로 되돌린 뒤 커밋

**미확인 (사용자 Play 확인 대기)**: 실제 플레이에서 「막을 수 없는 적」의 체감. 특히 **동시 등장 수** — unit 1 밸런싱의 실질이다.
