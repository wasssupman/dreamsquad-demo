# Unit 6 — 활성 RenderTexture 해제 방어

## 목적

유체 sim을 끄는 프레임에 마지막 Blit 대상이 `RenderTexture.active`로 남아 있더라도,
소유 RT를 콘솔 에러 없이 정리한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/Fluid/FluidRenderTargets.cs`
- `Assets/_Project/Tests/EditMode/FluidRenderTargetsTests.cs`

## 구현

`FluidRenderTargets.Destroy`가 해제할 RT와 현재 활성 RT가 같을 때만 활성 대상을 `null`로
비운 뒤 기존 `Release`/`Destroy` 수명주기를 수행한다. 다른 렌더 타깃은 건드리지 않는다.

새 인터페이스·매니저·ECS 통신은 추가하지 않는다.

## 완료 기준

- [x] 소유 RT를 활성 대상으로 둔 뒤 `Release`해도 콘솔 에러가 없고 `RenderTexture.active`가 비워진다.
- [x] 웨이포인트 PlayMode 씬 전환 테스트에서 활성 RT 해제 에러가 재발하지 않는다.
- [x] 컴파일 및 관련 EditMode 테스트 통과.

자동 검증 2026-08-11: 회귀 테스트 1/1 · 웨이포인트 PlayMode 1/1 · 전체 EditMode 2,150건(실패 0, 기존 Ignore 3). 제품 콘솔 에러 0.
