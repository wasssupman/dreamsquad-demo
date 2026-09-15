# Sprite Unit Backend

상태: **구현 완료 2026-09-15 · Play 확인 대기** (units 0~3 커밋 · unit 4 에셋/테스트 커밋 · PlayMode lane 미실행 · 대상 유닛 미정)

## 검증 질문

**스프라이트 시트로 저작한 유닛 1기가 Spine 유닛과 같은 판에서 같은 규칙으로
뜨고 · 싸우고 · 죽고 · 카드가 붙는가 — Spine 경로는 한 줄도 바뀌지 않은 채로.**

## 상위 목표

유닛의 시각 백엔드를 **Spine 하나에서 둘로** 늘린다. 유닛 SO 에 스프라이트 모션 세트가
저작돼 있으면 스프라이트 시트로 그리고, 비어 있으면 **지금까지와 완전히 같은 Spine 경로**로 간다.

「모션당 시트 1장」이 저작 원칙이고, 모션의 종류는 **Spine 유닛이 실제로 쓰는 것**을 그대로 따른다.

재생 레이어는 이미 서 있다 — `SpriteFlipbookPlayer`(프레임 진행·도메인 클럭) ·
`SpriteFlipbookData`(프레임·fps·루프) · N×M 격자 슬라이서(`sprite-flipbook-player`).
이 spec 이 만드는 것은 **그 재생기를 유닛 파이프라인에 꽂는 배선**이다.

## 이 spec 의 성질 (2026-09-15 사용자 결정)

*「비주얼 단의 새로운 축을 생성하되, 축이 활용하는 요소에 대한 확장은 필요없는 구조」*
*「일단은 임시기능이라 so 에 있으면 sprite 없으면 원래대로」*

- **새로 만드는 것은 축 하나** — 뷰 백엔드. 그 축이 **쓰는** 것들(빌보드·블롭 그림자·체력바·
  상태이상 VFX·정렬·시각 파라미터·20여 개 소비 seam)은 확장하지 않는다.
- **Spine 경로는 삭제·재작성하지 않는다.** 분기가 하나 늘 뿐이다. 되돌리기 = 유닛 SO 의 필드 비우기.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터·오소링 | `0_motion_set_data.md` | `UnitSpriteMotionSet` SO + 슬롯 해석 순수 함수 + 유닛 SO 필드 1개 + 슬라이서 피벗 인자 |
| 1 | 추상화 | `1_unit_view_base.md` | `UnitView` 추상 베이스 추출 + 풀·시그니처 전환 — **라이브 동작 무변** |
| 2a | 뷰 — 선다 | `2a_sprite_unit_view_stand.md` | `SpriteUnitView` 골격 + `TrySpawn` 분기 — **뜨고, 걷고, 픽킹된다** |
| 2b | 뷰 — 반응한다 | `2b_sprite_unit_view_react.md` | 공격·배치·사망·반응 어휘·앵커 — **싸우고, 죽고, 움찔한다** |
| 3 | 드래그 그림 | `3_drag_preview.md` | 손끝 고스트 + 보드 실루엣을 스프라이트로 |
| 4 | 저작·검증 | `4_authoring_and_verify.md` | 유닛 1기 실저작 + PlayMode 테스트 + Play 확인 |
| 6 | 대기 변형 | `6_idle_variants.md` | idle1/idle2… — 쉼(idle 0프레임) → 변형 원샷 → 쉼 (2026-09-16 사용자 요청) |

handoff 는 `5_handoff_summary.md` (구현 종료 시).

### rev 1 → rev 2 → rev 3 에서 무엇이 바뀌었나

- **rev 2 (사용자 지적 「과설계」)**: 11 단위 → 4. 반응 어휘·픽킹·앵커는 **브리지 분기가 아니라
  뷰의 멤버**라 단위가 아니었다. 스폰 게이트 3곳도 백엔드 선택을 `TrySpawn` 안에 두면 불변.
- **rev 3 (critic 리뷰 REVISE)**: 사실 주장 3건이 코드 대조에서 거짓이었다 —
  「시그니처 3곳」(실측 `DefenderRetireFlight` 한 파일에 9곳 + PlayMode 테스트) ·
  「소스 변경 0」(`view.transform` 접근 5곳은 인터페이스로 못 닿는다) ·
  **「인터페이스로 열어도 안전」 — Unity fake-null.** 정적 타입이 인터페이스면 `!= null` 이 참조
  비교로 떨어져 **파괴된 뷰가 생존 판정을 통과**한다. 풀 10곳 + 소비처 13곳. 컴파일러가 못 잡는다.
  → 설계 변경: **인터페이스 대신 «선언만 있는 추상 베이스» `UnitView : MonoBehaviour`.**
  정적 타입이 `Object` 파생이라 `!= null` 의미가 살고, `transform`/`gameObject`/`GetComponent`
  접근과 PlayMode 테스트가 전부 무변경. 구현 공유는 여전히 0 이라 「복사한다」 계약 유지.

## Feature-wide 계약

- **저작 진입점은 유닛 SO 의 필드 하나.** `UnitSpriteMotionSet` 참조 1개가 `DefenderUnitData` ·
  `AttackUnitData` 맨 뒤에 붙고, `ISpineUnitVisualData.SpriteMotions` getter 로 노출된다.
  **비면 Spine.** 이 getter 추가가 이 spec 이 「축이 쓰는 요소」에 하는 **유일한 확장**이다 —
  `TrySpawn(ISpineUnitVisualData …)` 이 인터페이스만 받으므로 다른 길이 없다(`SpineIdleVariants` 선례).

- **모션 슬롯은 Spine 이 정본이다.** `idle`(루프) · `walk`(루프) · `attack`(원샷) · `death`(원샷) ·
  `deploy`(원샷·방어유닛) · `drag`(루프·방어유닛) — 6개. ~~`idleVariants` 는 넣지 않는다~~ → **unit 6 에서 열림**(2026-09-16):
  `idleVariants[]` + `idleRestGap` — 쉼(idle 0프레임) → 풀에서 하나 원샷 → 쉼. Spine 의 변형(루프 이어 붙임)과 **다른 성질**이다.

- **빈 슬롯은 폴백하지 실패하지 않는다 — 단 `idle` 은 예외다.** 폴백은 순수 함수
  (`UnitSpriteMotionSet.Resolve*`, EditMode 테스트)가 정한다: `walk` 없음 → 이동/정지 구분 없이 idle ·
  `deploy` 없음 → drag→attack→idle · `drag` 없음 → idle · `death` 없음 → 즉시 파괴.
  **세트가 있는데 `idle` 이 비면 `TrySpawn` 이 false + 경고** → 쿼드 폴백. 조용히 안 보이는 유닛은
  `CreateEnemyEntity` 계약 9(「슬롯/경로 폴백은 조용하면 안 된다」) 위반이다.

- **`UnitView` 는 seam 이 실제로 호출하는 멤버만 추상 선언한다.** `SpineUnitView` 의 public 표면에서
  `Spawn`(백엔드별 시그니처 — 풀이 concrete 로 부른다) · `SetLoopOverride`/`ClearLoopOverride`
  (Spine 애니 **이름 문자열** API — 스프라이트에 대응 축 없음) 을 뺀 나머지. `CurrentAnimationName`
  은 올린다(PlayMode 테스트가 쓰고, 스프라이트는 현재 플립북 이름을 돌려주면 된다).
  소환사 sync 호출부 1곳은 `is SpineUnitView` 로 가른다. 구현체 2개라 제약 8 충족, 깊이 2 라 제약 7 충족.
  ⚠ `QuadUnitView` 는 편입하지 않는다 — 개발용 폴백을 백엔드로 승격시키는 별개 결정.

- **뷰 풀은 하나고, 소비 seam 은 손대지 않는다.** 딕셔너리 값 타입만 `UnitView` 로 연다.
  실측 변경 목록(rev 3): `SpineUnitPool` 시그니처 4곳 · `BattleBridge.TryGetUnitView` 1곳 ·
  소환사 sync 호출부 캐스트 1줄 · `DefenderRetireFlight` 타입 9곳(본문 무변경). **그 외 0.**

- **`SpineUnitView` 본문은 건드리지 않는다 — 두 곳만 예외다.** (1) `: MonoBehaviour` → `: UnitView`
  와 멤버 `override` 키워드(컴파일러 검증). (2) facing 히스테리시스를 순수 함수 `UnitFacing` 호출로
  치환(아래). 그 외 복사 — 23개 멤버 중 다수가 transform/색/카메라 수학이라 상속으로 묶고 싶어지지만,
  **구현 공유는 0** 이다(임시 기능이 Spine 경로에 회귀 위험을 만드는 쪽이 더 비싸다).

- **facing 규칙은 순수 함수 하나가 소유한다.** `FacingFlipAccum = 0.05`·`FacingMoveEpsilon` 은 지금
  `SpineUnitView` 의 private const 다. 스프라이트가 리터럴을 한 번 더 적으면 **제약 13 이 「두 번
  났다」고 기록한 그 결함 형태**(한쪽만 튜닝돼 같은 판에서 두 유닛이 다르게 팩팩거린다). 판정을
  `UnitFacing.ShouldFlip` 로 빼고 두 뷰가 호출만 한다(제약 10 (b)). Spine 은 `ScaleX = +1` 이 **왼쪽**,
  스프라이트는 `flipX` 기준 — 부호 번역은 각 뷰가 한다. 시트가 오른쪽을 보고 그려졌으면 세트의
  `sheetFacesRight` 로 정규화한다(`SkeletonFlipXModifier` 의 데이터 대응 — 코드 분기 금지).

- **피벗은 발이다.** 슬라이서에 피벗 인자를 더한다(기본값은 Center 유지 — 확인용 프리팹 무회귀).
  유닛은 `transform` 원점이 발(=셀 위치)이어야 `Billboard` 접지 계약·블롭 앵커·정렬이 성립한다.
  **뷰에서 오프셋으로 보정하지 않는다** — 프레임마다 실루엣이 달라져 흔들린다.
  ⚠ 이미 잘린 시트는 재슬라이스해야 바뀐다(GUID 보존이라 참조는 안 끊긴다).

- **크기 노브는 하나다.** `spineVisualScale × BattleBridge.CharacterVisualScale` — Spine 과 같은 식,
  필드 신설 0. **PPU 128 고정**(저작 규약 — 2026-09-15 확정. 상대 튜닝 3회로 133.33 에 닿은 뒤 2의 거듭제곱·프랍 256 의 절반으로 반올림, +4%). 유닛별 크기는 `spineVisualScale` 로만. 유닛별로 PPU 를 바꾸면 노브가 둘이 된다 — 크기 차이는 `spineVisualScale` 로만.

- **재생기 확장 1건 — `SpriteFlipbookPlayer.Speed`.** 공격 압축(발사 주기 맞춤)과 걷기 배율에
  필요하다. 대안 「뷰가 자가 tick」은 불가 — 재생기 `OnDisable` 이 `_playing` 을 내려 컴포넌트를
  끌 수 없고, 켜 두면 이중 진행. 3줄, 기본값 1 = 현행.

- **스프라이트가 못 따라오는 것 4개.** 파츠 스킨(`SpinePartSkins`) · 슬롯 틴트(`SpineSlotColors`) —
  불가, **세트와 동시 저작 시 `UnitVisualDataValidator` 가 경고**(unit 0). 캐스트 앵커 본 추적 —
  저작 필드 `SpineCastAnchorLocalOffset` 만 재사용하고 폴백 8줄은 **복사**해 `ScaleX<0` 을 `flipX` 로
  번역한다(rev 2 의 「경로 재사용」은 코드 축에서 거짓이었다). 무기 궤적 본 추적 —
  `WeaponTrailRig.Bind(null)` 구조물 경로.

- **틴트 4축은 `SpriteRenderer.color` 하나로 합성한다.** hover(RGB 저장/복원) · health(RGB) ·
  flash(RGB lerp) · dim(A) — Spine 의 `Skeleton.SetColor` 관용구를 그대로 옮긴다. 실그림자
  (`UseRealShadows`)는 N/A — 기본 off 이고 모바일 강제 off 라 라이브 경로가 죽어 있다.

## 이 spec 이 **확장하지 않는** 것

| 요소 | 왜 |
|---|---|
| `Billboard` / `BlobShadow` | 두 뷰가 이미 같은 두 줄 / 같은 `Attach` 호출 |
| 체력바·오버헤드 UI·상태이상 VFX | anchor 를 `ResolveUnitViewTransform`(3-way, 첫 분기가 풀) 에서 받는다 |
| 스폰 진입점 3곳 · `EnsureMonoViewPools` · 씬 SerializeField | 게이트가 `bool spawned = TrySpawn(...)` 이라 sprite 에 true 면 불변 |
| ECS 컴포넌트 · 시뮬 · 이벤트 채널 | 시뮬은 뷰 백엔드를 모른다 |
| `QuadUnitView`/`QuadUnitViewPool` | 폴백 관계 그대로 |
| 20여 소비 seam | `var` 로 받고 멤버가 베이스에 있다 |

## 파이프라인 커버리지

유닛의 **View/Pool 정거장 하나**를 갈래로 나눈다. 아키타입은 `docs/reference/object-pipeline-map.md` 의
**방어 유닛** · **적** 두 표를 따른다. 순찰 아군도 같은 `TrySpawn` 을 타므로 대상이다(전용 sync 루프는
뷰 멤버만 호출해 백엔드 중립 · death 미도달은 기존 성질).

| 정거장 | 이 spec | 비고 |
|---|---|---|
| 데이터 SO | `Data/UnitSpriteMotionSet.cs`(신규) + 유닛 SO 필드 1개 + `ISpineUnitVisualData` getter 1개 | 스탯·능력·bake·카탈로그 불변 |
| 스폰 진입점 | N/A | 불변 |
| ECS 컴포넌트 · 시뮬 · 이벤트 큐 | N/A | 신설 0 |
| View/Pool | `Presentation/UnitView.cs` · `SpriteUnitView.cs`(신규) · `SpineUnitPool` 값 타입 | 풀은 늘리지 않는다 |
| 체력 표시 | N/A (자동) | |
| 씬 wiring | **N/A — 신규 SerializeField 0** | |

## 후속 후보 (현 spec 범위 밖)

- **`SpineUnitPool` 개명** · 씬 SerializeField 배선이 걸려 있어 백엔드가 상설로 승격될 때.
- **`ISpineUnitVisualData` 개명** · 이제 sprite getter 까지 들어가 이름이 더 거짓이 됐다. 두 SO + validator + 테스트로 번진다.
- **소환 루프 오버라이드의 스프라이트 대응** · 소환 순찰병에 시트를 저작할 때.
- **파츠 조합의 스프라이트 대안** · 「조합 결과를 굽는」 오소링 문제로 다시 세운다.
- **Spine 전면 은퇴** · 이 spec 의 목표가 아니다.
