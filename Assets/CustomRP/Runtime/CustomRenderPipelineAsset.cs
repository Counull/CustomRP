using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace CustomRP.Runtime {
    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public class CustomRenderPipelineAsset : RenderPipelineAsset {
        [SerializeField] bool useDynamicBatching = true;

        [SerializeField] bool useGPUInstancing = true;

         [SerializeField] private bool useSrpBatcher = true;

        protected override RenderPipeline CreatePipeline() {
            return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSrpBatcher);
        }
    }
}