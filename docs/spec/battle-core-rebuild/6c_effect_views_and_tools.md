# 6c — 효과의 그림 · 디버그 도구 · 장부 (조각 C · 4/4)

> 5a 가 **일부러 안 만든** 네 풀(상태 FX · 빔 · VFX · 오라)을 여기서 만든다. 그때의 이유가 「구독할 사건이 코어에 없다」였고, 6a·6b·6b2 가 그 사건을 열었기 때문이다. **빈 풀을 먼저 만들지 않는다**는 규율의 반대쪽 절반이 이 unit 이다.

## 목적

6a·6b 가 연 사건이 **화면에 보인다.** 느려진 적에 감속 표식이 붙고, 타는 적에 불이 붙고, 실드가 부여되고 깨지고, 장판이 깔리고, 길막에 체력 바가 뜨고, 배치 순간에 링이 퍼지고 카메라가 흔들린다. 그리고 「왜 안 걸렸나」를 물을 수 있는 계측기 3개가 선다.

조각 C 는 이 unit 에서 끝난다 — **장부 마감과 `check_ledgers.py` 초록**이 그 경계다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 상태 FX | `BattleCoreUnity/View/CoreStatusFxSpawner.cs`(← `Presentation/StatusFxSpawner.cs` 118줄): 유닛당 붙는 상태 표식. 구독 = `CcApplied`·`CcCleared`·`DotApplied`·`StackChanged`·`ModifierApplied`·`ModifierRevoked` |
| 오라 | `View/CoreDcAuraVisualPool.cs`(← `DcAuraVisualPool.cs` 92줄) + `Effects/ModifierAuraClassifier.cs`(코어로 salvage — 순수 함수) |
| 빔 | `View/CoreBeamPresenter.cs`(← `BeamPresenter.cs` 238줄): 대상별 빔 세션(키 = **맞는 쪽**) |
| 일반 VFX | `View/CoreVfxSpawner.cs`(← `VfxSpawner.cs` 476줄): 타이밍 VFX 큐 · 배치 링 펄스 · 실드 부여/파열 원샷 |
| 해저드·픽업·사직서 뷰 | `View/CoreHazardViewPool.cs`(← `BlockingHazardPresenter` 278 + 존 비주얼) · `View/CorePickupViewPool.cs`(← `PickupPresenter` 131) · `View/CoreResignationViewPool.cs`(← `ResignationPresenter` 83) |
| 오버헤드 보강 | `View/CoreUnitOverheadUiLayer.cs`(5a 신설분): **실드 비율 · 스택 아이콘** 인자 개통(5a 는 0/`null` 로 흘렸다). 길막 오버헤드 게이지도 여기 |
| 선택 패널 | `BattleCoreUnity/UI/CoreSelectionPanel.cs`(5b 신설분): `ReadoutOf` 가 실효 스탯을 읽어 **델타 칩**이 그려진다. ⚠ **재곱 금지** — `MaxHealth` 에는 최대체력 배율이 **이미 반영돼 있고**(6a 구현 14), 조건부 배율(군중 제어 대상·최전방·바운스)은 **의도적으로 제외**한다(대상·시점 의존이라 접으면 거짓 표시가 된다) |
| 뷰 설정 SO | `Data/BattleView/StatusFxConfig.cs`(신설) + 5a 가 **소비처 0 으로 세워 둔** `PickupViewConfig`·`DcVisualConfig` 개통 |
| 방출 순서 | `BattleCoreUnity/ViewOrder.cs` 에 상수 추가: 해저드/장판(유닛 **앞** — 바닥에 깔린다) · 상태 FX·오라(유닛 **뒤** — 몸에 붙는다) |
| 디버그 도구 | `Editor/BattleCore/CoreHazardDebugMenu.cs`(존+길막 통합) · `CoreGimmickDebugMenu.cs`(피로·열기·픽업·사직서) — 전부 6b 의 커맨드를 넣는다 |
| 테스트 | `Tests/PlayModeCore/`: `CoreEffectViewTests`(사건 → 뷰 수 일치 · 소멸 회수 · 순서) |
| 장부 | `ledgers/bridge-methods.md` · `ledgers/tools.md` · `ledgers/rules.md` 마감 |

## 구현

1. **풀마다 자기 구독**(계약 12 — 통합 뷰 없음). 각 풀은 `SimEntityId → 자기 뷰` 사전 하나만 갖는다. 옛 `ReconcileStatusFx`(151줄)가 매 프레임 **월드를 폴링해 표식을 재조정**하던 모양은 안 옮긴다 — 사건 구독 + 소멸 사건 회수로 접힌다. 유령이 잡히면 자가 치유(경고 로그 + 회수)이고, 그것은 정상 경로가 아니라 **어떤 소멸 경로가 사건을 안 냈다**는 신호다(계약 7, 5a 와 같은 규율).
2. **오라 판정은 순수 함수가 한다.** `ModifierAuraClassifier` 를 코어로 salvage — 출처 필터 + **net 편차**를 함께 본다(중화된 슬롯은 비활성). `DamageVsCcMul`·`MaxHealthMul` 은 판정 제외다. 6a 가 회수를 슬롯 삭제로 바꿨으므로 「중화된 슬롯」은 이제 **삭제된 슬롯**이고, 판정은 그만큼 단순해진다 — 그래도 net 편차 축은 남긴다(같은 출처가 올리고 내리는 두 슬롯을 들 수 있다).
3. **실드 비율과 스택 아이콘을 오버헤드에 개통한다**(5a 이월 2행). 5a 가 인자를 0/`null` 로 흘린 자리다. ⚠ **부착 카드 줄은 여전히 안 만든다** — 부착 사건은 unit 7 이고, 빈 카드 슬롯을 먼저 만들면 「카드가 안 뜬다」를 사건이 아니라 UI 에서 찾게 된다.
4. **배치 연출은 유닛의 저작값으로 난다**(5b 이월 2행). `PlayDeploymentRingPulse` 의 프리팹·세기와 `FireOnPlaceCameraShake` 의 진폭이 전부 `DefenderUnitData` 에서 오므로 5b 가 지어낼 수 없었다. 카메라 흔들기의 주인은 `CameraDirector.Shake` 이고 **호출부만** 여기다.
5. **임팩트 소켓 높이를 개통한다**(5a 이월). 착탄 VFX 가 대상 몸통 높이에서 나려면 「대상이 누구인가」가 필요하고, 그 소비처가 이 unit 이다. 5a 가 `ProjectileViewFrame.targetSocketHeight`·`Blend` 를 **남겨 두고 0 으로 흘린** 자리를 채운다.
6. **제약 13 은 뷰에도 적용된다** — 그리는 자리는 사건의 `Site` 짝이 정한다. 제약 13 의 문면 그대로 **「몸 반경 0 = 그 자리는 칸이다」가 형 구분의 표현이고, 새 필드가 필요 없다**: `OriginBody == 0` 이면 **자리에 떨어지는 것**(장판·착탄·운석·착지 슬램)이라 칸 반폭으로 그리고, `> 0` 이면 **몸에서 나오는 것**(오라·자폭·시체 폭발·도발)이라 그 반경으로 그린다. 뷰가 반경을 **다시 계산하지 않는다** — 다시 계산하면 화면이 규칙을 틀리게 가르친다.
7. **방출 순서는 상수로 고정한다.** 바닥에 깔리는 것(해저드·장판·범위 표시)이 유닛보다 **앞**, 몸에 붙는 것(상태 FX·오라·오버헤드)이 **뒤**다. 씬 컴포넌트 등록 순서에 기대지 않는다(5a 의 `ViewOrder` 와 같은 규율, 회귀 방지는 `CoreViewOrderTests` 확장).
8. **런타임 머티리얼은 `RuntimeMaterialFactory` 경유다.** 옛 뷰의 `Shader.Find` + `new Material` 은 CLAUDE.md 추가 제약 위반이고(모바일 shader stripping 으로 null 이 돌아와 렌더가 깨진다) 복사하면서 같이 옮길 이유가 없다. 벤더 VFX 를 보드에 얹을 때의 함정 3종은 `unity-vfx-integration` 스킬을 먼저 읽는다.
9. **디버그 도구는 코어 커맨드를 넣는다**(tools.md 원칙). 브리지 메서드를 부르던 옛 메뉴 3개(`HazardDebugMenu`·`BlockingHazardDebugMenu`·`FatigueDebugMenu`)를 **둘로 통합 재작성**한다 — 존과 길막은 같은 메뉴(둘 다 「여기에 물건을 놓는다」), 기믹 셈판은 따로(피로·열기 스택 강제 + 픽업·사직서 스폰). 도구가 없으면 「재현이 먼저다」(CLAUDE.md 버그 수정 절차 1)가 이 영역에서 집행 불가다.
10. **계측 항목은 「왜 안 걸렸나」의 원인 수만큼 찍는다.** 효과가 안 먹는 경로가 넷이다 — ⑴ 대상 자격 거절(거점·보스 면역) ⑵ 진영·통행층 필터 ⑶ 반경 밖 ⑷ 병합 키 충돌(같은 슬롯을 덮음). 넷이 화면에서 구분되지 않으므로 메뉴가 **대상마다 네 답을 전부** 찍는다(`CoreDetectionProbeMenu` 가 감지의 네 원인에 대해 한 것과 같은 형태).

## 파이프라인 커버리지

5a 가 유닛·투사체 행을 열었고, 여기서 효과 아키타입 행을 연다(전면 재작성은 unit 8).

| 정거장 | 존 장판 | 길막 | 픽업·사직서 | 상태 FX·오라 |
|---|---|---|---|---|
| 저작 | `HazardSO.visualPrefab` | `BlockingHazardSO` | 기믹 SO 프리팹 | `StatusFxConfig`(신설) · `DcVisualConfig` |
| 정의표 | `HazardDef` + `MatchViewAssets` 줄 번호 | `BlockingHazardDef` | `GimmickDef` | N/A — 뷰 설정 SO 가 든다(코어는 그림을 모른다) |
| 생성 | `HazardSpawned` → `CoreHazardViewPool` | `UnitSpawned`(BlockingHazard) → 같은 풀 | `PickupSpawned`·`ResignationDropped` | `CcApplied`·`DotApplied`·`StackChanged`·`ModifierApplied` |
| 매 프레임 | 바닥 정렬(유닛 앞) | 오버헤드 게이지 동기 | 위치 고정 | 숙주 추종(유닛 뒤) |
| 소멸 | `HazardDestroyed` | `UnitDestroyed` | `PickupTaken` · 임계 소모 | `CcCleared`·`ModifierRevoked` + 숙주 `UnitDestroyed` |
| 소리 | N/A — 이 unit 은 전투음을 늘리지 않는다(5c 의 3종 그대로) | N/A | N/A | N/A |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `ReconcileStatusFx` 의 **매 프레임 월드 폴링**(151줄) | 사건 구독 + 소멸 회수로 접힌다. 자가 치유는 **경고**다 | 제거(계약 7) |
| 뷰가 쏘던 전투 규칙(슬램 발사 · 착지 해결) | 5a 가 이미 제거했다 — 코어가 낸다 | 제거(선행) |
| 오버헤드 **부착 카드 줄** | 부착 사건이 unit 7 이다. 빈 슬롯을 먼저 만들지 않는다 | 보류 · unit 7 |
| 회오리·포탈 **장 비주얼** | 개체는 6b 가 세웠지만 **까는 자가 없다**(unit 7). 사건이 0건인 풀은 만들지 않는다 | 보류 · unit 7 |
| `DcAuraVisualPool` 의 **매 프레임 생존 폴링** | Entities 누수 3곳 중 하나. 키 치환이 아니라 **계약**(모든 소멸은 소멸 사건을 낸다)으로 푼다 — unit 8 이 같은 규율로 나머지 둘을 푼다 | 완료(계약 7) |
| **공격음을 START 로 옮기기** | 5c 가 「플레이에서 어색하면 unit 6」으로 넘긴 항목이다. **어색하다는 관측이 아직 없다** — 관측 없이 `AttackStarted` 사건을 열면 소비처 하나짜리 사건이 append 된다. 6c 플레이에서 박자가 실제로 어긋나면 그때 연다 | 보류 · 관측 대기 |
| 상태 FX 의 **옛 우선순위 표**(한 몸에 표식 여러 개일 때) | 옛 코드가 들고 있던 암묵 순서를 그대로 베끼지 않는다 — **어느 상태가 이겨 보이나**는 플레이어가 겪는 규칙이라 6c 플레이에서 확인한 뒤 데이터로 굳힌다 | 보류 · 플레이 확인 |
| 벤더 VFX 프리팹 **신규 저작** | 이 unit 은 **배선**이다. 옛 씬이 쓰던 프리팹을 그대로 가리킨다 — 새 룩은 별도 spec | 제거(범위 밖) |
| 옛 **타이밍 VFX 큐**(`_pendingHitVfx` · `hitDelaySec` 지연) | 옛 시각 사건은 공격 **START** 에 나와 RESOLVE 까지 미뤘다. 새 `AttackResolved` 는 **그 자체가 RESOLVE** 라 미룰 것이 없다(미루면 타격보다 늦게 터진다). 공격 **애니**가 RESOLVE 에 시작하는 것은 위 「공격음을 START 로」와 같은 축이다 | 제거(사건이 이미 그 시점) |
| **착탄 예고 표식**(「추가」 절 1) | spec 전제가 틀렸다 — 예고 반경은 `ProjectileData` 필드가 **아니라** 스킬 intent 값이다(옛 `EcsSkillContext.cs:1121` `telegraphTileRange = intent.Telegraph ? intent.TileRange : 0`). 탄 정의표에 옮길 저작이 없고, 생산자(운석·스킬 조준)가 전부 unit 7 이다. 반경 없이 칠하면 규칙을 지어낸다 | 보류 · unit 7(7a intent) |
| **어그로 표식**(`StatusFxKind.Aggro`) | 켜는 사건(`AggroAcquired`)은 있는데 **풀리는 사건이 없다**. 옛 표식은 `Aggroed` 컴포넌트 보유를 매 프레임 폴링했다 — 풀림을 폴링으로 되살리면 이 unit 이 없애는 모양이 돌아온다 | **완료**(6c 후속 · 리드 결정) — 코어 `AggroReleased`(54) 구독으로 끈다. 「고친 것」 행 |
| 라스트런 표식의 **닫힘 사건** | 켜짐은 `PickupTaken`, 닫힘(crash)은 사건이 없다. 임시 다리 = 같은 몸의 스탯 회수 사건 + 초당 1회 정본 플래그(`LastRunActive`) 확인 | **완료**(6c 후속 · 리드 결정) — 코어 `LastRunEnded`(55) 구독으로 끈다. 임시 다리 철거. 「고친 것」 행 |
| **살찌운 제물 표식**(`Marked`) | 표식 등록부의 주인이 unit 7(저주 카드)이다 | 보류 · unit 7 |
| **실드 파열 원샷 VFX** | 옛 코드에 **없다**(옛 파열 드레인은 페이로드 실행·카드 펄스뿐). 파열의 그림은 오버헤드 실드 칸이 0 이 되는 것이다. 새 원샷은 신규 저작 | 제거(범위 밖) |
| 드래곤 **화염 브레스** VFX | 6c 후속 3 이 `AttackResolved` 에 축·도형·사거리를 실었지만 **브레스는 그 값으로 그릴 수 없다** — 옛 브레스의 콘은 공격 도형이 아니라 **드림캐쳐 메커닉 슬롯**(`AreaBreath` = 21, `ConeBreathSkill`)의 `coneHalfAngleDeg`·`tileRange` 였다(옛 `AttackSystem.cs:1942-1971` · `BattleBridge.cs:4768-4776`). 드래곤 저작은 공격 도형 전방위 · 사거리 2칸, 브레스 슬롯 반각 50° · 3칸(`Enemy_Dragon.asset`) — 공격 사건으로 그리면 **틀린 콘**이 나온다. 새 코어에는 `ConeBreath` 스킬이 아직 없다(7a) | 보류 · unit 7a(`ConeBreath` 발화 사건이 콘 스냅샷을 싣는다) — **7a 이월 확정**(리드 2026-09-24: 플레이어 규칙이 아니라 «어느 사건에서 그리나»의 배선 문제) |
| 길막 **절차 폴백 VFX**(떨어지는 돌·먼지) · 픽업 플레이스홀더의 **발광** | 파티클 수치가 전부 코드 리터럴이고 머티리얼이 `Shader.Find` 였다(제약 6 · 추가 제약). 프리팹이 비면 경고 + 그림 없음, 플레이스홀더는 `RuntimeMaterialFactory.CreateOpaque` | 제거 |
| 옛 장판 프리팹의 **자기 수명 시계**(`HazardVisualLifetime`) | 실시간으로 자기를 파괴해 정지·슬로모에서 규칙보다 먼저 사라졌다. 스폰 시 떼고 소멸 사건만 지운다(Play 스모크에서 실측) | 제거(계약 7) |
| 배치 **폴백 펄스**(`PlayFallbackDeploymentPulse`) | 배치 모션은 5a 가 `DefenderActivated` → 뷰 `PlayDeploy` 로 이미 옮겼다 | 제거(선행) |
| `DcVisualConfig` **개통** | 그 자산의 유일한 값(`procImpactMinIntervalSec`)은 **드림캐쳐 발동 임팩트**의 코얼레스 간격이고 발동 사건이 unit 7 이다. 6c 에서 읽으면 그 값을 다른 뜻으로 쓰게 된다 | 보류 · unit 7 |
| 방패 걸린 마음의 **부수 피해 제외 소비처**(README 6c 행) | 코어 변경이라 6c 본편의 배정(뷰·도구) 밖이었다 | **완료**(6c 후속 4 · 리드 결정) — 「고친 것」 행 |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 왜 |
|---|---|
| `CoreEvent.ProjectileHit` 가 **광역 반경(`AreaTiles`) · 페이로드 종류(`Payload`) · 탄 정의 줄(`DefIndex`)** 을 싣는다(필드 append — 번호 무변) | 「추가」 절 2. 옛 `ProjectileHitEvent` 가 값으로 나르던 둘이 빠져 광역 폭발 라우팅이 불가능했다. 착탄 뒤 탄은 곧 소멸하므로 되묻기가 아니라 스냅샷이다(계약 7). 반경은 `TileAoe` 의 `ImpactTileRange` 만(옛과 같다). **트레이스 무변**(채널 여섯 칸) → 골든 11종 무변(EditMode 코어 526/526) |
| `Effects/ModifierAuraClassifier` 코어 salvage(순수 함수) + `HasAnyFromOrigin` | 구현 2. 옛 것은 `NativeArray<StatModifierSlot>` 를 받았다 → `IReadOnlyList<ModifierSlot>` |
| `MatchViewAssets` += 장판·길막 목록, `BoardEffectDefinitionBuilder.Fill` 이 **같은 순회**로 채운다 | 뷰가 `DefIndex` 로 `HazardSO.visualPrefab`·`BlockingHazardSO.visualPrefab` 을 되찾는다. 뷰 쪽에서 다시 모으면 두 벌이 갈린다(5a 규율) |
| `BattleDriver._hazards`·`_extraBlockers` 저작 칸(씬: 불·독·얼음 3×3 · 바위 3×3) | 새 코어는 **정의표 줄**로만 깐다. 줄이 없으면 디버그 도구가 깔 것이 없다. 까는 자는 unit 7 이라 라이브 판은 무변 — 단 **라이브 `configHash` 는 바뀐다**(장판·길막이 canonical 에 실린다). 골든은 in-code 코퍼스라 무변 |
| `CoreUnitViewPool` 이 `UnitKind.BlockingHazard` 를 **건너뛴다** + `TryResolveViewPosition` | 5a 풀은 길막 스폰을 받아 그 줄 번호(`BlockingHazards`)를 **유닛 표로** 읽었다(엉뚱한 스켈레톤·쿼드). 길막은 해저드 풀의 것이다. 부팅 스모크의 유닛 수 대조도 길막을 뺀다 |
| `CoreProjectileViewPool` — 착탄 VFX 가 `e.DefIndex` 를 읽고, **임팩트 소켓**(구현 5)을 채운다 | 5a 는 탄 개체를 되물었고 소켓을 0 으로 흘렸다. 소켓 = 조준 임자의 저작 `impactSocketHeight`, 흡수 구간 2칸(옛 상수) |
| `CoreUnitOverheadUiLayer` — 실드 비율 · 스택 아이콘 · 길막 게이지 | 구현 3(5a 이월). 실드는 진영 무관(옛 브리지의 적 분기 리터럴 0 정정이 그대로 산다) |
| `CoreSelectionPanel.ReadoutOf` — 실효 값 | 최대 체력 = `unit.MaxHealth`(**재곱 없음**) · 공격력 × `DamageMul` · 초당 공격 × `AttackSpeedMul`. 조건부(`DamageVsCcMul`·최전방·바운스) 제외 |
| 총구 캐스트가 **탄마다** 난다(`ProjectileSpawned`) | 옛 것은 공격 시작 사건에 한 번이라 연발(머신건 10연발)은 첫 발만 섬광이었다. 리드 지시(총구 = `ProjectileSpawned`)의 결과이고 **그림만** 바뀐다. 하늘에서 떨어지는 탄은 캐스트가 없다 |
| 공격 빔 세션 키 = **쏘는 쪽** | spec 문면 「키 = 맞는 쪽」은 배치 스킬의 **대상별 조사**(unit 7) 얘기다(옛 주석). 공격 빔을 맞는 쪽으로 키잉하면 두 버스터즈가 한 적을 쏠 때 빔이 하나로 접힌다. `Open(key, …)` 는 키를 호출자가 고른다 |
| 배치 링 펄스 = **착지** 프레임 | 사건 `Placed` 는 드롭 순간이고 비행이 그 뒤다. 옛 연출은 착지에 났다 → 비행 키가 사라지는 프레임에 난다(비행 없는 경로는 다음 프레임). 흔들기는 `DefenderActivated`(옛: 배치 스킬 발화 시점) |
| `ViewOrder` += `Board 15` · `Effect 35` · `Status 45` | 구현 7. `CoreViewOrderTests` 가 씬 순서를 뒤집어 확인 |
| **어그로 풀림 = 사건**(6c 후속 · 리드 결정 1) — `CoreEvent.AggroReleased`(**54**, 트레이스 **52**, `Arg` = `AggroReleaseReason`{`Expired` · `GuardianGone` · `Rebuilt`}). 해제 자리 셋(시한 · 가디언 부재 · 추격판 무효화)이 `FieldPrepPhase.Release` **한 함수**를 부르고 그 함수가 낸다. 상태 표식 풀이 `AggroAcquired`/`AggroReleased` 로 어그로 표식을 켜고 끈다 | 이식 제외 「어그로 표식」 행. 옛 것은 `Aggroed` 보유를 매 프레임 폴링했다(`BattleBridge.cs:3525`). 적 자신의 소멸은 풀림이 아니다(`UnitDestroyed` 가 거둔다) · 도발 갈아타기도 아니다(획득이 한 번 더 온다). ⚠ 가디언이 빠지면 그 몸(장애물)이 풀려 **같은 틱의 추격판 무효화가 먼저** 히트 어그로를 푼다 — 사유가 `Rebuilt` 인 것이 그 순서의 증언이다(`DetectionRulesTests` 3건 · PlayMode `CoreEffectViewTests` 어그로 표식 1건). 골든 코퍼스는 어그로 획득 0건이라 무변 |
| **라스트런 닫힘 = 사건**(6c 후속 · 리드 결정 2) — `CoreEvent.LastRunEnded`(**55**, 트레이스 **53**, `Arg` = `LastRunEndReason`{`Crash` · `Death` · `Retire` · `Removed`}). 닫히는 문 둘(시간 끝 `CrashLastRun` · 중단 정책 `InterruptProgress`)이 `BattleWorld` 에 있고 사건은 거기 한 곳에서 난다. `ProgressiveStates.Interrupt` 는 「이 중단이 창을 닫았나」를 **전후 값 비교**로 돌려준다(정책 표를 두 번 적지 않는다). 퇴근은 제거 **앞**에 `Retire` 로 닫고, 그 밖의 제거(유출 등)는 `Destroy` 가 `Removed` 로 닫는다 | 이식 제외 「라스트런 닫힘」 행. 6c 의 임시 다리(스탯 회수 계기 + 초당 `LastRunActive` 확인)를 철거했다 — 표식은 레드불 `PickupTaken` 에 켜지고 이 사건에 꺼진다. 퇴근 경로는 전에는 중단 정책을 **안 불렀다**(`Reset` 이 대신 지웠다) — 규칙 결과는 같고, 사유가 「퇴근」으로 남는 것만 달라졌다(`PickupTests` 4건 · PlayMode `CoreEffectViewTests` 라스트런 1건). 골든 코퍼스는 픽업 0건이라 무변 |
| **`AttackResolved` 가 공격의 축·도형·사거리를 싣는다**(6c 후속 · 리드 결정 3) — 필드 append `AttackDir`(월드 XZ 정규화) · `AttackShape`(bake 형 그대로 — 반각은 `sinHalf`/`cosHalf`) · `AttackRange`(런타임 칸). 근접은 부가 타격을 고른 **그 축**, 평타 탄은 조준 방향. 폭탄·소환은 기본값. 참격 자국(`CoreVfxSpawner`)이 이 스냅샷으로만 그린다 | 5a~6c 의 참격은 공격자를 **되물어** `Attack.Shape`·`Attack.Range` 를 읽었다(계약 4·7 위반 — 옛 브리지 `BattleBridge.cs:4869-4884` 의 드레인 시점 `AttackState` 읽기를 옮긴 모양). 번호 무변 · **트레이스 무변**(채널 여섯 칸) → 골든 무변. `CombatRulesTests.공격_성사_사건은_판정한_도형_축_사거리를_값으로_싣는다`. 브레스 라우팅은 안 했다(위 이식 제외) |
| **방패 걸린 마음의 부수 피해 제외**(6c 후속 · 리드 결정 4 · unit 4 이월) — `HeartMeter.DrainTowers` 가 방패(`CoreShielded` 관찰) 동안 마음 타워 인박스의 피해·회복을 **버린다**(비운다 · 숫자 없음) | 옛 소비처 5(`DamageApplicationSystem.cs:139-144`)를 **그 모양 그대로** 옮겼다 — 생산자 쪽이 아니라 **피해 적용 한 곳**. 리드 지시는 「광역 생산자가 `EffectEligibility` 를 본다」였지만 옛 `CoreShielded.cs:34-38` 이 「`ProjectileHitSystem` 에 중복 필터를 넣지 말 것 — 한 곳에서 떨어뜨려야 새 피해 경로가 자동으로 덮인다」를 명시해 옛 것을 따랐다. ⚠ 증상 「옆에서 터진 광역이 마음을 깎는다」는 **이미 초록이었다** — 새 코어의 착탄·장판 피해자 선정이 `IsTargetable`(방패 = `Untargetable`)로 이미 거른다. 빨간 것은 조준을 안 지나는 생산자(인박스 직접 기입 — unit 7 의 스킬 페이로드 자리)였다. `MatchStructureTests` 광역 증상 1건(대조군 포함) · 백스톱 1건 |
| **배치 드래그 중 공격 도형 가이드**(6c 후속 · 이식 누락 — 플레이 1차 표 `impl-shape-guide` 행) — `CoreMapOverlay` ⑤. 방향 유닛(부채꼴·띠)을 끄는 동안 사거리 안 **최근접** 적 쪽으로 빨간 도형(채움 + 테 두 장)을 깐다. 적이 없거나 전방위면 없다 · 드래그를 놓으면 내려간다 | 옛 `TilemapMapView.SetShapeGuide`(`:878-925`) + 호출부 `BattleBridge.cs:8141-8183` 의 이식. 치수는 전부 정의표에서: 원점 = 발밑(링 중심, 옛 `:8178`) · 길이 = `range + 내 몸`(링과 같은 값, 옛 `:8180` — **대상 몸은 안 그린다**, 제약 13 · directional-attack-shape/7:52) · 각 = bake 역산 · 반폭 = bake + 테 폭 하한(옛 `TilemapMapView.cs:900`) — 참격 자국과 같은 역산 `CoreVfxSpawner.ShapeMarkOf` 를 지난다 · Omni 제외(옛 `:8176`) · 방향 0 이면 숨김(옛 `:884`). 대상 = 마크와 **같은 후보 루프**에서 코어 `NearestTargeting.RanksBefore`(최근접 → 낮은 `SimId`, 옛 `:8172`) — 뷰는 도달을 재지 않는다. 색 = `TileSetData.rangeTargetMarkColor`(옛 `:919`) · 알파 = 옛 코드 상수 0.22/0.85 를 `TileSetData.rangeShapeGuide{Fill,Rim}Alpha` 저작으로 올렸다 · 머티리얼 `RuntimeMaterialFactory.CreateTransparent` · 정렬 `PlacementShapeGuideOrder`. 씬 배선 변경 없음(오버레이의 기존 `_tileSet`). PlayMode `CoreShapeGuideTests` 1건. 후보 집합은 마크의 것을 그대로 쓴다(그 자격은 아래 행) |
| **사정권 마크 = 옛 규칙**(6c 후속 · 5b 이식 드리프트 · 리드 결정) — ① 후보 자격 ② 색 ③ 무효 배치 숨김 | ① 5b 는 「적 진영 + 생존」만 봤다 → 지상 전용 근접(말파이트)을 끌면 **못 때리는 비행 적**에 마크가 켜지고 도형 가이드도 그쪽을 봤다. 옛 `BattleBridge.cs:8138-8158` 그대로 되돌렸다: 마스크 `TargetDefaults.ResolveDefender`(적 거점 포함) · 통행 층 `LayerBits.CanTarget`(대상 층 읽기 = 코어 후보 스냅샷과 같은 식) · 제외 `Unit.IsTargetable`(도약 이탈의 후계) · 지원형(아군 마스크)은 마크·가이드 없음(옛 `:8062`). 전부 코어 진입점 **호출만**. ② 오버레이 `_markColor`(주황 리터럴, 제약 6 위반) 제거 → `TileSetData.rangeTargetMarkColor`(옛 `TilemapMapView.cs:766`) — 가이드와 한 빨강. ③ 배치가 무효면 마크를 숨긴다(옛 `ApplyTargetMarkVisibility` `:811-816` — 고스트 빨강과 시간으로 가른다). 가이드는 그 스위치를 안 탄다(옛 것과 같다). PlayMode `CoreShapeGuideTests` 「지상 전용 말파이트 × 더 가까운 비행 적」 1건. ⚠ 옛 거점 마크의 **점유 사각** 룩(`TilemapMapView.cs:776-800`)은 이식하지 않았다 — 거점도 유닛과 같은 고리 표식이다(후속 후보) |

## 완료 기준

- [x] **새 PlayMode lane 초록 44/44** (2026-09-24 · 40 + 새 4) — `CoreEffectViewTests`: 사건 1건 → 뷰 1개 · 소멸 사건 → 회수 · 숙주 소멸 → 붙어 있던 표식 전부 회수 · `ViewOrder` 로 정렬(씬 순서를 뒤집어 확인). 부팅 스모크의 「뷰 수 = 코어 개체 수」 검사에 **해저드·픽업·사직서**를 추가한다. ⚠ 오라 풀의 PlayMode 단언은 없다 — 드림캐쳐 출처 스탯의 생산자가 unit 7 이라 코어의 진짜 문으로 켤 길이 없다. 판정은 EditMode `ModifierAuraClassifierTests`(7건)가 증언한다.
- [x] 뷰 코드에 `Unity.Entities` **0건** · 판정 코드 **0건**(반경·자격 재계산 없음 — grep 으로 확인).
- [x] `ledgers/tools.md` **6·7·9행 닫힘**(존 해저드 · 길막 · 피로). 세 행의 처분이 「조각 C 앞」이었고 여기서 끝난다 — 남는 것은 조각 D 앞의 2행(순찰 10 · 재배치 11)뿐이다.
- [x] `ledgers/bridge-methods.md` 미정 **46 → 44**: 닫히는 것은 **2행**(`ReconcileStatusFx/0` · `HostBodyRadiusOf/1`)이다. 나머지 뷰 풀 행들은 이미 「뷰 풀」로 배정돼 있어 **주인 확정이지 잔량 감소가 아니다**. **잔량 44 를 README 상태 라인에 숫자로 적는다**(진행 규칙 — unit 7 이 그 44 를 닫는 것이 조각 E 진입 조건).
- [x] `python3 tools/battle-core-rebuild/check_ledgers.py` **exit 0**(미정 44).
- [x] `core-reviewer` APPROVE(2026-09-24 · `eca3e47e7` finding 0 — 매니저 0 · 판정 재계산 0 · `Shader.Find` 0 · 빔 키 = 쏘는 쪽이 옛 코드와 일치. 후속 5커밋 `405bf29f3`~`ef3606e4c` 보충 리뷰도 APPROVE finding 0 — 사건 54·55 append-only · 해제 함수 하나 · 퇴근 Retire 사유 1건 · 방패 백스톱 = 옛 `DamageApplicationSystem.cs:139-144` 동일 · 뷰 되묻기 0).

확인 2026-09-24 — 리드 export 재검증(`1a798bb8e`): build 0 · test 525 · Check 0 · 미정 44. Unity EditMode 코어+Assets 759/761(선행 2) · PlayMode 코어 46/46 · 골든 무변.
- [ ] **사용자 플레이 — 조각 C 의 질문**: *「효과가 걸린 게 보이나, 그리고 걸린 만큼 세기가 달라진 게 느껴지나」*. 구체 확인 6: ⑴ 감속·기절·수면·출혈·화상 표식이 각각 구분된다 ⑵ ~~실드가 부여되고 깨지는 순간이 보인다~~ → **unit 7 로 이월**(2026-09-24 플레이 1차: 실드 생산자는 전부 스킬·카드 — `GrantShield`3 은 7a, 카드 실드는 7b. 조각 C 에는 뷰만 있고 부여자가 없어 이 항목은 여기서 확인 불가) ⑶ 장판이 깔리고 밟은 적이 탄다 ⑷ 길막이 체력 바를 달고 부서진다 ⑸ 배치 순간 링과 흔들림이 난다(**배치 스킬 자체**는 7a — 이 항목은 연출만) ⑹ 선택 패널에 **델타 칩**이 뜬다. ⚠ 같이 볼 것 둘 — **6a 구현 15**(투사체 디버프 곱누적)는 사용자가 **(a) 고친다**로 답했고 6a2 가 구현했다(`732b5a00`) — 킨들러류가 눈에 띄게 약해졌는지 이 플레이에서 본다. **6b2 구현 1**(픽업 소비가 칸 → 제약 13 자)은 **더 잘 먹히게** 바뀐다. 반영 시점(버프가 몇 틱 뒤에 드나)은 **안 바뀌었다** — rev 3 §4 가 그 축을 「변경 없음」으로 닫았고 새 코어도 같은 단계 순서를 쓴다.
- [x] 「아직 안 보이는 것」 표를 unit 7 에 넘긴다(아래 표): 부착 카드 줄 · 회오리/포탈 장 · 픽업 스폰 · 사직서 드랍 · 열기/피로 누적 · 호접몽 · 운석 barrage.

### 아직 안 보이는 것 → unit 7

사용자 플레이(조각 C)에서 **안 보이는 것이 정상**인 목록이다. 전부 「놓는 자·계기·사건」이 unit 7 에 있다.

| 안 보이는 것 | 왜 | 뷰 쪽 준비 |
|---|---|---|
| 오버헤드 **부착 카드 줄** | 부착 사건(7b) | 없음(빈 슬롯 금지) |
| 회오리·포탈 **장 비주얼** | 장을 까는 자(7a·7d) | 없음 — `FieldSpawned` 소비처 0 |
| **픽업 스폰** · **사직서 드랍** | 주기 바인딩·사망 seam(7d). 기본 모드는 기믹 0 이라 디버그 커맨드도 `GimmickInactive` | `CorePickupViewPool`·`CoreResignationViewPool` 섬 |
| **열기·피로 누적** | 기믹 per-unit 타이머(7d) | 오버헤드 스택 아이콘 섬 |
| **호접몽** · **운석 barrage** | 7d | 광역 착탄 버스트 섬(`ProjectileHit.AreaTiles`) |
| **착탄 예고 표식** | 예고 반경이 스킬 intent 값(7a) | 없음(반경 없이 칠하면 규칙을 지어낸다) |
| **강화 오라**(드림캐쳐 출처 스탯) · 카드 페이로드 오라 | 카드 부착·시전(7b) | `CoreDcAuraVisualPool` 섬(판정 = 코어 순수 함수) |
| **살찌운 제물 표식** | 저주 카드(7b) | 등록부 `Marked` 줄만 |
| 드래곤 브레스 | 콘이 공격 도형이 아니라 `ConeBreath` 스킬 슬롯 값이다(위 이식 제외) — 스킬이 7a | 없음 |


## 추가 (2026-09-24 투사체 이식 감사)

- **착탄 예고 표식**(`telegraphTileRange` — 옛 `ProjectileSpawnRequest.cs:21` · `BattleBridge.cs:5456`, 낙하탄·운석이 떨어질 칸을 미리 칠하는 뷰)은 어느 unit 문서에도 없었다 → 이 unit 의 뷰 풀 목록에 넣는다. 사건은 `ProjectileSpawned` 가 이미 자리(`SiteTarget`)를 나르므로 새 사건은 없다. 예고 반경은 탄 정의표(`ProjectileDef`)의 값이다(제약 6). ⚠ **정정**: 탄 정의표 값이 아니라 스킬 intent 값이다 — 위 이식 제외 「착탄 예고 표식」 행 · 7a 이월 메모.
- 착탄 사건의 **광역 반경·페이로드 종류**(옛 `ProjectileHitEvent.cs:25`)가 `CoreEvent.ProjectileHit` 에 없어 광역 폭발 뷰 라우팅이 불가능하다 → 6c 가 사건 페이로드에 반경·페이로드 종류를 추가한다(값 스냅샷, 계약 7).

## 플레이 1차 (2026-09-24) — 사용자 보고

| 보고 | 판정 | 처분 |
|---|---|---|
| 실드셔틀이 실드를 안 준다 | **정상(미도달)** — 실드 부여는 스킬(`Ability_Shield_ShieldShuttle` → `GrantShield`)이라 7a. 조각 C 는 실드 **뷰**만 세웠다 | 확인 항목 ⑵ 를 unit 7 로 이월 |
| 말파이트·이쑤시개 공격 가이드와 실제 범위가 다르다(실제가 더 큼) | **옛 게임과 동일** — 모든 그림(참격 자국 `CoreVfxSpawner.cs:171` = 옛 `BattleBridge.cs:4883` · 사거리 링 `CoreMapOverlay.cs:269` = 옛 `:8180`)이 «범위 + 자기 몸»을 그리고, 판정은 «+ 대상 몸»(제약 13). `directional-attack-shape/7:52` 「대상 몸은 그리지 않는다」가 의도. 실측(대상 몸 0.25): 말파이트 자국 5.0 vs 도달 5.25 · 이쑤시개 폭 1.0 vs 1.5(띠가 좁아 가장 크게 체감). 판정 함수는 옛 것과 diff 공백뿐 | **사용자 결정**: (a) 그대로 (b) 그림에 표준 적 몸 0.25 를 더한다 (c) 판정을 바꾼다(제약 13 개정) |
| (조사 중 발견) 배치 드래그 중 **빨강 도형 가이드**(directional-attack-shape unit 6 `TilemapMapView.SetShapeGuide`)가 새 오버레이에 없다 — 원 링·사정권 표식만 있다. 어느 이식 제외 표에도 없는 **누락** | 이식(6c 후속) — 같은 «범위 + 자기 몸» 양으로 · **완료**(「고친 것」 행) | `impl-shape-guide` |
| 배치 스킬이 안 난다 | **정상(미도달)** — 배치 스킬은 `OnPlace` → `Periodic` seam 바인딩(7a). 조각 C 는 배치 **연출**(링·흔들림)만 | ⑸ 문면 정정 |
