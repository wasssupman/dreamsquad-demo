# 0 — `AreaSpin` payload (자기중심 반경 즉발)

## 목적

「돈다」의 판정을 만든다. 드래곤 브레스(`AreaBreath`)의 형제이고 **도형만 다르다** —
방향 있는 부채꼴 대신 방향 없는 반경이다. 이 단위가 끝나도 **소비자는 0** 이다(에셋은 unit 2).
그래야 unit 2 에서 문제가 났을 때 «수학이 틀렸나 / 배선이 틀렸나» 가 갈린다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.AreaSpin = 22` append
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `AttackN` 디스패치에 arm + `ApplyRadialSpin` private static
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics` 저작 검증
- `Assets/_Project/Tests/EditMode/RadialSpinPredicateTests.cs` (신규)

## 구현

**payload append.** `AreaSpin = 22`. 필드는 **재사용만** 한다 — `magnitude` = 피해,
`tileRange` = 반경(타일). `DcPayloadSpec` 신규 필드 0 · `DcTriggerSlot` 신규 필드 0
(둘 다 이미 범용으로 굽는다). `AreaBreath` 의 `coneCosSq` 같은 bake 변환도 없다.
주석은 파일 관례대로 «왜 새 kind 인가»를 남긴다 — `AreaBarrage`(원격 진앙)를 자기중심으로
재사용하는 것은 의미 남용이고, `SelfTileAoe` 는 `AttackN` arm 이 없다.

**arm.** `AreaBreath` 분기 바로 옆에 형제로 놓는다. 순회 본문은 `ApplyConeBreath` 와 같은
이유로 private static 으로 뺀다(1900줄 시스템을 키우지 않고 단위 테스트가 가능하게).

★**세 술어를 그대로 가져온다** — ① `AttackState.targetMask` 진영 ② `targetTraversalLayers`
교집합 ③ 자기 제외. 후보 배열은 **전 진영 통합 풀**이므로 빠뜨리면 동료와 적 마음을 간다
(README 계약 4).

★**반경 판정은 `TileAoe` 의 기존 반경 술어를 쓴다** — 신규 순수 함수를 쓰지 않는다.
그 술어는 **Chebyshev(사각)** 이고 유클리드 원이 아니다. 그게 맞다: 이 게임의 다른 모든
광역(`ProjectileHitSystem` TileAoe · 어그로 · 밀집도)이 같은 모양이라 **플레이어가 이미
학습한 도형**이다. 콘이 world-space 를 고른 이유(타일 양자화가 1~3타일에서 방향을 ~45°
흔든다)는 **방향이 없는 이 payload 에는 적용되지 않는다.**
⚠ 따라서 unit 1 의 VFX 가 딱 떨어지는 원 경계를 그리면 판정과 어긋난다.

**bake 검증** — loud fail 로:
- `magnitude <= 0` → 회전이 피해를 안 준다
- `tileRange < unitType.attackRange` → 자기를 때리는 근접 유닛이 원 밖에 든다(README 계약 5)

**만들지 않는 것**: 「돌고 있다」 상태 컴포넌트 · 채널링 틱 시스템 · 도형 enum
(README 계약 1·3·6).

## 완료 기준

- [ ] Unity 컴파일 에러 0 · 콘솔 에러 0 (Unity 가 정본 컴파일러 — `dotnet build` 는 csproj 가 신규 파일을 조용히 빠뜨린다)
- [ ] `RadialSpinPredicateTests` 신규 통과. `ConeBreathPredicateTests` 를 본으로 하되 **거짓 보증을 만들지 말 것**:
  - 진영 마스크 밖 후보가 안 맞는다
  - 통행층 불일치 후보가 안 맞는다
  - **자기 제외** — 공격자 진영이 마스크에 포함된 구성으로 세워야 유효하다(콘 테스트 초판이 이 함정에 빠져 자기 제외를 지워도 통과했다). 같은 호출에서 이웃 1기는 맞는다고 단언해 셋업이 관측 가능함을 증명한다
  - 피해 0 이면 아무것도 append 하지 않는다
- [ ] EditMode 전량 실행 — **신규 실패 0**. 기존 4건(Coil·Twin·Spiral·Zig 맵 폭 계약)은 이 spec 과 무관
- [ ] 저작 검증 2건이 실제로 로그를 낸다(잘못 저작한 임시 에셋으로 1회 확인 후 폐기)
- [ ] 이 시점에 **라이브 동작 변화 0** — 어떤 에셋도 `AreaSpin` 을 쓰지 않는다
