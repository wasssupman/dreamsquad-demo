# 2 — 4종 리맵 · 임포트 · 할당

## 목적

unit 1 툴을 4종 컷신 뎁스에 돌려 실자산을 교체한다. Play 체감 판정은 unit 3.

## 변경 대상

기존 뎁스 PNG 를 **제자리 교체**(GUID 유지 → `deployCutsceneDepth` 재할당 불필요):

- `Sprites/Cutscene/Ranger/Depth/Ranger_003-depth.png` (640×360)
- `Sprites/Cutscene/Archer/Depth/Archer_depth.png` (320×180)
- `Sprites/Cutscene/Guardian/Depth/Guardian_depth.png` (90×90)
- `Sprites/Cutscene/Cannon/Depth/Cannon_depth.png` (138×102)

## 구현

기본 파라미터(`--levels 4 --near-keep 0.80 --near-lo 0.40 --body-hi 0.40 --blur-sigma 1.2`)로
4종 동일하게 돌린다. (rev2 2026-07-16 — 초기값은 접힘/잔상을 만들었다. `0_remap_contract.md` rev2)

```
python Tools~/depth_layer_remap.py <unit>/Depth/<d>.png <unit>/<대표색프레임>.png <같은 경로> --stats
```

대표 색 프레임(알파 마스크 소스): Ranger `Ranger_003` · Archer `Archer_025` ·
Guardian `Guardian_025` · Cannon `Cannon_049`(뎁스 bake 시 대표 프레임과 동일).

- **제자리 교체 → GUID 유지** → SO 참조·임포트 설정(.meta) 보존. 재할당·재임포트 설정 불필요.
  단 교체 후 `refresh_unity` + 임포트 설정이 R8 로 유지되는지 확인.
- `deployCutsceneTiltGain` 은 이번에 손대지 않는다(unit 3 튜닝 여지).

## 실측 — Guardian 특수 케이스 해소 (2026-07-16)

README 가 남겨둔 "Guardian 은 투명부 0% 라 배경 분리/opt-out 판단 필요" 는 **불필요**로 판명:

- 알파 마스크는 100%지만 **뎁스 자체가 이미 장식 배경을 분리**한다 — 원본의 65.5%가
  far(0.00~0.12)에 깔려 있다(`depth-parallax` unit 8 의 "스타버스트 배경은 완전 far 로 깔끔히
  분리" 관찰과 일치).
- 리맵 후 그 배경은 **최하단 계단으로 밀린다**(평균 0.021, 98%가 0.039 이하) = 무해.
- 실제 캐릭터(원본 d>0.15)는 4단에 퍼지고 **17.4%가 손 대역**(앞으로 뻗은 부츠 밑창).

→ **Guardian 도 다른 3종과 동일 파라미터로 처리한다. opt-out 하지 않는다.**

## rev2 실측 (2026-07-16, 사용자 Play 잔상 보고 후 재적용)

| 유닛 | 주차 base→ | 변위 | 접힘 base→ | grad base→ | 계단보존 |
|---|---|---|---|---|---|
| Ranger | 9.1→6.0% | +13% | 0.33→0.33% | 12.59→3.67 | 90.3% |
| Archer | 1.1→0.6% | +14% | 0.29→0.24% | 2.23→2.30 | 97.2% |
| Guardian | 4.3→0.5% | +14% | 0→0% | 0.65→0.70 | 93.9% |
| Cannon | 19.3→1.2% | **+60%** | 0→0% | 0.91→1.01 | 92.5% |

접힘 회귀 0(Ranger·Archer 는 오히려 개선), 계단 90%+ 유지.

## 완료 기준

- 4종 뎁스 PNG 가 리맵본으로 교체되고, `--stats` 수치가 위 rev2 표를 재현.
- **접힘 회귀 0** — 어느 유닛도 baseline 보다 접힘이 늘지 않는다(툴이 자동 경고).
- **GUID 불변** → 각 `Defender_*.asset` 의 `deployCutsceneDepth` 가 여전히 같은 텍스처를 가리킨다.
- 임포트 설정이 R8/linear/no-mip/무압축/Clamp 유지(`DepthMapBaker` 관례).
- 콘솔 error/warning 0.
- 몸통 픽셀의 **≥85%가 4단 평탄면**에 있다(계단이 램프로 뭉개지지 않았다는 회귀 가드).
- 되돌릴 길: Ranger/Archer/Guardian = `git checkout`, Cannon(미커밋) =
  `~/.cache/wassup-depth-baseline/Cannon_depth_preremap.png`.
