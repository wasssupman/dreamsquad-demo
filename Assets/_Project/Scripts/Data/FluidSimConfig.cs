using UnityEngine;

namespace Wassup.Data
{
    // fluid-paint-mixing unit 0 — 축소 유체 솔버 튜닝 파라미터 (PavelDoGreat/WebGL-Fluid-Simulation 이식, MIT).
    // 하드코딩 금지(제약 6): 해상도·pressure 반복·dissipation·curl·splat·색 팔레트·앰비언트 cadence 전부 여기서.
    // FluidPaintSim(Presentation, unit 2)이 읽어 Graphics.Blit 패스 체인을 구성한다. 순수 데이터 — 로직 없음.
    [CreateAssetMenu(fileName = "FluidSimConfig", menuName = "Wassup/FluidSimConfig", order = 30)]
    public class FluidSimConfig : ScriptableObject
    {
        [Header("Resolution (짧은 변 기준; 실제 w/h 는 화면비로 산출)")]
        [Tooltip("시뮬(velocity/pressure) 해상도. 낮을수록 싸고 뭉근하다. 원본 기본 128")]
        [Range(32, 256)] public int simResolution = 128;
        [Tooltip("염료(dye/색) 해상도 = 시각 선명도. 모바일은 256~512 권장 (원본 기본 1024는 과함)")]
        [Range(64, 1024)] public int dyeResolution = 256;

        [Header("Solver")]
        [Tooltip("pressure Jacobi 반복. 높을수록 비압축성(소용돌이) 정확·비쌈. 원본 20")]
        [Range(1, 50)] public int pressureIterations = 20;
        [Tooltip("velocity 감쇠 (클수록 빨리 잦아듦). 흐름 유지 위해 낮게. 원본 0.2")]
        [Range(0f, 4f)] public float velocityDissipation = 0.15f;
        [Tooltip("dye(색) 감쇠 (클수록 빨리 옅어짐). 낮을수록 색이 흐르며 남는다(깜빡임↓). 원본 1.0")]
        [Range(0f, 4f)] public float densityDissipation = 0.4f;
        [Tooltip("pressure 유지율 (다음 프레임으로 남기는 비율). 원본 0.8")]
        [Range(0f, 1f)] public float pressure = 0.8f;
        [Tooltip("vorticity(소용돌이 컬) 세기. 원본 30")]
        [Range(0f, 50f)] public float curl = 22f;

        [Header("Splat (색·힘 주입)")]
        [Tooltip("splat 반경 (정규화; unit 2 가 /100 + 화면비 보정). 원본 0.25")]
        [Range(0.01f, 1f)] public float splatRadius = 0.25f;
        [Tooltip("splat 힘 = velocity 주입 세기 (방향×이 값). 원본 6000")]
        public float splatForce = 6000f;

        [Header("Ambient 흐름 (연속 유동 — 방울 터짐 아님)")]
        [Tooltip("떠다니는 유동 방출기 수 (0 = 자동 없음). 각 방출기가 매 프레임 부드럽게 색·힘을 흘려 넣는다")]
        [Range(0, 6)] public int ambientEmitters = 3;
        [Tooltip("방출기 배회 속도 (느릴수록 잔잔)")]
        [Range(0f, 1f)] public float ambientDrift = 0.14f;
        [Tooltip("방출기가 매 프레임 넣는 velocity 세기 (작을수록 잔잔한 흐름, 크면 격류)")]
        [Range(0f, 60f)] public float ambientFlow = 11f;
        [Tooltip("방출기가 매 프레임 더하는 색 양 (작을수록 은은히 쌓임, 크면 번쩍)")]
        [Range(0f, 0.5f)] public float ambientColorAmount = 0.055f;
        [Tooltip("색 순환 속도 (리본 색이 서서히 변함)")]
        [Range(0f, 1f)] public float ambientColorCycle = 0.05f;
        [Tooltip("색 팔레트 (비면 순환 HSV)")]
        public Color[] palette = new Color[0];

        [Header("Precision")]
        [Tooltip("half-float RT 우선 (미지원 시 자동 폴백). 끄면 full-float 우선")]
        public bool preferHalfFloat = true;
    }
}
