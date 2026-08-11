# waypoint-routing — 착수 인계 (구현 전)

> ⚠ **이 handoff 는 구현 «전» 인계다** — 커밋 해시·검증 결과가 아직 없다. 담는 것은 설계가 왜 이 모양인지, 무엇이 기각됐는지, 코드에서 무엇이 이미 확인됐는지(재조사 금지 목록), 어디부터 시작하는지다. 구현이 커밋되면 이 문서 하단에 결과를 덧붙인다(traversal-layers 가 unit 5 를 handoff 하단에 덧붙인 선례).

## Commit

- `5f123d9c` rev 1(비행 전용 궤도·직선) → `aa3e82a2` **rev 2**(웨이포인트를 일반 라우팅 축으로 — 사용자 리뷰) → `08c06baa` **rev 3**(비행 = `Air` 통행층, A안) → `4b4d99bf` units 0~5 문서
- **구현 코드 0줄.** 전부 docs 커밋이다.

## 설계가 이 모양인 이유 (요약 3줄)

1. **순서 관리 / 이동 / 비행은 직교다.** rev 1 이 셋을 한 컴포넌트에 묶어 «지상은 경로를 못 쓰고 비행은 반드시 써야 하는» 구조가 됐고, 사용자 리뷰로 해체됐다.
2. **비행 = `Air` 통행층.** «비주얼만»(rev 2)은 규칙 가독성이 깨진다 — 떠 있는 적이 방벽에 막히면 버그로 읽힌다. 웨이포인트가 rev 1 의 기각 근거(벽 없는 필드 = 직선 = 맵 개성 상실)를 죽였기 때문에 필드 기반 비행이 싸졌다.
3. **대응 축 대칭이 제품 의도다**: 마음사냥꾼 = 유인 무효 · 비행 = 길막 무효 · 둘 다 화력엔 죽는다.

## 기각된 것 (부활 금지)

- **비행 전용 직선 이동**(rev 1) — 별도 이동 경로 = 분리 시스템 제외·골 판정 우회·전용 버퍼 전부 필요. Air 층이면 이동 코드 한 벌.
- **비행 비주얼만**(rev 2) — 위 요약 2.
- **대공(비행만 공격) 아군 유닛** — 사용자 제안에서 분리 확정. 방어측 적-서브타입 필터가 현재 0건이라 신설 축 = 자기 spec. backlog 에 «전용 말고 특화 우선(무비행 판 죽은 픽)» 까지 적어뒀다.

## 코드에서 이미 확인된 것 — **재조사 금지**

- `FlowFieldSingleton` 은 이미 슬롯 stride(`[slot*CellCount+cell]`) · `FlowFieldBuilder.BuildFromSources` 가 임의 지점을 받는다 · `FlowFieldRebuildSystem` 은 이미 슬롯 전체를 루프한다. **유일한 실변경 = `sources` 계산을 루프 안으로**(`FlowFieldRebuildSystem.cs:64~67`, 현재 항상 골).
- `SlotFor` 는 완전일치 + **조용한 primary 폴백** — 경고 지점은 스폰 1회(unit 3).
- 적 로스터는 필드 설치 시점에 알 수 있다(`BattleBridge:475`) — 층 합집합을 거기서 뽑는다.
- `AggroChaseCell` 은 어그로 획득 시 **지상 walkMask** 로 굽는다(`AggroStateSystem:141~146`) — Air 적은 층 인지 필요(unit 4). 안 하면 유인당한 비행이 벽을 돌아 걷는다.
- 충돌 `NavGrid` 는 층별 조립이 **이미 있다**(traversal-layers unit 5) — Air 층 벽 없음은 자동.
- lift 는 `SpineUnitView.SetFlightHeight` + `UnitLiftVisual.Resolve` 재사용 — 뷰 코드 신규 최소.
- `dist` 를 «골까지 거리»로 읽는 소비처 4곳: `FrontmostTargeting` · 블링크 착지 · 스폰 예고(`BattleBridge:1957~`) · `AttackSystem:1460`.

## 불변식 (구현 중 흔들리면 정지)

1. **슬롯 0 = (골, DefaultMask) 고정** — 위 소비처 4곳+α 가 무수정으로 옳은 값을 읽는 근거. 이게 깨지면 «뒤쪽 웨이포인트로 가는 적이 골 앞의 적보다 앞선 적» 이 된다(계약 2, 이 spec 최대의 조용한 버그).
2. **unit 3 은 한 커밋** — 부착·주입·소비를 쪼개면 «붙었는데 안 움직인다»가 되고 순수 함수 테스트는 그때도 전부 초록이다(traversal-layers unit 5 사고).
3. **`Air` 개방은 `Derive` 단일 정의로만** + `cellLayers==0` 전제 소비자 전수 grep(unit 4) — default 가 0→Air 로 바뀐다.
4. **골 판정 무변경** — 경로가 골 셀을 지나면 유출이 맞다. 저작 경고(unit 0)가 잡는다.
5. **순서 관리 순수 함수에 이동 방식 금지** — 들어오는 순간 rev 1 재발.

## 시작 위치와 순서

`0_authoring_axis.md` 부터 파일 번호 순서대로. 각 파일이 한 커밋이다. **unit 4 는 unit 3 라이브 검증 통과 후에만** 착수(아트가 잘못된 동작을 포장하지 않게). unit 5 에서 덱 편입 시 **웨이브 재추첨 규칙**(structure-hunter unit 1: 시드 재기준·풀 중간 삽입·라이브 덱 7종 = `WaveKillBudgetPinTests` 정본·`maxPerWave`) 준수.

## 검증 방법 (마음사냥꾼에서 확립된 것 그대로)

- 증상을 **센다** — 장면 목격이 아니라 카운터(웨이포인트 통과 순서·차단 통과 프레임·피격 유입). **음성 대조군 필수** — 대조군 없는 0 은 «기능이 죽은 판»과 구분되지 않는다.
- 계측 하네스 함정 3개는 `structure-hunter-enemy/2_handoff_summary.md` §계측 하네스 함정 참조(`ActivateDeployedDefender` 필수·골 타워는 `StartBattle` 후 생성·에디터 포커스 없으면 sim 정지).

## Follow-up

`docs/spec/README.md` Follow-up Backlog «비행 적» 그룹 + 이 README §7(대공 특화 유닛·경로 예고·잔여 맵 저작).
