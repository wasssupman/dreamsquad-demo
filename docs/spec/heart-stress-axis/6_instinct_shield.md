# 6 — 본능이 마음의 방패다

## 목적

**맵 위의 모든 (방어측) 본능이 파괴된 뒤에야 마음이 피해를 받는다**(사용자 확정 2026-08-23).

본능이 서 있는 동안 마음은 **조준 대상도 아니고 갈 곳도 아니다.** 마지막 본능이 무너지는
순간 둘 다 열리고, 그때부터 스트레스가 오르기 시작한다.

## 변경 대상

- `Battle/Units/CoreShielded.cs` **(신설)** — 태그. 소비처 명단을 이 파일이 소유한다
- `Battle/Combat/AttackSystem.cs` · `Battle/Combat/EnemyAiStateSystem.cs` — 후보 쿼리 배제
- `Battle/Movement/StructureDestinationSystem.cs` — **경로** 후보 배제
- `Bridge/BattleBridge.cs` — 태그 토글 · 돌격형 직격 게이트 · 스폰 예고선 배제
- `Tests/EditMode/StructureSpawnAndBreachTests.cs` · `StructureDestinationTests.cs`

## 구현

**1. 「무적」이 아니라 「후보 제외」다.** 피해만 막으면 적이 마음 앞에 붙어 아무 일도 안 일어나는
그림이 되고, 플레이어에겐 버그로 읽힌다. 태그가 하는 일은 **후보 목록에서 빼는 것**이고
적은 애초에 본능·방어유닛을 조준한다.

**2. ⚠ 조준만 막으면 본능은 «벽»이 아니라 «타이머»가 된다.** 조준을 막아도 적은 여전히 마음으로
행군하고, 공성 게이트 때문에 마음 셀에서 멈춰 선다 — 때리진 못하고 대기하다 방패가 깨지는
순간 일제히 친다. 그래서 **경로**도 막아야 한다.

**3. 경로 차단은 이미 있던 기계에 한 줄이다.** `StructureDestinationSystem`(instinct-content
unit 3)이 이미 «내가 팰 수 있는 거점 중 가장 가까운 것»을 골라 `StructureDestination` 을
붙인다. 그 **후보 수집**에서 방패 걸린 마음을 빼면:
- 가장 가까운 **본능**이 뽑히고 → 적이 그리로 걸어간다
- 본능이 없거나 마스크가 본능을 안 보면 `pick < 0` → 컴포넌트 제거 → **골 폴백 = 현행**

선택 함수(`StructureChoice`)는 **한 줄도 안 바뀐다.** 후보 집합만 바뀐다.

**4. 소비처가 다섯이다.** 하나라도 빠지면 규칙이 샌다 — 명단은 `CoreShielded.cs` 가 소유한다.
조준 2 · 경로 1 · 예고선 1 + 돌격형 도달(브리지 게이트). 돌격형은 조준이 아니라 **도달**로
오므로 쿼리로는 못 막고 `DrainGoalEvents` 에서 따로 막는다.

**5. ⚠ 스폰 예고선도 같이 막는다.** 예고선은 이 선택의 **사본**을 갖고 있다(같은
`StructureChoice`). 안 고치면 **예고선은 마음으로 가는 길을 그리는데 적은 본능으로 간다.**

**6. ⚠ 태그 토글은 프레임 순서에 의존한다.** `MonoBehaviour.Update`(브리지) →
`BattleSimGroup`(시스템) 순서라 시스템이 스냅샷을 만들 때 아키타입 변경이 **이미 커밋돼
있다**. `LateUpdate` 로 옮기거나 sim 중에 두 번째 호출처를 만들면
`ObjectDisposedException: EntityTypeHandle invalidated by a structural change` 가 돌아온다
(AttackSystem 이 그 사고의 실측 주석을 갖고 있다). 쓰기는 **상태가 바뀔 때만** — 판당 최대 2회.

**7. ⚠ 조준·경로로 못 막는 여섯째 입구 — 부수 피해.** critic 리뷰가 찾았다:
`ProjectileHitSystem` 의 TileAoe 피해자 마스크가 `Factions.AnyDefender`(= `DefenderCore` 포함)라
**골 근처 방어유닛에 떨어진 광역**이 방패 선 마음을 깎았다(라이브 생산자 2곳 — 보스 임계
폭격 · 궁극기 슬램). 마음을 «겨눈» 게 아니라 옆에 떨어진 것이라 후보 배제로는 안 막힌다.

생산자마다 필터를 다는 대신 `DamageApplicationSystem` 에 **피해 버퍼 드랍 백스톱**을 뒀다 —
새 피해 경로(DoT·미래 페이로드)가 생겨도 자동으로 덮인다. 바로 위 `UltimateLeapState` 드랍과
**같은 이유·같은 형태**이고, 그 주석이 **왜 쿼리 배제가 아니어야 하는지**까지 설명한다:
「`WithNone` 으로 빼면 피해가 버퍼에 적립됐다가 통째로 터진다 — 무적이 아니라 지연 폭탄이 된다.」

heal 까지 건너뛰어도 안전하다: 방패는 판 시작에만 서고 **내려가기만** 한다(본능은 죽기만 하고
되살아나지 않는다). 즉 방패 중 마음은 늘 만피라 처치 회복은 어차피 clamp 로 버려진다.

## 무형 롤아웃

**라이브 9맵 중 방어 본능 저작은 Isle·Ford·Duel 셋뿐**(각 2기, `Structure_GuardInstinct`).
나머지 6맵은 태그가 한 번도 안 붙어 **현행과 1비트도 다르지 않다.** EditMode 가 이걸 고정한다
(`NoDefenderInstinct_CoreIsNeverShielded`).

## 알려진 구멍 — 웨이포인트 적

`waypointPathIndex >= 0` 인 적은 `MovementSystem` 우선순위가 **웨이포인트 > 거점 목적지**라
경로 차단이 **안 걸린다**. 웨이포인트를 다 쓴 뒤 마음으로 가서 대기한다(조준은 여전히 막힌다).

라이브 영향은 **`Enemy_Skimmer` 하나**다(`waypointPathIndex: 0`). 나머지
`Enemy_Waypoint*` 3종은 `Deck_WaypointLab` 전용이고, 같은 `Air` 층인 `Enemy_Dragon` 은
`waypointPathIndex: -1` 이라 골 직행 = 경로 차단 대상이다(**비행과 경로는 직교**).

수용하고 후속 후보로 남긴다 — Skimmer 1종이고, 「하늘길로 오는 적은 방패를 우회한다」로
읽히면 오히려 그 적의 정체성이 된다.

## 완료 기준

- [x] 컴파일 0 에러
- [x] EditMode **61/61 통과** (방패 3 + 돌격 게이트 2 + 경로 2 신규)
- [x] ECS 리뷰 CRITICAL 0 (지적 4건 반영)
- [ ] Play(Isle/Ford/Duel): 본능이 살아 있으면 적이 **본능으로 걸어가** 그것을 팬다
- [ ] Play: 마지막 본능이 무너지면 그때부터 마음이 깎이고 스트레스가 오른다
- [ ] Play: 스폰 예고선이 **본능으로 가는 길**을 그린다
- [ ] Play(Coil/Serpent 등 6맵): 방패가 한 번도 안 서고 현행과 같다
