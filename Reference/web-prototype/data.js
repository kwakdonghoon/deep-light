'use strict';
// ============================================================
// 데이터 테이블 — Unity 이식 시 ScriptableObject / JSON 으로 옮기는 부분
// ============================================================

const TILE = { FLOOR: 0, ROCK: 1, HARD: 2, DEEP: 3, SCRAP: 4, BRASS: 5, CRYSTAL: 6, BEDROCK: 7, LAMP: 8, VENT: 9, CHEST: 10 };

// hp: 파괴에 필요한 누적 피해, tier: 필요한 곡괭이 등급
const TILE_DEF = {
  [TILE.FLOOR]:   { name: '해저', solid: false, mini: '#16486b' },
  [TILE.ROCK]:    { name: '암반', solid: true, hp: 3, tier: 0, base: ['#3d527c', '#4b6590', '#2f4062'], mini: '#2a4060' },
  [TILE.HARD]:    { name: '경암', solid: true, hp: 6, tier: 1, base: ['#54447d', '#665293', '#42356a'], mini: '#3a2f58' },
  [TILE.DEEP]:    { name: '심암', solid: true, hp: 10, tier: 2, base: ['#6e3b6c', '#834c80', '#552d53'], mini: '#4f2c4d' },
  [TILE.SCRAP]:   { name: '고철 광맥', solid: true, hp: 4, tier: 0, base: ['#3d527c', '#4b6590', '#2f4062'], ore: ['#e0904a', '#ffd3a0'], drop: { scrap: [1, 2] }, mini: '#e0904a' },
  [TILE.BRASS]:   { name: '황동 광맥', solid: true, hp: 7, tier: 1, base: ['#54447d', '#665293', '#42356a'], ore: ['#f2c14e', '#fff0a8'], drop: { brass: [1, 2] }, mini: '#f2c14e' },
  [TILE.CRYSTAL]: { name: '심해 수정', solid: true, hp: 11, tier: 2, base: ['#6e3b6c', '#834c80', '#552d53'], ore: ['#4ff0e0', '#d8fffa'], drop: { crystal: [1, 2] }, glow: 16, mini: '#4ff0e0' },
  [TILE.BEDROCK]: { name: '기반암', solid: true, unbreakable: true, base: ['#0d1220', '#121828', '#0a0e19'], mini: '#05080f' },
  [TILE.LAMP]:    { name: '램프', solid: false, mini: '#ffd86b' },
  [TILE.VENT]:    { name: '환기구', solid: false, mini: '#7fe3ff' },
  [TILE.CHEST]:   { name: '보물 상자', solid: false, mini: '#ffd86b' },
};

const ITEM_NAME = { scrap: '고철', brass: '황동', crystal: '수정', pearl: '진주', gel: '젤', lamp: '램프', vent: '환기구' };

const ITEM_COLOR = { scrap: '#e0904a', brass: '#f2c14e', crystal: '#4ff0e0', pearl: '#f4f0ff', gel: '#d98cff' };

// 보물 상자 보상 (구역별, [min,max])
const CHEST_LOOT = [
  { scrap: [3, 6], gel: [1, 3], pearl: [0, 1], lamp: [1, 1] },
  { brass: [3, 6], gel: [2, 4], pearl: [1, 2], vent: [0, 1] },
  { crystal: [2, 4], brass: [3, 5], pearl: [2, 3], gel: [3, 5] },
];

// 업그레이드 트랙 (0레벨은 기본 장비)
const UP = {
  pick: [
    { name: '고철 곡괭이', tier: 0, power: 3 },
    { name: '황동 곡괭이', tier: 1, power: 5, cost: { scrap: 14, gel: 5 } },
    { name: '수정 곡괭이', tier: 2, power: 8, cost: { brass: 20, gel: 8, pearl: 2 } },
  ],
  harpoon: [
    { name: '작살 Lv1', dmg: 1 },
    { name: '작살 Lv2', dmg: 2, cost: { scrap: 10, gel: 3 } },
    { name: '작살 Lv3', dmg: 3, cost: { brass: 14, gel: 5, pearl: 2 } },
    { name: '작살 Lv4', dmg: 5, cost: { crystal: 10, brass: 12, pearl: 3 } },
  ],
  light: [
    { name: '랜턴 Lv1', r: 48 },
    { name: '랜턴 Lv2', r: 68, cost: { scrap: 8, gel: 2 } },
    { name: '랜턴 Lv3', r: 90, cost: { brass: 12, gel: 4 } },
    { name: '랜턴 Lv4', r: 116, cost: { crystal: 8, brass: 10, pearl: 2 } },
  ],
  tank: [
    { name: '산소통 Lv1', max: 40 },
    { name: '산소통 Lv2', max: 65, cost: { scrap: 10, gel: 3 } },
    { name: '산소통 Lv3', max: 100, cost: { brass: 12, gel: 6, pearl: 2 } },
    { name: '산소통 Lv4', max: 150, cost: { crystal: 8, brass: 12, pearl: 3 } },
  ],
  fins: [
    { name: '추진 핀 Lv1', speed: 55 },
    { name: '추진 핀 Lv2', speed: 65, cost: { scrap: 12, pearl: 3 } },
    { name: '추진 핀 Lv3', speed: 76, cost: { brass: 14, pearl: 5 } },
    { name: '추진 핀 Lv4', speed: 90, cost: { crystal: 10, pearl: 8 } },
  ],
  armor: [
    { name: '방호복 Lv1', hp: 10 },
    { name: '방호복 Lv2', hp: 14, cost: { scrap: 12, gel: 5, pearl: 2 } },
    { name: '방호복 Lv3', hp: 18, cost: { brass: 14, gel: 8, pearl: 4 } },
    { name: '방호복 Lv4', hp: 24, cost: { crystal: 10, gel: 10, pearl: 6 } },
  ],
};

// 제작 메뉴 (숫자키 1~6)
const CRAFTS = [
  { id: 'pick', label: '곡괭이', kind: 'upgrade' },
  { id: 'harpoon', label: '작살', kind: 'upgrade' },
  { id: 'light', label: '랜턴', kind: 'upgrade' },
  { id: 'tank', label: '산소통', kind: 'upgrade' },
  { id: 'fins', label: '추진 핀', kind: 'upgrade' },
  { id: 'armor', label: '방호복', kind: 'upgrade' },
  { id: 'lamp', label: '램프 x2', kind: 'item', give: { lamp: 2 }, cost: { scrap: 2, gel: 1 } },
  { id: 'vent', label: '환기구', kind: 'item', give: { vent: 1 }, cost: { scrap: 8, gel: 3 } },
];

// 몬스터 (drop: [min,max])
const ENEMY_DEF = {
  jelly:  { name: '해파리', hp: 3, speed: 14, dmg: 1, r: 5, drop: { gel: [1, 2] }, minZone: 0, weight: 5 },
  angler: { name: '아귀', hp: 7, speed: 40, dmg: 2, r: 6, drop: { gel: [2, 3] }, minZone: 1, weight: 3 },
  urchin: { name: '성게', hp: 4, speed: 0, dmg: 1, r: 5, drop: { scrap: [1, 2] }, minZone: 0, weight: 3 },
  // armor: 받는 피해 감소(최소 1). 작살을 올려야 편해지는 단단한 적
  crab:   { name: '게', hp: 10, speed: 26, dmg: 2, r: 6, armor: 1, drop: { scrap: [2, 3], pearl: [0, 1] }, minZone: 1, weight: 2 },
  // 예고(번쩍임) 후 돌진하는 적. 깊은 구역 전용
  eel:    { name: '전기뱀장어', hp: 9, speed: 30, dmg: 2, r: 6, drop: { gel: [2, 3], crystal: [0, 1] }, minZone: 2, weight: 3 },
};

const BOSS_DEF = { name: '심연의 눈', hp: 140, contactDmg: 3, bulletDmg: 1 };

// 튜닝 값
const TUNE = {
  reach: 44,   // 이동속도·체력은 UP.fins / UP.armor 로 이동
  beaconRadius: 80, craftRadius: 112,
  o2DrainBase: 1.0, o2DrainPerZone: 0.3, o2Refill: 25, ventRadiusTiles: 3,
  deathLossRate: 0.3,
};
