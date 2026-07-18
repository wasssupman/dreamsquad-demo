# Spec — Defender Deploy Cutscene

> 상태: **units 8·9 구현 완료 · Unity 컴파일 clean · Play 확인 대기 (2026-07-18)**
> — unit 8 `e29300eb`, unit 9 `e3632167`.
> 확장 이력: Guardian 프레임 (2026-07-15, `5cbee1b4`) · 뎁스 패럴랙스 통합 (2026-07-15, `de2275ee`)
> · Cannon (2026-07-16, unit 5 — `bc452d78`) · Sniper (`459e2d80`) · FireCaster·Healer (`5849e370`).
>
> **컷신 보유 7종**: Ranger · Archer · Guardian · Cannon · Sniper · FireCaster · Healer.
> 전부 자산·할당 완료, **Play 검증 대기**(scale/offset 은 계산 시작값).
>
> 인계 지도: `4_handoff_summary.md`(Ranger/Archer) → `6_handoff_summary.md`(Cannon~Healer)
> → `10_handoff_summary.md`(수명 정정·Guardian 공용 배선).

## 상위 목표

유닛을 드래그로 배치하는 스와이프 동안, 해당 유닛의 짧은 컷신(스프라이트 플립북)이
화면 **좌하단 모서리**에 등장한다. 컷신은 로비 캐릭터의 스프라이트 애니메이션 개념을
참고하되(프레임을 UI Image 에 순차 표시), 원샷 플립북이므로 Animator 없이 스크립트로
재생한다. 화면 왼쪽 **바깥에서 빠르게 슬라이드-인**하며 애니를 재생하고, 마지막 포즈를
0.5초 유지한 뒤 왼쪽으로 자동 퇴장한다. 배치 성공 시에는 연출 중이어도 즉시 사라진다.

> 이 문단은 rev 2026-07-18(unit 8 수명)·rev 2026-07-16(앵커=좌하단) 반영본이다.
> 과거 "스와이프 종료 연동" 계약은 폐지됐다.
> 상세는 아래 공통 원칙의 `수명`·`배치/연출` bullet 참조.

## 검증 질문

Defender_Ranger 를 드래그로 집으면 좌하단에 Ranger 컷신 플립북이 뜨고, 완주 후
마지막 포즈를 0.5초 유지한 뒤 왼쪽으로 슬라이드-아웃하며 사라지는가? 단, 배치가 성공하면
연출 단계와 무관하게 즉시 숨고 초기 상태로 돌아가는가? 스와이프 방향으로
컷신 아트가 3D 회전하듯 기울고 손을 떼면 0 으로 복귀하는가(뎁스 패럴랙스)? 컷신 프레임이
없는 유닛은 아무 일도 일어나지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_sprite_pipeline.md` | Ranger 프레임 전처리(역순 리넘버 + 검정 누끼 + 50% 축소 + 임포트) | 33장 Ranger 스프라이트 확보 |
| 1 | `1_data_field.md` | DefenderUnitData 에 컷신 프레임/fps 필드 + Ranger/Archer 에셋 할당 | 유닛→프레임 매핑 |
| 2 | `2_cutscene_player.md` | `DeployCutscenePlayer` — 좌하단 오버레이 플립북 재생기 | 슬라이드 인/아웃 + 자동 소멸 |
| 3 | `3_wiring.md` | BeginDrag 트리거 + DefenderSelector 주입 + Play 검증 | 드래그 스와이프에 연결 |
| 5 | `5_cannon_frames.md` | Cannon 49프레임(체커보드 누끼 · **정방향**) + 정적 뎁스 | 4번째 컷신 유닛 |
| 7 | `7_sniper_firecaster_healer.md` | Sniper·FireCaster·Healer 49프레임+뎁스 · **배경색 원리** | 5~7번째 컷신 유닛 |
| 8 | `8_hold_last_frame.md` | 최종 프레임 0.5초 유지 + 자동 퇴장 + 배치 성공 강제 초기화 | 컷씬 수명 확정 |
| 9 | `9_guardian_uses_sniper_cutscene.md` | Guardian 컷씬 참조를 Sniper 컷씬으로 교체 | 임시 공용 컷씬 배선 |

> 4·6 번은 handoff summary(작업 단위 아님). Guardian(49장)은 `5cbee1b4` 로 스펙 파일 없이
> 프레임만 추가됐다 — 소스 성질이 이 스펙에 미기록이다(후속 후보 참조).
>
> **unit 0 은 Ranger 전용 절차다.** 소스마다 배경색·재생 방향이 다르므로 새 유닛을 추가할 때
> 그대로 복사하지 말 것. 유닛별 차이는 각 작업 단위 파일이 소유한다(Cannon → unit 5).

## 공통 원칙 / Feature-wide 계약

- **트리거**: `DefenderDragPlacementController.BeginDrag` 진입 시 유닛에 컷신 프레임이
  있으면 1회 재생. 프레임 비어 있으면 no-op(다른 유닛은 조용히 skip).
- **수명 = 자동 종료 + 배치 성공 절대 우선** (unit 8, rev 2026-07-18): 플립북 Phase A가
  끝나면 마지막 non-null 컬러 프레임과 대응 뎁스를 명시 적용해 0.5초 유지한 뒤 왼쪽으로
  slide-out하고 숨는다. 드래그 실패·취소는 이 자동 종료를 방해하지 않는다. 단,
  `TryBeginDefenderDeployment` 성공은 절대 룰이라 재생/hold/slide-out 어느 단계든 즉시 중단하고
  Canvas·코루틴·틸트 상태를 초기화한다. 첫 프레임 또는 setup pose로 복귀해 노출되지 않는다.
  새 배치 세션을 시작할 때도 직전 실패·취소 컷씬을 먼저 초기화해 이전 유닛 연출이 넘어오지 않는다.
- **렌더**: ScreenSpaceOverlay 캔버스의 UI `Image` 1장. 프레임을 `Image.sprite` 로 교체하는
  스크립트 플립북(fps = 데이터 값). Animator/.anim 미사용.
- **배치/연출** (rev 2026-07-16: 앵커 정정): 앵커/피벗 = **좌하단** `(0,0)` + 인스펙터 마진.
  `cornerMarginPx` = (x: 이미지 왼쪽끝 위치, y: 하단에서 위로), 현 baseline `(-100, 24)`.
  초기 계약은 좌상단이었으나 튜닝으로 좌하단이 됐다 — **이 bullet 이 계약이고**, unit 2 의
  top-left 기술은 폐지본이다. 표시 크기는 스프라이트
  네이티브(**유닛마다 다름**: Ranger/Archer 640×360, Guardian 180×180, Cannon 276×204)
  × displayScale(공유, 현 1.2). 등장은 화면 왼쪽 '바깥'에서 빠른
  슬라이드-인(애니 동시 재생), 퇴장은 왼쪽으로 슬라이드-아웃. 세로 위치는 좌하단 고정.
- **누끼 자산 (유닛-불변 계약만)**: 각 유닛 프레임 = **배경 투명 PNG** · **재생 순서 = 줌-인**
  (`{Unit}_001` = 가장 줌-아웃) · **네이티브 해상도 유지**(크롭/축소 없음).
  **배경 제거 기법과 소스 넘버링 방향은 계약이 아니다** — 소스마다 다르며 각 작업 단위
  파일이 소유한다. 여기 있는 값을 새 유닛에 복사하지 말고 소스를 먼저 실측할 것.
  - Ranger/Archer: 검정 배경 → luma 매트 + **역순** 리넘버(소스가 줌-아웃) → `0_sprite_pipeline.md`
  - Cannon/Sniper: 체커보드가 RGB 에 베이크된 **가짜 투명** → flood-fill + **정방향** → `5`·`7`
  - FireCaster: 순백 배경(재수급) → flood-fill + 정방향 → `7_sniper_firecaster_healer.md`
  - Healer: 체커 → 정방향. **격자 잔존 감수**(사용자 결정) → `7_sniper_firecaster_healer.md`
  - Guardian: 정방향(`depth-parallax` unit 8 교차기록). 스펙 파일 없음 → 후속 후보 참조.
- **소스 수급 요청 (하드, 2026-07-16 실측)**: 신규 유닛은 **배경 검정(#000000) 또는 알파 채널
  유지**로 받는다. 전 컷신이 알파 평탄화 소스에서 왔고(반투명 중간알파 Ranger 18.0% /
  Archer 1.5% / Cannon 1.3% / Sniper 1.9%), **Ranger/Archer 가 무사했던 건 배경이 검정이었기
  때문이지 운이 아니다** — 글로우 VFX 를 검정에 합성하면 `P = a×F`(premultiplied)라 밝기에서
  알파를 되살릴 수 있다(unit 0 절차가 정확히 이것). 다른 배경은 반드시 뭔가를 삼킨다:
  **체커** → 반투명 VFX 에 격자가 배어듦(복원 불가) · **순백** → 흰 셔츠·색종이를 삼킴
  (MarginValue 스윕으로도 온전한 구간 없음) · **검정** → 검은 정장도 캐릭터 *안쪽*이라
  flood-fill 이 보호. 이 아트에 안전한 배경은 검정뿐이다.
- **하드코딩 금지**: 유닛별 값 **6종**은 `DefenderUnitData` — `deployCutsceneFrames` ·
  `deployCutsceneFps` · `deployCutsceneScale`(표시배율) · `deployCutsceneOffset`(도착 오프셋) ·
  `deployCutsceneDepth`(뎁스) · `deployCutsceneTiltGain`(틸트 배율).
  공유 값(hold·displayScale·baseline 마진·슬라이드 속도)은 `DeployCutscenePlayer` SerializeField.
  값은 전부 데이터/인스펙터에서 나온다.
  - 최종 크기 = 네이티브 × displayScale(공유, 현 1.2) × deployCutsceneScale(유닛별).
  - 도착 위치 = cornerMarginPx(공유 baseline, x=-100) + deployCutsceneOffset(유닛별).
    컷씬마다 캐릭터 위치/크기가 달라 유닛별 미세조정. **scale/offset 은 Play 튜닝으로 수렴시킨다.**
    - Play 튜닝 완료: Ranger 1 / 0 → -100 · Archer 1.5 / -150 → -250
    - **Play 미검증 계산 시작값**: Cannon 2.6 · Sniper/Guardian(unit 9 공유) 2.6 (204 네이티브) ·
      FireCaster 3.0 · Healer 3.0 (180 네이티브) — 전부 offset (0,0).
      계산식 = 목표 648px ÷ (네이티브 높이 × displayScale 1.2).
- **Guardian 임시 배선** (unit 9): Guardian은 자체 전투 데이터는 유지하되 컷씬 4필드
  (`frames/depth/scale/offset`)만 Sniper와 같은 자산·값을 참조한다. PNG를 복제하지 않는다.
- **기능 온/오프**: `DragSwaySettings.enableDeployCutscene`(bool). 이 SO 는 이미 드래그 배치
  프리뷰 연출 튜닝 허브로 컨트롤러에 주입돼 있어 재사용. 끄면 프레임이 있어도 재생 안 함.
- **뎁스 패럴랙스**: 별도 스펙 소유(`docs/spec/depth-parallax/`, 완료 2026-07-15, `de2275ee`).
  이 스펙은 **유닛별 자산 할당만** 담당(`deployCutsceneDepth` / `deployCutsceneTiltGain`).
  극성은 유닛별이 아니라 `DepthParallaxSettings.depthSign` 전역 노브다.
- **경계**: 순수 프레젠테이션(MonoBehaviour View). ECS/BattleBridge 시뮬레이션 경로를
  건드리지 않는다. `BattleBridge` 를 경유할 필요 없음(전투 상태 미참조).

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트(유닛/적/투사체/해저드)가 아닌 배치 UX 오버레이 연출.
스폰→렌더 파이프라인(`docs/reference/object-pipeline-map.md`) 대상이 아니다.
(cf. `outgame-lobby-characters` 도 같은 사유로 N/A.)

## 후속 후보 (이번 스코프 밖)

- 나머지 유닛 컷신 프레임 제작/할당. **완료 7종**: Ranger 33 · Archer 49 · Guardian 자체 49
  (현재 unit 9에서 Sniper 참조 사용)
  (`5cbee1b4`) · Cannon 49 (`bc452d78`) · Sniper 49 (`459e2d80`) · FireCaster·Healer 49
  (`5849e370`). **남은 9 디펜더 미착수** — 수급 시 위 "소스 수급 요청" 계약(배경 검정) 적용.
- **Healer 재수급**: 구 체커 소스라 `_025` 부근 파스텔 워시에 격자가 보인다. FireCaster 처럼
  배경을 바꿔 다시 받으면 해소된다. **프레임만 덮으면 GUID 유지라 재할당 불필요.**
- **Guardian 스펙 파일 부재** (`5cbee1b4` — 프레임만 커밋): 소스 성질이 이 스펙에 한 줄도
  없다(180×180 · **정방향** — `depth-parallax` unit 8 교차기록 · 배경 종류/누끼 기법 미기록).
  Cannon 으로 겪은 함정의 선례일 수 있는데 기록이 없다. 5번째 유닛 추가 시 선례는 unit 0/5
  만 삼을 것. 사후 복원은 추측이 섞이므로, 필요해지면 실제 자산을 실측해 unit 번호로 신설.
- Cannon 고해상도 재수급: 현 소스는 276×204(화면 3.1배 확대). 파일명이 광고하는 원본
  1932×1428 을 확보하면 재-누끼(`5_cannon_frames.md` 절차 그대로)로 Archer 급 선명도 가능.
- **뎁스 계단 리맵**: 근경 과장 + 몸통 4단 계단으로 패럴랙스를 이산 평면화 → 별도 spec
  `docs/spec/cutscene-depth-layering/`(초안, 2026-07-16). 4종 전부 대상이라 이 spec 범위 밖.
- 컷신 in/out 트랜지션(페이드·슬라이드), 프레임 아틀라스화로 메모리 최적화.
- 사운드(컷신 보이스/스팅어) 동기.
- 같은 유닛 연속 드래그 시 컷신 재생 정책(현재는 재생 중 재트리거 시 재시작).
