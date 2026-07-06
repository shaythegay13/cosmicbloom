// Cosmic Bloom art build — reuses the Farm & Give procedural pixel-art
// library (style.mjs / sprites/crops.mjs / sprites/characters.mjs, copied
// verbatim into this folder so this repo has no build-time dependency on the
// old one) to render crop-stage and NPC sprites straight from this project's
// own GameData JSON. No atlases, no build.mjs coupling — one PNG per frame,
// loaded at runtime by Assets/Resources/Sprites/Generated/.
//
// Usage:  node Tools/art/build-cosmic.mjs
// Needs @napi-rs/canvas resolvable (see README note below / CLAUDE.md).
import { createCanvas } from '@napi-rs/canvas';
import { mkdirSync, writeFileSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

import { SCALE, TILE, shade } from './style.mjs';
import { cropSprite } from './sprites/crops.mjs';
import { characterSprite } from './sprites/characters.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = join(HERE, '../..');                       // D:\Unity\Cosmic Bloom
const GAMEDATA_DIR = join(PROJECT_ROOT, 'Assets/Resources/GameData');
const OUT_ROOT = join(PROJECT_ROOT, 'Assets/Resources/Sprites/Generated');
const CROPS_OUT = join(OUT_ROOT, 'Crops');
const NPCS_OUT = join(OUT_ROOT, 'Npcs');

function readJson(path) {
    return JSON.parse(readFileSync(path, 'utf8'));
}

function writePng(px, outPath) {
    const canvas = createCanvas(TILE * SCALE, TILE * SCALE);
    const ctx = canvas.getContext('2d');
    px.render(ctx, 0, 0);
    writeFileSync(outPath, canvas.toBuffer('image/png'));
}

// Map a 0-based game stage index (0..gameStages-1) to a 0..4 generator stage
// (0 = sprout, 4 = ready). Endpoints are pinned; interior stages are spread
// proportionally and rounded, then de-duplicated forward so the sequence
// stays non-decreasing (monotonic) even for stages counts that don't divide
// evenly into 5 (e.g. stages=3 -> [0,2,4], stages=4 -> [0,1,3,4],
// stages=6 -> [0,1,2,2,3,4]).
function stageMap(gameStages) {
    if (gameStages <= 1) return [4];
    const out = [];
    let prev = -1;
    for (let s = 0; s < gameStages; s++) {
        let g = Math.round((s / (gameStages - 1)) * 4);
        if (g < prev) g = prev;   // guard: keep monotonic non-decreasing
        out.push(g);
        prev = g;
    }
    out[0] = 0;
    out[out.length - 1] = 4;
    return out;
}

function buildCrops() {
    mkdirSync(CROPS_OUT, { recursive: true });
    const { crops } = readJson(join(GAMEDATA_DIR, 'crops.json'));
    let frameCount = 0;
    for (const crop of crops) {
        const cropAdapter = { archetype: crop.archetype, color: crop.color, leaf: crop.leafColor };
        const mapping = stageMap(crop.stages);
        for (let gameStage = 0; gameStage < crop.stages; gameStage++) {
            const genStage = mapping[gameStage];
            const px = cropSprite(cropAdapter, genStage);
            const outPath = join(CROPS_OUT, `${crop.id}_${gameStage}.png`);
            writePng(px, outPath);
            frameCount++;
        }
        console.log(`  crop/${crop.id}: stages=${crop.stages} -> generator stages [${mapping.join(',')}]`);
    }
    console.log(`crops: ${crops.length} crops, ${frameCount} frames written to ${CROPS_OUT}`);
}

function buildNpcs() {
    mkdirSync(NPCS_OUT, { recursive: true });
    const { npcs } = readJson(join(GAMEDATA_DIR, 'npcs.json'));
    for (const npc of npcs) {
        const body = npc.palette && npc.palette.body ? npc.palette.body : '#cccccc';
        const accent = npc.palette && npc.palette.accent ? npc.palette.accent : '#888888';
        const chAdapter = {
            skin: shade(body, 0.35),
            hair: accent,
            outfit: body,
            hat: 'none',
        };
        const px = characterSprite(chAdapter, 'down', 0);
        writePng(px, join(NPCS_OUT, `${npc.id}.png`));
    }
    console.log(`npcs: ${npcs.length} sprites written to ${NPCS_OUT}`);
}

const t0 = Date.now();
buildCrops();
buildNpcs();
console.log(`done in ${Date.now() - t0}ms`);
