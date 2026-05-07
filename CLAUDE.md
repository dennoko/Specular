# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A lilToon custom shader extension for VRChat avatars that adds dual-layer Blinn-Phong specular highlights and a custom MatCap system. It integrates via lilToon's custom shader insert framework rather than creating standalone shaders.

**Target environment:** Unity 2022.3.22f1, lilToon v2.3.1+, VRChat

## Build & Development

There are no CLI build commands — shader compilation and testing occur inside the Unity Editor. Reload the project in Unity to recompile shaders after editing `.hlsl` or `.lilblock` files. C# changes in `Editor/` recompile automatically on Unity focus.

To validate the assembly definition compiles, check Unity's Console for errors after saving `Editor/CustomInspector.cs`.

## Architecture

### Extension Pattern
This project uses lilToon's **custom shader insert system**. Shaders are not standalone — they are composed by lilToon at build time using:
- `.lilcontainer` files: define each shader variant (opaque, cutout, transparent, outline, lite, etc.)
- `.lilblock` files: inject properties, metadata, and HLSL include paths into lilToon's shader pipeline
- `custom.hlsl`: the actual HLSL implementation, included via `lilCustomShaderInsert.lilblock`

### Component Map

| File | Role |
|------|------|
| [Shaders/custom.hlsl](Shaders/custom.hlsl) | Core HLSL: Blinn-Phong specular (2 layers), custom MatCap |
| [Shaders/lilCustomShaderProperties.lilblock](Shaders/lilCustomShaderProperties.lilblock) | All 68 shader property declarations |
| [Shaders/lilCustomShaderDatas.lilblock](Shaders/lilCustomShaderDatas.lilblock) | Shader name and editor class binding |
| [Editor/CustomInspector.cs](Editor/CustomInspector.cs) | Material inspector UI (extends `lilToonInspector`) |
| [Editor/dennoko_specularex.asmdef](Editor/dennoko_specularex.asmdef) | Editor-only assembly, references `lilToon.Editor` |

### HLSL Implementation (`custom.hlsl`)

Two identical specular layers (1st/2nd), each with:
- Mask texture + channel selection (R/G/B/A)
- Noise texture + channel selection
- Color texture or fixed color
- Intensity (texture or fixed, range 0–5)
- Smoothness (0–1) → maps to Blinn-Phong power (8–1024)

Custom MatCap with three blend modes (Add, Screen, Multiply), optional reflection-vector sampling, backface disable, and shadow/lighting interaction.

The channel selection system (`dnkw_pick_channel` / `DNKW_SAMPLE_SCALAR_CH` macro) allows packing R/G/B/A channels from a single texture to drive different scalar parameters.

### Inspector UI (`CustomInspector.cs`)

Extends `lilToon.lilToonInspector`. Property name convention:
- Shader properties: `_SpecColor1`, `_EnableSpec2`, `_SpecMask1_Channel`
- Tiling/Offset: `_ST` suffix (e.g., `_SpecMask1_ST`)
- HLSL macros: UPPERCASE
- Custom functions: `dnkw_` prefix, camelCase

### Shader Variants

`.lilcontainer` files cover: Opaque, Cutout, Transparent, OnePass/TwoPass Transparent, Gem, Refraction — each with and without outlines, plus `ltsl_*` lite variants.

## Key Constraints

- `Mat/` and `Tex/` directories are gitignored (local test materials/textures only); their `.meta` files are committed.
- The real shadow system is incompatible with this extension; users must revert to standard lilToon for shadow support.
- Assembly is Editor-only — no runtime C# code.
