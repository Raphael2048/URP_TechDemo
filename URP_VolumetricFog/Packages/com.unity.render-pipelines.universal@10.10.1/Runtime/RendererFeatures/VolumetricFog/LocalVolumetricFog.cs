using System;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteInEditMode]
    public class LocalVolumetricFog : MonoBehaviour
    {
        public Color ForwardScatteringColor = Color.grey;
        public Color BackwardScatteringColor = Color.grey;
        public Color AmbientLight = new Color(0.5f, 0.5f, 0.5f);
        
        //内部体积雾
        [Range(0, 30)]
        public float InnerIntensity = 0;
        [Range(1, 300)]
        public float Distance = 60f;
        public Texture3D Noise = null;
        [Range(0.05f, 20)]
        public float NoiseTiling = 1;
        public Vector3 NoiseSpeed = Vector3.right;
        public Texture3D Detail = null;
        [Range(0.05f, 20)]
        public float DetailTiling = 1;
        public Vector3 DetailSpeed = Vector3.left;
        [Range(0, 1)]
        public float DetailIntensity = 0.5f;
        public bool EnableVolumetricLight = false;
        [Range(0, 1)]
        public float EdgeFade = 0.1f;

        public Vector3 Size = Vector3.one * 10;

        [Range(1, 3)]
        public int Quality = 1;
        public bool IsActive(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var bounds = new Bounds(transform.position, Size / 2);
            return InnerIntensity >0 && GeometryUtility.TestPlanesAABB(planes, bounds);
        }
        public static LocalVolumetricFog Instance
        {
            get;
            private set;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            Instance = null;
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        private void Update()
        {
            Instance = this;
        }
    }
}