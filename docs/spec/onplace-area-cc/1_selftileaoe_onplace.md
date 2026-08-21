# 1 — `SelfTileAoe` 를 배치 트리거에서도 쓴다

## 목적

「자기 중심 N 타일에 피해 M」이라는 어휘는 **이미 있다**(`DcPayloadKind.SelfTileAoe`, 2).
소비 지점이 사망(작별 선물) · 임계(궁극기) · 피격(진동갑주) · 처치(시체폭발)뿐이라
**배치 순간에는 쓸 수 없을 뿐**이다. arm 하나를 열어 그 구멍을 메운다.

신규 payload 0개 — `on-place-skill-rework` 계약 3(기존 어휘 먼저).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/SelfTileAoeStaging.cs` — 캐리어 스테이징 헬퍼
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — 기존 스테이징을 헬퍼 호출로
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — payload arm
- `Assets/_Project/Tests/EditMode/` · `Assets/_Project/Tests/PlayMode/`

## 구현

### 사본을 만들지 않는다

폭발은 `ProjectileSpawnRequest` 캐리어 1개로 표현된다(movement `SkyFall` · payload `TileAoe` ·
`flightTime 0` = 즉발). 지금 **시스템 안에서** 그 캐리어를 만드는 곳은 `HealthThresholdSystem`
하나다(나머지 셋은 브리지 이벤트를 거친다). 여기에 그대로 한 벌 더 쓰면 두 번째 사본이 되고,
`targetFaction` 도출 같은 미묘한 줄이 갈린다.

→ 순수 static 헬퍼로 뽑아 **두 시스템이 같은 한 줄을 부른다**:

```
SelfTileAoeStaging.Stage(ref ecb, in slot, position, owner, hostIsEnemy);
```

`hostIsEnemy` 를 파라미터로 받는 이유는 `boss-jjangssen` unit 2 가 넣은 진영 도출
(적 host → Defender 를 때린다)을 잃지 않기 위해서다. 호출부가 `FactionTag` 를 읽고 넘긴다.

### arm — 계약 5(후보 0이면 캐리어 없음)

```
if (slot.magnitude > 0f && slot.tileRange >= 0 && slot.projectileDataIndex >= 0
    && HasComponent<LocalTransform>(entity))
    반경 안 «상대 진영» 후보 수를 센다  ← BuildEnemyPool + AuraPulse.SelectTargets 재사용
    if (후보 == 0) 스테이징 skip
    else SelfTileAoeStaging.Stage(...)
```

**후보 0 게이트가 이 unit 의 load-bearing 조각이다.** 브리지의 캐리어 드레인은
`if (!_running) return;` 아래에 있어서, 전투 시작 전(배치 페이즈)에 놓으면 요청이 큐에 남아
**전투가 시작되면 뒤늦게 터진다**(캐논 실측 — `on-place-skill-rework` 후속 후보의 「잔류 캐리어」).
배치 페이즈엔 적이 0마리이므로 이 게이트 하나가 그 경로를 닫는다.

⚠ 「시도를 소진한다」는 기존 정책은 **바꾸지 않는다**(적 0마리 배치는 여전히 스킬을 낭비한다).
그건 별건의 후속 후보이며 여기서 손대면 레거시 5분기의 회귀 표면이 생긴다.

### 저작 검증은 이미 있다

`SelfTileAoe` 에 `payload.projectile` 이 없으면 bake 가 이미 loud 거절한다 — 폭발 자체가
`ProjectileSpawnRequest` 라 `dataIndex < 0` 이면 드레인이 요청을 통째로 버려 **피해까지 사라지기**
때문이다(`boss-jjangssen` unit 2). 이 unit 에서 추가할 검증은 없다.

## 완료 기준

- [ ] compile 0 error
- [ ] `grep` — 시스템 내 `ProjectileSpawnRequest{ payload = TileAoe }` 스테이징이 **1곳**(헬퍼)
- [ ] EditMode
  - 헬퍼가 `hostIsEnemy` 에 따라 `targetFaction` 을 뒤집는다(적 host → Defender)
  - 기존 임계 경로(궁극기·진동갑주) 무회귀 — 같은 요청 필드가 나온다
- [ ] PlayMode (arm 단독 검증 — 임시 능력 SO 로)
  - 반경 안 적이 `magnitude` 만큼 체력을 잃는다 · 반경 밖 무영향
  - **전투 시작 전 배치 → 캐리어 0** (계약 5). 전투 시작 후에도 뒤늦은 폭발이 없다
