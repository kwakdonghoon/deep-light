// Seamless 64x64 water-caustic pattern: thin bright squiggles on a transparent background.
// The game tiles it over the whole screen at low alpha and scrolls it slowly, so the seabed shimmers like light through waves.
// Build: node <pxl>/bin/pxl.js build caustics.px.js --out out_caustics
pal({ q: "#bfeaff", c: "#7fd3f5" })

const N = 64, TAU = Math.PI * 2
const out = []
for (let y = 0; y < N; y++)
  for (let x = 0; x < N; x++) {
    // two interfering wave fields with integer cycles per tile, so the pattern wraps without a seam
    const s1 = Math.sin(TAU * (2 * x / N) + Math.sin(TAU * 3 * y / N) * 1.3)
    const s2 = Math.sin(TAU * (2 * y / N) + Math.sin(TAU * 2 * x / N) * 1.5)
    const v = Math.abs(s1 + s2)
    if (v < 0.10) out.push(px(x, y, "q"))
    else if (v < 0.19 && ((x + y) & 1) === 0) out.push(px(x, y, "c"))   // soft, dithered edge
  }

sprite("caustics", { w: N, h: N }, ...out)
