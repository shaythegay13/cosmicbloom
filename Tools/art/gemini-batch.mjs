// Cosmic Bloom — batch-generates real art via the Gemini image API instead
// of pasting prompts into the Gemini web app one at a time. Reads the same
// GameData JSON build-cosmic.mjs does, builds one prompt per asset (matching
// the templates worked out by hand in chat), attaches a reference image for
// style consistency, and calls the API in sequence.
//
// Setup:
//   npm install                          (installs @google/genai)
//   export GEMINI_API_KEY=...            (from https://aistudio.google.com/apikey)
//   mkdir -p gemini-ref
//   # drop reference PNGs in gemini-ref/: style-sheet.png (fallback for any
//   # category) and/or category-specific ones: npcs.png, crops.png,
//   # animals.png, tiles.png, landmarks.png
//
// Usage:
//   node gemini-batch.mjs npcs --dry-run       # print prompts, no API calls/cost
//   node gemini-batch.mjs npcs                 # actually generate
//   node gemini-batch.mjs npcs --id lumina     # just one
//   node gemini-batch.mjs crops --force        # regenerate even if output exists
//   node gemini-batch.mjs npcs --chain         # anchor each gen to the previous
//                                               # output instead of the fixed ref
//   node gemini-batch.mjs all                  # every category (~120 images)
//
// Cost: gemini-3.1-flash-image is ~$0.067/image at 1K output (no free tier on
// the raw API). ~120 total assets across every category is roughly $8-12.
// Always run --dry-run first to sanity-check prompts before spending money.
//
// Resumable: skips any output file that already exists unless --force is passed.
// Failures are logged to gemini-failures.log and don't stop the batch.
import { GoogleGenAI } from '@google/genai';
import { readFileSync, writeFileSync, existsSync, mkdirSync, appendFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = join(HERE, '../..');
const GAMEDATA_DIR = join(PROJECT_ROOT, 'Assets/Resources/GameData');
const OUT_ROOT = join(HERE, 'gemini-out');
const REF_DIR = join(HERE, 'gemini-ref');
const FAIL_LOG = join(HERE, 'gemini-failures.log');

const MODEL = 'gemini-3.1-flash-image';
const NO_TEXT_RULE = 'Do not render any text, numbers, IDs, or labels inside the image itself — image content only.';
const STYLE_PREFIX = 'Using the established Cosmic Bloom pixel-art style (reference image attached), ';

// ---- CLI ----
const args = process.argv.slice(2);
const category = args.find(a => !a.startsWith('--'));
const has = (name) => args.includes(`--${name}`);
const opt = (name, def) => {
    const i = args.indexOf(`--${name}`);
    if (i === -1 || i === args.length - 1) return def;
    return args[i + 1];
};
const DRY_RUN = has('dry-run');
const FORCE = has('force');
const CHAIN = has('chain');
const DELAY_MS = Number(opt('delay', 2000));
const MAX_RETRIES = Number(opt('retries', 3));
const ONLY_ID = opt('id', null);

function readJson(path) { return JSON.parse(readFileSync(path, 'utf8')); }
function sleep(ms) { return new Promise(res => setTimeout(res, ms)); }

function readRef(categoryName) {
    const specific = join(REF_DIR, `${categoryName}.png`);
    const fallback = join(REF_DIR, 'style-sheet.png');
    const path = existsSync(specific) ? specific : (existsSync(fallback) ? fallback : null);
    return path ? readFileSync(path).toString('base64') : null;
}

// ---------------------------------------------------------------------------
// Job builders — one per category, mirroring the hand-written prompt
// templates worked out in chat.
// ---------------------------------------------------------------------------

const ARCHETYPE_HINT = {
    stalk: 'grows taller with a grain/fruit head at the top',
    bush: 'widening leafy mound with berries',
    root: 'mostly leafy top until the final stage reveals the root at the soil line',
    vine: 'low sprawling growth with one large fruit',
    flower: 'stem grows taller, blooms at the top',
};

const LANDMARK_CONCEPT = {
    broken_greenhouse: 'a small glass-paned greenhouse',
    moonlit_pond: 'a small round pond with reeds',
    celestial_arboretum: 'a single ornamental tree/vault structure',
    solar_forge: 'a small stone forge/furnace',
    lunar_observatory: 'a small domed observatory with a telescope',
    stargazers_rest: 'a small bench and campfire lookout',
    heartseed_roots: 'the exposed roots of a great cosmic tree — the emotional climax structure, give it the most scale/weight of all seven',
};

const TIER_DESC = [
    'outer/wild (cracked/overgrown, least cultivated)',
    'transitional (patchy recovery)',
    'inner/lush-core (most cultivated/thriving)',
];

function buildNpcJobs() {
    const { npcs } = readJson(join(GAMEDATA_DIR, 'npcs.json'));
    return npcs.map(n => ({
        id: n.id,
        category: 'npcs',
        outPath: join(OUT_ROOT, 'npcs', `${n.id}.png`),
        prompt: `${STYLE_PREFIX}generate ONE character portrait for this NPC only. ` +
            `Do not generate any other characters in this image. ${NO_TEXT_RULE}\n\n` +
            `Name: ${n.name}\nRole: ${n.role}\nPersonality: ${n.personality}\n` +
            `Region: ${n.region} (use this region's established palette family)\n` +
            `Body color: ${n.palette?.body}\nAccent color: ${n.palette?.accent}\n\n` +
            `Design them to visibly reflect their role and personality, in the same chibi/cozy proportions as the reference.`,
    }));
}

function buildCropJobs() {
    const { crops } = readJson(join(GAMEDATA_DIR, 'crops.json'));
    return crops.map(c => ({
        id: c.id,
        category: 'crops',
        outPath: join(OUT_ROOT, 'crops', `${c.id}.png`),
        prompt: `${STYLE_PREFIX}generate ONE crop only, shown at all of its growth stages in a ` +
            `left-to-right strip from smallest sprout to fully mature/harvest-ready. Same canvas ` +
            `size for every stage. ${NO_TEXT_RULE}\n\n` +
            `Crop: ${c.name}\nArchetype: ${c.archetype} (${ARCHETYPE_HINT[c.archetype] || ''})\n` +
            `Number of stages: ${c.stages}\nFruit/flower color: ${c.color}\nFoliage color: ${c.leafColor}` +
            (c.id === 'heartseed' ? '\nThis is the finale crop — give its final stage a climactic, special feel.' : ''),
    }));
}

function buildAnimalJobs() {
    const { animals } = readJson(join(GAMEDATA_DIR, 'animals.json'));
    return animals.map(a => ({
        id: a.id,
        category: 'animals',
        outPath: join(OUT_ROOT, 'animals', `${a.id}.png`),
        prompt: `${STYLE_PREFIX}generate ONE animal only. ${NO_TEXT_RULE}\n\n` +
            `Animal: ${a.name} (${a.kind})\nBody color: ${a.palette?.body}\nAccent color: ${a.palette?.accent}\n` +
            `Notes: ${a.notes || ''}\n\n` +
            `Give it a clearly distinct silhouette from other similarly-shaped animals in this game — ` +
            `readability at small mobile size matters most.`,
    }));
}

function buildTileJobs() {
    const { regions } = readJson(join(GAMEDATA_DIR, 'regions.json'));
    const jobs = [];
    for (const r of regions) {
        r.groundTiers.forEach((color, tier) => {
            jobs.push({
                id: `${r.id}_tier${tier}`,
                category: 'tiles',
                outPath: join(OUT_ROOT, 'tiles', `${r.id}_tier${tier}.png`),
                prompt: `${STYLE_PREFIX}generate ONE single tileable ground texture — not a scene, ` +
                    `not a composition of multiple tiles, not a field or garden layout. Just one ` +
                    `square unit of texture that will be repeated edge-to-edge across a grid. It ` +
                    `must loop with no visible seam when placed next to copies of itself. ${NO_TEXT_RULE}\n\n` +
                    `Region: ${r.name}\nTier: ${TIER_DESC[tier]}\nBase color: ${color}`,
            });
        });
    }
    return jobs;
}

function buildLandmarkJobs() {
    const { regions } = readJson(join(GAMEDATA_DIR, 'regions.json'));
    const jobs = [];
    for (const r of regions) {
        for (const lm of r.landmarks || []) {
            for (const state of ['ruined', 'restored']) {
                jobs.push({
                    id: `${lm.id}_${state}`,
                    category: 'landmarks',
                    outPath: join(OUT_ROOT, 'landmarks', `${lm.id}_${state}.png`),
                    prompt: `${STYLE_PREFIX}generate ONE small restorable structure in its ` +
                        `${state.toUpperCase()} state. ${state === 'ruined'
                            ? 'It should look dull, cracked, or dormant.'
                            : 'It should feel bright and magical, visibly healed — but still clearly the same structure as its ruined counterpart.'} ` +
                        `Keep it modest in scale (a small building/feature, not a monument). ${NO_TEXT_RULE}\n\n` +
                        `Structure: ${lm.name}\nRegion: ${r.name}\nConcept: ${LANDMARK_CONCEPT[lm.id] || lm.name}`,
                });
            }
        }
    }
    return jobs;
}

const CATEGORY_BUILDERS = {
    npcs: buildNpcJobs,
    crops: buildCropJobs,
    animals: buildAnimalJobs,
    tiles: buildTileJobs,
    landmarks: buildLandmarkJobs,
};

function allJobs() {
    return Object.values(CATEGORY_BUILDERS).flatMap(fn => fn());
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

function usage() {
    console.log(`Usage: node gemini-batch.mjs <${Object.keys(CATEGORY_BUILDERS).join('|')}|all> [options]

Options:
  --dry-run       print prompts only, no API calls, no cost
  --force         regenerate even if the output file already exists
  --id <id>       only run the single job with this id
  --chain         anchor each generation to the previous output instead of
                  the fixed reference image (riskier — drift can compound)
  --delay <ms>    delay between API calls (default 2000)
  --retries <n>   retries per job on failure (default 3)
`);
}

async function generateOne(ai, job, refBase64) {
    const input = [{ type: 'text', text: job.prompt }];
    if (refBase64) input.push({ type: 'image', mime_type: 'image/png', data: refBase64 });

    let lastErr;
    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        try {
            const interaction = await ai.interactions.create({ model: MODEL, input });
            const img = interaction.output_image;
            if (!img) throw new Error('response had no output_image');
            return Buffer.from(img.data, 'base64');
        } catch (err) {
            lastErr = err;
            console.warn(`  [retry ${attempt}/${MAX_RETRIES}] ${job.id}: ${err.message}`);
            await sleep(1000 * attempt * attempt);
        }
    }
    throw lastErr;
}

async function run() {
    if (!category) { usage(); process.exit(0); }
    if (category !== 'all' && !CATEGORY_BUILDERS[category]) {
        console.error(`Unknown category '${category}'.\n`);
        usage();
        process.exit(1);
    }
    if (!DRY_RUN && !process.env.GEMINI_API_KEY) {
        console.error('Missing GEMINI_API_KEY env var. Get one at https://aistudio.google.com/apikey and set it before running.');
        process.exit(1);
    }

    const jobs = category === 'all' ? allJobs() : CATEGORY_BUILDERS[category]();
    const filtered = ONLY_ID ? jobs.filter(j => j.id === ONLY_ID) : jobs;
    if (ONLY_ID && filtered.length === 0) {
        console.error(`No job with id '${ONLY_ID}' in category '${category}'.`);
        process.exit(1);
    }

    console.log(`${filtered.length} job(s) queued${DRY_RUN ? ' (dry run — no API calls, no cost)' : ''}`);
    mkdirSync(OUT_ROOT, { recursive: true });

    const ai = DRY_RUN ? null : new GoogleGenAI({ apiKey: process.env.GEMINI_API_KEY });
    let lastGood = null;   // used only in --chain mode

    for (const job of filtered) {
        mkdirSync(dirname(job.outPath), { recursive: true });
        if (!FORCE && existsSync(job.outPath)) {
            console.log(`skip (exists): ${job.category}/${job.id}`);
            continue;
        }
        if (DRY_RUN) {
            console.log(`--- ${job.category}/${job.id} ---\n${job.prompt}\n`);
            continue;
        }

        const ref = CHAIN && lastGood ? lastGood.toString('base64') : readRef(job.category);
        try {
            console.log(`generating ${job.category}/${job.id}...`);
            const buf = await generateOne(ai, job, ref);
            writeFileSync(job.outPath, buf);
            lastGood = buf;
            console.log(`  saved ${job.outPath}`);
        } catch (err) {
            console.error(`  FAILED ${job.category}/${job.id}: ${err.message}`);
            appendFileSync(FAIL_LOG, `${new Date().toISOString()} ${job.category}/${job.id}: ${err.message}\n`);
        }
        await sleep(DELAY_MS);
    }
    console.log('done.');
}

run();
