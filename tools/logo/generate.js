// Draws every CourtFlow logo asset and writes it to src/CourtFlow.Web/wwwroot.
// Run from this folder: `npm install`, then `node generate.js` (add `--preview <dir>` for a review sheet).
// Colors are the brand palette in src/CourtFlow.Web/Theming/CourtFlowTheme.cs plus a clay accent
// lifted from the Secondary rust so it reads on the teal.
const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');

const C = {
  teal: '#245F73',
  tealBarDark: '#1E4F60',
  cream: '#F2F0EF',
  ink: '#1F2A30',
  clay: '#E8875A',
  clayDeep: '#C2643A',
  darkBg: '#1D2327',
};

const f = (n) => +n.toFixed(3);
const rot = ([x, y], deg) => {
  const a = (deg * Math.PI) / 180;
  return [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a)];
};

// One seam pair in a ball of radius 9 centred on the origin; the other two are 120 and 240 degree copies.
const PRIMARY = { from: [0, 0], to: [8, 4], r: 8 };
const SECONDARY = { from: [-4.5, 1.5], to: [4, 8], r: 12 };

// Moves the end of a small-arc segment further along its own circle until it sits outside the ball,
// so the ball outline, not the stroke cap, finishes the seam at the rim.
function extendPastRim({ from, to, r }, sweep, rim) {
  const [ax, ay] = from, [bx, by] = to;
  const mx = (ax + bx) / 2, my = (ay + by) / 2;
  const h = Math.hypot(bx - ax, by - ay) / 2;
  const dd = Math.sqrt(r * r - h * h);
  const ux = -(by - ay) / (2 * h), uy = (bx - ax) / (2 * h);
  const dir = sweep ? 1 : -1;
  const centres = [[mx + ux * dd, my + uy * dd], [mx - ux * dd, my - uy * dd]];
  const span = ([cx, cy]) => {
    let t = Math.atan2(by - cy, bx - cx) - Math.atan2(ay - cy, ax - cx);
    t = dir > 0 ? (t + 4 * Math.PI) % (2 * Math.PI) : -((-t + 4 * Math.PI) % (2 * Math.PI));
    return t;
  };
  const [cx, cy] = centres.find((c) => Math.abs(span(c)) <= Math.PI);
  let t = Math.atan2(by - cy, bx - cx);
  let p = to;
  while (Math.hypot(p[0], p[1]) < rim) {
    t += dir * 0.01;
    p = [cx + r * Math.cos(t), cy + r * Math.sin(t)];
  }
  return { from, to: p, r };
}

function ball({ cx, cy, R, tilt = -18, fill, accent, stroke, id, primaryOnly = false }) {
  const k = R / 9;
  const P = (pt, deg) => {
    const [x, y] = rot(pt, deg + tilt);
    return `${f(cx + x * k)} ${f(cy + y * k)}`;
  };
  const arc = (r, sweep, pt, deg) => `A${f(r * k)} ${f(r * k)} 0 0 ${sweep} ${P(pt, deg)}`;
  const rim = 9 + (stroke / k) * 1.5;
  const primary = extendPastRim(PRIMARY, 0, rim);
  const secondary = extendPastRim(SECONDARY, 0, rim);

  const seams = [0, 120, 240]
    .map((d) => `M${P(primary.from, d)} ${arc(primary.r, 0, primary.to, d)}` + (primaryOnly ? '' : ` M${P(secondary.from, d)} ${arc(secondary.r, 0, secondary.to, d)}`))
    .join(' ');

  // The strip between the first primary and secondary seam, closed by the neighbouring primary seam.
  // Its outer edge runs outside the ball and is clipped by the mask.
  const d = 0;
  const panel =
    `M${P([0, 0], d)} ${arc(primary.r, 0, primary.to, d)} ` +
    `A${f(rim * k)} ${f(rim * k)} 0 0 1 ${P(secondary.to, d)} ` +
    `${arc(secondary.r, 1, secondary.from, d)} ` +
    `${arc(PRIMARY.r, 1, [0, 0], d)} Z`;

  return (
    `<mask id="${id}" maskUnits="userSpaceOnUse">` +
    `<circle cx="${cx}" cy="${cy}" r="${R}" fill="#fff"/>` +
    `<path d="${seams}" fill="none" stroke="#000" stroke-width="${f(stroke)}" stroke-linecap="round"/>` +
    `</mask>` +
    `<g mask="url(#${id})">` +
    `<circle cx="${cx}" cy="${cy}" r="${R}" fill="${fill}"/>` +
    (accent ? `<path d="${panel}" fill="${accent}"/>` : '') +
    `</g>`
  );
}

const svg = (size, body, title = 'CourtFlow') =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" role="img" aria-label="${title}">${body}</svg>\n`;

// Bare mark for a dark or brand-teal surface, such as the app bar in both themes.
const markOnDark = (id = 'm') => svg(64, ball({ cx: 32, cy: 32, R: 30, fill: C.cream, accent: C.clay, stroke: 3.6, id }));
// Bare mark for a light surface.
const markOnLight = (id = 'm') => svg(64, ball({ cx: 32, cy: 32, R: 30, fill: C.teal, accent: C.clayDeep, stroke: 3.6, id }));

// App icon: brand-teal rounded tile. Reads on light and dark browser chrome alike.
const tile = (id = 't', { radius = 14, R = 21, stroke = 3, primaryOnly = false } = {}) =>
  svg(64, `<rect width="64" height="64" rx="${radius}" fill="${C.teal}"/>` + ball({ cx: 32, cy: 32, R, fill: C.cream, accent: C.clay, stroke, id, primaryOnly }));
// Tab-size icon: bigger ball and heavier seams so it survives 16px.
const tileSmall = (id = 's') => tile(id, { radius: 12, R: 25, stroke: 4.6 });
// 16px: the ball nearly fills the tile, only the three main seams remain, and each is a full pixel wide.
const tileTiny = (id = 'y') => tile(id, { radius: 10, R: 28, stroke: 6, primaryOnly: true });
// Full-bleed square for iOS and maskable manifest icons; the platform applies its own mask.
const tileFull = (id = 'f', R = 19) =>
  svg(64, `<rect width="64" height="64" fill="${C.teal}"/>` + ball({ cx: 32, cy: 32, R, fill: C.cream, accent: C.clay, stroke: 2.8, id }));

const png = (svgText, size) => new Resvg(svgText, { fitTo: { mode: 'width', value: size } }).render().asPng();

const out = path.resolve(__dirname, '../../src/CourtFlow.Web/wwwroot');
const write = (name, data) => fs.writeFileSync(path.join(out, name), data);
write('logo-mark.svg', markOnDark());
write('logo-mark-light.svg', markOnLight());
write('favicon.svg', tileSmall());
write('apple-touch-icon.png', png(tileFull('a', 21), 180));
write('icon-192.png', png(tile(), 192));
write('icon-512.png', png(tile(), 512));
write('icon-maskable-512.png', png(tileFull('x', 17), 512));
const favPngs = [png(tileTiny(), 16), png(tileSmall(), 32), png(tileSmall(), 48)];
require('png-to-ico').default(favPngs).then((ico) => write('favicon.ico', ico));

// `node generate.js --preview <dir>` also writes a contact sheet of the mark in place, for review.
const previewIndex = process.argv.indexOf('--preview');
if (previewIndex < 0) return;
const preview = path.resolve(process.argv[previewIndex + 1] ?? '.');
fs.mkdirSync(preview, { recursive: true });
const inner = (text) => text.replace(/^<svg[^>]*>/, '').replace(/<\/svg>\n$/, '');
const place = (text, x, y, s) => `<svg x="${x}" y="${y}" width="${s}" height="${s}" viewBox="0 0 64 64">${inner(text)}</svg>`;
const bar = (y, bg, label) =>
  `<rect x="0" y="${y}" width="900" height="64" fill="${bg}"/>` +
  place(markOnDark('b' + y), 64, y + 14, 36) +
  `<text x="112" y="${y + 40}" font-family="Segoe UI, Arial" font-size="20" font-weight="600" fill="${C.cream}">CourtFlow</text>` +
  `<text x="880" y="${y + 38}" text-anchor="end" font-family="Segoe UI, Arial" font-size="13" fill="${C.cream}" opacity=".7">${label}</text>`;
const sheet =
  `<svg xmlns="http://www.w3.org/2000/svg" width="900" height="560" viewBox="0 0 900 560">` +
  `<rect width="900" height="560" fill="#ffffff"/>` +
  bar(0, C.teal, 'app bar, light theme') +
  `<rect y="64" width="900" height="16" fill="${C.cream}"/>` +
  bar(80, C.tealBarDark, 'app bar, dark theme') +
  `<rect y="144" width="450" height="416" fill="${C.cream}"/><rect x="450" y="144" width="450" height="416" fill="${C.darkBg}"/>` +
  place(markOnLight('l1'), 40, 180, 120) + place(tile('l2'), 180, 180, 120) + place(tileSmall('l3'), 320, 180, 32) + place(tileTiny('l4'), 360, 180, 16) +
  place(markOnDark('d1'), 490, 180, 120) + place(tile('d2'), 630, 180, 120) + place(tileSmall('d3'), 770, 180, 32) + place(tileTiny('d4'), 810, 180, 16) +
  place(markOnLight('l5'), 40, 330, 48) + `<text x="98" y="364" font-family="Segoe UI, Arial" font-size="28" font-weight="700" fill="${C.ink}">CourtFlow</text>` +
  place(markOnDark('d5'), 490, 330, 48) + `<text x="548" y="364" font-family="Segoe UI, Arial" font-size="28" font-weight="700" fill="${C.cream}">CourtFlow</text>` +
  place(tileFull('l6', 21), 40, 420, 110) + place(tileFull('l7', 17), 180, 420, 110) +
  `</svg>`;
fs.writeFileSync(path.join(preview, 'logo-sheet.png'), png(sheet, 1800));
