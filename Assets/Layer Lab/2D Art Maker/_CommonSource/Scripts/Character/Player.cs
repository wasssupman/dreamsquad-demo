using System;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LayerLab.ArtMaker
{
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }
        [field: SerializeField] public PartsManager PartsManager { get; private set; } // 부품 매니저 참조 / Parts manager reference
        
        private void Awake()
        {
            Instance = this;
        }
    }
}