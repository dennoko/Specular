# lilToon Specular Extension
This is an extension for the lilToon Shader

---
## Features
- Adds a specular map to the lilToon shader
- Allows for more detailed control over the specular highlights on surfaces

---
## Parameters (per layer)
This extension provides two independent specular layers (1st / 2nd). Each layer has the same set of controls.

- Enable
  - Turns the specular for that layer on or off (by default, only 1st is enabled).

- Mask (R) + Channel
  - Mask texture defining where the specular applies. You can choose any channel (R/G/B/A).
  - White areas = applied more, black areas = not applied.
  - Supports Tiling/Offset for UV repetition and offset.

- Noise (R) + Channel
  - Noise multiplied to the specular intensity. Any channel (R/G/B/A) can be used.
  - Also supports Tiling/Offset similar to Mask.
  - Useful to add variation/grain to the highlight.

- Use Color Map (RGB) / Color
  - When ON, the Color Map (RGB) texture defines the specular color.
  - When OFF, the color picker value is used.

- Use Intensity Map / Intensity or Intensity Map (R) + Channel
  - When ON, uses a selected channel (R/G/B/A) of the Intensity Map to modulate intensity.
  - When OFF, uses the Intensity slider value (0–5).

- Use Smoothness Map / Smoothness or Smoothness Map (R) + Channel
  - When ON, uses a selected channel (R/G/B/A) of the Smoothness Map to modulate smoothness.
  - When OFF, uses the Smoothness slider value (0–1).
  - Higher smoothness makes the highlight sharper; lower values make it wider.

- Normal Strength (0–3)
  - Controls how strongly the normal map affects this layer.
  - 0: base normal (normal map disabled)
  - 1: regular normal map
  - 1–3: exaggerates the normal tilt (higher = stronger edges)

Tips:
- Color Map uses RGB. Intensity/Smoothness/Mask/Noise use a single scalar channel (R/G/B/A).
- Mask and Noise are multiplied together, and the layer contributes only when Enable is ON.

---
## LICENSE
This extension is licensed under the MIT License. See the LICENSE file for more details.
