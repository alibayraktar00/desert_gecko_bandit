// Ana menü altıpatları: Gila'nın eli, namlu sol üste bakıyor (taslaktaki gibi).
// Kamera silahın sağ arkasında: tamburun arka yüzü ve 6 hazne görünür (2.5D).
// Seçili hazne = en üstteki (horozun önü). Menü yazıları Unity'de bu haznenin yanında gösterilir.
//   revolver_idle  : bekleme (hafif nefes)
//   revolver_spin  : tambur bir hazne döner (yukarı/aşağı tuşu)
//   revolver_fire  : horoz kurulur, ateş, geri tepme, duman
pal({
  k: "#140a0c",
  a: "#24232d", b: "#3e3e4c", s: "#626474", S: "#9ca0b0", W: "#dadee8",
  y: "#6e4a1c", Y: "#b07e2c", B: "#e8b848", L: "#fbe6a8",
  d: "#3c180c", w: "#6c2f15", n: "#9e401e", N: "#c66e34",
  g: "#4a2c16", G: "#89572b", o: "#d9882c", O: "#eaa955", P: "#f6cf8a",
  c: "#2a1a12", e: "#5a3a22",
  f: "#fffbe8", F: "#ffe05a", r: "#ff9a24", R: "#d8461c",
  m: "#7c7468", M: "#aea594", q: "#dcd4c2",
})
ramps("kabsSW", "yYBL", "dwnN", "gGoOP", "mMq", "RrFf")

const W = 320, H = 250, SC = 1.25
const DEG = Math.PI / 180

function mk(st) {
  const ca = Math.cos(st.ang * DEG), sa = Math.sin(st.ang * DEG)
  return (x, y) => [st.cx + SC * (x * ca - y * sa), st.cy + SC * (x * sa + y * ca)]
}
function P(cv, t, pts, col) {
  const o = []
  for (let i = 0; i < pts.length; i += 2) {
    const [X, Y] = t(pts[i], pts[i + 1])
    o.push(Math.round(X), Math.round(Y))
  }
  cv.poly(o, col)
}
function E(cv, t, cx, cy, rx, ry, col, n = 24) {
  const pts = []
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2
    pts.push(cx + rx * Math.cos(a), cy + ry * Math.sin(a))
  }
  P(cv, t, pts, col)
}
function R(cv, t, x, y, w, h, col) { P(cv, t, [x, y, x + w, y, x + w, y + h, x, y + h], col) }
function rotAbout(px, py, deg, pts) {
  const ca = Math.cos(deg * DEG), sa = Math.sin(deg * DEG), o = []
  for (let i = 0; i < pts.length; i += 2) {
    const dx = pts[i] - px, dy = pts[i + 1] - py
    o.push(px + dx * ca - dy * sa, py + dx * sa + dy * ca)
  }
  return o
}

// ---- silahın kendisi (yerel koordinat: tambur merkezi = 0,0; namlu -x yönünde) ----
function drawGun(cv, st) {
  const t = mk(st)
  // kol ve el arkası (silahın arkasında kalan kısım)
  P(cv, t, [40, 40, 70, 30, 170, 96, 150, 140], "o")          // kol
  P(cv, t, [70, 30, 170, 96, 168, 104, 64, 38], "O")           // kolun üst ışığı
  P(cv, t, [46, 66, 150, 132, 150, 140, 44, 74], "G")          // kolun alt gölgesi
  E(cv, t, 100, 70, 6, 4, "G"); E(cv, t, 118, 86, 5, 3.5, "G"); E(cv, t, 86, 60, 3.5, 2.5, "G")
  E(cv, t, 132, 100, 6, 4, "G"); E(cv, t, 108, 92, 3, 2, "G")
  P(cv, t, [52, 36, 70, 26, 84, 46, 64, 58], "w")              // deri bileklik
  P(cv, t, [56, 34, 70, 26, 72, 30, 58, 38], "n")
  R(cv, t, 62, 40, 3, 3, "B")                                    // bileklik perçini

  // kabza (ceviz)
  P(cv, t, [18, 16, 36, 12, 48, 30, 60, 56, 64, 74, 46, 80, 38, 62, 28, 40, 16, 26], "w")
  P(cv, t, [36, 12, 48, 30, 60, 56, 54, 58, 42, 32, 32, 16], "n")
  P(cv, t, [38, 62, 46, 80, 64, 74, 62, 70, 48, 74], "d")
  R(cv, t, 58, 70, 8, 8, "s"); R(cv, t, 58, 70, 8, 2, "S")       // kabza dibi madeni

  // tetik korkuluğu
  E(cv, t, 6, 26, 14, 10, "b")
  E(cv, t, 6, 26, 11, 7, "_")
  P(cv, t, [2, 18, 6, 18, 4, 31, 0, 29], "a")                    // tetik

  // namlu
  R(cv, t, -110, -13, 90, 13, "s")
  R(cv, t, -110, -13, 90, 3, "S")
  R(cv, t, -108, -13, 84, 1, "W")
  R(cv, t, -110, -3, 90, 3, "b")
  R(cv, t, -114, -14, 5, 15, "b"); R(cv, t, -114, -14, 5, 2, "s")  // namlu ağzı
  P(cv, t, [-106, -13, -102, -19, -98, -19, -98, -13], "a")      // arpacık
  // harbi muhafazası
  R(cv, t, -82, 0, 62, 8, "b"); R(cv, t, -82, 0, 62, 2, "s")
  E(cv, t, -82, 4, 2.5, 4, "a")

  // gövde
  R(cv, t, -28, -22, 60, 8, "s"); R(cv, t, -28, -22, 60, 2, "S")  // üst köprü
  R(cv, t, -26, -24, 52, 2, "a")                                   // gez oluğu
  R(cv, t, -30, -22, 8, 40, "b"); R(cv, t, -30, -22, 2, 40, "s")   // ön dikme
  R(cv, t, -30, 16, 62, 8, "b"); R(cv, t, -30, 16, 62, 2, "s")     // alt köprü

  // tambur yan yüzü
  R(cv, t, -22, -18, 36, 36, "s")
  R(cv, t, -22, -18, 36, 5, "S")
  R(cv, t, -22, -18, 36, 1, "W")
  R(cv, t, -22, 11, 36, 7, "b")
  for (let i = 0; i < 6; i++) {                                    // oluklar döndükçe kayar
    const phi = (i * 60 + 30 + st.rot) * DEG
    if (Math.cos(phi) <= 0.15) continue
    const yy = -18 * Math.sin(phi)
    R(cv, t, -18, yy - 1.5, 28, 2, "a")
    R(cv, t, -18, yy + 0.5, 28, 1, Math.sin(phi) > 0 ? "S" : "s")
  }

  // tamburun arka yüzü ve hazneler
  E(cv, t, 14, 0, 10, 18, "b")
  E(cv, t, 14, 0, 9, 17, "s")
  E(cv, t, 13, -2, 7, 12, "S")
  E(cv, t, 14, 0, 3, 5, "a")                                       // orta mil
  E(cv, t, 14, 0, 1.6, 2.8, "S")
  for (let i = 0; i < 6; i++) {
    const a = (-90 + i * 60 + st.rot) * DEG
    const hx = 14 + 6 * Math.cos(a), hy = 11.5 * Math.sin(a)
    const sel = i === 0 && Math.abs(st.rot % 60) < 1
    E(cv, t, hx, hy, 3.4, 5.2, "y")                                // fişek tırnağı
    E(cv, t, hx, hy, 2.8, 4.4, sel ? "L" : "B")
    E(cv, t, hx - 0.4, hy - 0.6, 1.6, 2.6, sel ? "f" : "L")
    E(cv, t, hx, hy, 1.1, 1.6, sel ? "B" : "Y")                    // kapsül
  }

  // arka siper ve horoz
  P(cv, t, [24, -22, 34, -22, 34, 18, 26, 22, 22, 6], "b")
  P(cv, t, [24, -22, 34, -22, 33, -18, 25, -18], "s")
  const hp = rotAbout(30, -16, st.ham * 50, [24, -20, 28, -28, 34, -32, 40, -31, 38, -26, 34, -16])
  P(cv, t, hp, "a")
  const hs = rotAbout(30, -16, st.ham * 50, [30, -28, 34, -32, 40, -31, 38, -29, 34, -29])
  P(cv, t, hs, "S")

  // el: başparmak (horoza yakın) ve kabzayı saran parmaklar
  P(cv, t, [40, 22, 52, 14, 74, 34, 66, 62, 50, 70, 40, 46], "o")
  P(cv, t, [52, 14, 74, 34, 70, 40, 52, 22], "O")
  E(cv, t, 58, 46, 4, 3, "G"); E(cv, t, 66, 34, 3, 2, "G")
  P(cv, t, [44, 26, 34, 8, 28, 2, 34, 0, 52, 18], "o")             // başparmak
  P(cv, t, [34, 8, 28, 2, 34, 0, 40, 6], "O")
  E(cv, t, 30, 2, 4.5, 3.6, "P")                                   // başparmak yastığı (geko)
  const fingers = [[16, 34], [18, 44], [22, 54], [27, 64]]
  fingers.forEach(([fx, fy], i) => {
    P(cv, t, [fx, fy - 3, fx + 26, fy - 6 + i, fx + 26, fy + 5 + i, fx, fy + 3], "o")
    P(cv, t, [fx, fy - 3, fx + 26, fy - 6 + i, fx + 26, fy - 3 + i, fx, fy - 1], "O")
    E(cv, t, fx - 1, fy, 4.6, 4, "o")
    E(cv, t, fx - 1.6, fy - 0.4, 3.4, 3, "P")                      // geko parmak yastığı
  })
}

// dış hat: sadece silah ve el (alev ve duman hatsız)
function outline(cv) {
  const hits = []
  for (let y = 0; y < cv.h; y++)
    for (let x = 0; x < cv.w; x++) {
      if (!cv.is(x, y, ".")) continue
      if ((x > 0 && !cv.is(x - 1, y, ".")) || (x < cv.w - 1 && !cv.is(x + 1, y, ".")) ||
          (y > 0 && !cv.is(x, y - 1, ".")) || (y < cv.h - 1 && !cv.is(x, y + 1, "."))) hits.push([x, y])
    }
  for (const [x, y] of hits) cv.px(x, y, "k")
}

// namlu alevi: size 2 = tam patlama, 1 = sönen
function drawFlash(cv, st, size) {
  const t = mk(st)
  const L = size === 2 ? 1 : 0.6
  // ileri koni
  P(cv, t, [-114, -14, -150 * L - 114 * (1 - L) + 0, -7, -114, 1], "R")
  P(cv, t, [-114, -17, -134 - 22 * L, -26 * L, -146 - 30 * L, -7, -134 - 22 * L, 12 * L, -114, 4], "r")
  P(cv, t, [-114, -12, -130 - 14 * L, -16 * L, -140 - 20 * L, -7, -130 - 14 * L, 2 * L, -114, -1], "F")
  if (size === 2) {
    E(cv, t, -120, -7, 9, 7, "f")
    P(cv, t, [-118, -12, -128, -40, -124, -12], "F")               // yan dikenler
    P(cv, t, [-118, -2, -128, 26, -124, -2], "F")
    P(cv, t, [-24, -22, -26, -34, -20, -22], "F")                  // tambur boşluğu kıvılcımı
    P(cv, t, [-24, 18, -27, 28, -20, 18], "r")
  } else {
    E(cv, t, -124, -7, 5, 4, "F")
    E(cv, t, -122, -7, 2.5, 2, "f")
  }
}

function drawSmoke(cv, st, k) {
  // k: 0..1 duman yaşı. Namlu ağzından çıkıp yukarı süzülür.
  const t = mk(st)
  const puffs = [[-132, -8, 7], [-148, -16, 9], [-162, -28, 10], [-140, -26, 6], [-118, -22, 5]]
  puffs.forEach(([x, y, r], i) => {
    const age = k * (1 + i * 0.15)
    const rr = r * (0.5 + age * 0.9) * (k > 0.8 ? (1.6 - k) : 1)
    if (rr < 1.5) return
    const [X, Y] = t(x - age * 6, y - age * 14)
    const py = Y - age * 10
    cv.circ(Math.round(X), Math.round(py), Math.round(rr * SC), k > 0.7 ? "m" : "M")
    cv.circ(Math.round(X - rr * 0.25), Math.round(py - rr * 0.3), Math.max(1, Math.round(rr * 0.6 * SC)), k > 0.7 ? "M" : "q")
  })
}

const BASE = { cx: 196, cy: 122, ang: 20, rot: 0, ham: 0 }
function scene(o) {
  return (cv) => {
    const st = { ...BASE, ...o }
    drawGun(cv, st)
    outline(cv)
    if (o.smoke != null) drawSmoke(cv, st, o.smoke)
    if (o.flash) drawFlash(cv, st, o.flash)
  }
}

anim("revolver_idle", { w: W, h: H, fps: 4 },
  frame({ ms: 500 }, scene({})),
  frame({ ms: 350 }, scene({ cy: 123 })),
  frame({ ms: 500 }, scene({ cy: 124 })),
  frame({ ms: 350 }, scene({ cy: 123 })))

anim("revolver_spin", { w: W, h: H, loop: false },
  frame({ ms: 40 }, scene({ rot: 15 })),
  frame({ ms: 40 }, scene({ rot: 32 })),
  frame({ ms: 50 }, scene({ rot: 48 })),
  frame({ ms: 60 }, scene({ rot: 57 })),
  frame({ ms: 80 }, scene({ rot: 0 })))

anim("revolver_fire", { w: W, h: H, loop: false },
  frame({ ms: 90 }, scene({ ham: 0.45, rot: 25 })),
  frame({ ms: 260 }, scene({ ham: 1, rot: 0, cx: 197 })),
  frame({ ms: 50 }, scene({ ham: 0, ang: 24, cx: 199, flash: 2 })),
  frame({ ms: 60 }, scene({ ham: 0, ang: 31, cx: 202, cy: 121, flash: 1, smoke: 0.1 })),
  frame({ ms: 70 }, scene({ ham: 0, ang: 33, cx: 202, cy: 121, smoke: 0.25 })),
  frame({ ms: 80 }, scene({ ham: 0, ang: 28, cx: 200, smoke: 0.4 })),
  frame({ ms: 90 }, scene({ ham: 0, ang: 23, cx: 198, smoke: 0.55 })),
  frame({ ms: 120 }, scene({ ham: 0, ang: 20, smoke: 0.7 })),
  frame({ ms: 150 }, scene({ ham: 0, ang: 20, smoke: 0.85 })),
  frame({ ms: 200 }, scene({ ham: 0, ang: 20, smoke: 0.98 })))
