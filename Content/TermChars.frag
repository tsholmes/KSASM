#version 450

#include "../Core/Shaders/Common/Global.glsl"
#include "../Core/Shaders/Common/Gauges.glsl"
#include "GlyphAtlas.glsl"
#include "TermFont.glsl"

layout(set = 1, binding = 1) uniform sampler2D termAtlas;

layout(location = 0) out vec4 outColor;

void main()
{
  vec2 instSz = instUv.zw - instUv.xy;
  vec2 locUv = (inUv - instUv.xy) / instSz;

  uint ccount = bitfieldExtract(inData1.x, 0, 4) + 1;
  uint bg = bitfieldExtract(inData1.x, 4, 4);
  uint fg = bitfieldExtract(inData1.x, 8, 4);
  uint cidx = uint(floor(locUv.x * float(ccount)));
  locUv.x = locUv.x * float(ccount) - float(cidx);
  uint packed = inData[cidx/4];
  int offset = int((cidx % 4) * 8);
  uint c = bitfieldExtract(packed, offset, 8);
  
  float weight = float(bitfieldExtract(inData1.y, 0, 16))/pow(2,15) - 1;
  float edge = float(bitfieldExtract(inData1.y, 16, 16))/pow(2,16);

  float d = sampleAtlas(TERM_ATLAS, termAtlas, c, locUv) + weight;

  float a = smoothstep(0, 1, (d - edge/2) / edge);
  
  outColor = vec4(mix(INDEXED_COLOR[bg], INDEXED_COLOR[fg], a), 1);
}