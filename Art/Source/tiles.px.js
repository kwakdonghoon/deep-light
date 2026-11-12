// Abyss Keeper tiles and props, 16x16 each. Build: node <pxl>/bin/pxl.js build tiles.px.js --out out_tiles
// Tiles come in 4 variants (the game picks one per cell). Edge highlights/shadows are added by the game at runtime.
// Marks on a surface have a cause: chisel chips on rock, strata layers on hard rock, cooled veins in deep rock,
// sand ripples on the seabed, ore pieces embedded in stone.
pal({
  k: "#0a1626",
  // seabed
  s: "#16365a", S: "#1b4268", t: "#112a47", u: "#2f6a8c",
  // rock / hard rock / deep rock / bedrock: base, light, dark
  a: "#3d527c", b: "#4b6590", c: "#2f4062",
  h: "#54447d", i: "#665293", j: "#42356a",
  d: "#6e3b6c", e: "#834c80", f: "#552d53",
  x: "#0d1220", y: "#121828", z: "#0a0e19",
  // ores
  o: "#e0904a", O: "#ffd3a0", n: "#a85a24",
  m: "#f2c14e", M: "#fff0a8", q: "#a87a1c",
  g: "#4ff0e0", G: "#d8fffa", Q: "#23a6a8",
  // props
  v: "#2f8f6b", V: "#4fc79a", W: "#fff4c0", Y: "#ffd86b", I: "#5b6b80", r: "#8ea3b8",
  p: "#d9c9e8", P: "#9b88b8", w: "#ffffff", B: "#4a2a10", L: "#7a4a22", C: "#f2c14e", D: "#fff0a8",
  E: "#d9c9a8", F: "#fff4d6", H: "#a8946e", J: "#7fe3ff",
  X: "#000000b8"                                            // crack lines (translucent)
})

function rng(seed) {                       // small deterministic PRNG so variants are stable between builds
  let a = seed >>> 0
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }
}
const ri = (r, lo, hi) => lo + Math.floor(r() * (hi - lo + 1))

// ---------- seabed: sand ripples + a few pebbles ----------
function floorItems(v) {
  const r = rng(11 + v * 7), it = [rect(0, 0, 16, 16, "s")]
  for (let k = 0; k < 3; k++) {
    const y = 2 + k * 5 + (v % 2), x = ri(r, 0, 5), len = ri(r, 4, 7)
    it.push(hline(x, y, len, "S"))                         // ripple crest
    it.push(hline(x + 1, y + 1, Math.max(2, len - 2), "t"))// its shadow side
    if (r() < 0.6) it.push(px(x + ri(r, 0, len - 1), y, "u"))// glint on the crest
  }
  for (let k = 0; k < 2; k++) {                            // pebbles
    const x = ri(r, 1, 13), y = ri(r, 1, 13)
    it.push(rect(x, y, 2, 1, "S"), px(x, y, "u"), px(x + 1, y + 1, "t"))
  }
  return it
}

// ---------- chiselled rock: chips with a lit top edge and a shaded bottom edge ----------
function rockItems(v, [A, B, Cc], seed) {
  const r = rng(seed + v * 13), it = [rect(0, 0, 16, 16, A)]
  const n = 5
  for (let k = 0; k < n; k++) {
    const w = ri(r, 3, 5), x = ri(r, 1, 15 - w - 1), y = ri(r, 1, 11)
    it.push(hline(x, y, w, B), hline(x + 1, y + 2, w, Cc))
  }
  const cx = ri(r, 3, 10), cy = ri(r, 2, 9)                 // one hairline crack
  it.push(line(cx, cy, cx + 2, cy + 3, Cc), px(cx + 2, cy + 4, Cc))
  return it
}

// ---------- strata: wavy sediment layers ----------
function strataItems(v, [A, B, Cc], seed) {
  const r = rng(seed + v * 17), it = [rect(0, 0, 16, 16, A)]
  for (let y = 2 + (v % 3); y < 16; y += 4) {
    const dy = ri(r, -1, 1)
    it.push(line(0, y, 15, y + dy, B), line(0, y + 1, 15, y + 1 + dy, Cc))
  }
  for (let k = 0; k < 3; k++) it.push(px(ri(r, 1, 14), ri(r, 1, 14), B))   // grains caught in a layer
  return it
}

// ---------- deep rock: cooled veins ----------
function deepItems(v, [A, B, Cc], seed) {
  const r = rng(seed + v * 19), it = [rect(0, 0, 16, 16, A)]
  let x = ri(r, 2, 6), y = 0
  while (y < 15) {                                          // a vein running top to bottom, with a short branch
    const nx = Math.max(1, Math.min(14, x + ri(r, -1, 1))), ny = y + ri(r, 2, 3)
    it.push(line(x, y, nx, ny, Cc), px(nx + 1, ny, B))
    if (r() < 0.35) it.push(line(nx, ny, nx + ri(r, 2, 3), ny + 1, B))
    x = nx; y = ny
  }
  for (let k = 0; k < 4; k++) it.push(px(ri(r, 1, 14), ri(r, 1, 14), Cc))
  return it
}

function variants(name, fn) { for (let v = 0; v < 4; v++) sprite(`tile_${name}_${v}`, { w: 16, h: 16 }, ...fn(v)) }

variants("floor", floorItems)
variants("rock", v => rockItems(v, ["a", "b", "c"], 100))
variants("hard", v => strataItems(v, ["h", "i", "j"], 200))
variants("deep", v => deepItems(v, ["d", "e", "f"], 300))
variants("bedrock", v => strataItems(v, ["x", "y", "z"], 400))

// ---------- ore: pieces embedded in the stone ----------
function scatterPoints(r, n, minDist) {
  const pts = []
  let guard = 0
  while (pts.length < n && guard++ < 200) {
    const x = ri(r, 2, 11), y = ri(r, 2, 11)
    if (pts.every(([px_, py_]) => Math.abs(px_ - x) + Math.abs(py_ - y) >= minDist)) pts.push([x, y])
  }
  return pts
}

variants("scrap", v => {                                    // torn copper plates
  const r = rng(500 + v * 23), it = rockItems(v, ["a", "b", "c"], 100)
  for (const [x, y] of scatterPoints(r, 3, 6)) it.push(rect(x, y, 3, 2, "o"), px(x, y, "O"), px(x + 1, y, "O"), hline(x, y + 2, 3, "n"), px(x + 3, y + 1, "n"))
  return it
})
variants("brass", v => {                                    // round nuggets
  const r = rng(600 + v * 29), it = strataItems(v, ["h", "i", "j"], 200)
  for (const [x, y] of scatterPoints(r, 3, 6)) it.push(circ(x + 1, y + 1, 1, "m"), px(x, y, "M"), px(x + 2, y + 2, "q"), px(x + 1, y + 2, "q"))
  return it
})
variants("crystal", v => {                                  // pointed shards, lit on the left face
  const r = rng(700 + v * 31), it = deepItems(v, ["d", "e", "f"], 300)
  for (const [x, y] of scatterPoints(r, 2, 7)) {
    const h = ri(r, 5, 7)
    it.push(poly([x, y + h, x + 2, y, x + 4, y + h], "g"), line(x + 1, y + h - 1, x + 2, y + 1, "G"), px(x + 3, y + h - 1, "Q"), hline(x, y + h, 4, "Q"))
  }
  return it
})

// ---------- seaweed (3 variants), shell, clam, chest, lamp, vent ----------
// Each kelp variant has two frames (A, B) with the sway phase flipped, so the game can animate the strands.
for (const frame of [0, 1]) for (let v = 0; v < 3; v++) {   // kelp: continuous, swaying, thick at the root and thin at the tip, with leaf pairs
  const r = rng(800 + v * 5), it = []
  for (let s = 0; s < 3; s++) {
    const bx = 3 + s * 5, h = 7 + ((v + s) % 3) * 2, ph = r() * 6.28 + frame * 3.14
    let prev = bx
    for (let k = 0; k <= h; k++) {
      const y = 15 - k, x = bx + Math.round(Math.sin(k * 0.6 + ph) * Math.min(1.6, k * 0.3))
      if (k > 0 && Math.abs(x - prev) > 0) it.push(px(Math.min(x, prev), y, "v"))     // keep the stem connected when it shifts sideways
      it.push(px(x, y, "v"))
      if (k < h * 0.45) it.push(px(x + 1, y, "v"))                                   // thick base
      if (k > 1 && k % 3 === 0) it.push(px(x + (k % 2 ? 1 : -1) * (k < h * 0.45 ? 2 : 1), y - 1, "V"))   // leaves alternate sides
      prev = x
    }
    it.push(px(prev, 15 - h - 1, "V"))
  }
  sprite(frame === 0 ? `deco_seaweed_${v}` : `deco_seaweed_${v}_b`, { w: 16, h: 16 }, ...it)
}

// ---------- mining cracks (translucent black) in 3 stages, growing from the center ----------
for (let st = 1; st <= 3; st++) {
  const r = rng(900 + st * 3), it = []
  const arms = st === 1 ? 2 : st === 2 ? 4 : 6
  for (let a = 0; a < arms; a++) {
    const ang = a * Math.PI * 2 / arms + r() * 0.8
    let x = 8, y = 8
    const len = st === 1 ? 4 : st === 2 ? 6 : 7
    for (let k = 0; k < len; k++) {
      const nx = Math.round(8 + Math.cos(ang + (r() - 0.5) * 0.7) * (k + 1)), ny = Math.round(8 + Math.sin(ang + (r() - 0.5) * 0.7) * (k + 1))
      it.push(line(x, y, nx, ny, "X")); x = nx; y = ny
    }
  }
  if (st === 3) for (let k = 0; k < 6; k++) it.push(px(ri(r, 2, 13), ri(r, 2, 13), "X"))   // chips breaking loose
  sprite(`crack_${st}`, { w: 16, h: 16 }, ...it)
}

sprite("_shell", { outline: "k" }, `
  .EEE.
  EFEEH
  EEHEH
  .HHH.`)
sprite("deco_shell", { w: 16, h: 16 }, stamp("_shell", 5, 10))

sprite("_clam", { outline: "k" }, `
  ..PPPP..
  .PppppP.
  PppppppP
  PPPwwPPP
  .PPPPPP.`)
sprite("deco_clam", { w: 16, h: 16 }, stamp("_clam", 3, 9))

sprite("_chest", { outline: "k" }, `
  .BBBBBBBB.
  BLrLLLLrLB
  BLrLLLLrLB
  rrrrCCrrrr
  BLrLCDLrLB
  BLrLLLLrLB
  BLrLLLLrLB
  .BBBBBBBB.`)
sprite("deco_chest", { w: 16, h: 16 }, stamp("_chest", 2, 5))

// ---------- zone props: broken pillar (shallow ruins), sunken sign (drowned street), rib arch + glowing fungus (abyss) ----------
sprite("_pillar", { outline: "k" }, `
  ..rrrr..
  .rrIIIr.
  .rIrIIr.
  ..rIIIr.
  ..rIIIr.
  ..rIIIr.
  .rrIIIrr
  rIIIIIII`)
sprite("deco_pillar", { w: 16, h: 16 }, stamp("_pillar", 3, 5))

sprite("_sign", { outline: "k" }, `
  .IIIIII.
  .IYYYYI.
  .IYkkYI.
  .IYYYYI.
  .IIIIII.
  ...II...
  ...II...
  ...II...
  ...II...
  ..IIII..`)
sprite("deco_sign", { w: 16, h: 16 }, stamp("_sign", 3, 3))

sprite("_bones", { outline: "k" }, `
  ..EEEEEE..
  .EH....HE.
  EH......HE
  E........E
  E........E
  H........H
  HE......EH
  EEE....EEE`)
sprite("deco_bones", { w: 16, h: 16 }, stamp("_bones", 2, 6))

sprite("_fungus", { outline: "k" }, `
  ...ggg....
  ..gGGGg.gg
  ..gGGGgGGg
  ...QQ..QQ.
  ...Q...Q..
  ...Q...QQ.`)
sprite("deco_fungus", { w: 16, h: 16 }, stamp("_fungus", 2, 8))
sprite("_fungus_b", { base: "_fungus", swap: { g: "G", G: "g" } })                 // the caps pulse between light and dark
sprite("deco_fungus_b", { w: 16, h: 16 }, stamp("_fungus_b", 2, 8))

sprite("_lamp", { outline: "k" }, `
  .IIII.
  IYWWYI
  IYWWYI
  IYYYYI
  .IIII.
  ..II..
  ..II..
  ..II..
  ..II..
  .IIII.`)
sprite("deco_lamp", { w: 16, h: 16 }, stamp("_lamp", 4, 2))
sprite("_lamp_b", { base: "_lamp", swap: { Y: "W", W: "Y" } })                      // the flame flickers
sprite("deco_lamp_b", { w: 16, h: 16 }, stamp("_lamp_b", 4, 2))

sprite("_vent", { outline: "k" }, `
  rIIIIIIIIr
  ICcCcCcCcI
  IIIIIIIIII
  .II....II.`)
sprite("deco_vent", { w: 16, h: 16 }, stamp("_vent", 2, 9), px(5, 7, "J"), px(8, 4, "J"), px(11, 1, "J"), px(6, 5, "c"), px(10, 3, "c"))
sprite("deco_vent_b", { w: 16, h: 16 }, stamp("_vent", 2, 9), px(6, 6, "J"), px(9, 2, "J"), px(10, 5, "J"), px(5, 4, "c"), px(11, 7, "c"))   // the bubbles have risen
