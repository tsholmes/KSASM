// requires Gauges.glsl to be imported

#ifndef GLYPHATLAS_GLSL
#define GLYPHATLAS_GLSL

struct GlyphDef
{
  bool present; // if false, should never match
  float advance;
  vec4 plane; // rect inside rendered character to place glyph
  vec4 atlas; // rect in atlas texture
};

struct GlyphAtlas
{
  float distanceRange;
  vec2 size;
  float asc;
  float desc;
  float unY;
  float unWidth;
  GlyphDef[256] glyphs;
};

vec2 remap(vec2 pt, vec4 mapping)
{
  return (pt - mapping.xy) / (mapping.zw - mapping.xy);
}

bool uvValid(vec2 uv, vec2 expand)
{
  return uv.x >= -expand.x && uv.x <= 1+expand.x && uv.y >= -expand.y && uv.y <= 1+expand.y;
}

float sampleMTSDF_adj(sampler2D atlas, vec2 atlasUV, float glyphScale)
{
  vec4 tsdf = textureLod(atlas, atlasUV, 0.0);
  float normMS = median(tsdf.r, tsdf.g, tsdf.b) - 0.5;
  float normTS = tsdf.a - 0.5;
  float distMS = normMS * glyphScale;
  float distTS = normTS / fwidth(normTS);
  return distMS; // ignore true sdf for now
}

float sampleAtlas(GlyphAtlas atlas, sampler2D tex, uint c, vec2 uv)
{
  GlyphDef glyph = atlas.glyphs[c];
  if (!glyph.present)
    return -1e6;
  
  // x relative to advance, y relative to baseline
  vec2 originUv = mix(vec2(0, atlas.asc), vec2(glyph.advance, atlas.desc), uv);

  // 0-1 inside glyph rect
  vec2 glyphUv = remap(originUv, glyph.plane);
  // full atlas uv
  vec2 atlasUv = mix(glyph.atlas.xy, glyph.atlas.zw, glyphUv) / atlas.size;

  // how far outside the glyph rect the sdf field extends
  vec2 expand = vec2(atlas.distanceRange) / atlas.size;

  float pu = length(dFdx(uv));
  float pv = length(dFdy(uv));
  float scale = max(glyph.advance / pu, (atlas.desc - atlas.asc) / pv);
  scale = scale * atlas.distanceRange / global.camera.sdfResolution;

  float dist = sampleMTSDF_adj(tex, atlasUv, scale);
  if (!uvValid(glyphUv, expand))
    dist = -1e6;
  return dist;
}

#endif