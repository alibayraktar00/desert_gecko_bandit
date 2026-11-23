// Tozlu Dere - Gümüş Vadi hattının treni. 32 piksel = 1 birim; her sprite'ın alt kenarı ray üstü (tekerlek altı).
// Tekerlekler ayrı sprite: oyunda döner. Vagonun iç görünüşü ayrı: Gila içerideyken dış duvar saydamlaşır.
// Renkler kasabanın binalarından (istasyon, demirci) örneklendi.
pal({
  k: "#140c10",                                                   // çizgi
  R: "#3a1414", r: "#5e1f1c", s: "#7f2c22", S: "#a33f2b",          // vagon boyası (koyu -> açık)
  W: "#2d2029", w: "#432822", o: "#624330", O: "#7e593b", u: "#a07850", // ahşap
  M: "#1a1618", m: "#2b2528", n: "#403939", N: "#5b504b", g: "#7d716a", // demir
  B: "#6a4a20", b: "#a87a34", y: "#d8aa50", Y: "#f2d68a",           // pirinç
  l: "#8a5a20", L: "#e0a040", F: "#f6d27a",                         // yanan pencere
  p: "#1c2238", P: "#2c3656",                                       // gece camı
  v: "#22301f", V: "#34482e", x: "#4a6040",                         // iç duvar kâğıdı
  e: "#4a1c18", E: "#6e2a20",                                       // deri koltuk
  c: "#16141a", C: "#2e2a2e",                                       // kömür
  h: "#e8e0d0",                                                     // fener ışığı
})
ramps("RrsS", "WwoOu", "MmnNg", "BbyY", "lLF", "pP", "vVx", "eE", "cC")

// ---------------------------------------------------------------------------------------------
// Yolcu vagonu: 320 x 120. Gövde x 14-306, uç sahanlıklar (korkuluklu) iki yanda.
// Satırlar: çatı 0-10, gövde 11-95, taban (zemin üstü) 96, şasi 96-104, boji 100-112, tekerlek altı 119.
// ---------------------------------------------------------------------------------------------
const CW = 320, CH = 120, BODY_L = 14, BODY_R = 306, FLOOR = 96, CEIL = 11

function carRoof(c) {
  // Fener çatı (clerestory): ortada yükseltilmiş, küçük camlı şerit
  c.grad(10, 4, CW - 20, 7, "Nnm", "v", 0.6)
  c.hline(8, 10, CW - 16, "M")
  c.rect(36, 0, CW - 72, 5, "n")
  c.hline(36, 0, CW - 72, "g")
  for (let x = 44; x < CW - 44; x += 14) c.rect(x, 1, 6, 3, "P")
  c.rect(6, 10, CW - 12, 2, "k")
}

function carFrame(c) {
  // Şasi, gergi çubukları, bojiler, sahanlıklar
  c.rect(4, FLOOR, CW - 8, 5, "m")
  c.hline(4, FLOOR, CW - 8, "N")
  c.line(60, 101, 160, 106, "M")
  c.line(160, 106, 260, 101, "M")
  for (const bx of [52, 268]) {
    c.rect(bx - 26, 101, 52, 8, "M")
    c.rect(bx - 22, 103, 44, 3, "n")
    c.rect(bx - 4, 100, 8, 4, "N")
  }
  // Uç sahanlıklar: taban, korkuluk, merdiven
  for (const [x0, dir] of [[0, 1], [CW - BODY_L, -1]]) {
    c.rect(x0, FLOOR - 1, BODY_L, 3, "o")
    c.hline(x0, FLOOR - 1, BODY_L, "O")
    const rx = dir > 0 ? x0 + 1 : x0 + BODY_L - 2
    c.vline(rx, FLOOR - 26, 26, "n")
    c.hline(x0, FLOOR - 26, BODY_L, "N")
    c.hline(x0, FLOOR - 14, BODY_L, "n")
    for (let y = FLOOR + 3; y < FLOOR + 14; y += 4) c.hline(x0 + 3, y, BODY_L - 6, "n")
  }
}

function carOutside(c) {
  // Kırmızı ahşap kaplama, dikey tahta çizgileri
  c.rect(BODY_L, CEIL, BODY_R - BODY_L, FLOOR - CEIL, "s")
  for (let x = BODY_L + 4; x < BODY_R; x += 6) c.vline(x, CEIL, FLOOR - CEIL, "r")
  c.rect(BODY_L, CEIL, BODY_R - BODY_L, 3, "R")              // saçak altı gölgesi
  c.rect(BODY_L, FLOOR - 4, BODY_R - BODY_L, 4, "r")         // etek
  // Pirinç şeritler
  c.hline(BODY_L, CEIL + 6, BODY_R - BODY_L, "b")
  c.hline(BODY_L, 64, BODY_R - BODY_L, "y")
  c.hline(BODY_L, 65, BODY_R - BODY_L, "B")
  // Pencereler: 7 adet, sarı ışık, perde çizgisi
  for (let i = 0; i < 7; i++) {
    const x = 34 + i * 38
    c.rect(x - 1, 23, 24, 33, "R")
    c.rect(x, 24, 22, 31, "L")
    c.rect(x, 24, 22, 6, "F")
    c.rect(x, 24, 4, 31, "l")                                // perde
    c.hline(x, 39, 22, "R")                                  // kanat
    c.hline(x - 2, 56, 26, "O")                              // denizlik
  }
  // Uç kapılar
  for (const x of [BODY_L, BODY_R - 12]) {
    c.rect(x, CEIL + 9, 12, FLOOR - CEIL - 9, "r")
    c.box(x, CEIL + 9, 12, FLOOR - CEIL - 9, "R")
    c.rect(x + 3, CEIL + 14, 6, 14, "L")
    c.px(x + (x === BODY_L ? 9 : 2), 58, "y")                // tokmak
  }
  // Yazı
  c.text(160, 74, "COL HATTI", "y", { align: "c", scale: 2, shadow: "R" })
  for (let x = BODY_L + 5; x < BODY_R - 4; x += 10) { c.px(x, CEIL + 8, "y"); c.px(x, 67, "R") }   // perçinler
  c.light([BODY_L, CEIL + 3, BODY_R - BODY_L, 16], 1, 0.5)    // üstten vuran ay ışığı
  c.shade([BODY_L, FLOOR - 14, BODY_R - BODY_L, 14], 1, 0.5)
  // Kenar çizgisi
  c.box(BODY_L, CEIL, BODY_R - BODY_L, FLOOR - CEIL, "k")
}

function carInside(c) {
  // İç: duvar kâğıdı, lambalar, koltuklar, tahta taban. Uç kapılar açık.
  c.rect(BODY_L, CEIL, BODY_R - BODY_L, FLOOR - CEIL, "V")
  for (let x = BODY_L + 6; x < BODY_R; x += 12) c.vline(x, CEIL + 4, 50, "v")
  c.rect(BODY_L, CEIL, BODY_R - BODY_L, 4, "w")              // tavan kirişi
  c.rect(BODY_L, 62, BODY_R - BODY_L, FLOOR - 62, "o")        // lambri
  for (let x = BODY_L + 3; x < BODY_R; x += 16) c.vline(x, 64, FLOOR - 66, "w")
  c.hline(BODY_L, 62, BODY_R - BODY_L, "O")
  c.hline(BODY_L, 61, BODY_R - BODY_L, "b")
  // Arka duvarın camları: dışarısı gece
  for (let i = 0; i < 7; i++) {
    const x = 34 + i * 38
    c.rect(x - 2, 22, 26, 34, "w")
    c.rect(x, 24, 22, 30, "p")
    c.rect(x, 24, 22, 4, "P")
    c.px(x + 6, 30, "P"); c.px(x + 15, 35, "P")              // yıldız
    c.hline(x, 39, 22, "w")
    c.rect(x, 24, 3, 30, "E")                                // perde
  }
  // Tavan lambaları
  for (let i = 0; i < 6; i++) {
    const x = 52 + i * 38
    c.vline(x, CEIL + 4, 4, "n")
    c.rect(x - 2, CEIL + 8, 5, 5, "L")
    c.px(x, CEIL + 9, "F")
  }
  // Koltuk sıraları (arka planda, sırtları)
  for (let i = 0; i < 7; i++) {
    const x = 30 + i * 38
    c.rect(x, 66, 28, 18, "E")
    c.rect(x, 66, 28, 3, "e")
    c.rect(x + 2, 84, 24, 4, "e")
    c.vline(x + 2, 88, 7, "w"); c.vline(x + 25, 88, 7, "w")
  }
  // Uç kapı boşlukları
  for (const x of [BODY_L, BODY_R - 10]) c.rect(x, CEIL + 12, 10, FLOOR - CEIL - 12, "W")
  // Gölge: tavan altı ve köşeler karanlık
  c.shade([BODY_L, CEIL, BODY_R - BODY_L, 10], 1, 0.5)
  c.box(BODY_L, CEIL, BODY_R - BODY_L, FLOOR - CEIL, "k")
}

sprite("tren_vagon", { w: CW, h: CH }, carRoof, carOutside, carFrame)
sprite("tren_vagon_ic", { w: CW, h: CH }, carRoof, carInside, carFrame)

// ---------------------------------------------------------------------------------------------
// Kömür vagonu: 224 x 96. Gövde 26-78, kömür yığını 12-28 (üstünde yürünür), şasi 78-86.
// ---------------------------------------------------------------------------------------------
sprite("tren_komur", { w: 224, h: 96 }, (c) => {
  const top = 26, bot = 78
  c.rect(6, top, 212, bot - top, "m")
  for (let x = 10; x < 216; x += 12) c.vline(x, top + 2, bot - top - 4, "M")
  for (let x = 12; x < 214; x += 12) { c.px(x, top + 4, "g"); c.px(x, bot - 5, "g") }
  c.hline(6, top, 212, "N")
  c.rect(6, top + 1, 212, 3, "n")
  c.hline(6, 50, 212, "B")
  c.text(112, 56, "NO 7", "b", { align: "c", scale: 2 })
  // Kömür yığını
  const rng = c.rng(7)
  for (let x = 8; x < 216; x++) {
    const hgt = 10 + Math.round(Math.sin(x * 0.07) * 3 + Math.sin(x * 0.19) * 2 + rng() * 1.5)
    c.vline(x, top - hgt, hgt, "c")
    if (rng() < 0.25) c.px(x, top - hgt, "C")
  }
  for (let i = 0; i < 40; i++) c.px(8 + Math.floor(rng() * 208), top - 2 - Math.floor(rng() * 7), "C")
  c.box(6, top, 212, bot - top, "k")
  // Şasi ve bojiler
  c.rect(2, bot, 220, 6, "M")
  c.hline(2, bot, 220, "n")
  for (const bx of [48, 176]) { c.rect(bx - 26, bot + 5, 52, 7, "M"); c.rect(bx - 22, bot + 7, 44, 2, "n") }
})

// ---------------------------------------------------------------------------------------------
// Lokomotif: 416 x 160, önü sağda. Kabin 0-110 (arkada), kazan 104-380, baca, fener, mahmuz.
// Satırlar: kabin çatısı 8-16, kabin 16-122, kazan 62-120, yürüme sahanlığı 120, şasi 120-132.
// ---------------------------------------------------------------------------------------------
sprite("tren_lokomotif", { w: 416, h: 160 }, (c) => {
  // Kazan: koyu demir silindir, pirinç kuşaklar
  c.grad(104, 62, 276, 58, "nNgNnmmmM", "v", 0.6)      // silindirin yuvarlaklığı
  for (let x = 108; x < 340; x += 8) { c.px(x, 64, "g"); c.px(x, 116, "n") }
  for (const x of [150, 200, 250, 300, 350]) { c.vline(x, 62, 58, "b"); c.vline(x + 1, 62, 58, "B") }
  c.box(104, 62, 276, 58, "k")
  // Duman kutusu ve ön kapak
  c.grad(340, 58, 44, 64, "nmmMM", "v", 0.6)            // duman kutusu
  c.rect(382, 58, 4, 64, "n"); c.vline(382, 58, 64, "g")
  for (let y = 62; y < 120; y += 7) c.px(380, y, "g")
  c.box(340, 58, 46, 64, "k")
  // Çan
  c.rect(286, 46, 12, 12, "y"); c.rect(290, 42, 4, 4, "b"); c.hline(284, 58, 16, "B")
  // Baca: geniş ağızlı (balon)
  c.poly([318, 58, 344, 58, 340, 30, 322, 30], "m")
  c.poly([308, 2, 354, 2, 344, 30, 318, 30], "M")
  c.rect(306, 0, 50, 5, "n")
  c.hline(306, 0, 50, "g")
  c.hline(322, 30, 18, "b")
  // Buhar domu ve kum domu (pirinç)
  c.rect(232, 44, 28, 20, "b")
  c.ellipse(246, 44, 14, 6, "y")
  c.rect(234, 46, 4, 16, "Y")
  c.rect(176, 50, 22, 14, "b")
  c.ellipse(187, 50, 11, 4, "y")
  // Düdük
  c.rect(220, 34, 4, 12, "y")
  // Fener
  c.rect(360, 36, 26, 24, "M")
  c.rect(363, 39, 20, 18, "y")
  c.circ(380, 48, 7, "h")
  c.circ(380, 48, 4, "Y")
  // Kabin
  c.rect(4, 16, 106, 106, "s")
  for (let x = 8; x < 110; x += 6) c.vline(x, 16, 106, "r")
  c.rect(4, 16, 106, 4, "R")
  c.hline(4, 64, 106, "y")
  c.rect(16, 28, 34, 30, "R"); c.rect(18, 30, 30, 26, "L"); c.rect(18, 30, 30, 6, "F")   // arka cam
  c.rect(62, 28, 36, 30, "R"); c.rect(64, 30, 32, 26, "L"); c.rect(64, 30, 32, 6, "F")   // yan cam
  c.text(57, 80, "GUMUS", "y", { align: "c", scale: 2, shadow: "R" })
  c.text(57, 94, "EKSPRES", "B", { align: "c" })
  c.box(4, 16, 106, 106, "k")
  // Kabin çatısı
  c.rect(0, 8, 114, 8, "M")
  c.hline(0, 8, 114, "n")
  c.rect(-2, 14, 118, 3, "k")
  // Yürüme sahanlığı, şasi, silindir, mahmuz
  c.rect(100, 120, 290, 4, "o"); c.hline(100, 120, 290, "O")
  c.rect(0, 122, 392, 10, "M"); c.hline(0, 122, 392, "n")
  c.rect(300, 118, 44, 18, "n"); c.box(300, 118, 44, 18, "M"); c.rect(304, 122, 36, 3, "N")  // silindir
  c.poly([384, 120, 400, 120, 416, 158, 384, 158], "S")
  for (let x = 388; x < 414; x += 5) c.line(x, 124, x + 4, 157, "r")
  c.line(384, 120, 416, 158, "k")
  c.hline(384, 158, 32, "k")
  // Biyel kolu yuvası (tekerlekler ayrı)
  c.hline(120, 140, 170, "n")
})

// ---------------------------------------------------------------------------------------------
// Tekerlekler: 4 karelik dönme. Kareler sağa (ileri) dönüşü verir.
// ---------------------------------------------------------------------------------------------
function wheel(r, spokes, rimKey, hubKey) {
  return (c) => {
    const cx = r, cy = r, a0 = c.phase * Math.PI * 2 / spokes
    c.circ(cx, cy, r, "M")
    c.circ(cx, cy, r - 2, "m")
    for (let i = 0; i < spokes; i++) {
      const a = a0 + (i * Math.PI * 2) / spokes
      c.line(cx, cy, Math.round(cx + Math.cos(a) * (r - 3)), Math.round(cy + Math.sin(a) * (r - 3)), "n")
    }
    c.ring(cx, cy, r, "k")
    c.ring(cx, cy, r - 1, rimKey)
    c.circ(cx, cy, Math.max(2, Math.round(r / 5)), hubKey)
    // Krank pimi: biyel kolu buraya bağlanır görünür
    if (r > 14) {
      const a = c.phase * Math.PI * 2
      c.circ(Math.round(cx + Math.cos(a) * r * 0.5), Math.round(cy + Math.sin(a) * r * 0.5), 2, "g")
    }
  }
}
anim("tren_teker", { w: 21, h: 21, frames: 4, fps: 12 }, wheel(10, 6, "n", "N"))
anim("tren_teker_buyuk", { w: 45, h: 45, frames: 4, fps: 12 }, wheel(22, 12, "s", "y"))
anim("tren_teker_orta", { w: 27, h: 27, frames: 4, fps: 12 }, wheel(13, 8, "n", "N"))

// ---------------------------------------------------------------------------------------------
// Yolculukta akan parçalar: ray yatağı (128 x 64, yan yana sonsuz döşenir) ve telgraf direği.
// Ray üstü satır 4: sprite'ın alt kenarından 60 piksel yukarıda.
// ---------------------------------------------------------------------------------------------
pal({ T: "#2e1710", t: "#3c1f15", d: "#582c18", D: "#1f0f0b", G: "#6a5a50", Q: "#8a7a6a" })
ramps("DTtd", "GQ")
sprite("tren_ray_parca", { w: 128, h: 64 }, (c) => {
  // Balast (çakıl) yatağı ve set toprağı
  c.rect(0, 8, 128, 10, "G")
  c.noise(0, 8, 128, 10, "Qn", 0.35, 3)
  c.rect(0, 18, 128, 46, "t")
  c.grad(0, 18, 128, 46, "dtTD", "v", 0.5)
  for (let x = 4; x < 128; x += 23) c.hline(x, 26 + (x % 5), 7, "T")   // set toprağındaki kuruma çizgileri
  // Traversler
  for (let x = 2; x < 128; x += 16) { c.rect(x, 4, 11, 5, "w"); c.hline(x, 4, 11, "o") }
  // Ray: iki ray tek çizgide görünür (yandan bakış)
  c.rect(0, 1, 128, 3, "n"); c.hline(0, 0, 128, "g"); c.hline(0, 3, 128, "M")
  for (let x = 6; x < 128; x += 16) c.px(x, 4, "M")                   // bağlantı çivileri
})
sprite("tren_direk", { w: 30, h: 192 }, (c) => {
  c.rect(13, 10, 5, 182, "w"); c.vline(13, 10, 182, "o")
  c.rect(0, 14, 30, 4, "w"); c.hline(0, 14, 30, "O")
  c.rect(3, 30, 24, 3, "w")
  for (const x of [2, 9, 21, 27]) { c.rect(x - 1, 10, 3, 4, "P"); c.px(x, 10, "x") }
  for (const x of [6, 24]) { c.rect(x - 1, 26, 3, 4, "P") }
})

// Peron: ahşap güverte, kirişler ve ayaklar (128 x 40, yan yana döşenir). Güverte üstü satır 0.
sprite("tren_peron", { w: 128, h: 40 }, (c) => {
  c.rect(0, 0, 128, 6, "O")
  for (let x = 0; x < 128; x += 9) c.vline(x, 0, 6, "o")
  c.hline(0, 0, 128, "u")
  c.rect(0, 6, 128, 4, "w"); c.hline(0, 9, 128, "W")                 // alın tahtası
  for (const x of [10, 74]) {
    c.rect(x, 10, 6, 30, "w"); c.vline(x, 10, 30, "o")                 // ayak
    c.line(x + 6, 12, x + 40, 36, "W", 2)                              // çapraz destek
  }
})

// Raylara bağlanmış dinamit demeti: üç kırmızı çubuk, ip bağı, kıvrılan fitil (alt kenar ray üstü).
sprite("tren_dinamit", { w: 20, h: 20, outline: "k" }, (c) => {
  for (const x of [1, 7, 13]) {
    c.rect(x, 7, 5, 12, "s")                       // çubuk
    c.vline(x + 1, 8, 10, "S"); c.vline(x + 4, 7, 12, "R")
    c.hline(x, 7, 5, "y"); c.hline(x + 1, 6, 3, "y") // pirinç kapak
    c.vline(x + 5, 7, 12, "k")                     // çubuklar arası
  }
  c.rect(0, 12, 19, 2, "u"); c.hline(0, 13, 19, "O") // ip bağı
  c.line(9, 6, 10, 2, "N"); c.line(10, 2, 14, 0, "N"); c.px(15, 0, "g") // fitil
})
// Fitilin ucundaki kıvılcım (3 kare)
anim("tren_fitil", { w: 9, h: 9, frames: 3, fps: 16 }, (c) => {
  const r = [2, 4, 3][c.frame]
  c.circ(4, 4, r, "L"); c.circ(4, 4, Math.max(1, r - 2), "F"); c.px(4, 4, "h")
  if (c.frame === 1) { c.px(0, 4, "F"); c.px(8, 3, "L"); c.px(4, 0, "F") }
})
