using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class CustomRenderPipeline : RenderPipeline {
        private readonly CameraRenderer _renderer = new();
        readonly bool _useDynamicBatching;
        readonly bool _useGPUInstancing;

        public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSrpBatcher) {
            this._useDynamicBatching = useDynamicBatching;
            this._useGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSrpBatcher;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                _renderer.Render(context, camera, _useDynamicBatching, _useGPUInstancing);
            }
        }
    }
}