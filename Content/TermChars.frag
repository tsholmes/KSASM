#version 450

#include "../Core/Shaders/Common/Global.glsl"
#include "../Core/Shaders/Common/Gauges.glsl"
#include "GlyphAtlas.glsl"
#include "TermFont.glsl"

const int TERM_MAX_WIDTH = 32;
const int TERM_MAX_HEIGHT = 16;
const int TERM_MAX_CHARS = TERM_MAX_WIDTH * TERM_MAX_HEIGHT;

layout(set = 1, binding = 1) uniform sampler2D termAtlas;
layout(set = 1, binding = 2) uniform TermData {
  ivec4[TERM_MAX_CHARS/4/4] termChars;
  uint termWidth;
  uint termHeight;
  float termWeight;
  float termEdge;
  uint termBg;
  uint termFg;
};

layout(location = 0) out vec4 outColor;

uint termChar(uint idx)
{
  uint c4 = termChars[idx/16][(idx/4)%4];
  return bitfieldExtract(c4, int(idx%4)*8, 8);
}

void main()
{
  vec2 instSz = instUv.zw - instUv.xy;
  vec2 locUv = (inUv - instUv.xy) / instSz;

  vec2 termSz = vec2(float(termWidth), vec2(termHeight));
  vec2 scaledUv = locUv * termSz;

  uint row = uint(floor(scaledUv.y));
  uint col = uint(floor(scaledUv.x));
  uint cidx = row*termWidth + col;
  locUv = fract(scaledUv);

  if (cidx < 0 || cidx >= termWidth * termHeight)
    discard;
  
  float dist = sampleAtlas(TERM_ATLAS, termAtlas, termChar(cidx), locUv) + termWeight;
  float a = smoothstep(0, 1, (dist - termEdge/2) / termEdge);
  outColor = vec4(mix(INDEXED_COLOR[termBg], INDEXED_COLOR[termFg], a), 1);
}