using UnityEngine;
using UnityEngine.Rendering;


namespace CustomRP.Runtime {
    public class Shadows {
        struct ShadowedDirectionalLight {
            public int VisibleLightIndex;
            public float SlopeScaleBias;
            public float NearPlaneOffset;
        }

        const string BufferName = "Shadows";
        const int MaxShadowedDirectionalLightCount = 4, MaxCascades = 4;

        private int ShadowedDirLightCount;
        private bool _useShadowMask;

        private static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
            CascadeCountId = Shader.PropertyToID("_CascadeCount"),
            CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
            // ShadowDistanceId = Shader.PropertyToID("_ShadowDistance");
            CascadeDataId = Shader.PropertyToID("_CascadeData"),
            ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
            ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

        private static readonly string[] DirectionalFilterKeywords = {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7",
        };

        static readonly string[] CascadeBlendKeywords = {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_DITHER"
        };

        static readonly string[] ShadowMaskKeywords = {
            "_SHADOW_MASK_ALWAYS",
            "_SHADOW_MASK_DISTANCE"
        };

        private static readonly Matrix4x4[]
            DirShadowMatrices = new Matrix4x4[MaxShadowedDirectionalLightCount * MaxCascades];

        static readonly Vector4[] CascadeCullingSpheres = new Vector4[MaxCascades],
            CascadeData = new Vector4[MaxCascades];

        readonly ShadowedDirectionalLight[] _shadowedDirectionalLights =
            new ShadowedDirectionalLight[MaxShadowedDirectionalLightCount];

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };


        ScriptableRenderContext _context;

        CullingResults _cullingResults;

        ShadowSettings _settings;


        public void Setup(
            ScriptableRenderContext context, CullingResults cullingResults,
            ShadowSettings settings) {
            this._context = context;
            this._cullingResults = cullingResults;
            this._settings = settings;
            ShadowedDirLightCount = 0;
            _useShadowMask = false;
        }


        public void Render() {
            if (ShadowedDirLightCount > 0) {
                RenderDirectionalShadows();
            }
            else {
                //在没有shadow需要渲染时生成1x1的纹理占位（不然会出问题
                _buffer.GetTemporaryRT(
                    DirShadowAtlasId, 1, 1,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
                );
            }

            _buffer.BeginSample(BufferName);
            SetKeywords(ShadowMaskKeywords,
                _useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1
            );
            _buffer.EndSample(BufferName);
            ExecuteBuffer();
        }


        /// <summary>
        /// 判断可以投射阴影且对当前视野内的场景有影响的Light之后存储相关信息在_shadowedDirectionalLights中
        /// 新增判断是否需要阴影遮罩
        /// </summary>
        /// <param name="light">场景中的Light</param>
        /// <param name="visibleLightIndex">此Light对应的索引</param>
        /// <returns>
        /// shader中会转化为DirectionalShadowData
        /// x：阴影强度shadowStrength 
        /// y：当前光源被安排在阴影图集内的偏移量
        /// z：light.shadowNormalBias
        /// w:多光源阴影遮罩所使用的贴图通道 比如第一个光源为R通道第二个光源为G通道 共四个
        /// </returns>
        public Vector4 ReserveDirectionalShadows(
            Light light, int visibleLightIndex) {
            if (
                ShadowedDirLightCount < MaxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f
            ) {
                float maskChannel = -1;
                LightBakingOutput lightBaking = light.bakingOutput;
                if (
                    lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
                ) {
                    _useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }

                if (!_cullingResults.GetShadowCasterBounds(
                        visibleLightIndex, out Bounds b
                    )) {
                    return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
                }

                _shadowedDirectionalLights[ShadowedDirLightCount] =
                    new ShadowedDirectionalLight {
                        VisibleLightIndex = visibleLightIndex,
                        SlopeScaleBias = light.shadowBias,
                        NearPlaneOffset = light.shadowNearPlane
                    };
                return new Vector4(
                    light.shadowStrength,
                    _settings.directional.cascadeCount * ShadowedDirLightCount++,
                    light.shadowNormalBias, maskChannel
                );
            }

            return new Vector4(0f, 0f, 0f, -1f);
        }


        public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex) {
            if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
                LightBakingOutput lightBaking = light.bakingOutput;
                if (
                    lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
                ) {
                    _useShadowMask = true;
                    return new Vector4(
                        light.shadowStrength, 0f, 0f,
                        lightBaking.occlusionMaskChannel
                    );
                }
            }

            return new Vector4(0f, 0f, 0f, -1f);
        }


        /// <summary>
        /// 在需要渲染阴影时创建ShadowMap集贴图（里面会包含所以有光线产生的ShadowMap）
        /// ShadowMap集->每一个光源对应的ShadowMap->每一个级联对应的shadowMap
        /// </summary>
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

            int tiles = ShadowedDirLightCount * _settings.directional.cascadeCount;
            //分割数量应是二的幂数 这样重视可以进行整数除法
            //否则会遇到不对齐的问题浪费吞吐空间
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;

            for (int i = 0; i < ShadowedDirLightCount; i++) {
                RenderDirectionalShadows(i, split, tileSize);
            }

            _buffer.SetGlobalInt(CascadeCountId, _settings.directional.cascadeCount);
            _buffer.SetGlobalVectorArray(CascadeCullingSpheresId, CascadeCullingSpheres);
            _buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);
            _buffer.SetGlobalVectorArray(CascadeDataId, CascadeData);
            //   _buffer.SetGlobalFloat(ShadowDistanceId, _settings.maxDistance);


            //此处阴影淡出分为两部分 一个是阴影最远距离的淡入参数存储为xy只影响与最大距离相关的淡入淡出，另一个是最大级联两侧的淡入淡出


            float f = 1f - _settings.directional.cascadeFade;
            _buffer.SetGlobalVector(
                ShadowDistanceFadeId,
                // (1 - d / m) / f 插值 在此处把m和f变为倒数可避免在shader里进行除法运算储存为xy
                // z中是为级联淡出准备的参数f = 1 - square(1 - f) 
                new Vector4(1f / _settings.maxDistance, 1f / _settings.distanceFade, 1f / (1f - f * f))
            );

            SetKeywords(DirectionalFilterKeywords, (int) _settings.directional.filter - 1);
            SetKeywords(CascadeBlendKeywords, (int) _settings.directional.cascadeBlend - 1);
            _buffer.SetGlobalVector(ShadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)); //x纹理大小 //y纹素大小
            _buffer.EndSample(BufferName);
            ExecuteBuffer();
        }

        /// <summary>
        /// 向GPU传输shadow所需要的数据
        /// ShadowMap集->每一个光源对应的ShadowMap->每一个级联对应的shadowMap
        /// </summary>
        /// <param name="index">投射阴影的光线的索引</param>
        /// <param name="split">ShadowMap集内有几个shadowMap 每个ShadowMap对应一个光源</param>
        /// <param name="tileSize">每一张shadowMap的级联的长宽像素（长宽相等）</param>
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
                    light.NearPlaneOffset, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                    out ShadowSplitData splitData);
                shadowSettings.splitData = splitData; //包含关于shadow投射对象如何被剔除的信息

                if (index == 0) {
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }

                int tileIndex = tileOffset + i;
                //    SetTileViewport(index, split, tileSize);
                DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                    projMatrix * viewMatrix,
                    SetTileViewport(tileIndex, split, tileSize),
                    split);

                _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                _buffer.SetGlobalDepthBias(0f, light.SlopeScaleBias);
                ExecuteBuffer();
                _context.DrawShadows(ref shadowSettings);
                _buffer.SetGlobalDepthBias(0f, 0f);
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

        /// <summary>
        /// 计算阴影级联的相关数据 所有光线只计算一次即可因为需要的数据都一样
        /// 填充CascadeData x为r平方的倒数，y为对应级联对应的shadowMap的纹素的距离
        /// </summary>
        /// <param name="index">当前等级级联Map的索引</param>
        /// <param name="cullingSphere">当前等级的级联Map的CullingSphere数据xyz为圆心位置，
        /// w为半径 经过此函数后w修改为半径的平方</param>
        /// <param name="tileSize">当前级联等级Map的大小</param>
        void SetCascadeData(int index, Vector4 cullingSphere, float tileSize) {
            //    CascadeData[index].x = 1f / cullingSphere.w;
            float texelSize = 2f * cullingSphere.w / tileSize;
            float filterSize = texelSize * ((float) _settings.directional.filter + 1f);
            cullingSphere.w -= filterSize;
            cullingSphere.w *= cullingSphere.w;
            CascadeCullingSpheres[index] = cullingSphere;
            CascadeData[index] = new Vector4(
                1f / cullingSphere.w,
                filterSize * 1.4142136f);
        }


        void SetKeywords(string[] keywords, int enabledIndex) {
            // int enabledIndex = (int) _settings.directional.filter - 1;
            for (int i = 0; i < keywords.Length; i++) {
                if (i == enabledIndex) {
                    _buffer.EnableShaderKeyword(keywords[i]);
                }
                else {
                    _buffer.DisableShaderKeyword(keywords[i]);
                }
            }
        }


        void ExecuteBuffer() {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }


        public void Cleanup() {
            _buffer.ReleaseTemporaryRT(DirShadowAtlasId);
            ExecuteBuffer();
        }
    }
}