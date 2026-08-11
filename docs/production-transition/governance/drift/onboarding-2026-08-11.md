# Onboarding Drift — 2026-08-11

> **DORMANT · OWNER-GATED · NOT DEMO AUTHORITY.** 이 drift는 Demo 수정 요청이 아니다. Project owner가 transition을 명시적으로 활성화할 때만 재검토한다.

> `record_id`: `PT-DRIFT-ONBOARDING-001`
>
> 상태: **stale claim 확인 · resolution 미검토**

## 역사적 claim

2026-07-29의 [`demo-baseline.md`](../../demo-baseline.md) `BASE-003`과
[`product/learning-register.md`](../../product/learning-register.md) `LRN-PROD-008`은 첫
복귀에서 Squad와 Dreamcatcher를 함께 지목하는 흐름을 현재처럼 설명한다.

## 현행 source

[`docs/spec/outgame-tutorial/README.md`](../../../spec/outgame-tutorial/README.md)는
2026-08-01~02 변경으로 다음 순서를 정본화했다.

```text
A -> B1 Squad -> B2 Dreamcatcher -> C Keyring -> E Restart
D History는 matchesPlayed >= 2의 독립 gate
```

따라서 옛 baseline, source map과 product claim은 current onboarding package 근거로 쓸 수
없다.

## 처리

- 관련 historical record는 `freshness: stale`로 유지한다.
- Product/Client package에 onboarding을 포함하려면 현행 spec commit을 `as_of_commit`으로
  새 record를 만들고 watch path, owner와 review를 등록한다.
- 이 foundation은 onboarding 내용을 다시 작성하거나 production-v1 포함 여부를 결정하지
  않는다.
