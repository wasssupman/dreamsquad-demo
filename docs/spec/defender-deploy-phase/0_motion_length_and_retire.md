# 0 · 배치 모션 길이(파생 getter) — 가산 전용

## 목적

「배치 페이즈 길이 = 배치 모션 길이」를 데이터가 답하게 한다. **이 단위는 가산만** — 필드 삭제·클램프 제거는 unit 2 와 한 커밋(critic H-4: 따로 지우면
그 커밋 시점에 D&D 활성화 시계가 사라져 이 spec 이 고치려는 증상이 생긴다).

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `public float DeployMotionSeconds` getter
- 신규 `Assets/_Project/Tests/EditMode/DeployMotionSecondsTests.cs`(합성) · `Tests/EditMode.Assets/DeployMotionSecondsAssetTests.cs`(실에셋 불변식)

## 구현

```csharp
// BodyRadiusTiles 와 같은 «소유자가 파생값을 노출» 자리(제약 12 (a)).
// 명시 슬롯만 본다 — 폴백 체인(drag→attack→idle)은 «무엇을 틀까»의 답이지 «얼마나 기다릴까»의 답이 아니다.
// 백엔드 판별은 SpineUnitPool.TrySpawn 과 같은 게이트(HasIdle) — 시트가 그리는데 Spine 길이를 재는 일이 없게.
public float DeployMotionSeconds
{
    get
    {
        var sm = SpriteMotions;
        if (sm != null && sm.HasIdle)
            return sm.Deploy != null && sm.Deploy.FrameCount > 0 && sm.Deploy.Fps > 0f
                ? FlipbookMath.Duration(sm.Deploy.Fps, sm.Deploy.FrameCount) : 0f;
        if (skeletonDataAsset == null || string.IsNullOrEmpty(deployAnimation)) return 0f;
        var anim = skeletonDataAsset.GetSkeletonData(true)?.FindAnimation(deployAnimation);
        return anim != null ? anim.Duration : 0f;
    }
}
```
- `GetSkeletonData(true)` 는 에셋 로드(뷰 인스턴스 아님)이고 `skeletonData` 필드에 캐시된다(spine-unity `SkeletonDataAsset.cs:152·176`) — 헤드리스·EditMode 동일.

## 완료 기준

- 컴파일 0. 라이브 동작 무변(아직 소비자 없음).
- EditMode(합성): 시트 16f@24 → 0.667 · 시트 세트가 idle 만 있고 deploy 없음 → 0(Spine 으로 안 떨어진다) · Spine 트랙 이름 없음/빈 문자열 → 0.
- EditMode.Assets(불변식): 전 방어유닛에 대해 「명시 슬롯이 있으면 > 0, 없으면 == 0」 · 「스프라이트 세트 유닛은 시트 길이, 아니면 Spine 길이」. 정확한 수치 표는 README 가 든다.
