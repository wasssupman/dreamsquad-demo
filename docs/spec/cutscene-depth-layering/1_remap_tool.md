# 1 — depth_layer_remap.py (기존 뎁스 후처리 툴)

## 목적

`0_remap_contract.md` 의 산식을 정식 경로에 구현한다. `(기존 뎁스 PNG, 색 프레임) → 리맵된
뎁스 PNG`. 모델 재추론 없음 — unit 2 가 4종에 이걸 돌린다.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Tools~/depth_layer_remap.py`

`Tools~` 는 `~` 접미사라 Unity 가 무시한다(컴파일·임포트 대상 아님) → asmdef/모듈 경계 무영향.

## 구현

### 재사용 (중복 구현 금지)

`depth_bake.py` 의 `torch`/`transformers` 는 **지연 import**(함수 안)라, numpy+PIL 만으로
헬퍼를 가져올 수 있다. 다음 4개는 **반드시 재사용**한다 — 검증된 관례라 재구현하면 드리프트한다:

- `resize_float(arr, (w,h))` — bilinear float 리사이즈
- `gaussian_blur(arr, sigma)` — scipy 없으면 numpy 폴백 내장
- `quantize_r8(norm01, dither, rng)` — 8bit 밴딩 깨는 dither 관례
- `save_r8_png(u8, path)` — 임포터를 R8 로 강제하는 `mode="L"` 저장 트릭

### CLI

```
python depth_layer_remap.py <depth.png> <color.png> <out.png>
    [--levels 4] [--near-keep 0.80] [--near-lo 0.40] [--near-hi 1.00]
    [--body-lo 0.02] [--body-hi 0.40] [--blur-sigma 1.2] [--dither 0.5]
    [--seed 1234] [--stats]
```

> rev2 2026-07-16: 기본값이 바뀌었다 — `near_lo` 0.80→**0.40**(= body_hi, 간극 0),
> `body_hi` 0.44→**0.40**, `blur_sigma` 0.6→**1.2 (색px 기준)**. 사유는 `0_remap_contract.md`
> rev2(접힘/잔상 수정). **`--blur-sigma` 는 색 텍스처 px 기준**이고 툴이
> `sigma_texels = blur × (뎁스폭/색폭)` 로 환산한다.

- **해상도**: 연산은 **뎁스 자체 해상도**에서. 색 알파는 뎁스 크기로 리사이즈해 마스크로 쓴다
  (Cannon 뎁스 138×102 ↔ 색 276×204 처럼 다를 수 있다).
- **마스크**: `m = alpha > 0.5`. 색이 알파 없거나 `m` 이 전체면 그대로 진행(Guardian 케이스 —
  경고만 출력하고 unit 2 가 판단).
- `--stats`: 리맵 전/후 **힌지 주차%**(`|r-0.5|<0.05`)와 **평균 변위**(`|r-0.5|*0.022`)를
  출력해 unit 0 완료 기준의 수치 재현을 검증 가능하게 한다.

### 불변식 가드 (하드, rev2)

다음 넷을 **런타임 assert** 한다. 조용히 통과시키지 말고 즉시 실패시킨다:

1. `body_hi <= 0.5 < near_hi` — 몸통은 힌지 아래, **손끝**은 위(반대 운동).
2. `near_lo == body_hi` — 경계 연속. 점프가 있으면 손가락 뿌리가 접힌다(rev2 의 원인).
3. `body_hi <= 0.42` — 몸 대역을 힌지에 붙이면 다시 주차돼 변위 0(1차 rev 실패 사유).
4. `levels >= 2`.

**접힘 가드**는 `--stats` 와 무관하게 항상 돈다. 판정은 **절대값이 아니라 baseline 대비 회귀** —
Ranger 는 원본이 1px 하드 엣지(grad 12.59)라 접힘 0 이 불가능하므로, 악화만 없으면 통과.

### 하지 않는 것

- 모델 로드/추론 없음(`--contrast` 도 없음 — unit 0 계약상 리맵과 상호배타).
- 임포트 설정 손대지 않음(`DepthMapBaker` 담당). 에셋 할당도 unit 2.

## 완료 기준

- `depth_layer_remap.py` 가 torch 없이 실행된다(numpy+PIL만).
- `--stats` 출력이 **README 실측표를 ±2%p 로 재현**:
  힌지 주차 Ranger 9.1→1.0% · Archer 1.1→0.6% · Guardian 4.3→0.8% · Cannon 19.7→1.6%,
  변위 +19% / +14% / +13% / +53%.
- `body_hi >= 0.5` 또는 `near_lo <= 0.5` 로 부르면 assert 로 실패한다.
- 출력 PNG 가 R8(`mode="L"`) 이고 뎁스 입력과 같은 해상도다.
- 기존 자산을 덮어쓰지 않는다(출력 경로 분리) — 실제 교체는 unit 2.
