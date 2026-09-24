# 7e — 카드 효과 자동 검증 (조각 D 부록 · 검증 장치)

> 사용자 질문(2026-09-24): *「모든 걸 테스트해 보기 어려운데, 매번 드림캐쳐 효과를 검증하는 방식을 찾자.」* 카드 커버리지 감사(7b 말미)는 리드가 **손으로 한 번** 한 것이다 — 이 unit 은 그것을 **매 커밋 자동으로**, 그리고 **발동까지** 굳힌다. 규칙을 새로 만들지 않는다(계약 3). 검증 장치만 세운다.

## 목적

카드가 늘거나 시트·SO 가 바뀔 때 사람이 판을 돌려 보지 않아도 **「구워졌고 · 발동하고 · 그 종류의 효과가 실제로 걸렸다」**를 카탈로그 전량에 대해 테스트가 증언한다. 사람이 볼 것은 ×인 카드로 줄어든다.

한계를 먼저 적는다: 이 장치는 **발동과 효과의 존재**를 증언한다. 「세기가 게임적으로 맞다」 · 「그림이 잘 보인다」는 못 잡는다 — 그것은 플레이 몫이다.

## 세 겹

| 겹 | lane | 무엇을 증언하나 | 왜 이 lane |
|---|---|---|---|
| **① 전량 발동 관측** | `EditModeAssets` | 카탈로그 카드 전부: 붙이고(또는 시전하고) 강제 발동시키면 **그 카드의 의도 종류에 맞는 관측값**이 난다 | 굽기가 SO 를 읽어야 해서. Unity 안 |
| **② 저작 스냅샷** | `EditModeAssets` | 카탈로그를 구운 결과(카드별 규칙 줄 수 · 종류 · 수치)가 굳힌 파일과 같다 | 시트 임포트·SO 편집이 카드를 되돌리는 함정을 잡는다 |
| **③ 자가진단 메뉴** | 에디터 Play | ①을 **살아 있는 판**에서 돌려 콘솔에 카드별 ○/× + 「왜 안 걸렸나」를 찍는다 | 플레이 중 사람이 ×만 보게 |

②의 비자명한 규칙 시나리오(호접몽 파탄 · 인수인계 · 표식 배 · 궁지 폭발 게이트 · 실드 파열 · 광란 상한)는 7a·7b 의 증상 테스트가 이미 든다 — 이 unit 은 **부족분만** 채우고 카드 한 장 = 테스트 한 개 규율을 지킨다(표 ④).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 관측 규칙표 | `BattleCore/Trigger/EffectWitness.cs` — **의도 종류(`SimIntentKind`) → 무엇을 보면 걸린 것인가**. 순수 함수. 카드마다 기대값을 적지 않는다(카드가 늘어도 표가 안 는다) |
| 발동 하네스 | `BattleCore/Harness/CardProbe.cs` — 정의표·카드 줄 하나를 받아 **판 하나를 결정론으로 세우고**(더미 숙주 1 · 표적 적 N · 실드/체력/스택 전제) 부착 또는 시전 → 강제 발동(`DebugFireBinding` 25 / `DebugCastCard` 23) → `EffectWitness` 로 판정. 결과 = `CardProbeResult{ Row, Baked, Fired, Witnessed[], Missing[], Diagnosis }` |
| ① 테스트 | `Tests/EditModeAssets/CardEffectWitnessTests.cs` — 카탈로그 전량 + 액티브 6 을 `TestCaseSource` 로 한 장씩(실패가 카드 이름으로 보이게) |
| ② 스냅샷 | `Tests/EditModeAssets/CardBakeSnapshotTests.cs` + `Tests/EditModeAssets/Fixtures/card_bake_snapshot.txt`(canonical 텍스트 · 카드 순) · 갱신 메뉴 `Editor/BattleCore/CoreCardSnapshotMenu.cs`(「의도한 변경이면 갱신」) |
| ③ 메뉴 | `Editor/BattleCore/CoreCardSelfCheckMenu.cs` — `Wassup/BattleCore/Debug/카드 자가진단`: 살아 있는 `BattleDriver.Match` 정의표로 `CardProbe` 를 카드마다 돌리되 **별도 `BattleMatch` 인스턴스**에서(사용자 판을 건드리지 않는다) · 콘솔 표 |
| ④ 부족분 | `Tests/EditModeCore/CardRuleTests.cs` — 7a·7b 증상 테스트가 안 드는 비자명 규칙만(구현 중 목록 확정, 5장 안쪽 예상) |

## 구현

1. **관측은 의도 종류로 판정한다.** `IntentApplier` 가 쓰는 표면이 하나(S20)라, 카드의 페이로드가 무엇이든 결국 의도 종류 몇 개로 접힌다. 표: `DealDamage` → 표적 인박스/체력 감소 · `Heal` → 체력 증가 · `ApplyStatModifier` → `Modifiers` 에 그 출처 슬롯 · `ApplyStack` → `Stacks.CountOf` 증가 · `ApplyCc` → `Cc` 슬롯 · `ApplyDot` → `Dot.Slots` · `ClearCc` → 슬롯 감소 · `GrantShield` → 실드 합 증가(**다음 틱** 드레인 — 관측 시점 +1틱) · `Taunt` → `Aggro.Target` · `SpawnProjectile` → `ProjectileSpawned` 사건 · `SpawnHazard` → `HazardSpawned` · 진행형(치명·고치·표식·인수인계·배치 오라) → 해당 상태 플래그/`BindingAttached`. **기대 의도 목록은 카드에서 파생한다**: `BindingDef` 의 라우팅(`SkillRouting`)이 어떤 concrete 를 고르는지 → 그 concrete 가 내는 의도 종류. 카드마다 손으로 적지 않는다.
2. **전제는 페이로드가 정한다.** 처치/사망 트리거는 강제 발동(`DebugFireBinding`)으로 대신하되, 「피해」 관측엔 표적 적이 반경 안에 있어야 하고 「실드 파열」엔 실드가 먼저 있어야 한다. `CardProbe` 가 라우팅 concrete 의 형(`RangeCatalog`)을 읽어 표적을 **그 형 안에** 세운다(몸에서 나오는 것 = 숙주 옆 · 자리에 떨어지는 것 = 자리 옆). 판정 산식은 **정본 진입점만** 호출(제약 13).
3. **강제 발동은 규칙을 우회하지 않는다.** `DebugFireBinding` 은 카운터만 건너뛰고 상한·게이트·대상 필터는 그대로다(7d). 그래서 「게이트 때문에 안 터졌다」도 ×가 아니라 **진단**(`BindingDiagnosis` 4원인)으로 찍힌다 — ×는 「구워졌고 발동했는데 관측값이 없다」만이다.
4. **액티브 6 은 시전으로.** `DebugCastCard` 로 표적 칸에 쏘고 실행자별 관측(운석 → `ProjectileSpawned` · 포탈 → `FieldSpawned` · 파워서지/속사 → 아군 장 `FieldSpawned` + 스탯 슬롯 · 감속장 → `HazardSpawned` · 회오리 → `FieldSpawned`). 이 6 은 코어 테스트에 실행자 이름이 없었던 검증 공백(감사 기록)이다.
5. **스냅샷은 canonical 텍스트다.** `CardDef.Canonicalize` 가 이미 있다(configHash 재료) — 카드별 텍스트를 이어 붙인 파일 하나. diff 가 곧 「무엇이 바뀌었나」다. 갱신은 메뉴로만(테스트가 파일을 쓰지 않는다).
6. **자가진단은 사용자 판을 건드리지 않는다.** 살아 있는 `Definition` 을 빌려 **새 `BattleMatch`** 를 세운다. 결과는 콘솔 한 표(카드 · 구움 · 발동 · 관측 · 진단) + × 만 경고로.
7. **결정론.** 프로브 판은 시드 고정 · 틱 수 고정(관측 창은 의도별 최대 지연 = 실드 1틱 · 투사체 비행 N틱을 `EffectWitness` 가 안다). 헤드리스 코어 테스트가 필요하면 `CardProbe` 는 순수 C# 이라 고정구 정의표로도 돈다(④가 그렇게 쓴다).
8. **하드코딩 0.** 관측 창 틱 수·표적 수·거리는 `CardProbe` 의 **구조 상수**(주석에 근거)이고 밸런스 값이 아니다. 카드 수치는 전부 정의표에서.
9. **이 unit 은 코어 규칙을 바꾸지 않는다.** ①에서 ×가 나오면 **그 카드가 결함**이고 여기서 고치지 않는다 — 카드 이름·의도·진단을 보고서에 올리고 7b 「고친 것」으로 별도 커밋(리드 판단).

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 옛 `DcMechanic` 단위 EditMode 테스트 묶음 복사 | 옛 테스트는 ECS 고정구라 헤드리스에서 안 돈다. 규칙은 7a·7b 테스트가 이미 새 형으로 든다 | 제거 |
| 카드별 손으로 적은 기대값 표 | 카드가 늘면 표가 늘고 시트와 두 벌이 된다 — 의도 종류로 파생 | 설계 선택 |

## 완료 기준

- [x] **① `CardEffectWitnessTests`**: 카탈로그 45 + 몽마의 계약 1 + 액티브 6 = 52 케이스가 한 장씩 뜬다(`TestCaseSource`). 전부 ○ — ×가 있으면 **그 카드 이름과 진단**을 보고서에(코어 수정 금지). Assets lane 기준선 빨강은 `bomb_man`·`boomerang` 2건만.
- [x] **① 반증**: 카드 한 장의 의도 적용을 일부러 끄면(테스트 안에서 `IntentApplier` 우회 스텁 또는 라우팅 표 항목 제거) 그 카드가 **×로 떨어진다** — 장치가 「구워졌는데 아무 일도 없다」를 실제로 잡는다는 증언 1건.
- [x] **② 스냅샷** 파일 커밋 + 테스트 초록 + 갱신 메뉴. 반증: 카드 SO 값 하나를 메모리에서 바꾸면 빨갛다.
- [ ] **③ 메뉴** Play 중 실행 → 콘솔 표 52행 · 사용자 판 무변(틱·개체 수 전후 동일 단언은 PlayMode 스모크 1건).
- [x] **④ 부족분** 목록과 테스트(있으면). 없으면 「없음」과 근거.
- [x] 코어 변경은 `EffectWitness`·`CardProbe`(둘 다 판정·상태를 갖지 않는 순수 도구) 외 0. `Unity.Entities` 0 · 매니저 0.
- [ ] 헤드리스 export 3종 초록 · Unity EditMode 코어+Assets · PlayMode 코어 초록 · 골든 무변 · `check_ledgers.py` exit 0(미정 0 유지). — PlayMode 코어만 미충족(선행 빨강 4, 구현 기록)
- [ ] `core-reviewer` APPROVE.
- [x] README 작업 표에 7e 행 · 상태 라인에 「카드 52장 자동 증언」 · `docs/reference/test-procedure.md` 에 Assets lane 의 이 두 테스트와 스냅샷 갱신 절차 한 줄.

> **구현 기록(2026-09-24)** — `0164a940a`(① + 반증) · `8d98e45f4`(② + 반증) · `4d390a78a`(③) · `8d901b34d`(④).
> - ① 52 케이스 전부 ○(× 0 — 보고할 결함 없음). 반증 ⑴ 의도 적용 끄기(`CardProbeOptions.MuteIntents` — 기록은 하되 적용 안 함) → `cost1_as` 구움·발동 ○ 인데 × · ⑵ 라우팅 제거(실행자 null) → 굽기 ×. 헤드리스 자가 테스트 `CardProbeTests` 3(고정구 카드 ○ · 반증 · 원본 정의표 무변).
> - `AttackN` 만 25 대신 감지자 모양 사건(`RaiseFor` · 공격 seam) — 25 의 사건엔 대상이 없어 대상형 실행자가 할 일이 없다. Squad 줄은 부착 전개만(25 는 주어 필터를 안 지난다). 공격 수식자·인수인계는 의도가 없어 「달렸다」(`AttackMod`) · 「동반 카드가 손패 맨 앞」(`HandFront`)으로 본다.
> - ② 반증: `poke_needle` 크기 +1 → 478번째 줄에서 빨갛다, 되돌리면 초록.
> - ③ ⑴ 카탈로그 전량(드라이버 저작 + 카탈로그 카드) ⑵ 이 판의 덱 두 표. PlayMode 스모크 `CoreCardSelfCheckTests` 초록(틱·개체·사건·해시 무변). **메뉴 자체의 Play 실행은 사용자 확인 대기.**
> - ④ `CardRuleTests` 5 — 궁지 폭발 `OnDamagedN × HpBelow(Self)` 게이트(공격 쪽만 있었다) · 실드 파열 순간 · 퇴근 운석 자리형(몸 0 · 비워진 칸) · 마지막 불꽃 미귀속 죽음(처치 사건 0 · 각성 0) · 파쇄의 찬가 `DamageVsCcMul` 조건부 소비(테스트 0 건이었다 — pin·분류기뿐). 근거 grep: `OnShieldBreak`·`OnRetire`·`DamageVsCc` 는 라우팅/핀 테스트에만, `Lethal` 은 「선다」까지만.
> - 검증: 헤드리스 export 4 SHA 전부 build 0 · test 673 → **678** · Check 0 · `check_ledgers.py` exit 0(미정 0) · Unity EditMode 코어+Assets **992/994**(선행 `bomb_man`·`boomerang`) · 골든 무변(코어 lane 689/689) · PlayMode 코어 **53/57** — 빨강 4 는 전부 `CoreMatchOutcomeTests`, 원인 = `d06ae0bcd`(dev 카드 덱을 씬 `_cards` 에) 이후 각성 저작 없는 테스트 모드가 카드 빌더 에러를 낸다. 이 unit 과 무관 — 리드 판단.
