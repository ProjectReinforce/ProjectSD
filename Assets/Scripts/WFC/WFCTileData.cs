using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// WFC 타일 하나의 정보를 담는 ScriptableObject.
/// Project 창에서 우클릭 → Create → WFC → Tile Data 로 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewWFCTile", menuName = "WFC/Tile Data")]
public class WFCTileData : ScriptableObject
{
    [Header("타일맵에 칠할 타일")]
    public TileBase tile;

    [Header("이 타일의 가중치 (높을수록 자주 등장)")]
    [Range(1, 100)]
    public int weight = 10;

    [Header("바이옴 존 (노이즈 맵 기반 클러스터링)")]
    [Tooltip("None = 바이옴 부스트 없이 어디서나 기본 확률로 등장\n" +
             "Dark/Main/Glow = 해당 존에서 가중치 부스트")]
    public BiomeZone biomeZone = BiomeZone.None;

    [Header("인접 규칙 - 각 방향에 올 수 있는 타일")]
    [Tooltip("이 타일의 위쪽에 올 수 있는 타일들")]
    public WFCTileData[] allowedTop;

    [Tooltip("이 타일의 아래쪽에 올 수 있는 타일들")]
    public WFCTileData[] allowedBottom;

    [Tooltip("이 타일의 왼쪽에 올 수 있는 타일들")]
    public WFCTileData[] allowedLeft;

    [Tooltip("이 타일의 오른쪽에 올 수 있는 타일들")]
    public WFCTileData[] allowedRight;

    /// <summary>
    /// 특정 방향에서 허용된 타일 목록 반환
    /// </summary>
    public WFCTileData[] GetAllowed(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return allowedTop;
        if (direction == Vector2Int.down) return allowedBottom;
        if (direction == Vector2Int.left) return allowedLeft;
        if (direction == Vector2Int.right) return allowedRight;
        return new WFCTileData[0];
    }
}

/// <summary>
/// 바이옴 존 분류.
/// Perlin Noise 값에 따라 맵의 각 셀이 하나의 존에 배정되고,
/// 해당 존과 일치하는 biomeZone을 가진 타일의 가중치가 부스트됩니다.
/// </summary>
public enum BiomeZone
{
    None,   // 바이옴 부스트 없음 (전환 타일, 범용 타일에 적합)
    Dark,   // 어두운 구역 (노이즈 0.0 ~ darkThreshold)
    Main,   // 메인 구역   (노이즈 darkThreshold ~ glowThreshold)
    Glow    // 발광 구역   (노이즈 glowThreshold ~ 1.0)
}