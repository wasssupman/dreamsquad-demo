# 1 — 감지 반경 저작과 베이크

## 목적

「이 적은 몇 칸 안의 방어유닛을 발견하는가」를 **하나의 저작 축**으로 만든다. 오늘 같은 일을
하는 축이 둘이다 — `huntsDefenders`(무제한 사냥)와 «없음»(사거리에 들어온 것만). 그 사이의
유한 반경이 이 spec 의 값이므로, 셋을 한 필드로 접는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `huntsDefenders` 은퇴 → `detectionRange` 신설
- `Assets/_Project/Scripts/Battle/Combat/DetectionRange.cs` (신규) — 런타임 컴포넌트
- `Assets/_Project/Scripts/Battle/Combat/DefenderHunterTag.cs` — 부착 조건 주석 갱신
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateEnemyEntity` 베이크
- `Assets/_Project/Data/Enemies/*.asset` — 4건 마이그레이션(아래)
- **`Assets/_Project/Scripts/Data/BonusWaveData.cs:73`** — `OnValidate` 가 `huntsDefenders` 를 읽는다.
  필드를 지우면 **런타임 asmdef 컴파일 에러**다. 경고를 `detectionRange >= 0` 기준으로 이관한다.
- **`Assets/_Project/Tests/EditMode/EnemyTierBakeTests.cs`** — `:125` 가 `huntsDefenders` 를 세팅한다
  (**테스트 asmdef 컴파일 에러**). 더 중요한 것은 `:150~158`
  `메커닉_없는_보스도_사냥_태그를_받는다` 가 **티어 폴백을 단언**한다는 점이다 — 이 unit 이 그
  폴백을 의도적으로 없애므로 **그 테스트는 반드시 빨개진다.**
- `Assets/_Project/Scripts/Battle/Combat/DefenderHunterTag.cs:18` ·
  `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs:492` — 부착 조건을 서술한 주석 2곳

## 구현

**저작** — `AttackUnitData` 에 `public float detectionRange = 0f;` (Appended last, 직렬화 back-compat).

| 값 | 뜻 |
|---|---|
| `0` | 감지 안 함. **기본값이자 24종 중 20종의 값** — 사거리에 들어온 것만 친다(오늘 그대로) |
| `> 0` | 반경(칸). 이 안에 때릴 수 있는 방어유닛이 있으면 경로를 벗어나 향한다 |
| `< 0` | 무제한. 맵 어디든 방어유닛이 있으면 사냥한다(구 `huntsDefenders`) |

`[Min]` 을 붙이지 않는다 — 음수가 sentinel 이다.

⚠ **선례는 `classMask = -1` 이 아니다**(리뷰 지적). 그건 all-ones 비트마스크라 「모든 비트가 켜짐」
이라는 자연스러운 값이지 크기 축의 sentinel 이 아니다. 이 코드베이스의 진짜 선례는 **크기·인덱스
축의 `-1 = 미저작·특별 모드`** 다 — 같은 SO 안의 `waypointPathIndex: -1`(경로 미저작,
`BattleBridge.cs:10679`) 과 `DevMapOverride.Index = -1`(맵 강제 없음).

⚠ **`!= 0` 이 아니라 임계 비교**(`MinDetectionRange = 0.05`)로 판정한다. float 이라 `0.001` 저작이
`!= 0` 을 통과해 태그만 붙이고 반경은 무의미해진다 — 「태그는 있는데 아무것도 감지 못 하는」 적이
`DefenderFieldSystem` 의 재빌드 게이트까지 켠다.

**`huntsDefenders` 은퇴.** 제거 전에 그 값을 쓰던 에셋을 옮긴다. 실측 대상은 **4건**뿐이다:

- `Enemy_Boss_Jjangssen` · `Enemy_Boss_Mamemo` · `Enemy_Boss_Nightmare` (`tier: 2` = Boss) → `detectionRange = -1`
- `Enemy_DreamShard` (`huntsDefenders: 1`) → `detectionRange = -1`

⚠ **보스는 오늘 `tier == Boss` 로 사냥을 «공짜로» 받는다**(부착 조건이 `Boss || huntsDefenders`).
흡수 후 베이크는 **`detectionRange` 만** 본다 — 티어는 더 이상 사냥을 주지 않는다. 저작을 빠뜨리면
보스가 조용히 사냥을 잃으므로, 완료 기준의 테스트가 이 4건을 명시로 잡는다. 티어에 폴백을 남기는
쪽이 안전해 보이지만 그러면 「저작을 안 하면 티어가 대신 말한다」는 숨은 규칙이 생겨 축이 다시
둘이 된다 — 그건 이 unit 이 없애려는 것이다.

⚠ **시트에는 이 컬럼이 없다**(`huntsDefenders` 도 없었다). 임포터가 스킵하므로 SO 저작으로 끝나고,
로그인 임포트가 되돌리지 않는다. `bodyRadius`/`bodySize` 와 같은 처지다.

**런타임 컴포넌트** — Combat 소유:

```csharp
public struct DetectionRange : IComponentData { public float tiles; }   // < 0 = 무제한
```

**베이크** — `CreateEnemyEntity` 에서 `unitType.UsesDetection` 인 적에게만 부착한다. 0 이면 컴포넌트가
없고, 그 부재가 곧 「오늘과 같은 경로」다(분기 하나 대신 아키타입으로 가른다).

**`DefenderHunterTag` 는 남긴다.** 이 태그는 세 곳을 게이트한다(`MovementSystem` 사냥 이동 ·
`DefenderFieldSystem` 의 재빌드 skip 과 소스 반경 R 산출). 부착은 `BattleBridge.cs:10563` 한 줄이며,
조건만 `tier == Boss || huntsDefenders` 에서 **`UsesDetection`** 으로 바꾼다 — 태그의 뜻이 「방어유닛을 사냥한다」에서 「감지를 쓴다」로
넓어질 뿐이고, `DefenderFieldSystem` 은 **코드 변경 0** 이다. 주석의 부착 조건 문장을 갱신한다.

**기존 테스트 3건을 다시 쓴다 — 지우지 않는다.** `EnemyTierBakeTests` 의 사냥 태그 3케이스가
지키는 것은 태그의 **존재**가 아니라 **어디서 붙느냐**다(그 파일 `:101~110` 주석: 태그를
`BakeNightmareMechanics` 안에 두면 메커닉 없는 사냥꾼에게 안 붙는데 보스는 무회귀라 조용히 통과한다.
`DefenderHunterGateTests` 는 시스템 게이트만 보므로 **여기가 유일한 bake 그물이다**). 그 목적을
유지한 채 기준만 `detectionRange` 로 바꾼다:

| 기존 | 이후 |
|---|---|
| `메커닉_없는_사냥꾼도_태그를_받는다`(`hunts: true`) | `detectionRange = -1` + `nightmareMechanics = null` |
| `메커닉_없는_보스도_태그를_받는다`(tier 폴백 단언) | **`Boss` + `detectionRange = 0` 이면 태그가 «없다»** 로 뒤집는다 — 티어가 더 이상 사냥을 주지 않는다는 것이 이 unit 의 계약이므로, 그 계약을 지키는 테스트로 바꾼다 |
| `사냥꾼도_보스도_아니면_태그가_없다` | `detectionRange = 0` 이면 태그가 없다(그대로) |

## 완료 기준

- compile 통과(**런타임·테스트 asmdef 둘 다** — `BonusWaveData` 누락이 가장 흔한 실패다).
- EditMode 전체 초록(선행 실패 2건 `bomb_man`·`boomerang` 문안 단언 제외). ⚠ 위 3케이스를
  **다시 쓰기 전에는 초록이 될 수 없다** — 「티어 폴백 단언」이 이 unit 의 계약과 정면 충돌한다.
- EditMode 신규: 보스 3종 + `Enemy_DreamShard` 의 `detectionRange < 0`, 나머지 20종이 `== 0`.
  이 테스트가 마이그레이션 누락을 잡는 유일한 장치다.
- EditMode 신규: `detectionRange == 0` 인 적을 구우면 `DetectionRange` 가 **없고**
  `DefenderHunterTag` 도 없다(오늘과 같은 아키타입).
- **거동 무변** — 태그 부착 집합이 4건 그대로다. ⚠ **`Verify` 전건 통과로 확인할 수 없다**:

  이 unit 은 `AttackUnitData` 의 **직렬화 스키마**를 바꾸므로 `configHash` 가 8건 전부 움직인다
  (`CollectMatchConfig` 의 `[enemies]` 섹션이 적 SO 를 필드 단위로 접는다). 그리고 코퍼스는
  **이 unit 이전부터 이미 stale 이었다** — 마지막 코퍼스 동작이 「재생성」이라 verify 초록이
  기록된 적이 없고, 그 뒤 다른 세션의 sim 커밋이 6건의 거동을 바꿔 놨다.

  **확인 방법(실제로 쓴 것)**: `DetectionRange` **부착 한 줄만** 임시로 끄고 verify 를 돌려
  켠 실행과 대조한다. 2026-09-05 실측 — 이벤트/킬이 8건 전부 **완전히 동일**
  (`basic` 80·`long_boss` 1970/16킬·`seed_b` 238·`no_defense` 258·`summoner` 295·`restart` 327·
  `force_wave` 563). 아키타입이 하나 늘어도 sim 이 안 갈린다는 증거다.

  ⚠ **여기서 재베이크하지 않는다.** 재베이크하면 남의 세션이 만든 6건의 거동 변화가
  이 spec 의 커밋에 기준선으로 구워진다. 코퍼스 정리는 별도 작업이다.
