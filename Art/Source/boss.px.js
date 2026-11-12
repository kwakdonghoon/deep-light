// Abyss Keeper boss: the Abyss Warden, a giant eye in a barnacled shell. Build: node <pxl>/bin/pxl.js build boss.px.js --out out_boss
// Pieces (the game layers them): body 32x32, body (phase 2, cracked and angry) 32x32, white flash silhouette, iris 13x13 (+ phase 2 iris).
// Tentacles are drawn in code every frame (they sway), so they are not here.
pal({
  k: "#0a1626",
  T: "#34445c", S: "#566a86", B: "#8aa0b8", b: "#c5d5e5",   // shell dark / mid, barnacle, barnacle light
  E: "#efe3f5", F: "#bfa9d6", e: "#f6d6dc",                 // sclera, sclera shade, sclera (angry)
  v: "#d9606a", V: "#ff6a6a", g: "#ffb347",                 // veins, angry veins, glowing cracks
  i: "#8a2a20", I: "#c0392b", n: "#b8331f", N: "#ff6a3d",   // iris rings (calm / angry)
  p: "#1a0a0a", w: "#ffffff"
})

const C = 15                    // center of the 30x30 art (outline brings it to 32x32)
const pt = (a, r) => [Math.round(C + Math.cos(a) * r), Math.round(C + Math.sin(a) * r)]

function bodyItems(p2) {
  const items = []
  items.push(circ(C, C, 14, "T"))                       // shell, dark rim
  items.push(circ(C - 1, C - 1, 13, "S"))               // lit from the upper left
  for (let i = 0; i < 8; i++) {                          // shell plate seams, radiating from the eye socket
    const a = i * Math.PI / 4 + Math.PI / 8
    const [x0, y0] = pt(a, 11), [x1, y1] = pt(a, 14)
    items.push(line(x0, y0, x1, y1, "T"))
  }
  for (let i = 0; i < 10; i++) {                         // barnacles on the rim: a few big, the rest small
    const [x, y] = pt(i * Math.PI * 2 / 10 + 0.3, 12.5)
    const big = i % 3 === 0
    items.push(circ(x, y, big ? 2 : 1, "B")); items.push(px(x - 1, y - 1, "b"))
    if (big) items.push(px(x, y, "T"))                   // the little hole at the top of a big one
  }
  if (p2) for (let i = 0; i < 6; i++) {                  // phase 2: glowing cracks across the shell
    const a = i * Math.PI / 3 + 0.5
    const [x0, y0] = pt(a, 11), [x1, y1] = pt(a + 0.1, 14)
    items.push(line(x0, y0, x1, y1, "g"))
  }
  items.push(circ(C, C, 11, p2 ? "V" : "F"))            // eye socket rim / shade
  items.push(circ(C - 1, C - 1, 10, p2 ? "e" : "E"))    // sclera
  for (let i = 0; i < 7; i++) {                          // veins from the rim toward the iris
    const a = i * Math.PI * 2 / 7 + 0.2
    const [x0, y0] = pt(a, 10), [x1, y1] = pt(a + 0.15, p2 ? 5.5 : 7)
    items.push(line(x0, y0, x1, y1, p2 ? "V" : "v"))
  }
  return items
}

sprite("boss_body",  { w: 30, h: 30, outline: "k" }, ...bodyItems(false))
sprite("boss_body2", { w: 30, h: 30, outline: "k" }, ...bodyItems(true))
sprite("boss_flash", { base: "boss_body", swap: { k: "w", T: "w", S: "w", B: "w", b: "w", E: "w", F: "w", v: "w" } })

// eyelid for blinking: half-closed and fully closed (32x32, same center as the body). Drawn above the iris.
sprite("boss_lid_a", { w: 32, h: 32 }, clip([0, 0, 32, 12], circ(15, 15, 10, "S")), hline(6, 11, 19, "k"))
sprite("boss_lid_b", { w: 32, h: 32 }, circ(15, 15, 10, "S"), hline(5, 15, 21, "k"), hline(8, 14, 15, "T"))

sprite("boss_iris",  { w: 13, h: 13 }, circ(6, 6, 6, "i"), circ(6, 6, 5, "I"), circ(6, 6, 3, "p"), rect(3, 3, 2, 2, "w"))
sprite("boss_iris2", { w: 13, h: 13 }, circ(6, 6, 6, "n"), circ(6, 6, 5, "N"), circ(6, 6, 2, "p"), rect(3, 3, 2, 2, "w"))
