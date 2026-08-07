// Mechanical filter-vs-source diff (CLAUDE.md verification discipline).
// Extracts every [Test]/[UnityTest] method from Assets/Tests/PlayMode and diffs it
// against the union of the two canonical filter files. Requires 0 missing, 0 dead,
// 0 duplicates. Reading the lists by eye is exactly how this rotted three times.
//
// Usage, from anywhere:  node Tools/testing/check-filters.mjs
// Exits non-zero on any discrepancy, so it can gate a run.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

// Tools/testing/ -> repo root, so this works from any cwd and any clone.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const testDir = path.join(root, 'Assets/Tests/PlayMode');

const source = new Map();   // fq name -> file
for (const f of fs.readdirSync(testDir).filter(f => f.endsWith('.cs'))) {
  const text = fs.readFileSync(path.join(testDir, f), 'utf8');
  const ns = (text.match(/^\s*namespace\s+([\w.]+)/m) || [, 'GLOBAL'])[1];

  // Track the enclosing class with a brace-depth stack. A flat "last class seen"
  // scan is wrong: PlayerMovementTests.cs declares a nested `FakeInteractable`
  // helper mid-file, and the two [UnityTest]s AFTER it belong to the outer class.
  const stack = [];        // { name, depth }
  let depth = 0;
  let pendingClass = null; // class whose opening brace hasn't been seen yet
  let pendingAttr = false;

  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line.startsWith('//')) continue;

    const c = line.match(/\bclass\s+(\w+)/);
    if (c) pendingClass = c[1];

    if (/\[\s*(Test|UnityTest)\b/.test(line)) pendingAttr = true;
    else if (pendingAttr) {
      const m = line.match(/\b(?:public|private|protected|internal)?\s*(?:static\s+)?(?:IEnumerator|void|System\.Collections\.IEnumerator)\s+(\w+)\s*\(/);
      if (m) {
        const cls = stack.length ? stack[stack.length - 1].name : 'GLOBAL';
        source.set(`${ns}.${cls}.${m[1]}`, f);
        pendingAttr = false;
      }
    }

    for (const ch of line) {
      if (ch === '{') {
        depth++;
        if (pendingClass) { stack.push({ name: pendingClass, depth }); pendingClass = null; }
      } else if (ch === '}') {
        if (stack.length && stack[stack.length - 1].depth === depth) stack.pop();
        depth--;
      }
    }
  }
}

const nog = fs.readFileSync(path.join(root, 'Tools/testing/nographics_filter.txt'), 'utf8')
  .replace(/^\uFEFF/, '').split(';').map(s => s.trim()).filter(Boolean);
const ren = fs.readFileSync(path.join(root, 'Tools/testing/render_names.txt'), 'utf8')
  .replace(/^\uFEFF/, '').split(/\r?\n/).map(s => s.trim()).filter(Boolean);

const all = [...nog, ...ren];
const dupes = all.filter((n, i) => all.indexOf(n) !== i);
const filterSet = new Set(all);
const missing = [...source.keys()].filter(n => !filterSet.has(n));
const dead = all.filter(n => !source.has(n));

console.log(`source tests : ${source.size}`);
console.log(`nographics   : ${nog.length}`);
console.log(`render       : ${ren.length}`);
console.log(`filter total : ${all.length}`);
console.log(`duplicates   : ${dupes.length}${dupes.length ? '\n  ' + dupes.join('\n  ') : ''}`);
console.log(`missing (in source, not in any filter): ${missing.length}`);
missing.forEach(n => console.log(`  + ${n}   [${source.get(n)}]`));
console.log(`dead (in a filter, not in source): ${dead.length}`);
dead.forEach(n => console.log(`  - ${n}`));

const ok = missing.length === 0 && dead.length === 0 && dupes.length === 0;
console.log(ok ? 'RESULT: OK' : 'RESULT: FAIL');
process.exit(ok ? 0 : 1);
