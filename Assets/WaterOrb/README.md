# WaterOrb — Unity Package
A shader-based glowing water orb that matches the purple neon aesthetic.
All properties are live-editable in the Inspector.

---

## Quick Setup (2 minutes)

### 1. Import files
Copy the `Assets/WaterOrb/` folder into your Unity project's `Assets/` directory.

### 2. Create the Material
- In the Project window: **Right-click → Create → Material**
- Name it `WaterOrbMat`
- In the Inspector, change the shader to **Custom/WaterOrb**

### 3. Set up the GameObject
- Create a **Quad** (or Plane): `GameObject → 3D Object → Quad`
- Assign `WaterOrbMat` to the Quad's MeshRenderer
- Add the `WaterOrbController` component to the Quad
- Drag `WaterOrbMat` into the **Orb Material** field on the controller

### 4. Background
- Set the Camera's **Clear Flags** to `Solid Color` and **Background** to black `#050511`
- Or place the orb over any dark background in your scene

---

## Inspector Reference

### Fill & Wave
| Property | Description |
|---|---|
| Fill Amount | 0 = empty, 1 = full. Animate from code: `controller.SetFill(0.75f)` |
| Wave Amplitude | Height of the wave crests (0–0.15) |
| Wave Frequency | Horizontal density of wave 1 |
| Wave Speed | Horizontal scroll speed of wave 1 |
| Wave Frequency 2 | Secondary wave density |
| Wave Speed 2 | Secondary wave scroll speed |

### Water Colors
Three color stops define the water gradient (top → mid → bottom).
`Foam Color` is the bright highlight line on the wave surface.

### Interior Background
Two colors for the dark inside of the orb (inner glow → outer dark).

### Blob Shape
Controls the organic irregular outline of the orb.
- Radius: overall size
- Noise Freq/Amp 1 & 2: how bumpy the edge is

### Glow & Rim
- **Rim Color / Width**: thin bright edge around the blob
- **Glow Color / Radius / Intensity**: soft outer bloom

### Stars
Scattered twinkle points inside the orb, above the waterline.

### Inner Glint
Soft highlight in the top-left of the orb for a glass/liquid look.

---

## Controlling Fill from Code

```csharp
// Get reference
var orb = GetComponent<WaterOrbController>();

// Set immediately
orb.SetFill(0.5f);

// Animate over time (coroutine)
StartCoroutine(orb.AnimateFillTo(1f, duration: 2f));
```

---

## Color Presets

Create a preset asset:
**Assets → Create → WaterOrb → Color Preset**

Apply it at runtime:
```csharp
orb.ApplyPreset(myPreset);
```

---

## Demo Scene
Add `WaterOrbDemo.cs` to any GameObject, wire up:
- `orbController` → your WaterOrbController
- `fillSlider` → a UI Slider for manual control
- Check `autoAnimate` to watch it fill/drain automatically

---

## Compatibility
- **Unity 2020.3 LTS** and later
- Built-in Render Pipeline
- For URP/HDRP: change `Blend SrcAlpha OneMinusSrcAlpha` to a URP-compatible pass,
  or wrap the shader in a `UniversalRenderPipeline` SubShader block.
