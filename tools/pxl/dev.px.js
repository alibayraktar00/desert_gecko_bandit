// Kanyonun Sarsılmaz Devi (Juggernaut mini-boss). Yandan, sağa bakar.
// Faz 1: sakin dev — beyaz tişört, siyah eşofman, kıvırcık saç, kalın kaş.
// Faz 2: öfke (hulk) formu — şişmiş kaslar, yırtık tişört, kızarmış ten, parlayan göz, damarlar.
// Kare: 128x96 (bard/barmen ile aynı), ayak tabanı y=94.
pal({
  k: "#120c0b",                                   // dış hat
  h: "#1b1210", H: "#3b2820",                     // saç
  D: "#5a3020", s: "#8a5236", S: "#b8774e", L: "#d89c6e",   // ten
  w: "#f3f0e8", W: "#c6d2dc", V: "#8d9fae",        // beyaz tişört + buz mavisi gölge
  p: "#1c1b22", P: "#34343f", q: "#4d4d5c",        // siyah eşofman + antrasit
  e: "#efe6d6",                                   // göz akı / diş
  // öfke ten tonları
  R: "#6e2418", r: "#a2402c", T: "#c9654a", t: "#e48e68",
  y: "#ffe35a", Y: "#ff9a2a",                      // parlayan göz
  g: "#d9d3c6", G: "#a59e92",                  
// ---- gövde çizici: s = ölçek (ayak tabanı merkezli), rage = 2. form ----
function giant(c, o) {
  const s = o.s, ax = 62, ay = 94
  const X = (x) => Math.round(ax + (x - ax) * s * (o.wx || 1))
  const Y = (y) => Math.round(ay + (y - ay) * s)
  const R = (r) => Math.max(1, Math.round(r * s))
  const RX = (r) => Math.max(1, Math.round(r * s * (o.wx || 1)))
  const sk = o.rage ? { D: "R", s: "r", S: "T", L: "t" } : { D: "D", s: "s", S: "S", L: "L" }
  const aw = o.rage ? 1.35 : 1             // kol kalınlığı
  const px = (x, y, col) => c.px(X(x), Y(y), col)

  // --- arka kol (gövdenin arkasında, koyu): üst kol, dirsek, önkol, yumruk ---
  const arm = (sx, front) => {
    const S1 = front ? sk.S : sk.s, S2 = front ? sk.s : sk.D
    const d = front ? 1 : -1
    if (o.rage) c.ellipse(X(sx + 2 * d), Y(56), RX(7), R(6), S1)          // pazı
    else c.line(X(sx + d), Y(53), X(sx + 2 * d), Y(61), S1, R(8))
    c.line(X(sx + 2 * d), Y(61), X(sx + 3 * d), Y(71), S1, R((o.rage ? 10 : 7.5)))   // önkol
    c.line(X(sx + d * 0), Y(62), X(sx + 1 * d), Y(64), S2, 1)                       // dirsek kıvrımı
    c.circ(X(sx + 3.5 * d), Y(75), R(o.rage ? 7.5 : 5.5), S1)                       // yumruk
    c.line(X(sx + 0.5 * d), Y(73), X(sx + 6.5 * d), Y(73), S2, 1)                    // bilek çizgisi
    c.line(X(sx + 6 * d), Y(75), X(sx + 6 * d), Y(78), S2, 1)                        // boğum
  }
  if (o.rage) c.ellipse(X(40), Y(50), RX(7), R(6), sk.s)
  else c.ellipse(X(41), Y(51), RX(6), R(5), "W")
  arm(39, false)

  // --- arka bacak ---
  c.poly([X(46), Y(73), X(58), Y(73), X(57), Y(89), X(47), Y(89)], "p")
  c.rect(X(47), Y(87), X(57) - X(47), 2, "P")                    // lastikli paça
  c.rect(X(45), Y(89), X(58) - X(45), Y(94) - Y(89), "W")       // ayakkabı
  c.hline(X(45), Y(94) - 1, X(58) - X(45), "V")

  // --- kalça / eşofman belli ---
  c.rect(X(44), Y(66), X(79) - X(44), Y(76) - Y(66), "p")

  // --- ön bacak ---
  c.poly([X(62), Y(73), X(77), Y(73), X(78), Y(89), X(63), Y(89)], "p")
  c.line(X(75), Y(75), X(76), Y(87), "P", 1)                      // ön kıvrım hattı
  c.line(X(67), Y(78), X(69), Y(84), "P", 1)
  c.line(X(52), Y(77), X(53), Y(84), "P", 1)
  c.rect(X(63), Y(87), X(78) - X(63), 2, "P")
  c.poly([X(62), Y(89), X(78), Y(89), X(81), Y(91), X(81), Y(94), X(62), Y(94)], "w")   // ön ayakkabı
  c.hline(X(62), Y(94) - 1, X(81) - X(62), "V")
  c.line(X(66), Y(90), X(70), Y(90), "W", 1)

  if (o.rage) {                                                   // dizler yırtık, ten görünüyor
    c.ellipse(X(70), Y(81), RX(3), R(2), sk.S)
    px(68, 80, "p"); px(72, 82, "p")
    c.ellipse(X(52), Y(81), RX(2), R(2), sk.s)
  }

  // --- gövde (tişört) ---
  const shirt = o.rage ? "w" : "w"
  c.ellipse(X(61), Y(47), RX(22), R(5), shirt)                    // omuzlar
  c.ellipse(X(61), Y(56), RX(19), R(11), shirt)                   // göğüs
  c.ellipse(X(68), Y(60), RX(12), R(8), shirt)                    // göbek
  // gölgeler: arka yan + göbek altı + omuz altı
  c.shade((x, y) => c.is(x, y, "w") && x < X(48), 1)
  c.shade((x, y) => c.is(x, y, "w") && y > Y(64), 1)
  c.shade((x, y) => c.is(x, y, "W") && x < X(44), 1)
  c.line(X(58), Y(52), X(63), Y(57), "W", 1)                      // kıvrımlar
  c.line(X(72), Y(55), X(76), Y(58), "W", 1)
  c.line(X(55), Y(60), X(59), Y(63), "W", 1)
  c.hline(X(46), Y(67), X(80) - X(46), "V")                       // tişört etek gölgesi

  // bel bağcığı (beyaz)
  c.line(X(67), Y(68), X(66), Y(73), "w", 1)
  c.line(X(69), Y(68), X(70), Y(72), "w", 1)

  if (o.rage) {
    // yırtıklar: göğüs ve göbekte ten görünür, kenarlar pürüzlü
    const rip = (pts) => c.poly(pts.map((v, i) => (i % 2 ? Y(v) : X(v))), sk.S)
    rip([62, 47, 66, 45, 70, 49, 68, 53, 72, 56, 66, 58, 63, 54, 60, 52])     // göğüs
    rip([70, 60, 76, 58, 79, 61, 77, 65, 72, 66, 69, 63])                     // göbek
    rip([46, 56, 50, 54, 51, 59, 48, 61, 45, 60])                             // yan
    // kas çizgileri (göğüs, karın)
    c.line(X(64), Y(49), X(66), Y(53), sk.s, 1)
    c.hline(X(72), Y(62), X(76) - X(72), sk.s)
    px(66, 46, sk.L); px(74, 59, sk.L)
    // yırtık kenarlarında iplik
    px(61, 52, "W"); px(68, 54, "W"); px(69, 64, "W"); px(79, 62, "W"); px(45, 59, "W")
  }

  // --- boyun & kafa ---
  const hx = 66, hy = 37
  c.rect(X(60), Y(41), X(69) - X(60), Y(47) - Y(41), sk.S)
  if (o.rage) c.poly([X(56), Y(47), X(60), Y(41), X(69), Y(41), X(72), Y(47)], sk.S)   // trapez kası
  c.ellipse(X(hx), Y(hy), RX(7), R(7), sk.S)
  c.rect(X(61), Y(38), X(73) - X(61), Y(44) - Y(38), sk.S)        // kare çene
  c.shade((x, y) => c.is(x, y, sk.S) && x < X(61) && y < Y(47) && y > Y(29), 1)
  c.light((x, y) => c.is(x, y, sk.S) && x > X(71) && y < Y(40) && y > Y(31), 1)
  c.ellipse(X(62), Y(37), 1, R(2), sk.s)                          // kulak
  px(62, 37, sk.D)
  px(74, 37, sk.S); px(74, 38, sk.S); px(75, 38, sk.s)            // burun
  // kulaktan çeneye gölge hattı
  c.line(X(62), Y(42), X(66), Y(44), sk.s, 1)

  if (!o.rage) {
    // sakin: düz kalın kaş, küçük göz, kapalı ağız
    c.hline(X(68), Y(33), 5, "h"); c.hline(X(68), Y(34), 5, "h")
    px(70, 35, "e"); px(71, 35, "k"); px(71, 36, "s")
    c.hline(X(70), Y(41), 3, sk.D)
    px(73, 40, sk.s)
  } else {
    // öfkeli: içe doğru çatılmış kaş, parlayan göz, açık ağız + dişler
    c.line(X(67), Y(32), X(73), Y(35), "h", 2)
    px(70, 36, "y"); px(71, 36, "y"); px(71, 37, "Y")
    c.rect(X(69), Y(40), 5, 3, "k")
    c.hline(X(69), Y(40), 5, "e"); c.hline(X(69), Y(42) , 5, "e")
    px(71, 41, "R")
    c.line(X(64), Y(33), X(66), Y(35), "R", 1)                    // alında damar
  }

  // --- kıvırcık saç ---
  const hair = [[61, 31, 3], [64, 29, 3.5], [68, 29, 3], [71, 31, 2.5], [59, 34, 2.5], [60, 37, 2]]
  const bob = 0
  for (const [x, y, r] of hair) c.circ(X(x), Y(y + bob), R(r), "h")
  for (const [x, y] of [[60, 30], [63, 27], [67, 27], [70, 29], [58, 33], [65, 31], [62, 33], [69, 31], [72, 32]])
    px(x, y + bob, "H")
  for (const [x, y] of [[61, 30], [64, 28], [68, 28], [59, 34]]) px(x + 1, y + 1 + bob, "H")

  // --- ön kol ---
  if (o.rage) {
    c.ellipse(X(83), Y(50), RX(8), R(7), sk.S)                    // şişkin omuz (kol yırtıldı)
    px(82, 47, sk.L); px(84, 47, sk.L)
  } else {
    c.ellipse(X(82), Y(51), RX(7), R(5), "w")                     // kol ağzı
    c.hline(X(77), Y(55), X(89) - X(77), "W")
  }
  arm(83, true)
  c.light((x, y) => c.is(x, y, sk.S) && x > X(88) && y > Y(53) && y < Y(72), 1)
  if (o.rage) {                                                   // damarlar
    c.line(X(85), Y(58), X(86), Y(62), "R", 1)
    c.line(X(86), Y(62), X(88), Y(65), "R", 1)
    c.line(X(38), Y(58), X(37), Y(63), "R", 1)
  }
}

sprite("dev_faz1", { w: 126, h: 94, outline: "k" }, (c) => giant(c, { s: 1, rage: false }))
sprite("dev_faz2", { w: 126, h: 94, outline: "k" }, (c) => giant(c, { s: 1.18, wx: 1.08, rage: true }))

sheet("dev_formlar", ["dev_faz1", "dev_faz2"], { pad: 2 })
