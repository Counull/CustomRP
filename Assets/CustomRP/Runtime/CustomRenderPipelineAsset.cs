using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace CustomRP.Runtime {
    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public class CustomRenderPipelineAsset : RenderPipelineAsset {
        [SerializeField] bool
            useDynamicBatching = true,
            useGPUInstancing = true,
            useSRPBatcher = true,
            useLightsPerObject = true;

        [SerializeField] ShadowSettings shadows = default;

        protected override RenderPipeline CreatePipeline() {
            return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject,
                shadows);
        }
    }
}