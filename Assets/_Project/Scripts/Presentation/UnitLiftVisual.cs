using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    // flight-lift-feel unit 1 — lift(지면에서 뜬 view 공간 높이) → 시각 반응 세 배율.
    //
    // 왜 한 함수인가: 유닛 크기 · 그림자 크기 · 그림자 알파가 **같은 lift 에서 함께 파생된다**는 것이
    // 계약이다. 뷰마다 따로 계산하면 셋이 다른 lift 를 보고 갈라진다.
    //
    // 왜 "단위 높이당 비율" 인가: "이 연출의 apex 대비 몇 %" 로 정의하면 arcHeight 개념이 없는
    // 소비처(넉업 hop)가 기준을 만들 수 없다. 비율 정의는 **같은 높이 = 같은 크기**를 전 유닛에
    // 자동 보장한다 = 원근 일관성. 원근 자체가 거리에 대략 선형이라 물리적으로도 더 정직하다.
    //
    // 노브를 BattleBridge static 에서 직접 읽는 이유: Presentation 이 BlobShadowSprite/Size/Color 를
    // 그렇게 읽는 관용구 그대로다. 이 계산은 clamp+lerp 라 순수성을 위한 별도 파라미터 타입은 과잉.
    public static class UnitLiftVisual
    {
        public static void Resolve(float lift,
            out float unitScale, out float shadowScale, out float shadowAlpha)
        {
            // lift <= 0 = 지면 또는 반동으로 내려앉은 구간 — 전부 항등(반응 없음).
            if (lift <= 0f)
            {
                unitScale = 1f;
                shadowScale = 1f;
                shadowAlpha = 1f;
                return;
            }

            // 상한은 1 미만으로 내려가지 않는다 — 오설정이 유닛을 축소시키는 일이 없게.
            unitScale = Mathf.Min(1f + lift * BattleBridge.LiftScalePerHeight,
                                  Mathf.Max(1f, BattleBridge.LiftScaleMax));

            float r = Mathf.Clamp01(lift / Mathf.Max(0.01f, BattleBridge.LiftShadowFullHeight));
            shadowScale = Mathf.Lerp(1f, BattleBridge.LiftShadowMinScale, r);
            shadowAlpha = Mathf.Lerp(1f, BattleBridge.LiftShadowMinAlpha, r);
        }
    }
}
