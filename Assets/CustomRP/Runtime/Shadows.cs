using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class Shadows {
        struct ShadowedDirectionalLight {
            public int VisibleLightIndex;
        }

        static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
        const string BufferName = "Shadows";
        const int MaxShadowedDirectionalLightCount = 4;


        readonly ShadowedDirectionalLight[] _shadowedDirectionalLights =
            new ShadowedDirectionalLight[MaxShadowedDirectionalLightCount];


        private int _shadowedDirectionalLightCount;

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        ScriptableRenderContext _context;

        CullingResults _cullingResults;

        ShadowSettings _settings;


        public void Setup(
            ScriptableRenderContext context, CullingResults cullingResults,
            ShadowSettings settings
        ) {
            this._context = context;
            this._cullingResults = cullingResults;
            this._settings = settings;
            _shadowedDirectionalLightCount = 0;
        }

        void ExecuteBuffer() {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        public void ReserveDirectionalShadows(Light light, int visibleLightIndex) {
            if (_shadowedDirectionalLightCount < MaxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None &&
                light.shadowStrength > 0f &&
                _cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b) //检查是否影响到了最大阴影距离内的物体
               ) {
                _shadowedDirectionalLights[_shadowedDirectionalLightCount] = new ShadowedDirectionalLight() {
                    VisibleLightIndex = visibleLightIndex
                };
                _shadowedDirectionalLightCount++;
            }
        }

        public void Render() {
            if (_shadowedDirectionalLightCount > 0) {
                RenderDirectionalShadows();
            }
            else {
                //在没有shadow需要渲染时生成1x1的纹理占位（不然会出问题
                _buffer.GetTemporaryRT(
                    DirShadowAtlasId, 1, 1,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
                );
            }
        }

        void RenderDirectionalShadows() {
            int atlasSize = (int) _settings.directional.atlasSize;
            //使用纹理的property ID 作为参数 声明需要commendBuffer创建一张正方形纹理
            _buffer.GetTemporaryRT(DirShadowAtlasId, atlasSize, atlasSize
                , 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);

            //开始渲染阴影纹理
            _buffer.SetRenderTarget(DirShadowAtlasId, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store);
            _buffer.ClearRenderTarget(true, false, Color.clear);
            _buffer.BeginSample(BufferName);
            ExecuteBuffer();
            int split = _shadowedDirectionalLightCount <= 1 ? 1 : 2;
            int tileSize = atlasSize / split;

            for (int i = 0; i < _shadowedDirectionalLightCount; i++) {
                RenderDirectionalShadows(i, split, tileSize);
            }

            _buffer.EndSample(BufferName);
            ExecuteBuffer();
        }

        void RenderDirectionalShadows(int index, int split, int tileSize) {
            ShadowedDirectionalLight light = _shadowedDirectionalLights[index];
            var shadowSettings =
                new ShadowDrawingSettings(_cullingResults, light.VisibleLightIndex);
            //这是我见过最长的函数名
            _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.VisibleLightIndex, 0, 1, Vector3.zero, tileSize,
                0f, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                out ShadowSplitData splitData);
            shadowSettings.splitData = splitData; //包含关于shadow投射对象如何被剔除的信息
            SetTileViewport(index, split, tileSize);
            _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            ExecuteBuffer();
            _context.DrawShadows(ref shadowSettings);
        }

        void SetTileViewport(int index, int split, float tileSize) {
            Vector2 offset = new Vector2(index % split, index / split);
            _buffer.SetViewport(new Rect(
                offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            ));
        }

        public void Cleanup() {
            _buffer.ReleaseTemporaryRT(DirShadowAtlasId);
            ExecuteBuffer();
        }
    }
}