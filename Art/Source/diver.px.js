// DEEP LIGHT diver, 16x16 cells. Build: node <pxl>/bin/pxl.js build diver.px.js --out out_diver
// Sheet: 10 columns x 3 rows. Columns: idle0, idle1, swim0..swim5, attack0 (wind-up), attack1 (thrust).
// Rows: down (front), up (back), side (a swimming pose, faces right; the game flips it for left).
pal({
  k: "#0a1626",
  h: "#e8b23c", H: "#a87a1c",          // brass helmet
  g: "#8fe9ff", G: "#3aa0c4",          // visor glass
  s: "#3a74b8", S: "#25508a",          // wetsuit
  t: "#cfd9e2", T: "#8ea3b8",          // air tank
  f: "#e4572e", F: "#a8391c",          // fins
  a: "#dbe6ee", w: "#b9803a",          // harpoon tip / shaft
  p: "#f1c8a0"                         // hands
})

// ---------- upper bodies ----------
sprite("_front_top", { mx: "c" }, `
  ..hhhh
  .hhhhh
  hhhHHH
  hhHggg
  hhHgGG
  hhhHHH
  .hhhhh
  ssSsss
  ssSsss
  pSSsss
  .HhHhH`)

sprite("_back_top", { mx: "c" }, `
  ..hhhh
  .hhhhh
  hhhhhh
  hhhhhh
  hhhhhH
  hhHHHH
  .hhhhh
  ssttTT
  ssttTT
  psttTT
  .HhHhH`)

// side body, built upright (9 rows) and rotated 90 degrees clockwise below: head to the right, tank on the back (top), belly down
sprite("_side_top", `
  ...hhhhh...
  ..hhhhhhh..
  ..hhhhHHHh.
  ..hHhhHgGH.
  ..hHhhHHHh.
  ...hHHHHh..
  .ttssssss..
  ttTssSsssSp
  .tTHhHhHH..`)

// ---------- front/back legs: 3 rows x 11. L/R = 0 fin extended, 1 fin tucked ----------
function legsFB(name, L, R) {
  const it = [rect(2, 0, 7, 1, "s"), px(5, 0, "S")]
  if (L === 0) it.push(rect(2, 1, 3, 1, "s"), rect(1, 2, 3, 1, "f"), px(4, 2, "F"))
  else it.push(rect(2, 1, 3, 1, "s"), rect(2, 2, 2, 1, "f"))
  if (R === 0) it.push(rect(6, 1, 3, 1, "s"), rect(7, 2, 3, 1, "f"), px(6, 2, "F"))
  else it.push(rect(6, 1, 3, 1, "s"), rect(7, 2, 2, 1, "f"))
  sprite(name, { w: 11, h: 3 }, ...it)
}
legsFB("_fb_ext_ext", 0, 0); legsFB("_fb_ext_tuck", 0, 1); legsFB("_fb_tuck_ext", 1, 0); legsFB("_fb_tuck_tuck", 1, 1)

// ---------- side legs: 5 rows x 11, a flutter kick. phase 0..2pi, amp = how far the legs swing ----------
function legsSide(name, phase, amp) {
  const it = [rect(3, 0, 5, 1, "s")]
  const clamp = (x, w) => Math.max(0, Math.min(11 - w, x))
  for (let r = 1; r <= 4; r++) {
    const dl = Math.round(Math.sin(phase) * amp * r), dr = Math.round(Math.sin(phase + Math.PI) * amp * r)
    if (r <= 2) { it.push(rect(clamp(3 + dl, 2), r, 2, 1, "s"), rect(clamp(6 + dr, 2), r, 2, 1, "s")) }
    else {
      const wL = r === 3 ? 3 : 4, wR = r === 3 ? 3 : 4
      it.push(rect(clamp(2 + dl, wL), r, wL, 1, "f"), rect(clamp(6 + dr, wR), r, wR, 1, "f"))
      if (r === 4) it.push(px(clamp(2 + dl, wL) + wL - 1, r, "F"), px(clamp(6 + dr, wR), r, "F"))
    }
  }
  sprite(name, { w: 11, h: 5 }, ...it)
}
for (let i = 0; i < 6; i++) legsSide("_side_swim_" + i, i * Math.PI / 3, 0.7)
legsSide("_side_idle_0", Math.PI / 2, 0.28)
legsSide("_side_idle_1", Math.PI * 1.5, 0.28)

// ---------- frame composition (14x14 art, +1px outline = 16x16) ----------
const O = { w: 14, h: 14, outline: "k" }
const FB_SWIM = ["_fb_ext_tuck", "_fb_ext_ext", "_fb_tuck_ext", "_fb_ext_ext", "_fb_ext_tuck", "_fb_tuck_ext"]   // 6-step flutter
const FB_IDLE = ["_fb_ext_ext", "_fb_tuck_tuck"]                                                               // slow breathing

for (const [dir, top] of [["down", "_front_top"], ["up", "_back_top"]]) {
  for (let i = 0; i < 2; i++) sprite(`diver_${dir}_i${i}`, O, stamp(top, 1, 0), stamp(FB_IDLE[i], 1, 11))
  for (let i = 0; i < 6; i++) sprite(`diver_${dir}_s${i}`, O, stamp(top, 1, 0), stamp(FB_SWIM[i], 1, 11))
}
// attacks: harpoon beside the body, short in the wind-up and fully thrust in the second frame
sprite("diver_down_a0", O, stamp("_front_top", 1, 0), stamp("_fb_ext_ext", 1, 11), rect(12, 7, 1, 3, "w"), px(12, 10, "a"), px(11, 8, "s"))
sprite("diver_down_a1", O, stamp("_front_top", 1, 0), stamp("_fb_ext_ext", 1, 11), rect(12, 7, 1, 5, "w"), px(12, 12, "a"), px(12, 13, "a"), px(11, 8, "s"))
sprite("diver_up_a0", O, stamp("_back_top", 1, 0), stamp("_fb_ext_ext", 1, 11), rect(12, 5, 1, 3, "w"), px(12, 4, "a"), px(11, 8, "s"))
sprite("diver_up_a1", O, stamp("_back_top", 1, 0), stamp("_fb_ext_ext", 1, 11), rect(12, 2, 1, 6, "w"), px(12, 1, "a"), px(12, 0, "a"), px(11, 8, "s"))

// side: upright composition rotated into a horizontal swimmer
for (let i = 0; i < 2; i++) {
  sprite(`_up_side_i${i}`, O, stamp("_side_top", 1, 0), stamp(`_side_idle_${i}`, 1, 9))
  sprite(`diver_side_i${i}`, { base: `_up_side_i${i}`, rot: 90 })
}
for (let i = 0; i < 6; i++) {
  sprite(`_up_side_s${i}`, O, stamp("_side_top", 1, 0), stamp(`_side_swim_${i}`, 1, 9))
  sprite(`diver_side_s${i}`, { base: `_up_side_s${i}`, rot: 90 })
}
// harpoon thrust while swimming: the hand sits under the belly after the rotation, the shaft points forward (right)
sprite("diver_side_a0", { base: "diver_side_i0" }, hline(8, 12, 3, "w"), px(11, 12, "a"), px(12, 12, "a"))
sprite("diver_side_a1", { base: "diver_side_i0" }, hline(8, 12, 6, "w"), px(14, 12, "a"), px(15, 12, "a"), px(14, 11, "a"))

const ROW = d => ["i0", "i1", "s0", "s1", "s2", "s3", "s4", "s5", "a0", "a1"].map(c => `diver_${d}_${c}`)
sheet("diver", [...ROW("down"), ...ROW("up"), ...ROW("side")], { cols: 10, pad: 0 })
