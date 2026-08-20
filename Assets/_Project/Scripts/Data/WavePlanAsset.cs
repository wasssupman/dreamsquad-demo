using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // wave-authoring-test-mode unit 0 (rev unit 6) — 에디터에서 직접 작성하는 웨이브 플랜.
    // 각 웨이브 = durationSec(N) 구간이고, 스폰은 웨이브 상대 시각(0~N)으로 그룹마다 배치한다.
    // 웨이브들은 순차로 이어붙는다(웨이브 i 절대 시작 = 앞 웨이브 durationSec 합).
    [CreateAssetMenu(fileName = "WavePlan", menuName = "Wassup/WavePlan", order = 12)]
    public class WavePlanAsset : ScriptableObject
    {
        public string displayName = "Test Plan";

        [Tooltip("0 = endless: 시간제한 없음. 전 웨이브가 dispatch되고 적이 전멸하면 승리. " +
                 ">0 이면 해당 초에 타임아웃 승리(라이브와 동일).")]
        public float timerDurationSec = 0f;

        public List<AuthoredWave> waves = new();
    }

    [Serializable]
    public class AuthoredWave
    {
        [Tooltip("이 웨이브의 길이(초). 다음 웨이브는 이 시간만큼 뒤에서 시작한다.")]
        public float durationSec = 12f;

        [Tooltip("그룹의 count 마리를 펼치는 간격(초). 0 = 동시, >0 = 그 간격으로 순차.")]
        public float intervalSec = 0.5f;

        public List<AuthoredSpawnGroup> groups = new();
    }

    [Serializable]
    public class AuthoredSpawnGroup
    {
        [Tooltip("웨이브 시작 기준 상대 스폰 시각(0~durationSec).")]
        public float triggerTimeSec;

        [Tooltip("적 SO 를 드래그.")]
        public AttackUnitData unit;

        [Min(1)] public int count = 1;

        // first-run-tutorial unit 8 — 스폰 지점 저작. -1 = 무지정(기존 동작:
        // 펼침 순번 % 레인 수 라운드로빈). 런타임 WaveSpawnGroup 은 처음부터 이 축을
        // 갖고 있었고 ExpandWave/ResolveAuthoredLane 이 존중하는데, 저작 표면만 없어서
        // FromPlanAsset 이 3인자 생성자로만 불렀다 — 그 구멍을 막는다.
        //
        // 온보딩 1웨이브가 이 값을 쓴다: 배치 스킬 한 번이 한 덩어리를 통째로 처리하는
        // 장면을 만들려면 적이 한 레인으로 와야 한다(spec 계약 14 rev).
        [Min(-1)] public int laneIndex = -1;
    }
}
