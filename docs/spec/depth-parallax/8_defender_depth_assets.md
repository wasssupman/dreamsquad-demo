# 8 — 디펜더 뎁스 자산 bake·핸드터치·임포트

## 목적

실제 컷신 유닛(Ranger·Archer, 필요 시 Guardian)의 뎁스 프레임을 만들어 데이터에 할당한다. 셰이더·
통합이 준비된 뒤 실 자산으로 채운다.

## 변경 대상

- New: `Assets/_Project/Sprites/Cutscene/{Unit}/Depth/{Unit}_NNN_depth.png` (+ .meta, R8)
- Modify(asset): `Defender_Ranger.asset` / `Defender_Archer.asset` 의 `deployCutsceneDepth[]` 할당

## 구현

- **bake**: `Tools~/depth_bake.py`(unit 4)로 각 유닛 컷신 → 뎁스.
  - **기본은 단일 정적 뎁스 1장**(대표 프레임 추론, `deployCutsceneDepth` 길이 1 → 전 프레임 공유).
    실루엣이 실제로 움직이는 유닛만 프레임별로 에스컬레이션(unit 4 절차). 글로벌 퍼센타일 정규화.
  - 프레임별로 갈 경우 색 프레임과 **index 정렬**(색 001↔뎁스 001). Guardian 정방향, Ranger/Archer 는
    역순 리넘버된 색 순서에 맞춤.
- **핸드터치**: 셀아트 약점(뜬 소품/무기/부츠/머리카락/평면 배경)을 페인트오버로 보정. 몇 프레임 먼저
  eyeball 로 outline halo·극성 확인.
- **임포트**: `DepthMapBaker` 로 R8/linear/no-mip/무압축/non-atlased 임포트.
- **할당**: `deployCutsceneDepth[]` 를 색 프레임과 같은 순서로 채움(UnityMCP `manage_scriptable_object`
  또는 일회용 MenuItem, GUID/fileID 정확히). `deployCutsceneTiltGain` 유닛별 초기값(기본 1).
- **극성**: 첫 Play 에서 near/far 가 뒤집혀 보이면 `DepthParallaxSettings.depthSign` 을 -1 로(자산 재bake
  아님). 자산은 흰색=near 관례 유지.

## 관찰 (2026-07-15 Guardian 3프레임 실측 — DA-V2 Small, MPS)

실제로 뽑아 확인한 이 아트의 특성(스펙 knowledge):

- **품질 양호**: 셀아트인데 캐릭터/배경 분리·상대 깊이가 정확. 앞으로 뻗은 부츠 밑창이 가장 near,
  몸통→뒷다리→머리 순으로 멀어짐. 스타버스트 장식 배경은 완전 far(검정)로 깔끔히 분리 → 핸드터치
  리스크 낮음.
- **경계 급락 주의**: 배경이 순수 0(far)이라 캐릭터 실루엣에서 뎁스가 절벽 → 패럴랙스 시 외곽 늘어짐
  가능. **뎁스 blur + 진폭 ≤4%** 로 눌러야(unit 2/9). 배경을 mid-gray 로 살짝 들어올리는 것도 옵션.
- **부츠 밑창 과-near**: 밑창이 유독 밝아 틸트 시 혼자 크게 움직일 수 있음 → 극성/진폭 튜닝 지점.
- **정적 단일 뎁스로 충분해 보임**: 3프레임 뎁스가 거의 동일(줌 미세) → 기본 방침(정적 1장) 타당.
- 추출 환경: `scratchpad/depthenv`(venv) + `bake_depth_proof.py`. 전체 세트도 수분 내.

## 완료 기준

- Ranger(및 Archer) 뎁스가 R8 임포트로 존재(정적 1장 또는 프레임별 index 정렬).
- 해당 `.asset` 의 `deployCutsceneDepth` 길이가 1(정적) 또는 색 프레임 수(프레임별).
- 대상 유닛 드래그 시 패럴랙스가 캐릭터 실루엣을 따라 자연스럽게(육안, 상세 튜닝은 unit 9).
