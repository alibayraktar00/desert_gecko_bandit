// Yön oku: oyuncuya gideceği yönü gösteren, havada sallanan büyük ok (aşağı bakar; Unity'de 90° döndürülür).
// Sıcak sarı-turuncu gövde, koyu kahve dış çizgi; sol yarı çizilir, aynalanır. Çıktı: tools/pxl/aktar_yon_oku.sh
pal({ k: "#2a160c", o: "#c4561c", y: "#f0a830", Y: "#ffe27a" })
ramps("koyY")

sprite("yon_oku", { mx: true, outline: "k" }, `
  ....oyyY
  ....oyyY
  ....oyyY
  ....oyyY
  ....oyyY
  ....oyyY
  ....oyyY
  ooooooyY
  .oyyyyyY
  ..oyyyyY
  ...oyyyY
  ....oyyY
  .....oyY
  ......oY
  .......o`)
