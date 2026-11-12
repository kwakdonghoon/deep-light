'use strict';
// ============================================================
// ABYSS KEEPER — 심연의 파수꾼 (프로토타입)
// 섹션: 설정 / 월드 생성 / 스프라이트 / 입력 / 플레이어 / 적·보스 / 조명 / 렌더 / HUD / 루프
// ============================================================

const W = 160, H = 160, TS = 16, VH = 180;
let VW = 320;                                   // 가로 논리 해상도: 화면 비율에 맞춰 fit()에서 288~428 로 조정
let touchMode = false, paused = false, sheetOpen = false, menuOpen = false;
const CX = W / 2, CY = H / 2;
const BX = CX * TS, BY = CY * TS;                 // 비콘(기지) 월드 좌표
const ARENA = { x: 80, y: 20, r: 9 };             // 보스 아레나(타일 좌표)
const AX = ARENA.x * TS + 8, AY = ARENA.y * TS + 8;

const canvas = document.getElementById('game');
const ctx = canvas.getContext('2d');
canvas.width = VW; canvas.height = VH;
const lightCv = document.createElement('canvas');
lightCv.width = VW; lightCv.height = VH;
const lctx = lightCv.getContext('2d');

function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const params = new URLSearchParams(location.search);
const SAVE_KEY = 'abyss_keeper_save_v1';
const AUTOSAVE = !params.get('seed');            // ?seed= 로 열면 저장 없이 샌드박스
function loadSave() { try { const s = localStorage.getItem(SAVE_KEY); return s ? JSON.parse(s) : null; } catch (e) { return null; } }
const SAVED = AUTOSAVE ? loadSave() : null;
const SEED = parseInt(params.get('seed')) || (SAVED && SAVED.seed) || Math.floor(Math.random() * 1e9);
const rnd = mulberry32(SEED);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const rr = (a, b) => a + Math.random() * (b - a);
const rint = (a, b) => Math.floor(rr(a, b + 1));

// ---------- 월드 ----------
const tiles = new Uint8Array(W * H);
const dmg = new Float32Array(W * H);
const deco = new Uint8Array(W * H);
const explored = new Uint8Array(W * H);
const idx = (x, y) => y * W + x;
const inb = (x, y) => x >= 0 && y >= 0 && x < W && y < H;
const tileAt = (x, y) => inb(x, y) ? tiles[idx(x, y)] : TILE.BEDROCK;
const isSolid = (x, y) => TILE_DEF[tileAt(x, y)].solid;
function zoneOfTile(x, y) {
  const d = Math.hypot(x - CX, y - CY);
  return d < 30 ? 0 : d < 55 ? 1 : 2;
}
const zoneOfPx = (x, y) => zoneOfTile(Math.floor(x / TS), Math.floor(y / TS));

function generate() {
  let g = new Uint8Array(W * H);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    g[idx(x, y)] = (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) ? 1 : (rnd() < 0.46 ? 1 : 0);
  }
  for (let it = 0; it < 4; it++) {
    const n = new Uint8Array(W * H);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) { n[idx(x, y)] = 1; continue; }
      let c = 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) c += g[idx(x + dx, y + dy)];
      n[idx(x, y)] = c >= 5 ? 1 : 0;
    }
    g = n;
  }
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (Math.hypot(x - CX, y - CY) < 7) g[idx(x, y)] = 0;
    if (Math.hypot(x - ARENA.x, y - ARENA.y) <= ARENA.r) g[idx(x, y)] = 0;
  }
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = idx(x, y);
    if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) { tiles[i] = TILE.BEDROCK; continue; }
    if (g[i] === 0) { tiles[i] = TILE.FLOOR; continue; }
    const z = zoneOfTile(x, y);
    tiles[i] = z === 0 ? TILE.ROCK : z === 1 ? TILE.HARD : TILE.DEEP;
  }
  const isRock = t => t === TILE.ROCK || t === TILE.HARD || t === TILE.DEEP;
  function pickOre(z) {
    const r = rnd();
    if (z === 0) return r < 0.85 ? TILE.SCRAP : TILE.BRASS;
    if (z === 1) return r < 0.65 ? TILE.BRASS : r < 0.90 ? TILE.SCRAP : TILE.CRYSTAL;
    return r < 0.25 ? TILE.CRYSTAL : r < 0.65 ? TILE.BRASS : TILE.SCRAP;
  }
  for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) {
    if (!isRock(tiles[idx(x, y)]) || rnd() > 0.024) continue;
    const ore = pickOre(zoneOfTile(x, y));
    tiles[idx(x, y)] = ore;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      if (inb(x + dx, y + dy) && isRock(tiles[idx(x + dx, y + dy)]) && rnd() < 0.5) tiles[idx(x + dx, y + dy)] = ore;
    }
  }
  for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) {
    if (tiles[idx(x, y)] !== TILE.FLOOR) continue;
    if (isSolid(x, y - 1) && rnd() < 0.14) deco[idx(x, y)] = 1;
    else if (rnd() < 0.02) deco[idx(x, y)] = 2;
    else if (rnd() < 0.008 && Math.hypot(x - CX, y - CY) > 10) deco[idx(x, y)] = 3;           // 조개
    else if (rnd() < 0.0035 && Math.hypot(x - CX, y - CY) > 12 && Math.hypot(x - ARENA.x, y - ARENA.y) > ARENA.r + 1) tiles[idx(x, y)] = TILE.CHEST; // 상자
  }
}
generate();

// ---------- 스프라이트(픽셀 문자열 → 캔버스) ----------
function makeSprite(rows, pal) {
  const c = document.createElement('canvas');
  c.width = rows[0].length; c.height = rows.length;
  const g = c.getContext('2d');
  rows.forEach((row, y) => [...row].forEach((ch, x) => {
    if (pal[ch]) { g.fillStyle = pal[ch]; g.fillRect(x, y, 1, 1); }
  }));
  return c;
}
const SPR = {};
SPR.player = makeSprite([
  '...oooooo...',
  '..ohhhhhho..',
  '.ohhggggGho.',
  '.ohgggggGho.',
  '.ohhgggGhHo.',
  '..ohhhhHHo..',
  '.otoSsssSo..',
  'ottoSssssSo.',
  'ottosssssSSo',
  '.ootSsssSSo.',
  '..ooSsSSSo..',
  '...osso.oso.',
  '..offo..offo',
], { o: '#0a1626', h: '#e8b23c', H: '#a87a1c', g: '#8fe9ff', G: '#3aa0c4', s: '#3a74b8', S: '#25508a', t: '#cfd9e2', f: '#e4572e' });
const JP = { b: '#d98cff', B: '#9b5de0', e: '#ffffff' };
const JELLY_TOP = ['....bbbb....', '..bbbbbbbb..', '.bbbbbbbbbb.', '.bbebbbbebb.', '.bbbbbbbbbb.', '.BBBBBBBBBB.'];
SPR.jelly = [
  makeSprite([...JELLY_TOP, '..B.B..B.B..', '..B.B..B.B..', '.B..B..B..B.'], JP),
  makeSprite([...JELLY_TOP, '...B.B.B.B..', '..B.B.B.B...', '...B.B..B.B.'], JP),
];
SPR.angler = makeSprite([
  '....dddddd....',
  '..dddddddddd..',
  '.ddddewdddddd.',
  'ddddddddddddDD',
  'wdwdwddddddDD.',
  'dDDDDDDDDDDD..',
  '.DDDDDDDDDD...',
  '...DD..DD.....',
], { d: '#2d3f6b', D: '#1b2748', w: '#ffffff', e: '#ff4d4d' });
SPR.urchin = (() => {
  const c = document.createElement('canvas'); c.width = 12; c.height = 12;
  const g = c.getContext('2d');
  g.fillStyle = '#a34fd6';
  for (let a = 0; a < 8; a++) {
    const dx = Math.cos(a * Math.PI / 4), dy = Math.sin(a * Math.PI / 4);
    for (let k = 2; k <= 5; k++) g.fillRect(Math.round(6 + dx * k), Math.round(6 + dy * k), 1, 1);
  }
  g.fillStyle = '#6b2d8f'; g.fillRect(4, 4, 4, 4);
  g.fillStyle = '#c98bff'; g.fillRect(5, 5, 1, 1);
  return c;
})();

SPR.crab = makeSprite([
  'cc........cc',
  'cC........Cc',
  '.cccccccccc.',
  '..cecccecc..',
  '.CcccccccCc.',
  '..CCCCCCCC..',
  '.c.c.cc.c.c.',
  'c..c....c..c',
], { c: '#e05a3a', C: '#a53a22', e: '#ffffff' });
SPR.eel = makeSprite([
  '..........yy....',
  '.....YYYYYYYYyy.',
  'eYYYYYYYYYYYYYYy',
  '.YYYYYYYYYYYYYY.',
  '..yyYYYYYYyyyy..',
  '.......yy.......',
], { y: '#ffe14d', Y: '#b8a000', e: '#ff3b3b' });
SPR.clam = makeSprite([
  '..PPPP..',
  '.PppppP.',
  'PppppppP',
  'PPPwwPPP',
  '.PPPPPP.',
], { p: '#d9c9e8', P: '#9b88b8', w: '#ffffff' });
SPR.chest = makeSprite([
  '..BBBBBBBB..',
  '.BbbbbbbbbB.',
  'BbbbbbbbbbbB',
  'BBBBggggBBBB',
  'BbbbbGGbbbbB',
  'BbbbbggbbbbB',
  'BbbbbbbbbbbB',
  'BbbbbbbbbbbB',
  'BBBBBBBBBBBB',
  '.BB......BB.',
], { b: '#7a4a22', B: '#4a2a10', g: '#f2c14e', G: '#fff0a8' });

// 타일 텍스처(변형 4종)
const TEX = {};
function makeTex() {
  for (const k in TILE_DEF) {
    const def = TILE_DEF[k];
    if (!def.base) continue;
    TEX[k] = [];
    for (let v = 0; v < 4; v++) {
      const r = mulberry32(Number(k) * 97 + v * 13 + 5);
      const c = document.createElement('canvas'); c.width = TS; c.height = TS;
      const g = c.getContext('2d');
      g.fillStyle = def.base[0]; g.fillRect(0, 0, TS, TS);
      for (let i = 0; i < 46; i++) {
        g.fillStyle = def.base[1 + (r() < 0.5 ? 0 : 1)];
        g.fillRect(Math.floor(r() * TS), Math.floor(r() * TS), r() < 0.3 ? 2 : 1, 1);
      }
      if (def.ore) {
        const n = 3 + Math.floor(r() * 3);
        for (let i = 0; i < n; i++) {
          const ox = 2 + Math.floor(r() * 11), oy = 2 + Math.floor(r() * 11);
          g.fillStyle = def.ore[0]; g.fillRect(ox, oy, 2, 2); g.fillRect(ox - 1, oy, 1, 1);
          g.fillStyle = def.ore[1]; g.fillRect(ox, oy, 1, 1);
        }
      }
      TEX[k].push(c);
    }
  }
  TEX.floor = [];
  for (let v = 0; v < 4; v++) {
    const r = mulberry32(777 + v);
    const c = document.createElement('canvas'); c.width = TS; c.height = TS;
    const g = c.getContext('2d');
    g.fillStyle = '#16365a'; g.fillRect(0, 0, TS, TS);
    for (let i = 0; i < 30; i++) {
      g.fillStyle = ['#1b4268', '#112a47', '#2f6a8c'][Math.floor(r() * 3)];
      g.fillRect(Math.floor(r() * TS), Math.floor(r() * TS), 1, 1);
    }
    TEX.floor.push(c);
  }
}
makeTex();

function disc(g, cx, cy, r) {
  cx = Math.round(cx); cy = Math.round(cy); r = Math.round(r);
  for (let dy = -r; dy <= r; dy++) {
    const w = Math.floor(Math.sqrt(r * r - dy * dy + 0.5));
    g.fillRect(cx - w, cy + dy, w * 2 + 1, 1);
  }
}

// ---------- 입력 ----------
const keys = {}, pressed = {};
const mouse = { x: 160, y: 90, l: false, r: false };
let started = false;
addEventListener('keydown', e => {
  if (!keys[e.code]) pressed[e.code] = true;
  keys[e.code] = true;
  if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Tab'].includes(e.code)) e.preventDefault();
  if (!started) {
    if (e.code === 'KeyN' && SAVED) { try { localStorage.removeItem(SAVE_KEY); } catch (_) {} location.href = location.pathname; return; }
    startGame();
  }
});
addEventListener('keyup', e => { keys[e.code] = false; });
function updateMouse(e) {
  const r = canvas.getBoundingClientRect();
  mouse.x = (e.clientX - r.left) * VW / r.width;
  mouse.y = (e.clientY - r.top) * VH / r.height;
}
addEventListener('mousemove', e => { if (!touchMode) updateMouse(e); });
addEventListener('mousedown', e => {
  if (touchMode) return;
  updateMouse(e); if (e.button === 0) mouse.l = true; if (e.button === 2) mouse.r = true;
  if (!started && !e.target.closest('button')) startGame();
});
addEventListener('mouseup', e => { if (touchMode) return; if (e.button === 0) mouse.l = false; if (e.button === 2) mouse.r = false; });
addEventListener('contextmenu', e => e.preventDefault());
addEventListener('blur', () => { for (const k in keys) keys[k] = false; mouse.l = mouse.r = false; });

// ---------- 효과음 (WebAudio, 외부 파일 없음) ----------
let AC = null, muted = false, lastMineSfx = 0;
function audioInit() { try { AC = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { AC = null; } }
function tone(f0, f1, dur, type = 'square', vol = 0.05) {
  if (!AC || muted) return;
  const t = AC.currentTime, o = AC.createOscillator(), g = AC.createGain();
  o.type = type; o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(30, f1), t + dur);
  g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); g.connect(AC.destination); o.start(t); o.stop(t + dur);
}
function noise(dur, vol = 0.05, hp = 800) {
  if (!AC || muted) return;
  const n = Math.floor(AC.sampleRate * dur), buf = AC.createBuffer(1, n, AC.sampleRate), d = buf.getChannelData(0);
  for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / n);
  const s = AC.createBufferSource(), f = AC.createBiquadFilter(), g = AC.createGain();
  s.buffer = buf; f.type = 'highpass'; f.frequency.value = hp; g.gain.value = vol;
  s.connect(f); f.connect(g); g.connect(AC.destination); s.start();
}
const SFX = {
  mine: () => noise(0.05, 0.04, 1500),
  brk: () => { noise(0.12, 0.07, 300); tone(120, 60, 0.12, 'triangle', 0.08); },
  swing: () => noise(0.08, 0.03, 2500),
  hit: () => tone(300, 120, 0.1, 'sawtooth', 0.05),
  hurt: () => tone(200, 70, 0.25, 'sawtooth', 0.08),
  pick: () => tone(700, 1200, 0.08, 'sine', 0.06),
  craft: () => { tone(440, 880, 0.12, 'square', 0.05); setTimeout(() => tone(660, 1320, 0.15, 'square', 0.05), 90); },
  chest: () => { tone(520, 1040, 0.1, 'triangle', 0.07); setTimeout(() => tone(780, 1560, 0.18, 'triangle', 0.07), 100); },
  boss: () => tone(90, 40, 0.8, 'sawtooth', 0.1),
};

// ---------- 상태 ----------
const P = {
  x: BX, y: BY + 28, vx: 0, vy: 0, hp: UP.armor[0].hp, o2: UP.tank[0].max, face: 1,
  inv: 0, atkCd: 0, swing: 0, swingAng: 0, bob: 0, suffocate: 0, bubbleT: 0,
  lvl: { pick: 0, harpoon: 0, light: 0, tank: 0, fins: 0, armor: 0 },
};
const inv = { scrap: 0, brass: 0, crystal: 0, pearl: 0, gel: 0, lamp: 3, vent: 0 };
const enemies = [], bullets = [], particles = [];
const boss = { active: false, dead: false, x: AX, y: AY, hp: BOSS_DEF.hp, t: 0, fireT: 2, chargeT: 5, charge: 0, cdx: 0, cdy: 0, flash: 0 };
const cam = { x: 0, y: 0, shake: 0 };
const cursor = { tx: 0, ty: 0, reach: false };
let time = 0, mining = false, spawnT = 0, hintCd = 0, lastPT = { x: -1, y: -1 };
let victory = false;

const pickDef = () => UP.pick[P.lvl.pick];
const maxO2 = () => UP.tank[P.lvl.tank].max;
const maxHp = () => UP.armor[P.lvl.armor].hp;

function addItem(id, n) { inv[id] = (inv[id] || 0) + n; hudDirty = true; }
function dropFrom(drop) {
  for (const id in drop) {
    const n = rint(drop[id][0], drop[id][1]);
    if (n <= 0) continue;
    addItem(id, n); floatText(`+${n} ${ITEM_NAME[id]}`); SFX.pick();
  }
}

// ---------- 충돌 ----------
function collides(x, y, h) {
  const x0 = Math.floor((x - h) / TS), x1 = Math.floor((x + h - 0.01) / TS);
  const y0 = Math.floor((y - h) / TS), y1 = Math.floor((y + h - 0.01) / TS);
  for (let ty = y0; ty <= y1; ty++) for (let tx = x0; tx <= x1; tx++) if (isSolid(tx, ty)) return true;
  return false;
}
function moveBox(e, dx, dy, h) {
  let hit = false;
  if (!collides(e.x + dx, e.y, h)) e.x += dx; else hit = true;
  if (!collides(e.x, e.y + dy, h)) e.y += dy; else hit = true;
  return hit;
}

// ---------- 파티클/텍스트 ----------
function spawnParticle(x, y, vx, vy, life, color, size = 1) {
  if (particles.length > 400) return;
  particles.push({ x, y, vx, vy, life, max: life, color, size });
}
const floaters = [];
function floatText(t) { toast(t, 0.9, true); }

// ---------- 토스트 ----------
const toastEl = document.getElementById('toast');
let toastTimer = 0;
function toast(msg, sec = 2.2, quiet = false) {
  if (quiet && toastTimer > 0.8) return;
  toastEl.textContent = msg; toastEl.style.opacity = 1; toastTimer = sec;
  if (sheetOpen) { const m = document.getElementById('sheetMsg'); if (m) m.textContent = msg; }   // 제작 시트가 토스트를 가리므로 시트 안에도 표시
}
function hint(msg) { if (hintCd <= 0) { toast(msg, 1.4); hintCd = 1.2; } }

// ---------- 플레이어 ----------
function damagePlayer(n, ignoreInv = false) {
  if (!started || victoryLock) return;
  if (P.inv > 0 && !ignoreInv) return;
  P.hp -= n; if (!ignoreInv) P.inv = 0.9;
  cam.shake = 0.25; hudDirty = true; SFX.hurt(); buzz(40);
  for (let i = 0; i < 6; i++) spawnParticle(P.x, P.y, rr(-40, 40), rr(-40, 40), 0.4, '#ff6b6b');
  if (P.hp <= 0) die();
}
let victoryLock = false;
function die() {
  for (const k of ['scrap', 'brass', 'crystal', 'gel']) inv[k] = Math.floor(inv[k] * (1 - TUNE.deathLossRate));
  P.x = BX; P.y = BY + 28; P.vx = P.vy = 0; P.hp = maxHp(); P.o2 = maxO2(); P.inv = 1.5;
  boss.active = false; boss.hp = BOSS_DEF.hp; bullets.length = 0;
  toast('기절해서 기지로 돌아왔다… 자원 일부를 잃었다', 3.5); hudDirty = true;
}
function nearBeacon() { return Math.hypot(P.x - BX, P.y - BY) < TUNE.craftRadius; }

function nearVent() {
  const ptx = Math.floor(P.x / TS), pty = Math.floor(P.y / TS), R = TUNE.ventRadiusTiles;
  for (let y = pty - R; y <= pty + R; y++) for (let x = ptx - R; x <= ptx + R; x++) {
    if (tileAt(x, y) === TILE.VENT && Math.hypot(x * TS + 8 - P.x, y * TS + 8 - P.y) <= R * TS) return true;
  }
  return false;
}

function updatePlayer(dt) {
  let ax = (keys.KeyD || keys.ArrowRight ? 1 : 0) - (keys.KeyA || keys.ArrowLeft ? 1 : 0);
  let ay = (keys.KeyS || keys.ArrowDown ? 1 : 0) - (keys.KeyW || keys.ArrowUp ? 1 : 0);
  const l = Math.hypot(ax, ay) || 1; ax /= l; ay /= l;
  if (touchMode) { ax = touch.jx; ay = touch.jy; }          // 조이스틱은 아날로그(0~1)
  const k = Math.min(1, dt * 7);
  const spd = UP.fins[P.lvl.fins].speed;
  P.vx += (ax * spd - P.vx) * k;
  P.vy += (ay * spd - P.vy) * k;
  moveBox(P, P.vx * dt, P.vy * dt, 5);
  P.bob += dt * 4;
  if (!touchMode || Math.abs(touch.face.x) > 0.2) P.face = (mouse.x + cam.x) >= P.x ? 1 : -1;
  if (P.inv > 0) P.inv -= dt;

  // 산소
  const safe = Math.hypot(P.x - BX, P.y - BY) < TUNE.beaconRadius || nearVent();
  if (safe) {
    // 비콘 안에서는 체력도 천천히 회복 (젤을 업그레이드에 쓰게 하려는 장치)
    if (Math.hypot(P.x - BX, P.y - BY) < TUNE.beaconRadius && P.hp < maxHp()) P.hp = Math.min(maxHp(), P.hp + 0.5 * dt);
    P.o2 = Math.min(maxO2(), P.o2 + TUNE.o2Refill * dt);
    P.suffocate = 0;
  } else {
    P.o2 -= (TUNE.o2DrainBase + zoneOfPx(P.x, P.y) * TUNE.o2DrainPerZone) * dt;
    if (P.o2 <= 0) {
      P.o2 = 0; P.suffocate += dt;
      if (P.suffocate >= 1) { P.suffocate = 0; damagePlayer(1, true); toast('산소 부족! 비콘이나 환기구로 돌아가자', 1.5, true); }
    }
  }
  // 거품
  P.bubbleT -= dt;
  if (P.bubbleT <= 0) { P.bubbleT = rr(0.3, 0.7); spawnParticle(P.x + P.face * 3, P.y - 6, rr(-4, 4), -14, 1.4, '#9fe8ff'); }
  // 먹기
  if (pressed.KeyF) {
    if (inv.gel >= 2 && P.hp < maxHp()) { inv.gel -= 2; P.hp = Math.min(maxHp(), P.hp + 3); hudDirty = true; toast('젤을 먹고 체력 회복'); }
    else toast(inv.gel < 2 ? '젤이 2개 필요하다' : '체력이 가득 찼다', 1.2);
  }
}

// 채굴 / 설치
function breakTile(tx, ty) {
  const i = idx(tx, ty), def = TILE_DEF[tiles[i]];
  const col = def.ore ? def.ore[0] : def.base[1];
  for (let k = 0; k < 8; k++) spawnParticle(tx * TS + 8, ty * TS + 8, rr(-50, 50), rr(-50, 50), 0.5, col, 2);
  if (def.drop) dropFrom(def.drop);
  SFX.brk();
  tiles[i] = TILE.FLOOR; dmg[i] = 0; deco[i] = 0;
  miniDirtyTile(tx, ty);
}
function updateMining(dt) {
  const tx = Math.floor((mouse.x + cam.x) / TS), ty = Math.floor((mouse.y + cam.y) / TS);
  cursor.tx = tx; cursor.ty = ty;
  cursor.reach = Math.hypot(tx * TS + 8 - P.x, ty * TS + 8 - P.y) <= TUNE.reach;
  mining = false;
  if (mouse.l && cursor.reach && inb(tx, ty)) {
    const i = idx(tx, ty), t = tiles[i], def = TILE_DEF[t];
    if (t === TILE.CHEST) {
      openChest(tx, ty);
    } else if (t === TILE.LAMP || t === TILE.VENT) {
      tiles[i] = TILE.FLOOR; addItem(t === TILE.LAMP ? 'lamp' : 'vent', 1); miniDirtyTile(tx, ty);
    } else if (!def.solid && deco[i] === 3) {
      deco[i] = 0;
      const n = zoneOfTile(tx, ty) === 2 ? 2 : 1;
      addItem('pearl', n); floatText(`+${n} 진주`); SFX.pick();
      for (let k = 0; k < 6; k++) spawnParticle(tx * TS + 8, ty * TS + 12, rr(-30, 30), rr(-40, 0), 0.5, '#ffffff');
    } else if (def.solid) {
      if (def.unbreakable) hint('기반암은 깰 수 없다');
      else if (def.tier > pickDef().tier) hint(`${def.name}: 더 좋은 곡괭이가 필요하다`);
      else {
        dmg[i] += pickDef().power * dt; mining = true;
        if (time - lastMineSfx > 0.14) { lastMineSfx = time; SFX.mine(); }
        if (Math.random() < dt * 14) spawnParticle(tx * TS + 8, ty * TS + 8, rr(-30, 30), rr(-30, 30), 0.35, def.ore ? def.ore[0] : def.base[1], 1);
        if (dmg[i] >= def.hp) breakTile(tx, ty);
      }
    }
  }
  if (pressed.KeyQ) placeItem('lamp', TILE.LAMP);
  if (pressed.KeyE) placeItem('vent', TILE.VENT);
}
function openChest(tx, ty) {
  const loot = CHEST_LOOT[zoneOfTile(tx, ty)], got = [];
  tiles[idx(tx, ty)] = TILE.FLOOR; miniDirtyTile(tx, ty);
  for (const id in loot) {
    const n = rint(loot[id][0], loot[id][1]);
    if (n > 0) { addItem(id, n); got.push(`${ITEM_NAME[id]} ${n}`); }
  }
  SFX.chest(); buzz(20); toast(`보물 상자! ${got.join(', ')}`, 3);
  for (let k = 0; k < 12; k++) spawnParticle(tx * TS + 8, ty * TS + 8, rr(-50, 50), rr(-60, 10), 0.7, ['#ffd86b', '#fff0a8', '#ffffff'][k % 3], 2);
}
function placeItem(item, tileId) {
  const { tx, ty } = cursor;
  if (inv[item] <= 0) return toast(`${ITEM_NAME[item]}이(가) 없다 — 기지에서 제작하자`, 1.6);
  if (!cursor.reach || tileAt(tx, ty) !== TILE.FLOOR) return toast('여기에는 놓을 수 없다', 1.2);
  tiles[idx(tx, ty)] = tileId; deco[idx(tx, ty)] = 0; inv[item]--; hudDirty = true; miniDirtyTile(tx, ty);
}

// 전투
function updateCombat(dt) {
  P.atkCd -= dt; if (P.swing > 0) P.swing -= dt;
  if ((mouse.r || keys.Space) && P.atkCd <= 0) {
    P.atkCd = 0.38; P.swing = 0.16; SFX.swing();
    const ax = mouse.x + cam.x - P.x, ay = mouse.y + cam.y - P.y;
    const al = Math.hypot(ax, ay) || 1, nx = ax / al, ny = ay / al;
    P.swingAng = Math.atan2(ny, nx);
    const d = UP.harpoon[P.lvl.harpoon].dmg, range = touchMode ? 36 : 26;   // 터치는 정밀 조준이 어려워 사거리를 넉넉히
    const targets = enemies.slice();
    if (boss.active) targets.push(boss);
    for (const e of targets) {
      const er = e === boss ? 14 : ENEMY_DEF[e.type].r;
      const dx = e.x - P.x, dy = e.y - P.y, dist = Math.hypot(dx, dy);
      if (dist > range + er) continue;
      if (dist > 10 && (dx * nx + dy * ny) / dist < 0.25) continue;
      hitEnemy(e, d, nx, ny);
    }
  }
}
function hitEnemy(e, d, nx, ny) {
  if (e !== boss && ENEMY_DEF[e.type].armor) d = Math.max(1, d - ENEMY_DEF[e.type].armor);
  e.hp -= d; e.flash = 0.15; SFX.hit();
  if (e !== boss) { e.kx = nx * 90; e.ky = ny * 90; }
  for (let k = 0; k < 5; k++) spawnParticle(e.x, e.y, rr(-40, 40), rr(-40, 40), 0.35, '#ffffff');
  if (e === boss) { if (e.hp <= 0) killBoss(); return; }
  if (e.hp <= 0) {
    dropFrom(ENEMY_DEF[e.type].drop);
    const i = enemies.indexOf(e); if (i >= 0) enemies.splice(i, 1);
  }
}

// ---------- 적 ----------
function spawnEnemy(type, x, y) {
  enemies.push({ type, x, y, vx: 0, vy: 0, kx: 0, ky: 0, hp: ENEMY_DEF[type].hp, t: Math.random() * 10, flash: 0, lunge: 0, lt: rr(1.5, 3), dirx: 0, diry: 0, wt: 0 });
}
function pickType(zone) {
  const list = Object.keys(ENEMY_DEF).filter(k => ENEMY_DEF[k].minZone <= zone);
  let total = list.reduce((s, k) => s + ENEMY_DEF[k].weight, 0), r = Math.random() * total;
  for (const k of list) { r -= ENEMY_DEF[k].weight; if (r <= 0) return k; }
  return list[0];
}
function populate() {
  for (let y = 3; y < H - 3; y++) for (let x = 3; x < W - 3; x++) {
    if (tiles[idx(x, y)] !== TILE.FLOOR) continue;
    if (Math.hypot(x - CX, y - CY) < 14) continue;
    if (Math.hypot(x - ARENA.x, y - ARENA.y) <= ARENA.r + 1) continue;
    const z = zoneOfTile(x, y);
    if (Math.random() < 0.009 + z * 0.0025) spawnEnemy(pickType(z), x * TS + 8, y * TS + 8);
  }
}
populate();

function wander(e, dt, spd) {
  e.wt -= dt;
  if (e.wt <= 0) { e.wt = rr(1.5, 3); const a = Math.random() * 6.28; e.dirx = Math.cos(a); e.diry = Math.sin(a); }
  return [e.dirx * spd, e.diry * spd];
}
function updateEnemies(dt) {
  for (const e of enemies) {
    const def = ENEMY_DEF[e.type];
    e.t += dt; if (e.flash > 0) e.flash -= dt;
    const dx = P.x - e.x, dy = P.y - e.y, dist = Math.hypot(dx, dy) || 1;
    let vx = 0, vy = 0;
    if (e.type === 'jelly') {
      e.wt -= dt;
      if (e.wt <= 0) { e.wt = rr(1.5, 3.5); const a = Math.random() * 6.28; e.dirx = Math.cos(a); e.diry = Math.sin(a); }
      const pulse = 0.5 + 0.5 * Math.sin(e.t * 3);
      vx = e.dirx * def.speed * pulse; vy = e.diry * def.speed * pulse;
    } else if (e.type === 'angler') {
      e.lt -= dt;
      if (e.lunge > 0) { e.lunge -= dt; vx = e.lx * 120; vy = e.ly * 120; }
      else if (dist < 120) {
        vx = dx / dist * def.speed; vy = dy / dist * def.speed;
        if (e.lt <= 0 && dist < 80) { e.lunge = 0.35; e.lx = dx / dist; e.ly = dy / dist; e.lt = rr(2.5, 3.5); }
      } else {
        e.wt -= dt;
        if (e.wt <= 0) { e.wt = rr(1.5, 3); const a = Math.random() * 6.28; e.dirx = Math.cos(a); e.diry = Math.sin(a); }
        vx = e.dirx * 12; vy = e.diry * 12;
      }
    }
    if (e.type === 'crab') {
      if (dist < 100) { vx = dx / dist * def.speed; vy = dy / dist * def.speed; }
      else { [vx, vy] = wander(e, dt, 10); }
    } else if (e.type === 'eel') {
      e.lt -= dt;
      if (e.lunge > 0) { e.lunge -= dt; vx = e.lx * 170; vy = e.ly * 170; if (e.lunge <= 0) e.back = 0.9; }
      else if (e.back > 0) { e.back -= dt; vx = -dx / dist * 40; vy = -dy / dist * 40; }   // 돌진 후 물러나기
      else if (e.wind > 0) { e.wind -= dt; if (e.wind <= 0) { e.lunge = 0.5; e.lx = dx / dist; e.ly = dy / dist; } }
      else if (dist < 130 && e.lt <= 0) { e.wind = 0.5; e.lt = rr(2.2, 3.2); }
      else if (dist < 160) { vx = dx / dist * 18; vy = dy / dist * 18; }
      else { [vx, vy] = wander(e, dt, 14); }
    }
    vx += e.kx; vy += e.ky; e.kx *= 0.88; e.ky *= 0.88;
    if (vx || vy) {
      const hit = moveBox(e, vx * dt, vy * dt, def.r - 1);
      if (hit) { e.dirx = -e.dirx; e.diry = -e.diry; }
    }
    if (dist < def.r + 5) {
      damagePlayer(def.dmg);
      if (P.inv > 0.8) { P.vx = -dx / dist * 120; P.vy = -dy / dist * 120; }
    }
  }
}
function spawner(dt) {
  spawnT -= dt;
  if (spawnT > 0 || enemies.length > 45) return;
  spawnT = 3;
  for (let tries = 0; tries < 12; tries++) {
    const a = Math.random() * 6.28, d = rr(150, 260);
    const x = P.x + Math.cos(a) * d, y = P.y + Math.sin(a) * d;
    const tx = Math.floor(x / TS), ty = Math.floor(y / TS);
    if (!inb(tx, ty) || tiles[idx(tx, ty)] !== TILE.FLOOR) continue;
    if (Math.hypot(x - BX, y - BY) < 200) continue;
    if (Math.hypot(tx - ARENA.x, ty - ARENA.y) <= ARENA.r + 1) continue;
    spawnEnemy(pickType(zoneOfTile(tx, ty)), tx * TS + 8, ty * TS + 8);
    break;
  }
}

// ---------- 보스 ----------
const bossBar = document.getElementById('bossbar'), bossFill = document.getElementById('bossfill');
function updateBoss(dt) {
  if (boss.dead) return;
  const dA = Math.hypot(P.x - AX, P.y - AY);
  if (!boss.active) {
    if (dA < (ARENA.r + 3) * TS && started) {
      boss.active = true; boss.x = AX; boss.y = AY - 40; boss.hp = BOSS_DEF.hp; boss.t = 0; boss.fireT = 2.5; boss.chargeT = 6; boss.charge = 0;
      toast('심연의 눈이 깨어났다!', 3); SFX.boss();
    }
    bossBar.style.display = 'none';
    return;
  }
  if (dA > (ARENA.r + 14) * TS) { boss.active = false; bossBar.style.display = 'none'; return; }
  boss.t += dt; if (boss.flash > 0) boss.flash -= dt;
  const phase2 = boss.hp < BOSS_DEF.hp * 0.5;
  const dx = P.x - boss.x, dy = P.y - boss.y, dist = Math.hypot(dx, dy) || 1;
  if (boss.charge > 0) {
    boss.charge -= dt; boss.x += boss.cdx * 140 * dt; boss.y += boss.cdy * 140 * dt;
  } else {
    boss.x += dx / dist * (phase2 ? 30 : 22) * dt; boss.y += dy / dist * (phase2 ? 30 : 22) * dt;
    boss.chargeT -= dt;
    if (boss.chargeT <= 0) { boss.charge = 0.7; boss.cdx = dx / dist; boss.cdy = dy / dist; boss.chargeT = phase2 ? 4 : 6; }
  }
  boss.fireT -= dt;
  if (boss.fireT <= 0) {
    boss.fireT = phase2 ? 1.5 : 2.4;
    const n = phase2 ? 14 : 10, off = Math.random() * 6.28;
    for (let i = 0; i < n; i++) {
      const a = off + i * 6.28318 / n;
      bullets.push({ x: boss.x, y: boss.y, vx: Math.cos(a) * 55, vy: Math.sin(a) * 55, life: 6 });
    }
  }
  const bdx = boss.x - AX, bdy = boss.y - AY, bd = Math.hypot(bdx, bdy), lim = (ARENA.r - 2) * TS;
  if (bd > lim) { boss.x = AX + bdx / bd * lim; boss.y = AY + bdy / bd * lim; }
  if (dist < 18) damagePlayer(BOSS_DEF.contactDmg);
  bossBar.style.display = 'block'; bossFill.style.width = `${Math.max(0, boss.hp / BOSS_DEF.hp * 100)}%`;
}
function killBoss() {
  boss.dead = true; boss.active = false; bossBar.style.display = 'none'; bullets.length = 0;
  for (let k = 0; k < 40; k++) spawnParticle(boss.x, boss.y, rr(-90, 90), rr(-90, 90), 1.2, ['#ff6b6b', '#ffd86b', '#4ff0e0'][k % 3], 2);
  addItem('crystal', 10); addItem('brass', 10);
  victory = true; SFX.boss(); saveGame();
  document.getElementById('victory').style.display = 'flex';
  setTimeout(() => { document.getElementById('victory').style.display = 'none'; }, 7000);
}
function updateBullets(dt) {
  for (let i = bullets.length - 1; i >= 0; i--) {
    const b = bullets[i];
    b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt;
    if (b.life <= 0 || isSolid(Math.floor(b.x / TS), Math.floor(b.y / TS))) { bullets.splice(i, 1); continue; }
    if (Math.hypot(b.x - P.x, b.y - P.y) < 6) { damagePlayer(BOSS_DEF.bulletDmg); bullets.splice(i, 1); }
  }
}
function updateParticles(dt) {
  for (let i = particles.length - 1; i >= 0; i--) {
    const p = particles[i]; p.life -= dt;
    if (p.life <= 0) { particles.splice(i, 1); continue; }
    p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.96; p.vy *= 0.96;
  }
}

// ---------- 미니맵 ----------
const miniCv = document.getElementById('mini'), miniCtx = miniCv.getContext('2d');
miniCv.width = W; miniCv.height = H;
const miniBase = document.createElement('canvas'); miniBase.width = W; miniBase.height = H;
const miniBaseCtx = miniBase.getContext('2d');
const miniImg = miniBaseCtx.createImageData(W, H);
function hexRGB(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
for (let i = 0; i < W * H; i++) { miniImg.data[i * 4] = 5; miniImg.data[i * 4 + 1] = 8; miniImg.data[i * 4 + 2] = 15; miniImg.data[i * 4 + 3] = 255; }
let miniDirty = true;
function miniPixel(x, y) {
  const i = idx(x, y);
  const [r, g, b] = hexRGB(TILE_DEF[tiles[i]].mini);
  miniImg.data[i * 4] = r; miniImg.data[i * 4 + 1] = g; miniImg.data[i * 4 + 2] = b;
  miniDirty = true;
}
function miniDirtyTile(x, y) { if (explored[idx(x, y)]) miniPixel(x, y); }
function explore() {
  const ptx = Math.floor(P.x / TS), pty = Math.floor(P.y / TS);
  if (ptx === lastPT.x && pty === lastPT.y) return;
  lastPT = { x: ptx, y: pty };
  const R = Math.ceil(UP.light[P.lvl.light].r / TS) + 1;
  for (let y = pty - R; y <= pty + R; y++) for (let x = ptx - R; x <= ptx + R; x++) {
    if (!inb(x, y) || Math.hypot(x - ptx, y - pty) > R) continue;
    if (!explored[idx(x, y)]) { explored[idx(x, y)] = 1; miniPixel(x, y); }
  }
}
function drawMini() {
  if (miniDirty) { miniBaseCtx.putImageData(miniImg, 0, 0); miniDirty = false; }
  miniCtx.clearRect(0, 0, W, H);
  miniCtx.drawImage(miniBase, 0, 0);
  miniCtx.fillStyle = '#ffffff'; miniCtx.fillRect(CX - 1, CY - 1, 3, 3);
  miniCtx.fillStyle = (Math.floor(time * 3) % 2) ? '#ffffff' : '#ff5050'; miniCtx.fillRect(Math.floor(P.x / TS) - 1, Math.floor(P.y / TS) - 1, 3, 3);   // 플레이어는 점멸
  if (!boss.dead) { miniCtx.fillStyle = '#c04040'; miniCtx.fillRect(ARENA.x - 1, ARENA.y - 1, 3, 3); }
}

// ---------- 조명 ----------
const BANDS = [[1, 0.25], [0.78, 0.4], [0.56, 0.55], [0.34, 0.7]];   // 4단 밴드, 알파 간격을 균등하게
function lightAt(x, y, r) {
  if (x + r < 0 || y + r < 0 || x - r > VW || y - r > VH) return;
  lctx.fillStyle = '#000';
  for (const [k, a] of BANDS) { lctx.globalAlpha = a; disc(lctx, x, y, r * k); }
  lctx.globalAlpha = 1;
}
function renderLight(cx, cy) {
  lctx.globalCompositeOperation = 'source-over';
  lctx.globalAlpha = 1;
  lctx.fillStyle = 'rgba(5,10,22,0.93)';
  lctx.fillRect(0, 0, VW, VH);
  lctx.globalCompositeOperation = 'destination-out';
  const fl = Math.sin(time * 9) * 1.5;
  lightAt(P.x - cx, P.y - cy, UP.light[P.lvl.light].r + fl);
  lightAt(BX - cx, BY - cy, 96 + fl);
  const tx0 = Math.floor(cx / TS) - 8, tx1 = Math.floor((cx + VW) / TS) + 8;
  const ty0 = Math.floor(cy / TS) - 8, ty1 = Math.floor((cy + VH) / TS) + 8;
  for (let ty = ty0; ty <= ty1; ty++) for (let tx = tx0; tx <= tx1; tx++) {
    if (!inb(tx, ty)) continue;
    const t = tiles[idx(tx, ty)];
    if (t === TILE.LAMP) lightAt(tx * TS + 8 - cx, ty * TS + 6 - cy, 52 + fl);
    else if (t === TILE.VENT) lightAt(tx * TS + 8 - cx, ty * TS + 8 - cy, 30);
    else if (t === TILE.CRYSTAL) lightAt(tx * TS + 8 - cx, ty * TS + 8 - cy, TILE_DEF[t].glow);
    else if (t === TILE.CHEST) lightAt(tx * TS + 8 - cx, ty * TS + 8 - cy, 22);
    else if (deco[idx(tx, ty)] === 3) lightAt(tx * TS + 8 - cx, ty * TS + 12 - cy, 11);
  }
  for (const e of enemies) {
    if (e.type === 'jelly') lightAt(e.x - cx, e.y - cy, 15);
    else if (e.type === 'angler') lightAt(e.x - cx + (e.vx > 0 ? 8 : -8), e.y - cy - 9, 22 + fl);
    else if (e.type === 'eel') lightAt(e.x - cx, e.y - cy, 18 + fl);
  }
  if (boss.active || (!boss.dead && Math.hypot(P.x - AX, P.y - AY) < 200)) lightAt(boss.active ? boss.x - cx : AX - cx, boss.active ? boss.y - cy : AY - cy, 60 + fl * 2);
  for (const b of bullets) lightAt(b.x - cx, b.y - cy, 12);
  lctx.globalCompositeOperation = 'source-over';
}

// ---------- 렌더 ----------
function hash(x, y) { return ((x * 73856093) ^ (y * 19349663)) >>> 0; }
function render() {
  let cx = Math.round(clamp(P.x - VW / 2, 0, W * TS - VW)), cy = Math.round(clamp(P.y - VH / 2, 0, H * TS - VH));
  if (cam.shake > 0) { cx += Math.round(rr(-2, 2)); cy += Math.round(rr(-2, 2)); }
  cam.x = clamp(P.x - VW / 2, 0, W * TS - VW); cam.y = clamp(P.y - VH / 2, 0, H * TS - VH);
  const ox = cx - cam.x, oy = cy - cam.y; // shake 오프셋 포함 적용 값은 cx,cy 사용
  ctx.fillStyle = '#050a14'; ctx.fillRect(0, 0, VW, VH);

  const tx0 = Math.max(0, Math.floor(cx / TS)), tx1 = Math.min(W - 1, Math.floor((cx + VW) / TS));
  const ty0 = Math.max(0, Math.floor(cy / TS)), ty1 = Math.min(H - 1, Math.floor((cy + VH) / TS));
  for (let ty = ty0; ty <= ty1; ty++) for (let tx = tx0; tx <= tx1; tx++) {
    const i = idx(tx, ty), t = tiles[i], sx = tx * TS - cx, sy = ty * TS - cy, h = hash(tx, ty) & 3;
    const def = TILE_DEF[t];
    if (def.solid) {
      ctx.drawImage(TEX[t][h], sx, sy);
      if (!isSolid(tx, ty - 1)) { ctx.fillStyle = 'rgba(255,255,255,0.14)'; ctx.fillRect(sx, sy, TS, 2); }
      if (dmg[i] > 0) {
        const p = dmg[i] / def.hp; ctx.fillStyle = 'rgba(0,0,0,0.65)';
        const r = mulberry32(i);
        const n = Math.floor(p * 14) + 1;
        for (let k = 0; k < n; k++) { ctx.fillRect(sx + 2 + Math.floor(r() * 12), sy + 2 + Math.floor(r() * 12), 1, 2); }
      }
    } else {
      ctx.drawImage(TEX.floor[h], sx, sy);
      if (isSolid(tx, ty - 1)) { ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.fillRect(sx, sy, TS, 4); }
      const dc = deco[i];
      if (dc === 1) {
        ctx.fillStyle = '#2f8f6b';
        for (let s = 0; s < 3; s++) {
          const bx = sx + 3 + s * 5, hh = 5 + ((h + s) % 3) * 2;
          for (let k = 0; k < hh; k++) ctx.fillRect(bx + Math.round(Math.sin(time * 2 + tx + s) * k * 0.2), sy + 4 + hh - k, 1, 1);
        }
      } else if (dc === 2) {
        ctx.fillStyle = '#d9c9a8'; ctx.fillRect(sx + 6, sy + 11, 3, 2);
        ctx.fillStyle = '#fff4d6'; ctx.fillRect(sx + 7, sy + 11, 1, 1);
      }
      if (dc === 3) ctx.drawImage(SPR.clam, sx + 4, sy + 9);
      if (t === TILE.CHEST) ctx.drawImage(SPR.chest, sx + 2, sy + 5);
      if (t === TILE.LAMP) {
        ctx.fillStyle = '#4a5a70'; ctx.fillRect(sx + 7, sy + 6, 2, 8);
        ctx.fillStyle = '#ffd86b'; ctx.fillRect(sx + 5, sy + 3, 6, 4);
        ctx.fillStyle = '#fff4c0'; ctx.fillRect(sx + 6, sy + 4, 2, 2);
      } else if (t === TILE.VENT) {
        ctx.fillStyle = '#5b6b80'; ctx.fillRect(sx + 3, sy + 10, 10, 4);
        ctx.fillStyle = '#7fe3ff'; ctx.fillRect(sx + 4, sy + 11, 8, 1);
        ctx.fillStyle = '#9fe8ff';
        for (let k = 0; k < 3; k++) { const ph = (time * 1.2 + k * 0.33) % 1; ctx.fillRect(sx + 5 + k * 3, sy + 10 - Math.floor(ph * 18), 1, 1); }
      }
    }
  }

  // 비콘
  const bsx = BX - cx, bsy = BY - cy;
  if (bsx > -120 && bsx < VW + 120 && bsy > -120 && bsy < VH + 120) {
    ctx.fillStyle = 'rgba(127,227,255,0.35)';
    for (let a = 0; a < 6.28; a += 0.1) ctx.fillRect(Math.round(bsx + Math.cos(a) * TUNE.beaconRadius), Math.round(bsy + Math.sin(a) * TUNE.beaconRadius), 1, 1);
    ctx.fillStyle = '#3b4a63'; ctx.fillRect(bsx - 6, bsy + 4, 12, 6);
    ctx.fillStyle = '#5b6b80'; ctx.fillRect(bsx - 3, bsy - 8, 6, 14);
    ctx.fillStyle = '#7fe3ff'; ctx.fillRect(bsx - 4, bsy - 14, 8, 7);
    ctx.fillStyle = '#e8fbff'; ctx.fillRect(bsx - 2, bsy - 12, 3, 3);
  }
  // 제단
  if (!boss.dead && !boss.active) {
    const asx = AX - cx, asy = AY - cy;
    if (asx > -30 && asx < VW + 30 && asy > -30 && asy < VH + 30) {
      ctx.fillStyle = '#35203a'; ctx.fillRect(asx - 8, asy + 4, 16, 6);
      ctx.fillStyle = Math.sin(time * 3) > 0 ? '#ff6b6b' : '#c04040';
      ctx.fillRect(asx - 3, asy - 10, 6, 14); ctx.fillRect(asx - 1, asy - 14, 2, 4);
    }
  }

  // 파티클(아래층)
  for (const p of particles) {
    ctx.globalAlpha = Math.min(1, p.life / p.max * 1.5);
    ctx.fillStyle = p.color; ctx.fillRect(Math.round(p.x - cx), Math.round(p.y - cy), p.size, p.size);
  }
  ctx.globalAlpha = 1;

  // 적
  for (const e of enemies) {
    const sx = Math.round(e.x - cx), sy = Math.round(e.y - cy);
    if (sx < -20 || sx > VW + 20 || sy < -20 || sy > VH + 20) continue;
    let spr;
    if (e.type === 'jelly') spr = SPR.jelly[Math.floor(e.t * 3) % 2];
    else spr = SPR[e.type];
    const bobY = e.type === 'urchin' ? 0 : Math.round(Math.sin(e.t * 3) * 1);
    ctx.save();
    if (e.type === 'angler' || e.type === 'eel') {
      e.vx = e.lunge > 0 ? e.lx : (P.x - e.x);
      ctx.translate(sx, sy + bobY);
      if (e.vx > 0) ctx.scale(-1, 1);
      ctx.drawImage(spr, -spr.width / 2, e.type === 'eel' ? -3 : -5);
    } else ctx.drawImage(spr, sx - spr.width / 2, sy - spr.height / 2 + bobY);
    ctx.restore();
    if (e.type === 'angler') {
      const lx = sx + (e.vx > 0 ? 8 : -8), ly = sy - 9;
      ctx.fillStyle = '#ffe36b'; disc(ctx, lx, ly, 2); ctx.fillStyle = '#ffffff'; ctx.fillRect(lx, ly - 1, 1, 1);
      ctx.fillStyle = '#2d3f6b'; ctx.fillRect(Math.round((sx + lx) / 2), Math.round((sy - 4 + ly) / 2), 1, 1);
    }
    if (e.wind > 0 && Math.floor(e.t * 16) % 2) { ctx.fillStyle = 'rgba(255,255,100,0.6)'; ctx.fillRect(sx - 8, sy - 3, 16, 6); }
    if (e.flash > 0) { ctx.fillStyle = 'rgba(255,255,255,0.7)'; ctx.fillRect(sx - 5, sy - 5, 10, 10); }
  }

  // 보스
  if (boss.active) {
    const bx = Math.round(boss.x - cx), by = Math.round(boss.y - cy);
    ctx.fillStyle = '#6b2d8f';
    for (let k = 0; k < 8; k++) {
      const a = boss.t * 0.8 + k * Math.PI / 4;
      for (let s = 12; s <= 22; s += 2) ctx.fillRect(Math.round(bx + Math.cos(a) * s), Math.round(by + Math.sin(a) * s), 2, 2);
    }
    ctx.fillStyle = boss.flash > 0 ? '#ffffff' : '#e8d8f0'; disc(ctx, bx, by, 14);
    const dx = P.x - boss.x, dy = P.y - boss.y, dl = Math.hypot(dx, dy) || 1;
    const ix = bx + dx / dl * 4, iy = by + dy / dl * 4;
    ctx.fillStyle = '#c0392b'; disc(ctx, ix, iy, 7);
    ctx.fillStyle = '#1a0a0a'; disc(ctx, ix + dx / dl * 1, iy + dy / dl * 1, 3);
    ctx.fillStyle = '#ffffff'; ctx.fillRect(Math.round(ix) - 2, Math.round(iy) - 2, 2, 2);
  }
  // 탄
  for (const b of bullets) {
    const sx = Math.round(b.x - cx), sy = Math.round(b.y - cy);
    ctx.fillStyle = '#ff6b6b'; disc(ctx, sx, sy, 3); ctx.fillStyle = '#ffe0e0'; ctx.fillRect(sx - 1, sy - 1, 2, 2);
  }

  // 플레이어
  {
    const sx = Math.round(P.x - cx), sy = Math.round(P.y - cy + Math.sin(P.bob) * 1);
    ctx.save(); ctx.translate(sx, sy);
    if (P.inv > 0 && Math.floor(P.inv * 20) % 2 === 0) ctx.globalAlpha = 0.4;
    if (P.face < 0) ctx.scale(-1, 1);
    ctx.drawImage(SPR.player, -6, -7);
    ctx.restore(); ctx.globalAlpha = 1;
    // 작살 휘두르기
    if (P.swing > 0) {
      const a = P.swingAng, k = P.swing / 0.16;
      ctx.fillStyle = `rgba(255,255,255,${0.5 + k * 0.4})`;
      for (let s = 8; s <= 28; s += 2) {
        const aa = a + (k - 0.5) * 0.8;
        ctx.fillRect(Math.round(sx + Math.cos(aa) * s), Math.round(sy + Math.sin(aa) * s), 2, 2);
      }
    }
  }

  // 커서
  {   // 타깃 표시: 코너 브래킷 (닿으면 흰색이 0.5초 주기로 1px 맥동, 안 닿으면 빨강 + X)
    const sx = cursor.tx * TS - cx, sy = cursor.ty * TS - cy, L = 4;
    const o = cursor.reach ? (Math.floor(time * 2) % 2) : 0;
    const x0 = sx - o, y0 = sy - o, x1 = sx + TS + o, y1 = sy + TS + o;
    const bars = [[x0, y0, L, 1], [x0, y0, 1, L], [x1 - L, y0, L, 1], [x1 - 1, y0, 1, L],
      [x0, y1 - 1, L, 1], [x0, y1 - L, 1, L], [x1 - L, y1 - 1, L, 1], [x1 - 1, y1 - L, 1, L]];
    ctx.fillStyle = '#0a1626'; for (const [x, y, w, h] of bars) ctx.fillRect(x - 1, y - 1, w + 2, h + 2);
    ctx.fillStyle = cursor.reach ? '#ffffff' : '#ff6b6b'; for (const [x, y, w, h] of bars) ctx.fillRect(x, y, w, h);
    if (!cursor.reach) for (let k = -3; k <= 3; k++) { ctx.fillRect(sx + 8 + k, sy + 8 + k, 1, 1); ctx.fillRect(sx + 8 + k, sy + 8 - k, 1, 1); }
  }

  // 조명
  renderLight(cx, cy);
  ctx.drawImage(lightCv, 0, 0);

  // 피격 비네팅
  if (P.inv > 0.6) { ctx.fillStyle = 'rgba(255,40,40,0.18)'; ctx.fillRect(0, 0, VW, VH); }
  if (P.o2 < 10 && P.o2 >= 0 && !(Math.hypot(P.x - BX, P.y - BY) < TUNE.beaconRadius)) {
    ctx.fillStyle = `rgba(0,30,60,${0.25 + Math.sin(time * 8) * 0.1})`; ctx.fillRect(0, 0, VW, VH);
  }
  drawMini();
}

// ---------- HUD ----------
let hudDirty = true, hudT = 0;
const hpFill = document.getElementById('hpfill'), o2Fill = document.getElementById('o2fill');
const hpTxt = document.getElementById('hptxt'), o2Txt = document.getElementById('o2txt');
const invEl = document.getElementById('inv'), craftEl = document.getElementById('craft'), zoneEl = document.getElementById('zone');
const ZONE_NAME = ['얕은 폐허', '가라앉은 거리', '심연'];
function costStr(cost) {
  return Object.keys(cost).map(k => `<span class="${inv[k] >= cost[k] ? 'ok' : 'no'}">${ITEM_NAME[k]} ${inv[k] || 0}/${cost[k]}</span>`).join(' ');
}
function canAfford(cost) { return Object.keys(cost).every(k => (inv[k] || 0) >= cost[k]); }
function updateHud() {
  hpFill.style.width = `${P.hp / maxHp() * 100}%`; hpTxt.textContent = `${Math.ceil(P.hp)}/${maxHp()}`;
  o2Fill.style.width = `${P.o2 / maxO2() * 100}%`; o2Txt.textContent = `${Math.ceil(P.o2)}/${maxO2()}`;
  // 색만으로 경고하지 않도록: 낮으면 색 + 깜빡이는 테두리 + 느낌표
  const lowO2 = P.o2 < 10, lowHp = P.hp <= 3;
  o2Fill.style.background = lowO2 ? '#ff6b6b' : '#3aa7e8';
  $('o2bar').classList.toggle('low', lowO2); $('hpbar').classList.toggle('low', lowHp);
  const zn = zoneOfPx(P.x, P.y);
  zoneEl.textContent = `구역: ${ZONE_NAME[zn]}`;
  if (zn !== lastZone) { if (lastZone !== -1 && started) toast(`구역: ${ZONE_NAME[zn]}`, 2); lastZone = zn; }
  invEl.innerHTML = ['scrap', 'brass', 'crystal', 'pearl', 'gel'].map(k =>
    `<div class="chip" title="${ITEM_NAME[k]}"><img class="i12" src="assets/${k}.png" alt="${ITEM_NAME[k]}"><b>${inv[k]}</b></div>`).join('');
  const near = nearBeacon();
  let html = `<div class="ch">제작 ${near ? '' : '<small>(기지 비콘 근처에서만)</small>'}</div>`;
  CRAFTS.forEach((c, i) => {
    if (c.kind === 'upgrade') {
      const track = UP[c.id], lv = P.lvl[c.id], next = track[lv + 1];
      html += next
        ? `<div class="${near && canAfford(next.cost) ? 'can' : ''}">[${i + 1}] ${next.name} ${costStr(next.cost)}</div>`
        : `<div class="max">[${i + 1}] ${track[lv].name} (MAX)</div>`;
    } else html += `<div class="${near && canAfford(c.cost) ? 'can' : ''}">[${i + 1}] ${c.label} ${costStr(c.cost)}</div>`;
  });
  if (!touchMode) craftEl.innerHTML = html;
  updateMobileHud(near);
}
function tryCraft(i) {
  const c = CRAFTS[i]; if (!c) return;
  if (!nearBeacon()) return toast('기지 비콘 근처에서만 제작할 수 있다', 1.6);
  if (c.kind === 'upgrade') {
    const track = UP[c.id], next = track[P.lvl[c.id] + 1];
    if (!next) return toast('이미 최대 등급이다', 1.2);
    if (!canAfford(next.cost)) return toast('재료가 부족하다', 1.2);
    for (const k in next.cost) inv[k] -= next.cost[k];
    P.lvl[c.id]++; toast(`${next.name} 제작 완료!`);
    if (c.id === 'tank') P.o2 = maxO2();
    if (c.id === 'armor') P.hp = maxHp();
  } else {
    if (!canAfford(c.cost)) return toast('재료가 부족하다', 1.2);
    for (const k in c.cost) inv[k] -= c.cost[k];
    for (const k in c.give) addItem(k, c.give[k]);
    toast(`${c.label} 제작 완료`);
  }
  SFX.craft(); buzz(15); saveGame();
  hudDirty = true;
}

// ---------- 루프 ----------
function startGame() {
  if (started) return;
  started = true; audioInit(); enterFullscreenIfTouch();
  document.getElementById('start').style.display = 'none';
  toast(touchMode ? '왼쪽 화면을 끌어서 이동하세요' : '비콘 주변에서 산소가 채워진다. 암반을 파서 고철을 모아보자', 4);
}
function update(dt) {
  time += dt;
  if (hintCd > 0) hintCd -= dt;
  if (toastTimer > 0) { toastTimer -= dt; if (toastTimer <= 0) toastEl.style.opacity = 0; }
  if (cam.shake > 0) cam.shake -= dt;
  if (!started) return;
  if (paused) { if (hudDirty) { updateHud(); hudDirty = false; } return; }
  if (touchMode) touchSynth();
  if (pressed.KeyM) { muted = !muted; toast(muted ? '소리 끔' : '소리 켬', 1); }
  saveT -= dt; if (saveT <= 0) { saveT = 10; saveGame(); }
  for (let i = 0; i < CRAFTS.length; i++) if (pressed['Digit' + (i + 1)]) tryCraft(i);
  updatePlayer(dt); updateMining(dt); updateCombat(dt);
  updateEnemies(dt); updateBoss(dt); updateBullets(dt); updateParticles(dt); spawner(dt);
  explore();
  hudT -= dt; if (hudT <= 0 || hudDirty) { updateHud(); hudT = 0.15; hudDirty = false; }
}
let last = performance.now();
function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000); last = now;
  update(dt); render();
  for (const k in pressed) delete pressed[k];
  requestAnimationFrame(frame);
}
function fit() {
  const aw = innerWidth, ah = innerHeight;
  const newVW = clamp(Math.round(VH * aw / ah / 2) * 2, 288, 428);   // 높이 180 고정, 폭은 화면 비율에 맞춤
  if (newVW !== VW) { VW = newVW; canvas.width = VW; lightCv.width = VW; }
  const sc = Math.min(aw / VW, ah / VH);
  canvas.style.width = VW * sc + 'px'; canvas.style.height = VH * sc + 'px';
  const rot = document.getElementById('rotate');
  const portrait = touchMode && ah > aw;
  if (rot) rot.style.display = portrait ? 'flex' : 'none';                  // 세로로 들면 회전 안내
  if (portrait && started && !menuOpen) openMenu();                         // 플레이 중이면 자동 일시정지
}
addEventListener('resize', fit); fit();
// ---------- 모바일 입력 / UI ----------
const $ = id => document.getElementById(id);
let sheetSel = -1, hintStep = 0, lastZone = -1, curActIcon = 'pick';
const CRAFT_ICON = { pick: 'pick', harpoon: 'harpoon', light: 'lamp', tank: 'tank', fins: 'fins', armor: 'armor', lamp: 'lamp', vent: 'vent' };
const touch = {
  joy: { id: null, ox: 0, oy: 0, x: 0, y: 0, active: false }, jx: 0, jy: 0, face: { x: 1, y: 0 },
  act: false, tap: { id: null, active: false, vx: 0, vy: 0 }, press: { lamp: false, vent: false, heal: false },
};
const joyEl = $('joy'), thumbEl = $('joythumb'), joyHint = $('joyhint');
const joyR = () => clamp(innerHeight * 0.12, 36, 64);
let hasGesture = false;                        // 브라우저는 실제 터치 전 진동을 막고 경고를 남긴다
addEventListener('pointerdown', e => { if (e.isTrusted) hasGesture = true; }, true);
function buzz(ms) { if (touchMode && hasGesture && navigator.vibrate) { try { navigator.vibrate(ms); } catch (e) { /* 미지원 */ } } }
function setTouchMode(v) { touchMode = v; document.body.classList.toggle('touch', v); fit(); }
function viewXY(cx, cy) { const r = canvas.getBoundingClientRect(); return [(cx - r.left) * VW / r.width, (cy - r.top) * VH / r.height]; }
function capture(el, e) { try { el.setPointerCapture(e.pointerId); } catch (err) { /* 합성 이벤트 등 */ } }

// 터치 기기 감지: 거친 포인터만 있거나, 첫 터치가 들어오면 전환 (?touch=1 로 강제)
if (params.get('touch') === '1' || (matchMedia('(pointer: coarse)').matches && !matchMedia('(pointer: fine)').matches)) setTouchMode(true);
addEventListener('pointerdown', e => { if (e.pointerType === 'touch' && !touchMode) setTouchMode(true); }, true);

// 왼손 모드
let lefty = false;
try { lefty = localStorage.getItem('abyss_lefty') === '1'; } catch (e) { /* 무시 */ }
document.body.classList.toggle('lefty', lefty);

// --- 플로팅 조이스틱 ---
const joyZone = $('joyzone');
joyZone.addEventListener('pointerdown', e => {
  if (!started) { startGame(); return; }
  if (touch.joy.id !== null || paused) return;
  e.preventDefault(); capture(joyZone, e);
  const j = touch.joy; j.id = e.pointerId; j.ox = j.x = e.clientX; j.oy = j.y = e.clientY; j.active = true;
  joyEl.style.display = 'block'; joyEl.style.left = e.clientX + 'px'; joyEl.style.top = e.clientY + 'px';
  thumbEl.style.transform = 'translate(-50%,-50%)'; joyHint.classList.add('off');
});
joyZone.addEventListener('pointermove', e => {
  const j = touch.joy; if (e.pointerId !== j.id) return;
  j.x = e.clientX; j.y = e.clientY;
  const dx = j.x - j.ox, dy = j.y - j.oy, len = Math.hypot(dx, dy) || 1, R = joyR(), k = len > R ? R / len : 1;
  thumbEl.style.transform = `translate(calc(-50% + ${dx * k}px), calc(-50% + ${dy * k}px))`;
});
function joyEnd(e) {
  const j = touch.joy; if (e && e.pointerId !== j.id) return;
  j.id = null; j.active = false; joyEl.style.display = 'none'; joyHint.classList.remove('off');
}
joyZone.addEventListener('pointerup', joyEnd); joyZone.addEventListener('pointercancel', joyEnd);

// --- 월드 터치 (정밀 채굴) ---
const tapZone = $('tapzone');
tapZone.addEventListener('pointerdown', e => {
  if (!started) { startGame(); return; }
  if (paused || touch.tap.id !== null) return;
  e.preventDefault(); capture(tapZone, e);
  const [vx, vy] = viewXY(e.clientX, e.clientY);
  Object.assign(touch.tap, { id: e.pointerId, active: true, vx, vy });
});
tapZone.addEventListener('pointermove', e => { if (e.pointerId === touch.tap.id) { const [vx, vy] = viewXY(e.clientX, e.clientY); touch.tap.vx = vx; touch.tap.vy = vy; } });
function tapEnd(e) { if (e && e.pointerId !== touch.tap.id) return; touch.tap.id = null; touch.tap.active = false; }
tapZone.addEventListener('pointerup', tapEnd); tapZone.addEventListener('pointercancel', tapEnd);

// --- 버튼 ---
function holdButton(el, onDown, onUp) {
  el.addEventListener('pointerdown', e => { e.preventDefault(); e.stopPropagation(); capture(el, e); el.classList.add('down'); onDown(); });
  const up = () => { el.classList.remove('down'); if (onUp) onUp(); };
  el.addEventListener('pointerup', up); el.addEventListener('pointercancel', up); el.addEventListener('pointerleave', up);
}
holdButton($('bAct'), () => { if (!started) startGame(); else touch.act = true; }, () => { touch.act = false; });
holdButton($('bLamp'), () => { touch.press.lamp = true; });
holdButton($('bVent'), () => { touch.press.vent = true; });
holdButton($('bHeal'), () => { touch.press.heal = true; });

// --- 자동 조준 ---
function nearestTarget() {
  let best = null, bd = 1e9;
  const list = boss.active ? enemies.concat([boss]) : enemies;
  for (const e of list) {
    const er = e === boss ? 14 : ENEMY_DEF[e.type].r, d = Math.hypot(e.x - P.x, e.y - P.y) - er;
    if (d < bd) { bd = d; best = e; }
  }
  return bd <= 30 ? best : null;
}
// 바라보는 방향으로 광선을 쏴 처음 만나는 벽/광맥/상자/조개를 찾는다 (램프·환기구는 제외)
function frontTile() {
  for (let d = 6; d <= 32; d += 4) {
    const tx = Math.floor((P.x + touch.face.x * d) / TS), ty = Math.floor((P.y + touch.face.y * d) / TS);
    if (!inb(tx, ty)) break;
    const i = idx(tx, ty), t = tiles[i];
    if (TILE_DEF[t].solid || t === TILE.CHEST || (t === TILE.FLOOR && deco[i] === 3)) return [tx, ty];
  }
  return null;
}
// 터치 입력을 기존 마우스/키 입력으로 변환 (게임 로직은 한 벌만 유지)
function setActIcon(name) {                            // 가까운 적이 있으면 작살, 없으면 곡괭이로 행동 버튼 아이콘 전환
  if (name === curActIcon) return;
  curActIcon = name; $('actIcon').src = `assets/${name}.png`;
}
function touchSynth() {
  const j = touch.joy; let jx = 0, jy = 0;
  if (j.active) {
    const dx = j.x - j.ox, dy = j.y - j.oy, len = Math.hypot(dx, dy) || 1, mag = Math.min(1, len / joyR());
    if (mag > 0.18) { jx = dx / len * mag; jy = dy / len * mag; touch.face.x = dx / len; touch.face.y = dy / len; }
  }
  touch.jx = jx; touch.jy = jy;
  setActIcon(nearestTarget() ? 'harpoon' : 'pick');
  // 첫 플레이 힌트: 이동 -> 행동 버튼 순서로 한 번씩만
  if (hintStep === 0 && (jx || jy)) { hintStep = 1; toast('오른쪽 아래 큰 버튼을 꾹 눌러 채굴하세요', 4); }
  else if (hintStep === 1 && touch.act) hintStep = 2;
  let ax = P.x + touch.face.x * TS * 1.2, ay = P.y + touch.face.y * TS * 1.2, useTap = false;
  mouse.l = false; mouse.r = false;
  if (touch.tap.active) { mouse.x = touch.tap.vx; mouse.y = touch.tap.vy; mouse.l = true; useTap = true; }
  else if (touch.act) {
    const e = nearestTarget();
    if (e) {
      ax = e.x; ay = e.y; mouse.r = true;
      const dl = Math.hypot(e.x - P.x, e.y - P.y) || 1; touch.face.x = (e.x - P.x) / dl; touch.face.y = (e.y - P.y) / dl;
    } else { const t = frontTile(); if (t) { ax = t[0] * TS + 8; ay = t[1] * TS + 8; mouse.l = true; } }
  }
  if (touch.press.lamp || touch.press.vent) {            // 설치: 앞 칸이 바닥이면 앞, 아니면 내 칸
    let tx = Math.floor((P.x + touch.face.x * TS) / TS), ty = Math.floor((P.y + touch.face.y * TS) / TS);
    if (tileAt(tx, ty) !== TILE.FLOOR) { tx = Math.floor(P.x / TS); ty = Math.floor(P.y / TS); }
    ax = tx * TS + 8; ay = ty * TS + 8; useTap = false; mouse.l = false;
    if (touch.press.lamp) pressed.KeyQ = true;
    if (touch.press.vent) pressed.KeyE = true;
  }
  if (touch.press.heal) pressed.KeyF = true;
  touch.press.lamp = touch.press.vent = touch.press.heal = false;
  if (!useTap) { mouse.x = ax - cam.x; mouse.y = ay - cam.y; }
}

// --- 제작 시트 / 메뉴 / 일시정지 ---
function effectText(id, lv) {
  const o = UP[id] && UP[id][lv]; if (!o) return '';
  return { pick: `티어${o.tier}·속도${o.power}`, harpoon: `공격${o.dmg}`, light: `시야${o.r}`, tank: `산소${o.max}`, fins: `이동${o.speed}`, armor: `체력${o.hp}` }[id];
}
function renderSheet() {
  let h = '';
  CRAFTS.forEach((c, i) => {
    let title, eff, cost, maxed = false;
    if (c.kind === 'upgrade') {
      const lv = P.lvl[c.id], nx = UP[c.id][lv + 1];
      if (!nx) { title = UP[c.id][lv].name; eff = effectText(c.id, lv); maxed = true; }
      else { title = nx.name; eff = c.id === 'pick'
        ? `티어${UP.pick[lv].tier}→${nx.tier}·속도${UP.pick[lv].power}→${nx.power}`          // 곡괭이만 압축 표기(말줄임 방지)
        : `${effectText(c.id, lv)}→${effectText(c.id, lv + 1)}`;
      cost = nx.cost; }
    } else { title = c.label; eff = c.id === 'lamp' ? '어둠을 밝힌다' : '산소 충전 거점'; cost = c.cost; }
    const ok = !maxed && canAfford(cost);
    const sel = (sheetSel === i && c.kind === 'upgrade' && !maxed) ? ' sel' : '';
    const st = maxed ? 'max' : ok ? 'can' : 'lack';
    const just = (sheetJust === i) ? ' just' : '';
    h += `<div class="card fr ${st}${sel}${just}" data-i="${i}"><img class="ci" src="assets/${CRAFT_ICON[c.id]}.png" alt="">`
      + `<div class="tx"><div class="t"><span class="n">${title}</span><span class="e">${eff}</span></div>`
      + `<div class="c">${maxed ? '<span class="cc ok"><b>최대 등급</b></span>' : costChips(cost)}</div></div>`
      + `<div class="go" data-go="1">제작</div></div>`;
  });
  sheetJust = -1;
  $('sheetBody').innerHTML = h;
}
let sheetJust = -1;
// 재료 칩: 아이콘 + 보유/필요. 부족한 재료만 빨강 (색 + 숫자 비교로 읽을 수 있게)
function costChips(cost) {
  return Object.keys(cost).map(k => {
    const have = inv[k] || 0, need = cost[k];
    return `<span class="cc ${have >= need ? 'ok' : 'no'}"><img class="i12" src="assets/${k}.png" alt="${ITEM_NAME[k]}"><b>${have}/${need}</b></span>`;
  }).join('');
}
function updateMobileHud(near) {
  $('cntLamp').textContent = inv.lamp; $('cntVent').textContent = inv.vent;
  $('cntGel').textContent = `x${Math.floor(inv.gel / 2)}`;              // 회복 가능 횟수 (젤 2개 = 1회)
  $('bLamp').classList.toggle('zero', inv.lamp <= 0); $('bVent').classList.toggle('zero', inv.vent <= 0);
  $('bHeal').classList.toggle('zero', inv.gel < 2 || P.hp >= maxHp());
  if (!touchMode) return;
  const can = CRAFTS.some(c => { const nx = c.kind === 'upgrade' ? UP[c.id][P.lvl[c.id] + 1] : c; return nx && nx.cost && canAfford(nx.cost); });
  const bc = $('bCraft');
  bc.classList.toggle('show', near && !sheetOpen && !menuOpen); bc.classList.toggle('pulse', can);
  if (sheetOpen) { if (!near) closeSheet(); else renderSheet(); }
}
function syncPause() {
  paused = menuOpen || sheetOpen;
  if (paused) {
    joyEnd(); tapEnd(); touch.act = false; mouse.l = mouse.r = false;
    for (const k in keys) keys[k] = false;
    $('bAct').classList.remove('down');
  }
}
function openSheet() { if (!started || menuOpen) return; sheetOpen = true; sheetSel = -1; $('sheetMsg').textContent = ''; $('sheet').style.display = 'flex'; syncPause(); updateHud(); }
function closeSheet() { sheetOpen = false; $('sheet').style.display = 'none'; syncPause(); }
function openMenu() { if (!started) return; if (sheetOpen) closeSheet(); menuOpen = true; $('menu').style.display = 'flex'; syncPause(); updateMenuLabels(); }
function closeMenu() { menuOpen = false; $('menu').style.display = 'none'; $('help').style.display = 'none'; syncPause(); }
$('mHelp').addEventListener('click', () => { $('help').style.display = 'flex'; });
$('helpClose').addEventListener('click', () => { $('help').style.display = 'none'; });
function updateMenuLabels() {
  $('mSound').textContent = `소리: ${muted ? '꺼짐' : '켜짐'}`;
  $('mLefty').textContent = `왼손 모드: ${lefty ? '켜짐' : '꺼짐'}`;
  $('mNew').textContent = '새 게임';
}
function toggleFullscreen() {
  try {
    if (document.fullscreenElement) document.exitFullscreen();
    else { const el = document.documentElement; (el.requestFullscreen || el.webkitRequestFullscreen).call(el); }
  } catch (e) { toast('이 브라우저는 전체화면을 지원하지 않는다', 2); }
}
function enterFullscreenIfTouch() {
  if (!touchMode || params.get('nofs') === '1' || document.fullscreenElement) return;
  try {
    const el = document.documentElement, p = (el.requestFullscreen || el.webkitRequestFullscreen).call(el);
    Promise.resolve(p).then(() => screen.orientation && screen.orientation.lock && screen.orientation.lock('landscape')).catch(() => {});
  } catch (e) { /* iOS 사파리 등 미지원 */ }
}
$('start').addEventListener('pointerdown', e => { if (e.pointerType === 'touch') e.preventDefault(); startGame(); });
$('bCraft').addEventListener('click', openSheet);
$('sheetClose').addEventListener('click', closeSheet);
// 업그레이드는 되돌릴 수 없으므로 두 단계: 카드 선택 -> 카드 안의 '제작' 버튼. 소모품(램프·환기구)은 한 번에.
$('sheetBody').addEventListener('click', e => {
  const c = e.target.closest('.card'); if (!c) return;
  const i = Number(c.dataset.i), cr = CRAFTS[i];
  if (c.classList.contains('max')) return;
  if (cr.kind === 'item') { sheetJust = i; tryCraft(i); return; }
  if (sheetSel !== i) { sheetSel = i; renderSheet(); return; }
  if (e.target.closest('[data-go]') && !c.classList.contains('lack')) { sheetJust = i; sheetSel = -1; tryCraft(i); }
  else if (c.classList.contains('lack')) toast('재료가 부족하다', 1.4);
});
$('bMenu').addEventListener('click', () => { if (!started) return; menuOpen ? closeMenu() : openMenu(); });
$('mContinue').addEventListener('click', closeMenu);
$('mSound').addEventListener('click', () => { muted = !muted; updateMenuLabels(); });
$('mFull').addEventListener('click', toggleFullscreen);
$('mLefty').addEventListener('click', () => {
  lefty = !lefty; document.body.classList.toggle('lefty', lefty);
  try { localStorage.setItem('abyss_lefty', lefty ? '1' : '0'); } catch (e) { /* 무시 */ }
  updateMenuLabels();
});
let newArmed = false;
$('mNew').addEventListener('click', () => {
  if (!newArmed) { newArmed = true; $('mNew').textContent = '정말 삭제하고 새로 시작? (한 번 더)'; setTimeout(() => { newArmed = false; updateMenuLabels(); }, 3000); return; }
  try { localStorage.removeItem(SAVE_KEY); } catch (e) { /* 무시 */ }
  window.removeEventListener('beforeunload', saveGame); location.href = location.pathname;
});
$('mini').addEventListener('click', () => $('hudTR').classList.toggle('big'));
addEventListener('keydown', e => { if (e.code === 'Escape' && started) { menuOpen ? closeMenu() : openMenu(); } });
// 앱 전환/탭 숨김: 즉시 저장하고 메뉴를 띄워 일시정지 (모바일 브라우저는 beforeunload 가 불안정)
document.addEventListener('visibilitychange', () => { if (document.hidden) { saveGame(); if (started && !menuOpen) openMenu(); } });

// ---------- 저장 / 불러오기 ----------
let saveT = 10;
function b64(u8) { let s = ''; for (let i = 0; i < u8.length; i += 8192) s += String.fromCharCode.apply(null, u8.subarray(i, i + 8192)); return btoa(s); }
function unb64(s, u8) { const b = atob(s); for (let i = 0; i < b.length && i < u8.length; i++) u8[i] = b.charCodeAt(i); }
function saveGame() {
  if (!AUTOSAVE) return;
  try {
    localStorage.setItem(SAVE_KEY, JSON.stringify({
      seed: SEED, tiles: b64(tiles), deco: b64(deco), explored: b64(explored),
      P: { x: P.x, y: P.y, hp: P.hp, o2: P.o2, lvl: P.lvl }, inv, bossDead: boss.dead,
    }));
  } catch (e) { /* 저장 불가 환경이면 조용히 무시 */ }
}
function applySavedState() {
  try {
    unb64(SAVED.tiles, tiles); unb64(SAVED.deco, deco); unb64(SAVED.explored, explored);
    Object.assign(P, { x: SAVED.P.x, y: SAVED.P.y, hp: SAVED.P.hp, o2: SAVED.P.o2 });
    Object.assign(P.lvl, SAVED.P.lvl); Object.assign(inv, SAVED.inv); boss.dead = !!SAVED.bossDead;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (explored[idx(x, y)]) miniPixel(x, y);
    const info = document.getElementById('saveinfo');
    if (info) info.innerHTML = '저장된 게임을 이어합니다 · <b>N</b> 키: 새 게임';
  } catch (e) { console.warn('save load failed', e); }
}
if (SAVED) applySavedState();
addEventListener('beforeunload', saveGame);
window.__dbg = { P, inv, enemies, boss, tiles, TILE, get cursor() { return cursor; }, SEED };
updateHud();
requestAnimationFrame(frame);
