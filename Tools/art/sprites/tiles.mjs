// Ground tile texture masks (Phase 9), replacing the flat procedural tier
// colors (ProceduralSprites.Square tinted by Tile.color). Unlike crops/npcs,
// tiles stay grayscale-only: RegionWorldBuilder still sets Tile.color from
// regions.json's groundTiers hex, and Unity's tilemap tint multiplies that
// color against the sprite's pixels. So a mask pixel of white (#fff) reads
// as exactly the region's tier color, and a darker mask pixel reads as a
// darker shade of that same color — texture (cracks/tufts) without losing
// regions.json as the single source of truth for per-region color, and
// without touching RegionWorldTests' direct Tile.color assertions.
// Adapted from the old repo's tools/art/sprites/tiles.mjs groundTile().
import { Px, TILE, tuft, mulberry32, hashStr } from '../style.mjs';

function gray(v) {
    const h = Math.round(v * 255).toString(16).padStart(2, '0');
    return `#${h}${h}${h}`;
}

// tier 0 = outer wild (cracked/dry), 1 = transitional (patchy recovery),
// 2 = lush core (dense growth). seedKey (region id) gives each region its
// own deterministic crack/tuft placement even though all regions share the
// same per-tier texture logic.
export function groundTileMask(tier, seedKey) {
    const rng = mulberry32(hashStr(`${seedKey}-tier${tier}`));
    const px = new Px(TILE, TILE);
    px.rect(0, 0, TILE, TILE, '#ffffff');
    px.speckle(0, 0, TILE, TILE, gray(0.88), 0.06, rng);
    px.speckle(0, 0, TILE, TILE, gray(0.7), 0.05, rng);

    if (tier === 0) {
        for (let n = 0; n < 3; n++) {
            let x = Math.floor(rng() * TILE);
            let y = Math.floor(rng() * (TILE / 2));
            const len = 8 + Math.floor(rng() * 10);
            for (let i = 0; i < len; i++) {
                px.set(x, y, gray(0.55));
                x += rng() < 0.5 ? 1 : (rng() < 0.5 ? -1 : 0);
                y += 1;
            }
        }
        for (let n = 0; n < 3; n++) {
            const x = Math.floor(rng() * (TILE - 2)), y = Math.floor(rng() * (TILE - 2));
            px.set(x, y, gray(0.6));
            px.set(x + 1, y, gray(0.92));
        }
    } else if (tier === 1) {
        for (let n = 0; n < 6; n++)
            tuft(px, 2 + Math.floor(rng() * (TILE - 4)), 2 + Math.floor(rng() * (TILE - 5)), gray(0.65));
    } else {
        for (let n = 0; n < 10; n++)
            tuft(px, 2 + Math.floor(rng() * (TILE - 4)), 2 + Math.floor(rng() * (TILE - 5)), gray(0.6));
        for (let n = 0; n < 2; n++)
            px.set(3 + Math.floor(rng() * (TILE - 6)), 3 + Math.floor(rng() * (TILE - 6)), gray(0.97));
    }
    // no outline() — tiles butt edge-to-edge and an outline would seam them
    return px;
}
