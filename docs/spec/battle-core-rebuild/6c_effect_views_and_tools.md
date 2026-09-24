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

## 고친 것 (기존 코어·Unity 층 변경)

*(구현 중 채운다.)*

## 완료 기준

- [ ] **새 PlayMode lane 초록** — `CoreEffectViewTests`: 사건 1건 → 뷰 1개 · 소멸 사건 → 회수 · 숙주 소멸 → 붙어 있던 표식 전부 회수 · `ViewOrder` 로 정렬(씬 순서를 뒤집어 확인). 부팅 스모크의 「뷰 수 = 코어 개체 수」 검사에 **해저드·픽업·사직서**를 추가한다.
- [ ] 뷰 코드에 `Unity.Entities` **0건** · 판정 코드 **0건**(반경·자격 재계산 없음 — grep 으로 확인).
- [ ] `ledgers/tools.md` **6·7·9행 닫힘**(존 해저드 · 길막 · 피로). 세 행의 처분이 「조각 C 앞」이었고 여기서 끝난다 — 남는 것은 조각 D 앞의 2행(순찰 10 · 재배치 11)뿐이다.
- [ ] `ledgers/bridge-methods.md` 미정 **46 → 44**: 닫히는 것은 **2행**(`ReconcileStatusFx/0` · `HostBodyRadiusOf/1`)이다. 나머지 뷰 풀 행들은 이미 「뷰 풀」로 배정돼 있어 **주인 확정이지 잔량 감소가 아니다**. **잔량 44 를 README 상태 라인에 숫자로 적는다**(진행 규칙 — unit 7 이 그 44 를 닫는 것이 조각 E 진입 조건).
- [ ] `python3 Tools/battle-core-rebuild/check_ledgers.py` **exit 0**.
- [ ] `core-reviewer` APPROVE(Unity 층 포함: 매니저·브리지·컨트롤러 이름 0 · 판정 이전 0 · `Shader.Find` 0).
- [ ] **사용자 플레이 — 조각 C 의 질문**: *「효과가 걸린 게 보이나, 그리고 걸린 만큼 세기가 달라진 게 느껴지나」*. 구체 확인 6: ⑴ 감속·기절·수면·출혈·화상 표식이 각각 구분된다 ⑵ 실드가 부여되고 깨지는 순간이 보인다 ⑶ 장판이 깔리고 밟은 적이 탄다 ⑷ 길막이 체력 바를 달고 부서진다 ⑸ 배치 순간 링과 흔들림이 난다 ⑹ 선택 패널에 **델타 칩**이 뜬다. ⚠ 같이 볼 것 둘 — **6a 구현 15**(투사체 디버프 곱누적)는 **사용자 결정 대기**이고 「고친다」로 답이 오면 킨들러류가 눈에 띄게 약해진다. **6b2 구현 1**(픽업 소비가 칸 → 제약 13 자)은 **더 잘 먹히게** 바뀐다. 반영 시점(버프가 몇 틱 뒤에 드나)은 **안 바뀌었다** — rev 3 §4 가 그 축을 「변경 없음」으로 닫았고 새 코어도 같은 단계 순서를 쓴다.
- [ ] 「아직 안 보이는 것」 표를 unit 7 에 넘긴다: 부착 카드 줄 · 회오리/포탈 장 · 픽업 스폰 · 사직서 드랍 · 열기/피로 누적 · 호접몽 · 운석 barrage.


## 추가 (2026-09-24 투사체 이식 감사)

- **착탄 예고 표식**(`telegraphTileRange` — 옛 `ProjectileSpawnRequest.cs:21` · `BattleBridge.cs:5456`, 낙하탄·운석이 떨어질 칸을 미리 칠하는 뷰)은 어느 unit 문서에도 없었다 → 이 unit 의 뷰 풀 목록에 넣는다. 사건은 `ProjectileSpawned` 가 이미 자리(`SiteTarget`)를 나르므로 새 사건은 없다. 예고 반경은 탄 정의표(`ProjectileDef`)의 값이다(제약 6).
- 착탄 사건의 **광역 반경·페이로드 종류**(옛 `ProjectileHitEvent.cs:25`)가 `CoreEvent.ProjectileHit` 에 없어 광역 폭발 뷰 라우팅이 불가능하다 → 6c 가 사건 페이로드에 반경·페이로드 종류를 추가한다(값 스냅샷, 계약 7).

- **발사 순간 총구(캐스트) VFX 미배선**(감사 LOW): 옛 `BattleBridge.cs:5014-5027 TrySpawnCastVfx → PlayCast(castPrefab, anchor, dir, castVfxLifetime)`(호출처 `:4923`). 새 `CoreProjectileViewPool.cs:514 PlayCast` 는 복사돼 있으나 호출자 0 · `castPrefab`·`castVfxLifetime` 을 읽는 줄 0 → 방어유닛 11종·적 6종의 총구 섬광이 안 나온다. 규칙 무변·연출만 누락. **이 unit 이 `ProjectileSpawned` 구독으로 배선**한다(값은 유닛 뷰 데이터 SO).
