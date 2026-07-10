// Cosmic Bloom art build — reuses the Farm & Give procedural pixel-art
// library (style.mjs / sprites/crops.mjs / sprites/characters.mjs, copied
// verbatim into this folder so this repo has no build-time dependency on the
// old one) to render crop-stage and NPC sprites straight from this project's
// own GameData JSON. No atlases, no build.mjs coupling — one PNG per frame,
// loaded at runtime by Assets/Resources/Sprites/Generated/.
//
// Usage:  node Tools/art/build-cosmic.mjs <category...>
//         categories: crops npcs animals tiles landmarks player
// Categories are REQUIRED since the 2026-07-10 Gemini art swap: every
// category folder now holds curated Gemini-generated PNGs under these same
// filenames, so an unqualified run would silently overwrite all of them with
// procedural output. Name exactly the categories you intend to regenerate.
// Needs @napi-rs/canvas resolvable (see README note below / CLAUDE.md).
import { createCanvas } from '@napi-rs/canvas';
import { mkdirSync, writeFileSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

import { SCALE, TILE, shade } from './style.mjs';
import { cropSprite } from './sprites/crops.mjs';
import { characterSprite } from './sprites/characters.mjs';
import { animalSprite } from './sprites/animals.mjs';
import { groundTileMask } from './sprites/tiles.mjs';
import { landmarkSprite } from './sprites/landmarks.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = join(HERE, '../..');                       // D:\Unity\Cosmic Bloom
const GAMEDATA_DIR = join(PROJECT_ROOT, 'Assets/Resources/GameData');
const OUT_ROOT = join(PROJECT_ROOT, 'Assets/Resources/Sprites/Generated');
const CROPS_OUT = join(OUT_ROOT, 'Crops');
const NPCS_OUT = join(OUT_ROOT, 'Npcs');
const ANIMALS_OUT = join(OUT_ROOT, 'Animals');
const TILES_OUT = join(OUT_ROOT, 'Tiles');
const LANDMARKS_OUT = join(OUT_ROOT, 'Landmarks');
const PLAYER_OUT = join(OUT_ROOT, 'Player');

// animals.json has no body-plan field, so (same pattern as
// AnimalSpawner.ExclusiveRegionByAnimal) this is a code-side table mapping
// each animal to a shared body archetype (bird/quad/blob/swarm/amphibian)
// plus one small marking that keeps same-archetype animals (3 birds, 6
// quads) visually distinct — chicken vs duck vs owl in particular.
const ANIMAL_VISUALS = {
    chicken: { body: 'bird', marking: 'comb' },
    comet_duck: { body: 'bird', marking: 'bill' },
    star_owl: { body: 'bird', marking: 'owl' },
    sheep: { body: 'quad', size: 'big', marking: 'wool' },
    nebula_cow: { body: 'quad', size: 'big', marking: 'spots' },
    crater_goat: { body: 'quad', size: 'big', marking: 'horns' },
    asteroid_pig: { body: 'quad', size: 'big', marking: 'snout' },
    moon_rabbit: { body: 'quad', size: 'small', marking: 'ears' },
    biolume_fox: { body: 'quad', size: 'small', marking: 'tail' },
    cosmic_tortoise: { body: 'quad', size: 'small', marking: 'shell' },
    glow_axolotl: { body: 'amphibian' },
    star_slime: { body: 'blob' },
    bee_swarm: { body: 'swarm', marking: 'stripes' },
    butterfly_swarm: { body: 'swarm', marking: 'wings' },
    firefly_swarm: { body: 'swarm', marking: 'glow' },
};

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

function buildAnimals() {
    mkdirSync(ANIMALS_OUT, { recursive: true });
    const { animals } = readJson(join(GAMEDATA_DIR, 'animals.json'));
    for (const animal of animals) {
        const visual = ANIMAL_VISUALS[animal.id];
        if (!visual) throw new Error(`no visual archetype mapped for animal '${animal.id}' — add one to ANIMAL_VISUALS in build-cosmic.mjs`);
        const body = animal.palette && animal.palette.body ? animal.palette.body : '#cccccc';
        const accent = animal.palette && animal.palette.accent ? animal.palette.accent : '#888888';
        const px = animalSprite({ ...visual, color: body, accent });
        writePng(px, join(ANIMALS_OUT, `${animal.id}.png`));
    }
    console.log(`animals: ${animals.length} sprites written to ${ANIMALS_OUT}`);
}

function buildTiles() {
    mkdirSync(TILES_OUT, { recursive: true });
    const { regions } = readJson(join(GAMEDATA_DIR, 'regions.json'));
    let count = 0;
    for (const region of regions) {
        for (let tier = 0; tier < 3; tier++) {
            const px = groundTileMask(tier, region.id);
            writePng(px, join(TILES_OUT, `${region.id}_tier${tier}.png`));
            count++;
        }
    }
    console.log(`tiles: ${regions.length} regions x 3 tiers = ${count} frames written to ${TILES_OUT}`);
}

function buildLandmarks() {
    mkdirSync(LANDMARKS_OUT, { recursive: true });
    const { regions } = readJson(join(GAMEDATA_DIR, 'regions.json'));
    const seen = new Set();
    let count = 0;
    for (const region of regions) {
        for (const lm of region.landmarks || []) {
            if (seen.has(lm.id)) continue;   // ids are unique across regions.json today; guard anyway
            seen.add(lm.id);
            for (const restored of [false, true]) {
                const px = landmarkSprite(lm.id, restored);
                writePng(px, join(LANDMARKS_OUT, `${lm.id}_${restored ? 'restored' : 'ruined'}.png`));
                count++;
            }
        }
    }
    console.log(`landmarks: ${seen.size} landmarks x 2 states = ${count} frames written to ${LANDMARKS_OUT}`);
}

// Player has no GameData entry — the "teal cosmic gardener" placeholder color
// (PlayerController's Capsule tint, #7dd8c8) becomes the outfit, same
// palette-to-adapter mapping buildNpcs uses. Stopgap until Gemini credits are
// restored; one sprite, same 64x64/bottom-pivot conventions as NPCs.
function buildPlayer() {
    mkdirSync(PLAYER_OUT, { recursive: true });
    const body = '#7dd8c8';
    const px = characterSprite({
        skin: shade(body, 0.35),
        hair: '#355e54',
        outfit: body,
        hat: 'none',
    }, 'down', 0);
    writePng(px, join(PLAYER_OUT, 'player.png'));
    console.log(`player: 1 sprite written to ${PLAYER_OUT}`);
}

const BUILDERS = {
    crops: buildCrops,
    npcs: buildNpcs,
    animals: buildAnimals,
    tiles: buildTiles,
    landmarks: buildLandmarks,
    player: buildPlayer,
};

const requested = process.argv.slice(2).map(a => a.toLowerCase());
if (requested.length === 0 || requested.some(c => !BUILDERS[c])) {
    console.error(`Usage: node build-cosmic.mjs <category...>   (${Object.keys(BUILDERS).join(' ')})`);
    console.error('Categories are required: category folders hold curated Gemini art under');
    console.error('these same filenames — an unqualified run would overwrite ALL of it.');
    process.exit(1);
}

const t0 = Date.now();
for (const c of requested) BUILDERS[c]();
console.log(`done in ${Date.now() - t0}ms`);
