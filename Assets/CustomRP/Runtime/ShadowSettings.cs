using UnityEngine;

namespace CustomRP.Runtime {
    [System.Serializable]
    public class ShadowSettings {
        public enum TextureSize {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }

        public enum FilterMode {
            PCF2x2,
            PCF3x3,
            PCF5x5,
            PCF7x7
        }

        public enum CascadeBlendMode {
            Hard,
            Soft,
            Dither
        }


        [System.Serializable]
        public struct Directional {
            public TextureSize atlasSize;
            public FilterMode filter; //PCF(软阴影采样数）
            [Range(1, 4)] public int cascadeCount;
            [Range(0f, 1f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
            [Range(0.001f, 1f)] public float cascadeFade;
            public CascadeBlendMode cascadeBlend;

            public Vector3 CascadeRatios =>
                new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
        }


        [System.Serializable]
        public struct Other {
            public TextureSize atlasSize;
            public FilterMode filter;
        }


        [Min(0.001f)] public float maxDistance = 100f;
        [Range(0.001f, 1f)] public float distanceFade = 0.1f;

        public Directional directional = new Directional {
            atlasSize = TextureSize._1024,
            filter = FilterMode.PCF2x2,
            cascadeCount = 4,
            cascadeRatio1 = 0.1f,
            cascadeRatio2 = 0.25f,
            cascadeRatio3 = 0.5f,
            cascadeFade = 0.1f,
            cascadeBlend = CascadeBlendMode.Hard
        };

        public Other other = new Other {
            atlasSize = TextureSize._1024,
            filter = FilterMode.PCF2x2
        };
    }
}