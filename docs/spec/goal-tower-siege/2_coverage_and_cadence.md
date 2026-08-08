# 2 — 공격 커버리지 · 케이던스 상호작용 · 계약 정리

## 목적

"때릴 수 있다"의 **구멍**을 메우고, 공성이 웨이브 진행에 만드는 부작용을 판정한다. 그리고
`PastGoalTag` 의 의미 전환에 걸려 있는 타 spec 계약을 같은 커밋에서 갱신한다.
선행: unit 1.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (필요 시)
- `docs/spec/dreamcatcher-content-2/README.md` ·
  `docs/spec/dreamcatcher-attack-decoupling/README.md` · `.../2_payload_target_fallback.md`
- `docs/spec/three-minute-survival/README.md` — 안정도 모델 전환 한 줄
- `docs/reference/score-formula.md` — 안정도 절의 "유출 즉시" 서술
- `docs/reference/object-pipeline-map.md` — 골 타워 아키타입 등재

## 구현

**1. 투사체 페이로드 커버리지** — `ProjectileTargetFaction` 은 2값(`Enemy`/`Defender`)이고
`ProjectileHitSystem` 의 피해자 풀은 `DefenderUnitTag` / `AttackUnitTag` 쿼리다. `GoalTower` 는
어느 쪽에도 없다. 아키타입별로 표를 만들어 **못 때리는 것은 N/A + 이유**로 남긴다:

| 적 공격 아키타입 | 타워 피격 | 근거 |
|---|---|---|
| 근접(Melee) | ○ | `AttackSystem` 이 타겟에 직접 `IncomingDamage` append |
| 투사체 `SingleSplash`/`HomingToEntity` | ○ | `target` 엔티티에 직접 append |
| 투사체 `TileAoe` | ○ (**이번에 고침**) | 피해자 풀이 `DefenderUnitTag` 쿼리라 타워가 빠져 있었다 → `WithAny<DefenderUnitTag, GoalTowerTag>`. 증상이 "보스만 타워를 못 부순다" 라 조용했다 |
| 해저드 캐스트 | N/A | `HazardCastSystem` 후보가 `PathFollowState` 를 요구 — 타워는 안 가진다 |

> rev 2 로 타워가 `Faction.Defender` 가 되면서 `TileAoe` 를 제외한 나머지는 **원래부터**
> 동작했다는 것이 확실해졌다. `TileAoe` 만 피해자 풀이 `DefenderUnitTag` 쿼리라 여전히
> 예외이고, 그래서 `WithAny<DefenderUnitTag, GoalTowerTag>` 패치가 남는다.

**실측 결과 결함이 실재했다.** `ProjectileHitSystem` 의 defender 풀은
`WithAll<DefenderUnitTag, LocalTransform>` 이라 타워가 빠졌다 — 보스 AreaBarrage 가 골 위에
떨어져도 안정도가 한 톨도 안 줄었다. 근접 공격은 타겟에 직접 append 라 멀쩡했기 때문에
"보스만 타워를 못 부순다" 는 형태로 조용했다. `WithAny<DefenderUnitTag, GoalTowerTag>` 로
수정(진영 하드코딩을 늘리지 않고 쿼리에 타워를 포함).

**2. 웨이브 케이던스 상호작용** — 공성 적은 `AttackUnitTag` 를 유지하므로
`NoQueuedAttackersRemain()` 이 거짓이 되고 **"전멸 즉시 진행" 이 꺼진다**(상한 20초만 작동).
이건 three-minute-survival 의 사용자 결정("포함 — 필드에 한 마리도 없어야 진행")과 일치하지만,
**골에 사거리가 닿는 배치칸이 없는 맵이면 영구적**이다. 6개 맵의 골 인접 `Place` 타일을
실측해 표로 남기고, 닿지 않는 맵이 있으면 지형 수정을 별도 spec 으로 올린다.

**실측 결과: 6개 맵 9개 골 전부 인접 거리 1 에 배치칸이 있다.** 사거리 1 짜리 근접 방어유닛도
골에 붙은 적을 때릴 수 있으므로 구조적으로 막히는 맵은 없다.

| 맵 | 격자 | 골 | 최근접 Place 거리(Chebyshev) |
|---|---|---|---|
| Coil | 15×12 | (13,6) | 1 |
| Hook | 13×12 | (9,0) | 1 |
| Serpent | 15×11 | (7,6) · (7,4) | 1 · 1 |
| Spiral | 15×12 | (7,0) | 1 |
| Twin | 15×12 | (5,10) · (13,10) | 1 · 1 |
| Zig | 15×12 | (9,0) · (5,11) | 1 · 1 |

측정 방법: `MapDocument_*.asset` 의 `tiles`(셀당 hex 1바이트, `Place = 1`)를 골 셀에서
Chebyshev 반경으로 훑어 최초 Place 를 찾는다. 지형이 바뀌면 다시 재면 된다.

**3. 계약 문서 갱신** — `PastGoalTag` 를 "유출 대기 = 무효 타겟" 으로 못박은 곳:
`dreamcatcher-content-2/README.md:80,99,107-109,236` · `dreamcatcher-attack-decoupling/README.md:67`
· `2_payload_target_fallback.md:26`. 새 의미("타워에 붙어 살아 있음 = 유효 타겟")로 고친다.
안 고치면 미래 세션이 버그로 오인해 배제를 되살린다.

**4. 파이프라인 맵 등재** — `docs/reference/object-pipeline-map.md` 에 골 타워 아키타입 행을
추가한다(Blocking 해저드와 나란히). README 의 커버리지 표를 그대로 옮긴다.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러/경고 0
- [ ] Play: 보스(`TileAoe` 계열 포함)가 골에 도달해 타워를 때리면 안정도가 준다
- [ ] 6개 맵 골 인접 배치칸 실측 표가 채워졌다
- [ ] Play: 골 근처에 방어유닛을 두면 공성 적을 잡아 **전멸 진행이 다시 살아난다**
- [ ] 드림캐쳐 spec 3파일의 `PastGoalTag` 계약 문구가 새 의미로 갱신됐다
- [ ] `object-pipeline-map.md` 에 골 타워 행이 있다
