# 5 — Handoff Summary

> units 0~6 구현 종료 인계. 최신 계약은 README/번호 문서, 구현 상세는 코드/커밋 우선.

## Commit

- `cbd88b67` README rev2 (critic 2종 반영 — 초안의 계약 1 이 뒤집혔다)
- `e391f42e` · `26995950` unit 0 — 적용성 판정 계층 + 지원 행렬
- `5cbf497a` · `81817768` · `12958858` unit 1 — 판정 수렴 + 무효 조합 거절 (+ Play 가 잡은 `FacingVolley` 판별 수정)
- `65444522` · `f3cffadb` · `2f340095` unit 2 — 타게팅 폴백 규칙 + 반경 데이터
- `f0e4ac53` · `8f0f21dc` · `0af01cf6` unit 3 — 폭탄맨 사건 지점 (첫 잠금 해제)
- `2bfc9084` · `ed930e2b` · `44ae506d` unit 4 — 캐스트 사건 채널 (전 아키타입 개통)
- `398850dc` 리팩토링 — 중복 3벌 제거 + 게이트 구멍/PastGoal 누락 수정
- `a3aa32bc` `DcNeedleTargeting` → `NearestTargeting` (도메인 중립 유틸)
- `2978d960` 캐스트 드레인 EditMode 2건
- `7c19b2bd` unit 6 — 방향탄 bounce 개통(통통구슬×머신거너)
- unit 6 후속 — 대상 사망 재조준 + 리팩토링 + EditMode 6건 (이 커밋)

## Implemented

- **"부착 가능 ⇒ 반드시 발동"이 계약이 됐다.** host 종속 판정이 `DcApplicability` 한 곳으로 모였고 UI preflight 와 커밋 bake 가 같은 함수를 쓴다 — 예전엔 두 미러를 손으로 맞췄다.
- 전 아키타입이 자기 **공격 성립 지점 1곳**을 갖는다: Standard/FacingVolley = RESOLVE · BombThrow = 폭탄 발사 성사 · HazardCast = 캐스트 성사(Effects→Combat 22번째 채널).
- 비수가 폭탄맨·해저드 캐스터에서 발동한다. host 가 대상을 안 주면 `payload.tileRange` 반경으로 스스로 고른다(host 우선 — B안).
- 무효 조합이 부착 시점에 거절된다(무차감·loud): 통통구슬×{아틸러리·폭탄맨}, CC/스택×{폭탄맨·캐스터}, 비수×힐러.
- **통통구슬이 머신거너에서 걸린다**(unit 6 — 이 spec 의 원래 동기). 방향탄이 pierce 를 다 쓰면 호밍으로 전환해 튕긴다. `pierceCount: 1` 인 머신건 탄의 실사용 형태는 "맞히고 튕김".
- **투사체가 대상의 죽음에 증발하지 않는다**(opt-in `retargetTileRange`). `DeadTag` 만 붙은 시체까지 재조준 트리거로 본다.
- `NearestTargeting` — 반경 내 최근접 선정이 도메인 중립 유틸로 존재한다(`FrontmostTargeting` 계열).

## Key Files

- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — 판정의 유일한 권위
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 세 사건 지점 + `SpawnNeedleCarrier`/`PickFallbackTarget`
- `Assets/_Project/Scripts/Battle/Combat/NearestTargeting.cs` · `CastEvents.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `BuildHostProfile`, bake 단일 게이트
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (PathHit arm 꼬리) · `ProjectileMoveSystem.cs` (Homing arm 재조준)
- `Assets/_Project/Tests/EditMode/DcApplicability*Tests.cs` · `AttackSystemUnifiedLoopTests.cs` · `ProjectileRetargetAndBounceTests.cs`

## Verified

- EditMode **1480 전원 통과**(실패 0, 기존 Ignored 2).
- Play 실측: 4 host × 3 카드 판정이 잠금 표와 일치 · 재진입 2회 후 `CastEventsSingleton` **정확히 1개**(큐 3점 세트 대칭) · 콘솔 에러 0.
- critic 2종(설계·ECS) 병렬 리뷰 2회. 1차는 spec 초안, 2차는 구현.

## Notes (되돌리면 안 되는 의도)

- **RESOLVE 카운트를 START 로 옮기지 마라.** 응축된 일격 pre-scan 이 이미 증가한 카운터를 읽고, 처형타 게이트가 wind-up 이전 HP 를 보고, "지연 무산 시 카운트 없음"이 사라진다. 이 spec 은 RESOLVE 를 **손대지 않고** 도달 불가 host 에만 지점을 추가했다.
- **폭탄맨 카운트는 `landValid` 안.** 폭탄이 실제로 손을 떠난 프레임만 1카운트다.
- **폴백 진영은 `Faction.Enemy` 고정.** host mask 를 재사용하면 힐러 자해가 되돌아온다.
- **판정 키는 SO `flightMode` 가 아니라 host 의 실제 발사 경로.** `Projectile_Bomb` 은 `flightMode: 0`(Homing)이라 SO 만 보면 폭탄맨이 bounce 지원으로 오판된다.
- **바운스 전환은 마지막 히트 프레임에서만.** 못 맞히고 사거리 끝에 닿으면 기준점이 없어 소멸한다 — 프레임을 넘겨 `lastVictim` 을 기억하는 상태를 만들지 마라.
- **전환 시 `AttackOutputElement` 를 뗀다.** 안 떼면 경로 히트엔 없던 상태이상이 바운스 홉에만 걸리는 비대칭이 생긴다(방향탄에 non-Damage output 이 붙는 순간 열리는 구멍).
- **테스트에서 PathHit 투사체를 손으로 만들면 `PathHitRecord` 버퍼를 붙여라.** 없으면 ECB playback 이 `AppendToBuffer` 에서 끊겨 뒤따르는 `SetComponent`/`DestroyEntity` 가 유실된다 — "데미지는 들어갔는데 상태만 안 바뀐" 유령 증상으로 위장한다.
- **`HazardCastSystem [UpdateBefore(AttackSystem)]`.** 없으면 상대 순서가 정렬기 tie-break 에 맡겨져 "가끔 한 프레임 늦게"가 된다.
- **`DcRejectReason.Unclassified` 는 배선 누락 전용.** 정상 거절 사유와 섞으면 다음 사람이 틀린 단서를 쫓는다. total 테스트가 이 값으로 미분류를 잡는다.
- 캐스터의 상호배타는 **에셋 값**(`attackRange 0` ∧ outputs 없음)에 기대므로 `HazardCasters_CannotAlsoCountViaResolve` 가드를 지우지 마라.

## Follow-up

- ~~시트 `tileRange` → 4~~ **완료**(2026-07-27, `curl` 대조). 다만 `PokeNeedle_HasPositiveFallbackRange` 는 SO 만 보므로 **시트 드리프트는 여전히 못 잡는다** — 이 셀이 다시 `0` 이 되면 폭탄맨·캐스터의 비수가 조용히 죽는다. 시트 수정 시 주의.
- 시트 `DcMechanics._projectileId` 가 아직 `ga_shard02` (실제는 `needle_flame`). `_` 접두사라 import 대상 밖이라 무해하지만 값은 stale.
- **실전투 시각 확인**: 폭탄맨·캐스터가 5회마다 니들을 쏘는 장면(시뮬 계약은 EditMode 4건이 덮는다). ※ 재조준·bounce 는 확인 완료, 이 항목만 남음.
- ~~방향탄 bounce 개통~~ **완료**(unit 6). 남은 축은 **밸런스**: 볼리 1회 = 10발이고 각 탄이 카드값만큼의 bounce 예산을 통째로 들고 나가 호밍 유닛 대비 실효 10배다(ecs-review M7). 시트에서 조정.
- ~~Play 시각 확인~~ **완료**(2026-07-28 사용자 확인) — 머신건 탄의 맞히고-튕김, 비수의 대상 사망 재조준 둘 다.
- `FrontmostTarget × facing 유닛` — 경로 의존이 아니라 **타게팅 규칙 의존**이라 현재 행렬로는 표현이 어색하다(붙지만 보너스가 inert).
- `payload × trigger` 배선표 — 지금 판정은 host 필터일 뿐이라 `AttackN × SelfTileAoe` 같은 조합이 통과한다(현 카탈로그엔 없음). 두 critic 모두 "별도 spec" 판정.
- `Projectile_Shuriken_GA` 의 `flightMode` 미직렬화(기본값 의존) — 명시하지 않으면 다음 사람이 대포를 ballistic 으로 오독한다.
