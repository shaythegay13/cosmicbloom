// Animal sprites for Cosmic Bloom's 15 animals (Phase 9). Adapts the two
// Farm & Give creature body plans (quad/bird, from the old repo's
// tools/art/sprites/creatures.mjs) and adds two archetypes that plan never
// needed: blob (star_slime — a legless jelly) and swarm (the three insect
// helpers — a cluster, not a single body). One static pose per animal:
// AnimalController doesn't swap sprite frames, only tints/wanders.
//
// animals.json carries no body-plan field, so ANIMAL_VISUALS in
// build-cosmic.mjs maps id -> {body, size, marking} — same code-side-table
// pattern as AnimalSpawner.ExclusiveRegionByAnimal. `marking` adds one
// small distinguishing feature on top of the shared body plan (comb, horns,
// ears, ...) so same-archetype animals (three quads, three birds) still
// read apart at a glance, per the phase requirement that a player can tell
// which animal is which — chicken vs duck vs owl in particular.
import { Px, TILE, shade } from '../style.mjs';

function quad(a) {
    const px = new Px(TILE, TILE);
    const c = a.color, acc = a.accent;
    const big = a.size === 'big';
    const bodyW = big ? 11 : 8;
    const bodyH = big ? 7 : 5;
    const cy = big ? 18 : 21;

    px.blob(17, cy, bodyW, bodyH, c);
    px.blob(21, cy + 1, bodyW * 0.45, bodyH * 0.6, shade(c, -0.08));

    const hx = 17 - bodyW + 1, hy = cy - bodyH + (big ? 0 : 1);
    px.blob(hx, hy, big ? 5 : 4, big ? 5 : 4, c);
    px.set(hx - 2, hy - (big ? 5 : 4), acc);
    px.set(hx + 2, hy - (big ? 5 : 4), acc);
    px.set(hx - 2, hy - (big ? 4 : 3), c);
    px.set(hx + 2, hy - (big ? 4 : 3), c);
    px.set(hx - 1, hy - 1, '#2d2438');
    px.set(hx - 1, hy - 2, '#ffffff');
    px.set(hx - 3, hy + 1, shade('#e0527a', 0.3));
    px.set(hx - (big ? 5 : 4), hy + 1, shade(c, -0.2));

    const legY = cy + bodyH - 1;
    const legLen = big ? 5 : 4;
    px.rect(11, legY, 2, legLen, shade(c, -0.15));
    px.rect(15, legY, 2, legLen, shade(c, -0.22));
    px.rect(19, legY, 2, legLen, shade(c, -0.15));
    px.rect(23, legY, 2, legLen, shade(c, -0.22));
    px.blob(17 + bodyW - 1, cy - 2, 2, 2, acc);

    quadMarking(px, a, { hx, hy, bodyW, bodyH, cy, big });
    px.outline();
    return px;
}

function quadMarking(px, a, ctx) {
    const c = a.color, acc = a.accent;
    const { hx, hy, bodyW, bodyH, cy, big } = ctx;
    switch (a.marking) {
        case 'wool': // sheep — cloud-like fluff bumps across the back
            for (const [dx, dy] of [[-3, -2], [0, -3], [3, -2], [-1, -1], [2, -1]])
                px.blob(hx + bodyW - 3 + dx, cy - bodyH + 2 + dy, 2.5, 2, shade(c, 0.15));
            break;
        case 'spots': // nebula_cow
            px.blob(hx + bodyW - 1, cy - 1, 2.2, 1.6, acc);
            px.blob(hx + bodyW + 3, cy + 2, 1.8, 1.4, acc);
            break;
        case 'horns': // crater_goat
            px.set(hx - 2, hy - (big ? 6 : 5), acc);
            px.set(hx + 2, hy - (big ? 6 : 5), acc);
            px.set(hx - 3, hy - (big ? 7 : 6), shade(acc, -0.2));
            px.set(hx + 3, hy - (big ? 7 : 6), shade(acc, -0.2));
            break;
        case 'snout': // asteroid_pig — big round snout at the front of the head
            px.blob(hx - (big ? 5 : 4) - 1, hy + 1, 2, 1.6, shade(c, 0.2));
            px.set(hx - (big ? 6 : 5), hy + 1, shade(c, -0.3));
            px.set(hx - (big ? 6 : 5), hy + 2, shade(c, -0.3));
            break;
        case 'ears': // moon_rabbit — tall ears, the single most readable feature
            px.rect(hx - 1, hy - (big ? 11 : 9), 2, big ? 6 : 5, c);
            px.rect(hx + 2, hy - (big ? 11 : 9), 2, big ? 6 : 5, c);
            px.set(hx - 1, hy - (big ? 9 : 7), shade(c, 0.25));
            px.set(hx + 2, hy - (big ? 9 : 7), shade(c, 0.25));
            break;
        case 'tail': // biolume_fox — bushy tail + pointed ears
            px.blob(hx + bodyW + 2, cy - 3, 3, 3, acc);
            px.set(hx - 2, hy - (big ? 5 : 4) - 1, c);
            px.set(hx + 2, hy - (big ? 5 : 4) - 1, c);
            break;
        case 'shell': // cosmic_tortoise — domed shell over the whole back
            px.blob(hx + bodyW - 1, cy - 1, bodyW * 0.7, bodyH * 0.75, acc);
            px.hline(hx + bodyW - 3, cy - 2, 4, shade(acc, -0.2));
            px.vline(hx + bodyW - 1, cy - bodyH + 1, bodyH, shade(acc, -0.2));
            break;
    }
}

function bird(a) {
    const px = new Px(TILE, TILE);
    const c = a.color, acc = a.accent;
    const cy = 21;
    const hx = 12, hy = cy - 5;

    px.blob(17, cy, 6, 5, c);
    px.blob(19, cy + 1, 4, 2.5, shade(c, -0.12));
    px.blob(hx, hy, 4, 4, c);
    px.set(11, cy - 6, '#2d2438');
    px.set(11, cy - 7, '#ffffff');
    px.set(8, cy - 5, acc);
    px.set(7, cy - 5, shade(acc, -0.15));
    px.hline(22, cy - 2, 4, acc);
    px.hline(22, cy - 1, 3, shade(acc, -0.15));
    px.vline(15, cy + 5, 3, '#b08a4a');
    px.vline(19, cy + 5, 3, '#b08a4a');

    switch (a.marking) {
        case 'comb': // chicken — red comb + wattle + a proper orange beak (the
                     // shared bird() beak uses this animal's accent color, which
                     // for the Star Chicken's palette is pale blue, not
                     // beak-colored — override it so this specifically reads
                     // as a chicken, not a generic pale bird)
            px.set(hx, hy - 5, '#d8453a');
            px.set(hx + 1, hy - 6, '#d8453a');
            px.set(hx + 2, hy - 5, '#d8453a');
            px.set(hx - 1, hy - 5, '#e06a5a');
            px.set(hx, hy + 2, '#d8453a');
            px.set(8, hy, '#f5a33b');
            px.set(7, hy, shade('#f5a33b', -0.15));
            break;
        case 'bill': // comet_duck — wide flat bill instead of chicken's pointed beak
            px.rect(6, hy - 1, 4, 3, acc);
            px.rect(6, hy - 1, 4, 1, shade(acc, 0.2));
            break;
        case 'owl': // star_owl — big forward-facing eyes + ear tufts
            px.blob(hx + 1, hy, 3, 3, '#f5f2ec');
            px.blob(hx + 4, hy, 3, 3, '#f5f2ec');
            px.set(hx + 1, hy, '#2d2438');
            px.set(hx + 4, hy, '#2d2438');
            px.set(hx - 1, hy - 4, acc);
            px.set(hx + 6, hy - 4, acc);
            break;
    }

    px.outline();
    return px;
}

function blob(a) {
    const px = new Px(TILE, TILE);
    const c = a.color, acc = a.accent;
    px.blob(16, 22, 9, 8, c);
    px.blob(13, 17, 3, 3, shade(c, 0.25));
    px.set(12, 26, shade(c, -0.15));
    px.set(20, 27, shade(c, -0.15));
    px.set(14, 15, '#2d2438'); px.set(14, 14, '#ffffff');
    px.set(19, 15, '#2d2438'); px.set(19, 14, '#ffffff');
    px.blob(16, 24, 4, 2, acc);
    px.outline();
    return px;
}

function amphibian(a) {
    const px = new Px(TILE, TILE);
    const c = a.color, acc = a.accent;
    px.blob(16, 23, 10, 5, c);
    px.blob(9, 21, 5, 4, c);
    px.set(6, 19, '#2d2438'); px.set(6, 18, '#ffffff');
    for (const dy of [-3, 0, 3]) px.blob(6, 21 + dy, 1.6, 1.2, acc);
    px.rect(11, 26, 2, 3, shade(c, -0.15));
    px.rect(20, 26, 2, 3, shade(c, -0.15));
    px.blob(25, 22, 3, 3, shade(c, 0.1));
    px.outline();
    return px;
}

function swarm(a) {
    const px = new Px(TILE, TILE);
    const c = a.color, acc = a.accent;
    const positions = [[10, 18], [17, 12], [23, 20]];
    for (const [x, y] of positions) {
        if (a.marking === 'wings') {
            // butterflies: big colorful wings dominate the tiny body
            px.blob(x - 2, y, 3, 3, acc);
            px.blob(x + 2, y, 3, 3, acc);
            px.set(x, y, c);
        } else if (a.marking === 'glow') {
            // fireflies: dark silhouette + a bright glow dot — the glow (not
            // this animal's own hues, which vary) is what reads as "firefly"
            px.blob(x, y, 2, 1.6, shade(c, -0.6));
            px.set(x, y - 2, shade(c, -0.6));
            px.set(x + 1, y - 2, shade(c, -0.6));
            px.set(x + 1, y + 1, '#fff3b0');
        } else {
            // bees: round body + a darker stripe band + pale wing dots
            px.blob(x, y, 2, 1.8, c);
            px.set(x, y - 1, shade(c, -0.55));
            px.set(x - 1, y - 2, '#f5f2ec');
            px.set(x + 1, y - 2, '#f5f2ec');
        }
    }
    px.outline();
    return px;
}

export function animalSprite(a) {
    switch (a.body) {
        case 'bird': return bird(a);
        case 'blob': return blob(a);
        case 'swarm': return swarm(a);
        case 'amphibian': return amphibian(a);
        default: return quad(a);
    }
}
