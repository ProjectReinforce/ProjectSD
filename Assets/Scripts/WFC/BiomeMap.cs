using UnityEngine;

/// <summary>
/// Perlin Noise 기반 바이옴 맵 생성기.
/// WFC 실행 전에 각 셀의 바이옴 존을 미리 결정합니다.
/// 
/// 동작 원리:
/// 1. Perlin Noise로 0.0~1.0 값의 2D 맵 생성
/// 2. threshold 값으로 3개 존(Dark/Main/Glow)으로 분류
/// 3. WFC collapse 시 해당 존과 일치하는 타일 가중치 부스트
/// 
/// 노이즈 스케일이 작을수록 → 큰 덩어리
/// 노이즈 스케일이 클수록 → 잘게 쪼개진 구역
/// </summary>
public class BiomeMap
{
    private readonly BiomeZone[,] zones;
    private readonly float[,] rawNoise;  // 디버그용 원본 노이즈 값
    
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// 바이옴 맵 생성
    /// </summary>
    /// <param name="width">맵 너비</param>
    /// <param name="height">맵 높이</param>
    /// <param name="noiseScale">노이즈 스케일 (작을수록 큰 덩어리, 권장: 0.05~0.15)</param>
    /// <param name="darkThreshold">Dark 존 상한 (0.0 ~ 이 값)</param>
    /// <param name="glowThreshold">Glow 존 하한 (이 값 ~ 1.0)</param>
    /// <param name="seed">시드 값 (노이즈 오프셋에 사용)</param>
    public BiomeMap(int width, int height, float noiseScale, 
                    float darkThreshold, float glowThreshold, int seed)
    {
        Width = width;
        Height = height;
        zones = new BiomeZone[width, height];
        rawNoise = new float[width, height];

        // 시드를 오프셋으로 변환 (Perlin Noise는 시드가 없으므로 위치를 이동)
        System.Random rng = new System.Random(seed);
        float offsetX = (float)(rng.NextDouble() * 10000);
        float offsetY = (float)(rng.NextDouble() * 10000);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx = x * noiseScale + offsetX;
                float ny = y * noiseScale + offsetY;
                float noise = Mathf.PerlinNoise(nx, ny);

                // 0~1 범위로 클램프 (Perlin Noise는 가끔 살짝 벗어남)
                noise = Mathf.Clamp01(noise);
                rawNoise[x, y] = noise;

                // 존 분류
                if (noise < darkThreshold)
                    zones[x, y] = BiomeZone.Dark;
                else if (noise > glowThreshold)
                    zones[x, y] = BiomeZone.Glow;
                else
                    zones[x, y] = BiomeZone.Main;
            }
        }
    }

    /// <summary>
    /// 해당 셀의 바이옴 존 반환
    /// </summary>
    public BiomeZone GetZone(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return BiomeZone.Main;  // 범위 밖은 기본 존
        return zones[x, y];
    }

    /// <summary>
    /// 해당 셀의 원본 노이즈 값 반환 (디버그용)
    /// </summary>
    public float GetRawNoise(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return 0.5f;
        return rawNoise[x, y];
    }

    /// <summary>
    /// 특정 타일의 바이옴 존이 해당 셀의 존과 일치하는지 확인.
    /// biomeZone이 None이면 항상 false (부스트 대상 아님).
    /// </summary>
    public bool IsMatchingZone(int x, int y, BiomeZone tileZone)
    {
        if (tileZone == BiomeZone.None) return false;
        return GetZone(x, y) == tileZone;
    }

    /// <summary>
    /// 존 분포 통계 로그 출력 (디버그용)
    /// </summary>
    public void LogStats()
    {
        int dark = 0, main = 0, glow = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                switch (zones[x, y])
                {
                    case BiomeZone.Dark: dark++; break;
                    case BiomeZone.Main: main++; break;
                    case BiomeZone.Glow: glow++; break;
                }
            }
        }

        int total = Width * Height;
        Debug.Log($"[BiomeMap] 존 분포 - Dark: {dark} ({100f * dark / total:F1}%), " +
                  $"Main: {main} ({100f * main / total:F1}%), " +
                  $"Glow: {glow} ({100f * glow / total:F1}%)");
    }
}