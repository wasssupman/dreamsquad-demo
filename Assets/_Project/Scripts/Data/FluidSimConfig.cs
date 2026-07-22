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
        [Tooltip("velocity 감쇠 (클수록 빨리 잦아듦). 원본 0.2")]
        [Range(0f, 4f)] public float velocityDissipation = 0.2f;
        [Tooltip("dye(색) 감쇠 (클수록 빨리 옅어짐). 원본 1.0")]
        [Range(0f, 4f)] public float densityDissipation = 1f;
        [Tooltip("pressure 유지율 (다음 프레임으로 남기는 비율). 원본 0.8")]
        [Range(0f, 1f)] public float pressure = 0.8f;
        [Tooltip("vorticity(소용돌이 컬) 세기. 원본 30")]
        [Range(0f, 50f)] public float curl = 30f;

        [Header("Splat (색·힘 주입)")]
        [Tooltip("splat 반경 (정규화; unit 2 가 /100 + 화면비 보정). 원본 0.25")]
        [Range(0.01f, 1f)] public float splatRadius = 0.25f;
        [Tooltip("splat 힘 = velocity 주입 세기 (방향×이 값). 원본 6000")]
        public float splatForce = 6000f;

        [Header("Ambient 자율 구동")]
        [Tooltip("초당 자동 splat 횟수 (0 = 자동 없음, 외부 Splat() 만)")]
        [Range(0f, 10f)] public float ambientSplatsPerSecond = 2f;
        [Tooltip("자동 splat 색 팔레트 (비면 랜덤 HSV)")]
        public Color[] palette = new Color[0];

        [Header("Precision")]
        [Tooltip("half-float RT 우선 (미지원 시 자동 폴백). 끄면 full-float 우선")]
        public bool preferHalfFloat = true;
    }
}
