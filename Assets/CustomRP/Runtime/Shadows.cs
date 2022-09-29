using UnityEngine;
using UnityEngine.Rendering;


namespace CustomRP.Runtime {
    public class Shadows {
        struct ShadowedDirectionalLight {
            public int VisibleLightIndex;
        }

        const string BufferName = "Shadows";
        const int MaxShadowedDirectionalLightCount = 4, MaxCascades = 4;

        private static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");

        private static readonly Matrix4x4[]
            DirShadowMatrices = new Matrix4x4[MaxShadowedDirectionalLightCount * MaxCascades];


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


        //找到对视图有影响的光源之后存储
        public Vector2 ReserveDirectionalShadows(
            Light light, int visibleLightIndex
        ) {
            if (
                _shadowedDirectionalLightCount < MaxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f &&
                _cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
            ) {
                _shadowedDirectionalLights[_shadowedDirectionalLightCount] =
                    new ShadowedDirectionalLight {
                        VisibleLightIndex = visibleLightIndex
                    };
                return new Vector2(light.shadowStrength,
                    _settings.directional.cascadeCount * _shadowedDirectionalLightCount++
                );
            }

            return Vector3.zero;
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

            int tiles = _shadowedDirectionalLightCount * _settings.directional.cascadeCount;
            //分割数量应是二的幂数 这样重视可以进行整数除法
            //否则会遇到不对齐的问题浪费吞吐空间
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;

            for (int i = 0; i < _shadowedDirectionalLightCount; i++) {
                RenderDirectionalShadows(i, split, tileSize);
            }

            _buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);

            _buffer.EndSample(BufferName);
            ExecuteBuffer();
        }

        void RenderDirectionalShadows(int index, int split, int tileSize) {
            ShadowedDirectionalLight light = _shadowedDirectionalLights[index];
            var shadowSettings =
                new ShadowDrawingSettings(_cullingResults, light.VisibleLightIndex);
            int cascadeCount = _settings.directional.cascadeCount;
            int tileOffset = index * cascadeCount;
            Vector3 ratios = _settings.directional.CascadeRatios;
            for (int i = 0; i < cascadeCount; i++) {
                //这是我见过最长的函数名
                _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light.VisibleLightIndex, i, cascadeCount, ratios, tileSize,
                    0f, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                    out ShadowSplitData splitData);
                shadowSettings.splitData = splitData; //包含关于shadow投射对象如何被剔除的信息
                int tileIndex = tileOffset + i;
                //    SetTileViewport(index, split, tileSize);
                DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                    projMatrix * viewMatrix,
                    SetTileViewport(tileIndex, split, tileSize),
                    split);
                
                _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                ExecuteBuffer();
                _context.DrawShadows(ref shadowSettings);
            }
        }

        Vector2 SetTileViewport(int index, int split, float tileSize) {
            Vector2 offset = new Vector2(index % split, index / split);
            _buffer.SetViewport(new Rect(
                offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            ));
            return offset;
        }


        Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
            if (SystemInfo.usesReversedZBuffer) {
                m.m20 = -m.m20;
                m.m21 = -m.m21;
                m.m22 = -m.m22;
                m.m23 = -m.m23;
            }

            float scale = 1f / split;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
            return m;
        }

        public void Cleanup() {
            _buffer.ReleaseTemporaryRT(DirShadowAtlasId);
            ExecuteBuffer();
        }
    }
}