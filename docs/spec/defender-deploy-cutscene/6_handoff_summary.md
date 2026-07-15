# 6 — Handoff Summary (Cannon 시점)

## Commit

`bc452d78` — **주의: 커밋 제목이 내용과 다르다.** 제목은 "feat(result-screen-lobby-exit):
unit 0 — 결과창 다시하기 → 로비로" 인데, 병행 세션과 공유 인덱스를 쓰다 발생한 사고로
**Cannon 작업 108개 파일이 그 커밋에 혼입**됐다(2026-07-16). 작업물 자체는 온전하다.
Cannon 관련 파일을 찾으려면 커밋 제목이 아니라 경로로 검색할 것.

## Implemented

- **Cannon 컷신 49프레임** — `Sprites/Cutscene/Cannon/Cannon_001..049.png` (276×204, 투명).
- **정적 뎁스 1장** — `Cannon/Depth/Cannon_depth.png` (138×102 R8, DA-V2 Small).
  이후 `cutscene-depth-layering` `d6cc06a3` 에서 계단 리맵본으로 교체됐다(이 커밋이 baseline).
- `Defender_Cannon.asset` — frames 49 · fps 24 · scale 2.6 · offset (0,0) · depth ×1 · tiltGain 1.
- 스펙 정합성 정리: README(앵커 좌상단→**좌하단** 정정, 폐지된 수명 계약, 누끼 자산 계약을
  유닛-불변으로 재작성) · unit 0 에 "Ranger 전용" 헤더 · unit 2/3 에 폐지 rev 라인.

## Key Files

- `docs/spec/defender-deploy-cutscene/5_cannon_frames.md` — **Cannon 절차 (함정 필독)**
- `docs/spec/defender-deploy-cutscene/README.md` — feature-wide 계약 (누끼 자산 bullet)
- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs` — 재생기(코드 무변경)

## Verified

- 누끼 육안(어두운/중간 배경, 흰 사각형·테두리 없음), bilinear 3.1배 확대 halo 시뮬,
  GUID/SO 참조, 임포트 Sprite/무압축, 콘솔 error·warning 0.
- **Play 미검증** — `scale 2.6` / `offset (0,0)` 은 계산 시작값이다.

## Notes (되돌리면 안 되는 의도)

- **Cannon 소스는 가짜 투명이었다** — `hasAlpha=yes` 지만 alpha 전면 255, 회색 체커보드가
  RGB 에 베이크(주기 ≈9px, 245↔253). 뷰어에서 투명해 보이는 것에 속지 말 것.
- **역순 리넘버 금지** — 소스가 이미 줌-인(커버리지 31.2→36.8%). unit 0 의 역순은 Ranger/Archer
  소스가 줌-아웃이었기 때문이지 규칙이 아니다. 계약은 **재생 = 줌-인**.
- **color dilation 필수** — 투명부가 흰 RGB 를 물고 있어 3.1배 bilinear 이 흰 halo 를 번지게
  한다. Ranger/Archer 는 투명부가 검정이라 무증상이었다.
- README 의 `누끼 자산` bullet 은 **유닛-불변 계약만** 담는다. 여기 값을 새 유닛에 복사하지 말고
  소스를 먼저 실측할 것 — 이게 이 spec 이 겪은 함정의 요약이다.

## Follow-up

- **Play 검증** — 좌하단 컷신 재생 + 위치/크기 체감(`scale`/`offset` 튜닝).
- **Healer(49장)·Sniper(49장)** 소스가 `Sprites/Cutscene/` 에 들어와 있다(미착수, untracked).
  **Sniper 는 `1932x1428` 로 Cannon 과 같은 출처 패턴** → 체커보드 가짜 투명일 가능성이 높다.
  unit 5 절차를 그대로 쓸 수 있다. Healer 는 `2240x1260`.
- Cannon 고해상도 재수급(현 소스 276×204, 화면 3.1배 확대). 원본 1932×1428 은 디스크에 없다.
