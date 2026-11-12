// Abyss Keeper enemies. Build: node <pxl>/bin/pxl.js build enemies.px.js --out out_enemies
// Final sizes (art + 1px outline) match the old code-drawn sprites so hitboxes still read right:
// jelly 12x9 (2 frames), angler 14x8, urchin 12x12, crab 12x8, eel 16x6. Angler and eel face left; the game flips them.
pal({
  k: "#0a1626",
  b: "#d98cff", B: "#9b5de0", L: "#f3d2ff", q: "#2a1450",        // jelly
  d: "#2d3f6b", D: "#1b2748", w: "#ffffff", e: "#ff4d4d",        // angler
  s: "#a34fd6", u: "#6b2d8f", v: "#c98bff",                      // urchin
  c: "#e05a3a", C: "#a53a22", o: "#ffb08a",                      // crab
  y: "#ffe14d", Y: "#b8a000", z: "#fff6a8",                      // eel
  x: "#c8ecff", X: "#7fb6d8"                                     // wraith
})

// ---- jelly: bell + two tentacle poses ----
sprite("jelly_0", { mx: true, outline: "k" }, `
  ..bbb
  .bLbb
  bbqbb
  BBBBB
  .B.B.
  .B.B.
  B..B.`)
sprite("jelly_1", { mx: true, outline: "k" }, `
  ..bbb
  .bLbb
  bbqbb
  BBBBB
  B.B..
  .B.B.
  ..B..`)

// ---- angler (faces left, teeth at the mouth) ----
sprite("angler", { outline: "k" }, `
  ...dddddd...
  ..dwedddddDD
  dddddddddDD.
  wdwdwdddDDD.
  DDDDDDDDDD..
  .DD..DD.....`)

// ---- urchin: quarter drawn, mirrored both ways ----
sprite("urchin", { mx: true, my: true, outline: "k" }, `
  s...s
  .s..s
  ..uuu
  ..uuv
  sssuu`)

// ---- crab ----
sprite("crab", { mx: true, outline: "k" }, `
  cc...
  cCc..
  .cccc
  .cwcc
  .CCCC
  c.c.c`)

// ---- eel (faces left, red eye) ----
sprite("eel", { outline: "k" }, `
  ..yyyyyyyyyy.y
  YeYYYYYYYYYYYy
  YYYYYYYYYYYyy.
  .YYYYyyyyy....`)

// ---- second animation frames (the game alternates frame A / B, so every enemy keeps moving) ----
sprite("angler_b", { outline: "k" }, `
  ...dddddd...
  ..dwedddddDD
  dddddddddDD.
  dwdwdwddDDD.
  DDDDDDDDDDD.
  ..DD..DD....`)
sprite("urchin_b", { mx: true, my: true, outline: "k" }, `
  ....s
  .s..s
  ..uuu
  ..uuv
  .ssuu`)
sprite("crab_b", { mx: true, outline: "k" }, `
  c....
  ccc..
  .cccc
  .cwcc
  .CCCC
  .c.c.`)
sprite("eel_b", { outline: "k" }, `
  ..yyyyyyyyyy..
  YeYYYYYYYYYYyy
  YYYYYYYYYYYYYy
  .YYYYyyyyy.y..`)
sprite("wraith_b", { mx: true, outline: "k" }, `
  ..xxx
  .xxxx
  xxkxx
  xxkxx
  xxxxx
  XxXxX
  .X.X.`)

// ---- mini bosses (zone lairs). Bigger than normal enemies, crowned. Final size art + 1px outline. ----
// Rusted Claw King: a hulking crab with huge claws and a gold crown (art 26x14 -> 28x16)
sprite("crab_king", { mx: true, outline: "k" }, `
  .........y..y
  .........yyyy
  ccc.....ccccc
  cCCc..ccccooc
  cCCcccccccccc
  .cCccccwkcccc
  ..cccccccccCC
  ..ccccccCCCCC
  ...CCCCCCCCCC
  ..c.c.c.c.c.c
  .c..c..c..c.c
  c...c.....c..`)

// Lightning Eel King: long gold-striped eel with a crown and a fan tail (30x11 -> 32x13). Faces left like the small eel.
// The tail swishes between two poses (frame A / frame B).
function eelKing(name, tail) {
  sprite(name, { w: 30, h: 11, outline: "k" },
    ellipse(15, 6, 14, 4, "Y"),
    ellipse(15, 5, 12, 2, "y"),
    rect(0, 5, 5, 3, "Y"),
    px(2, 5, "e"),
    rect(2, 2, 5, 1, "y"), px(2, 1, "y"), px(4, 1, "y"), px(6, 1, "y"), px(4, 0, "z"),
    poly([26, 3 + tail, 29, 0 + tail, 29, 10 + tail, 26, 9 + tail], "Y"),
    line(8, 7, 24, 7, "Y"))
}
eelKing("eel_king", 0)
eelKing("eel_king_b", 1)

sprite("crab_king_b", { mx: true, outline: "k" }, `
  .........y..y
  .........yyyy
  .cc.....ccccc
  cCCc..ccccooc
  cCCcccccccccc
  .cCccccwkcccc
  ..cccccccccCC
  ..ccccccCCCCC
  ...CCCCCCCCCC
  .c.c.c.c.c.c.
  c..c..c..c.c.
  .c..c.....c..`)

// ---- wraith (abyss only; drifts through walls, drawn semi-transparent in game) ----
sprite("wraith", { mx: true, outline: "k" }, `
  ..xxx
  .xxxx
  xxkxx
  xxkxx
  xxxxx
  xXxXx
  X.X.X`)
