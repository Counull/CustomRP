using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class CustomRenderPipeline : RenderPipeline {
        private readonly CameraRenderer _renderer = new();
        readonly bool _useDynamicBatching;
        readonly bool _useGPUInstancing;
        private readonly ShadowSettings _shadowSettings;

        public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSrpBatcher,
            ShadowSettings shadowSettings) {
            this._shadowSettings = shadowSettings;
            this._useDynamicBatching = useDynamicBatching;
            this._useGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSrpBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                _renderer.Render(context, camera, _useDynamicBatching, _useGPUInstancing, _shadowSettings);
            }
        }
    }
}