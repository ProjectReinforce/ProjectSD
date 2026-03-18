using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// WFC 맵 생성 에디터 윈도우.
/// Unity 메뉴 → Tools → WFC Map Generator 로 열기.
/// 
/// v2: Perlin Noise 바이옴 클러스터링 지원 추가.
/// 바이옴을 활성화하면 노이즈 맵으로 존을 나누고,
/// 각 존에 맞는 타일이 더 자주 등장하여 자연스러운 지형 덩어리가 형성됩니다.
/// </summary>
public class WFCEditorWindow : EditorWindow
{
    // ─── 기본 설정 ───
    private Tilemap targetTilemap;
    private int mapWidth = 50;
    private int mapHeight = 50;
    private int seed = 0;
    private bool useRandomSeed = true;

    // 타일 데이터 목록
    private WFCTileData[] tileDataList = new WFCTileData[0];

    // 경계 타일
    private WFCTileData borderTile;

    // ─── 바이옴 설정 ───
    private bool useBiome = true;
    private float noiseScale = 0.08f;
    private float darkThreshold = 0.35f;
    private float glowThreshold = 0.65f;
    private float biomeBoost = 3.0f;

    // ─── UI 상태 ───
    private Vector2 scrollPos;
    private bool foldoutTiles = true;
    private bool foldoutBiome = true;

    // ─── 디버그 ───
    private Texture2D biomePreview;

    [MenuItem("Tools/WFC Map Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<WFCEditorWindow>("WFC 맵 생성기");
        window.minSize = new Vector2(350, 600);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("WFC 타일맵 생성기 v2", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // ── 타일맵 대상 ──
        EditorGUILayout.LabelField("1. 대상 설정", EditorStyles.boldLabel);
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField(
            "타일맵", targetTilemap, typeof(Tilemap), true);

        EditorGUILayout.Space(5);
        mapWidth = EditorGUILayout.IntSlider("맵 너비", mapWidth, 10, 200);
        mapHeight = EditorGUILayout.IntSlider("맵 높이", mapHeight, 10, 200);

        EditorGUILayout.Space(10);

        // ── 시드 ──
        EditorGUILayout.LabelField("2. 시드 설정", EditorStyles.boldLabel);
        useRandomSeed = EditorGUILayout.Toggle("랜덤 시드", useRandomSeed);
        if (!useRandomSeed)
        {
            seed = EditorGUILayout.IntField("시드 값", seed);
        }

        EditorGUILayout.Space(10);

        // ── 타일 데이터 목록 ──
        foldoutTiles = EditorGUILayout.Foldout(foldoutTiles, "3. 타일 데이터 목록", true);
        if (foldoutTiles)
        {
            EditorGUI.indentLevel++;

            int newSize = EditorGUILayout.IntField("타일 종류 수", tileDataList.Length);
            if (newSize != tileDataList.Length)
            {
                System.Array.Resize(ref tileDataList, newSize);
            }

            for (int i = 0; i < tileDataList.Length; i++)
            {
                tileDataList[i] = (WFCTileData)EditorGUILayout.ObjectField(
                    $"타일 {i}", tileDataList[i], typeof(WFCTileData), false);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);

        // ── 경계 타일 ──
        borderTile = (WFCTileData)EditorGUILayout.ObjectField(
            "경계 타일 (선택사항)", borderTile, typeof(WFCTileData), false);

        EditorGUILayout.Space(10);

        // ── 바이옴 설정 ──
        foldoutBiome = EditorGUILayout.Foldout(foldoutBiome, "4. 바이옴 클러스터링", true);
        if (foldoutBiome)
        {
            EditorGUI.indentLevel++;

            useBiome = EditorGUILayout.Toggle("바이옴 활성화", useBiome);

            if (useBiome)
            {
                EditorGUILayout.Space(3);

                noiseScale = EditorGUILayout.Slider(
                    new GUIContent("노이즈 스케일", 
                        "작을수록 큰 덩어리, 클수록 잘게 쪼개짐\n" +
                        "권장: 0.05(아주 큰) ~ 0.15(중간) ~ 0.3(잘게)"),
                    noiseScale, 0.01f, 0.5f);

                darkThreshold = EditorGUILayout.Slider(
                    new GUIContent("Dark 존 상한",
                        "노이즈 0.0 ~ 이 값 = Dark 존"),
                    darkThreshold, 0.1f, 0.5f);

                glowThreshold = EditorGUILayout.Slider(
                    new GUIContent("Glow 존 하한",
                        "이 값 ~ 1.0 = Glow 존"),
                    glowThreshold, 0.5f, 0.9f);

                biomeBoost = EditorGUILayout.Slider(
                    new GUIContent("부스트 배율",
                        "존 매칭 타일의 가중치 곱하기\n" +
                        "2.0 = 2배, 5.0 = 5배 (높을수록 구역이 뚜렷)"),
                    biomeBoost, 1.0f, 10.0f);

                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox(
                    $"현재 존 비율 추정:\n" +
                    $"  Dark: ~{darkThreshold * 100:F0}%  |  " +
                    $"Main: ~{(glowThreshold - darkThreshold) * 100:F0}%  |  " +
                    $"Glow: ~{(1f - glowThreshold) * 100:F0}%\n\n" +
                    "각 타일의 biomeZone을 설정하세요:\n" +
                    "  None = 어디서나 기본 확률 (전환 타일)\n" +
                    "  Dark/Main/Glow = 해당 존에서 부스트",
                    MessageType.Info);

                EditorGUILayout.Space(3);

                // 프리뷰 버튼
                if (GUILayout.Button("노이즈 맵 미리보기", GUILayout.Height(25)))
                {
                    GenerateBiomePreview();
                }

                if (biomePreview != null)
                {
                    EditorGUILayout.Space(3);
                    float previewSize = Mathf.Min(EditorGUIUtility.currentViewWidth - 40, 200);
                    Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
                    EditorGUI.DrawPreviewTexture(previewRect, biomePreview);

                    EditorGUILayout.BeginHorizontal();
                    DrawColorLabel(new Color(0.2f, 0.1f, 0.3f), "Dark");
                    DrawColorLabel(new Color(0.3f, 0.5f, 0.4f), "Main");
                    DrawColorLabel(new Color(0.4f, 0.9f, 0.8f), "Glow");
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(20);

        // ── 버튼 ──
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("맵 생성", GUILayout.Height(40)))
        {
            GenerateMap();
        }

        GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
        if (GUILayout.Button("타일맵 비우기", GUILayout.Height(40)))
        {
            ClearTilemap();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "사용법:\n" +
            "1. Project에서 WFCTileData 에셋을 타일 종류별로 생성\n" +
            "2. 각 에셋의 인접 규칙 + biomeZone 설정\n" +
            "3. 씬에 Grid → Tilemap 오브젝트 배치\n" +
            "4. 위 설정 후 '맵 생성' 클릭\n" +
            "5. 마음에 들면 Ctrl+S로 씬 저장!",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void GenerateMap()
    {
        // ── 검증 ──
        if (targetTilemap == null)
        {
            EditorUtility.DisplayDialog("오류", "타일맵을 지정해주세요!", "확인");
            return;
        }

        var validTiles = System.Array.FindAll(tileDataList, t => t != null);

        if (validTiles.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "타일 데이터를 1개 이상 추가해주세요!", "확인");
            return;
        }

        // ── Undo 등록 ──
        Undo.RecordObject(targetTilemap, "WFC 맵 생성");

        // ── 시드 결정 ──
        int useSeed = useRandomSeed ? Random.Range(0, int.MaxValue) : seed;
        Debug.Log($"[WFC] 시드: {useSeed} / 크기: {mapWidth}x{mapHeight}" +
                  (useBiome ? $" / 바이옴: ON (스케일={noiseScale}, 부스트={biomeBoost}x)" : " / 바이옴: OFF"));

        // ── 바이옴 맵 생성 (프리패스) ──
        BiomeMap biomeMap = null;
        if (useBiome)
        {
            biomeMap = new BiomeMap(mapWidth, mapHeight, noiseScale, 
                                    darkThreshold, glowThreshold, useSeed);
            biomeMap.LogStats();
        }

        // ── WFC 실행 ──
        var generator = new WFCGenerator(mapWidth, mapHeight, validTiles);

        if (biomeMap != null)
        {
            generator.SetBiomeMap(biomeMap, biomeBoost);
        }

        WFCTileData[,] map = generator.Generate(useSeed);

        if (map == null)
        {
            // 모순 발생 시 최대 5회 재시도
            for (int retry = 0; retry < 5; retry++)
            {
                useSeed = Random.Range(0, int.MaxValue);
                Debug.Log($"[WFC] 재시도 {retry + 1}/5, 새 시드: {useSeed}");

                // 바이옴 맵도 새 시드로 재생성
                if (useBiome)
                {
                    biomeMap = new BiomeMap(mapWidth, mapHeight, noiseScale, 
                                            darkThreshold, glowThreshold, useSeed);
                }

                generator = new WFCGenerator(mapWidth, mapHeight, validTiles);
                if (biomeMap != null)
                    generator.SetBiomeMap(biomeMap, biomeBoost);

                map = generator.Generate(useSeed);

                if (map != null) break;
            }

            if (map == null)
            {
                EditorUtility.DisplayDialog("실패",
                    "5회 시도했으나 맵 생성에 실패했습니다.\n" +
                    "타일 인접 규칙이 너무 엄격하진 않은지 확인해주세요.\n" +
                    "바이옴 부스트가 너무 높으면 모순이 발생할 수 있습니다.",
                    "확인");
                return;
            }
        }

        // ── 타일맵에 적용 ──
        targetTilemap.ClearAllTiles();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                WFCTileData tileData = map[x, y];

                // 경계 타일 처리
                if (borderTile != null && IsBorder(x, y))
                {
                    tileData = borderTile;
                }

                if (tileData != null && tileData.tile != null)
                {
                    targetTilemap.SetTile(new Vector3Int(x, y, 0), tileData.tile);
                }
            }
        }

        // ── 완료 ──
        targetTilemap.RefreshAllTiles();
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log($"[WFC] 맵 생성 완료! 시드: {useSeed}");
        EditorUtility.DisplayDialog("완료",
            $"맵 생성 완료!\n시드: {useSeed}\n\n" +
            "마음에 드시면 Ctrl+S로 씬을 저장하세요.\n" +
            "마음에 안 들면 Ctrl+Z로 되돌린 후 다시 생성하세요.",
            "확인");
    }

    /// <summary>
    /// 노이즈 맵 프리뷰 텍스처 생성
    /// </summary>
    private void GenerateBiomePreview()
    {
        int previewSeed = useRandomSeed ? Random.Range(0, int.MaxValue) : seed;
        var preview = new BiomeMap(mapWidth, mapHeight, noiseScale, 
                                   darkThreshold, glowThreshold, previewSeed);

        int texSize = 128;
        biomePreview = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);
        biomePreview.filterMode = FilterMode.Point;

        Color darkColor = new Color(0.2f, 0.1f, 0.3f);   // 보라 어두움
        Color mainColor = new Color(0.3f, 0.5f, 0.4f);    // 청록 중간
        Color glowColor = new Color(0.4f, 0.9f, 0.8f);    // 청록 밝음

        for (int px = 0; px < texSize; px++)
        {
            for (int py = 0; py < texSize; py++)
            {
                int mapX = Mathf.FloorToInt((float)px / texSize * mapWidth);
                int mapY = Mathf.FloorToInt((float)py / texSize * mapHeight);
                mapX = Mathf.Clamp(mapX, 0, mapWidth - 1);
                mapY = Mathf.Clamp(mapY, 0, mapHeight - 1);

                BiomeZone zone = preview.GetZone(mapX, mapY);
                Color color = zone switch
                {
                    BiomeZone.Dark => darkColor,
                    BiomeZone.Glow => glowColor,
                    _ => mainColor
                };

                biomePreview.SetPixel(px, py, color);
            }
        }

        biomePreview.Apply();
        Debug.Log($"[BiomeMap] 프리뷰 생성 (시드: {previewSeed})");
    }

    /// <summary>
    /// 범례 색상 라벨 그리기
    /// </summary>
    private void DrawColorLabel(Color color, string label)
    {
        Rect rect = GUILayoutUtility.GetRect(60, 16);
        Rect colorRect = new Rect(rect.x, rect.y + 2, 12, 12);
        Rect labelRect = new Rect(rect.x + 16, rect.y, 44, 16);

        EditorGUI.DrawRect(colorRect, color);
        EditorGUI.LabelField(labelRect, label);
    }

    private bool IsBorder(int x, int y)
    {
        return x == 0 || x == mapWidth - 1 || y == 0 || y == mapHeight - 1;
    }

    private void ClearTilemap()
    {
        if (targetTilemap == null)
        {
            EditorUtility.DisplayDialog("오류", "타일맵을 지정해주세요!", "확인");
            return;
        }

        Undo.RecordObject(targetTilemap, "타일맵 비우기");
        targetTilemap.ClearAllTiles();
        EditorUtility.SetDirty(targetTilemap);

        Debug.Log("[WFC] 타일맵 비우기 완료");
    }

    private void OnDestroy()
    {
        if (biomePreview != null)
        {
            DestroyImmediate(biomePreview);
        }
    }
}