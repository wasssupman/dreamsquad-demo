# 3 — 뷰·풀·프리팹 (DamageNumberView)

## 목적

월드 공간 데미지 숫자 팝업을 자체 애니메이션(펀치 스케일-인 → 위로 드리프트 → 페이드/축소)으로 재생하고, 카메라를 향해 빌보드한다. 데미지 크기에 따라 **폰트 크기 + 색(흰→노랑→주황→빨강) + 펀치 강도**가 비례한다. GC 스파이크 방지를 위해 풀에서 재사용.

## 변경 대상 (신규)

- `Assets/_Project/Scripts/Presentation/DamageNumberStyle.cs` — 직렬화 튜닝 묶음
- `Assets/_Project/Scripts/Presentation/DamageNumberView.cs` — 팝업 1개 애니메이션
- `Assets/_Project/Scripts/Presentation/DamageNumberPool.cs` — 경량 풀(plain C#)
- `Assets/_Project/VFX/DamageNumber_Popup.prefab` — TextMeshPro(3D) + Bangers SDF + 아웃라인 머티리얼 + `DamageNumberView`

## 구현

### DamageNumberStyle (모든 수치는 여기 + Inspector)

- `lifetime`, `driftUp`(월드 상승 거리), `minFontSize`/`maxFontSize`(월드 TMP 크기), `lowDamage`/`highDamage`(정규화 임계), `damageColor`(Unity `Gradient`, 흰→노랑→주황→빨강), `bigHitPunchMul`(큰 히트 펀치 증폭), `scaleCurve`/`alphaCurve`(AnimationCurve).
- `Normalize(amount) = clamp01((amount-low)/(high-low))`.
- `EvaluateColor(t) = damageColor.Evaluate(t)`.
- 커브/그라데이션이 비어 있으면 `SetDefaults()` 로 합리적 기본값(오버슈트 펀치, 0.8s 수명 등).

### DamageNumberView

- `Play(int amount, Vector3 worldPos, Camera cam, in DamageNumberStyle style, Action<DamageNumberView> onComplete)`.
- `t = style.Normalize(amount)` → 폰트 크기 `Lerp(min,max,t)`, 색 `EvaluateColor(t)`.
- 매 프레임: 위치 = start + up·(driftUp·n); 스케일 = `1 + (scaleCurve(n)-1)·Lerp(1, bigHitPunchMul, t)`(큰 히트일수록 오버슈트↑); 페이스 알파 = `alphaCurve(n)`.
- `LateUpdate`: `transform.rotation = cam.transform.rotation`(완전 빌보드, 가독성).
- 끝나면 `SetActive(false)` + `onComplete(this)` 로 풀 반환.
- MeshRenderer `sortingOrder` 를 크게(유닛 위 렌더).

### DamageNumberPool

- plain C# 클래스. 생성자: `(GameObject prefab, Transform parent)`.
- `Get()` → 비활성 인스턴스 재사용 또는 Instantiate. `Return(view)` → Queue 적재.
- `DamageNumberView` 의 `onComplete` 가 `Return` 을 호출.

### 프리팹 생성 (execute_code, 스크립트 컴파일 후)

- 빈 GameObject → `AddComponent<TextMeshPro>()`(3D), `font = Bangers SDF`, `fontSharedMaterial = DamageNumber Outline Mat`, 가운데 정렬, `AddComponent<DamageNumberView>()`.
- `SaveAsPrefabAsset` → `Assets/_Project/VFX/DamageNumber_Popup.prefab`. 씬 임시 오브젝트 삭제.

## 계약/주의

- 색·수명·드리프트·임계·커브는 전부 `DamageNumberStyle`(직렬화) — 코드 상수 하드코딩 금지.
- 빌보드는 카메라 회전 복사(전투 카메라 yaw=0, pitch 고정이지만 회전 복사로 일반화).
- 페이스 알파만 페이드(아웃라인은 머티리얼 고정) — 0.8s 단발이라 충분. 전체 알파/세로 그라데이션은 후속 후보.
- Dynamic SDF 라 숫자 글리프는 unit 2 에서 사전 추가됨.

## 완료 기준

- compile: CS 에러 0.
- 프리팹 `DamageNumber_Popup.prefab` 존재 + TMP(폰트=Bangers SDF, 머티리얼=아웃라인) + `DamageNumberView` 부착(검토).
- 실제 표시/튜닝은 unit 4 Play 에서.

✅ 2026-06-05 compile 클린 + 프리팹 생성 확인(prefab/tmp+view/font/mat 모두 True). 커밋 대기.
