// Landmark sprites (Phase 9 extension) — regions.json's landmark list has
// no shared visual archetype the way crops/animals do (7 unique, one-off
// structures), so each gets a hand-authored pair of sprites instead: ruined
// and restored. That's the direct data-driven analog here — LandmarkController
// already has exactly two states (Def.id in FarmState.landmarksRestored or
// not) driving a color-tint swap on a flat placeholder square, the same
// shape crop *stages* take for crops (multiple generated frames per id,
// swapped as game state changes) rather than NPCs' single static portrait.
import { Px, TILE, shade } from '../style.mjs';

function greenhouse(px, restored) {
    const glass = restored ? '#8ff0c8' : '#5a6058';
    const frame = restored ? '#6e8a5e' : '#4a463e';
    px.rect(9, 10, 14, 16, shade(frame, -0.1));
    px.rect(10, 11, 12, 14, glass);
    px.hline(9, 17, 14, frame);
    px.vline(15, 10, 16, frame);
    px.vline(19, 10, 16, frame);
    px.set(16, 6, frame); px.set(15, 7, frame); px.set(17, 7, frame);
    px.hline(11, 9, 12, frame);
    if (restored) {
        px.blob(16, 20, 3, 4, '#6fd48a');
        px.set(16, 15, '#fff3b0');
    } else {
        px.set(12, 14, shade(glass, -0.3));
        px.set(18, 18, shade(glass, -0.3));
        px.set(14, 22, shade(glass, -0.3));
    }
}

function pond(px, restored) {
    const water = restored ? '#6ab8e8' : '#8a7a5e';
    px.blob(16, 22, 11, 6, restored ? shade(water, -0.15) : '#5d4a2e');
    px.blob(16, 21, 10, 5, water);
    if (restored) {
        px.set(12, 19, shade(water, 0.3));
        px.set(20, 22, shade(water, 0.3));
        px.blob(21, 20, 2, 1.4, '#6fd48a');
    } else {
        for (const [x, y] of [[10, 20], [18, 23], [22, 19]]) px.set(x, y, shade(water, -0.3));
    }
}

function arboretum(px, restored) {
    const trunk = '#5d4226';
    px.rect(14, 20, 4, 9, trunk);
    if (restored) {
        px.blob(16, 14, 10, 8, '#5fae62');
        px.blob(10, 18, 5, 4, '#5fae62');
        px.blob(22, 18, 5, 4, '#5fae62');
        for (const [x, y] of [[12, 12], [20, 11], [16, 8]]) px.set(x, y, '#fff3b0');
    } else {
        px.hline(8, 14, 8, trunk); px.vline(8, 8, 6, trunk);
        px.hline(17, 11, 9, trunk); px.vline(25, 6, 6, trunk);
        px.vline(16, 6, 8, trunk);
    }
}

function forge(px, restored) {
    const stone = '#6e625a';
    px.rect(9, 14, 14, 13, stone);
    px.rect(11, 8, 10, 6, shade(stone, -0.1));
    if (restored) {
        px.blob(16, 18, 5, 5, '#ff9a3b');
        px.blob(16, 12, 3, 3, '#ffcf6b');
        px.set(14, 10, '#fff3b0'); px.set(18, 10, '#fff3b0');
    } else {
        px.blob(16, 19, 4, 3, '#3a3630');
        px.set(13, 16, shade(stone, -0.3));
    }
}

function observatory(px, restored) {
    const dome = restored ? '#c9d0e8' : '#6a6e78';
    px.rect(11, 18, 10, 8, shade(dome, -0.15));
    px.ellipse(16, 16, 8, 7, dome);
    if (restored) {
        px.vline(20, 8, 8, '#8a92a0');
        px.set(21, 8, '#fff3b0');
        for (const [x, y] of [[9, 10], [23, 12], [13, 6]]) px.set(x, y, '#fff3b0');
    } else {
        px.vline(19, 12, 6, shade(dome, -0.3));
        px.set(12, 15, shade(dome, -0.3));
    }
}

function rest(px, restored) {
    px.rect(11, 24, 10, 2, '#5d4226');
    px.rect(11, 24, 2, 4, '#4a3320');
    px.rect(19, 24, 2, 4, '#4a3320');
    if (restored) {
        px.blob(16, 19, 4, 4, '#ff9a3b');
        px.set(16, 14, '#fff3b0');
        px.set(14, 16, '#ffcf6b'); px.set(18, 16, '#ffcf6b');
    } else {
        px.blob(16, 21, 3, 2, '#3a3630');
    }
}

function heartseedRoots(px, restored) {
    const c = restored ? '#ff8fd4' : '#6a5a5e';
    for (const [dx, len] of [[-6, 8], [0, 10], [6, 8], [-3, 6], [3, 6]]) {
        px.vline(16 + dx, 26 - len, len, c);
    }
    if (restored) {
        px.blob(16, 14, 5, 5, '#ffd0ec');
        for (const [x, y] of [[11, 18], [21, 17], [16, 10]]) px.set(x, y, '#fff3b0');
    } else {
        px.set(13, 20, shade(c, -0.2));
        px.set(19, 22, shade(c, -0.2));
    }
}

const BUILDERS = {
    broken_greenhouse: greenhouse,
    moonlit_pond: pond,
    celestial_arboretum: arboretum,
    solar_forge: forge,
    lunar_observatory: observatory,
    stargazers_rest: rest,
    heartseed_roots: heartseedRoots,
};

export function landmarkSprite(id, restored) {
    const builder = BUILDERS[id];
    if (!builder) throw new Error(`no landmark visual mapped for '${id}' — add one to Tools/art/sprites/landmarks.mjs`);
    const px = new Px(TILE, TILE);
    builder(px, restored);
    px.outline();
    return px;
}
