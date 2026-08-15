# 5 — 퇴직 위로금 카드 (레인 C)

## 목적

이 유닛이 **퇴근**하면 비워진 그 자리에 운석이 떨어진다.
`defender-clock-out` 이 만든 퇴근 경로에 **드림캐쳐 사건 지점**을 처음 낸다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — **`RetireDefender` 함수 내부만**
- `Assets/_Project/Data/Dreamcatcher/Card_SeveranceMeteor.asset` **(신규)**
- `Assets/_Project/Tests/PlayMode/DefenderRetireTest.cs` (케이스 추가)

> enum(`DcTriggerKind.OnRetire`) · 적용성 · bake(`OnRetire × SelfTileAoe` 만 허용, `duration` =
> 낙하 예고) · 문안은 **unit 0 이 이미 놓았다.** 이 unit 은 **퇴근 순간의 실행**만 한다.
> ⚠ `BattleBridge.cs` 의 다른 영역을 만지지 않는다(README 계약 P2 — 같은 파일을 다른 레인이
> 읽고 있을 수 있다).

## 구현

### 1) 파괴 직전 슬롯 직독

`RetireDefender` 는 `_em.DestroyEntity(binding.entity)` 로 엔티티를 **직접 파괴**한다.
그래서 이 경로는 사망과 달리 **payload 를 미리 구워 나를 필요가 없다** — 파괴 직전에
`DcTriggerSlot` 버퍼를 그냥 읽으면 된다(`defender-clock-out` 후속 후보가 지목한 이 경로의 성질).

순서를 지킨다:
1. `ReleaseDefenderTile(cell, out binding)` (기존)
2. **`binding.entity` 의 `DcTriggerSlot` 을 훑어 `trigger == OnRetire && payload == SelfTileAoe`
   슬롯의 값(피해·반경·예고·dataIndex·visualScale)을 로컬로 스냅샷** ← 신규
3. `_em.DestroyEntity(binding.entity)` (기존)
4. 스냅샷대로 운석 cast ← 신규
5. 뷰 반납 / `DefenderRetired` 이벤트 (기존)

⚠ 되돌릴 수 없는 sim 변경(3)을 뷰 코드보다 먼저 끝내는 기존 주석의 의도를 깨지 않는다.
스냅샷(2)은 `EntityManager` **읽기**뿐이고, cast(4)는 파괴 뒤라 소멸한 엔티티를 참조하지 않는다.

### 2) 운석 cast

`SelfTileAoe` 의 기존 실행 형태 그대로 — `SkyFall × TileAoe` 투사체 하나
(`DrainShieldBreakEvents` / 작별 선물과 같은 경로). 셀 중심을 `origin` = `impact` 로 두고
`flightTime = 예고 초`(`slot.duration`), `arcHeight = 탄 SO 의 dropHeight`,
`targetFaction = Enemy`, `owner = Entity.Null`(퇴근한 유닛은 이미 없다).

### 2-1) 다른 운석과 값을 공유하지 않는다

이 게임의 운석은 **출처가 셋이고 파라미터가 전부 독립**이다. 이 카드는 세 번째다:

| 운석 | 값 출처 | 실행 지점 |
|---|---|---|
| 액티브 카드 운석 | `Active_Meteor.asset` → `SkillData`(magnitude / range / warningSec) | `ApplyMeteor` |
| 시즌 기믹(사직서 임계) 폭격 | `ClockOutGimmickData`(meteorDamage / TileRange / WarningSec / Stagger) | `DrainMeteorBarrageRequests` |
| **퇴근 운석(이 카드)** | **카드 에셋 `mechanics[0].payload`**(magnitude / tileRange / duration) | `RetireDefender` |

공유하는 것은 겉모습(`Projectile_Meteor.asset`)뿐이다. 그것도 나중에 퇴근 전용 look 이 필요하면
탄 SO 하나 복제로 갈라진다 — **피해·반경·예고는 처음부터 카드가 소유한다.**
⚠ 위 두 운석의 값을 참조하거나 상수로 복제하지 말 것(제약 6).

**전 매칭 슬롯이 발동한다** — 카드를 2장 붙였으면 운석 2발.
(`OnDeath` 의 "첫 매칭 슬롯만(v1)"과 다르다. 그 제약은 event-stamp 구조 때문이었고 여기는
버퍼를 직독하므로 해당 없다. `HealthThreshold` 가 슬롯당 발동인 것과 같은 자리.)

### 3) 카드 에셋 — `Card_SeveranceMeteor.asset`

`id=severance_meteor` · `displayName="퇴직 위로금"` · `type=Unit` · `art=null` ·
`mechanics[0]`: trigger `OnRetire` × payload `SelfTileAoe`
(`magnitude=120` · `tileRange=1` · `duration=0.8`(낙하 예고) · `projectile=Projectile_Meteor`).
**초기값이며 튜닝 대상.** `description` 은 formatter 정확 미러.

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0.
- **PlayMode 교차 무발동 2건**(`DefenderRetireTest` 확장) — 이 카드의 load-bearing 계약:
  ① 카드를 붙인 유닛을 **퇴근**시키면 그 셀에 운석이 떨어진다(인접 더미가 피해를 받는다)
  ② 같은 유닛을 **죽이면** 운석이 안 떨어진다
  ③ (대조군) `OnDeath` 카드(작별 선물)를 붙인 유닛을 **퇴근**시키면 폭발이 없다 —
     기존 `DefenderRetireTest` 의 "퇴근은 사망의 결과를 하나도 일으키지 않는다" 단정과 한 몸
- **Play 육안**: 퇴근 연출(줄에 걸려 뱅글뱅글 이탈, ~1.6초)과 운석 예고가 화면에서 겹칠 때
  읽히는가. 겹쳐서 지저분하면 예고 초를 늘리는 **저작 조정**으로 푼다(코드 아님).
- 컴파일까지만 확인하고 **커밋하지 않는다**(README 계약 P3).
