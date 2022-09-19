using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public partial class CameraRenderer {
        private ScriptableRenderContext _context;
        private Camera _camera;
        private CullingResults _cullingResults;
        private const string BufferName = "Render Camera";

        private static readonly ShaderTagId UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");


        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        public void Render(ScriptableRenderContext context, Camera renderingCamera) {
            this._context = context;
            this._camera = renderingCamera;
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull()) {
                return;
            }

            Setup();
            DrawVisibleGeometry();
            DrawUnsupportedShaders();
            DrawGizmos();
            Submit();
        }

        private void DrawVisibleGeometry() {
            //Draw Opaque
            var sortingSettings = new SortingSettings(_camera) {criteria = SortingCriteria.CommonOpaque};
            var drawingSettings = new DrawingSettings(UnlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = true,
                enableInstancing = false
            };
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);

            //Draw Sky Box
            _context.DrawSkybox(_camera);

            //Draw Transparent
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
        }

        private void Setup() {
            CameraClearFlags flags = _camera.clearFlags;
            _context.SetupCameraProperties(_camera);
            _buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? _camera.backgroundColor.linear : Color.clear);
            _buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private void Submit() {
            _buffer.EndSample(SampleName);
            ExecuteBuffer();
            _context.Submit();
        }

        void ExecuteBuffer() {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        bool Cull() {
            if (_camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
                _cullingResults = _context.Cull(ref p);
                return true;
            }

            return false;
        }
    }
}