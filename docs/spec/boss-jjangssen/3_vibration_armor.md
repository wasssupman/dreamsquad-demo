# 3 — 진동갑주 (경계마다 자기중심 폭발)

## 목적

최대체력 20% 경계를 하향 돌파할 때마다 짱쎈놈 자기중심 반경 2 타일에 폭발을 일으킨다.
밀집 배치를 응징하는 광역 담당이고, 사건 구동이라 보스 생존 시간이 짧아도 확실히 발동한다.

`HealthThreshold × SelfTileAoe` arm 은 이미 존재한다(진동갑주 = `dreamcatcher-content-3` unit 4).
**그러나 보스 bake 경로가 미완이라 코드 0줄이 아니다.**

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics` 의 `projectileDataIndex` 분기
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — `SelfTileAoe` 캐리어의 진영
- `Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset` — `nightmareMechanics[0]`
- 덱 asset — `bossPool` 에 짱쎈놈 투입(로테이션 라이브 확인이 여기서 끝난다)

## 구현

### bake — `SelfTileAoe` 의 `projectileDataIndex` 를 채운다

현재 `BakeNightmareMechanics` 에서 `projectileDataIndex` 를 채우는 분기는 `SelfBlink || AllyMoveSpeedAura`
**뿐**이다. `SelfTileAoe` 는 초기값 `-1` 이 남고, 브리지 드레인이 `dataIndex < 0` 이면
**`ProjectileSpawnRequest` 를 통째로 버린다**("dataIndex -1 out of range; dropping").

폭발이 그 요청 하나로 표현되므로 **VFX 만 빠지는 게 아니라 데미지도 나가지 않는다** — 능력이 완전
무효가 되고, 로그는 뜨지만 원인이 드러나지 않는다.

- bake 조건에 `SelfTileAoe` 를 추가한다.
- `payload.projectile` 이 null 이면 **loud skip**. defender 슬롯 경로가 이미 같은 규칙("AOE view 없으면
  skip + 경고")을 쓰고 있으므로 그 표현을 맞춘다. bake 에 loud 거절 선례가 이미 4개 있다.
- 같은 커밋에서 **degenerate 값 loud 거절**을 추가한다: `fraction <= 0` 이면 순수함수가 조용히 발동하지
  않아 오타 하나에 능력이 로그 없이 사라진다.

### 진영 — 캐리어가 방어유닛을 때려야 한다

`SelfTileAoe` 캐리어의 `targetFaction` 기본값이 `Enemy` 라 **보스가 쓰면 자기 진영을 때린다.**
host 진영에서 도출하도록 고친다 — `BuildPatternTemplate` 의 `hostIsEnemy ? Defender : Enemy` 가 선례다.

defender 의 기존 진동갑주/작별선물/시체폭발 경로가 `Enemy` 를 유지해야 하므로, **host 가 적일 때만**
`Defender` 로 바뀌는 형태여야 한다(기본 경로 byte-identical).

### mechanic 데이터

`nightmareMechanics[0]` = `trigger { kind = HealthThreshold, fraction = 0.20 }` ×
`payload { kind = SelfTileAoe, magnitude = <폭발 데미지>, tileRange = 2, projectile = unit 1 의 AOE SO }`.

HP 950 · fraction 0.20 → 760 / 570 / 380 / 190 에서 **4회**. 래치 단조라 회복해도 재발동하지 않는다.
폭발 데미지는 placeholder 로 두고 Play 튜닝한다 — 방어유닛 HP 대역이 90~195 이므로 즉사 여부가
분산 배치 압력의 세기를 결정한다.

## 완료 기준

- 컴파일 통과. 기존 EditMode 전량 통과(defender 진동갑주·작별선물·시체폭발 무회귀).
- **PlayMode**: 보스 HP 를 79% 로 직접 세팅 → 다음 프레임에 자기 위치 중심 반경 2 의 **방어유닛**이
  피해를 입는다(적이 아니라 방어유닛인 것을 확인 — 진영 도출 회귀 가드).
- **Play 육안**: 방어유닛을 뭉쳐 배치 → 보스 HP 가 깎이는 경계마다 폭발이 터지고 **AOE VFX 가 보인다**.
  4회 전부 발동한다.
- `payload.projectile` 을 비운 asset 으로 bake 하면 **경고가 뜨고 skip** 된다(조용히 죽지 않는다).
- `fraction` 을 0 으로 둔 asset 이 bake 에서 loud 거절된다.
- 덱 `bossPool` 에 나이트메어 + 짱쎈놈 둘을 넣고 Play → **웨이브마다 하나가 결정론적으로** 등장하고
  "꿈결 위기!!" 배너가 둘 다 정상.
