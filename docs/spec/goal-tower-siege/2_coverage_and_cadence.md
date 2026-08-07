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
| 투사체 `TileAoe` | **확인 필요** | 피해자 풀이 `DefenderUnitTag` 쿼리 — 보스 계열이 여기 걸린다 |
| 해저드 캐스트 | N/A | `HazardCastSystem` 후보가 `PathFollowState` 를 요구 — 타워는 안 가진다 |

`TileAoe` 가 타워를 못 때리면 **보스가 골에 도착해도 안정도가 안 줄어드는** 침묵 결함이다.
실측 후, 고칠 경우 피해자 풀에 `GoalTowerTag` 를 더한다(진영 하드코딩을 늘리지 말고 쿼리에
타워를 포함시키는 방향).

**2. 웨이브 케이던스 상호작용** — 공성 적은 `AttackUnitTag` 를 유지하므로
`NoQueuedAttackersRemain()` 이 거짓이 되고 **"전멸 즉시 진행" 이 꺼진다**(상한 20초만 작동).
이건 three-minute-survival 의 사용자 결정("포함 — 필드에 한 마리도 없어야 진행")과 일치하지만,
**골에 사거리가 닿는 배치칸이 없는 맵이면 영구적**이다. 6개 맵의 골 인접 `Place` 타일을
실측해 표로 남기고, 닿지 않는 맵이 있으면 지형 수정을 별도 spec 으로 올린다.

| 맵 | 골 인접 Place 타일 | 사거리 1 유닛으로 공성 적 타격 가능 |
|---|---|---|
| (실측 후 채운다) | | |

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
