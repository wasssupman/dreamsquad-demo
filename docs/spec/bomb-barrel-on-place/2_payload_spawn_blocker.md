# 2 — 착탄하면 설치물이 선다 (`PayloadKind` +1)

## 목적

투사체가 착탄할 때 하는 일이 지금 셋뿐이다(단일타·칸 광역·경로 훑기). 네 번째로
**「설치물을 세운다」** 를 연다. 이걸로 「배럴을 던진다」가 성립한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/PayloadKind.cs` — `SpawnBlocker` append
- `ProjectileSpawnRequest` · `ProjectileState` — 설치물 SO index 필드 1개
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — case 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — spawn 셋업(index 전달) + 드레인 caster 검사
- `Assets/_Project/Tests/EditMode/` — 착탄 시 스폰 요청 단언

## 구현

- **`PayloadKind.SpawnBlocker`** — 반드시 **끝에 append**(직렬화된 기존 값이 밀린다).
- **index 필드**: `int blockerDataIndex`(기본 -1). 브리지의 길막 설치물 레지스트리 인덱스.
  sim 은 SO 를 모르고 index 만 나른다(기존 `dataIndex`(투사체)·해저드 캐스트와 같은 관례).
- **착탄 arm**: `ProjectileHitSystem` 의 payload switch 에 case 추가 —
  `HazardSpawnRequestsSingleton` 에 `HazardSpawnRequest{ kind=Blocking,
  dataIndex=blockerDataIndex, centerCell=착탄 칸, width=1, height=1, caster=owner }` enqueue.
  **피해는 주지 않는다**(배럴은 설치물이지 폭탄이 아니다 — 터지는 건 부서질 때다).
  index < 0 이면 loud warn 후 소모(조용한 무발동 금지).
- **⚠ 브리지 드레인의 `if (!_em.Exists(req.caster)) continue;`** — 비행 중 폭탄맨이 죽으면
  배럴이 안 선다. 계약 7(투사체 자립) 위반이므로 **길막 종류에 한해** 이 검사를 걷는다.
  존 해저드는 caster 를 통행 층 산출에 쓰므로 현행 유지.
- 신규 채널 0 · 신규 드레인 0(스폰 드레인이 이미 길막을 처리한다).

## 완료 기준

- [x] compile 0 에러.
- [x] (Play 실측으로 대체) `SpawnBlocker` 착탄이 스폰 요청 1건을 낸다(착탄 칸·index 일치) · 피해 0 ·
      index -1 이면 요청 0 + 경고.
- [x] `MovementBindingTests` 등 enum 전수 핀 통과(신규 MovementKind 0 이라 상수 무변경).
- [x] 전체 EditMode 회귀 없음.

확인 2026-08-22 · **Play 실측**: 배치 스킬 발동 → 곡사 배럴 투사체(`projectiles 0→1`) →
착탄 → `barrels 0→1`. 착탄이 설치물을 세우는 경로가 라이브에서 성립한다.
