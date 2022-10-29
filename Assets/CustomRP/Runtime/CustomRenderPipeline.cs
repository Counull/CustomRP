using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public partial class CustomRenderPipeline : RenderPipeline {
        private readonly CameraRenderer _renderer = new();

        readonly bool _useDynamicBatching;
        readonly bool _useGPUInstancing;
        readonly bool _useLightsPerObject;

        private readonly ShadowSettings _shadowSettings;

        public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing,
            bool useSrpBatcher, bool useLightsPerObject, ShadowSettings shadowSettings) {
            this._shadowSettings = shadowSettings;
            this._useDynamicBatching = useDynamicBatching;
            this._useGPUInstancing = useGPUInstancing;
            this._useLightsPerObject = useLightsPerObject;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSrpBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;

            InitializeForEditor();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                _renderer.Render(context, camera, _useDynamicBatching, _useGPUInstancing, _useLightsPerObject,_shadowSettings);
            }
        }
    }
}