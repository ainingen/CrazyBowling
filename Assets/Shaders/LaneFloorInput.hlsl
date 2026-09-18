#ifndef CRAZYBOWLING_LANE_FLOOR_INPUT_INCLUDED
#define CRAZYBOWLING_LANE_FLOOR_INPUT_INCLUDED

// LaneFloor.shader が使う変数と計算。
// 各パスの中で、Core.hlsl を読んだあとに include すること。
// HLSLINCLUDE に置くと Core.hlsl がキーワードより先に展開され、
// MAIN_LIGHT_CALCULATE_SHADOWS が定義されず、影を受けなくなる。

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _LowColor;
    half4 _HighColor;
    half _Smoothness;
    half _Metallic;
    half _HeightStrength;
    float _HeightCenter;
    float _HeightSpan;
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// ガンマ空間との行き来。URP の Color.hlsl はパスによっては届かないので、
// 依存を作らずここで済ませる
half3 LaneToGamma(half3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
half3 LaneToLinear(half3 c) { return pow(max(c, 0.0001), 2.2); }

// 高さから床の色を作る。格子はこのあとで掛ける。
// 混ぜ算はガンマ空間で行う。リニア空間で混ぜると、茶色は青成分がほぼ0なので
// 「濃さ15%」でも青が何倍にもなってしまい、見た目が一気に紫になる
half3 CalculateFloorColor(float worldY, half4 grid)
{
    float span = max(abs(_HeightSpan), 0.0001);
    float t = saturate((worldY - _HeightCenter) / span + 0.5);

    half3 tintGamma = lerp(LaneToGamma(_LowColor.rgb), LaneToGamma(_HighColor.rgb), t);
    half3 mixedGamma = lerp(LaneToGamma(_BaseColor.rgb), tintGamma, _HeightStrength);

    return LaneToLinear(mixedGamma) * grid.rgb;
}

#endif
