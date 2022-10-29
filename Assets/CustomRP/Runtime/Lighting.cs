using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class Lighting {
        const string BufferName = "Lighting";
        const int MaxDirLightCount = 4, MaxOtherLightCount = 64;
        readonly Shadows _shadows = new Shadows();

        const string LightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

        static readonly int
            DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

        private static readonly int otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
            otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
            otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions"),
            otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
            otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
            otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");


        static readonly Vector4[]
            DirLightColors = new Vector4[MaxDirLightCount],
            DirLightDirections = new Vector4[MaxDirLightCount],
            DirLightShadowData = new Vector4[MaxDirLightCount];

        static readonly Vector4[] otherLightColors = new Vector4[MaxOtherLightCount],
            otherLightPositions = new Vector4[MaxOtherLightCount],
            otherLightDirections = new Vector4[MaxOtherLightCount],
            otherLightSpotAngles = new Vector4[MaxOtherLightCount],
            otherLightShadowData = new Vector4[MaxOtherLightCount];

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        private CullingResults _cullingResults;

        public void Setup(ScriptableRenderContext context,
            CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject) {
            this._cullingResults = cullingResults;
            _buffer.BeginSample(BufferName);
            //SetupDirectionalLight();
            _shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights(useLightsPerObject);
            _shadows.Render();
            _buffer.EndSample(BufferName);

            context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void SetupLights(bool useLightsPerObject) {
            NativeArray<int> indexMap = useLightsPerObject ? _cullingResults.GetLightIndexMap(Allocator.Temp) : default;
            NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;

            int dirLightCount = 0, otherLightCount = 0;
            int i;
            for (i = 0; i < visibleLights.Length; i++) {
                int newIndex = -1;
                VisibleLight visibleLight = visibleLights[i];
                switch (visibleLight.lightType) {
                    case LightType.Directional:
                        if (dirLightCount < MaxDirLightCount) {
                            SetupDirectionalLight(dirLightCount++, ref visibleLight);
                        }

                        break;
                    case LightType.Point:
                        if (otherLightCount < MaxOtherLightCount) {
                            newIndex = otherLightCount;
                            SetupPointLight(otherLightCount++, ref visibleLight);
                        }

                        break;

                    case LightType.Spot:
                        if (otherLightCount < MaxOtherLightCount) {
                            newIndex = otherLightCount;
                            SetupSpotLight(otherLightCount++, ref visibleLight);
                        }

                        break;
                }

                if (useLightsPerObject) {
                    indexMap[i] = newIndex;
                }
            }

            if (useLightsPerObject) {
                for (; i < indexMap.Length; i++) {
                    indexMap[i] = -1;
                }

                _cullingResults.SetLightIndexMap(indexMap);
                indexMap.Dispose();
                Shader.EnableKeyword(LightsPerObjectKeyword);
            }
            else {
                Shader.DisableKeyword(LightsPerObjectKeyword);
            }

            _buffer.SetGlobalInt(DirLightCountId, dirLightCount);

            if (dirLightCount > 0) {
                _buffer.SetGlobalVectorArray(DirLightColorsId, DirLightColors);
                _buffer.SetGlobalVectorArray(DirLightDirectionsId, DirLightDirections);
                _buffer.SetGlobalVectorArray(DirLightShadowDataId, DirLightShadowData);
            }

            _buffer.SetGlobalInt(otherLightCountId, otherLightCount);
            if (otherLightCount > 0) {
                _buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
                _buffer.SetGlobalVectorArray(otherLightPositionsID, otherLightPositions);
                _buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
                _buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
                _buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
            }
        }


        /// <summary>
        /// only sun
        /// </summary>
        /// <param name="index">Index of visibleLight</param>
        /// <param name="visibleLight">VisibleLight object</param>
        void SetupDirectionalLight(int index, ref VisibleLight visibleLight) {
            DirLightColors[index] = visibleLight.finalColor;

            //local to world matrix XYZ为世界坐标下的local坐标系的XYZ轴（基）
            //  |Xx Yx Zx 0|
            //  |Xy Yy Zy 0|
            //  |Xz Yz Zz 0|
            //  | 0  0  0 1|
            DirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            DirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }


        void SetupPointLight(int index, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightPositions[index] = position;
            otherLightSpotAngles[index] = new Vector4(0f, 1f);
            Light light = visibleLight.light;
            otherLightShadowData[index] = _shadows.ReserveOtherShadows(light, index);
        }

        void SetupSpotLight(int index, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w =
                1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightPositions[index] = position;
            otherLightDirections[index] =
                -visibleLight.localToWorldMatrix.GetColumn(2);

            //计算聚光灯的衰减
            Light light = visibleLight.light;
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
            float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
            otherLightSpotAngles[index] = new Vector4(
                angleRangeInv, -outerCos * angleRangeInv
            );
            otherLightShadowData[index] = _shadows.ReserveOtherShadows(light, index);
        }


        public void Cleanup() {
            _shadows.Cleanup();
        }
    }
}