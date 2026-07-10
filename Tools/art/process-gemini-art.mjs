// Post-processes raw Gemini API output (Tools/art/gemini-out/) into game-ready
// sprites matching the existing procedural convention that GeneratedSpriteImporter.cs
// and GeneratedSprites.cs already expect: 64x64 PNGs, folder/name convention unchanged.
// Raw Gemini renders come back at ~900-1400px with solid or inconsistent backgrounds
// and (for crops) all growth stages in one wide strip — this normalizes all of that
// with zero Unity/C# changes required.
import { createCanvas, loadImage } from '@napi-rs/canvas';
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync } from 'fs';
import { join, dirname, basename } from 'path';
import { fileURLToPath } from 'url';

const HERE = dirname(fileURLToPath(import.meta.url));
const RAW = join(HERE, 'gemini-out');
const OUT = join(HERE, 'gemini-processed');
const CROPS = JSON.parse(readFileSync(join(HERE, '..', '..', 'Assets', 'Resources', 'GameData', 'crops.json'), 'utf8')).crops;

const CANVAS = 64;
// Color-distance tolerance for flood-fill background removal, adaptive to the background's own
// lightness (see removeBackground) rather than one fixed constant: dark backgrounds (a handful of
// renders came back near-black instead of the usual light gray) sit close enough to the outline
// strokes — which are themselves near-black in this pixel-art style — that the old fixed 42
// crossed into outline territory in spots, breaking the outline's job as a barrier between the
// background and interior fills and fragmenting the subject into dozens of stray components.
const BG_TOLERANCE_MAX = 42;
const BG_TOLERANCE_MIN = 15;

function ensureDir(p) { if (!existsSync(p)) mkdirSync(p, { recursive: true }); }

function getImageData(img) {
    const c = createCanvas(img.width, img.height);
    const ctx = c.getContext('2d');
    ctx.drawImage(img, 0, 0);
    return ctx.getImageData(0, 0, img.width, img.height);
}

// Background removal: sample every pixel along the image's outer border into a color
// palette (not just the 4 corners), then flood-fill inward from the border, treating a
// pixel as background if it's within tolerance of ANY palette color. Several renders have
// a speckled/marbled background rather than a flat color or smooth gradient (adjacent pixels
// can jump 50+ units) — a chain that compares each pixel only to the neighbor that reached it
// either dies out partway across the noise (leaving most of the background untouched) or, if
// tolerance is loosened enough to survive the noise, leaks through antialiased outline pixels
// into similarly light/gray interior fills (e.g. a white chicken's body). Matching against the
// full sampled border palette handles the noise directly (the palette already contains its
// range of tones) without needing looser tolerances, while flood-fill connectivity (not a
// standalone per-pixel classifier) still protects any interior region that merely happens to
// share a similar color but isn't actually connected to the border.
const PALETTE_QUANT = 8; // dedupe bucket size — a noisy background still only has a few dozen
    // distinct tones; deduping keeps the per-pixel palette scan cheap across ~1M-pixel images.
// Several NPC portraits are framed tight enough that a shoulder/sleeve touches the image edge,
// so "every frontier pixel is background" isn't quite true — a handful are actually subject
// color. Unfiltered, those got folded into the palette too, and since matching is "close to ANY
// palette color", the subject's own clothing tones then matched large parts of the subject
// elsewhere in the image, erasing most of the character. Requiring a color to appear a meaningful
// fraction of the frontier before trusting it as background filters that out — actual background
// dominates the frontier by a wide margin, an incidentally-touching sleeve doesn't.
const FRONTIER_PALETTE_MIN_FRACTION = 0.02;
function buildFrontierPalette(data, w, frontier) {
    const counts = new Map(); // quantized key -> {count, r, g, b} (first-seen representative color)
    let opaqueCount = 0;
    for (const [x, y] of frontier) {
        const i = (y * w + x) * 4;
        if (data[i + 3] <= 10) continue; // already-transparent pixels carry no meaningful RGB (the
            // encoder is free to leave arbitrary leftover values there) — counting them corrupts the
            // palette with noise, which then matches opaque pixels elsewhere by sheer coincidence.
        opaqueCount++;
        const r = data[i], g = data[i + 1], b = data[i + 2];
        const key = `${r >> 3},${g >> 3},${b >> 3}`; // >>3 == /8 rounded down, matches PALETTE_QUANT
        const entry = counts.get(key);
        if (entry) entry.count++; else counts.set(key, { count: 1, r, g, b });
    }
    const minCount = Math.max(8, opaqueCount * FRONTIER_PALETTE_MIN_FRACTION);
    const palette = [];
    for (const { count, r, g, b } of counts.values()) {
        if (count >= minCount) palette.push([r, g, b]);
    }
    return palette;
}

// Tolerance scales with how dark the palette is — see BG_TOLERANCE_MAX/MIN above.
function toleranceForPalette(palette) {
    if (palette.length === 0) return BG_TOLERANCE_MAX;
    const avgLightness = palette.reduce((sum, [r, g, b]) => sum + (r + g + b) / 3, 0) / palette.length;
    return Math.min(BG_TOLERANCE_MAX, Math.max(BG_TOLERANCE_MIN, avgLightness * 0.8));
}

function matchesPalette(data, i, palette, tolerance) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    for (const [pr, pg, pb] of palette) {
        const dr = r - pr, dg = g - pg, db = b - pb;
        if (dr * dr + dg * dg + db * db <= tolerance * tolerance) return true;
    }
    return false;
}

// BFS from `seeds`, clearing any pixel reachable through others matching `palette`.
function floodFillPalette(imageData, palette, seeds, tolerance) {
    const { width: w, height: h, data } = imageData;
    const queued = new Uint8Array(w * h);
    const qx = new Int32Array(w * h), qy = new Int32Array(w * h);
    let qHead = 0, qTail = 0;
    let cleared = 0;
    for (const [x, y] of seeds) {
        const idx = y * w + x;
        if (queued[idx]) continue;
        queued[idx] = 1;
        qx[qTail] = x; qy[qTail] = y; qTail++;
    }

    while (qHead < qTail) {
        const x = qx[qHead], y = qy[qHead];
        qHead++;
        const idx = y * w + x;
        const i = idx * 4;
        const alreadyClear = data[i + 3] <= 10;
        const isBg = alreadyClear || matchesPalette(data, i, palette, tolerance);
        if (!isBg) continue;
        if (!alreadyClear) { data[i + 3] = 0; cleared++; }
        const neighbors = [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]];
        for (const [nx, ny] of neighbors) {
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            const ni = ny * w + nx;
            if (queued[ni]) continue;
            queued[ni] = 1;
            qx[qTail] = nx; qy[qTail] = ny;
            qTail++;
        }
    }
    return cleared;
}

function borderFrontier(w, h) {
    const pts = [];
    for (let x = 0; x < w; x++) { pts.push([x, 0], [x, h - 1]); }
    for (let y = 1; y < h - 1; y++) { pts.push([0, y], [w - 1, y]); }
    return pts;
}

// Opaque pixels adjacent to an already-cleared pixel — the "next layer in" after the previous
// pass cleared everything it could reach.
function exposedFrontier(data, w, h) {
    const pts = [];
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            const i = (y * w + x) * 4;
            if (data[i + 3] <= 10) continue; // only opaque pixels are candidates
            const neighbors = [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]];
            for (const [nx, ny] of neighbors) {
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                if (data[(ny * w + nx) * 4 + 3] <= 10) { pts.push([x, y]); break; }
            }
        }
    }
    return pts;
}

// A handful of renders use a nested picture-frame composition — a decorative border strip
// distinct from the actual (different-colored) background behind the subject — or are framed
// tight enough that a hat brim spans most of the top edge. Either way, a single frontier-based
// pass only clears the outer layer (the frame strip, or almost nothing), leaving the true
// background — which never touches the image's actual border — untouched. Treating the newly-
// exposed inner boundary as a new frontier and repeating peels through any such layers; it's a
// no-op (0 additional cleared) for ordinary single-background images, where the first pass
// already reaches everything connected to the border.
const MIN_CLEARED_FRACTION = 0.35; // calibrated against real samples: clean single-layer removals
    // clear ~50-60% of the canvas; the nested-frame/tight-framing failures clear only ~8-23%.
// NPC/animal/landmark portraits fill most of the frame (a real background there tops out around
// 60-70%, even peeling 2-3 nested frame layers), so >85% cleared in one jump is a reliable signal
// the fill broke into the subject. Crop growth strips are the opposite composition — several tiny
// early-stage sprouts scattered across a mostly-empty canvas — where legitimately clearing ~90%
// in one pass is normal, not a leak; applying the tight cap there rolled back correct results.
const MAX_CLEARED_FRACTION_DEFAULT = 0.85;
const MAX_CLEARED_FRACTION_SPARSE = 0.97;
const MAX_PEEL_ITERATIONS = 4;
function removeBackground(imageData, maxClearedFraction = MAX_CLEARED_FRACTION_DEFAULT) {
    const { width: w, height: h, data } = imageData;
    let seeds = borderFrontier(w, h);
    let totalCleared = 0;
    for (let iter = 0; iter < MAX_PEEL_ITERATIONS; iter++) {
        // Snapshot before this iteration — a subject's own dominant color can occasionally end up
        // as the majority tone along a newly-exposed inner frontier (e.g. a large closeup face
        // breaking past a weak/thin part of its own outline), and a single flood-fill pass can't be
        // partially undone once it's run. Rolling back an iteration that clears an implausible
        // fraction in one jump (a real background layer is a few dozen percent at most; the subject
        // itself is the rest of the frame) is cheaper than trying to predict it beforehand.
        const before = data.slice();
        const beforeCleared = totalCleared;
        const palette = buildFrontierPalette(data, w, seeds);
        if (palette.length === 0) break;
        const tolerance = toleranceForPalette(palette);
        const cleared = floodFillPalette(imageData, palette, seeds, tolerance);
        totalCleared += cleared;
        if (totalCleared >= w * h * maxClearedFraction) {
            data.set(before);
            totalCleared = beforeCleared;
            break;
        }
        if (totalCleared >= w * h * MIN_CLEARED_FRACTION) break;
        if (cleared === 0) break; // this layer's frontier didn't match its own palette — stuck, stop
        seeds = exposedFrontier(data, w, h);
        if (seeds.length === 0) break;
    }
    return imageData;
}

function contentBBox(imageData) {
    const { width: w, height: h, data } = imageData;
    let minX = w, minY = h, maxX = -1, maxY = -1;
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            const a = data[(y * w + x) * 4 + 3];
            if (a > 10) {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
    }
    if (maxX < 0) return { minX: 0, minY: 0, maxX: w - 1, maxY: h - 1 }; // nothing found; keep whole image
    return { minX, minY, maxX, maxY };
}

// All 4-connected opaque components within a rectangular region of `data` (full-image
// width `w`), each as {count, minX, maxX, minY, maxY} in coordinates local to (x0,y0).
// Used for crop stage segmentation: equal-width column slicing assumes stages are evenly
// spaced, which holds for tall "stalk" plants but breaks for wide/sprawling "vine"/"bush"
// archetypes where a stage's plant can occupy very different, unevenly-spaced widths.
// Finding actual connected blobs and sorting left-to-right is robust to that; unlike NPCs/
// animals/landmarks (which can legitimately have multiple blobs, e.g. swarms), a crop image
// is expected to resolve to exactly `stages` blobs, so this is crop-strip-specific.
function findComponents(data, w, x0, y0, pw, ph) {
    const visited = new Uint8Array(pw * ph);
    const components = [];
    for (let ly = 0; ly < ph; ly++) {
        for (let lx = 0; lx < pw; lx++) {
            const li = ly * pw + lx;
            if (visited[li]) continue;
            const gx = x0 + lx, gy = y0 + ly;
            if (data[(gy * w + gx) * 4 + 3] <= 10) { visited[li] = 1; continue; }
            let count = 0, minX = lx, maxX = lx, minY = ly, maxY = ly;
            const stack = [[lx, ly]];
            visited[li] = 1;
            while (stack.length) {
                const [cx, cy] = stack.pop();
                count++;
                if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
                const neighbors = [[cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]];
                for (const [nx, ny] of neighbors) {
                    if (nx < 0 || ny < 0 || nx >= pw || ny >= ph) continue;
                    const ni = ny * pw + nx;
                    if (visited[ni]) continue;
                    const ngx = x0 + nx, ngy = y0 + ny;
                    if (data[(ngy * w + ngx) * 4 + 3] <= 10) { visited[ni] = 1; continue; }
                    visited[ni] = 1;
                    stack.push([nx, ny]);
                }
            }
            components.push({ count, minX, maxX, minY, maxY });
        }
    }
    return components;
}

function imageDataToCanvas(imageData) {
    const c = createCanvas(imageData.width, imageData.height);
    c.getContext('2d').putImageData(imageData, 0, 0);
    return c;
}

// Fit a source canvas region into a 64x64 transparent canvas, preserving aspect ratio.
// anchor: 'bottom' (npc/animal, small margin below content) or 'center' (crops/landmarks).
function composite(srcCanvas, sx, sy, sw, sh, anchor, marginFrac = 0) {
    const out = createCanvas(CANVAS, CANVAS);
    const ctx = out.getContext('2d');
    ctx.imageSmoothingEnabled = true;
    const margin = Math.round(CANVAS * marginFrac);
    const availH = CANVAS - margin;
    const scale = Math.min(CANVAS / sw, availH / sh);
    const dw = sw * scale, dh = sh * scale;
    const dx = (CANVAS - dw) / 2;
    const dy = anchor === 'bottom' ? (CANVAS - margin - dh) : (CANVAS - dh) / 2;
    ctx.drawImage(srcCanvas, sx, sy, sw, sh, dx, dy, dw, dh);
    return out;
}

async function processSingle(inPath, outPath, anchor, marginFrac) {
    const img = await loadImage(inPath);
    const imageData = getImageData(img);
    removeBackground(imageData);
    const bbox = contentBBox(imageData);
    const srcCanvas = imageDataToCanvas(imageData);
    const w = bbox.maxX - bbox.minX + 1, h = bbox.maxY - bbox.minY + 1;
    const out = composite(srcCanvas, bbox.minX, bbox.minY, w, h, anchor, marginFrac);
    writeFileSync(outPath, out.toBuffer('image/png'));
}

// Tiles: opaque ground texture, no transparency concept — center-crop to square, downscale.
async function processTile(inPath, outPath) {
    const img = await loadImage(inPath);
    const side = Math.min(img.width, img.height);
    const sx = (img.width - side) / 2, sy = (img.height - side) / 2;
    const out = createCanvas(CANVAS, CANVAS);
    out.getContext('2d').drawImage(img, sx, sy, side, side, 0, 0, CANVAS, CANVAS);
    writeFileSync(outPath, out.toBuffer('image/png'));
}

// Crops: one wide strip with N growth stages left-to-right. Background-removed as one
// image first, then segmented into N per-stage blobs by connectivity (not equal-width
// columns — sprawling vine/bush archetypes space stages very unevenly), sorted left-to-right,
// each independently re-centered but sharing the same vertical scale/baseline.
const CROP_MARGIN = 3; // bottom margin px, matches procedural convention
const CROP_MIN_STAGE_FRAC = 0.30; // smallest stage still occupies at least this much of availH — a literal
    // scale (tiniest sprout rendered at its true tiny fraction of the tallest stage's height) made early
    // growth stages nearly invisible (1-2px), a real gameplay-readability regression versus the procedural
    // sprites' more moderate progression. Floor keeps ordering monotonic while guaranteeing visibility.
const CROP_NOISE_MIN_AREA = 30; // components smaller than this (px, at ~900-1400px source res) are
    // stray sparkle/dust artifacts, not a legitimate stage blob — dropped before segmentation.

async function processCropStrip(inPath, outPaths) {
    const img = await loadImage(inPath);
    const imageData = getImageData(img);
    removeBackground(imageData, MAX_CLEARED_FRACTION_SPARSE);
    const bbox = contentBBox(imageData);
    const trimmedW = bbox.maxX - bbox.minX + 1;
    const trimmedH = bbox.maxY - bbox.minY + 1;
    const srcCanvas = imageDataToCanvas(imageData);
    const stages = outPaths.length;

    // Re-read pixel data cropped to trimmed region so component scans stay in local coords.
    const trimmedCtx = createCanvas(trimmedW, trimmedH).getContext('2d');
    trimmedCtx.drawImage(srcCanvas, bbox.minX, bbox.minY, trimmedW, trimmedH, 0, 0, trimmedW, trimmedH);
    const trimmedData = trimmedCtx.getImageData(0, 0, trimmedW, trimmedH);
    const availH = CANVAS - CROP_MARGIN;

    let components = findComponents(trimmedData.data, trimmedW, 0, 0, trimmedW, trimmedH)
        .filter(c => c.count >= CROP_NOISE_MIN_AREA);

    let panels;
    if (components.length === 0) {
        panels = null; // handled by fallback below
    } else {
        if (components.length > stages) {
            // A handful of renders have genuine per-pixel grain/noise in the background — no small
            // color palette can match truly random noise, so dozens of 1-pixel-ish islands survive
            // background removal, fragmenting the real stages apart from (and bridging together) each
            // other. Merging by closest gap chains right through that noise and produces garbage.
            // The real stages are still unambiguous by SIZE though (thousands of px vs a few dozen for
            // a noise speck), so keeping the `stages` largest and discarding the rest is far more
            // robust here than trying to gap-merge everything down to the right count.
            components.sort((a, b) => b.count - a.count);
            components = components.slice(0, stages);
        }
        components.sort((a, b) => (a.minX + a.maxX) - (b.minX + b.maxX));
        if (components.length === stages) {
            panels = components.map(c => ({ x0: c.minX, y0: c.minY, w: c.maxX - c.minX + 1, h: c.maxY - c.minY + 1 }));
        } else {
            panels = null; // under-segmented (stages touching/overlapping) — fall back below
        }
    }

    if (!panels) {
        console.log(`  ! ${basename(inPath)}: connectivity found ${components.length}/${stages} stage blobs — falling back to equal-width columns (check output)`);
        const panelW = trimmedW / stages;
        panels = [];
        for (let i = 0; i < stages; i++) {
            const panelX0 = Math.round(i * panelW);
            const panelX1 = Math.round((i + 1) * panelW);
            const pw = panelX1 - panelX0;
            const comps = findComponents(trimmedData.data, trimmedW, panelX0, 0, pw, trimmedH)
                .filter(c => c.count >= CROP_NOISE_MIN_AREA)
                .sort((a, b) => b.count - a.count);
            const b = comps[0] || { minX: 0, maxX: pw - 1, minY: 0, maxY: trimmedH - 1 };
            panels.push({ x0: panelX0 + b.minX, y0: b.minY, w: b.maxX - b.minX + 1, h: b.maxY - b.minY + 1 });
        }
    }

    // Small safety pad around each detected bbox: a stage's true content occasionally extends a
    // few px past its connected-component bbox (e.g. a heart-shaped bloom's top notch separated
    // from the rest by a single anti-aliased gap during background removal, becoming its own
    // tiny disconnected sliver) — padding before the fit-to-canvas scale means a slightly-tight
    // bbox just adds a hair of margin instead of visibly clipping the content.
    const PANEL_PAD = 10;
    panels = panels.map(p => {
        const x0 = Math.max(0, p.x0 - PANEL_PAD), y0 = Math.max(0, p.y0 - PANEL_PAD);
        const x1 = Math.min(trimmedW, p.x0 + p.w + PANEL_PAD), y1 = Math.min(trimmedH, p.y0 + p.h + PANEL_PAD);
        return { x0, y0, w: x1 - x0, h: y1 - y0 };
    });

    const maxContentH = Math.max(...panels.map(p => p.h));

    for (let i = 0; i < stages; i++) {
        const p = panels[i];
        const normalized = p.h / maxContentH; // 0..1, 1 = tallest stage
        const targetFrac = CROP_MIN_STAGE_FRAC + (1 - CROP_MIN_STAGE_FRAC) * normalized;
        const scale = (targetFrac * availH) / p.h;
        const dw = p.w * scale, dh = p.h * scale;
        const dx = (CANVAS - dw) / 2;
        const dy = CANVAS - CROP_MARGIN - dh; // bottom-anchored, shared baseline across stages

        const out = createCanvas(CANVAS, CANVAS);
        const ctx = out.getContext('2d');
        ctx.drawImage(trimmedCtx.canvas, p.x0, p.y0, p.w, p.h, dx, dy, dw, dh);
        writeFileSync(outPaths[i], out.toBuffer('image/png'));
    }
}

async function main() {
    const args = process.argv.slice(2);
    const only = args.find(a => !a.startsWith('--'));
    const idFilter = args.find(a => a.startsWith('--id='))?.slice(5);
    const doCategory = (name) => !only || only === name;
    const matchId = (f) => !idFilter || basename(f, '.png').includes(idFilter);

    if (doCategory('npcs')) {
        const dir = join(RAW, 'npcs'); ensureDir(join(OUT, 'npcs'));
        for (const f of readdirSync(dir).filter(f => f.endsWith('.png') && matchId(f))) {
            console.log('npc', f);
            await processSingle(join(dir, f), join(OUT, 'npcs', f), 'bottom', 0.06);
        }
    }
    if (doCategory('animals')) {
        const dir = join(RAW, 'animals'); ensureDir(join(OUT, 'animals'));
        for (const f of readdirSync(dir).filter(f => f.endsWith('.png') && matchId(f))) {
            console.log('animal', f);
            await processSingle(join(dir, f), join(OUT, 'animals', f), 'bottom', 0.10);
        }
    }
    if (doCategory('landmarks')) {
        const dir = join(RAW, 'landmarks'); ensureDir(join(OUT, 'landmarks'));
        for (const f of readdirSync(dir).filter(f => f.endsWith('.png') && matchId(f))) {
            console.log('landmark', f);
            await processSingle(join(dir, f), join(OUT, 'landmarks', f), 'center', 0);
        }
    }
    if (doCategory('tiles')) {
        const dir = join(RAW, 'tiles'); ensureDir(join(OUT, 'tiles'));
        for (const f of readdirSync(dir).filter(f => f.endsWith('.png') && matchId(f))) {
            console.log('tile', f);
            await processTile(join(dir, f), join(OUT, 'tiles', f));
        }
    }
    if (doCategory('crops')) {
        const dir = join(RAW, 'crops'); ensureDir(join(OUT, 'crops'));
        for (const f of readdirSync(dir).filter(f => f.endsWith('.png') && matchId(f))) {
            const id = basename(f, '.png');
            const crop = CROPS.find(c => c.id === id);
            if (!crop) { console.log('  ! no crops.json entry for', id, '- skipping'); continue; }
            console.log('crop', id, `(${crop.stages} stages)`);
            const outPaths = Array.from({ length: crop.stages }, (_, i) => join(OUT, 'crops', `${id}_${i}.png`));
            await processCropStrip(join(dir, f), outPaths);
        }
    }
    console.log('done.');
}

main().catch(e => { console.error(e); process.exit(1); });
