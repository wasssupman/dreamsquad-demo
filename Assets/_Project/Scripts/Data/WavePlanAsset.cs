using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // wave-authoring-test-mode unit 0 — 에디터에서 직접 작성하는 웨이브 플랜.
    // seed 생성(WavePatternGenerator) 을 대체하는 테스트 모드 입력. 런타임은
    // 이 SO 를 GeneratedWavePlan(N-entry) 으로 변환해 소비한다(unit 2).
    [CreateAssetMenu(fileName = "WavePlan", menuName = "Wassup/WavePlan", order = 12)]
    public class WavePlanAsset : ScriptableObject
    {
        public string displayName = "Test Plan";

        [Tooltip("0 = endless: 시간제한 없음. 전 웨이브가 dispatch되고 적이 전멸하면 승리. " +
                 ">0 이면 해당 초에 타임아웃 승리(라이브와 동일).")]
        public float timerDurationSec = 0f;

        [Tooltip("웨이브 내 개별 스폰 사이 간격(초). 기존 intraWaveSpacing 과 동일 의미.")]
        public float intraWaveSpacingSec = 0.35f;

        public List<AuthoredWave> waves = new();
    }

    [Serializable]
    public class AuthoredWave
    {
        [Tooltip("이 웨이브가 호출되는 시각(초). 오름차순 권장.")]
        public float triggerTimeSec;
        public List<AuthoredSpawnGroup> groups = new();
    }

    [Serializable]
    public class AuthoredSpawnGroup
    {
        [Tooltip("적 SO 를 드래그.")]
        public AttackUnitData unit;
        [Min(1)] public int count = 1;
    }
}
