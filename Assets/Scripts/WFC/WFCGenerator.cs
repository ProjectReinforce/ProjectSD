using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Wave Function Collapse 알고리즘 핵심 로직.
/// 에디터에서만 사용하며, 런타임 코드에는 포함되지 않음.
/// 
/// v2: Perlin Noise 바이옴 맵 지원 추가.
/// 바이옴 맵이 제공되면, 각 셀의 존과 일치하는 타일의 가중치를 부스트합니다.
/// 인접 규칙은 그대로 유지되며, "선호도"만 존별로 변경됩니다.
/// </summary>
public class WFCGenerator
{
    // ─── 설정 ───
    private readonly int width;
    private readonly int height;
    private readonly WFCTileData[] allTiles;

    // ─── 바이옴 ───
    private BiomeMap biomeMap;
    private float biomeBoost = 3.0f;  // 존 매칭 시 가중치 곱하기 배율

    // ─── 내부 상태 ───
    private HashSet<WFCTileData>[,] possibilities;
    private WFCTileData[,] result;

    // 4방향
    private static readonly Vector2Int[] Directions = 
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // 반대 방향 매핑
    private static readonly Dictionary<Vector2Int, Vector2Int> Opposite = new()
    {
        { Vector2Int.up, Vector2Int.down },
        { Vector2Int.down, Vector2Int.up },
        { Vector2Int.left, Vector2Int.right },
        { Vector2Int.right, Vector2Int.left }
    };

    public WFCGenerator(int width, int height, WFCTileData[] allTiles)
    {
        this.width = width;
        this.height = height;
        this.allTiles = allTiles;
    }

    /// <summary>
    /// 바이옴 맵 설정. Generate() 호출 전에 설정해야 합니다.
    /// </summary>
    /// <param name="map">Perlin Noise로 생성된 바이옴 맵</param>
    /// <param name="boost">존 매칭 시 가중치 배율 (기본 3.0 = 3배)</param>
    public void SetBiomeMap(BiomeMap map, float boost = 3.0f)
    {
        biomeMap = map;
        biomeBoost = Mathf.Max(1.0f, boost);  // 최소 1배 (부스트 없는 것과 동일)
    }

    /// <summary>
    /// WFC 실행. 성공하면 결과 2D 배열 반환, 실패하면 null.
    /// </summary>
    public WFCTileData[,] Generate(int? seed = null)
    {
        System.Random rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        // ── 1단계: 초기화 (모든 셀 = 모든 가능성) ──
        possibilities = new HashSet<WFCTileData>[width, height];
        result = new WFCTileData[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                possibilities[x, y] = new HashSet<WFCTileData>(allTiles);
            }
        }

        // ── 2단계: 반복 (엔트로피 가장 낮은 셀 → 붕괴 → 전파) ──
        int maxIterations = width * height;

        for (int i = 0; i < maxIterations; i++)
        {
            Vector2Int? cell = FindLowestEntropyCell(rng);

            if (cell == null)
            {
                // 모든 셀이 결정됨 → 완성!
                break;
            }

            WFCTileData chosen = CollapseCell(cell.Value, rng);

            if (chosen == null)
            {
                Debug.LogWarning($"WFC 모순 발생: ({cell.Value.x}, {cell.Value.y})");
                return null;
            }

            result[cell.Value.x, cell.Value.y] = chosen;
            Propagate(cell.Value);
        }

        return result;
    }

    /// <summary>
    /// 아직 결정되지 않은 셀 중 가능성이 가장 적은 셀을 찾음.
    /// 동점이면 랜덤 선택 (노이즈 추가).
    /// </summary>
    private Vector2Int? FindLowestEntropyCell(System.Random rng)
    {
        int minEntropy = int.MaxValue;
        List<Vector2Int> candidates = new();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (result[x, y] != null) continue;

                int entropy = possibilities[x, y].Count;

                if (entropy < minEntropy)
                {
                    minEntropy = entropy;
                    candidates.Clear();
                    candidates.Add(new Vector2Int(x, y));
                }
                else if (entropy == minEntropy)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    /// <summary>
    /// 셀을 하나의 타일로 붕괴. 
    /// 바이옴 맵이 있으면 존 매칭 타일의 가중치를 부스트합니다.
    /// 
    /// 예: biomeBoost = 3.0, 셀이 Glow 존일 때
    /// - biomeZone=Glow 타일 (weight 10) → 실효 가중치 30
    /// - biomeZone=Main 타일 (weight 10) → 실효 가중치 10
    /// - biomeZone=None 타일 (weight 10) → 실효 가중치 10
    /// </summary>
    private WFCTileData CollapseCell(Vector2Int cell, System.Random rng)
    {
        var possible = possibilities[cell.x, cell.y];

        if (possible.Count == 0) return null;

        // 실효 가중치 계산 (바이옴 부스트 적용)
        float totalWeight = 0f;
        foreach (var tile in possible)
        {
            totalWeight += GetEffectiveWeight(tile, cell.x, cell.y);
        }

        // float 기반 가중치 선택
        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;

        foreach (var tile in possible)
        {
            cumulative += GetEffectiveWeight(tile, cell.x, cell.y);
            if (roll < cumulative)
            {
                possibilities[cell.x, cell.y] = new HashSet<WFCTileData> { tile };
                return tile;
            }
        }

        // fallback
        var fallback = possible.First();
        possibilities[cell.x, cell.y] = new HashSet<WFCTileData> { fallback };
        return fallback;
    }

    /// <summary>
    /// 타일의 실효 가중치 계산.
    /// 바이옴 맵이 없거나 타일의 biomeZone이 None이면 기본 weight 반환.
    /// 존이 매칭되면 weight × biomeBoost 반환.
    /// </summary>
    private float GetEffectiveWeight(WFCTileData tile, int x, int y)
    {
        float baseWeight = tile.weight;

        if (biomeMap != null && biomeMap.IsMatchingZone(x, y, tile.biomeZone))
        {
            return baseWeight * biomeBoost;
        }

        return baseWeight;
    }

    /// <summary>
    /// 제약 조건 전파: 결정된 셀의 영향을 주변으로 퍼뜨림.
    /// BFS 방식으로 연쇄적으로 전파.
    /// </summary>
    private void Propagate(Vector2Int startCell)
    {
        Queue<Vector2Int> queue = new();
        queue.Enqueue(startCell);

        HashSet<Vector2Int> visited = new();

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in Directions)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x < 0 || neighbor.x >= width ||
                    neighbor.y < 0 || neighbor.y >= height)
                    continue;

                if (result[neighbor.x, neighbor.y] != null) continue;

                HashSet<WFCTileData> allowedForNeighbor = new();

                foreach (var possibleTile in possibilities[current.x, current.y])
                {
                    var allowed = possibleTile.GetAllowed(dir);
                    if (allowed != null)
                    {
                        foreach (var a in allowed)
                            allowedForNeighbor.Add(a);
                    }
                }

                int beforeCount = possibilities[neighbor.x, neighbor.y].Count;
                possibilities[neighbor.x, neighbor.y].IntersectWith(allowedForNeighbor);
                int afterCount = possibilities[neighbor.x, neighbor.y].Count;

                if (afterCount < beforeCount && !visited.Contains(neighbor))
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }
    }
}