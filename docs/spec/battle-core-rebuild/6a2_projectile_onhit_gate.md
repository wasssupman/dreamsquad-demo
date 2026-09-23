# 6a2 — 투사체 착탄 출력 관문 (조각 C · 슬롯 뒤)

## 목적

**시전자가 쏘는 모든 탄이 시전자의 착탄 효과를 싣는다.** 사용자 결정 2026-09-24 ①. 옛 전투는 착탄 출력(스탯·스택 부여 — 킨들러 화염, 난도질꾼 출혈)을 **평타 팔 안**에서만 주입해 포물선탄·카드탄·배치 스킬탄은 원천 배제했다(`docs/spec/dreamcatcher-attack-mod-bounce/README.md:39` 가 그 배제를 계약으로 적었다). 새 코어의 `ProjectileRequest` 는 `Damage` 만 싣고 착탄 출력은 unit 3 이 「unit 6 이월」로 남겼다 — 지금은 탄이 스택을 걸 자리가 없다. 이 unit 이 그 자리를 **관문 한 곳**으로 연다.

용어: 「부여」는 새 어휘가 아니다. 어휘 = 이미 있는 착탄 출력 `AttackOutputDef`(Damage·Heal·ApplyStat·ApplyStack) + 6a 의 CC/DoT 부여. 새로 여는 것은 **적용 범위**(모든 탄)와 **겹침 규칙**(합·상한)뿐.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 시전자 부여 슬롯 | `BattleCore/Effects/ProjectileImbueSet.cs`: `Unit.Imbue`(부분, null 허용). 슬롯 키 = (부여자 `SimEntityId`, 출력 종류·대상: `ApplyStack` 이면 `StackKind`, `ApplyStat` 이면 `(Stat, Op)`, CC 면 `CcRequestKind`). 병합 = **합**(같은 키의 `Magnitude` 가산), **상한** = 정의표 값(`ImbueCapDef`, 종류별) — 6a 의 `StackCap`/회수(슬롯 삭제)/`dirty` 규율 재사용. 사용자 결정 ② |
| 관문 | `Phases/TickProjectilePhase.SpawnRequested`: 요청을 탄으로 만들기 **직전**에 `req.Owner` 의 (저작 착탄 출력 + `Imbue` 슬롯)을 접어 `Projectile.OnHit`(값 스냅샷 배열, 풀)에 싣는다. 순서 = 정의표 → 요청 → 부여, **요청이 명시한 값은 부여가 덮지 않는다**. 시전자가 착탄 전에 죽어도 탄은 발사 시점 값으로 적용(제약 13 스냅샷 선례) |
| 착탄 적용 | `TickProjectilePhase` 착탄 지점(단일·칸광역·경로스윕·길막 — 길막은 출력 0)에서 `CombatPhase.ApplyOutputs` 와 **같은 함수**(6a 가 여는 적용 관문 `EffectApply.*`)를 호출한다 — 평타와 탄이 다른 자를 쓰지 않는다 |
| 정의표 | `Match/CombatDefs.cs`: `ImbueCapDef[]`(종류별 상한, SO `ImbueCapConfig` → 빌더). 값이 없으면 상한 없음이 아니라 **빌더 오류**(제약 6) |
| 사건 | `CoreEvent.ImbueChanged`(시전자·키·합계) — 뷰(6c)·트레이스 구독. 착탄 부여는 6a 의 `StackChanged`·`ModifierApplied`·`CcApplied` 그대로 |
| 테스트 | EditMode: 카드탄·배치 스킬탄·포물선탄이 시전자 착탄 출력을 싣는다(생산자는 `DebugFireProjectile` 커맨드로) · 같은 키 부여 둘 = 합 · 상한 초과 안 됨 · 회수 = 슬롯 삭제 · 시전자 사망 뒤 착탄도 적용 · 요청 명시값 우선 · 결정론 |

## 구현

1. **생산자는 자기가 무엇에 얹히는지 모른다.** 평타 루프·배치 스킬·카드·도약·해저드 어느 경로든 `ProjectileRequests` 에 넣기만 한다. 접는 것은 관문뿐이다. 생산자 경로 자체(스킬·카드)는 unit 7.
2. **평타의 착탄 출력도 관문을 지난다.** `CombatPhase` 가 직접 `ApplyOutputs` 하는 근접·즉시 공격은 그대로이고, 투사체 경로는 발사 시점에 접힌 `OnHit` 을 착탄에서 적용한다. 킨들러 화염·난도질꾼 출혈은 이 경로로 **처음** 탄에 실린다(unit 3 이월 해소).
3. **겹침 = 합, 상한 = 정의표.** 사용자 결정 ②. 스택 종류의 최대 중첩(`StackRuleDef`)은 별개 축이다 — 부여 상한은 「한 발에 실리는 세기」, 최대 중첩은 「피해자에게 쌓이는 수」.
4. **어휘를 늘리지 않는다.** 사용자 결정 ③: 「화염 부여」= `ApplyStack(Fire)`, 「출혈 부여」= `ApplyStack(Bleed)`. 새 enum 없음.
5. 공격 수식자 5종(튕김 부여·최전방 배율·응축 배율·수면 배율·2연발)은 이 슬롯의 **다른 종류**로 같은 관문에 모인다 — 실행은 unit 7(rev 3 `AttackMod` 축).

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 평타 팔 안에서만 주입되던 배제(`DcApplicability` 발사 경로 사전 차단) | 사용자 결정 ①로 폐기 — 모든 탄에 적용 | 규칙 변경(사용자) |

## 고친 것

(구현 후)

## 완료 기준

- [ ] 헤드리스 초록 · EditMode 코어 lane 초록(위 테스트) · PlayMode 40 무변.
- [ ] 킨들러/난도질꾼의 탄이 스택을 건다(EditMode 규칙 테스트) — unit 3 이월 행 닫힘.
- [ ] `ProjectileRequest` 에 출력 필드를 **추가하지 않는다**(관문이 접는다) — grep.
- [ ] 골든: 값이 바뀌는 시나리오 기록(재굽기는 6b2 에서 Unity 로 한 번).
- [ ] `core-reviewer` APPROVE.
