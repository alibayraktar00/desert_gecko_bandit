# Gemini Asset Promptları — Desert Gecko Bandit

Oyun şu an kodla üretilen **geçici (placeholder) kutularla** çalışıyor. Bu asset'ler hazır olunca gerçek görsellerle değiştireceğiz. Acele yok, öncelik sırasıyla ilerle.

## Nasıl kullanılır

1. **Önce "Stil Kartı"nı** her promptun başına yapıştır. Bütün görseller aynı tarzda çıksın diye var.
2. **Önce Gila'nın referans görselini (1.1) üret.** Beğendiğin sonucu sonraki her karakter promptuna **görsel olarak ekle** ve "use the attached image as the character reference" de. Böylece karakter her seferinde aynı görünür.
3. Gemini gerçek şeffaf arka plan üretemiyor. Bu yüzden promptlarda **düz macenta (#FF00FF) arka plan** istiyoruz; arka planı ben Unity tarafında temizlerim.
4. Çıkan görselleri `Assets/Sprites/_Gelen/` klasörüne at, dosya adını tablodaki gibi koy. Küçültme (32 piksele indirme), kesme ve animasyon ayarlarını ben yaparım.
5. **Her promptu ayrı mesajda gönder.** Birden fazla promptu tek mesajda gönderirsen Gemini hepsini tek bir küçük kolajda çizer ve görseller kullanılamaz. Arka planlarda her promptun sonuna şunu ekle: `Output ONE single full-size image only. No collage, no grid, no multiple panels, no labels, no borders.`
6. Bir görsel tutarsız çıkarsa (kare sayısı yanlış, karakter değişmiş vb.) aynı promptla tekrar üret. Sprite sheet'lerde bu sık oluyor, normal.

---

## 🎨 Stil Kartı (her promptun başına ekle)

```
Pixel art game asset for a 2D side-scrolling platformer called "Desert Gecko Bandit".
Style: clean retro 16-bit pixel art, crisp hard pixel edges, NO anti-aliasing, NO blur,
NO gradients, NO soft shadows, limited palette (max 16 colors per sprite),
1-pixel dark outline (#2B1A12) around characters and objects.
Warm Wild West desert palette: sandy orange #E8A04A, burnt red #B5452B,
sun yellow #F4D35E, dusty brown #7A4B2A, cactus green #5E8C3A, bone white #F2E6D0.
Pure flat magenta background #FF00FF filling the whole canvas (it will be removed),
no ground shadow, no text, no watermark, no border, no UI.
```

**Gece sahneleri için** paletin yerine bunu kullan:
```
Night palette: deep indigo #1E1B3A, violet #4B3A78, moon blue #7FA7D9,
pale star white #E6EEFF, cold sand #8E7A9E.
```

---

## 1. Oyuncu — Gila (en öncelikli)

Karakter: Antropomorfik, iki ayak üstünde duran çöl kertenkelesi (gecko). Turuncu-kum rengi pullu, sırtında kırmızı benekler, uzun kıvrık kuyruk, iri sarı gözler. Kırmızı haydut bandanası (yüzün alt kısmında değil, boynunda), küçük kahverengi kovboy şapkası. Sevimli ama yaramaz bir ifade.

Her kare **32x32 piksel karakter** olarak düşünülmeli. Gemini büyük üretir, sorun değil, ben küçültürüm.

### 1.1 Karakter referansı — `gila_referans.png`
```
[STİL KARTI]
Character reference sheet of "Gila", an anthropomorphic desert gecko outlaw standing upright
on two legs. Sandy orange scales with red spots on the back, long curled tail, big yellow eyes,
red bandit bandana tied around the neck, small brown cowboy hat, mischievous cute expression.
Designed to be readable at 32x32 pixels: chunky proportions, big head, short body.
Show 3 views side by side with equal spacing: side view facing right, front view, back view.
Same size and same pixel scale in all three views.
```

### 1.2 Animasyon sheet'leri

Hepsi için ortak ek (promptun sonuna ekle):
```
Use the attached image as the character reference, keep design, colors and proportions identical.
Side view, character faces RIGHT. Horizontal sprite sheet: frames in a single row,
evenly spaced in equal square cells, character centered in each cell with feet on the
same baseline in every frame. Exactly [N] frames.
```

| Dosya adı | Kare | Prompt (ortak ekten önce) |
|---|---|---|
| `gila_idle.png` | 4 | `Idle animation: subtle breathing, tail gently swaying, blinks on frame 3.` |
| `gila_run.png` | 6 | `Fast run cycle, body leaning forward, tail streaming behind for balance.` |
| `gila_jump.png` | 2 | `Jump: frame 1 crouch take-off, frame 2 rising in the air with legs tucked.` |
| `gila_fall.png` | 2 | `Falling: arms up, tail flicking upward, slightly worried face.` |
| `gila_double_jump.png` | 3 | `Double jump in mid-air: the tail whips downward and pushes the gecko up, small dust puff under the tail.` |
| `gila_wall_cling.png` | 2 | `Clinging to a wall on the RIGHT side of the frame: body vertical, hands and feet stuck flat against an invisible wall, tail hanging down. Frame 2 slides slightly down.` |
| `gila_glide.png` | 3 | `Gliding slowly in the air: body horizontal, limbs spread wide, tail flattened and spread like a rudder.` |
| `gila_tongue.png` | 4 | `Tongue attack: frame 1 mouth opens, frame 2-3 long pink sticky tongue shoots straight forward (to the right) about 2 body lengths, frame 4 tongue retracts.` |
| `gila_tail_throw.png` | 4 | `Tail throw attack: the gecko spins and detaches its tail, throwing it forward like a boomerang. Last frame shows the gecko with only a tiny tail stump.` |
| `gila_hurt.png` | 2 | `Getting hit: recoils backward, eyes squeezed shut, hat bouncing off the head.` |
| `gila_camouflage.png` | 4 | `Camouflage: the gecko gradually fades into the colors of the sand, frame 4 is a faint sandy silhouette with only the eyes visible.` |

### 1.3 Ayrı kuyruk mermisi — `gila_tail_projectile.png`
```
[STİL KARTI]
The detached curled tail of the gecko character from the attached reference, as a spinning
projectile. Horizontal sprite sheet with exactly 4 frames showing a full rotation.
Each frame is a small 16x16 pixel object.
```

---

## 2. Bölge 1 — Kızgın Kum Vadisi ortamı

### 2.1 Tileset — `tiles_kum_vadisi.png`
```
[STİL KARTI]
Seamless 2D platformer tileset for a desert canyon, 32x32 pixel tiles arranged on a strict grid
with no gaps between tiles. Include: sand ground top surface (left edge, middle, right edge),
sand ground fill, red canyon rock wall (left side, right side, fill), floating rock platform
(left, middle, right), small one-way wooden plank platform, inner and outer corners.
Organized as a clean 8 columns by 6 rows tile atlas.
```

### 2.2 Dekor objeleri — `props_kum_vadisi.png`
```
[STİL KARTI]
Desert decoration props sprite sheet for a side-view platformer, separated with generous spacing:
tall saguaro cactus, small round cactus, tumbleweed, cow skull, wooden "WANTED" poster on a post
showing a gecko face, wooden fence piece, small rock pile, dry dead bush, wooden signpost,
old wagon wheel. Each prop sized for a 32x32 pixel tile grid (cactus can be 1x2 tiles).
```

### 2.3 Paralaks arka plan — gündüz
Arka plan **4 ayrı katman**dan oluşur. Oyunda her katman farklı hızda kayar, derinlik hissini bu verir.
Bu bölümdeki promptlar **tam haliyle** yazıldı; stil kartını ayrıca ekleme.

**Kurallar:**
- **Her prompt ayrı mesajda**, mümkünse her katman için **yeni sohbette**. Aynı mesajda birden fazla prompt olursa Gemini hepsini tek bir küçük kolaja çizer.
- Gemini'de oran seçeneği varsa **16:9 (yatay)** seç.
- Gündüz katmanlarının arka planı **macenta (#FF00FF)**, gece katmanlarınınki **yeşil (#00FF00)**. Bu renkler silinip şeffaf yapılacağı için çizimin kendisinde bu tonlar kullanılmaz.
- Kaydetmeden önce kontrol et: tek görsel mi, yatay mı, arka plan düz tek renk mi? Değilse aynı promptu tekrar gönder.
- Kenarların birbirine tam uyması şart değil, uymayan yerleri ben düzeltirim.

**1) `bg_gunduz_1_gokyuzu.png`**: gökyüzü, en arkada, kaymaz
```
Create ONE single pixel art image. No collage, no grid, no multiple panels, no labels, no text, no borders, no frame. Aspect ratio 16:9, landscape.

STYLE: retro 16-bit SNES-era pixel art for a 2D side-scrolling platformer background. The image must look like it was drawn at a native resolution of 640x360 pixels and upscaled with nearest-neighbor: small, crisp, square pixels that are all the same size. NO anti-aliasing, NO blur, NO soft brushes, NO smooth gradients, NO painterly texture, NO realism. Flat colors with simple pixel dithering for transitions. Limited palette.

CONTENT: a clear desert daytime sky ONLY.
- Top: deep turquoise blue (#2E8FA8), changing downward through 6 to 8 flat horizontal color bands with dithered borders into warm pale yellow (#F6E3A8) at the bottom.
- A big round pale-yellow pixel sun (#FFE27A) in the upper right third, with 2 soft concentric glow rings made of dithering.
- 4 or 5 flat stylized pixel clouds in cream and light teal, spread across the width at different heights.
- Nothing else: no mountains, no ground, no horizon objects, no birds.
- Fill the entire canvas edge to edge. The left and right edges must match so the image can repeat horizontally.
```

**2) `bg_gunduz_2_uzak_dag.png`**: uzak mesalar, çok yavaş kayar
```
Create ONE single pixel art image. No collage, no grid, no multiple panels, no labels, no text, no borders, no frame. Aspect ratio 16:9, landscape.

STYLE: retro 16-bit SNES-era pixel art for a 2D side-scrolling platformer background. The image must look like it was drawn at a native resolution of 640x360 pixels and upscaled with nearest-neighbor: small, crisp, square pixels that are all the same size. NO anti-aliasing, NO blur, NO soft brushes, NO smooth gradients, NO painterly texture, NO realism. Flat colors with simple pixel dithering. Limited palette.

CONTENT: far background parallax layer of a Monument Valley style Wild West desert.
- A wide row of distant flat-topped mesas and tall buttes spanning the whole width, in dusty terracotta and pale rust (#C9714A, #A85A3C, #E0A27A, #8E4A33), slightly faded by distance haze (a bit lighter toward the top of the rocks).
- Simple horizontal rock strata lines and a few vertical cracks. Flat shading, light coming from the upper right.
- The tallest peaks reach 45% of the image height from the bottom. The bottom 10% is a flat continuous band of distant desert floor that touches the bottom edge across the full width with no gaps.
- The rock silhouette at the left edge and at the right edge must be at the same height so the image can repeat horizontally.
- Everything above the rocks is pure flat solid magenta #FF00FF: one exact color, no gradient, no noise, no sky, no sun, no clouds.
- Do NOT use any purple, pink or magenta tones in the rocks.
```

**3) `bg_gunduz_3_orta_tepe.png`**: orta mesafe tepeler
```
Create ONE single pixel art image. No collage, no grid, no multiple panels, no labels, no text, no borders, no frame. Aspect ratio 16:9, landscape.

STYLE: retro 16-bit SNES-era pixel art for a 2D side-scrolling platformer background. The image must look like it was drawn at a native resolution of 640x360 pixels and upscaled with nearest-neighbor: small, crisp, square pixels that are all the same size. NO anti-aliasing, NO blur, NO soft brushes, NO smooth gradients, NO painterly texture, NO realism. Flat colors with simple pixel dithering. Limited palette.

CONTENT: middle parallax layer of a Wild West desert canyon, closer to the viewer than distant mesas.
- Rolling sandstone hills and red canyon rock ledges spanning the whole width, in warm orange, burnt red and brown (#E8A04A, #D9822B, #B5452B, #7A4B2A), with more contrast and detail than a distant layer. Light from the upper right.
- On the hills: 4 or 5 saguaro cacti of different sizes (#5E8C3A with a dark green outline), one small wooden windmill with a water tank on the right half, and a few small rocks.
- The hills reach up to 35% of the image height from the bottom. The bottom 10% is solid ground that fills the full width and touches the bottom edge.
- The hill line at the left edge and at the right edge must be at the same height so the image can repeat horizontally.
- Everything above the hills is pure flat solid magenta #FF00FF: one exact color, no gradient, no noise, no sky.
- Do NOT use any purple, pink or magenta tones in the artwork.
```

**4) `bg_gunduz_4_on_plan.png`**: ön plan, en hızlı kayar
```
Create ONE single pixel art image. No collage, no grid, no multiple panels, no labels, no text, no borders, no frame. Aspect ratio 16:9, landscape.

STYLE: retro 16-bit SNES-era pixel art for a 2D side-scrolling platformer background. The image must look like it was drawn at a native resolution of 640x360 pixels and upscaled with nearest-neighbor: small, crisp, square pixels that are all the same size. NO anti-aliasing, NO blur, NO soft brushes, NO smooth gradients, NO realism. Flat colors. Limited palette.

CONTENT: near foreground parallax layer of a Wild West desert.
- Along the very bottom: a continuous dark ground strip (#4A2A18) filling the bottom 6% of the image across the full width.
- Standing on that strip, sparse silhouettes with lots of empty space between them: 3 dry desert bushes, 2 small rock piles, a cow skull, a short broken wooden fence, and a few tufts of dry grass. No object is taller than 20% of the image height.
- Objects are dark brown (#4A2A18, #6B3E22) with a thin light orange rim highlight (#E8A04A) only on their top edges.
- The left and right edges must match so the image can repeat horizontally.
- Everything else is pure flat solid magenta #FF00FF: one exact color, no gradient, no noise, no sky.
- Do NOT use any purple, pink or magenta tones in the artwork.
```

### 2.4 Paralaks arka plan — gece
Gece katmanlarında **ilgili gündüz görselini Gemini'ye ekle**. Gemini şekilleri birebir koruyup sadece renkleri değiştirsin; böylece oyunda gündüzden geceye yumuşak geçiş yapılabilir.

**5) `bg_gece_1_gokyuzu.png`**: `bg_gunduz_1_gokyuzu.png` görselini ekle
```
I attached a daytime sky image. Create the NIGHT version of it as ONE single image. No collage, no grid, no multiple panels, no labels, no text, no borders. Keep exactly the same size, aspect ratio, composition, cloud positions and pixel art style.
- Replace the sun with a large pale moon (#E6EEFF) at the same position, with soft dithered glow rings and a few grey crater pixels.
- Sky: flat dithered horizontal bands from deep indigo (#1E1B3A) at the top to violet (#4B3A78) at the bottom.
- Clouds: same shapes and positions, dark blue-grey (#3A3F6B) with pale moonlit blue top edges.
- Add many small stars: single white pixels and a few tiny 3x3 pixel crosses, denser near the top.
- Fill the entire canvas. Crisp square pixels, NO anti-aliasing, NO blur, NO smooth gradients.
```

**6) `bg_gece_2_uzak_dag.png`**, **7) `bg_gece_3_orta_tepe.png`**, **8) `bg_gece_4_on_plan.png`**: her biri için ilgili gündüz görselini ekle, prompt aynı
```
I attached a daytime parallax background layer. Create the NIGHT version of it as ONE single image. No collage, no grid, no multiple panels, no labels, no text, no borders.
Keep EXACTLY the same size, aspect ratio, composition, shapes, outlines and pixel layout: every rock, hill, cactus and object stays in the same place with the same silhouette. Only change the colors:
- Recolor the artwork to a moonlit night using deep indigo, dark violet and cold blue (#1E1B3A, #2C2752, #4B3A78, #7FA7D9).
- Add pale blue-white moonlight highlights (#E6EEFF) on the top edges that face the upper right.
- Cacti become dark blue-teal (#2F4B5C). Do not use any green in the artwork itself.
- Replace the magenta background with pure flat solid green #00FF00: one exact color, no gradient, no noise.
- Crisp square pixels, NO anti-aliasing, NO blur.
```
---

## 3. Düşmanlar

Hepsi yandan görünüm, **sola bakıyor** (oyuncuya doğru). Ortak ek:
```
Side view, facing LEFT. Horizontal sprite sheet, single row, equal square cells,
feet on the same baseline in every frame. Exactly [N] frames.
```

| Dosya adı | Kare | Prompt |
|---|---|---|
| `akrep_yuru.png` | 4 | `[STİL KARTI] Desert scorpion enemy, dark red-brown shell, raised stinger tail, small angry eyes, walking cycle. 32x32 per frame.` |
| `akrep_saldiri.png` | 3 | `[STİL KARTI] Same desert scorpion, stinger strike forward attack.` |
| `yilan_surun.png` | 4 | `[STİL KARTI] Rattlesnake outlaw enemy wearing a tiny black bandit mask, coiling and slithering, rattle on the tail shaking. 32x32 per frame.` |
| `yilan_lider.png` | 4 | `[STİL KARTI] BOSS: big rattlesnake gang leader, wears a black cowboy hat and gold sheriff-style vest, menacing grin, idle animation with rattle shaking. 64x64 per frame.` |

Akbaba Şerif (son boss) ve Bölge 2–3 düşmanları sonraki aşamada; şimdilik gerek yok.

---

## 4. Toplanabilirler — `toplanabilir.png`
```
[STİL KARTI]
Collectible items sprite sheet, each item 16x16 pixels, separated with equal spacing in one row:
green shiny beetle (health), small fly, blue-purple desert crystal gem (currency),
red-orange carved thermometer totem with a sun symbol (temperature bonus),
torn old parchment map piece. Then a second row: 4-frame sparkle/shine animation of the crystal.
```

---

## 5. Arayüz (UI) — `ui_ikonlar.png`
```
[STİL KARTI]
Game UI icons sprite sheet, each 16x16 pixels, equal spacing:
full heart, half heart, empty heart (drawn as small cactus-flower hearts in western style),
crystal counter icon, sun icon, moon icon, thermometer icon, tail-ready icon, tail-cooldown icon (greyed).
Plus a wooden western-style panel frame 9-slice (corners and edges clearly separable), 48x48.
```

---

## Öncelik sırası
1. `gila_referans.png` → sonra `gila_idle`, `gila_run`, `gila_jump`, `gila_fall`, `gila_wall_cling`
2. `tiles_kum_vadisi.png`
3. Gündüz paralaks katmanları (4 katman)
4. Kalan Gila animasyonları
5. Akrep ve yılan
6. Toplanabilirler ve UI
7. Gece katmanları
