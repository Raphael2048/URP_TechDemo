using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class WindPass : ScriptableRenderPass
    {
        private static readonly int m_WindParamsID = Shader.PropertyToID("_WindParams");
        

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public WindPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            useNativeRenderPass = false;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Wind wind = VolumeManager.instance.stack.GetComponent<Wind>();
            Vector3 direction = Vector3.zero;
            float angle = Mathf.Deg2Rad * wind.WindDirection.value;
            cmd.SetGlobalVector(m_WindParamsID, new Vector4(Mathf.Cos(angle), 0, Mathf.Sin(angle), wind.WindIntensity.value));
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._WIND, false);
        }
    }
}
