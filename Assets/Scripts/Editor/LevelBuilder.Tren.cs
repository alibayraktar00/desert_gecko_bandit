using System.Collections.Generic;
using DesertGecko.Effects;
using DesertGecko.Environment;
using DesertGecko.Levels;
using DesertGecko.Player;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    public static partial class LevelBuilder
    {
        /// <summary>
        /// Level 4 tren kazasının anlarını görüntüler (sahneyi kaydetmez): çarpma anı (lokomotif şahlanmış, vagonlar
        /// yalpalıyor, patlama), uyanış (Gila yerde, yıldızlar). Temp/Screenshots/kaza_*.png
        /// </summary>
        public static string PreviewTrainCrash()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene($"{SceneFolder}/Level_4.unity");
            var crash = Object.FindAnyObjectByType<TrainCrash>();
            var so = new SerializedObject(crash);
            Transform[] Items(string field)
            {
                var arr = so.FindProperty(field);
                var list = new Transform[arr.arraySize];
                for (int i = 0; i < list.Length; i++)
                    list[i] = (Transform)arr.GetArrayElementAtIndex(i).objectReferenceValue;
                return list;
            }
            Transform[] cars = Items("cars"), pieces = Items("locoPieces");
            float front = so.FindProperty("locoFrontX").floatValue;
            Vector2 wake = so.FindProperty("wakePoint").vector2Value;
            Transform loco = cars[cars.Length - 1];
            Vector2 blast = new Vector2(loco.position.x + 1f, 0f);
            var rng = new System.Random(3);

            // 1) Dinamit yaklaşırken (fitil yanıyor)
            var dyn = (Transform)so.FindProperty("dynamite").objectReferenceValue;
            dyn.gameObject.SetActive(true);
            dyn.position = new Vector3(front + 6f, 0f, 0f);
            Camera cam = Camera.main;
            float size = cam.orthographicSize;
            cam.orthographicSize = 6.3f;
            SceneCapture.Capture("Temp/Screenshots/kaza_dinamit.png", new Vector3(front + 3f, 3.2f, -10f));
            dyn.gameObject.SetActive(false);

            // 2) Patlamadan ~0.4 sn sonra: lokomotif parçaları havada, kömür vagonu burnunu sokuyor, vagonlar yalpalıyor
            ((GameObject)so.FindProperty("locoLook").objectReferenceValue).SetActive(false);
            var flying = new List<Transform>(pieces);
            foreach (WheelSpin w in loco.GetComponentsInChildren<WheelSpin>())
                flying.Add(w.transform);
            foreach (Transform piece in flying)
            {
                piece.gameObject.SetActive(true);
                Vector2 away = (Vector2)piece.position - blast;
                piece.position += (Vector3)(away.normalized * 2.2f + new Vector2(1.5f, 2.5f + (float)rng.NextDouble() * 2f));
                piece.rotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 160 - 80));
            }
            int n = cars.Length;
            for (int i = 0; i < n - 1; i++)
            {
                int fromFront = n - 2 - i;
                float angle = fromFront == 0 ? -38f : (fromFront % 2 == 0 ? 1f : -1f) * Mathf.Max(0f, 22f - fromFront * 4f);
                cars[i].position += new Vector3(Mathf.Max(0f, 2.5f - fromFront * 0.5f), fromFront == 0 ? 2.4f : Mathf.Max(0f, 1.2f - fromFront * 0.3f), 0f);
                cars[i].rotation = Quaternion.Euler(0f, 0f, angle);
            }
            Sprite[] boom = (Sprite[])typeof(TrainCrash).GetField("explosionFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(crash);
            var fx = new GameObject("onizleme").AddComponent<SpriteRenderer>();
            fx.sprite = boom[Mathf.Min(6, boom.Length - 1)];
            fx.sortingOrder = 30;
            fx.transform.position = blast;
            fx.transform.localScale = Vector3.one * 1.15f;
            var player = Object.FindAnyObjectByType<PlayerController>();
            player.transform.position = new Vector3(blast.x - 5f, 7f, 0f);
            SceneCapture.Capture("Temp/Screenshots/kaza_patlama.png", new Vector3(front + 3f, 3.2f, -10f));
            cam.orthographicSize = size;

            // 3) Uyanış: Gila yüzüstü uyuyor, başında yıldızlar
            Object.DestroyImmediate(fx.gameObject);
            player.transform.position = wake;
            var visual = player.GetComponentInChildren<PlayerVisual>();
            Sprite[] getUp = (Sprite[])typeof(TrainCrash).GetField("getUpFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(crash);
            visual.GetComponent<SpriteRenderer>().sprite = getUp[0];
            Sprite[] stars = (Sprite[])typeof(TrainCrash).GetField("stunFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(crash);
            var st = new GameObject("yildiz").AddComponent<SpriteRenderer>();
            st.sprite = stars[3];
            st.sortingOrder = 40;
            st.transform.position = wake + new Vector2(0.1f, -0.75f);
            SceneCapture.Capture("Temp/Screenshots/kaza_uyanis.png", new Vector3(wake.x + 2f, wake.y + 1.5f, -10f));

            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? $"{SceneFolder}/Level_4.unity" : previous); // değişiklikler atılır
            return "tamam";
        }

        internal partial class LevelPainter
        {
            // Tren görselleri tools/pxl/tren.px.js'ten (32 piksel = 1 birim), efektler pixelfx'ten: tools/pxl/aktar_tren.py.
            const float Px = 1f / 32f;
            const float CarWidth = 10f, TenderWidth = 7f, LocoWidth = 13f, CarGap = 1f;
            const float CarFloor = 24f * Px;       // vagon tabanı (ray üstünden)
            const float CarCeiling = 109f * Px;    // iç tavan
            const float CarRoof = 116f * Px;       // dam
            const float TenderTop = 80f * Px;      // kömür yığınının üstü
            const int TrainOrder = -5;             // vagon dışı; içi -7, tekerlek -4, Gila ve düşmanlar önde

            /// <summary>Geçişin teleportla yapıldığı yerler (dünya x'i, oradan -> buraya): menzil denetimi bunları atlar.</summary>
            readonly List<(float from, float to)> teleports = new List<(float, float)>();

            static Sprite Tren(string name) => TestSceneBuilder.Prop("Tren/" + name);

            // Lokomotif parçaları: tools/pxl/aktar_tren.py LOCO_PIECES ile aynı (lokomotif sprite'ında piksel)
            static readonly (string name, int x0, int y0, int x1, int y1)[] LocoPieces =
            {
                ("kabin", 0, 6, 110, 134), ("kazan_arka", 104, 56, 204, 122), ("kazan_orta", 204, 30, 300, 122),
                ("kazan_on", 300, 0, 392, 122), ("mahmuz", 380, 116, 416, 160), ("sasi_arka", 0, 118, 200, 136),
                ("sasi_on", 200, 118, 392, 136),
            };

            static void SetArray(Object target, string field, IList<Transform> items)
            {
                var so = new SerializedObject(target);
                SerializedProperty p = so.FindProperty(field);
                p.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            /// <summary>Serileşen alanı yazar: nesne, sayı, bool, Vector2 ya da sprite dizisi.</summary>
            static void Set(Object target, string field, object value)
            {
                var so = new SerializedObject(target);
                SerializedProperty p = so.FindProperty(field);
                switch (value)
                {
                    case Object o: p.objectReferenceValue = o; break;
                    case float f: p.floatValue = f; break;
                    case int i: p.intValue = i; break;
                    case bool b: p.boolValue = b; break;
                    case Vector2 v: p.vector2Value = v; break;
                    case Sprite[] arr:
                        p.arraySize = arr.Length;
                        for (int k = 0; k < arr.Length; k++)
                            p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                        break;
                    default: throw new System.ArgumentException(field);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            static Sprite[] TrenFrames(string prefix)
            {
                var frames = new List<Sprite>();
                for (int i = 0; ; i++)
                {
                    Sprite s = Tren($"{prefix}_{i}");
                    if (s == null)
                        break;
                    frames.Add(s);
                }
                return frames.ToArray();
            }

            SpriteRenderer TrainSprite(Transform parent, string name, Sprite sprite, Vector2 worldPos, int order)
            {
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(parent);
                sr.transform.position = worldPos;
                sr.sprite = sprite;
                sr.sortingOrder = order;
                return sr;
            }

            void Box(Transform parent, string name, float x0, float x1, float y0, float y1, bool oneWay = false, bool climbable = true)
            {
                var go = new GameObject(name) { layer = climbable ? ctx.GroundLayer : 0 };
                go.transform.SetParent(parent);
                go.transform.position = new Vector2((x0 + x1) / 2f, oneWay ? y1 : (y0 + y1) / 2f);
                if (oneWay)
                    AddOneWayCollider(go, x1 - x0, 0.3f, new Vector2(0f, -0.15f));
                else
                    go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, y1 - y0);
            }

            void Wheels(Transform parent, string frames, float radius, float left, float trackY, params int[] centersPx)
            {
                Sprite[] f = TrenFrames(frames);
                foreach (int cx in centersPx)
                {
                    SpriteRenderer sr = TrainSprite(parent, "Teker", f[0], new Vector2(left + cx * Px, trackY), TrainOrder + 1);
                    Set(sr.gameObject.AddComponent<WheelSpin>(), "frames", f);
                    Set(sr.GetComponent<WheelSpin>(), "radius", radius);
                }
            }

            /// <summary>Bu çağrıda decor ve geometry altına eklenen her şeyi verilen köke taşır.</summary>
            void Gather(Transform into, System.Action build)
            {
                int d = decor.childCount, g = geometry.childCount;
                build();
                var moved = new List<Transform>();
                for (int i = d; i < decor.childCount; i++) moved.Add(decor.GetChild(i));
                for (int i = g; i < geometry.childCount; i++) moved.Add(geometry.GetChild(i));
                foreach (Transform t in moved)
                    t.SetParent(into, true);
            }

            /// <summary>
            /// İstasyon ve kalkan tren. Peron yerel 0-26'da (güverte 0.8), tren 26.5'ten başlar: yolcu vagonları,
            /// kömür vagonu, lokomotif. Gila bir vagona basınca tren kalkar, peron geride kalır, dünya akar (TrainRide).
            /// Lokomotifin kabinine varınca kaza sahnesi (TrainCrash) başlar; Gila kaza yerinde (wakeX, wakeY) uyanır.
            /// Dönüş: lokomotifin önünden ileriye kadar uzanan x (yerel), kaza yerine teleport işareti için.
            /// </summary>
            public float TrainJourney(int passengerCars, float wakeX, float wakeY, System.Action<int, float, float, float> populate)
            {
                const float trackY = 0f, deck = 0.8f, trainStart = 26.5f;
                float o = OriginX;
                var root = new GameObject("TrenYolculugu").transform;
                root.SetParent(geometry);

                // --- Peron: kalkınca dünyayla birlikte geriye kayar ---
                var station = new GameObject("Peron").transform;
                station.SetParent(root);
                station.position = new Vector2(o + 13f, deck);
                Sprite peron = Tren("tren_peron");
                for (float x = 0f; x < 28f; x += 4f)
                    TrainSprite(station, "Guverte", peron, new Vector2(o + x + 2f, deck - 40f * Px), TrainOrder - 3);
                Box(station, "PeronZemin", o, o + trainStart - 0.1f, deck - 0.3f, deck);
                platforms.Add((o, o + trainStart - 0.1f, deck));
                Gather(station, () =>
                {
                    Deco("kasaba_istasyon", 11f, deck, BuildingOrder);
                    Lamp(8.3f, deck + 1.6f, 1.6f);
                    Lamp(13.7f, deck + 1.6f, 1.6f);
                    TownDeco("kasaba_bank_sandalye", 18f, deck, 1f, -8);
                    TownDeco("kasaba_erzak", 23.5f, deck, 1f, -8);
                    Sign(21f, deck, "Gümüş Ekspres kalkıyor!\nVagonların üstünden ya da\niçinden lokomotife koş");
                });
                station.gameObject.AddComponent<WorldScroller>().Configure(1f, 0f, 0f, o - 45f);

                // --- Tren ---
                var cars = new List<Transform>();
                float x0 = o + trainStart;
                for (int i = 0; i < passengerCars; i++)
                {
                    Transform car = PassengerCar(root, x0, trackY, i);
                    cars.Add(car);
                    populate?.Invoke(i, x0 - o, trackY + CarFloor, trackY + CarRoof);
                    x0 += CarWidth + CarGap;
                }
                cars.Add(TenderCar(root, x0, trackY));
                x0 += TenderWidth + CarGap;
                float locoLeft = x0;
                Transform loco = Locomotive(root, locoLeft, trackY);
                cars.Add(loco);
                float trainEnd = locoLeft + LocoWidth;

                // --- Akan dünya: ray yatağı, direkler, öndeki kaktüsler ---
                float wrapMin = o, wrapWidth = Mathf.Ceil((trainEnd + 44f - wrapMin) / 16f) * 16f;
                var track = new GameObject("RayYatagi").transform;
                track.SetParent(root);
                Sprite rail = Tren("tren_ray_parca");
                for (float x = wrapMin; x < wrapMin + wrapWidth; x += 4f)
                {
                    SpriteRenderer sr = TrainSprite(track, "Ray", rail, new Vector2(x + 2f, trackY + 4f * Px - 64f * Px), TrainOrder - 4);
                    sr.gameObject.AddComponent<WorldScroller>().Configure(1f, wrapMin - 2f, wrapWidth);
                }
                SolidRect(decor, "RayAlti", wrapMin, wrapMin + wrapWidth, GroundBottom, trackY - 60f * Px + 0.02f, new Color32(0x1F, 0x0F, 0x0B, 0xFF), TrainOrder - 5);
                Sprite pole = Tren("tren_direk");
                for (float x = wrapMin + 6f; x < wrapMin + wrapWidth; x += 16f)
                {
                    SpriteRenderer sr = TrainSprite(track, "Direk", pole, new Vector2(x, trackY - 0.2f), TrainOrder - 9);
                    sr.color = new Color(0.78f, 0.78f, 0.85f);
                    sr.gameObject.AddComponent<WorldScroller>().Configure(0.85f, wrapMin - 2f, wrapWidth);
                }
                string[] cacti = { "kaktus_07", "kaktus_12", "kaktus_21", "kaktus_16" };
                for (int i = 0; i * 48f < wrapWidth; i++)
                {
                    Sprite s = TestSceneBuilder.Prop(cacti[i % cacti.Length]);
                    if (s == null)
                        continue;
                    SpriteRenderer sr = TrainSprite(track, "OnKaktus", s, new Vector2(wrapMin + 20f + i * 48f, trackY - 1.3f), 25);
                    sr.color = new Color(0.35f, 0.3f, 0.38f);
                    sr.transform.localScale = Vector3.one * 1.3f;
                    sr.gameObject.AddComponent<WorldScroller>().Configure(1.35f, wrapMin - 4f, wrapWidth);
                }

                // Trenden düşen raylara iner: düşmüş sayılır (son checkpoint'e döner).
                var pit = new GameObject("TrenBoslugu") { layer = 0 };
                pit.transform.SetParent(root);
                pit.transform.position = new Vector2((o + trainEnd + 15f) / 2f, trackY - 1.6f);
                pit.AddComponent<BoxCollider2D>().size = new Vector2(trainEnd + 15f - o, 2.4f);
                pit.AddComponent<PitZone>();

                // --- Yolculuk ---
                var ride = root.gameObject.AddComponent<TrainRide>();
                Set(ride, "trainMinX", o + trainStart);
                Set(ride, "trainMaxX", trainEnd);
                Set(ride, "floorY", trackY + CarFloor);
                Set(ride, "boardCheckpoint", new Vector2(o + trainStart + 5f, trackY + CarFloor + 0.05f));
                var audio = root.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;
                audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tren_ray.wav");
                Set(ride, "railLoop", audio);
                Set(ride, "whistle", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tren_dudugu.wav"));

                // --- Kaza: raylara bağlı dinamit, lokomotif parçaları ---
                var dynamite = new GameObject("Dinamit").transform;
                dynamite.SetParent(root);
                dynamite.position = new Vector2(trainEnd + 30f, trackY);
                TrainSprite(dynamite, "Demet", Tren("tren_dinamit"), dynamite.position, TrainOrder + 3).transform.localScale = Vector3.one * 1.5f;
                SpriteRenderer fuse = TrainSprite(dynamite, "Fitil", Tren("tren_fitil_0"), dynamite.position + new Vector3(0.22f, 0.85f), TrainOrder + 4);
                fuse.transform.localScale = Vector3.one * 1.5f;
                fuse.gameObject.AddComponent<SpriteLoop>().Configure(TrenFrames("tren_fitil"), 16f);

                var pieces = new List<Transform>();
                foreach (var (name, px0, _, px1, py1) in LocoPieces)
                {
                    SpriteRenderer sr = TrainSprite(loco, "Parca_" + name, Tren("tren_lokomotif_" + name),
                        new Vector2(locoLeft + (px0 + px1) / 2f * Px, trackY + (160 - py1) * Px), TrainOrder);
                    sr.gameObject.SetActive(false); // kazada açılır
                    pieces.Add(sr.transform);
                }

                var crash = root.gameObject.AddComponent<TrainCrash>();
                Set(crash, "ride", ride);
                SetArray(crash, "cars", cars);
                SetArray(crash, "locoPieces", pieces);
                Set(crash, "locoLook", loco.Find("Gorsel").gameObject);
                Set(crash, "triggerX", locoLeft - 2.5f);
                Set(crash, "locoFrontX", trainEnd);
                Set(crash, "trackY", trackY);
                Set(crash, "dynamite", dynamite);
                Set(crash, "wakePoint", new Vector2(wakeX, wakeY));
                Set(crash, "explosionFrames", TrenFrames("tren_patlama"));
                Set(crash, "dustFrames", TrenFrames("tren_toz"));
                Set(crash, "stunFrames", TrenFrames("sersem"));
                var getUp = new List<Sprite>();
                for (int i = 0; ; i++)
                {
                    Sprite f = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Player/Gila/kalkis/gila_kalkis_{i}.png");
                    if (f == null)
                        break;
                    getUp.Add(f);
                }
                Set(crash, "getUpFrames", getUp.ToArray());
                Set(crash, "whistleClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tren_dudugu.wav"));
                Set(crash, "brakeClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tren_fren.wav"));
                Set(crash, "fuseClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/fitil.wav"));
                Set(crash, "blastClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/patlama.wav"));
                Set(crash, "crashClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tren_kaza.wav"));

                teleports.Add((trainEnd, wakeX));
                return trainEnd - o;
            }

            Transform PassengerCar(Transform parent, float left, float trackY, int index)
            {
                var car = new GameObject(Name("Vagon")).transform;
                car.SetParent(parent);
                car.position = new Vector2(left + CarWidth / 2f, trackY);
                var look = new GameObject("Gorsel").transform;
                look.SetParent(car, false);
                TrainSprite(look, "Ic", Tren("tren_vagon_ic"), car.position, TrainOrder - 2);
                SpriteRenderer outside = TrainSprite(look, "Dis", Tren("tren_vagon"), car.position, TrainOrder);
                Set(look.gameObject.AddComponent<CarSway>(), "phase", index * 0.37f);
                Set(car.gameObject.AddComponent<TrainCarInterior>(), "outside", outside);
                Wheels(car, "tren_teker", 10f * Px, left, trackY, 42, 62, 258, 278);

                float x1 = left + CarWidth;
                Box(car, "Taban", left, x1, trackY + CarFloor - 0.3f, trackY + CarFloor);
                Box(car, "Dam", left + 14f * Px, x1 - 14f * Px, trackY + CarCeiling, trackY + CarRoof);
                platforms.Add((left, x1, trackY + CarFloor));
                platforms.Add((left + 14f * Px, x1 - 14f * Px, trackY + CarRoof));
                return car;
            }

            Transform TenderCar(Transform parent, float left, float trackY)
            {
                var car = new GameObject(Name("KomurVagonu")).transform;
                car.SetParent(parent);
                car.position = new Vector2(left + TenderWidth / 2f, trackY);
                var look = new GameObject("Gorsel").transform;
                look.SetParent(car, false);
                TrainSprite(look, "Govde", Tren("tren_komur"), car.position, TrainOrder);
                Set(look.gameObject.AddComponent<CarSway>(), "phase", 0.9f);
                Wheels(car, "tren_teker_orta", 13f * Px, left, trackY, 36, 60, 164, 188);
                Box(car, "Govde", left + 6f * Px, left + 218f * Px, trackY + 10f * Px, trackY + TenderTop);
                platforms.Add((left, left + TenderWidth, trackY + TenderTop));
                return car;
            }

            Transform Locomotive(Transform parent, float left, float trackY)
            {
                var car = new GameObject(Name("Lokomotif")).transform;
                car.SetParent(parent);
                car.position = new Vector2(left + LocoWidth / 2f, trackY);
                var look = new GameObject("Gorsel").transform;
                look.SetParent(car, false);
                TrainSprite(look, "Govde", Tren("tren_lokomotif"), car.position, TrainOrder);
                Set(look.gameObject.AddComponent<CarSway>(), "phase", 0.3f);
                Wheels(car, "tren_teker_buyuk", 22f * Px, left, trackY, 150, 200, 250);
                Wheels(car, "tren_teker_orta", 13f * Px, left, trackY, 340, 368);
                Box(car, "Kabin", left + 4f * Px, left + 110f * Px, trackY + 28f * Px, trackY + 152f * Px);
                Box(car, "Kazan", left + 110f * Px, left + 384f * Px, trackY + 28f * Px, trackY + 98f * Px);
                platforms.Add((left, left + 110f * Px, trackY + 152f * Px));
                platforms.Add((left + 110f * Px, left + LocoWidth, trackY + 98f * Px));

                var stack = new GameObject("Baca").transform;
                stack.SetParent(car);
                stack.position = new Vector2(left + 331f * Px, trackY + 160f * Px);
                var smoke = stack.gameObject.AddComponent<TrainSmoke>();
                Set(smoke, "frames", TrenFrames("tren_duman"));
                PointLight(car, new Vector2(left + 380f * Px, trackY + 112f * Px), new Color(1f, 0.85f, 0.55f), 5f, 0.9f, 0f); // fener
                return car;
            }

            /// <summary>Kaza yerindeki enkaz: dönmüş vagon, yere oturtulur (dönmüş gövdenin en alt noktası zeminde). Çarpışmasız.</summary>
            public void TrainWreck(string kind, float x, float groundTop, float angle, bool smoking = false)
            {
                string sprite = kind == "lokomotif" ? "tren_lokomotif" : kind == "komur" ? "tren_komur" : "tren_vagon";
                Sprite s = Tren(sprite);
                float half = s.bounds.extents.x, height = s.bounds.size.y;
                float a = angle * Mathf.Deg2Rad, lowest = 0f;
                foreach (Vector2 c in new[] { new Vector2(-half, 0f), new Vector2(half, 0f), new Vector2(-half, height), new Vector2(half, height) })
                    lowest = Mathf.Min(lowest, c.x * Mathf.Sin(a) + c.y * Mathf.Cos(a));
                var root = new GameObject(Name("Enkaz")).transform;
                root.SetParent(decor);
                root.position = new Vector2(x + OriginX, groundTop - lowest - 0.45f); // toprağa saplanmış
                var dirty = new Color(0.78f, 0.72f, 0.72f); // is ve toz
                TrainSprite(root, sprite, s, root.position, BuildingOrder + 1).color = dirty;
                (string frames, int[] xs)[] wheelSets = kind == "lokomotif"
                    ? new[] { ("tren_teker_buyuk", new[] { 150, 200, 250 }), ("tren_teker_orta", new[] { 340, 368 }) }
                    : kind == "komur" ? new[] { ("tren_teker_orta", new[] { 36, 60, 164, 188 }) }
                    : new[] { ("tren_teker", new[] { 42, 62, 258, 278 }) };
                foreach (var set in wheelSets)
                    foreach (int cx in set.xs)
                        TrainSprite(root, "Teker", Tren(set.frames + "_0"), root.position + new Vector3(cx * Px - half, 0f), BuildingOrder + 2).color = dirty;
                root.rotation = Quaternion.Euler(0f, 0f, angle);
                if (smoking)
                {
                    var stack = new GameObject("Tuten").transform;
                    stack.SetParent(root, false);
                    stack.localPosition = new Vector2(half * 0.4f, height * 0.6f);
                    var smoke = stack.gameObject.AddComponent<TrainSmoke>();
                    Set(smoke, "frames", TrenFrames("tren_duman"));
                    Set(smoke, "idleInterval", 0.55f);
                    Set(smoke, "sortingOrder", BuildingOrder + 2);
                }
                occupied.Add((x + OriginX, half));
                bareGround.Add((x + OriginX - half, x + OriginX + half));
            }

            /// <summary>
            /// Dinamitle parçalanmış lokomotif: kabin, kazan parçaları, baca, şasi ve tekerlekler kaza yerine saçılmış;
            /// kazan tüter. Her parça dönmüş haliyle zemine oturtulur. Çarpışmasız.
            /// </summary>
            public void LocoDebris(float x, float groundTop)
            {
                (string piece, float dx, float angle)[] layout =
                {
                    ("kabin", -5.5f, -14f), ("kazan_arka", -1.8f, 168f), ("kazan_orta", 1.6f, 22f), ("kazan_on", 5.2f, -96f),
                    ("mahmuz", 8.2f, 140f), ("sasi_arka", -2.5f, 6f), ("sasi_on", 3.5f, -9f),
                };
                var dirty = new Color(0.72f, 0.66f, 0.66f);
                foreach (var (piece, dx, angle) in layout)
                {
                    Sprite s = Tren("tren_lokomotif_" + piece);
                    var root = new GameObject(Name("Parca")).transform;
                    root.SetParent(decor);
                    float half = s.bounds.extents.x, height = s.bounds.size.y, a = angle * Mathf.Deg2Rad, lowest = 0f;
                    foreach (Vector2 c in new[] { new Vector2(-half, 0f), new Vector2(half, 0f), new Vector2(-half, height), new Vector2(half, height) })
                        lowest = Mathf.Min(lowest, c.x * Mathf.Sin(a) + c.y * Mathf.Cos(a));
                    root.position = new Vector2(x + dx + OriginX, groundTop - lowest - 0.2f);
                    TrainSprite(root, piece, s, root.position, BuildingOrder + 1).color = dirty;
                    root.rotation = Quaternion.Euler(0f, 0f, angle);
                    if (piece == "kazan_orta" || piece == "kazan_on")
                    {
                        var smoke = new GameObject("Tuten").AddComponent<TrainSmoke>();
                        smoke.transform.SetParent(root, false);
                        smoke.transform.localPosition = new Vector2(0f, height * 0.5f);
                        Set(smoke, "frames", TrenFrames("tren_duman"));
                        Set(smoke, "idleInterval", 0.6f);
                        Set(smoke, "sortingOrder", BuildingOrder + 2);
                    }
                }
                // Kopan tekerlekler: kimi devrik, kimi dik
                float[] wheelsX = { -7.5f, -3.6f, 0.2f, 7f, 10f };
                for (int i = 0; i < wheelsX.Length; i++)
                {
                    Sprite w = Tren(i < 3 ? "tren_teker_buyuk_0" : "tren_teker_orta_0");
                    SpriteRenderer sr = TrainSprite(decor, "Teker", w, new Vector2(x + wheelsX[i] + OriginX, groundTop - 0.1f), BuildingOrder + 3);
                    sr.color = dirty;
                    if (i % 2 == 1)
                        sr.transform.localScale = new Vector3(1f, 0.35f, 1f); // yere yatmış: yandan ince görünür
                }
                occupied.Add((x + OriginX, 8f));
                bareGround.Add((x + OriginX - 8f, x + OriginX + 8f));
            }

            /// <summary>Görünmez, tırmanılmaz duvar (dünya dışına ya da geri dönüşü kapatmak için).</summary>
            public void InvisibleWall(float x, float yMin, float yMax)
            {
                var go = new GameObject(Name("GorunmezDuvar"));
                go.transform.SetParent(geometry);
                go.transform.position = new Vector2(x + OriginX, (yMin + yMax) / 2f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(1f, yMax - yMin);
            }

            bool IsTeleport(float fromX, float toX)
            {
                foreach (var t in teleports)
                    if (Mathf.Abs(t.from - fromX) < 2f && toX <= t.to + 2f && toX >= t.from)
                        return true;
                return false;
            }
        }
    }
}
