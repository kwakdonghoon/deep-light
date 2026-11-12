// DEEP LIGHT main title key art, 428x180 (the game's widest logical screen). Build: node <pxl>/bin/pxl.js build title.px.js --out out_title
// Layers: sky gradient + light shafts, far city, near city, beacon (glowing), seabed + kelp, diver, bubbles, logo.
pal({
  k: "#0a1626",
  // sky / deep water (dark -> lighter toward the horizon)
  0: "#04091a", 1: "#071530", 2: "#0a2342", 3: "#0d3157", 4: "#114468", 5: "#17608a",
  // far city / windows
  6: "#0b2745", 7: "#0f3358", 8: "#ffd86b", 9: "#b8742f",
  // near city
  n: "#061629", N: "#0a1f38",
  // seabed, kelp
  u: "#0f3a56", U: "#16506f", z: "#0a2a40", v: "#1f6f55", V: "#2f8f6b",
  // bubbles, beacon
  b: "#bfeaff", c: "#7fd3f5", e: "#4fc3f7", q: "#9fe8ff", W: "#ffffff", i: "#5b6b80", I: "#3b4a63",
  // logo
  L: "#e8fbff", l: "#9fe8ff", o: "#4fc3f7", d: "#1f7fb0",
  // diver (same colors as diver.px.js)
  h: "#e8b23c", H: "#a87a1c", g: "#8fe9ff", G: "#3aa0c4", s: "#3a74b8", S: "#25508a",
  t: "#cfd9e2", T: "#8ea3b8", f: "#e4572e", F: "#a8391c", a: "#dbe6ee", w: "#b9803a", p: "#f1c8a0",
  // the eye
  R: "#5a1824", r: "#c0392b", P: "#1a0a0a"
})

function rng(seed) {
  let a = seed >>> 0
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }
}
const W = 428, H = 180, HORIZON = 150

// ---- diver facing right (copy of the in-game side idle frame) ----
sprite("_diver_top", `
  ...hhhhh...
  ..hhhhhhh..
  ..hhhhHHHh.
  ..hHhhHgGH.
  ..hHhhHHHh.
  ...hHHHHh..
  .ttssssss..
  ttTssSsssSp
  .tTHhHhHH..`)
sprite("_diver_legs", `
  ...sssss...
  ...ss.ss...
  ..ss...ss..
  .fff...ffff
  ffF.....Fff`)
sprite("_diver_up", { w: 11, h: 14, outline: "k" }, stamp("_diver_top", 0, 0), stamp("_diver_legs", 0, 9))
sprite("_diver", { base: "_diver_up", rot: 90 })   // swimming: head to the right, fins trailing in a flutter kick

// ---- procedural pieces ----
function lightShafts() {
  const out = []
  for (const [x, w, drift] of [[40, 26, 30], [120, 18, 22], [250, 30, -26], [330, 20, -18], [390, 24, -30]])
    out.push(poly([x, 0, x + w, 0, x + w + drift, 118, x + drift, 118], "2"))
  return out
}

// mode "all" = buildings + windows (the far layers), "b" = buildings only, "w" = windows only.
// For "w", only every other lit window (parity 0/1) is emitted so the two halves can twinkle out of phase in the game.
// The random calls are identical in every mode, so the skyline is the same in all of them.
function city(seed, color, minH, maxH, minW, maxW, from, to, litChance, skip, mode = "all", parity = 0) {
  const r = rng(seed), out = []
  let x = from, lit = 0
  while (x < to) {
    const w = minW + Math.floor(r() * (maxW - minW + 1)), h = minH + Math.floor(r() * (maxH - minH + 1))
    if (!(skip && x + w > skip[0] && x < skip[1])) {
      const bld = mode !== "w", win = mode !== "b"
      if (bld) out.push(rect(x, HORIZON - h, w, h + 2, color))
      if (r() < 0.35) { if (bld) out.push(rect(x + 2, HORIZON - h - 3, w - 4, 3, color)) }                   // roof step
      if (r() < 0.3) { if (bld) out.push(px(x + Math.floor(w / 2), HORIZON - h - 6, color), px(x + Math.floor(w / 2), HORIZON - h - 5, color)) }   // antenna
      for (let wy = HORIZON - h + 4; wy < HORIZON - 4; wy += 6)                                 // windows: mostly dark, a few still lit
        for (let wx = x + 2; wx < x + w - 2; wx += 4)
          if (r() < litChance) {
            const col = r() < 0.7 ? "8" : "9"
            if (mode === "all" || (mode === "w" && (lit % 2) === parity)) out.push(rect(wx, wy, 2, 3, col))
            lit++
          }
    }
    x += w + Math.floor(r() * 3)
  }
  return out
}

function kelp(x, h, seed) {
  const r = rng(seed), out = []
  let cx = x
  for (let k = 0; k < h; k++) {
    cx += Math.round(Math.sin(k * 0.55 + seed) * 0.8)
    out.push(px(cx, HORIZON + 12 - k, "v"), px(cx + 1, HORIZON + 12 - k, k % 3 === 0 ? "V" : "v"))
    if (k > 3 && k % 4 === 0) out.push(px(cx + (k % 8 === 0 ? 2 : -1), HORIZON + 11 - k, "V"))
  }
  return out
}

function bubbles(seed, n, x0, x1, y0, y1) {
  const r = rng(seed), out = []
  for (let i = 0; i < n; i++) {
    const x = x0 + Math.floor(r() * (x1 - x0)), y = y0 + Math.floor(r() * (y1 - y0)), big = r() < 0.25
    out.push(big ? rect(x, y, 2, 2, r() < 0.5 ? "b" : "c") : px(x, y, r() < 0.5 ? "b" : "c"))
  }
  return out
}

// ---- layers (each 428x180, transparent) so the game can animate them separately: twinkling windows, swaying kelp, a blinking eye, a flickering beacon, a shining logo ----
// No diver anywhere in the title art: the game draws its own characters swimming across.
const sky = [grad(0, 0, W, HORIZON + 4, "01234", "v", 0.7)]
const shafts = lightShafts()
const cityBack = [
  ...city(11, "6", 22, 62, 12, 26, -4, W, 0.2, null),
  ...city(23, "7", 12, 34, 14, 28, -4, W, 0.1, [176, 252]),
]
// near city: buildings are baked into the bg, the lit windows are their own two layers (twinkle out of phase)
const NEAR = 0.16
const cityNear = [
  ...city(37, "n", 30, 78, 16, 34, -6, 168, NEAR, null, "b"),
  ...city(53, "n", 30, 78, 16, 34, 258, W + 6, NEAR, null, "b"),
]
const winA = [...city(37, "n", 30, 78, 16, 34, -6, 168, NEAR, null, "w", 0), ...city(53, "n", 30, 78, 16, 34, 258, W + 6, NEAR, null, "w", 0)]
const winB = [...city(37, "n", 30, 78, 16, 34, -6, 168, NEAR, null, "w", 1), ...city(53, "n", 30, 78, 16, 34, 258, W + 6, NEAR, null, "w", 1)]
const landmarks = [
  rect(70, 70, 10, 80, "N"), rect(68, 66, 14, 6, "N"), circ(75, 76, 4, "6"), px(75, 76, "8"), line(75, 76, 75, 73, "N"), px(75, 62, "N"), px(75, 61, "N"),
  line(356, HORIZON, 356, 58, "N", 2), line(356, 60, 392, 70, "N", 2), line(392, 70, 392, 84, "N")
]
const seabed = [
  rect(0, HORIZON, W, H - HORIZON, "u"),
  ...Array.from({ length: 9 }, (_, i) => hline(10 + i * 49, HORIZON + 5 + (i % 3) * 7, 22 + (i * 7) % 12, "U")),
  ...Array.from({ length: 9 }, (_, i) => hline(34 + i * 47, HORIZON + 6 + (i % 3) * 7, 14 + (i * 5) % 10, "z")),
  rect(0, HORIZON + 24, W, 6, "z"),
  rect(236, HORIZON + 3, 4, 3, "e"), px(237, HORIZON + 3, "W"), rect(246, HORIZON + 6, 3, 2, "8"), rect(180, HORIZON + 5, 3, 2, "e"),
  rect(262, HORIZON + 2, 9, 6, "w"), rect(262, HORIZON + 2, 9, 2, "H"), rect(265, HORIZON + 4, 3, 2, "8")
]

sprite("title_bg", { w: W, h: H }, ...sky, ...shafts, ...cityBack, ...cityNear, ...landmarks, ...seabed)
sprite("title_win_a", { w: W, h: H }, ...winA)
sprite("title_win_b", { w: W, h: H }, ...winB)
sprite("title_kelp_l", { w: W, h: H }, ...kelp(18, 18, 1), ...kelp(30, 12, 4), ...kelp(150, 8, 3))
sprite("title_kelp_r", { w: W, h: H }, ...kelp(280, 9, 6), ...kelp(396, 20, 2), ...kelp(410, 13, 5))
// the Eye of the Abyss, a faint red stare in the dark above the far city (right side, clear of the logo)
sprite("title_eye", { w: W, h: H }, circ(392, 50, 11, "6"), circ(392, 50, 8, "R"), circ(392, 50, 5, "r"), circ(393, 50, 2, "P"), px(390, 47, "R"))
// beacon tower + lantern (its glow is a separate light in the game)
sprite("title_beacon", { w: W, h: H },
  rect(209, 118, 10, 32, "I"), rect(210, 118, 8, 32, "i"), rect(204, 146, 20, 6, "I"), rect(206, 144, 16, 3, "i"),
  rect(207, 104, 14, 14, "e"), rect(209, 106, 10, 10, "q"), rect(211, 108, 6, 6, "W"),
  rect(205, 101, 18, 3, "I"), rect(211, 97, 6, 4, "I"))
// logo: shadow, body, bevel
sprite("title_logo", { w: W, h: H },
  text(215, 25, "DEEP LIGHT", "k", { scale: 6, gap: 1, align: "c" }),
  text(213, 21, "DEEP LIGHT", "l", { scale: 6, gap: 1, align: "c" }),
  recolor({ l: "o" }, [0, 30, W, 6]),
  recolor({ l: "d" }, [0, 36, W, 6]),
  recolor({ l: "L" }, [0, 21, W, 4]),
  glow("L", { r: 4, strength: 0.4, color: "#9fe8ff" }),
  text(214, 56, "THE LAST BEACON UNDER THE SEA", "q", { scale: 1, gap: 1, align: "c" }))
