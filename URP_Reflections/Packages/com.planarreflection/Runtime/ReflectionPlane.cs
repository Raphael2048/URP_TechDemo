using UnityEngine;

namespace H3D.URP
{
    [ExecuteInEditMode]
    public class ReflectionPlane : MonoBehaviour
    {
        public enum PlanarReflectionTechnique
        {
            Usual,
            PixelProjected
        }
        public static ReflectionPlane Instance
        {
            get;
            private set;
        }
        public enum ReflectAxis
        {
            P_X = 0,
            N_X = 1,
            P_Y = 2,
            N_Y = 3,
            P_Z = 4,
            N_Z = 5
        }

        public enum ReflectTexSize
        {
            _Full,
            _Half,
            _Quarter,
        }
        
        public PlanarReflectionTechnique m_Technique = PlanarReflectionTechnique.Usual;
        // 通用
        public ReflectAxis m_ReflectAxis = ReflectAxis.P_Y;

        public ReflectTexSize m_TextureSize;
        [Range(-1.0f, 1.0f)]
        public float m_ClipPlaneOffset = 0.0f;
        
        public bool m_GradientIntensity = false;
        [Range(0.01f, 10)]
        public float m_GradientDistance = 4;
        [Range(0, 2)]
        public int m_BlurPower = 0;
        public bool m_AdaptiveBlur = false;
        
        // For Usual
        public uint m_RenderLayerMask = 0x00000001;

        public bool m_ReflectSky = false;

        private void Update()
        {
            Instance = this;
        }

        void OnEnable()
        {
            Instance = this;
        }

        //Cleanup all the objects we possibly have created
        void OnDisable()
        {
            Instance = null;
        }

        public bool Configurable()
        {
            if (!enabled)
            {
                return false;
            }
            return true;
        }

        public Vector3 GetPlanePosition()
        {
            return transform.position;
        }

        public Vector3 GetPlaneNormal()
        {
            Vector3 normal = transform.up;
            switch (m_ReflectAxis)
            {
                case ReflectAxis.P_X:
                    normal = transform.right;
                    break;
                case ReflectAxis.N_X:
                    normal = -transform.right;
                    break;
                case ReflectAxis.P_Y:
                    normal = transform.up;
                    break;
                case ReflectAxis.N_Y:
                    normal = -transform.up;
                    break;
                case ReflectAxis.P_Z:
                    normal = transform.forward;
                    break;
                case ReflectAxis.N_Z:
                    normal = -transform.forward;
                    break;
            }
            return normal;
        }
    }
}