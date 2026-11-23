## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` (from the repo root; `.graphifyignore` limits it to Assets/Scripts) to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
- On this machine `graphify.exe` is blocked by Windows Application Control: run it as `python -m graphify ...` (e.g. `python -m graphify query "..."`, `python -m graphify update .`).

## Skill rehberi: hangi iş için hangi skill

Oyun Unity (C#), 2D piksel platformer. Skill'ler global olarak `~/.claude/skills/` altında kurulu. İşe başlamadan
ilgili skill'i Skill aracıyla yükle; ne seçeceğinden emin değilsen önce `router`.

| İş | Skill | Not |
|---|---|---|
| Karakter, düşman, eşya, tile, sprite animasyon karesi çizmek | `pxl-pixel-art` | Kaynak `.px.js` dosyaları `tools/pxl/` altında tutulur (örnek: `tools/pxl/barmen_reload.px.js`). Çıktı PNG'ler `Assets/Sprites/...` içine, mevcut karelerle aynı boyut ve hizada konur. Canlı izleme: `node ~/.claude/skills/pxl-pixel-art/bin/pxl.js serve <dosya>.px.js` |
| Efektler: patlama, vuruş, namlu ateşi, parıltı, toz, ölüm, büyü, hava durumu | `pixelfx` | Kütüphane checkout'u `~/.claude/pixelfx` (araçları oradan çalıştır). Önce hazır efekt ara (`node tools/list.mjs <kelime>`), oyunun paletine boya, Unity için sprite sheet olarak dışa aktar |
| Hangi gamedev skill'i gerektiğine karar vermek | `router` | Görev değiştikçe yeniden yönlendir |
| Unity C# betikleri, `.meta`/GUID, Inspector alanları | `unity-csharp-scripting` | Yeni/yeniden adlandırılan `.cs` ve sprite dosyalarında `.meta` disiplini |
| Unity fizik (Rigidbody2D, katman matrisi, raycast) | `unity-physics` | Genel ayar sorunları (tünelleme, titreme) için `physics-tuning` |
| Unity Animator, Input System, Tilemap, ScriptableObject, NavMesh | `unity-animation`, `unity-input-system`, `unity-tilemap-2d`, `unity-scriptableobjects`, `unity-navmesh` | |
| Unity build ve boyut | `unity-build-pipeline`, `unity-build-size` | |
| Zıplama hissi, coyote time, checkpoint, tek yönlü platform | `platformer` | |
| Vuruş hissi: ekran sarsıntısı, hit-stop, squash & stretch, geri tepme | `game-feel` | |
| Düşman yapay zekâsı (devriye, kovalama, algı) | `game-ai`, `ai-behavior-trees-utility-ai` | |
| Bölüm tasarımı, tempo, kilit/anahtar | `level-design` | Proje kuralı: her bölüm farklı fikir, Level 1 giriş, sonrakiler orta ve üstü |
| Kamera takibi, ölü bölge | `camera-systems` | |
| HUD ve menüler | `game-ui-ux` | |
| Ses karışımı, müzik | `audio-design` | |
| Kayıt/yükleme | `save-systems` | |
| Performans, FPS düşüşü | `performance-optimization` | |
| Shader, prosedürel üretim, diyalog, girdi mimarisi | `shader-programming`, `procedural-gen`, `dialogue-systems`, `input-systems` | |
| Görsel üretim planı ve kalite kontrolü | `create-game-assets` | Ücretli/uzak üretim servisi kullanma; çizimi `pxl-pixel-art`, efekti `pixelfx` ile yerelde yap |
| Tasarım dokümanı, hızlı prototip, jam, yayınlama | `game-docs`, `prototype-fast`, `game-jam`, `itch-publish`, `steam-publish` | |

Kurulu olmayanlar (Godot, Unreal, web motorları, diğer türler) gerekirse
`https://github.com/gamedev-skills/awesome-gamedev-agent-skills` reposundan `~/.claude/skills/` altına kopyalanır.
