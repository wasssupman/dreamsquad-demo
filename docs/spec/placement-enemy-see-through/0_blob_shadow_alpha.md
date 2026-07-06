# 0 — BlobShadow 페이드 훅

**작업 구분**: 토대 (공용)

## 목적

blob 그림자(적 발밑 바닥 스프라이트)도 dim 동안 함께 흐려지게, `BlobShadow` 에 알파 배수 setter 를
추가한다. unit 1·2 의 뷰가 이 API 를 호출한다. (그림자 얼룩이 남아 타일이 안 보이는 문제 방지.)

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs`

## 구현

- `Attach()` 가 만든 `SpriteRenderer` 를 필드로 캐시(`_sr`), 스폰 시 `color`(=`BlobShadowColor`)를
  `_baseColor` 로 보관. `authoredInPrefab` 경로(Awake)도 `_sr`/`_baseColor` 세팅.
- 공개 메서드:
  ```csharp
  public void SetDimAlpha(float factor) // factor ∈ [0,1]
  ```
  `_sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * factor);`
  `_sr == null` 이면 no-op. `factor` 는 clamp01.
- 매 프레임 호출돼도 저렴(Color 대입 1회). 별도 상태/코루틴 없음.

## 완료 기준

- compile 통과, 콘솔 무에러.
- `SetDimAlpha(1f)` 는 원래 그림자 색과 동일(회귀 없음).
- `SetDimAlpha(0.3f)` 호출 시 blob 스프라이트가 옅어짐(unit 1·2 배선 후 Play 로 실증).
- transform/스케일/정렬은 일절 안 건드림(색 알파만).
