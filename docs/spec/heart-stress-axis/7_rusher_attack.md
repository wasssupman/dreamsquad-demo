# 7 — 돌격형이 일반 공격을 갖는다

## 목적

**돌격형(`Runner`·`Swift`)이 가는 길에 싸운다.** 단, 마음에 닿으면 여전히 **몸으로 들이받고
산화**한다(unit 0 rev 2 의 컨셉 유지).

> 왜 지금인가 — unit 6(본능 방패)이 「본능이 남아 있는 동안 마음이 안 깎이는」 국면을 만들었다.
> 그 국면에서 돌격형은 마음에 닿아도 아무 일 없이 사라져 **완전히 공짜**였다.
> 공성형은 마음 앞에서 기다렸다 치는데 돌격형만 한 방이 영영 없어지는 비대칭이었다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Runner.asset` · `Enemy_Swift.asset`
- `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs` — `canSiege` 정밀화
- `Tests/EditMode/UnitLifecycleSystemTests.cs` · `Tests/EditModeAssets/AuthoredTargetMaskTests.cs`

## 구현

**1. 저작 — 일반 공격 + 마음만 뺀 마스크.**

```
attackMethod    : None → Melee
outputs         : []   → 피해 10 (쿨다운 1초)
targetFactions  : 0    → 21 = DefenderUnit(1) | BlockingHazard(4) | DefenderInstinct(16)
                                                   ↑ DefenderCore(8) 만 빠져 있다
stabilityDamage : 50 유지 — 마음 도달 시 산화 직격
aggroAttackDamage: 5 → 0 — 진짜 공격이 생겨 도발 프로필이 죽은 데이터가 됐다
```

**2. ⚠ `DefenderUnit` 비트를 반드시 남긴다.** 「거점만 패는 놈」으로 만들면(마스크 20) 도발이
안 걸린다 — `AggroStateSystem` 이 «유닛을 노리는 적»에게만 도발을 거는데(battle-structures
계약 1), 그 판정이 마스크의 `AnyUnit` 비트를 본다. `Runner`·`Swift` 는 **13개 덱 전부**에 있는
상비 편성이라, 유인으로 못 막는 적이 두 종 늘어나는 것은 플레이 손실이 크다.
(설계 중 실제로 마스크 20 으로 갔다가 사용자 지적으로 되돌렸다 — 「어그로는 끌려야 한다」.)

**3. ⚠ `canSiege` 정의를 정밀화했다. 이게 이 unit 의 핵심이다.**

```
예전:  canSiege = AttackState 를 갖고 있나
지금:  canSiege = AttackState 가 있고 **그 마스크에 DefenderCore 가 있나**
```

정밀화하지 않으면 공격을 주는 순간 돌격형이 `canSiege=true` 가 되어 **마음 앞에 눌러앉는다.**
때리지도 못하면서(마스크에 마음이 없으니) 「필드에 적 0기」 판정을 영구히 막아 웨이브가
안 넘어간다 — `battle-structures` unit 0 이 회귀로 규정하고 되돌렸던 바로 그 증상이다.

**기존 적은 전원 무회귀**다: 일반 적(29)·거점 전담(28) 둘 다 마스크에 `DefenderCore` 를 갖는다.

**4. 저작 가드가 근거를 요구한다.** `AuthoredTargetMaskTests` 는 기본 마스크를 좁힌 적을 발견하면
**id 별로 근거가 적혀 있어야** 통과한다. 돌격형 항목에 두 단언을 넣었다 —
「마음이 없어야 한다」(산화의 근거)와 **「방어유닛이 있어야 한다」**(도발의 근거).
후자가 이 unit 에서 한 번 잘못 갔던 자리라 그물을 남긴다.

## 결과

| 국면 | 돌격형이 하는 일 |
|---|---|
| 수호 본능이 남아 있음 | 가는 길에 방어유닛·본능·방벽을 **판다**(10/초) |
| 본능이 다 무너짐 | 마음으로 달려가 **50 꽂고 산화** |
| 도발당함 | 일반 적과 **같은 경로**로 가디언에게 붙는다 |

## ⚠ 미해결 위험 — 시트가 이 저작을 절반 되돌릴 수 있다

코드 리뷰 2026-08-24 발견. unit 7 이 바꾼 5개 중 **3개가 시트 소유 필드**다
(`Data/StatImport/UnitStatImportDto.cs` 의 `EnemyStatDto` 가 리플렉션으로 SO 에 쓴다):

| 변경값 | 시트 소유 | 근거 |
|---|---|---|
| `attackMethod: None→Melee` | **예** | `UnitStatImportDto.cs:65` |
| `outputs[0].magnitude: 10` | **예** (`atk` 투영) | `:76` + `UnitStatFieldMapper` |
| `aggroAttackDamage: 5→0` | **예** | `:83` — 주석이 «LIVE scalar … NOT in the skip-list» 로 못박음 |
| `targetFactions: 0→21` | 아니오 | DTO 에 없음 |
| `stabilityDamage: 1→50` | 아니오 | DTO 에 없음 |

**되돌아가는 조합이 특히 나쁘다**: `attackMethod` 가 `None` 으로 복귀하면 `AttackState` 가
안 붙는데 `targetFactions: 21` 과 `stabilityDamage: 50` 은 **남는다** — 스펙 어디에도 없는
상태가 된다(공격은 없는데 마스크만 좁은 적).

**미확인**: 실제 시트 행에 그 컬럼이 채워져 있는지. 비어 있으면 null-skip 되어 무해하다.
확인은 **읽기 전용 curl** 로만 한다 — 임포터는 dry-run 이 없어 돌리면 즉시 에셋에 쓴다.

해소 방법 둘:
1. **시트에 반영** — 세 값을 시트 행에 써넣는다. 정본이 시트인 필드는 그게 정답이다.
2. **시트 컬럼이 비어 있음을 확인** — 그러면 SO 저작이 그대로 산다(현행 유지).

## 완료 기준

- [x] 컴파일 0 에러
- [x] EditMode **2604 실행 · 신규 실패 0**
      (잔여 1건 `UnitKitCatalogTests.malphite` 는 **사전 실패** — 커밋 `4bfba2c2`(2026-08-20)가
      설명에 「피해 40」을 덧붙여 2번째 줄이 30자가 됐다. Assets lane 이라 그때 안 걸렸고
      `desc` 는 시트 소유라 에셋만 고치면 로그인 임포트가 되돌린다 — 별건)
- [ ] Play: 돌격형이 가는 길에 방어유닛을 팬다
- [ ] Play: 가디언으로 **유인이 된다**
- [ ] Play: 마음에 닿으면 여전히 산화한다(눌러앉지 않는다)
- [ ] Play: 웨이브가 정상 케이던스로 넘어간다(「필드에 적 0기」가 성립한다)
