# 5 — Cannon 컷신 프레임 + 뎁스

## 목적

Cannon(캐논) 배치 컷신 49프레임 + 정적 뎁스를 만들어 데이터에 할당한다. 소스가 unit 0
(Ranger)의 전제와 **배경·재생순서 두 지점에서 다르므로** 그 차이를 이 파일이 소유한다.

## 변경 대상

경로는 `Assets/_Project/` 기준.

- 입력: `Sprites/Cutscene/Cannon/8a87c598-…_frame_001..049.png` (276×204) — 누끼 후 삭제
- 출력: `Sprites/Cutscene/Cannon/Cannon_001..049.png` (276×204, 투명)
  + `Sprites/Cutscene/Cannon/Depth/Cannon_depth.png` (R8 138×102, 정적 1장)
- Modify: `Data/Defenders/Defender_Cannon.asset`
- 툴: 누끼 = scratchpad python(Pillow/numpy, 레포 밖) · 뎁스 =
  `Modules/DepthParallax/Tools~/depth_bake.py`(venv: torch+transformers).
  뎁스 상세 → `docs/spec/depth-parallax/{4,8}_*.md`

## 구현

1. **누끼 — 배경이 검정이 아니라 체커보드다 (핵심 함정).** `hasAlpha=yes` 지만 alpha 가 전
   픽셀 255 다. 뷰어에서 투명해 *보이는* 건 **회색 체커보드가 RGB 에 베이크**된 것
   (주기 ≈9px, 245↔253, R=G=B). 그대로 임포트하면 캐릭터 뒤에 흰 사각형이 뜬다.
   unit 0 의 검정 luma 매트를 쓰면 안 된다 — 배경이 near-white 인데 이 캐릭터는 **흰
   머리카락·하이라이트·스파크**를 갖고 있어 통째로 먹힌다. 4단계 전부 필수:
   - `is_bg = luma>=240 && (채널 max-min)<=6` 에서 **테두리 flood-fill** → 외곽 연결분만
     배경 확정. 내부 near-white(≈0.83%) 보존.
   - 소프트 알파: `a = (249 - luma) / (249 - F_luma)`, `F_luma` = 인접 내부 픽셀 median.
     `|249 - F_luma| < 20` 이면 구분 불가로 보고 `a=1`(흰 머리 경계 방어).
   - **디컨탬**: `F = (P - (1-a)·249) / a`. 생략 시 경계에 흰 테두리.
   - **color dilation(엣지 패딩)**: 투명부 색을 밖으로 dilate. 생략 시 bilinear 가 투명
     픽셀 RGB 를 끌어와 프린지(여기선 흰 halo). Ranger/Archer 는 투명부가 검정이라 무증상.
2. **정방향 — 역순 리넘버 금지.** **소스가** 이미 줌-인이다(001→049 실루엣 커버리지
   31.2%→36.8% 단조 증가). 재생 방향은 전 유닛 공통(줌-인)이고, 역순을 건너뛰는 근거는
   *소스 순서*가 이미 그 방향이라는 것(Guardian 과 같음).
3. **크롭/축소 없음**: 276×204 네이티브 유지(크롭은 픽셀 밀도를 못 올리고 앵커만 흔든다).
   화면 3.1배 확대는 감수 — Guardian 3.6배 선례. 고해상도 재수급은 README 후속 후보.
4. **임포트**: Sprite(Single), alphaIsTransparency=on, mipmap off, Bilinear, 무압축.
5. **뎁스**: `depth_bake.py` 기본 경로(DA-V2 Small, Apache-2.0, **정적 1장** — 줌이 미세해
   충분, depth-parallax unit 8 관찰). 출력 = 색의 1/2 해상도 **138×102 R8**(툴 기본, 의도).
   `--flatten` 금지(배경 전용). `DepthMapBaker` 로 임포트. 자산은 **흰색=near** 관례 유지 —
   극성이 뒤집히면 유닛별 재bake 가 아니라 `DepthParallaxSettings.depthSign`(**전역**)을 -1 로.
6. **할당**: frames = Cannon_001..049, fps 24, `deployCutsceneScale` 2.6
   (204×1.2×2.6 ≈ 636px, Archer/Guardian 648px 급), `deployCutsceneOffset` (0,0),
   `deployCutsceneDepth` = [Cannon_depth], `deployCutsceneTiltGain` 1.
   **scale/offset 은 계산 시작값 — Play 튜닝 대상**(다른 3종과 동일).

## 완료 기준

- `Cannon_001..049.png` 49장(276×204, 실제 alpha), `.meta` 동반, Sprite 임포트.
- 어두운 배경 합성에서 **흰 사각형/흰 테두리 없음**, 흰 머리카락·스파크 보존(육안).
- `Cannon_001` = 소스 `frame_001`(가장 줌-아웃). 역순 아님.
- `Depth/Cannon_depth.png` = 138×102 R8(색의 1/2, 의도) · 흰=near, `deployCutsceneDepth` 길이 1.
- UUID 원본(`8a87c598-…`) 잔여 0건 — png/meta 모두.
- 콘솔 error/warning 0.
- Play: Cannon 슬롯 드래그 시 좌하단 컷신 재생 + 스와이프 시 틸트 패럴랙스 동작.

_확인: 2026-07-16 — 자산·할당·임포트·콘솔 검증 완료(누끼 육안, bilinear halo 시뮬, GUID/SO 참조).
**scale 2.6 / offset (0,0) 은 계산 시작값이며 Play 미검증** — 위치·크기 체감은 다음 세션 과제.
뎁스는 이후 `cutscene-depth-layering` unit 2 에서 계단 리맵으로 교체됨(baseline 은 이 커밋)._
