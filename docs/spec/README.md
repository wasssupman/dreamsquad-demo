# Spec Documentation Structure

이 폴더는 프로토타이핑 이후의 feature 단위 구현 스펙을 보관한다. 새 기능은 `docs/spec/{feature-slug}/` 폴더 하나로 관리하고, 구현 단위는 번호가 붙은 작은 문서로 나눈다.

## 기본 구조

```text
docs/spec/{feature-slug}/
├── README.md
├── 0_{topic}.md
├── 1_{topic}.md
├── ...
├── N_{topic}.md
└── {N+1}_handoff_summary.md
```

## README.md

feature 의 입구 문서다.

- 현재 상태
- 목표
- 연결 문서
- 구현 문서 목록
- feature-wide 계약과 공통 원칙
- 비목표 또는 후속 후보

README 는 상세 구현서를 대신하지 않는다. 다음 작업자가 어디까지 완료됐고 어떤 번호 문서부터 읽어야 하는지 안내하는 인덱스다. 단, feature 전체에 영향을 주는 load-bearing 계약은 README 에 남긴다.

## 번호 문서

`0_{topic}.md` 부터 작업 순서대로 작성한다.

권장 섹션:

- 목적
- 변경 대상
- 구현
- 완료 기준

원칙:

- 1문서 = 1커밋에 가까운 작업 단위
- 1~3KB 정도의 작은 문서 유지
- 파일 경로를 명시
- 완료 기준은 compile/test/Play 확인 기준까지 포함
- 기존 번호를 재사용하지 않고 뒤에 추가
- 구현 완료 후에도 바뀌면 안 되는 계약만 갱신한다
- diff 설명이나 코드 흐름을 사후 문서화하지 않는다

## Handoff Summary

feature 구현이 끝났거나 세션 인계 가능성이 높으면 마지막 번호로 `{N+1}_handoff_summary.md` 를 작성한다.

예:

```text
docs/spec/map-system/20_claude_handoff_summary.md
docs/spec/wave-pattern/5_handoff_summary.md
```

필수 섹션:

- Commit
- Implemented
- Key Files
- Verified
- Notes
- Follow-up — 본 문서에 상세 항목을 적지 말고 본 README 하단 **Follow-up Backlog** 섹션으로 옮기고 한 줄 포인터만 남긴다

권장 길이:

- 30~80줄
- 핵심 파일 5~15개
- 완료 동작 5~10개

handoff 는 source of truth 가 아니다. 최신 상태와 계약은 README/번호 문서가 우선하고, 구현 상세는 코드와 커밋 히스토리가 우선한다. handoff 는 다음 에이전트가 무엇을 읽고 무엇을 건드리지 말아야 하는지 빠르게 파악하기 위한 지도다.

## Source Of Truth

```text
README.md                 최신 상태 + feature-wide 계약
{N}_{topic}.md            작업 단위 계약 + 완료 기준
{N+1}_handoff_summary.md  커밋 이후 인계 지도
code + git history        구현 상세
```

문서는 구현 상세를 전부 따라가지 않는다. 하지만 계약이 바뀌면 문서도 같이 바꾼다.

## Review 반영 기준

- 코드 버그를 유발하는 계약 공백: 코드 + 테스트 + 관련 spec 갱신
- 구현과 문서의 표현 불일치: 문서 갱신
- 단순 구현 설명 요구: handoff 에 짧게 쓰거나 생략
- 미래 확장/취향 제안: 후속 후보 또는 Follow-up 으로 이동

## 기존 예시

- `docs/spec/map-system/`
- `docs/spec/defender-drag-drop-deployment/`
- `docs/spec/defender-on-place-skills/`
- `docs/spec/wave-pattern/`
- `docs/spec/spawn-point-alert/`

---

## Follow-up Backlog

### 전투 판정 산식 통일 — 남은 결함 (2026-09-06, `distance-based-range` unit 23 에서 전수 감사)

`CLAUDE.md` **절대 제약 13** 은 「전투에서 «닿나»를 묻는 전부」에 적용된다. 23a/23b 가 도달 질의와
자기 자리 폭발을 통일했고, **아래는 같은 감사에서 나온 미이행분**이다(우선순위 순).

- **[높음] `AttackSystem.PickFallbackTarget`(:331·:264·:403)이 아직 «체비셰프 사각»** — 양쪽 몸 0.
  ⚠ 이름과 달리 **폴백이 아니라 유일 경로**다(그 아키타입은 RESOLVE 를 안 타 `AttackReach` 를
  한 번도 안 지난다). 해당 유닛: **폭탄맨 + 캐스터 4종**(화염·냉기·독·차단)의 **일반 공격/캐스트**.
  ※ 사용자 지시로 **뒤로 미룸**(2026-09-06).
- **[높음] `UltimateLeap` 착지 예고가 «사각» 인데 피해는 «원»** — N≥2 부터 모서리가 **거짓 예고**.
  주석의 「예고 셀 = 피해 셀 계약」은 unit 4b 이후 거짓이다. **오늘 이미 화면이 거짓말 중.**
- **[중] `SkillCone` 부채꼴 거리 게이트**(양쪽 몸 0) — 프리필터만 고치면 **변화 0**이라 콘 자체를 고쳐야 한다.
- **[중] 저작 툴팁·주석이 아직 "Chebyshev"** (`DcMechanic`·`ProjectilePatternData`·`BattleBridge.Dreamcatcher`)
  — 실제 자는 원이라 **저작자가 툴팁을 믿고 값을 정하면 어긋난다.**
- **[중] 골든 코퍼스가 «자기 자리 폭발» 축을 관측하지 못한다** — 회귀 탐지기로 약한 정도가
  아니라 **구조적 사각**이다(unit 23 A/B 2회로 확인). 세 경로 전부 Δ 가 0 이다:
  ① 배치되는 4기에 **배스티온이 없다**(23a 의 Δ 최대 유닛) · ② 자폭 저작인 **브루저가 덱에 없고**
  나머지 자기 자리 폭발은 전부 **드림캐쳐 카드인데 하네스가 카드를 안 붙인다** ·
  ③ 코퍼스 보스 `boss_jjangssen` 의 몸이 **정확히 0.5 = 칸 반폭**이라 슬램 판정이 항상 같은 답.
  → 배스티온·브루저가 배치되는 시나리오, 또는 몸이 0.5 가 아닌 보스(`nightmare` 0.615 ·
  `mamemo` 0.558) 장수 판, 또는 하네스의 카드 부착 확장. **「8/8 일치」를 «효과 없음»으로
  읽지 말 것** — 무회귀의 증거일 뿐이다.
- **[낮음]** 포탈 입구 진입(대상 몸 누락) · 드롭 최근접 적 픽(대상 몸 누락) ·
  `AttackSystem.AnchorCellOf`(게이트 원점이 앵커 셀 → 2×2 가 x 로 반 칸 치우침) ·
  `CollectAlliesInRange`(로그 전용, 후보를 앵커 셀로 접음).
- **[낮음] 실드 파열 브리지 arm 철거** — `if (!routedToSkillLayer)` 로 감싸진 **죽은 코드**
  (`skill-layer-migration unit 8` 잔여물). 남아 있으면 「실드 파열은 브리지가 실행한다」로 오해된다.
- **[낮음] 소비처 0 인 체비셰프 유틸 은퇴** — `Combat/AuraPulse.cs` · `FootprintMath.RectChebyshevDistance`.
  살려 두면 다음 사람이 체비셰프를 재유입시킨다.

### 적 감지 반경 — 남은 것 (`enemy-detection-range`)

- **「발견」 표식 프리팹 미저작** — 채널·트레이스만 완료라 **화면에는 안 뜬다**(`unity-vfx-authoring`).
- **Play 육안 4종** + **비행이 배치 구역으로 파고드는 새 성질**(실측 0.02%지만 육안 확인 필요).
- **사냥판 층별 슬롯** — 무제한 감지가 아직 공용 사냥판(지상 마스크)을 쓴다. 오늘 무제한 저작
  4종이 전부 지상이라 무해하지만, **비행 무제한 사냥꾼을 저작하는 날** 필요해진다.
- **Android 실기기 비용** — 감지 획득당 그리드 BFS 최대 3회(실측 ≈0.44회/초, 기기 측정 없음).

종료된 spec 의 follow-up 후보를 한 곳에 모은다. 개별 spec 의 handoff 에는 한 줄 포인터만 남기고 항목은 여기로 이관한다.

### 사용 규칙

- 각 항목 **1~3줄 요약** (What · Why · Scope). 상세 설계는 새 spec 에서 다룬다.
- Scope: **S** = 단일 unit, **M** = 2~5 unit spec, **L** = 5+ unit spec.
- **같은 결의 작업은 테마 서브그룹** (`#### {테마명}`) 으로 묶는다. 예: "Modifier framework — Legacy migration".
- 출처 spec 이 여럿 섞이기 시작하면 항목 끝에 `(spec-slug)` 라벨로 표기.
- 새 spec 으로 승격되면 줄을 `→ docs/spec/{slug}/` 링크로 대체한다.
- 더 이상 유효하지 않으면 줄을 삭제하거나 한 줄 사유와 함께 `Promoted` 로 옮긴다.

### Active

> 출처 spec 이 섞여 있다. 그룹 헤더 또는 항목 끝의 `(spec-slug)` 라벨로 출처 표기.

- ~~**골든 코퍼스의 세션-교차 조건 축 미상** [M]~~ → **정체 확인·차단 2026-09-04.**
  범인은 **`DevMapOverride`(PlayerPrefs `dev_forceMapIndex`)** — 로비의 개발용 맵 스테퍼(◀▶)나
  중단된 PlayMode 테스트(`StructureLivePlayTest` 등이 이 값을 바꿨다 되돌린다)가 남기는
  **머신 상태**이고, `BattleBridge` 맵 선택의 **1순위**다. 실측: 같은 날 두 베이크가 index 3 /
  index 0 으로 갈려 킬이 전건 달라졌고(`no_defense` 가 0킬 → 3킬 — 방어유닛이 없는데 킬이 났다
  = 본능 거점이 있는 다른 맵) configHash 도 8건 전부 갈렸다. 2026-09-03 의 「이벤트 가족이
  바뀌었다 돌아왔다」도 같은 축으로 설명된다. **차단**: 하네스가 베이크·검증 동안 override 를
  끄고 끝나면 원복한다(`SimHarnessRunner.PinMapCondition`). 이후 조건은 씬 규칙(fixedMapSeed →
  토너먼트 시드 → 폴백 0)으로 고정. (battle-sim-extraction · 발견은 distance-based-range unit 22)

#### 부착 범위 프리뷰 (dreamcatcher-attach-range-preview — **구현 완료 2026-09-03 · 검증 마감 대기**)

인계: `docs/spec/dreamcatcher-attach-range-preview/5_handoff_summary.md`. 닫기 전 필수(골든 정본 조건 결정·재베이크,
unit 4 육안)는 그 문서에. 아래는 닫은 뒤 후속.

- **카드 면 「범위 있음」 글리프** [S] · 46장 중 4장만 링을 그려 학습 기회가 드물다 — 드래그 전 예고. (F1)
- **오라 카드 문안 「발동 시 반경 N칸 안의 …」** [S] · `StatAura` 는 TTL 만 회수(반경 이탈 무관). 첫 `StatAura`/`GrantShield(N>0)`
  카드와 함께. (F2)
- **dim 면제 렌더 또는 링 라이브 중 `dimAlpha` 완화** [M] · 채움 주신호(D1)가 실기기에서 부족할 때의 다음 수. (F3)
- **조준 중심 마커(토네이도)** [S] · 원의 중심이 엄지 밑 — SDF 중심 도트 또는 중심 셀 1칸. (F4)
- **인스펙트 패널 범위 재열람** [S] · 손가락 없이 링을 보는 유일한 자리라 학습 채널로 우선. (F5)
- **배치 사거리 링 `+0.25` 를 결정 8(가장자리)로 정합** [S] · 카드 범위 링은 가장자리, 배치 링은 표준 상대 항 — 읽기가 갈린다.
  (F6, distance-based-range)
- **콜아웃 「반경 N칸」 힌트** [S] · D4 에서 기각(프리젠터 비접촉). 오독이 실측되면 재론.
- **폴백 반경·궤도 반경·부채꼴 표기** [S~M] · 폴백은 `PickFallbackTarget` 셀 체비셰프를 먼저 원으로 바꿔야 참말이 된다.
- **새 area concrete 미분류 감지 테스트** [S] · 카탈로그는 fail-closed 라 새 concrete 가 조용히 None 이 된다 — 전 `ISkill` Id 를
  명시 분류하는지 잡는 테스트. `IsPlacementRangeCell` 포워더(소비처 0) 삭제, dormant `BodyOverlapsSquare` 삭제 시점도 여기.

#### 넓은 판 3부작 (wide-board — **작성됨 2026-08-25**, 착수 전)

한 화면에 판을 우겨넣는 프레이밍을 은퇴시키고 자유 팬으로 더 큰 판을 플레이한다. 브레인스토밍에서
**세 spec 으로 분리**했다 — 셋을 한 spec 에 두면 카메라 커밋이 세이브 마이그레이션과 맵 밸런스에
인질로 잡힌다.

- **A → `docs/spec/wide-board-camera/`** (L, units 0~8) — 관심점·팬·홀드-투-피크 오버뷰·입력 중재·
  채널 재저작. **기존 23×10 에서 완결·출하 가능.** 먼저 착수하는 것이 이것 하나다.
- **B → `docs/spec/wide-board-content/`** (초안, L) — 36×14 저작 + 마음 N개 공유 체력 + 밸런스.
  A 가 사용자 체감 통과한 뒤 시작.
- **C → `docs/spec/squad-slots-ten/`** (초안, M) — 편성 7→10. 세이브·게이트 축은 독립이라 언제든
  병렬 가능하되, **전투 HUD 코너 예산은 A 와 공유**한다(A unit 5 의 오버뷰 버튼이 코너 위젯을 먹으면
  슬롯 폭이 더 좁아진다) — A 착수 후라면 그 결과를 읽고 슬롯 수를 확정한다.
  ⚠ 상수가 둘(`SquadPreset.SlotCount` / `SquadDraw.FieldCount`)이고 `LoadoutGate` 가 정확 일치를
  요구해 잘못 건드리면 **기존 프로필 전원이 출전 차단**된다.
- 선행 계약: **`docs/spec/heart-stress-axis/12_shared_heart_pool.md`** — 마음 N개가 체력을 공유
  (사용자 결정 2026-08-25). B 가 요구하고, A unit 7 의 「화면 밖 신호는 통합 스트레스 하나」가
  이 계약에 기댄다.

#### 틸트 빌보드 / 블롭 접지 (tilted-billboard — **완료 2026-08-30**, units 0~9)

블롭 그림자의 자리를 씬 전역 절대 Y 에서 **스테이지가 선언한 보드 평면**으로 옮기고,
정렬 스윕이 덮어쓰던 그림자 대역을 되찾았다. 상세: `docs/spec/tilted-billboard/12_handoff_summary.md`.

- **빌보드 카메라 추종 → `docs/spec/billboard-camera-follow/`** (M, units 0~2 · README 작성됨, 착수 전) —
  캐릭터·프랍이 `BillboardMode.Tilted` 45° 로 코드 4곳에 하드코딩돼 **카메라 pitch 를 추종하지 않는다.**
  이 spec 의 계약 「틸트 각은 데이터에서 온다」가 각도만 이행하고 모드는 안 한 상태라 부채 상환에 가깝다.
- **`BlobShadowStyle` SO 이관** (S) — `BattleBridge` 의 blob 튜닝 serialized 4개 + static 4개를 SO 로.
  제약 6 정합. 제약 12 판정으로 unit 7 범위에서 이관됐다.
- **큰 보스의 1타일 그림자** (S) — 적은 sim 이 1칸 점유라 `FootprintWidthCells => 1` 이 참값인데,
  `spineVisualScale` 3.2 짜리 보스가 폭3 디펜더 옆에 서면 그림자가 몸집과 어긋난다. 손대려면
  「점유」와 별개로 **「시각 크기」 축**을 세워야 한다(유닛별 크기 노브 금지는 사용자 결정).
- **블롭 지름의 `tileSize` 환산 부재** (S) — 셀 단위 footprint 를 월드 지름으로 바로 쓴다.
  `tileSize = 1` 인 동안 무해하고 전역 노브로 균일 보정되지만, 그 노브가 예술적 배율과 환산을 겸직한다.
- **프랍 블롭의 진짜 질문** (S) — 라이브 스테이지 3종의 BlobShadow 22개가 전부 `authoredInPrefab: 0` 이라
  `ApplyTransform` 이 **한 번도 안 돈다**(화면에 나오는 건 프리팹에 구워진 SpriteRenderer). 즉 「authored
  프랍을 unit 7 계약으로 편입」은 실행되지 않는 분기를 겨냥한 것이다. 손대려면 이 사실부터 다시 볼 것.

#### 유닛 Footprint · 드래그 실루엣 (defender-footprint — **완료 2026-08-30**, units 0~9)

유닛이 W×H 논리 타일을 점유할 수 있게 하고(시스템만 — **실값은 전 유닛 1×1 로 철회**, 사용자 결정),
배치 드래그의 유닛 그림을 손끝 키링에서 **보드 footprint 위 실루엣**으로 바꿨다.
상세: `docs/spec/defender-footprint/6_handoff_summary.md`.

- **`DropDismountTest` 3번째 원인 규명** [S] · 이 spec 이전부터 red. 원인 3겹 중 둘은 해소했고
  남은 하나는 «셀→화면→셀 roundtrip 이 어떤 배치 가능 칸에서도 성립 안 함». 두 함수가 서로 역이어야
  하므로 **디오라마 스테이지 보드 평면 vs 뷰 매핑 어긋남**이 유력 — 그렇다면 테스트가 아니라
  좌표계 결함이고 `map-diorama-stage` 소관이다. 시작점은 `8_dnd_silhouette.md` 의 조사 기록.
- **제스처 시작점 통합** [M] · 트레이 D&D 를 «arm + 보드 드래그»로 접어 드래그 구현을 하나로.
  armed 경로에 없는 세션 소유 기능 8종(슬로우모·적 반투명·배치 컷신·취소 존·거부 라벨·카메라
  포커스·throttle·튜토리얼 훅)을 함께 옮겨야 한다.
- **철수의 환급 유예** [S] · «배치 후 N초 안에 철수하면 코스트 환급». 되돌리기 버튼이 은퇴한 자리를
  기존 어휘로 메우는 안 — 밸런스 결정이라 owner 판단 필요.
- **multi-cell 저작 재개** [S] · 값만 올리면 되고 코드 0. 재개 시 배치·선택·부착·재배치
  한 바퀴 Play 검증이 완료 기준(리뷰 공통 지적). ~~시너지~~ 는 2026-09-03 기능 은퇴로 제외.
- **거리 기반 판정 전환** [L] · 사거리·셀 효과를 거리 중심으로. 이 spec 이 소비처를 `FootprintMath`
  하나로 수렴시켜 **전환 지점을 한 곳으로 확보**해 뒀다(2026-08-28 사용자 계획).
- **적 통행 차단 footprint** [M] · 방어유닛이 통행을 막는 축. 흐름장 1회 굽기 제약과 교착 리스크 동반.

#### 보너스 당기기 (bonus-wave-pull — **완료 2026-08-24**, units 0~9)

일반 당김 알약 **위에** 조건부로 뜨는 두 번째 버튼. 누르면 맵에 저작된 보드 중앙 포탈 2개에서
보너스 적 10기가 나와 방어유닛을 사냥하다 전멸시키면 거점으로 간다. 등장 조건은 **일반 처치
30기(회수) AND 스트레스 30 이하(창)** 이고, 회수는 창이 닫혀 있어도 쌓인다.
상세: `docs/spec/bonus-wave-pull/10_handoff_summary.md`.

> ~~브랜치가 `heart-stress-axis` 다(`main` 아님)~~ **해소 2026-08-24** — `heart-stress-axis`
> 가 main 에 머지됐다(`8f24cf26`). `StressMath` 가 main 에 있으므로 이 feature 도 main 에 있다.

- **골든 코퍼스 재생성** [S] · 판 조건 지문(`configHash`)에 항목 3개가 추가돼 기준선이 어긋난다.
  회귀가 아니다. ⚠ 워킹트리에 무관 dirty 가 있으면 그게 기준선에 구워지므로 격리 후 별도 커밋.
- **포탈 전용 비주얼** [S] · 지금 일반 스폰 포탈과 **완전히 같은 프리팹**이라 화면에서 구분이 안 된다.
  `portal-vfx-upgrade` 의 「입구/출구 시각 구분」과 같은 자리.
- **임계 30 실측 튜닝** [S] · 판당 1~4회 추정. **잘하는 플레이어일수록 자주 온다** — 스트레스 게이트가
  그 성질을 증폭한다. 반대 설계(위험할 때 구원)로 뒤집는 건 `maxStressToOffer` 를 하한으로 바꾸는 한 줄.
- **「왜 안 뜨지」 힌트** [S] · `BattleBridge.BonusPullBlockedByStress`(회수는 찼는데 스트레스가 막는 중)가
  API 로만 있고 도크는 안 읽는다. 문안을 붙일지 판단.
- **R-별 헌터 필드 분리** [S] · 사냥 안내판이 판에 한 장뿐이라 도착지 반경이 **팔 제일 짧은 사냥꾼**
  기준으로 잡힌다. 근접 보너스 적이 살면 그 반경이 1로 내려간다. **증상은 거의 없다** — 보스가
  사냥을 포기하려면 도착지가 0개여야 하고, 그건 모든 방어유닛이 사방 벽에 파묻힌 배치일 때뿐이다
  (Duel 은 230칸 중 224칸이 걸을 수 있는 땅이라 불가능). 벽 많은 맵에서 실제 관측되면 착수.
- **보너스 적을 덱 풀에 넣기** [M] · 넣는 순간 트리거 판별(SO 동일성)이 함께 깨진다 —
  판별을 이벤트 페이로드 기반으로 먼저 바꿔야 한다. `BonusEnemyNotInDeckTests` 가 지키고 있다.

- **웨이브 난이도 파라미터화** [M] — 같은 맵을 파라미터로 다른 난이도로 돌리는 축. 현재 난이도는 덱 정적 저작뿐이고 `Generate` 입력은 (덱, 시드, laneCount) 셋이다. 후보 3안 검토됨(critic 2026-08-15): A 맵당 난이도별 덱(현행 관용구 — Endless/Tutorial 이 이미 이 방식, 코드 0줄) / B DifficultyProfile 배율 SO(배율 3층 스태킹 위험) / C 난이도 = 웨이브 오프셋 정수(수량·로스터·컨셉 해금이 한 축으로 정합 상승, 표시·케이던스는 `i` 유지). **착수 조건: 같은 맵을 같은 빌드에서 둘 이상 난이도로 플레이할 소비자(난이도 선택 UI 또는 서버 티어 수신처)가 생겼을 때** — 그 전엔 A 가 코드 0줄로 첫 요구를 받는다. 확정 계약 한 줄: **난이도는 결정론 키의 일부다(`laneCount` 와 같은 지위) — 같은 맵 + 같은 난이도 = 같은 웨이브.** (wave-concept-blocks)

#### 패배 없는 3분 킬 경쟁 (three-minute-kill-race — **완료 2026-08-16**, units 0~4)

판에서 **지는 일을 없앤 spec**. 3분 고정·전원 완주이고, 판을 끝내는 것은 **만료와 유저 제출
둘뿐**이다(시스템 판정 4개 은퇴). 점수는 **1킬 = 1점, 예외 없음**이고 마음은 판정 권한 0 ·
게이지 없이 균열로만 보인다. 상세: `docs/spec/three-minute-kill-race/5_handoff_summary.md`.

> ⚠ **`EndMatch` 를 부르는 코드를 새로 만들면 패배 조건의 부활이다.** 이 spec 의 단일 최상위 계약.
> — **2026-08-23 갱신**: `heart-stress-axis` 가 이 계약을 **의도적으로 뒤집는다**(사용자 결정).
>   마음이 판을 끝내는 축으로 돌아오면서 `EndMatch` 경로가 3개(`complete`·`submitted`·`stress_full`)가 된다.
>   계약의 정신(「시스템이 마음대로 판을 끝내지 않는다」)은 **경로 수를 3 으로 고정**하는 형태로 승계된다 —
>   → `docs/spec/heart-stress-axis/README.md`. 이 줄을 근거로 그 spec 을 위반으로 읽지 말 것.

- **엔드리스 모드의 정체** [M] · 본 모드가 «시간 고정 + 패배 없음» 이 되면서 차이가 «시간 무제한» 하나로 줄었다. 게다가 엔드리스는 만료가 없어 **유저 제출이 유일한 종료**다. 존치 여부부터 결정해야 한다. (three-minute-kill-race)
- **몽마의 계약 코스트 재지정** [M] · 스트레스 한계가 패배와 무관해진 데 이어 **한계 표기 자체가 사라져** «허용치 선불»(`_leakAllowancePenalty`)이 완전히 공짜다. unit 2 의 스트레스 표기가 «일단은» 인 이유. (three-minute-kill-race)
- **적 마음(공성 맵)의 새 역할** [M] · 부숴도 판이 안 끝나면 지금은 «사격이 멎는 것» 만 남는다. 점수원이 될지 연출로 남을지. (three-minute-kill-race)
- **제출 개방 인지** [S] · 60초가 지나 제출이 열린 것을 유저가 어떻게 아는가. 햄버거 배지 정도가 후보. (three-minute-kill-race)
- **조기 제출의 동기** [S] · 무페널티라 «3분을 다 쓰지 않을 이유» 가 지금은 없다. 시간 절약 외의 이유가 필요한지 플레이 후 판단. (three-minute-kill-race)
- **해몽 — 마음의 서사 회수** [L] · 판 후 마음이 겪은 일을 서사로 돌려준다. 코드에 없고 `docs/reference/드림캐쳐_각성안_최종스펙_v1.md` 의 한 줄이 전부. 게이지를 없앤 자리를 최종적으로 메우는 축. (three-minute-kill-race)
- **`MenuPopup` 브리지 참조 승격** [S] · 지금 `FindFirstObjectByType` 이다(씬이 타 세션 WIP 를 물고 있어 저장을 피한 의도적 예외). 씬이 깨끗해지면 SerializeField 로. (three-minute-kill-race)

#### 엘리트 등급 적 (elite-enemy-tier — **완료 2026-08-13**, units 0~7)

적을 **일반/엘리트/보스** 3등급으로 가른 spec. 보스 특권이 「메커닉 보유」가 아니라 `tier == Boss`
에서만 나오게 바꿔 **«메커닉을 가진 비보스»** 를 가능하게 했다. 엘리트 2종(슬라임 분열 · 드래곤
화염 브레스) 출하 + 라이브 덱 7종 편입(`2712aa01`, wave-concept-blocks unit 7).
상세: `docs/spec/elite-enemy-tier/8_handoff_summary.md` · 파이프라인 = `object-pipeline-map.md` **적** 아키타입.

- ~~밸런스 실전 미검증~~ · **2026-08-13 라이브 플레이 확인 완료.** `66004836`(슬라임 피해·체력 ×2 · 드래곤 기본 20 · 브레스 50 · 화염 10/틱)로 확정. 이후 이 값을 만질 땐 **테스트가 회귀를 잡지 못한다**는 것만 기억할 것 — 단언이 전부 상대·구조형이다(의도된 것). (elite-enemy-tier)
- **화염 스택을 출처별로 가르기** [M] · `_stackThresholds` 가 `StackKind` 당 규칙 한 벌이라 드래곤과 Kindler 가 `StackModifier_Fire` 를 **물리적으로 공유**한다. 드래곤 화염을 4→10 으로 올릴 때 Kindler 도 같이 올라갔다(사용자 승인). 따로 주려면 새 StackKind 나 출처별 오버라이드가 필요하다 — `DotOrigin` 2축 분리(dot-effect-extraction unit 0)와 같은 결의 문제다. (elite-enemy-tier)
- **광역 도형 어휘 통합** [M] · 이 spec 이 접은 것(`EffectArea` 철회). 착수 조건은 «저작이 도형을 고르는 소비자가 2개 이상 생겼는가» — 그 전에는 같은 과설계가 반복된다. 후보: `SingleSplash` splash · `HazardShapeSampler`(managed→Burst) · `AllyBuffFieldSystem` · `AuraPulse` · 어그로 반경. (elite-enemy-tier)
- **브레스 «지속 콘» 전환 — 하지 않기로 함(사용자 결정 2026-08-13, 플레이 확인 후)** · 즉발 유지가 현상이다. 재론될 때 **다시 조사하지 않도록** 그때 확인한 비용을 적어둔다 — 셋은 의미가 다르고 비용이 한 자릿수 차이다. (elite-enemy-tier)
  - **A. 기존 장판(Zone) 재사용 = M 이상.** 두 군데가 막혀 있다. ① `ZoneApplySystem` 이 대상을 `Faction.EnemyUnit` 으로 **하드 게이트**한다(존은 적에게만) — 주석이 「진영 축을 열지 않는다(제약 8)」로 의도적 미개방임을 밝힌다. ② `HazardShape` 에 콘이 없고(`SingleCell`/`Square3x3`/`RadiusSquare`) 샘플러가 **타일 리스트** 기반인데 브레스 판정은 월드 연속이다 — 1~3타일에서 타일 양자화하면 방향이 ~45° 흔들린다(unit 1 이 world-space 를 고른 이유). 게다가 아래 「광역 도형 어휘 통합」의 착수 조건을 **인위적으로 만드는** 셈이라 접었던 과설계가 돌아온다.
  - **B. 채널링(n초 반복 즉발) = M.** 브레스는 arm 순간 1프레임 즉발이고 **채널링 선례가 0** 이다(`NextAttackDoubleFire` 같은 1회성 플래그만 있다). 공격자별 지속 상태 + 틱 시스템 + 이동·사망·CC 중단 규칙 + 「채널 중 조준 갱신 여부」 설계 결정이 붙는다.
  - **C. 콘에 걸린 대상에게 DoT = S(반나절).** `DotEffect`+`DotApplyEventsSingleton` 이 있고 생산자 3곳이 같은 패턴이라 `ApplyConeBreath` 에서 `DotApplyEvent` 를 넣고 `DotOrigin` 하나 추가(append-only)면 된다. **단 지속 «콘» 이 아니다** — 맞은 순간 화상이 붙어 걸어나가도 타고 나중에 들어오면 안 탄다. ⚠ 그리고 **드래곤은 이미 지속 피해가 있다**(화염 5스택 → 10/틱 × 4.85초). origin 이 다르면 슬롯이 갈려 **합산**되므로 C 는 신규가 아니라 기존 화상과 겹치는 설계다 — 수치 재조정 동반.
- **브레스 예고(telegraph)** [S] · 브레스는 즉발이고 공격 애니가 없어 플레이어가 받는 신호가 **VFX 하나뿐**이다. 지금은 읽힌다(2026-08-13 확인). 안 읽히는 상황이 오면 `hitDelaySec` + 바닥 링. (elite-enemy-tier)
- **슬라임 3단계 이상 분열** [S] · 중간 SO 를 하나 더 만들면 되고 **코드 변경 0** 이다(`SplitChain` 예산 깊이 8 · 총 32 안에서). (elite-enemy-tier)
- **엘리트 전용 등장 연출·HUD·아트** [M] · 보스경보를 재사용하지 않기로 해서(계약 1) 엘리트를 구분하는 수단이 지금은 스켈레톤 크기뿐이다. 둘 다 벤더 Spine 예제 as-is. (elite-enemy-tier)
- **`EnemyKilledEvent` 페이로드 태그화** [S] · 각성·점수·킬버스트·분열로 **네 번째** 필드 append 가 됐다. 다섯 번째가 붙기 전에 «태그 + union» 검토. (elite-enemy-tier)
- **남은 5개 e2e 를 `BattleBridgeTestAccess` 로 이관** [S] · `Boss*`·`*Shield*`·`Kindler` 가 리플렉션 스폰 레시피 사본을 유지하고 **이름 단언이 없다** — 개명 한 번에 NRE 로 조용히 죽는다(2026-08-11 `deckIndex`→`laneIndex` 가 Kindler 를 그렇게 죽였다). 그 파일을 여는 다음 세션이 같이 옮기는 것이 싸다. (elite-enemy-tier)
- **콘 브레스 VFX 카탈로그 항목** [S] · `common-skill-vfx-reference.md` 에 부채꼴 화염 항목이 없다. 스킬 규칙상 **사용자 승인 없이 카탈로그에 추가 금지**라 초안만 있고 미등재. (elite-enemy-tier)

#### 소환사 & 순찰병 (summon-patrol-defender — **완료 2026-08-12**, units 0~12 · unit 6 철회)

이 게임의 3번째 유닛 유형 — **아군인데 walk 위를 이동하는 첫 유닛**. 「이동은 적 전용」 전제를 깼고,
게임 최초의 고유 스파인 리그 2종(CH1·Doll)이 여기서 들어왔다. 신규 이벤트 채널 0.
상세: `docs/spec/summon-patrol-defender/13_handoff_summary.md` · 파이프라인 = `object-pipeline-map.md` **순찰 아군** 아키타입.

- **⚠ 검증 질문 미판정** [S] · *"소환사를 뒤에 두고 순찰병을 앞세우는 것이, 타일에 유닛을 직접 놓는 것과 다른 배치 결정을 만드는가?"* 기능은 검증됐지만 이 질문의 답이 기록되지 않은 채 종료했다. **순찰병 콘텐츠를 이어서 만들기 전에 이 답부터 받는다.** (summon-patrol-defender)
- **사거리 술어 미러 동치성 PlayMode 단언** [M] · `AttackReach` 를 쓰는 5곳이 같은 답을 내는지는 지금 **사람이 손으로 맞춘다**. EditMode 는 술어 자체만 보고 술어를 우회하는 경로(락·커밋 재판정)는 커버리지 밖인데, 실제로 그 우회가 코드 리뷰에서 발견됐다. 「락을 문 공격자가 게이트 경계로 벌어졌을 때 `AttackSystem.bestTarget` 과 `EnemyAiState` 가 같은 답을 낸다」를 고정하면 이 교착 클래스 전체가 덮인다. (summon-patrol-defender)
- **순찰병이 실제로 타격하는지 PlayMode 단언** [S] · 182프레임 교착이 실측으로 발견됐는데 자동 그물이 없다. 「순찰병이 N 프레임 안에 적 HP 를 깎는다」 한 줄이면 «멈추는데 못 때림» 전체를 잡는다. (summon-patrol-defender)
- **아웃게임 크기 정규화** [S] · `outgameScaleMul`(소환사 0.372)은 임시다. 리그가 정규본이 되면 1 로 되돌리고 **필드째 제거**한다. 그때까지 이 필드는 «리그가 비정규본이다»의 표식. (summon-patrol-defender)
- **영구 봉쇄 밸런스 감시** [S] · 순찰병이 경로 위에 서면 적이 멈추고(`Engaging`+`Halt`), 죽어야 다시 간다. 재소환이 빠르면 봉쇄가 성립한다. 막는 knob 은 HP 가 아니라 **재소환 쿨다운**이다 — 관찰 기록 없음. (summon-patrol-defender)
- **보스가 순찰병에 눌러앉는지** [S] · 보스 사냥 대상에 편입되는데(계약 1 의 `DefenderUnitTag` 귀결) 관찰 기록이 없다. (summon-patrol-defender)
- **파츠형 42유닛 육안 무회귀** [S] · 고유 리그 도입 후 데이터 레벨 대조군(Scout)만 확인됐다. `LoadingRunnerRigTests` 가 카탈로그 전 유닛의 리그 리졸브·스킨 실존을 자동 고정하지만 그것이 육안 무회귀는 아니다. (summon-patrol-defender)
- **고유 리그 잔여 3경로** [S] · 항아리 피규어(`SpineFigureBuilder` — `SkeletonGraphic` 이 새 아틀라스를 처음 만나는 자리, `sizeDelta` 400×600 고정) · facing 정합 기록 없음 · 소환사 death 트랙 부재 수용 판정. (summon-patrol-defender)
- **다중 순찰병** [S] · `SummonerState.current` 를 버퍼로 바꾸고 "빈 슬롯이 있으면 소환"으로 규칙 전환. 지금은 1기 고정(사용자 결정 2026-08-03). (summon-patrol-defender)
- **배치형 이동 아군** [M] · `PatrolAnchor` 만 붙이고 `SummonedBy` 를 안 붙이면 성립한다. 지금 만들지 않는 이유는 생성 경로가 소환 하나뿐이라서다(제약 9). (summon-patrol-defender)
- **거점 이동 명령** [M] · 플레이어가 드래그로 거점을 재지정. 배치 결정의 성격이 달라지므로 별도 결정. (summon-patrol-defender)
- **순찰병 어그로 보유** [M] · `AggroCapacity` 를 주면 적을 붙잡아 세우는 성격이 강해진다. 현재는 어그로 없이 `Engaging`+`Halt` 에 의존. (summon-patrol-defender)
- **`ZoneApplySystem` 아군 대상 존** [S] · unit 0 은 "존은 적에게만" 게이트 하나만 넣었다. 아군 대상 존(회복 장판 등)이 실제로 생기면 그때 `HazardEffect` 에 진영 축을 연다 — 지금 여는 것은 투기(제약 8). (summon-patrol-defender)
- **`EnemySample` 구조체 통합** [S] · `StepDir` 이 `enemyCells`/`enemyPositions` 두 배열의 index 정렬을 **관례로만** 유지한다. 하나로 접으면 길이 불일치 가드(지금은 조용히 정지 = 교착으로 폴백)와 파라미터 과다가 함께 사라진다. (summon-patrol-defender)
- **아군 이동체 가독성 일반 규칙** [S] · unit 6(발밑 링)은 고유 리그가 실루엣으로 해결해 철회됐다. 다음 이동형 아군이 **파츠형 리그를 재사용**하면 같은 문제가 돌아온다 — 그때 `6_ally_readability.md` 의 선택지 A/B/C 에서 다시 고른다. (summon-patrol-defender)
- **발밑 데칼이 캐릭터 대역으로 끌려간다** [S] · `SpineUnitView.UpdateSortingOrder` 가 `GetComponentsInChildren<Renderer>` 로 자식 렌더러를 전부 캐릭터 order 로 덮어써(궤적 리그만 예외), `BlobShadow` 의 `ShadowOrder`(-5)가 매 프레임 지워진다. 궤적 리그처럼 자기 대역을 소유하게 빼는 게 맞지만 **유닛 전원(44)** 의 그림자 정렬이 바뀌므로 육안 확인 동반 별도 작업. unit 6 철회 시 함께 되돌렸다. (summon-patrol-defender)

#### 세 번째 보스 마메모 (boss-mamemo — **완료 2026-08-11**, Play 확인 + 코드리뷰 + 재우기 버그 계측·수정까지)

웨이브 회전을 멈춰 점수를 깎는 보스. 아무도 안 죽이면서 시간을 가져간다.
신규 맥락·시스템·채널 0. 상세: `docs/spec/boss-mamemo/5_handoff_summary.md`.

- **마메모 웨이브 소요 시간 관측** [S] · 이 보스의 정체가 회전 지연이다 — 실전 판에서 마메모 웨이브가 다른 웨이브보다 눈에 띄게 오래 걸리는지가 최종 성립 조건. (~~자장가 발동 횟수 관측~~ 은 `BossLullabyLiveTest` 상시 계측으로 상환했었으나 **그 테스트는 2026-08-17 삭제** — 랜덤 매치 시드 위에서 emergent 타이밍을 단언하는 계측형이라 통과/실패가 운이었다. 관측이 다시 필요하면 결정론적 픽스처로 새로 쓴다.) (boss-mamemo)
- **악몽의 가호가 골 앞에서 유지되는가** [S] · 호위 이속(2.2~2.5, 러너 7.2) vs 마메모 1.4 → 반경 4 를 약 4초에 이탈한다. 실드가 스폰 직후에만 붙으면 「호위가 골에 눌러앉아 전멸 지연」 논지가 성립하지 않는다. (boss-mamemo)
- **가호에 대상 수 상한이 없다** [S] · 반경 4 원판(81칸) 전부에 flat 60. 수혜자 EHP 배율이 러너(HP 20) 4.0× ~ 뱅가드(HP 120) 1.5× 로 흔들린다. 필요해지면 cap 축 신설. (boss-mamemo)
- **적 오버헤드 바 압축** [S] · HP 20 러너가 실드 60 을 받으면 HP 구간이 바의 25%. 적 실드 게이지가 이번에 열려 처음 보이는 현상. (boss-mamemo)
- **⚠ 자는 캐스터가 계속 시전한다 — 사용자 판정 대기** [M] · `HazardCastSystem`·`ShieldCastSystem` 이 CC 를 안 본다(`shield-guardian-defender` 계약 7 의 **의도**). 스펙은 "캐스터 편성이 자장가의 답" 으로 프레이밍했는데 **사용자는 버그로 읽었다**(Play 관측 2026-08-11). 고치면 가디언·해저드 캐스터 **전원**의 동작이 바뀌므로 별 spec. 당장 안 고치기로 결정됨. **2026-08-26 재확인 — 「후속」**(skill-layer-migration unit 5 가 이 결함을 건드리지 않고 이전만 한다는 것을 다시 확인받았다). (boss-mamemo)
- **보스 `OnShieldBreak` 개방** [M] · 마메모와 궁합이 가장 좋다(실드 깨지는 순간 반격). 단 **실행기 진영 파라미터화가 선행** — 브리지 파열 드레인의 대상 풀이 `AttackUnitTag` 하드코딩이라 보스가 쓰면 자기 진영을 때린다. 지금은 `DcTrigger.EnemyTriggerArmed` 가 막고 EditMode 가 고정한다. 짱쎈놈 README 의 동일 항목과 병합. (boss-mamemo)
- **악몽의 늪(자리에 남는 장판)** [L] · 배치 공간 박탈 축. 장판 효과가 `PathFollowState`+적 진영 게이트라 **타일 고정인 방어유닛에게 구조적으로 안 닿는다**. **다음 보스에서 딥하게 논의**(사용자 결정 2026-08-11). (boss-mamemo)
- **네 번째 보스: 소환형** [L] · 잡몹을 직접 뱉는 물량 축. 소환 페이로드 + 브리지 스폰 seam 필요. (boss-mamemo)

#### BattleBridge 해체 (battlebridge-dissolution — **초안 2026-08-26 · 승인 대기**)

→ `docs/spec/battlebridge-dissolution/`

목표 = **BattleBridge 제거 또는 「완전한 채널」로 축소** (사용자 결정 2026-08-26).
발단은 스킬 레이어 종료 후의 실측이다 — 그 작업이 브리지에 준 것은 **한계비용**이었고
(스킬 하나 추가 = arm 하나 → `case` 한 줄 + `Register` 한 줄, 큰 arm 셋 소멸),
**총량은 안 줄었다**(12,852줄 · 메서드 348 · public 110 · 외부 참조 67파일).

채널의 정의를 spec 이 먼저 박는다: **번역과 전달뿐. 판정·소유·저장은 채널이 아니다.**
그 자로 재면 지금 브리지는 셋을 겸직한다 — 채널(`Drain…` 22) · 뷰 설정 보관소
(`BlobShadowSprite`·`CharacterVisualScale` 류) · 게임 규칙 호스트
(`CollectShieldBreakTargets`·`ApplyEffectTileIfAny`·bake 검증 19곳).

⚠ **ECS 경계는 그대로다.** 채널이 여럿이 되는 것이지 제약 1 이 열리는 게 아니다.

#### 스킬 단일 레이어 (skill-layer-foundation + skill-layer-migration — **구현 완료 2026-08-26**)

→ `docs/spec/skill-layer-foundation/` · `docs/spec/skill-layer-migration/`

끝점 = **이 게임의 모든 스킬(보스 · 배치 · 특수)이 하나의 레이어 위에 있다.** 스킬 하나 =
concrete 하나이고 `Execute` 를 호출하는 주체가 그 스킬의 소유자다. **도메인(`ISkill`/concrete)은
ECS 를 참조하지 않고** 포트를 통해서만 모듈과 주고받는다(사용자 하드 제약 2026-08-24).
`skill-fire-dispatch`(rev 4, 홀드)는 **흡수**됐다 — 그 spec 은 읽기 전용 이력이고 계약 6·12 는 폐기.

**결과 (units 0~8).** 스킬 34종이 `Wassup.Skills` concrete 로 왔고, 감지자는 발화만 알린다.
어휘 셋 중 `OnPlaceEffectType` 은 타입째 사라졌고, `SkillEffectType` 은 저작 enum 으로 남아
라우팅만 한다(사용자 결정). `DcPayloadKind` arm 은 **둘만 남았고 그 둘은 스킬이 아니다**:

- `PlacementAura` — **발동 규칙**(시제가 다르다: 지금 실행이 아니라 미래에 적용될 규칙 등록)
- `HeavyStrike` — **그 공격의 성질**(자기참조: 자기를 부른 사건 자체를 바꾼다)

두 이유가 다르다는 것이 요점이다 — 뭉뚱그리면 다음 후보를 잘못 분류한다. 판별기는
「반환값처럼 보이는 것」의 우회 가능 여부였다: 스킬이면 우회됐고(세는 건 읽기 · 캐리어는
재조정 · 예고는 전방 흐름), 안 되면 스킬이 아니었다.

**곁들여 걷은 것**: 진영 화이트리스트 2술어(→ 「감지자가 있나」 하나로), 「방어유닛 전용」
하드코딩 셋(강공 pre-scan · 자기 죽음 루프 · 투사체 splash/bounce 풀).

**후속 후보**: 출처 사망 시 모디파이어 회수(생기면 `PlacementAura` 의 영수증이 불필요해진다) ·
`SplitOnDeath` 형태 점검(시제상 스킬인데 배선만 다른 길이다) · `EmitProjectilePattern` 의
splash 저작 검증.
설계 근거: `docs/plans/2026-08-24-skill-layer-unification-critic.md`(critic 5트랙 수렴본,
전원 REQUEST CHANGES · CRITICAL 7건).

착수 전 알아야 할 것 (critic 산출):

- **드레인 지점은 3개다** [계약] · 감지자들의 same-frame 하류 계약 구간이 겹치지 않아
  (#8 < #45) 단일 지점은 산술적으로 불가. `BattleBridge.Update` 는 `Mono Update →
  SimulationSystemGroup` 순서라 원리적으로 탈락 → `BattleSimGroup` 내 managed `SystemBase` 3인스턴스.
- **골든은 증인이 아니다** [S] · 코퍼스에 스킬 발화 기록 **0회**, Cc·Aggro·Blink·StatModifier 는
  채널 자체가 없다. 게다가 코퍼스가 stale. 그물을 먼저 깐다(`foundation/1_golden_and_net.md`).
- **census 는 ~75행** [-] · 액티브(`SkillData` 6에셋) · 소환(`SummonPatrolAbility`) ·
  드래곤 `AreaBreath` · 방어유닛 규칙 5행이 초기 초안에서 누락됐었다. 어휘가 **3개**다.
- **`SimEntityId` 싱글턴 승격은 M1 로 반환** [S] · 공유 카운터가 ID 열을 밀어 골든 전건 발산.
- **spec 종료 시 골든 재기준 1회** [S] · 그것이 `battle-sim-extraction` M1 의 새 A/B 기준선.

이 spec 이 흡수하지 않은 것 (여전히 후속):

- **무거운 arm 이관** [M] · 발동 지점이 전 유닛 순회 Burst 코드 내부라 별도 seam 설계 필요.
- **카드 authoring 의 SO 이전** [M] · 어댑터 은퇴. 시트 연동 재설계와 함께.
- **악몽의 늪(자리에 남는 장판)** [L] · skill-fire-dispatch 홀드 인계 #4 가 함께 대기시킨 축.
  **다음 보스 제작 때 콘텐츠 결정으로 꺼낸다**(사용자 결정 2026-08-11, boss-mamemo 그룹과 동일 항목).
- **스킬 보유 ≠ 보스 분리** [M] · 기술 선행은 **이미 해소됐다**(`BattleBridge.cs:9355` 가
  `tier == EnemyTier.Boss` 로 가른다 — 이 백로그 항목이 stale 했다). 남은 것은 콘텐츠 결정뿐:
  잡몹에게 능동 스킬을 **라이브로** 허용할지(현 blueprint 정의: "능동 스킬을 가진 적 = 보스").
- **(시전자, 스킬) → 고유 연출 매핑** · **스킬 툴팁 노출** · **호스트당 슬롯 스케줄러**
  (발동 중재 콘텐츠가 실재할 때).

#### 거점 사냥꾼 (structure-hunter-enemy — units 0~1 완료 2026-08-11, **사용자 Play 확인 대기**)

유인·차단이 통하지 않는 첫 적(마음사냥꾼). 저작 마스크만으로 도발 면역이 파생되고,
동시 등장 상한(`maxPerWave`)을 신설해 라이브 덱 7종에 올렸다.
상세: `docs/spec/structure-hunter-enemy/2_handoff_summary.md`.

- **사용자 Play 체감 확인** [S] · 웨이브 8 이후 실제 등장 + 2기 붙었을 때 골 압박. 스탯(HP 400 / 근접 25 / 속도 2.0 / `maxPerWave` 2)은 계측값이고 체감으로 확정된 적이 없다. (structure-hunter-enemy)
- **웨이브 재기준 여파 재밸런싱** [M] · 풀에 1종이 늘어 라이브 7덱의 웨이브가 **전부 재추첨**됐다(시드 20260811~17 로 갱신해 명시). 6개 맵의 난이도 곡선이 미검증 상태다 — 마음사냥꾼과 무관한 회귀가 여기서 나올 수 있다. (structure-hunter-enemy)
- **`Kindler` 가 `Deck_Endless` 풀에 없다** [S] · 이 spec 이 만든 문제는 아니고 이전부터 누락돼 있었다. 무한 모드만 조용히 이 적을 못 본다. 라이브 덱 열거의 정본은 `WaveKillBudgetPinTests`. (structure-hunter-enemy)
- **본능(Instinct)도 노리는 변형** [S] · 마스크에 `DefenderInstinct` 를 더하면 끝이지만 **현재 맵에 방어 본능 저작이 없다**. (structure-hunter-enemy)
- **「거점 우선, 유닛도 때림」 변형** [S] · 도발이 걸리는 중간 형태. 지금 후보는 **거리로만 경쟁**하므로(`AttackSystem.cs:109`) faction 우선순위 축이 필요하다. (structure-hunter-enemy)
- **스폰 예고에서 「유인 불가」 구분 표시** [S] · 지금은 스폰 후에야 안다. (structure-hunter-enemy)

#### 비행 적 (waypoint-flight-enemy — units 0~4+7 완료 2026-08-11, 다음 unit 5)

맵이 N개 웨이포인트 경로를 저작하고 적 SO가 하나를 선택한다. 기존 flow field를
`목적지 × 통행층` 슬롯으로 재사용하며, Air 적은 지상 차단을 무시한다.
상세: `docs/spec/waypoint-flight-enemy/README.md`.

- **unit 5 경로 페인터 + 맵 2~3장** [M] · 유일한 미완료 작업 단위. 기존 저장/bake와 unit 0 검증 함수를 재사용하고, 맵마다 다른 방어 위치를 요구하는지 사용자 Play로 닫는다. (waypoint-flight-enemy)
- **넉백머신(구 대공사수) 고유 콘텐츠화** [S] · ~~Path·Air 동시 타겟이 정체성~~ → **2026-08-17 로 소멸**: 아틸러리·폭탄맨을 뺀 전 방어유닛이 `Path|Air` 가 됐다(사용자 결정). 대신 **매 피격 넉백**(0.3칸/0.08초)이 새 정체성이 되고 이름도 그걸 따라갔다 — `docs/spec/defender-knockback-on-impact/`. `id` 는 `anti_air` 유지. 남은 것은 고유 아트·최종 밸런스와 **시트 반영**(`displayName`/`desc` 는 시트 소유라 미반영 시 로그인 임포트가 되돌림). (waypoint-flight-enemy)
- **잔여 맵 경로 저작** [S] · unit 5는 2~3장까지만 다루며 나머지 맵은 후속 콘텐츠 작업이다. (waypoint-flight-enemy)

#### 목표지점 안정도 (goal-stability — 완료 2026-08-04)

- **붕괴 골을 적 라우팅에서 제외** [M] · 골 목적지가 빌드 시 고정이라, 멀티골 맵에서 한 마음이 부서지면 그쪽이 최근접인 적은 **살아있는 마음을 놔두고 부서진 골에서 소멸+유출**된다(2026-08-12 비행 적이 표면화, `waypoint-flight-enemy/6_handoff_summary.md` 조사 기록). **사용자 결정 B — 착수 보류**: 진행 중인 맵 개편으로 폭1 단방향 멀티골 맵이 은퇴 예정이라 시인성 보강(붕괴 프랍 그을림+주저앉음)만 반영. 개편 후에도 멀티골이 남으면 재평가. (waypoint-flight-enemy × goal-stability)
- 실맵 `goalMaxStability` 콘텐츠 값 결정 — 검증용 임시값(전 골 300)은 미커밋. 스트레스 예산과 함께 밸런싱 [S]
- 안정도 잔량 점수화 / 스트레스 예산 재균형 — 공성 전환으로 유출 빈도가 구조적으로 줄어 `score-formula` "한계·점당 동조" 경고가 발동한다 [M]
- 골 피격 데미지 넘버 — `DamageApplicationSystem` 의 `AttackUnitTag` 필터 확장 [S]
- 붕괴 프랍 교체/파괴 상태 아트 + 정식 붕괴 VFX(현재 록버스트 재사용) [S]
- 골 힐 — 힐러/스킬의 안정도 회복(Faction 마스크 확장, 사용자 결정 필요) [M]
- 스폰 예고 라인의 공성 반영(예고 경로 끝 표현 차별화) + 붕괴 시 즉시 스트레스 보너스 knob [S]

#### 기믹 인지 (gimmick-recognition-upgrade — 완료 2026-08-01, 사용자 리빌 실기 확인 통과)

매판 배정되는 기믹을 배치 페이즈 안 우상단 카드에서 **배치 앞 독립 리빌 페이즈**로 옮겼다.
문구를 4단(룰 라벨·두 줄 요약·정서 카피·상세)으로 쪼개고 아이콘·의미 색·등장음을 붙였으며,
기존 `GimmickGuideView` 는 은퇴했다. 상세: `docs/spec/gimmick-recognition-upgrade/4_handoff_summary.md`.

- **familiarity 게이팅** [S] · 같은 기믹을 N번 본 뒤 리빌이 자동으로 짧아진다. 지금은 매판 같은 길이라 반복 플레이에서 탭 스킵에 의존한다. `TutorialProgress`/`PlayerProfileSO` 플래그 전례가 있다. **읽을 시간이 여전히 모자라거나 반대로 길게 느껴지면 이걸 먼저 검토.** (gimmick-recognition-upgrade)
- **`summary` 수치의 stale 위험** [S] · 수치가 문자열로 박혀 있어 기믹 SO 를 재튜닝하면 문구가 조용히 어긋난다. 드림캐쳐 카드가 같은 문제를 `DreamcatcherCardText` 포매터로 풀었다(에셋 텍스트는 폴백). 기믹 수치를 자주 만지게 되면 그 패턴으로 이관. (gimmick-recognition-upgrade)
- **리빌 VFX 가 딤 아래 깔린다** [S] · 캔버스가 `ScreenSpaceOverlay` 라 월드 VFX 는 구조상 딤 밑이다. **2026-08-01 사용자 판정으로 현행 유지.** 살리려면 VFX 를 캔버스 자식으로 옮기고 스케일 knob 을 SO 에 추가해야 한다(프리팹이 월드 단위 저작이라 픽셀 캔버스에선 먼지만 해진다). (gimmick-recognition-upgrade)
- **회상 경로** [S] · 판 중간에 "이번 기믹 뭐였지"를 답할 곳이 없다. 신규 UI 없이 가능한 자리가 둘 있다 — `MenuPopup`(일시정지)에 `description` 한 줄, 결과 화면에 "이번 판 특수룰" 한 줄. 후자는 룰↔결과 인과를 붙여 다음 판 인지를 올린다. **"진입 시점에만 집중" 지시로 이번 범위에서 제외됨.** (gimmick-recognition-upgrade)
- **기믹 진행 표시** [M] · 사직서 3/5, 열기 4/6 같은 누적 상태와 임계 도달 신호. 인지 효과는 크지만 ECS→Bridge→View 데이터 seam 이 필요하고, **전투 중 상시 UI 는 2026-08-01 사용자 판정으로 비목표**다(정보량·화면 점유). 되살리려면 그 판정부터 뒤집어야 한다. (gimmick-recognition-upgrade)
- **사직서·온천 리빌 VFX** [S] · `revealVfxPrefab` 슬롯이 비어 절차 파티클로만 돈다(번아웃·레드불은 기존 프리팹 연결됨). 아트가 생기면 코드 0줄로 꽂힌다. (gimmick-recognition-upgrade)
- **등장음 테이크 확정** [S] · Take1 채택 상태, Take2·Take3 가 `Audio/GimmickRevealTakes/` 에 대기. 교체는 `GimmickReveal.mp3` GUID 유지 덮어쓰기. 확정 후 테이크 폴더 삭제. (gimmick-recognition-upgrade)
- **`GimmickData` 시트 임포터** [S] · 문구가 4단으로 늘어 시트 관리 가치가 올라갔다. 지금은 수기 편집. (gimmick-recognition-upgrade)
- **로비에서 다음 판 기믹 예고** [M] · 배정이 배틀 씬 `GameManager.Start` 라 로비에서 미리 알 수 없다. 구조 변경 필요. (gimmick-recognition-upgrade)

#### 액티브 드림캐쳐 (active-dreamcatcher-tile-aim + active-ally-zone — 완료 2026-07-30, 사용자 Play 확인 통과)

액티브 6종을 "화살표 + 타일 지정" 한 문법으로 통일하고(대상축 `SkillTargetType` 폐기), 아군 버프를
시간제 장판으로 바꿔 "액티브는 지정한 칸에 영역을 만든다" 를 성립시켰다. 선택 중 액티브 차단도 폐기.
상세: `docs/spec/active-dreamcatcher-tile-aim/4_handoff_summary.md` ·
`docs/spec/active-ally-zone/5_handoff_summary.md`.

- **감속장을 캐리어로** [M] · `ApplySlowField` 는 아직 **스냅샷**이다 — 시전 시점 반경 내 적에게 지속시간 모디파이어를 직접 걸어, 나중에 들어온 적은 안 걸리고 나간 적은 계속 느리다. 원칙("안에 있는 대상이 영향을 받는다")의 **마지막 예외**. `AllyBuffField`/`TornadoField` 패턴을 적 쪽에 그대로 적용하면 된다. (active-ally-zone)
- **장판 위 아군 하이라이트** [M] · 바닥 점등이 은퇴(2026-09-03)한 뒤로 장판 수명 동안 시각 신호가 **0** 이다 — "누가 강화 중인가" 가 유닛에 안 붙는다. `SetHoverHighlight` 는 단일 슬롯 래치라 조준 틴트와 공존 불가 → 정식 경로는 `StatusFxKind` append + `ReconcileStatusFx` 의 `origin == Skill` 분기. **프리팹 1개(아트) 필요.** (active-ally-zone × active-dreamcatcher-tile-aim)
- **press 시점 범위 프리뷰** [S] · 카드를 집는 순간 어디에 얼마나 퍼지는지 보이면 "이 카드는 영역" 을 글로 배우지 않아도 된다. 지금은 끌어야 보인다. 액티브/부착 공통. (active-ally-zone)
- **적/아군 장판 프리뷰 색 구분** [S] · 조준 링이 적 대상·아군 대상 모두 `TileSetData.aimRingStyle` 하나다. 가르려면 스타일 2개(SO)로. (active-dreamcatcher-tile-aim × active-ally-zone × dreamcatcher-attach-range-preview)
- **장판 수명 표시 재도입** [S] · 바닥 사각 타일 점등은 은퇴(2026-09-03 — 원 판정·조준과 어긋나 「부착 후 타일 잔존」으로 읽힘). 다시 필요하면 원 링/VFX. 링 채널은 단일 owner 라 장판 여러 장 동시 표시 설계가 선행. (active-ally-zone × dreamcatcher-attach-range-preview)
- **손가락 오클루전 오프셋** [S] · 타일 조준점이 손끝에 가려지는지 실기기 확인 후 판단. 배치 쪽에는 이미 가상 포인터 오프셋 선례가 있다. (active-dreamcatcher-tile-aim)
- **`TornadoField`/`PortalLink` 매치 경계 정리 누락** [S] · `DestroyBattleEntities` 에 없어 캐리어가 다음 판까지 산다. 적 전용이라 매치 사이엔 대상이 없어 실질 무해했지만 `AllyBuffField` 와 같은 구멍이었다. (active-ally-zone)
- **`SkillData.cooldownSec`/`cost` 완전 삭제** [S] · 액티브 흡수 후 dormant(각성치가 비용, 순환이 재등장 간격). 에셋 값만 남아 있다. (active-dreamcatcher-tile-aim)
- **`PendingDeployment` 제외 테스트 커버** [S] · 배치 대기 유닛이 장판/오라 멤버십에서 빠지는 규칙에 테스트가 없다(재배치 비행 창 포함). (active-ally-zone)
- **액티브 전용 카드 아트** [S] · 현재 uiTint/스킬명 폴백. (active-dreamcatcher-tile-aim)

#### 토너먼트 덱 정보 (tournament-deck-info · tournament-history-deck-view · deck-info-preset-apply — 완료)

서버 `deckInfo` 를 채우고, 히스토리에서 참가자의 덱을 보여주며, 남의 편성을 스쿼드/드림캐쳐별
새 프리셋 작업본으로 가져온다. 히스토리는 1뎁스 2컬럼으로 재설계됐다. 상세:
`docs/spec/tournament-deck-info/3_handoff_summary.md`,
`docs/spec/tournament-history-deck-view/5_handoff_summary.md`,
`docs/spec/deck-info-preset-apply/6_handoff_summary.md`.

- **0점 마감이 덱 기록을 덮어쓰는지 확인** [S] · 유일하게 남은 미확인. 좋은 판 뒤 **나가기로** 한 판을 끝내고 같은 엔트리를 다시 열면 판정된다. 덮인다면 클라를 더 고칠 게 아니라(이미 값 없으면 키를 안 보낸다) **서버에 최고점 가드를 요청**하는 쪽이 맞다. (tournament-deck-info)
- **카드 문안 정합** [S] · 팝업은 `card.description` 원문을 쓴다. 게임 내 다른 카드 표면은 `DreamcatcherCardText` 를 거쳐 축/타입 헤더와 "○○ 전용" 부착 제한을 붙인다. 남의 덱에서 부착 제한이 안 보이는 것은 의식적 선택이지 누락이 아니다. (tournament-history-deck-view)
- **결과 화면에도 덱보기** [S] · `LeaderboardList.Render` 의 옵트인 콜백을 켜기만 하면 된다. 판 직후 상대 덱을 보는 흐름이 자연스러운지는 별도 판단. (tournament-history-deck-view)
- **랭킹 행 룩 통일** [S] · `ResultScreen` 은 `LeaderboardList.Render` 를 안 쓰고 자체 행 페인팅을 갖는다(`result-screen-ranking-ui` 재설계 때 갈라짐). 두 화면의 행 모양이 이미 미세하게 다르다. (tournament-history-deck-view)
- **랭킹 캐시** [S] · 토너먼트를 오가며 볼 때 재조회를 줄인다. 지금은 단순함 우선(선택마다 fetch + epoch 가드). (tournament-history-deck-view)
- **빈 목록 안내 라이브 확인** [S] · 실계정에 22건이 있어 못 봤다. EditMode 로만 고정돼 있다. (tournament-history-deck-view)
- **드림스톤 캐리인 로그가 미해석 id 를 버린다** [S] · `GameManager.LogDreamstoneCarryIn` 이 `stoneCatalog.ById == null` 이면 슬롯을 지운다(유닛은 raw id 를 남기는 것과 비대칭). 시트에 새 스톤이 추가됐는데 로컬 SO 가 stale 하면 장착 스톤이 조용히 사라진 덱이 기록된다. `slotIndex` 유실도 같이 판단. (tournament-deck-info)

#### 드래그 취소 (drag-cancel-affordance — 완료 2026-07-30, 사용자 Play 확인 통과)

유닛 트레이 / 드림캐쳐 손패 D&D 의 취소 수단. 트레이·손패 복귀 취소 + 격자 밖 관용(`Resolve` 가
`Vector2Int?` 반환)으로 "보드 밖에 놓으면 취소" 를 성립시켰다. 상세:
`docs/spec/drag-cancel-affordance/4_handoff_summary.md`.

- **출발 슬롯 지목** [S] · 취소 상태에서 그 유닛의 슬롯만 코랄 링/미세 확대로 "이 슬롯으로 돌아간다" 를 지목. rev3 에서 덮는 배너를 지울 때 함께 보류했으나 **덮지 않는 보강**이라 재고 가치가 있다. 현재 예고는 프리뷰 고스트 + 포인터 라벨뿐. (drag-cancel-affordance)
- **취소 수단 최초 발견 힌트** [S] · 취소 라벨은 취소 상태에 **들어간 뒤에만** 뜨므로 수단을 모르는 플레이어는 계속 모른다. 첫 배치 드래그 1회에 1.5초 안내 — `UserDragStarted` 훅이 이미 있고 `GimmickGuideView` 가 같은 훅을 쓴다. (drag-cancel-affordance)
- **취소 존 진입 햅틱** [S] · 모바일에서 취소 상태 진입 시 짧은 진동. 실기기 확인이 필요해 분리했다. (drag-cancel-affordance)
- **감각 노브 재조정** [S] · `placementOutsideToleranceCells` 1(가장자리가 빡빡하면 2, 취소가 멀면 0) · `cancelHintDwellSeconds` 0.18. 둘 다 `DragSwaySettings.asset` 에서 Play 중 실시간 반영 — 실기기 감각이 갈리면 여기부터. (drag-cancel-affordance)
- **손패의 기존 ESC 취소 제거** [S] · `DreamcatcherHandView.Update` 의 ESC → `CancelAllCardInteraction` 은 이 spec 이전 동작이라 남겼다. unit 2 철회 사유("모바일에서 드래그 중 back 은 손가락이 닿지 않는다")가 그것에도 적용되지만, 사용자에게 취소 수단으로 안내되지 않는 에디터 편의라 제거는 별건 판단. (drag-cancel-affordance)
- **재배치(DefenderRelocation) 취소** [M] · 이미 배치된 유닛을 드는 경로라 "무차감" 정의가 다르다(원위치 복귀가 취소). 트레이 존 판정을 그대로 쓸 수 없어 별도 설계 필요. (drag-cancel-affordance)
- **손패 패널 배경을 부채 크기로** [S] · 이 spec 은 **판정 rect** 만 카드 부채(310)에 맞추고 배경 그림은 그대로 뒀다. 배경을 키우면 "취소 영역 = 보이는 손패" 가 그림으로도 일치하지만 보드 가림이 60px 늘어난다. (drag-cancel-affordance × dreamcatcher-hand-drag-clearance)

#### 화염 스택 원거리 적 (enemy-fire-stack-shooter — 완료 2026-07-30, 사용자 Play 확인 통과)

킨들러(레인저 전용 하드 타겟팅 원거리 적) + 프로젝트 최초 Fire 스택 producer. 선행 결함
(투사체가 부여한 스택이 누적되지 않던 것)을 unit 0 에서 수정. 상세:
`docs/spec/enemy-fire-stack-shooter/4_handoff_summary.md`.

- **`ApplyStat` 의 투사체 귀속 결함** [S] · `ProjectileHitSystem` 의 `ApplyStat` 이 아직도 `source` 로 **투사체 엔티티**를 보내 발사마다 새 `StatModifierSlot` 을 만든다. `Enemy_Debuffer`(Needle 투사체, `DamageMul ×0.6 Multiplicative`)는 **한 기만 있어도** 0.6ⁿ 로 곱누적된다 — `modifier-stacking-policy` 가 "서로 다른 소스"로 진단하고 클램프 `[0.2, 5]` 로 막은 증상의 실제 뿌리로 보인다. `ApplyStack` 은 unit 0 에서 고쳤고 이쪽만 남았다. 고치면 곱누적 → 상시 ×0.6 이라 **라이브 밸런스가 움직이므로 수치 재조정과 한 묶음**이어야 한다. (enemy-fire-stack-shooter)
- **전투 스택 오버헤드 아이콘** [M] · `OverheadStackKind` 가 기믹 전용(`Fatigue`/`Heat`) 2종뿐이라 전투 스택(Fire/Ice/Bleed/Poison) 축적이 화면에 안 보인다. 5스택이 터지기 전까지 플레이어가 받는 신호가 피격뿐. `bleed-fighter-defender` 후속 후보와 **같은 항목** — 먼저 착수하는 쪽이 흡수한다. (enemy-fire-stack-shooter × bleed-fighter-defender)
- **화상 히트 VFX 분리** [S] · 스택 적재 순간의 피격 피드백과 5스택 발화 순간의 폭발 연출이 구분되지 않는다(둘 다 벤더 `FireballHit` 원샷). (enemy-fire-stack-shooter)
- **다중 공격자 DoT 합산** [M] · 킨들러 N기가 붙어도 화상은 한 슬롯(`(Stack, Fire)`)으로 접혀 `remainingTime = max` 갱신만 된다 — **화상 화력이 마릿수에 비례하지 않고 4.0 DPS 가 천장**. 2026-07-30 사용자 결정으로 **그대로 두기로 확정**했다(뒤집으려면 `enemy-fire-stack-shooter` README 계약 2·6-1 인용 후 재승인). 열려면 `dot-effect-extraction` 의 "다중 공격자 출혈 합산"과 같은 작업 — 도트 전용 가산 병합이 필요하고 난도질꾼도 같이 바뀐다. (enemy-fire-stack-shooter × dot-effect-extraction)
- **킨들러 전용 아트** [S] · 현재 Spine 은 공용 스켈레톤 + 파츠 placeholder(풀페이스 레이싱 헬멧 + 화염 틴트), 투사체는 벤더 as-is. ⚠ 부품 라이브러리가 **캐주얼 현대물**이라 로브/마법사 모자가 없다 — 판타지 캐스터 외형은 아트 신규 제작 사안. (enemy-fire-stack-shooter)

#### 발사 명세 시스템 (projectile-emission-pattern — units 0~5 완료 2026-07-28, Play e2e 대기)

- **카드 bake payload 화이트리스트 + terminal else** [S] · 카드 bake 의 payload `if/else if` 체인에 terminal `else` 가 없어, 배선 안 된 kind 가 조용히 슬롯으로 붙고 "부착됨"으로 집계된다(설명 공란). 단순 추가가 안 되는 이유는 `NextAttackDoubleFire`·`SelfBuffLethal` 처럼 **분기 없이 통과해도 정상인 kind** 가 섞여 있고 그 목록이 어디에도 없기 때문 — 실제 작업은 화이트리스트 명시다. `DcApplicability` 는 이미 전수 테스트로 강제되므로 bake 만 비대칭. 다음에 payload 를 추가하는 spec 이 흡수하면 된다. (projectile-emission-pattern)
- **범용성 갭 4종** [M~L] · 무타겟 패턴(방향/셀 지정) · host 독립 발사(사망 유언·bridge-cast) · 서브 발사(착탄 → 자식 패턴) · non-Damage 패턴(Stat/Stack/Heal outputs). 상세는 `docs/spec/projectile-emission-pattern/README.md` 후속 후보.
- **defender 패턴 개통 시 안정 키** [S] · 타겟 선택의 tie-break 가 같은 셀 중복 시 스냅샷 순서에 의존한다. 적 후보는 자유 이동이라 셀 공유가 상시 — `hostIsDefender` 경로를 여는 커밋에서 반드시 함께 처리. (projectile-emission-pattern)

#### 드림캐쳐 공격 결합 (dreamcatcher-attack-decoupling — 완료 2026-07-27)

- **페이로드 다연발(n초 간격 m발)** [M] · 비수가 발동당 1발만 쏜다. `VolleyMath.TickBurst`(머신거너 10연발)와 `SpawnNeedleCarrier` 를 그대로 재사용하면 되고, 실제 작업은 "버스트 상태를 어디에 두고 언제 틱하느냐" 하나다 → `docs/spec/dreamcatcher-payload-burst/`
- **방향탄 bounce 개통** [M] · 통통구슬×머신거너. `defender-directional-volley` 후속 후보의 사용자 결정("차단이 아니라 개통")이 살아 있다. 볼리 arm 이 bounce 필드를 적재 + `PathHit` pierce 소진 후 재조준 + `pierceCount>1` 합성 규칙.
- **`payload × trigger` 배선표** [M] · 현재 적용성 판정은 host 필터일 뿐이라 `AttackN × SelfTileAoe` 같은 조합이 통과한다(현 카탈로그엔 없음). `DcTrigger.GateComboSupported` 가 gate 조합에 하는 일과 같은 형태. critic 2종 모두 "별도 spec" 판정.
- **`FrontmostTarget × facing 유닛`** [S] · 붙지만 보너스가 inert(레인 타게팅이 우선순위를 덮는다). 경로 의존이 아니라 **타게팅 규칙 의존**이라 현재 지원 행렬로는 표현이 어색하다.
- **`Projectile_Shuriken_GA` 의 `flightMode` 미직렬화** [S] · 기본값(Homing) 의존이라 다음 사람이 대포를 ballistic 으로 오독한다. 명시 저장.

#### 손패 카드 시인성 (dreamcatcher-hand-card-face — 완료 2026-07-25)

손패 카드에서 아트를 걷고 타입색 헤더 + 대상 태그 + 효과 본문 구조로 교체(프로토 검증용 —
정식판은 원안 복귀 전제), 상단 툴팁은 조작 브리핑으로 전환했다.
상세: `docs/spec/dreamcatcher-hand-card-face/`.

- **아웃게임 카드 문법 통일** [M] — 덱빌더 그리드/덱 스트립/브라우저/상세 팝업을 같은 BG·태그
  문법으로. `CardCategoryStyle` 손패 함수(HandHeader/TargetTag)가 그대로 소스
- **실기기(Android) 폰트 가독 확인** [S] — 본문 18~24pt 는 에디터 Game 뷰 기준 검증. 태그 칩
  (10~15pt)이 최우선 확인 대상
- **PlayMode 기존 실패 6건 트래킹** [M] — 본 spec 검증 중 발견, spec 무관 판정(인증 서버 500 ·
  Gift 페이즈 진입 2건 · 씬 전환 · 덱 캐리인 0장 · CcEffect ECS 예외). 타 세션 in-flight 작업과
  대조 필요
- **태그 아이콘화 + 색약 팔레트 검증** [S~M] — 칩에 아이콘 병행(색약 대응 겸), 타입 3색 대비 검증

#### 점수 시스템 (battle-score-formula · score-tally-sequence — 둘 다 완료 2026-07-21)

최종 점수를 예산 소모 모델(시간+스트레스+킬)로 교체하고, 전투 종료 후 합산 연출을 붙였다.
상세: `docs/spec/battle-score-formula/`, `docs/spec/score-tally-sequence/`.

- **유출 한계 10 의 플레이 감각 미확인** [S] — 근거 없는 시작값. 조정 시 **한계×점당점수=예산**이라 `stressScorePerPoint` 를 짝으로 움직여야 한다 (battle-score-formula)
- **점수 재검증 / 무효 플래그** [L] — 서버가 배틀로그 입력값으로 재계산해 클라 제출값과 대조. 결정론적 재시뮬은 고정 타임스텝 도입이 선결 (battle-score-formula)
- **정예 등급 도입** [M] — 현재 잡몹 9종 + 보스 1종뿐이라 중간 등급이 없다. 밸런스 변경 (battle-score-formula)
- **계약 지불의 점수 손실 HUD 경고** [S] — 몽마의 계약 1회 = 900점인데 유출 카운터만 보이고 점수 손실 표시가 없다 (battle-score-formula)
- ~~Tally 흐름 PlayMode 테스트~~ → **완료** (`score-tally-sequence` unit 4). `TallyFlowTest` 2건. `onDone` 을 일부러 끊어 검출력까지 증명함
- **Tally 구간 무음** [S] — `SoundManager` 가 `phase == Battle` 에서만 BGM 유지라 4초 연출이 완전 무음. 축별 사운드와 함께 볼 것 (score-tally-sequence)
- **연출 카메라 동반 · 축별 사운드 · 신기록 갱신 강조** [S~M] — 상세는 spec README "후속 후보" (score-tally-sequence)

#### Next Wave 조기 호출의 시간점수 보상 (wave-pattern unit 9 이관, 2026-07-21)

unit 9 로 `Next Wave` 가 남은 웨이브 전체를 앞당기게 되면서 "빨리 불러 빨리 끝내기"가 실제로
성립한다. 사용자 인지 완료 — **밸런싱 영역이라 unit 9 스코프 밖으로 둔다.**

- **조기 클리어의 시간점수 보상 과다** [M] · `timeScorePerSecond: 100` × 180초 = 시간점수 예산
  18,000 으로 총 예산 37,300 의 약 48%. `ForceNextWave` 는 예정 시각 도달 여부를 검사하지 않아
  시작 직후 연타로 전 웨이브를 한꺼번에 쏟을 수 있다(연타 허용은 README 계약, unit 9 이전부터의
  동작). 조기 클리어가 지배 전략이 될 개연성이 높다. 제동 후보: 호출 쿨다운, "예정 시각 −N초부터만
  호출 가능" 게이트, 시간점수 상한 또는 곡선화. 어느 쪽이든 `stressScorePerPoint`·킬점수와의
  예산 균형을 함께 봐야 한다. (wave-pattern)

#### PlayMode 사전 실패 (이력: 07-21 3건 → 07-30 13건 → **2026-08-16 재측정 9건**)

> **2026-08-16 재측정 (test-suite-fast-lane unit 2 — 144건, 같은 날 2회 전체 실행 대조)**:
> 7월 13건 중 8건 해소. Gift stale 2건(Squad/DreamstoneCarryIn)·BountyMark·
> DreamcatcherEffect 2건은 gift-phase-removal 등으로 자연 해소됐고, 3건은 stale 기대값
> 갱신으로 해소했다 — DeckCarryIn(폴백 덱 0장, 사용자 결정 2026-07-15) ·
> CursedRelic·DreamCocoon(거부 경고가 «already has {payload.kind} state» 형식으로
> 바뀐 것 동기). `AuthE2ETest` 는 환경 의존(dev 서버 계정/스키마)이라 `[Explicit]` 로
> 기본 실행에서 제외 — Test Runner 직접 선택 시에만 돈다.

**남은 9건** (전부 원인 분류 완료 — fast-lane unit 2, 2026-08-16):

- **`PlacementAuraTest` 3건** [S] · 기대 1.0, 실측 **1.012** 로 일관. +1.2% 는 Common 최하
  티어 드림스톤 수치와 정확히 일치 — 다른 테스트의 프로필 스톤 장착이 새는 교차 오염 가설.
  격리 실행으로 가설 검증부터. (dreamstone-loadout / active-ally-zone 접점)
- **`SceneTransitionSmokeTest`** [S] · 순서 의존(7월 격리-통과 확인과 동일). OutgameScene
  활성 상태로 진입해 실패.
- **`DragCancelZoneTest`** [S] · 조준점(100px)·트레이 취소 존 기하 판정. 7월부터 지속,
  2회 실행 모두 재현. UI 개편으로 트레이 기하가 바뀐 stale 인지 제품 버그인지 판별 필요.
- **`DropDismountTest`** [M] · **신규**(7월 목록에 없음). 실행마다 증상이 다르다(1회차
  «commit frame: cell occupied» false → 2회차 InvalidCastException). 최근 배치/재배치
  계열 변경(defender-clock-out·relocation)과 접점 — 우선 조사 후보.
- **`OutgameFlowSmokeTest` · `WaypointRoutingLiveTest.DefenderCatalog_…` 2건** [M] · 실체는
  **PrimeTween «Tween's OnComplete callback was ignored» 에러 1개**(Sequence 1.32s)가 그때
  돌던 테스트에 임의 귀속되는 것(EntitiesAssetGC NRE 와 같은 패턴). gift-phase-removal 의
  트윈 풀·teardown 작업(`2e4aaf63`·`abf0115a`)과 시기 일치 — **그 spec 후속으로 라우팅.**
- ~~**`BossLullabyLiveTest`**~~ · **삭제 2026-08-17** (duel-live-focus). flaky 의 원인이 계측 창이
  아니라 «랜덤 매치 시드 위 emergent 타이밍 단언» 이라는 설계였다. 같은 이유로
  `InstinctNearestTargetMeasureTest`·`MapCrowdClearanceTest` 도 함께 삭제.
  ⚠ **군집 통과 교착의 회귀 가드가 이로써 없어졌다** — 되살릴 땐 결정론적 픽스처로.

**추가 5건 — 2026-08-17 전체 실행에서 새로 분류** (duel-live-focus 의 PlayMode 재측정.
전부 이 spec 과 무관하다는 근거를 함께 적는다 — 근거 없는 «무관» 은 다음 세션에서 다시 의심받는다):

- **`OnPlaceTauntNearbyTest.FlyingEnemy_IsNotTaunted`** [M] · 「비행 적이 근접 가디언에게 끌려왔다」.
  **맵을 Serpent 로 고정해도 재현**되므로 판 문제가 아니다. 오늘 확정된 «아틸러리·폭탄맨을 뺀 전
  방어유닛 `Path|Air`» 결정이 도발 대상 층 게이트를 통과시킨 것으로 보인다 — 그 작업으로 라우팅.
- **`SlimeSplitE2ETest`** [S] · 작은 슬라임 = 중간의 50%(125) 기대, 실측 150. **적 에셋은 git clean**
  (디스크 250/150 = 60%)이고 `SlimeSplitAuthoringTests`(디스크 직독)는 통과한다 — 즉 시트↔SO
  드리프트이거나 기대값 stale. 밸런스 쪽으로 라우팅.
- **`StructureLivePlayTest.SiegeMap_DerivesSpawnFromEnemyCore_AndWavesComeFromIt`** [S] · `spawns.Length`
  1 기대, 실측 2. `StructurePlacements.SiegeSpawnOffsets` 가 2개(하단·상단)이므로 **코드상 항상 2** —
  `siege-lane-spawn` unit 0 이 파생 스폰을 2개로 바꿀 때 갱신되지 않은 stale 단언.
- **`StructureLivePlayTest.Structures_BootOnDevMap_SpawnBlockAndSurviveConnectivity`** [S] · 거점 프랍 0.
  `MapDocument_Test.asset` 이 **다른 세션의 미커밋 편집**으로 `structures:` 블록이 통째로 삭제된 상태다
  (`git diff` 확인). 그 세션의 작업이 커밋되면 자연 해소 또는 그쪽에서 갱신.
- **`WhirlpotLiveRepro.Whirlpot_SustainsDps_NotJustOneHit`** [M] · 6초 144 피해(24.0 DPS) < 저작 152.
  맵 고정 후에도 남는다 — 회오리 연타 성사율 문제. (elite-whirlpot)
  ※ 같은 파일의 다른 2건(`TakesNoDamage`·`WalksIn_ThenEngages`)은 **맵 미선언**이 원인이었고
  `PinMap` 으로 해소됐다.

**추가 15건 — 2026-09-02 전체 실행 219건 재측정** (dreamcatcher-attach-range-preview 검증 중. **격리 A/B 로 귀속 완료**:
같은 그룹을 0a 이전 코드와 이후 코드로 각각 돌려 아래 15건은 **양쪽에서 동일 실패** → 그 spec 무관. 0a 귀속 2건
`OnPlaceBindNearbyTest` 는 픽스처를 원 계약으로 고쳐 통과. 증상은 배치 실패·초과 피해(44·80 — 기본 공격이 더미에
닿음)·프로필 스톤 ×1.012 로, `distance-based-range` 의 2×2 몸 반경(도달 +0.75) 이후 PlayMode 전체가 돌지 않은 것으로 보인다.
원인 분류는 미완 — 각 spec 소유자가 판독할 것):

- **`AbilityAreaShieldTest`** [S] · 「실드셔틀 배치」 false — 배치 가능 셀 없음(맵/판형 의존?). (shield-guardian-defender)
- **`AbilityBombManBarrelTest`** [M] · 길막 설치물 0개 — 투척→착탄→해저드 큐→드레인 사슬. (bomb-thrower-defender)
- **`ActiveAllyZoneTest.Zone_BuffsAlliesInside_NotOutside`** [S] · 「place scout adjacent」 false. (active-ally-zone)
- **`BossThresholdSelfAoeTest.ThresholdNova…`** [M] · 기대 540, 실측 500 — 자폭이 두 번 또는 다른 피해원. (boss)
- **`DefenderRetireTest.Retire_WithOnRetireCard_DropsMeteorOnVacatedCell`** [M] · 인접 더미 피해 0. (defender-clock-out)
- **`DreamcatcherKillThresholdTest`** 2건(CorpseBurst·EmberField) [S] · 전제 「방어유닛이 적을 처치」 불성립.
- **`OnPlaceBoostNearbyTest`** [S] · 「반경 밖 아군 배치」 false. · **`OnPlaceDotNearbyTest`** [M] · 총 피해 127(기대 ~70).
- **`OnPlaceMeleeBurstTest`** 2건 [M] · 피해 0 저작인데 44 · 70 기대에 114 — 기본 공격 44 가 더미에 닿는 형태.
- **`OnPlaceSkyStrikeTest`** 2건 [M] · 200 기대에 280 · 적 2기에 미사일 3발.
- **`OnPlaceTauntNearbyTest.TauntedEnemy_WalksTowardTheBastion`** [M] · 도발된 적이 이동하지 않음(2.00 → 2.00).
- **`PatrolDefenderPlayTest.Summoner_SpawnsOnePatrol…`** [S] · 스폰 위치 (1,0) 기대, (2,1) 실측.
- (기존 항목 재확인) `DragCancelZoneTest`·`DragPlacementReachTest` 는 `ResolveFocusAndTarget` 리플렉션 인자 수 불일치 —
  컨트롤러 시그니처 변경에 테스트가 못 따라온 것. `AuthE2ETest` 는 `[Explicit]` 인데 전체 실행에 포함돼 돌았다.

> **배치 실행(`-batchmode -nographics`)으로 재면 부풀어 보인다** —
> `EntitiesAssetGC.GetAdditionalRoots` NRE 가 GC 타이밍에 터져 임의 귀속된다.
> **PlayMode 판정은 에디터 실행으로 한다.** 배치는 EditMode 전용으로 쓸 것.

#### 첫 판 튜토리얼 개선 (first-session-tutorial units 10~12 이관, 2026-07-21)

- **신규 동작의 테스트 커버리지** [S] · `ClassHint` 전이(`OnPlacementCommitted → ClassHint →
  ContinueTapped → Start` + 12초 폴백)와 `AwakeningGaugeView.SetSuppressed`(=`SetActive` 직접 호출로는
  못 잡는 회귀)에 자동 테스트가 없다. `TutorialDragGuidanceTests` 는 레이아웃 헬퍼만, PlayMode 스모크는
  `UnlockTutorialStart()` 를 스스로 불러 ClassHint 를 통과하지 않는다. (first-session-tutorial units 10~12)
- **클래스 안내 문구 정합성 3건** [S] · 전부 사실 확인 완료, 문구는 사용자 작성본이라 보류.
  ① 배치 트레이 role 배지가 `원/수/근/술/보` 단일 글자라 안내문 클래스 이름과 겹치는 글자가 0개 —
  읽어도 트레이에서 대응시킬 앵커가 없다. ② `적 이동경로에 방해물을 설치` 는 캐스터 4종 중
  `BlockingCaster` 하나에만 맞다(나머지는 장판, 기본 스쿼드에 Blocking·Fire 둘 다 포함).
  ③ 표기 4중 드리프트 — 배지 `보` / `UnitLabels.ClassLabel` `서포트` / 유닛 desc `서포트` / 안내문
  `서포터`. 게임 desc 는 `어그로` 대신 `도발` 을 쓴다. (first-session-tutorial unit 11)
- **서포터가 첫 판 스쿼드에 없다** [S] · 기본 스쿼드는 카탈로그 앞 7개(Archer·Bastion·BlockingCaster·
  Bruiser·Cannon·FireCaster·Guardian)이고 유일한 Support 인 Healer 는 8번째다. 첫 판 트레이에 `보` 배지가
  한 칸도 없는데 안내문은 서포터를 설명한다. 안내문에서 빼거나 기본 스쿼드를 바꾸는 두 방향. (unit 11)
- **온보딩 총량** [M] · 로비 챕터A(2탭) → 첫 판(목표·배치·클래스 6줄·시작) → 선물 홀드 2회 →
  로비 챕터B(2탭) → 2판 각성 3단계 = 안내 14비트·22줄. 게임플레이를 만들지 않는 순수 해제 탭이 5회다.
  체감 후 뺄 것을 정한다(후보: 클래스 안내, 각성 0단계, 선물 2번째 홀드). (units 10~12 리뷰)
- **첫 판 각성 봉인의 첫인상 리스크** [S] · 첫 세션 전체(로비 A → 첫 판 → 결과)에서 "드림캐쳐" 라는
  단어가 한 번도 등장하지 않는다. 첫 판 이탈자는 게임의 차별점을 영영 못 본다. 대안은 "숨김" 대신
  "보이되 침묵"(버튼·게이지는 노출, 힌트만 억제) — 채택 시 unit 12 의 0단계가 불필요해진다. (units 10·12 리뷰)

#### 아웃게임 튜토리얼 (outgame-tutorial 종료 이관, 2026-07-21)

- **Android 실기기 QA** [S] · 노치·safe area dim 커버리지, Android 백키로 안내가 닫히는지(`Keyboard.current` 가 null 인 기기에서 미동작 가능 — 기존 `DreamcatcherHandView` 와 같은 제약), dim 톤 `UiOverlay.Dim` 알파 0.92 가 로비 배경 위에서 과한지. 링/홀 정렬은 실측 완료라 제외. (outgame-tutorial)
- **챕터 B 게이트를 독립 신호로 교체** [S] · 현재는 인게임 core 튜토리얼 완료 플래그를 재사용하는데, 그 실제 의미는 "core 튜토리얼이 발동하고 Battle 페이즈에 도달했다"다. `FirstSessionTutorialController` 의 fail-open 경로(참조 누락·affordable 슬롯 부재)를 탄 플레이어는 전투를 몇 판 하든 **챕터 B 를 영원히 못 본다**. 매치 카운트 같은 독립 신호가 의미에 맞다. (outgame-tutorial)
- **`SQUAD`/`DREAMCATCHER` 버튼 라벨 한글 통일** [S] · 좌측 열 4개 중 둘만 영문이라, 한국어 안내 문구("스쿼드와 드림캐쳐")를 읽고 라틴 라벨로 대응시켜야 한다. 같은 줄의 `프리셋`/`히스토리` 는 한글이라 잘못된 그룹핑도 유발한다. 로비 레이아웃 스펙 범위. (outgame-tutorial)
- **온보딩 인지 지표 관측** [M] · 현 spec 은 노출과 실행만 보장하고 인지는 검증하지 않는다. `2번째 판 시작 시 안내 없이 START 도달`, `첫 복귀 이후 세션에서 스쿼드/덱 패널 1회 이상 열기` 같은 사후 관측 지표가 필요. (outgame-tutorial)
- **챕터 A `IntroFocus` 에 대상 사전 검사가 없다** [S] · 다른 포커스 단계와 달리 `startButton` 이 null/비활성이어도 열려서 "구멍 없는 풀 dim + 8초 잠금" 경로가 살아 있다. units 11~13 이 dim 탭 종료를 없애면서 `ShowFocus` 의 "구멍 없이 표시" 폴백이 더 이상 탈출구가 아니게 됐고, 나머지 스텝은 `TryEnterFocusStep` 의 사전 검사로 그 경로를 막았다. A 만 남았다. (outgame-tutorial units 11~13 이관, 2026-08-02)
- **패널 닫기가 막히는 배선 경로** [S] · 스쿼드 페이지가 dirty 인데 `confirmPopup` 이 미배선이면 닫기가 LogError 로 차단돼(`SquadCharacterPageController.cs:152-157`) 로비 복귀 훅(`ClosePanels` → `OnLobbyShown`)이 안 돌고 시퀀스가 그 자리에 선다. 배선 버그 한정이지만, 로드아웃 시퀀스가 패널 왕복 2회로 늘어 노출 면이 커졌다. (outgame-tutorial units 11~13 이관, 2026-08-02)
- **로비 구간 온보딩 총량** [M] · 재편으로 로비가 2스텝 → 4스텝 + 패널 왕복 2회가 됐다. 아래 "첫 판 튜토리얼 개선" 의 **온보딩 총량** [M] 항목과 같은 문제이고 그만큼 급해졌다 — 체감 후 뺄 것을 정한다. (outgame-tutorial units 11~13 이관, 2026-08-02)

#### 어그로 이동/클램프 (aggro-tile-chase 종료 이관, 2026-07-20)

- **cell-trim wall-slide** [S] · `ClampToBoundary` 가 양 축을 함께 clamp 해, 대각 desired 의 목적 셀이 벽이면 합법인 축 진행까지 취소된다(코너에서 넉백/외력이 흡수됨). 축 분해(x-only → z-only) 슬라이드로 완화. 어그로 경로는 cardinal 이라 무영향 — 잔여 노출은 impulse/tornado 품질. (aggro-tile-chase C1)
- **대각 코너 슬립 차단** [S] · 직교 이웃 둘이 벽이고 대각만 walk 면 모서리 틈으로 새어나간다. 신맵처럼 코너 많은 지형에서 시각적 노출 가능. (aggro-tile-chase C3)
- **동적 해저드의 chase 경로 무효화** [S] · chase field 는 획득 시 1회 계산이라, blocking hazard 가 나중에 경로를 막아도 재판정하지 않는다(해저드는 일시적이라 현 scope 밖으로 뒀음). 재빌드 트리거 또는 하강 실패 시 해제 규칙 필요. (aggro-tile-chase)
- **`aggroAttackRange` 데이터 결정** [S] · 현재 전 적 1 고정이라 "통로에서 2칸 밖 가디언"은 어그로가 안 걸린다(사양). 탱커를 더 멀리서도 끌려면 2로 올리거나 유닛별 분화 — 밸런스 결정. (aggro-tile-chase × aggro-standoff 승계)

#### 수동 맵 authoring (manual-map-authoring 종료 이관, 2026-07-19)

- **맵 authoring 에디터 툴화 + 분류값 재계산** [M] · 현재 레시피는 execute_code 로 road 배열→검증→WriteToDocument. 툴로 승격하면서 mergeDegree/chokepoint 를 저장값이 아니라 **로드/임포트 시 CellClassifier 로 재계산** — 수작업 값과 분류 정의의 조용한 드리프트 차단. (manual-map-authoring)
- **스테이지별 맵 운영** [M] · MapDocument 여러 장 + 스테이지→document 매핑. 현재는 ArkFunnel 1장 고정 배선. (manual-map-authoring)
- **시드 권한 일원화** [S] · fixedMapSeed(BattleBridge, 비0 코드 기본값)와 GameManager.debugFixedMatchSeed 이원화 정리. 비0 코드 기본값은 씬 저장 시 베이크되는 함정 + document 배선 중엔 무효인 유령 노브. 맵 빌드 로그에 시드 provenance(document/fixed/derived) 1줄 추가. (manual-map-authoring)
- **MapDocument 로드 방어** [S] · ToGeneratedMap 이 보조 배열(mergeDegree/chokepoint/propLayerId) 길이를 미검증 — 손상 에셋이 IndexOutOfRange 로 배틀 init 중단. PickGridSize 의 `math.abs(int.MinValue)` 음수 인덱스 경로도 동반 수리. (manual-map-authoring)
- **맵 설정 패널 잔여 위생** [S] · 패널이 표현 못 하는 mapSource(Manual/Fixture/Procedural_Legacy) 시 하이라이트/섹션 오표시, document 배선 중 크기/goalEdge 컨트롤 무피드백 no-op, SetGoalEdgeOnly 의 공유 SO 에셋 쓰기(Play 정지로 안 되돌아감). (manual-map-authoring)

#### 효과 트리거 통합 — 드림캐쳐↔기믹 (아키텍처, 파킹 2026-07-15)

- **트리거→효과 엔진 도메인 중립화** [M] · 드림캐쳐의 `DcMechanic`(이미 데이터주도 trigger→payload 중립계약)을 `Dc*` 명칭·`Data/Dreamcatcher/` 위치에서 떼어내 공용 `TriggerEffect*` 로 승격 + `EffectDomain{Dreamcatcher,Gimmick,...}` 태그로 소비처(오라/UI/dispel/밸런스) 구분. 기믹 Fatigue/Pickup/LastRun 을 그 위 rule 로 이관. **당장 안 함** — 논의·설계는 `docs/plans/2026-07-15-effect-trigger-unification-design.md` 에 기록. 권고: 0~1단계(태그+rename)만 저위험 선행, 시스템 이관은 2번째 기믹 생길 때(제약 8). rename 은 공유파일 광범위 → 세션 조율 필수.

#### 방향 지정 배치 · 다연발 (defender-directional-volley 종료 이관, 2026-07-17)

- **배치 취소/코스트 환불** [S] · 공격방향 페이즈엔 취소 제스처가 없다 — 드롭 = 코스트 확정. 데드존 릴리즈는 가이드를 유지하고 재스와이프를 기다린다. 취소를 넣으려면 `TryBeginDefenderDeployment` 의 spend 를 되감는 경로가 먼저 필요 — `drag-cancel-affordance` 는 커밋 **이전**에만 갈라져서 환불이 필요 없었고(계약 1) 그래서 이 페이즈를 범위 밖으로 뒀다. (defender-directional-volley × drag-cancel-affordance)
- **배치 후 방향 재지정** [S] · 유닛 탭 → 가이드 재오픈. `DeployedFacing` 은 현재 1회 기록 후 불변 계약이라 쓰기 소유권부터 재정의해야 한다. (defender-directional-volley)
- **레인 폭 파라미터화** [S] · 현재 1타일 고정(`LaneMath.IsInLane` 의 `side == 0`). 폭 2+ 는 side 허용치를 SO 로. (defender-directional-volley)
- **target-bound 투사체 wind-up 발사 보장** [M] · 일반 호밍 투사체는 RESOLVE 시 타깃을
  재판정하므로 wind-up 중 유일한 타깃이 죽거나 이탈하면 START 모션 뒤 투사체가 생략된다.
  방향탄은 `projectile-shot-sequence`에서 해결됐고 머신거너·폭탄맨은 구조상 노출되지 않는다.
  일반 투사체까지 바꾸려면 START 타깃 커밋·재타겟·빗나감 중 정책을 별도 spec에서 결정한다.
  (projectile-shot-sequence 후속)
- **버스트/스프레드 × Homing·Ballistic 조합** [S] · 볼리는 전 궤적에 열려 있으나 e2e 는 Directional 에서만 했다. Homing×버스트는 발마다 타겟이 재평가되지 않고 템플릿 스냅샷을 쓴다는 점 확인 필요. (defender-directional-volley)
- **머신건 연사음** [S] · 버스트 캐리어 발은 `DefenderUnitTag` 가 없어 drain 의 발사 SFX 게이트 밖 → 볼리당 1회. rat-tat-tat 을 원하면 battle-audio 쪽 게이트 재설계. (defender-directional-volley)
- **방향 가이드 정식 아트 + 머신건 아트** [S] · 가이드는 절차적 보드 스프라이트(레인 점등 + 삼각 화살표, unit 9), 유닛은 Marksman Spine + Sniper 파츠 플레이스홀더(guid 유지 교체 전제). (defender-directional-volley)
- **곡사 방향 발사** [S] · `DirectionalLinear` + arc 시각. 현재 방향탄은 평면 직선(sim-Y 없음). (defender-directional-volley)
- **tap-to-place 경로 연동** [S] · 공격방향 페이즈 진입점이 D&D `CommitPlacementAt` 하나뿐 — tap 확정 경로에도 같은 핸드오프가 필요하다. (defender-directional-volley × defender-tap-to-place)
- **bounce(통통구슬)×방향 유닛** [S] · 현재 통통구슬(`ProjectileBounce`)이 방향 유닛에 붙어도 inert(부착 가드는 ProjectileRef 만 보고 movement 무시 → 슬롯은 붙지만 Directional arm·PathHit 이 bounce 를 안 탐). **사용자 결정: 차단이 아니라 트리거당 N발(버스트/스프레드 캐리어 포함)이 각각 bounce 를 받아 진행**(각 방향탄이 경로 히트 후 다음 적으로 튕김). 과제 = 볼리 arm 이 bounce 필드를 template/캐리어에 싣기 + PathHit 이 pierce 소진 후 bounce 재조준을 지원(또는 bounce×pierce 합성 규칙 정의). 집계(count 합/range max/mul 곱)는 기존 재사용. (defender-directional-volley × dreamcatcher-attack-mod-bounce)

#### 로드아웃 규칙 위생 (game-start-loadout-gate 종료 이관, 2026-07-16)

- **스쿼드 저장 가드** [S] · 드림캐쳐 덱은 사용자 편집마다 즉시 저장하고 `LoadoutGate` 에서 출전만 판정한다. `SquadBuilderView.OnSave` / `SquadCharacterPageController.Save` 에도 `IsLoadedThisSession` 가드를 넣어, 직접 씬 실행 때 비어 있는 프로필을 저장하지 않도록 한다. (dreamcatcher-deck-autosave)
- **낡은 주석 정정** [S] · `PlayerProfile.cs:112` 의 "exactly 10, unique<=2" 중 **덱 크기 10 은 라이브와 일치**(현 `DeckRuleConfig_Default.asset` `deckSize=10` — 8→10 복귀), **"unique<=2" 만 stale**(라이브 `maxSquad=-1` → 타입캡 없음). **`DeckRules.cs:5-10` 은 건드리지 말 것** — 정확할 뿐 아니라 카탈로그 null → 폴백 10 함정을 경고하는 유일한 문서다. (game-start-loadout-gate · 2026-07-20 덱=10 확정 정정)
- **`7` 이중 하드코딩** [S] · `SquadSave.SlotCount` 와 `SquadDraw.FieldCount` 가 독립 상수다. 한쪽만 바꾸면 조용히 어긋나고 요구치가 도달 불가능해질 수 있다 — `LoadoutGate` 는 `min()` 으로 방어만 해뒀다. (game-start-loadout-gate)
- **`DreamcatcherDeckBuilderView.cs:45 DeckColumns = 10`** [S] · 현재는 라이브 `deckSize=10` 과 일치해 증상 없음(8장 시절의 셀 폭 불일치는 해소). 다만 `DeckColumns` 가 `EffectiveDeckSize` 를 읽지 않는 하드코딩이라 다음 deckSize flip 때 재발한다. (game-start-loadout-gate · 2026-07-20 덱=10 확정 정정)
- **`UiOverlay.Dim` 톤** [S] · alpha 0.92 는 전체화면 takeover 용이라 "유닛 2명 부족" 안내 팝업엔 무거울 수 있다. 공유 상수라 단독 spec 에서 바꾸지 않았다 — 조정하려면 전 팝업 영향 확인 필요. (game-start-loadout-gate)

#### 나이트매어 보스 스킬 (nightmare-whip-aura 종료 이관, 2026-07-12)

- **defender-side 오라 카드** [S] · `AllyMoveSpeedAura` 는 arm·오라 연출 모두 진영/kind 중립 — 카드 데이터 선언만으로 성립. 카드 taxonomy/밸런스는 product 결정. (nightmare-whip-aura)
- **채찍질 전용 연출 고도화** [S] · 현 WindAura 재사용 → 전용 채찍 스윙/버프 링 저작(unity-vfx-authoring). 발동-순간 원샷 arm(`payload.projectile`)도 SO 게이트로 잔존해 조합 가능. (nightmare-whip-aura)
- **수치 실측 튜닝** [S] · 펄스 0.5s/TTL 0.6s/+20%/3타일/오라 scale 1.2 — 전부 SO, 밸런스 실측 후 조정. (nightmare-whip-aura)
- nightmare-catcher 잔여 후속(기본공격 원거리화 · 위협 감쇠 · GA hitPrefab 전수 정비 · 폭격 피격 체감)은 `docs/spec/nightmare-catcher/README.md` 후속 후보 참조. **보스 어그로 면역은 `docs/spec/boss-jjangssen/3_boss_immunity.md` 로 승격(2026-07-29)** — 부착 1곳 차단 + 직접 행동정지·넉백 면역까지, `BossTag` 전체 적용(나이트메어 포함).

#### 보스 방어유닛 지향 이동 (헌터 재구현, 2026-07-11)

- **defender field dirty-skip 최적화** [S] · 방어유닛 셀 집합 불변 시 매 프레임 BFS 재빌드 skip. 현 그리드(20x10)에선 무의미 — 대형 그리드/프로파일 압박 시. (boss-defender-field, ecs-review M2)
- ~~**ecs-reviewer 채널 목록 stale**~~ — **완료 2026-07-11**. 재발 방지를 위해 목록 사본 자체를 제거 — 에이전트 정의가 CLAUDE.md § "ECS 맥락 분리"(source of truth) + 코드 실측 grep 을 가리키도록 변경. 코드 실측 18개 = CLAUDE.md 일치 확인. (boss-defender-field, ecs-review M1)

#### 유닛 상태 표현 / 인디케이터 (aggro-targeting 파생, 2026-07-09)

어그로 아이콘("!")을 만들며 드러난 일반화. **두 축으로 분리** — "느끼게 할 상태 연출" ↔ "훑어볼 정보 배지". 순차 진행 예정.

- ~~**상태별 프리팹 연출 인프라 (unit-status-fx)**~~ — **완료 2026-07-09** (`02a9db24`). `AggroIcon*` → `StatusFx*` 일반화: `StatusFxKind` + `StatusFxRegistry`(상태마다 프리팹) + `StatusFxSpawner`/`View`. 어그로 이관(현 "!" 폴백 유지). **잔여**: 실제 상태(스턴/빙결/독) registry 등록 + ECS 소스 훅, 어그로 전용 프리팹 연출(가디언 tether 등). (unit-status-fx)
- **모디파이어 인디케이터 스트립 (unit-modifier-indicators)** [M] · 버프/디버프(`ModifierStats` 델타·DoT 스택 Fire/Ice/Bleed/Poison)를 머리 위 아이콘 행으로. 스택/듀레이션 뱃지 + `+N` 오버플로. 상태 연출과 **다른 축**(정보 vs 느낌). 한 상태가 둘 다일 수 있음(예: 독=온-바디 VFX + 스택 아이콘). ~~드림캐쳐 부착 표기~~ 는 `docs/spec/unit-dreamcatcher-icons/` 로 분리 완료(2026-07-12) — 인디케이터 뷰 설계 시 해당 스트립과 y-오프셋/레이아웃 공존 고려. (aggro-targeting)

#### 곡사포 / 투사체 후속 (artillery-defender, projectile-trajectory-payload)

곡사포 유닛 완료(→ Promoted). 남은 후속:

- ~~**신규 유닛 프로필 reconcile**~~ [해결 2026-07-06] · 유닛을 프로필-소유(`ownedUnitIds`)에서 아예 제거하고 SquadBuilderView 가 `DefenderCatalog` 를 직독 → 모든 유닛 상시 오픈, 신규 유닛 자동 노출. 유닛 수집/가챠 도입 시 재검토(그때 소유 개념 부활). PlayerProfile.ownedUnitIds 삭제(JSON back-compat: 구 필드 무시).
- **slow-곡사포 / 임팩트 CC / arcHeight 거리비례 / 전용 Spine rig** [S/M] · artillery-defender 후속.
- ~~**Meteor→TileAoe 수렴 + GA 낙하 비주얼**~~ → `docs/spec/projectile-trajectory-payload/` units 7~9 **완료(2026-07-06)** — 레거시 3파일+큐 삭제(채널 15→14), Rock02 낙하+Hit_Rock03 파편, 스킬 aim/텔레그래프 격자 통일은 `placement-attack-range-preview/3_skill_aim_range.md`.
- **Bezier 궤적 / non-Damage payload / Homing+TileAoe** [S/M] · projectile-trajectory-payload 엔진 확장 후속.
- **적별 피격 반경(per-target hit radius)** [S] · 투사체 충돌은 현재 `투사체.hitThreshold` vs 적 **중심점 하나** 판정(`SweepHitMath.SegmentHits` / `ProjectileMoveSystem` HomingToEntity 도달). 적 SO엔 자기 hurtbox 필드가 없어(있는 건 `attackRange`=자기 공격 사거리뿐), `spineVisualScale 3.2` 보스처럼 몸 큰 유닛은 큰 몸통을 눈으로 관통해도 무판정 → 머신거너 등 직선 탄이 시각적으로 안 걸림. **제안**: `AttackUnitData.hitRadius` 필드(기본 0=현행 점 판정) + 스폰 시 `HitRadius` 컴포넌트 bake(1줄) + 충돌 2곳에서 `유효반경 = hitThreshold + target.hitRadius` 합산 + EditMode 1. 보스만 크게(≈1.2), 일반 적은 0으로 바이트 무회귀. **임시 완화**: `Projectile_MachineGunBullet.hitThreshold` 0.4→0.7(전 적 대상 균일 확대라 3.2배 보스는 여전히 부분 관통 — feature 착수 시 0.4 복귀 검토). projectile-trajectory-payload 엔진 확장. (nightmare-catcher 보스 피격 체감 관련)

#### 적 스폰/이동 비주얼 (enemy-spawn-positioning)

스폰 위치 개선(완료 2026-06-29, units 0~4) 후 남은 항목.

- **적 타일 이동 무결성** → `docs/spec/enemy-tile-movement-integrity/` (완료 2026-06-29 — `movement-lane-centering` 리프레임). 결함 3종: 코너 엣지-허깅 복원(target=0+deadband) · aggro 타일 제약 · 결정론 스폰. 레인 대형 시스템(II) · QuadUnit 뷰 누수는 후속 후보.
- **Quad 폴백 visualOffset 배선** [S] · Spine 없는 적의 `QuadUnitView` 경로에 `AttackUnitData.visualOffset` 전달. 현재 미배선(적=Spine 라 무영향).
- **유닛 간 separation/boid** [M] · 겹침 동적 해소(스폰 분산과 별개로 행진 중 밀집 완화).
- **블록 시 우회 재라우팅** [M] · 복도 차단 시 `BuildFlowField` rebuild 트리거(walk 마스크에 blockedCells 반영). flow field 유지 결론(유닛별 BFS 아님). 이동 아키텍처 별도 스펙.

#### 점수 HUD 타격감 (score-hud-impact-upgrade)

점수 HUD 임팩트 업그레이드 **완료(2026-07-07, units 0~4)** — 탄성 슬램/골드 아이덴티티/Kanit 폰트·골드 스파클 버스트·발광+샤인·패널 킥/마일스톤 플래시(Play 통과) + SoundManager 처치 틱(ElevenLabs `ScoreTick`, 피치 상승). 상세: `docs/spec/score-hud-impact-upgrade/`.

- **연속처치 heat · 킬 위치 "+N" 플로팅 · 진짜 URP Bloom · SFX 다양화** [S~M] · 상세는 spec README "후속 후보".
- ~~적별 차등 점수~~ → **완료** (`score-tally-sequence` unit 0 — HUD 가 유닛별 `killScore` 를 쓴다).
  ~~콤보 배수 스코어링~~ → **기각** — `battle-score-formula` 계약 10 이 콤보 배율을 금지한다.
  마일스톤 플래시는 누계 기준 → **1초 내 300점 순간 화력** 기준으로 교체됨.

#### 체력 표기 (unit-health-display)

적/방어유닛 체력 표기(완료 2026-07-04, units 0~3 — 적 피격 마이크로바 + 저체력 틴트, 방어유닛 타일 테두리 게이지, 투트랙 리뷰 반영). 상세 후속: `docs/spec/unit-health-display/README.md`.

- **킬 포어캐스트 마크** [M] · `IncomingDamage` + 비행 투사체 예약 데미지 ≥ 잔여 HP 인 적에 스컬 마크. 바가 못 주는 의사결정 정보. 투사체 데미지 귀속 필요.
- **체력 표기 poll 효율화** [S] · `SyncMonoUnitViews` 가 매 프레임 적/방어유닛 `Health` 조회 + 뷰 write(틴트/게이지). entity/cell→last-ratio 캐시로 skip. 비블로커(유닛 수 그리드 상한).
- **타일 게이지 시각 폴리시** [S] · fill inset `pad=0.18` SO화, 코너 조인트 갭 보정, 4-edge 계단식 → 연속 SDF 셰이더 교체.
- **hazard 체력 표시 / 상태이상 틴트 합성 / 웨이브 압력 게이지 / 보스 상시 바** — unit-health-display README 후속 후보.

#### 배치 프리뷰 / 범위 (placement-attack-range-preview, placement-drag-preview-polish, keyring-cord-preview)

드래그 배치 UX(공격범위 격자 표시 + 프리뷰 sway → **키링화** 완료 2026-07-05) 후 남은 항목.

- **배치 스킬 범위 표시** [M] · `onPlaceRange`/`hazardCastRange` 를 다른 색 채널로. 웜(공격)/쿨(스킬) 색코드 + 채널별 펄스 위상차, 필요 시 border 타일. 2번째 색 채널 시점에 `EnsureRangeTilemap`/펄스 로직 파라미터 추출. (range-preview)
- **Guardian 어그로 반경 시각화** [S] · `aggroRange` 를 또 다른 표기로(공격 범위와 별개 성격). (range-preview)
- **이미 배치된 유닛 선택/탭 시 범위 표시** [S] · 현재는 드래그 중만. (range-preview)
- **키링 중력 드롭 방식** [S] · 움직일 땐 유닛이 손가락에 붙고, 멈추면 중력으로 툭 떨어져 매달리는 물리감(사용자 제안, 현 스프링 follow 의 대안). (keyring)
- **키링 고리/줄 실제 아트** [S] · 현재 절차적 원 링 + 단색 LineRenderer. 금속 링/체인 스프라이트로 스왑. (keyring)
- **줄 sag 곡선** [S] · 현재 2점 직선. 정적 catenary 곡선으로 끈 느낌. (keyring)
- **배치 유닛 idle sway** [S] · 현재 sway 는 드래그 프리뷰 전용. 배치된 유닛의 상시 미세 흔들림. (drag-preview)
- **드롭 bounce / 세로·전후 흔들림** [S] · 드롭 착지 반동·다축 진자. (drag-preview)
- **fallback capsule 프리뷰 sway** [S] · Spine 없는 유닛 경로(현재 스킵, 키링 미적용). (keyring/drag-preview)

#### Modifier framework — Producer 확장 (modifier-framework-and-healer)

framework 코어 변경 0. 새 producer 레이어 추가로 다양한 효과 적용 경로 확보. producer-agnostic 설계 검증 시점.

- **Aura defender** [M] · 지속 영역 효과 producer (`AuraOutput[]` + `AuraApplySystem`). 일정 반경 ally 에 매 프레임/N초마다 StatModifier 발화.

#### Modifier framework — 내부 보강 (modifier-framework-and-healer)

framework 코어/UX/테스트 보강. 콘텐츠 확장 전후 모두 가치 있음.

- **Modifier UI 시각화** [M] · defender HUD + 적 머리 위 활성 modifier 아이콘 표시. ModifierStats / Slot buffer read-only 구독. UI 리소스 의존.
- **Dispel/Cleanse 채널** [S] · ModifierBuffer 슬롯 제거 채널 (kind/source 기반). CombineOp 별 면역 정책. 콘텐츠 디자인 선행.
- **Testability — Stack threshold dispatch** [S] · `BattleBridge._stackThresholds` 에 test 주입 API 또는 `IStackThresholdRegistry` 인터페이스 도입. skipped Test 3 활성화 목적.
- **Testability — AttackSystem outputs dispatch helper 추출** [S] · `OnUpdate` 의 4-way 분기를 `static ProcessAttackOutputs(...)` 로 추출. skipped Test 4 활성화 목적.
- **추가 EditMode 회귀 테스트** [S] · Stack threshold edge (5→6→5 재발화) / Consume 모드 stack 차감 / IncomingHeal drain Clear / RegenPerSec 누적. Testability 보강과 합쳐 진행.

#### Enemy 콘텐츠 / 비주얼 (enemy-unit-development)

신규 적 3종 + Tanker Spine 전환 후 남은 검증/콘텐츠 작업.

- **PlayMode 밸런스/시각 검증** [S] · Rootcaster 공격 후 1초 pause, Needler 빠른 투사체 연사, Runner 과속 체감, Tanker BellKnight Spine 크기/정렬 — 실기 확인 후 SO 값 튜닝.
- **적 projectile VFX 분리** [S] · 현재 defender projectile prefab + tint/scale 재활용. enemy variant prefab 또는 material variant 분리. 적/방어 투사체 식별성 개선.
- **WavePatternGenerator unit weight 지원** [S] · 현재 균등 확률. `AttackDeck.attackUnitPool` 반복 참조 또는 weight 필드 도입. Runner/Needler 과다 출현 회피.
- **Enemy attack animation event 일반화** [S] · `DefenderAttackEvent` → `UnitAttackVisualEvent` 로 일반화. `SpineUnitPool.NotifyAttack(entity, target)` 으로 적 공격 애니메이션 트리거 연결.

#### Hazard caster — 확장 (hazard-caster-defenders)

hazard caster defender 4종 MVP 이후 남은 확장 후보.

- **footprint sampler** [S] · `SampleRect(center, width, height)` 구현. HazardCastSystem 의 width/height 고정을 제거하고 rect 범위 multi-cell spawn 지원. 콘텐츠 디자인 선행.
- **target priority 정책** [S] · 현재 nearest(world distancesq). first-path-progress / random policy 추가. `HazardCastState.targetPriority` 필드 도입.
- **cast warning VFX / tile preview** [S] · cooldown 직전 타겟 셀 하이라이트 또는 파티클. BattleBridge drain 에서 visual hint 생성.
- **same-frame hazard 효과** [M] · 현재 next Simulation tick 적용. ECS 내부 drain 으로 이동 시 같은 frame 적용 가능. `HazardLifetimeSystem` 순서 재편 필요.
- **DefenderCatalogSO** [S] · 씬 레벨 draft catalog 수동 배선 대신 공유 `DefenderCatalogSO`로 통합. roster 증가 시 씬 배선 brittle 해질 때.

#### CC / Obstacle 확장 (cc-pipeline-and-obstacle)

- **큐브 spawn 게임 통합** [S] · 디펜더 능력 / 스킬 카드에서 `BattleBridge.SpawnObstacle` 호출. 현재는 디버그 메뉴만 진입점.
- **Obstacle 시각 Presenter** [S] · `ObstaclePresenter` MonoBehaviour, mesh/particle. 현재 큐브는 시뮬만 있고 렌더 없음.
- **추가 CcKind** [S] · Stun/Root/Reverse/Pull/Push 등 enum + `MovementSystem` switch case 추가. 콘텐츠 디자인 선행.
- **멀티셀 큐브 / 적-적 분산** [S] · 현재 단일 셀, 단일 큐브.
- **CC merge helper 추출** [S] · 3번째 CC caller 등장 시 `EffectSpawner.ApplyCc` 와 `CcApplySystem.MergeOrAdd` 듀얼 구현 통합 (I1).
- **ObstacleLifetimeSystem Burst 분리** [S] · 큐브 16+ 시점 `OnUpdate` Burst 분리 + `blockedCells` incremental (I4).

#### 렌더 파이프라인 / 시각 (board-visualization, wrapped)

board-visualization spec 자체는 ROI 부족으로 wrap 종료. 진단/실험은 `docs/spec/board-visualization/29_final_handoff.md` 참조.

- ~~**palette-and-overlay-fix**~~ [무효화 2026-07-03] · 대상(Legacy 렌더 텍스처/오버레이/tint 경로)이 legacy-render-removal 로 통삭제됨.
- **BattleScene MapView 잔재 씬 청소** [S] · 구 MapView GameObject(missing-script) + `BattleBridge.mapView` stale serialized 참조 제거. 씬 dirty WIP 정리 후 SaveScene 격리 절차로. (legacy-render-removal handoff)
- **17r prop-distribution-retry** [S] · V-001 잔존. Poisson 정공법 재구현.
- **23 volcano-theme-fill** [M] · 두 번째 테마 자산 채움.
- **BattleBridge.StartBattle Persistent allocates 경고** [S] · 반복 시작 시 leak 추적. ECS 컨텍스트 정리 경로 점검.

#### Seasonal — 후속 (seasonal-map-backdrop)

> 백드롭 서브시스템(BackdropMounter/SeasonBackdropData)은 Legacy3D 전용이라 legacy-render-removal unit 2 에서 통삭제(사용자 결정 2026-07-03). backdrop 의존 항목 3개(tint/exposure 튜닝·미세 시차·라이팅 매칭) 무효화로 제거. 시즌 시스템(SeasonRuntime/mapTheme)은 ACTIVE.

- **시즌별 차별화된 MapThemeData** [L] · 현재 4시즌 모두 forest 테마 공유. Lava/Lunar/Cosmic 전용 타일/장애물 정의. 별도 spec.
- **토너먼트 메타 hook** [M] · 서버 응답 → `SeasonRuntime` active season swap API.
- **시즌 배지 UI** [S] · 매치 시작 시 활성 시즌 배지 노출.

#### 배경 프랍 영역 풀 (prop-area-pools)

근경/원경 풀 분리(완료 2026-07-02, units 0~3) 후 남은 확장 후보.

- **영역별 밀도/falloff 리스트 이관** [S] · 현재 `tilePropDensity`/`ringPropDensity` 는 테마 전역. WeightedProp 리스트 단위 또는 영역별 파라미터로 세분화.
- **원경 카테고리 회피** [S] · `sameCategoryMinDistanceCells` 를 원경 링에도 적용(현재 근경 전용). 원경 나무 군집 자연화.

#### 프랍 접지/프레임 (prop-upright-root 파생)

- ~~**desert 테마 접지 fix**~~ [완료 2026-07-03] · desert prop_style_*/prop_dummy_* + 공유 forest dummy PropData 를 Tilted + offset 0 + 텍스처 BottomCenter 로 정합. 실제 렌더 sink 는 dummy 2종뿐(prop_style_* 는 공유 forest 프리팹의 baked data 로 이미 정상)이었고 나머지는 데이터 hygiene. Play 검증(`desert_dummy_grounding_verify.png`).

#### 모바일 디스플레이·Battle HUD 대응

모바일 aspect/framerate 수정(`GameManager.Awake` — 세로 1080 캡 + 기기 aspect 로 가로만 확장, `targetFrameRate=60` + `vSyncCount=0`) 후 남은 UI 후속.

- **UI CanvasScaler Height + Safe Area 통일** → `docs/spec/mobile-ui-safe-area/` [M, 설계 완료·승인 대기]. Battle/Outgame 전체를 full-bleed/safe root로 분리하고 16:9~20:9 + Android cutout/gesture를 검증한다.
- **Battle HUD Safe Action Tray** → `docs/spec/battle-hud-action-tray/` [L, 선행 spec 대기]. 비용·role·affordability 슬롯 정보, compact energy rail, tray↔hand 시각 정합, 배치 거부 원인 피드백.
- **남은 허용 유출 HUD** [S/M] · `defeatGoalReachedCount` 대비 현재 유출/잔여 허용치를 전투 중 상시 표시해 패배 원인 예측성을 높인다. Action Tray와 다른 상단 생존 정보 scope로 별도 승격.

#### PlayMode 스모크 위생 (subconscious-curse-expansion unit 4 실측, 2026-07-16)

전체 PlayMode 34 중 4건이 main 에서 이미 실패 — 내 커밋 이전(`ea155e65`) detached 재실행으로 재현 확정. 신규 회귀 아님, 테스트가 낡음:

- **DreamcatcherDeckCarryInTest** [S] · 8장 시절 mismatch 는 해소(라이브 `deckSize=10` 복귀 → 테스트의 10장 저장 기대와 일치). 잔여 과제는 제거된 기본(fallback) 덱 10장 기대(2026-07-15 사용자 결정으로 폐기됨) → **무폴백 계약**으로 재작성. (2026-07-20 덱=10 확정 정정)
- **SquadCarryInSmokeTest · DreamstoneCarryInSmokeTest** [S] · `RequestPlacement()` 직후 `Placement` 기대 — gift-phase(2026-07-13)가 `Gift` 를 삽입한 뒤 stale. Gift 통과(또는 우회 경로) 반영 필요. Dreamstone 쪽은 PrimeTween OnComplete 에러 표출도 동반.
- **MovementIntegritySmokeTest** [M] · "guardian aggro ≥1" 실패 — 원인 미조사(오늘 pull 의 gimmick 랜덤 배정 주입이 용의선상). 별도 조사.

#### 기타

- **Healer 전용 Spine asset** [S] · 현재 Archer Spine reuse. 전용 rig + idle/heal-cast/death 애니메이션. 시각 식별성, 기능 영향 없음. (modifier-framework-and-healer)
- **Spec 5~10 backfill** [S] · hybrid 진행 시 누락된 단위 spec 파일 작성. commit/handoff 가 임시 대체 중. 필수는 아님. (modifier-framework-and-healer)
- **VFX magic number 정리** [S] · `VfxSpawner` 의 y-offset / lifetime 하드코딩 → SerializedField 또는 ParticleSystem main.duration + startLifetime 으로 동기화. heal/placement/meteor 일괄 대상. (heal-vfx)
- **Heal VFX amount scaling** [S] · `HealAppliedEvent.amount` 를 `VfxSpawner.SpawnHealApplied` 에서 ParticleSystem main.startSize/startColor 에 매핑. 큰 힐 = 큰 펄스. 시그니처는 이미 amount 파라미터 확보됨. (heal-vfx)
- **GA 투사체 최종화** [S] · 디펜더별 최종 변종 선택(50종 중) + 스케일/높이 취향 미세조정 + 안 쓰는 변종 SO/프리팹 정리. (projectile-ga-reskin)
- **GA 투사체 모바일 최적화** [M] · 라이트/트레일 감축 · soft particle 토글 · 실기기 프로파일. tint 데이터-드리븐 recolor 는 별도(preserveVfxColors 우회 필요). (projectile-ga-reskin)

#### 스폰 예고 라인 — 후속 (spawn-point-alert, 2026-07-20 종료 이관 · 사용자 Play 확인 완료)

- **예고선 실기기 성능** [S] · lane 당 LineRenderer 3 + SpriteRenderer 1(3레인 = 12개), 매 프레임 폴리라인 재구축. Android 미측정. 부담되면 코너 정점만 유지하는 현 구조에서 갱신 주기를 낮추는 선택지. (spawn-point-alert)
- **보스 웨이브 예고 차별화** [S] · 보스 웨이브 lane 만 색/굵기를 달리해 구분. 현재는 크림슨 워닝 배너(boss-wave-cadence)가 별도로 존재해 중복 여부 판단 필요. (spawn-point-alert)
- ~~**Wave 1 사전 예고** [S]~~ · **2026-07-26 해소** — `wave-pattern/11`(스폰 리드인 2초) + `spawn-point-alert/3`(예보를 큐잉 웨이브 기준으로)로 Wave 1·강제 웨이브 모두 예고를 받는다. 남은 후보는 "배치 페이즈(배틀 시작 전)에 미리 노출"뿐 — 시작 시점 예지 필요. (spawn-point-alert)
- **예고 SFX** [S] · 라인이 그어질 때 저음 경고음. ElevenLabs 파이프라인 활용. (spawn-point-alert)

#### 파이프라인 커버리지 — 후속 (object-pipeline-map)

- **spec 파일 트리거 훅** [S] · `docs/spec/**/README.md` Write/Edit 시 파이프라인 커버리지 섹션 리마인더 주입(PostToolUse 훅). 템플릿 규칙 정착 후 잔여 누락 케이스 확인되면.
- **리뷰 게이트** [S] · two-track-review/critic 체크리스트에 "파이프라인 정거장 누락" 항목 추가.

#### 워크플로우 재현성 — 후속 (workflow-reproducibility)

- **deepinit ↔ AGENTS symlink 충돌 정책** [S] · deepinit 재실행 시 AGENTS.md 를 실제 파일로 재생성해 symlink 이 풀림 — 재적용 자동화 또는 deepinit 출력 위치 변경.
- **첫 실전 클론 체크리스트 완주 확인** [S] · 새 머신/팀원 첫 클론에서 루트 README 부트스트랩 체크리스트(훅 승인·Unity 첫 Play) 실전 검증.
- **thick 하네스 표준화** [S] · OMC/superpowers 를 `enabledPlugins`+`extraKnownMarketplaces` 로 커밋해 팀 동일 오케스트레이션(사용자 결정 시).

#### Outgame / squad / dreamcatcher — 후속 (outgame-scene-and-flow, squad-loadout, ingame-dreamcatcher)

- **드림캐쳐 카드 보유/콘텐츠 확장** [L] · ownedCardIds + 가챠/꿈런 파밍, 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널), 무의식 편입. (D 후속) — **다중 덱 수집/전환·이름 편집은 승격** → `docs/spec/page-local-presets/`
- **드림캐쳐 복합 효과** [L] · row-only/crit/pierce/splash/lowcost-summon/guardian-taunt/match-start-cost + 무의식 2장. 신규 메커닉/채널 필요. 트리거형 메커닉(개별유닛 바인딩 + N회 공격 발동) 토대는 → `docs/spec/dreamcatcher-unit-trigger/` 로 부분 승격 (2026-07-08). **★`lowcost-summon` 의 소환 파이프라인은 완료됐다** — `summon-patrol-defender`(완료 2026-08-12)가 `CreatePatrolEntity` + `SummonedBy` 연쇄 소멸 + 캐리어 스폰 seam 을 만들었다. 남은 것은 **카드 배선**뿐이고, 그건 이 항목의 다른 효과들보다 싸다.
- **진짜 MaxHealthMul 채널** [M] · 현재 HP 카드는 DmgTakenMul 프록시. 정확한 max-HP 증가 채널(Health/Units 맥락).
- **스쿼드 class/특성** [L] · class 라벨(완료, C unit0)을 이용한 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%). 가챠/꿈런 파밍/교환/리롤/등급. — **다중 스쿼드 수집/전환은 승격** → `docs/spec/page-local-presets/`
- **한글 TMP 폰트** [S] · 현재 LiberationSans only → UI 라벨 영문. 로컬라이즈 패스에서 한글 폰트 에셋 도입.
- **반복 씬 로드 ECS leak 점검** [M] · 2-씬 전환으로 BattleScene 반복 로드 → 기존 **BattleBridge.StartBattle Persistent allocates 경고** 백로그가 더 중요. 재진입 시 ECS World/Persistent 정리 경로 검증.

#### 로비 네온 리스킨 — 후속 (lobby-neon-restyle — 완료 2026-07-31, 사용자 실기 확인 통과)

외부 시안(네온 시티 목업)에서 **배경과 UI 스타일만** 로비 1차 화면에 이식했다(시안 캐릭터는 제외).
낮/밤 페어 + 디졸브 시간대 전환, 다크 칩 메뉴 버튼, START 네온 리본 배너.
상세: `docs/spec/lobby-neon-restyle/3_handoff_summary.md`.

- **START 전용 폰트** [S] · 시안은 넓은 헤비 이탤릭인데 Anton 은 콘덴스드라 자폭이 좁다. 형태·색·아웃라인은 맞췄고 남은 차이는 글자 자체뿐. 폰트 수급 시 교체. (lobby-neon-restyle)
- **씬에 박힌 무오버라이드 TMP 머티리얼 인스턴스 정리** [S] · 라벨 4개의 머티리얼 출처가 제각각(공유 에셋 / 기존 인스턴스 / 신규 인스턴스)이라 공유 머티리얼을 고쳐도 일부 라벨엔 안 먹는다. START 라벨은 실제 아웃라인 오버라이드가 있어 해당 없음. (lobby-neon-restyle)
- **패널 프레임까지 네온 확장** [M] · 스쿼드/덱/히스토리 패널의 프레임·헤더는 이번 스코프 밖이라 기존 스타일 그대로다. 1차 화면과 패널 내부의 룩이 갈린다. (lobby-neon-restyle)
- **프로필/재화 헤더 UI** [M] · 시안 상단의 레벨 바·코인/젬 카운터. `outgame-lobby-layout` 의 같은 후속 후보와 병합해서 판단. (lobby-neon-restyle × outgame-lobby-layout)
- **네온 배경 고해상 버전** [S] · 현재 1672×941. 구 항구 배경은 2391×1345 였어서 고해상 기기에서 상대적으로 소프트하다. (lobby-neon-restyle)

### Promoted / Closed

- **Production-transition 준비 자료 격리** → `docs/production-transition/` (owner-gated dormant downstream — Project owner의 명시적 활성화 전에는 Demo 정본·작업 후보·검증 gate가 아니며 이 backlog에서 후속을 추적하지 않음)
- **뜬 높이의 시각 규칙 + 아치 비행 감각** → `docs/spec/flight-lift-feel/` (완료 2026-08-02, units 0~3, `3743abb0`~`50cafa76` — 공중에 뜬 유닛을 화면이 읽게. **lift(지면에서 뜬 view 공간 높이) 하나에서 유닛 확대·그림자 축소·그림자 페이드를 파생**(`UnitLiftVisual`, 소비처 4개 공유: 드롭 하마·보스 도약·재배치 던지기·넉업 hop) + **ease-out-in 시간 재매핑**(`KeyringSim.FlightTimeRemap`, power=1 항등이라 무회귀 공짜 — 초반 급상승/정점 체공/후반 급하강) + 착지 스쿼시. 원인 진단은 "아치 높이가 시선축에 수직이라 **원근 확대가 구조적으로 0**". 스케일 쓰기를 `ApplyRenderScale` 단일 지점으로 모아 매 프레임 피드 ↔ 펀치/스쿼시 코루틴 경합 제거. 비행 중 블롭은 **아치 기저선**을 접지 앵커로 받아 착지 타일에 남는다(camUp 아치가 유닛 XZ 를 2타일 밀던 문제). **`useRealShadows` 0 으로 PC 도 블롭 전환** — 유닛 그림자는 조명이 아니라 "어느 칸에 있나" 앵커라는 룩 판정. 최종 튜닝: 아치 제어점 6.0(apex 2.4)·블롭 알파 0.5·hangPower 0.7·눌림 0.10/0.05. EditMode 1796/1794, 독립 코드 리뷰 1회(지적 4건 수정: 스쿼시가 프레임레이트 비례로 약해짐·사망 시 확대 굳음 등). **미해결(수용)**: lift 축이 드롭(camUp)과 도약(월드 +Y)에서 pitch 60° 기준 정확히 2배 어긋나 계약 2 의 "같은 높이=같은 크기"가 참이 아님. 후속: 탭 배치 던지기 적용·먼지/카메라 킥·`liftScaleMax` 포화·안드로이드 프로파일)
- **무의식 저주 유물 2종** → `docs/spec/subconscious-cursed-relics/` (완료 2026-07-15 — 플레이스홀더 `깊은 잠`/`꿈결 가속`을 재앙의 심장(시한부 공속·3타 강공·사망 폭발)과 금이 간 성배(전군 화력↑/생존↓)로 교체. 기존 mechanic/effect 조합만 사용하고 신규 메커니즘·인터페이스·ECS 채널은 추가하지 않음. 기존 `LethalTimer` 중복 부착 사전검증과 회귀 테스트 보강. 신규 2장 및 문자 placeholder 3장 카드 아트 완성.)
- **Dreamcatcher empower aura** → `docs/spec/dreamcatcher-empower-aura/` (완료 2026-07-15, units 0~3, `34c99cf3`~`33d3b90f` — 구 슬러그 `unit-buff-debuff-aura` 에서 개념 전환. **ModifierOrigin 출처 1급 태깅 프레임워크**(생산자 19곳, `ModifierHeader.origin`, 머지 키 아닌 메타데이터 — 같은 크기 버프도 슬롯 단위로 출처 구분; dispel/UI 재사용 토대) 위에서 **드림캐쳐 출처 스탯 모디파이어가 활성인 유닛에만** 온-바디 파워업 오라(`StatusFxKind.Empowered`, 순수 `ModifierAuraClassifier`, 상태 구동 reconcile·defender 한정, revoke=identity 자동 해제). 드림스톤/시너지/on-place/slow 배제. **버그 2건 수정**: `ApplyActiveDcEffectsTo` 드림스톤 origin 오태깅(`ActiveDcEffect.origin`) · revoke 감소형(Multiplicative) 미중립화(op-aware `EnqueueStatModifierRaw`, PlayMode 회귀). 미의도 `fallbackDeck` 제거. 오라=5요소(쉘·창끝·백라이트·충격파링·스파크) 파랑/주황 이중톤 + 전용 텍스처 2종, `EmpowerAura.prefab` 정식화. EditMode 780/PlayMode 2, 투트랙 리뷰 반영. 후속: 전용 셰이더 글로우·강도별 단계 오라·출처 태그 재사용(dispel/modifier UI))
- **Gift phase presentation — "카드 딜러의 선물"** → `docs/spec/gift-phase-presentation/` (완료 2026-07-14, units 0~3 + 코드리뷰 rev1, `cb3d99d9`~`f8b6d9a3` — gift-phase 연출 전면 재작성을 5 서사 비트로: 내가 짠 덱(하단중앙 딜-인→5×2 그리드) → 존재의 개입(Lucid 금빛 강림/Rim 적색 침투, 뒷면 플립 리빌 2.1x+1s 읽기 홀드, 내 덱 움찔) → 융합(스택 수렴 출렁+리플 지퍼+잔상 트레일+글로우 리플) → 제시(부채꼴 좌→우=소비 1→12, 스윕) → 각성치 장전(수신 앵커 링 n세그먼트+가속 케이던스+12번째 피니셔 찰칵). 프레임 3종=UiRoundedSprite 링+DraftCardFoil 시머 2층(셰이더 신작 0, **포일은 _MainTex 미샘플 오버레이라 단층 불가**). 좌표수학=`GiftPhaseLayout` 순수+EditMode 12. 총시간 판정=**PhaseChanged 실측**(명목치 합산은 7.0s 적발 이력) 5.95s+홀드 1s. 리뷰 10건 반영(피니셔 조기증발·판당 28텍스처 누수·NRE 소프트락·PrimeTween 풀 400 선예약 등). 덱순서 계약·ECS·씬 배선 불변. 후속: 실아트 교체·fly 타깃 정렬·게이지 연동·SFX)
- **각성 손패 카드감(Dreamcatcher hand deal-in)** → `docs/spec/dreamcatcher-hand-deal-in/` (완료 2026-07-13, units 0~4, `3f574a9c`·`f34ce20e` — 각성 손패를 StS/HS 카드감으로 재설계. 평면 UI 행 → 포물선 아치 부채(가운데 솟음)+겹침+접선회전, 슬롯 `target`+매프레임 **스프링 모델**(focus/idle/드래그/딜이 한 모델 공유). **모바일: hover 없음 → press-to-lift**(`OnPointerDown/Up`=focus, raise/확대/펴짐/최상단/이웃 scatter). 딜 소스=**하단 덱**(각성 버튼 아님, 초기 "버튼 정확좌표 딜"은 UI스럽다는 사용자 판정으로 폐기)→곡선 상승 OutBack 안착+원근 틸트+squash flex+버튼 pulse. 무입력 **idle bob/sway**(index 위상차). 퇴장=하단 덱 침강. critic 리뷰 반영(focus 가드·`OwnedByInteraction` 헬퍼). 모든 연출 realtime(TimeManager 도메인, `timeScale`=1 불변). ECS 변경 0·채널 불변. 트위닝 육안검증=사용자 포커스 Play. **후속**: 진짜 버텍스 커브(②-A)+꼬깃꼬깃(③)은 서브디바이드 메시/셰이더 토대 공유라 별도 spec)
- **선물 페이즈(Gift Phase)** → `docs/spec/gift-phase/` (구현 완료 2026-07-13, units 0~5 + 코드리뷰, `29d9ffd7`~`4d4eee69` — 배치 직전 `GamePhase.Gift` 삽입. "루시드의 선물"(스킬 Active 2) / "림의 선물"(무의식 2) 랜덤 이벤트 → 저장10+선물2=12장 발라트로식 셔플 연출 → 각성버튼 fly-out → 배치. **CycleDeck 무변경**(Gift 에서 덱 1회 생성·`Hand(전체)` 연출·배치 재사용, 이중셔플 없음). Lucid=기존 SkillLoadout 재사용, Rim=Subconscious 시드추출+폴백. 무의식 카드 2장 저작(기존 effects 채널)·덱빌더 제외. HUD 노출을 `PhaseChanged(Placement)` 로 재게이팅. 순수 프레젠테이션+Mono, ECS 변경 0. **연출 시각 상세조정은 후속 스펙**. 트위닝 육안검증=사용자 포커스 Play)
- **유닛 드림캐쳐 아이콘** → `docs/spec/unit-dreamcatcher-icons/` (완료 2026-07-12, units 0~2 — 배치 유닛 머리 위 부착 카드 미니 타로 스트립. `card.art` 재사용(신규 에셋 0), HandController registry + `AttachmentsChanged` 이벤트 구동, Squad 골드/Unit 청록 프레임, 사망 회수→소멸. 순수 프레젠테이션, ECS 변경 0. 트리거 진행도 뱃지·부착 연출은 후속)
- **보스 방어유닛 지향 이동** → `docs/spec/boss-defender-field/` (완료 2026-07-11, units 0~3, `dc298ceb` — 방어유닛 walkable 이웃 multi-source BFS "defender field"(Effects 싱글톤+매 프레임 재빌드) 를 보스(`BossTag`)가 Marching 에서 flow-follow. 지나친/뒤 배치 방어유닛에 역주행 재교전, 전멸까지 사냥(leak-proof), 0마리면 goal 마칭(무상태 fallback). FSM/채널 변경 0, 비-보스 무회귀 라이브 확인. 폐기된 enemy-hunter-targeting 의 직선추격/wall-slide 는 재도입 금지 계약)

- **Enemy walk anim speed match** → `docs/spec/enemy-walk-anim-speed/` (완료 2026-07-10, units 0~2 — 적 Spine 걷기 애니를 실제 view 변위 기반 재생속도로 변조해 발 미끄러짐(문워크) 제거. `skeleton.timeScale = battleScale × walkFactor`(sim-time 정규화 속도/refSpeed, min/max/스무딩/텔레포트가드 = `WalkAnimSpeedStyle` SO + BattleBridge 미러). 포탈 텔레포트 무시·standoff 바닥. **회귀 수정**: timeScale 트랙 전역이라 걷기 배율이 공격/사망/배치까지 늦추던 것 → 로코모션 루프(Loop==true)에만 적용. 튜닝 확정 refSpeed 1.2/max 3.0. 순수 프레젠테이션, ECS 변경 0. 후속: 코너 접지 스냅·Android 프로파일)
- **Attack anim speed match** → `docs/spec/attack-anim-speed-match/` (완료 2026-07-10, units 0~1 — 공격 Spine 애니를 실제 발사 주기에 compress-to-fit → 공속이 "빠른 스윙"으로 체감. `TrackEntry.TimeScale = max(1, animDuration / max(cooldownDuration/attackSpeedMul, hitDelaySec))`. **별도 튜닝 SO 없이 공격속도 필드(SO attackCooldown+버프+hitDelay)에서 직접 파생**(SoT 불변, 사용자 결정). 하한 1.0=구조 상수(느린 공격 자연+대기), 상한 없음(attackSpeedMul [0.2,5] 캡+authoring 규율). 산식 critic 1회 준수 판정+MEDIUM/LOW 반영. 시뮬 rate/데미지 불변. 후속: hit 프레임 정렬)
- **Result screen visual upgrade** → `docs/spec/result-screen-visual-upgrade/` (완료 2026-07-08, units 0~3 — 결과 팝업 리더보드를 인게임 HUD 언어(네이비/골드 홀로그램)로 리스킨: `UiRoundedSprite` 공용 절차 스프라이트 + 행별 플레이트·순위 배지(금/은/동)·본인 골드 강조·WAITING 회색 + **RESTART 하단 고정 3영역 앵커 레이아웃**(단일 VerticalLayoutGroup 겹침 결함 제거). 순수 `BuildRows` + EditMode 6. tournament-play-report 배선 불변, 순수 프레젠테이션. 배경은 시즌 아트 시도 → 인게임에서 풀스크린이 보드 덮어 폐기, `UiOverlay.Dim` 유지. 직렬화 필드 0(씬 diff 0). 후속: 등장 애니메이션·ScrollRect·한글 폰트)
- **Damage number visual upgrade** → `docs/spec/damage-number-visual-upgrade/` (완료 2026-07-07, units 0~3 + Play 튜닝 다회 — 순수 프레젠테이션(ECS 변경 0). 머리위 앵커(sim-Y drop 회피)·카메라축 겹침방지 격자·청록→골드→오렌지 팔레트·정점 그라데이션·TimeManager 델타 교정·index 결정론 셰이크/회전 + 하프톤/글로우/흰아웃라인/드롭섀도 머티리얼(비-모바일 Distance Field 변종) + 클러스터 스파크. 스파크는 별→**GA Circle18 라운드 도트 버스트 + 폰트색 틴트 emissive + 임팩트 플래시** 로 재작업(GA 텍스처만 재활용, 단일 경량 PS). 2트랙 critic BLOCKER 2건 반영. 후속: unit 2 Android 실기 프로파일 게이트·유닛별 정밀 앵커·진짜 emissive(URP Bloom))

- **Placement enemy see-through** → `docs/spec/placement-enemy-see-through/` (completed 2026-07-06, units 0~6, `9941f27` — 드래그 배치 중 적 유닛(Spine·Quad 혼합)을 반투명화해 가려진 뒤 보드 타일 노출. cutout↔transparent 런타임 전환(Quad)·PMA skeleton.A(Spine)·그림자 페이드·health tint 합성·매프레임 재적용. 프리뷰 불투명/최상단(unit 5) + 배치 하이라이트 적 위로(unit 6). 순수 프레젠테이션, ECS 변경 0·채널 14개 불변. two-track APPROVE(0~4)+M1 반영. 스텐실/후처리 리빌·블로킹 하자드 반투명은 후속)

- **Portal VFX upgrade** → `docs/spec/portal-vfx-upgrade/` (completed 2026-07-06, unit 0 — 물빔(WaterBeam 어거지) 제거 + 스월 지속화(loop+사이클 오버라이드, LocationVfx 가 duration 무시하는 원인 해소). 룬 게이트 실험은 사용자 반려·롤백. 입구/출구 시각 구분은 후속 후보)
- **Object pipeline map** → `docs/spec/object-pipeline-map/` (completed 2026-07-06, unit 0, 커밋 `aeccbc3a` — 플레이 오브젝트 생성→렌더 정거장 체크표 `docs/reference/object-pipeline-map.md`(아키타입 10종, `.cs` 앵커 57건 실측) + CLAUDE.md 파이프라인 커버리지 필수 섹션 규칙(N/A+이유 강제). artillery-defender 사후 대조로 카탈로그 등록 확인 포인트 승격. 훅/리뷰 게이트는 후속)

- **Workflow reproducibility** → `docs/spec/workflow-reproducibility/` (completed 2026-07-06, units 0~3 — fresh clone 워크플로우 재현: `.claude` 표준 추적+settings 분할(훅·read-only 권한 커밋) + auto-memory 27건 → `docs/reference/lessons/` 승격 + AGENTS=CLAUDE symlink + 루트 README 부트스트랩. critic APPROVE-WITH-CHANGES 반영, fresh clone 실측 검증. MCP/LFS 는 범위 밖)

- **Artillery defender** → `docs/spec/artillery-defender/` (completed 2026-07-06 — 곡사포 유닛: `Projectile_ArtilleryShell`(Rock ballistic) + `Defender_Artillery`(range7/cd3.5/dmg60, Cannon Spine 재사용) + DefenderCatalog 등록. projectile-trajectory-payload 엔진의 첫 Play 실증. 신규유닛 프로필 reconcile 은 후속)

- **Projectile trajectory × payload** → `docs/spec/projectile-trajectory-payload/` (엔진 완료 2026-07-06, units 0~5 — 투사체를 궤적(Homing/BallisticArc)×페이로드(SingleSplash/TileAoe) 직교 2축으로 분해. 홈잉 무회귀 이관 + BallisticArc 궤적 + TileAoe 반경 AOE + 곡사 발사 배선. 커밋 `e5836bc`~`27a452a`, 양트랙 리뷰 3게이트, EditMode 498/499. Play e2e 는 artillery-defender 로 이관. 신규 시스템/큐/맥락 0)

- **Placement keyring cord preview** → `docs/spec/keyring-cord-preview/` (completed 2026-07-05, squash 머지 `d197bc7` — 드래그 프리뷰 키링화: 고리=손가락(공중)·유닛=보드 스프링 follow(무게추 흔들림)·**하이라이트는 마우스 고정**(스윙 유닛 아님). 이전 drag-preview sway 완전 교체(SO 스키마도). camUp 수직분리·워밍업 금지 등 되돌리면 안 되는 설계는 handoff 참조. 탐색 이력 16커밋은 `feature/keyring-cord` 브랜치. 중력 드롭·아트 스왑은 후속)
- **Placement attack-range preview** → `docs/spec/placement-attack-range-preview/` (completed 2026-07-04, units 0~2 — 드래그 배치 중 공격범위를 노란 격자 outline 로 동기 펄스 표시. `Tilemap.color` tint + 전용 `_rangeTilemap`(sorting -12) + Chebyshev `RangeToTiles`. e2e 드래그 추종 Play 검증)
- **Placement drag-preview polish** → `docs/spec/placement-drag-preview-polish/` (completed 2026-07-04, units 0~1 + rev — 프리뷰 빌보드 각도 정합 + 매달린 키링 velocity-lean sway(SO 튜닝) + 프랍 위 정렬. Play(MCP) 검증)
- **Dreamstone loadout** → `docs/spec/dreamstone-loadout/` (completed 2026-07-06, units 0~7 — 스쿼드 4슬롯 장착 + set-then-apply 반입 + 개별 아이템 64종(순차 id·캐파 내 티어 스탯) + 코스트 생산속도 매치 배선 + 아이콘 스크롤 피커. 리뷰 4단 + 실측 검증. 획득/인벤토리는 후속)

- **Spine weapon trail** → `docs/spec/spine-weapon-trail/` (completed 2026-08-01, units 0~4 — Hovl `HS_SwordMeshTrail` 을 Spine `Gear` 본에 물린 무기 궤적. 룩 7종 Variant + Guardian/Fighter 7종 + 보스 2종. 심 변경 0. 후속: 보스 궤적 크기 결정 · 구조물 호스트 · `WeaponTrailRig._stopPending` 풀링 도입 시 수정 · Lightning 룩 활용처 · 무기 종류별 프리셋 분기 · 타격 순간 강조 · 모바일 실기기 프로파일 · 공격 애니 다양성)

- **Legacy render removal** → `docs/spec/legacy-render-removal/` (completed 2026-07-03, units 0~4 — Legacy MapView 렌더/Legacy3D 모드/시즌 백드롭/테마 LEGACY 43필드 완전 삭제, ~6,300줄 순삭. Tilemap 경로 무회귀. 씬 MapView 잔재 청소는 follow-up)

- **Projectile GA reskin** → `docs/spec/projectile-ga-reskin/` (completed 2026-07-03, units 0~6 — GabrielAguiar UniqueProjectiles Vol4 50종 라이브러리 + 스트립/스왑 툴 + ViewPool as-is 가드(streak/preserveVfxColors) + 높이오프셋 + ProjectileOffset sorting + muzzle-hit. 실게임 검증 PASS. 최종 변종선택/스케일/미사용정리는 사용자 취향 후속)

- **Prop upright root** → `docs/spec/prop-upright-root/` (completed 2026-07-03, units 0~1 — 프랍을 90° 타일맵 루트에서 떼어 upright 저작 프레임(+Y=위). 루트 flip + 블롭 마이그레이션 + EditMode 테스트. desert 접지는 follow-up)

- **Prop area pools** → `docs/spec/prop-area-pools/` (completed 2026-07-02, units 0~3 — 근경 playAreaProps / 원경 distantRingProps 독립 WeightedProp[] 풀 분리 + 인스펙터 영역별 weight. tileProps/placementWeight 등 retire)

- **Dreamcatcher deck builder** → `docs/spec/dreamcatcher-deck-builder/` (completed 2026-06-03, 10장 빌더+저장+인게임 반입 MVP. 10·고유≤2)

- **Ingame dreamcatcher** → `docs/spec/ingame-dreamcatcher/` (completed 2026-06-02, 인게임 카드 선택+효과 MVP. 드래프트 prep 단계 대체. modifier-framework 버그 수정 동반)
- **Squad loadout** → `docs/spec/squad-loadout/` (completed 2026-06-02, 편성+반입 MVP. 드래프트 유닛선택 대체)
- **Outgame scene & flow** → `docs/spec/outgame-scene-and-flow/` (completed 2026-06-02, 2-씬 분리 + 프로필 영속 기반. B/C/D 의 토대)
- **Seasonal map backdrop** → `docs/spec/seasonal-map-backdrop/` (completed 2026-05-22, 4시즌 + Skybox 전환)
- **Modifier framework — Legacy migration** → `docs/spec/modifier-legacy-migration/` (completed 2026-05-01)
- **Modifier framework & Healer** → `docs/spec/modifier-framework-and-healer/` (completed 2026-05)
- **CC pipeline & Obstacle** → `docs/spec/cc-pipeline-and-obstacle/` (completed 2026-04-29)
- **Enemy unit development** → `docs/spec/enemy-unit-development/` (completed 2026-04-30, PlayMode 검증 후속)
- **Board visualization** → `docs/spec/board-visualization/` (wrap 2026-04-27 — ROI 부족 중단, palette-and-overlay-fix 로 후속)
