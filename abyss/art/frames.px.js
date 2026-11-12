// 9-슬라이스 픽셀 프레임 (16x16, 모서리 5px). CSS: border-image: url(..) 5 fill / calc(var(--px)*5)
// 외곽 2px 짙은 남색 + 안쪽 1px 하이라이트(위/왼) + 그림자(아래/오른쪽) + 모서리 3px 계단 컷
const CUTS = [[0, 0], [1, 0], [0, 1], [15, 0], [14, 0], [15, 1], [0, 15], [1, 15], [0, 14], [15, 15], [14, 15], [15, 14]]

function frame(name, o) {
  sprite(name, { w: 16, h: 16 },
    rect(0, 0, 16, 16, o.k || "#0a1626"),
    rect(2, 2, 12, 12, o.fill),
    rect(2, 2, 12, 1, o.hi), rect(2, 2, 1, 12, o.hi),
    rect(2, 13, 12, 1, o.sh), rect(13, 2, 1, 12, o.sh),
    ...CUTS.map(([x, y]) => px(x, y, "_")))
}

frame("panel",    { fill: "#0d1a2e", hi: "#3d527c", sh: "#16233b" })
frame("btn",      { fill: "#16304f", hi: "#5b8bb8", sh: "#0f2036" })
frame("btn_down", { fill: "#24456e", hi: "#0f2036", sh: "#5b8bb8" })
frame("act",      { fill: "#1d4a63", hi: "#7fe3ff", sh: "#123247" })
frame("act_down", { fill: "#2f7090", hi: "#123247", sh: "#7fe3ff" })
frame("card",     { fill: "#0f2036", hi: "#2e4668", sh: "#0a1626" })
frame("card_can", { fill: "#12301f", hi: "#6fd18a", sh: "#2f7a4c" })
frame("card_sel", { fill: "#2a2410", hi: "#ffe08a", sh: "#a87a1c" })
frame("card_max", { fill: "#0c1524", hi: "#222f45", sh: "#0a1220" })
frame("chip",     { fill: "#0a1626", hi: "#2e4668", sh: "#050a14", k: "#050a14" })

sheet("ui_frames", ["panel", "btn", "btn_down", "act", "act_down", "card", "card_can", "card_sel", "card_max", "chip"], { pad: 2 })
