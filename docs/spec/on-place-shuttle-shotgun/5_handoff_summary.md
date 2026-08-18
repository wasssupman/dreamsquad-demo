# 5 — 인계 요약 (units 0~4)

## Commit

| 커밋 | 범위 |
|---|---|
| `4aafe374` | unit 0 — 실드셔틀 배치 보호막(에셋 2 + `UnitKitSummary` 절) + spec 문서 |
| `368c72b1` | unit 1 — `OnPlaceFireAim` + 발사 arm 조준 스냅샷 + EditMode 8케이스 |
| `7aa1288b` | units 2~4 — 저작 가드 · 샷건맨 에셋 4 · 문안 |
| ⚠ `52aba94d` | unit 1 의 **브리지 조각**(`ResolveForwardBurstDirection` → 순수 함수). 워크트리를 공유한 다른 세션 커밋에 딸려 들어갔다 — 코드는 정상, 위치만 다르다 |

## Implemented

- 실드셔틀 배치 = 반경 2 **아군 전원**에게 실드 250(자신 제외). 시뮬 코드 0줄 — 페이로드 arm 이
  이미 방어유닛 아군 풀·만충 제외·대상별 부여 VFX 를 갖고 있었다.
- 규칙 경로가 **방향 바인딩 탄**을 쏠 수 있다. 조준 = 「`DeployedFacing` 있으면 그 방향, 없으면
  사거리 안 최근접 합법 후보」. 조준도 후보도 없으면 발사하지 않는다.
- 샷건맨 배치 = 겨눈 쪽이 아니라 **가까운 적 쪽으로** 큰 덩어리 3발(±40°) 관통 + 2타일 넉백.
  무방향 밀쳐냄 제거. 탄 뷰는 WALLCOEUR 초록 화염 복사본 + 머즐 자식.
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
- 탄 프리팹은 **루트 Local · 머즐 World**. 벤더 원본은 루트도 World 라 그대로 쓰면 불꽃이 궤적에
  흩뿌려진다. `scalingMode` 는 Hierarchy 여야 `visualScale` 이 먹는다.
- 발사 취소 시 `break`/`continue` 금지 — 루프 끝의 `slots[si]` 되쓰기를 건너뛰어 뒤 슬롯을 잃는다.

## Follow-up

- **시트 `desc` 반영** — 지금 SO 만 고친 상태라 로비 진입 임포트가 되돌린다. unit 4 의 문안 검증은
  그 전엔 성립하지 않는다. 공유 데이터라 **푸시는 사용자 승인 후**.
- **Play 육안 검증 전량** — 각 unit 문서의 완료 기준. 특히 ① 실드가 캐스트보다 두꺼워 보이는가
  ② 충격파가 평타 산탄과 한눈에 구분되는가 ③ 머즐이 발사 지점에 남는가 ④ 같은 초록 불꽃인
  실드 부여 연출과 헷갈리지 않는가.
- **넉백 2.0 의 두 축**(리뷰 M1) — 조준이 없어 이미 지나간 적을 **골 쪽으로** 밀 수 있고,
  카드 부메랑(1.5)보다 세다. 재배치가 배치 스킬을 재무장하므로 코스트 3 에 반복 가능하다.
  Play 에서 빈도를 재고 필요하면 1.5 로 낮추거나 조준을 켠다.
- **재배치 hop 실드**(리뷰 M2) — 같은 대상엔 펌프가 안 되지만 새 대상은 계속 덮인다.
- 부모 spec 계약 2 의 **레거시 전량 이관**은 여전히 선행 조건이다. 이번에 `onPlacePush*` 소비자만
  0 이 됐다(무비용으로 뗄 수 있는 첫 조각).
