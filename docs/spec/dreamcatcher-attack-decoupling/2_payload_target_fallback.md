# 2 — 페이로드 타게팅 폴백

## 목적

`ProjectileToTarget`(비수)이 host 의 `bestTarget` 을 못 받는 상황에서 **스스로 대상을 고르는** 경로를 만든다. host 가 대상을 확정했으면 지금처럼 그것을 쓴다(계약 3 — B안).

이 단위는 **폴백 규칙 + 데이터**만 만든다. 실제 호출은 unit 3·4 다(지금은 RESOLVE 안에서만 arm 이 돌고 거기선 `bestTarget` 이 항상 유효하므로, 폴백은 호출되지 않는다 = **기존 동작 무변화**).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/DcNeedleTargeting.cs` — 순수 선정 함수
- 신규 `Assets/_Project/Tests/EditMode/DcNeedleTargetingTests.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `ProjectileToTarget` bake 가 `slot.tileRange` 를 싣는다
- `Assets/_Project/Data/Dreamcatcher/Card_PokeNeedle.asset` — `payload.tileRange` 설정
- 시트 `DcMechanics` / `poke_needle` 행의 `tileRange` 셀

## 구현

### 선정 규칙 (순수 함수)

`FrontmostTargeting` · `LowestHealthTargeting` 선례를 따라 plain 값 입출력 static 함수로 둔다 — 결정론 tie-break 를 테스트로 고정하기 위해서다(CLAUDE.md 제약 10의 (c) sim-critical 타게팅).

- **진영 = `Faction.Enemy` 고정.** host mask 를 재사용하지 않는다 — 재사용하면 힐러가 아군을 겨눌 때 니들도 아군을 겨눈다(unit-trigger 계약 10 이 막고 있던 바로 그 버그).
- **진입 필터 = Chebyshev 타일**(`GridMath.RangeToTiles`), host 사거리 판정과 같은 자.
- **랭킹 = 유클리드 XZ 최근접**, 동점은 **후보 스냅샷 인덱스 순**(결정론).
- `PastGoalTag`(유출 대기) 제외 — 끝을 보는 눈 선례(`AttackSystem.cs:334`).
- 후보 없음 → `Entity.Null` 반환. 호출부는 발사를 건너뛴다(카운트는 이미 소비 — README 계약 5).

후보 스냅샷(`AttackSystem.cs:45~47`의 `targetEntities/targetTransforms/targetFactions`)은 `OnUpdate` 지역변수라 폭탄 분기·드레인 지점 어디서든 재사용할 수 있다. 별도 쿼리나 `ComponentLookup` 을 만들지 않는다.

### 반경 데이터

`DcPayloadSpec.tileRange` 를 `ProjectileToTarget` 의 **폴백 탐색 반경**으로 개통한다(기존 필드 재사용, 신규 필드 0). host `attackRange` 폴백은 금지 — 캐스터가 `0` 이라 즉사한다.

`Card_PokeNeedle` 은 **4**로 둔다(기존 실질 반경 = host `attackRange` 1~6 의 중간값). 시트에서 조정 가능하다.

### `tileRange <= 0` 을 거절하지 않는 이유

spec critic 은 "반경 0 = 절대 발동 불가 = 부착 거절"을 제안했지만 그건 **A안(항상 자체 탐색) 전제**였다. B안에서는 `tileRange` 가 0이어도 host 우선 경로로 정상 발동한다 — 폴백만 비활성이다. 따라서 unit 2 는 거절하지 않는다.

대신 **unit 3·4 가 사건 지점을 열 때** 적용성 판정에 조건이 붙는다: host 타겟이 구조적으로 없는 아키타입(`BombThrow`/`HazardCast`)에서 `ProjectileToTarget` 은 `tileRange > 0` 을 요구한다. 그 판정은 그때 추가한다(지금 넣으면 쓰이지 않는 죽은 규칙).

### 카드 문안

문안은 **바꾸지 않는다**. B안에서 주 경로는 여전히 "그 공격의 대상"이라 `"5번째 공격마다 → 대상에게 추가 투사체 피해 20"` 이 거짓이 아니다. (A안이었다면 "반경 N칸 최근접 적"으로 고쳐야 했다 — critic M4 는 그 전제였다.) 폭탄맨·캐스터가 열리는 unit 3·4 에서 문안에 반경을 노출할지 재검토한다.

## 완료 기준

- [ ] 컴파일 클린 + EditMode 그린. **기존 동작 무변화** — 폴백은 아직 호출되지 않는다.
- [ ] `DcNeedleTargetingTests`: 반경 밖 제외 · 아군/해저드 제외(`Faction.Enemy` 고정) · `PastGoalTag` 제외 · 동거리 tie-break 가 인덱스 순으로 **결정론** · 후보 없음 → `Entity.Null`.
- [ ] `Card_PokeNeedle.payload.tileRange == 4` 이고 bake 가 `slot.tileRange` 에 싣는다(EditMode 로 에셋 값 고정).
- [ ] **시트 왕복**: `DcMechanics` / `poke_needle` 행 `tileRange` 셀이 `4`. 현재 명시적 `0` 이라 갱신하지 않으면 다음 로그인 import 가 SO 를 되돌린다(`DcSheetApplier.cs:209` — blank 만 keep). `curl` 읽기 전용으로 대조까지가 완료.

---

확인 일자 / 커밋: (미완)
