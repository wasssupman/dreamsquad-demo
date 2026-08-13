# 1 — 회오리 연출 (지속으로 읽히게)

## 목적

**연출은 지속, 판정은 연타**(README 계약 6)를 화면에서 성립시킨다. 회전 pulse 는 이산 사건인데
플레이어는 «계속 돌고 있다» 로 읽어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/UnitAttackVisualEvents.cs` — 필드 append
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `AreaSpin` arm 에서 필드 채움
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 드레인에서 **위임만**
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — `SpawnWhirl` + 프리팹 슬롯
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 밴드 1개
- `Assets/_Project/VFX/` — 회오리 프리팹(신규 또는 벤더 복제본)

## 구현

**채널 신설 0.** 기존 `UnitAttackVisualEvent` 에 `hasWhirl` + 반경을 append 한다 —
드래곤 브레스가 `hasAreaBreath`/`breathRangeWorld` 로 같은 일을 했다. Burst ISystem 은
`VfxSpawner` 를 부를 수 없으므로 경로는 sim → 이벤트 → 브리지 드레인 → 스포너다.

★**연출 소유권은 `VfxSpawner`.** 프리팹 슬롯·스폰·정렬·수명이 전부 거기 있고 브리지는
뷰 앵커만 풀어서 위임한다. **브리지에 프리팹 슬롯을 되돌리지 말 것** — `b7750a4b` 에서
사용자 지적으로 이관한 소유권이고 드래곤이 그 회귀를 테스트로 고정하고 있다.
루프형 벤더 프리팹의 단발화는 `ConfigureOneShot` 이 하고 **공유 에셋은 건드리지 않는다.**

**지속감 = 수명 이어붙이기.** pulse 마다 원샷을 쏘고 **수명을 공격 주기보다 길게**
(1.2~1.5배) 두면 끊김 없이 이어지고 최대 2개만 겹친다. 공격이 멈추면 자연 소멸한다 —
on/off 상태도, sim 상태 읽기도, 새 컴포넌트도 필요 없다(README 계약 1).

> **겹침 누적으로 밝기가 튀거나 깜빡이면** escalation 은 `StatusFxSpawner`/`StatusFxView`
> 다 — 유닛당 인스턴스 1개를 보장하는 지속 FX 계층(수면 Zz·스턴 별이 이미 탄다). 대가는
> `StatusFxKind` append + 레지스트리 항목 + 「교전 중인가」 sim 상태 폴링이고, 무엇보다
> **회오리는 상태가 아니라 능력이라서** 그 계층의 의미와 어긋난다. 먼저 싼 쪽을 시도한다.

**회전.** 스파인 스켈레톤은 **돌리지 않는다** — 벤더 리그에 회전 애니가 없어 억지로 돌리면
발이 하늘로 간다. 「돈다」는 체감은 이펙트가 전담하고 몸은 로코모션 루프를 유지한다.
⚠ 이펙트를 돌릴 때 **뷰 루트의 `transform.rotation` 을 쓰지 말 것** — `Billboard` 가
*"틸트/페이싱 회전의 유일 소유자"* 로 매 `LateUpdate` 에 덮어쓴다. **자식**에서 돌려야
빌보드가 세운 평면을 상속한다(무기 궤적 리그가 자식이어야 하는 것과 같은 이유).

**모양 주의 2건**
- 판정은 Chebyshev(사각)다 — 딱 떨어지는 **원 경계선을 그리지 말 것**(unit 0)
- **번아웃 먹구름과 달라야 한다**(README 계약 8). 번아웃은 방어유닛 *상태*, 이건 적 *공격*이다

**정렬**: `BoardSortOrder` 밴드 1개 추가. 회오리는 유닛 발밑에 깔리는 편이 자연스러우니
유닛보다 아래를 1차 후보로 두고 **육안 확인 후 값을 확정**한다(`AreaBreathOrder = 14000` 참고).

## 완료 기준

- [ ] Unity 컴파일 에러 0 · 콘솔 에러 0
- [ ] 빈 프리팹 슬롯에서 경고 1줄 후 무동작(폴백 로그 관례) — 조용히 사라지지 않는다
- [ ] `VfxSpawner` 에 프리팹 슬롯이 있고 **브리지에는 없다**(드래곤 회귀 테스트와 같은 성질을 grep 으로 확인)
- [ ] EditMode 전량 — 신규 실패 0
- [ ] **Play 육안**(unit 2 이후 가능): 회전 중 회오리가 **끊겨 보이지 않는다** · 밝기가 튀지 않는다 · 번아웃 먹구름과 헷갈리지 않는다 · 회오리가 몸을 가리지 않는다
