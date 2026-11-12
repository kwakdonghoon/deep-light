// Abyss Keeper UI icons. Build: node <pxl>/bin/pxl.js build icons.px.js --out out_icons
// Palette matches the game (game.js / data.js). Outline key k = dark navy.
pal({
  k: "#0a1626",
  a: "#dbe6ee", b: "#8ea3b8", B: "#4a5a73",       // steel: light / mid / dark
  h: "#b9803a", H: "#7a4f1d",                     // wood
  y: "#ffd86b", Y: "#d99a2b", W: "#fff6c8",       // lamp light
  c: "#9fe8ff", C: "#4fc3f7", D: "#2a7fa8",       // water / tank
  r: "#ff6b6b", R: "#b53a4c", w: "#ffffff",       // heart
  o: "#e0904a", O: "#ffd3a0", n: "#a85a24",       // scrap
  m: "#f2c14e", M: "#fff0a8", d: "#a87a1c",       // brass
  t: "#4ff0e0", T: "#23a6a8", e: "#d8fffa",       // crystal
  p: "#f4f0ff", P: "#b9a8e0",                     // pearl
  g: "#d98cff", G: "#8f55d6", q: "#f3d2ff",       // gel
})

// ---- button icons (14px art + 1px outline = 16px) ----
sprite("pick", { mx: true, outline: "k" }, `
  ..aaaaa
  .abbbbb
  ab....h
  a.....h
  ......h
  ......h
  ......h
  ......h
  ......h
  ......h
  ......H
  ......H`)

sprite("harpoon", { mx: true, outline: "k" }, `
  ......a
  ......a
  .....ab
  ....abb
  ...aabb
  ......h
  ......h
  ......h
  ......h
  ......h
  ......H
  ......H`)

sprite("lamp", { mx: true, outline: "k" }, `
  ...yyyy
  ..yyyyy
  .yyyWWW
  .yyyyyy
  .yyyyyy
  ..yyyyY
  ...yYYY
  ....bbb
  ....BBb
  ....bbb
  .....bb`)

sprite("vent", { outline: "k" }, `
  ...cc.....c...
  ...cC....cC...
  ........c.....
  .....cc.cC....
  .....cC.......
  ..............
  ..bbbbbbbbbb..
  .bBbBbBbBbBbb.
  .bbbbbbbbbbbb.
  .bDCDCDCDCDDb.
  .bbbbbbbbbbbb.
  .BBBBBBBBBBBB.`)

sprite("heart", { mx: true, outline: "k" }, `
  .rrr...
  rwrrrr.
  rwrrrrr
  rrrrrrr
  rrrrrrr
  .rrrrrr
  ..rrrrR
  ...rrrR
  ....rRR
  .....RR`)

sprite("hammer", { mx: true, outline: "k" }, `
  .aaaaaa
  .abbbbb
  .abbbbb
  .BBBBBB
  .....hh
  .....hh
  .....hh
  .....hh
  .....hh
  .....hH
  .....HH`)

// ---- move (4-way arrows) for the tutorial ----
sprite("move", { mx: true, outline: "k" }, `
  ......a
  .....aa
  ....aaa
  ......b
  ..a...b
  .aa...b
  aaabbbb
  .aa...b
  ..a...b
  ......b
  ....aaa
  .....aa
  ......a`)

// ---- craft card icons ----
sprite("tank", { mx: true, outline: "k" }, `
  ......B
  .....bb
  ...bbbb
  ..cccCC
  .ccwCCC
  .ccwCCC
  .ccwCCC
  .cccCCC
  .ccDDDD
  .cCCCCC
  .cCCCCC
  ..DDDDD`)

sprite("fins", { mx: true, outline: "k" }, `
  ......b
  ......b
  .....bb
  ....ooo
  ...oooo
  ..ooonn
  .oooonn
  ooooonn
  oooonnn`)

sprite("armor", { mx: true, outline: "k" }, `
  .aaaaaa
  .abbbbb
  .abbbbb
  .abbCCC
  .abbCCC
  .abbbbb
  ..abbbb
  ...abbb
  ....abb
  .....ab
  ......B`)

// ---- resource icons (10px art + 1px outline = 12px) ----
sprite("scrap", { mx: true, outline: "k" }, `
  ...OO
  .OOoo
  .Ooon
  Oooo.
  Ooo..
  Ooo..
  Oooo.
  .Oonn
  .Oonn
  ...nn`)

sprite("brass", { mx: true, outline: "k" }, `
  ..MMM
  .Mmmm
  Mmmmm
  Mmmmm
  ddddd`)

sprite("crystal", { mx: true, outline: "k" }, `
  ....e
  ...et
  ..ett
  .ettT
  etttT
  .tttT
  ..ttT
  ...tT
  ....T`)

sprite("pearl", { mx: true, outline: "k" }, `
  ..ppp
  .pwpp
  ppwpp
  ppppp
  ppppP
  .pPPP
  ..PPP`)

sprite("gel", { mx: true, outline: "k" }, `
  ....g
  ...gg
  ..ggg
  .gqgg
  gqggg
  ggggg
  .gggg
  ..GGG`)

sheet("ui_icons", ["pick", "harpoon", "lamp", "vent", "heart", "hammer", "tank", "fins", "armor", "scrap", "brass", "crystal", "pearl", "gel"], { pad: 2 })

// ---- oxygen potion (cork, glass neck, round flask filled with water) ----
sprite("potion", { mx: true, outline: "k" }, `
  ....h
  ...DD
  ...cc
  ..ccC
  .cccC
  ccCCC
  cCCCC
  cCCCD
  cCCDD
  .CDDD
  ..DDD`)

// ---- beacon expansion (16px: lantern head on a tapering tower) ----
sprite("beacon", { mx: true, outline: "k" }, `
  ......y
  ....yyW
  ....yWW
  ....yyW
  .....bb
  .....bB
  .....bB
  ....bbB
  ....bBB
  ...bbbB
  ..bbbBB
  .bbbbBB
  bbbbBBB
  BBBBBBB`)
