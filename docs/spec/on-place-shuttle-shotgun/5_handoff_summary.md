# 5 — 인계 요약 (units 0~4)

## Commit

| 커밋 | 범위 |
|---|---|
| `4aafe374` | unit 0 — 실드셔틀 배치 보호막(에셋 2 + `UnitKitSummary` 절) + spec 문서 |
| `368c72b1` | unit 1 — `OnPlaceFireAim` + 발사 arm 조준 스냅샷 + EditMode 8케이스 |
| `7aa1288b` | units 2~4 — 저작 가드 · 샷건맨 에셋 4 · 문안 |
| ⚠ `52aba94d` | unit 1 의 **브리지 조각**(`ResolveForwardBurstDirection` → 순수 함수). 워크트리를 공유한 다른 세션 커밋에 딸려 들어갔다 — 코드는 정상, 위치만 다르다 |
| `71446582` | 투트랙 코드 리뷰 반영(조준이 «닿는 거리» 를 보게 · 후보 풀 진영 구분 · 퇴화 조준 · 카드 경로 가드) |
| `09ca7a2b` · `13e19e9e` · `7851ac18` · `c280ea62` · `a6e42d3a` | 연출·밸런스 반복(머즐 축소 → 초록 연기 탄 + 이동방향 꼬리 → 5발/저속 → 사거리 4 → 꼬리 수명) |
| `85fe2c13` | 캐논 배치 폭격 상향(반경 3 · 적당 200) + 샷건 피해 상향 |
| `765970fa` | SO 를 시트 값에 정렬(실드셔틀 40 · 웨이포인트 2.0) + 시트에 desc 3건 push |

## Implemented

- 실드셔틀 배치 = 반경 2 **아군 전원**에게 실드 250(자신 제외). 시뮬 코드 0줄 — 페이로드 arm 이
  이미 방어유닛 아군 풀·만충 제외·대상별 부여 VFX 를 갖고 있었다.
- 규칙 경로가 **방향 바인딩 탄**을 쏠 수 있다. 조준 = 「`DeployedFacing` 있으면 그 방향, 없으면
  사거리 안 최근접 합법 후보」. 조준도 후보도 없으면 발사하지 않는다.
- 샷건맨 배치 = **가까운 적 쪽으로** 큰 덩어리 **5발**(±40°) 관통 + 2타일 넉백. 무방향 밀쳐냄 제거.
  탄 뷰는 WALLCOEUR **초록 연기** 복사본(꼬리가 이동 방향으로 늘어짐) + 발사 지점에 남는 머즐.
  사거리 4타일 · 탄속 6(비행 0.67초) — 사거리·탄속·꼬리 수명이 한 묶음으로 움직인다.
- 저작 가드 3건(방향 패턴 × `tileRange 0` skip / `damage 0` 경고 / `randomize` 경고),
  `onPlacePush*` 충돌 경고, `StartBattle` 의 실드 VFX 큐 정리.

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/OnPlaceFireAim.cs` (신규 순수 함수)
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — `EmitProjectilePattern` arm
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryBuildPatternSlot` 가드 · `StartBattle` ·
  `ResolveForwardBurstDirection` · 충돌 경고
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 배치 문안 절 2개
- 에셋: `Ability_AreaShield_ShieldShuttle` · `Ability_OnPlaceBlast_Shotgunner` ·
  `Projectile_ShotgunBlast` · `Pattern_Shotgunner_Blast` · `vfx_Projectile_ShotgunBlast_Green`

## Verified

- EditMode **2517개 중 실패 0**(스킵 5는 기존 문서화된 것). 신규 `OnPlaceFireAimTests` 8케이스 포함.
- **투트랙 코드 리뷰**(ECS + 일반) 양측 REQUEST CHANGES → 전건 반영 후 재검(`71446582`).
- **사용자 Play 확인 2026-08-19.**
- **시트 push 완료** — Defenders `desc` 3칸만 변경(9탭 added 0), 재조회로 반영·보존값 확인.
- 임포트 후 실측: 실드셔틀 abilities 2개·`requiresFacing False`·`OnPlace × GrantShield 250/r2/d0`,
  샷건맨 패턴 3발 `directionT 0/0.5/1`·damage 25·`tileRange 3`, 탄 프리팹 root **Local** + Muzzle **World**.
- 유닛 요약 실측: 셔틀 「배치 시 주변 2타일 아군에게 보호막」 / 샷건맨 「가까운 적 쪽으로 충격파 ·
  밀어냄」 / 캐논은 「미사일 낙하」 그대로.
- **Play 육안 검증은 아직 안 했다.**

## Notes (되돌리면 터진다)

- **이미 조준된 템플릿은 건드리지 않는다.** 발사 arm 은 방향을 미리 실어 보내는 소비자와 공유되고
  그쪽은 후보 0에도 발사한다. 초판이 무조건 덮어써서 `DirectionPattern_FiresWithoutTargets…` 를
  깼다. 「방향이 비어 있다」가 「아직 조준 안 됨」의 표식이다.
- **브리지 레거시 폴백 `(0,1)`** — 전방 관통 4종의 무회귀 조건. 순수 함수의 `false` 를 그대로
  취소로 번역하면 규칙이 바뀐다.
- 탄 프리팹은 **꼬리 World · 머즐 World**(꼬리는 지나온 자리에 남아야 꼬리다). `scalingMode` 는
  Hierarchy 여야 `visualScale` 이 먹고, **중력 0** 이어야 연기가 하늘로 안 뜬다(벤더 원본은 음수).
  방출은 **거리 기준** — 시간 기준이면 탄속을 바꿀 때마다 꼬리 밀도가 달라진다.
- **연출이 판정보다 커지면 안 된다.** 머즐을 360° 로 크게 뿌렸더니 앞쪽 부채꼴만 맞는 스킬이
  «주변 전체 타격» 으로 읽혔다(`09ca7a2b`).
- 발사 취소 시 `break`/`continue` 금지 — 루프 끝의 `slots[si]` 되쓰기를 건너뛰어 뒤 슬롯을 잃는다.

## Follow-up

- **탄이 소멸할 때 꼬리가 팝아웃한다** — 뷰 풀이 즉시 `SetActive(false)` 하는데 꼬리는 월드 공간이라
  공중의 연기가 페이드 없이 증발한다. 고칠 자리는 프리팹이 아니라 **뷰 풀의 반환 시점**
  (「방출만 멈추고 남은 파티클이 죽은 뒤 반환」)이고, 월드 공간 트레일을 쓰는 **모든** 투사체가
  같은 증상을 겪는다. 이 spec 범위 밖.
- **밀림의 두 축**(리뷰 M1) — 조준이 없어 이미 지나간 적을 **골 쪽으로** 밀 수 있고, 재배치가
  배치 스킬을 재무장하므로 코스트를 내면 반복할 수 있다. Play 에서 빈도를 보고 판단한다.
- **재배치 hop 실드**(리뷰 M2) — 같은 대상엔 펌프가 안 되지만 새 대상은 계속 덮인다.
- 부모 spec 계약 2 의 **레거시 전량 이관**은 여전히 선행 조건이다. 이번에 `onPlacePush*` 소비자만
  0 이 됐다(무비용으로 뗄 수 있는 첫 조각).
- **에디터 공유 사고 2건**(이번에 겪음 — `docs/reference/lessons/01-unity-mcp-operation.md` 에 기록):
  실행 중 `Resources.UnloadAsset` 이 카탈로그 참조를 끊어 스쿼드가 빈 칸이 됐고(복구=도메인 리로드),
  전역 `SaveAssets` 가 남의 시트 값을 디스크로 흘렸다. 이 워크트리에선 **조회만** 하고 저장은
  `SaveAssetIfDirty` 로 대상만.
