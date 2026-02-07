using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Physics;

public class NavGrid
{
    private readonly bool[,] _walkable;
    private readonly float[,] _height;
    private readonly float _cellSize;
    private readonly float _originX;
    private readonly float _originY;
    private readonly int _width;
    private readonly int _depth;
    private readonly float _maxStepHeight;

    public int Width => _width;
    public int Depth => _depth;
    public float CellSize => _cellSize;
    public bool IsBuilt { get; private set; }

    public NavGrid(float cellSize = 2.0f, float maxStepHeight = 1.5f)
    {
        _cellSize = cellSize;
        _maxStepHeight = maxStepHeight;
        _width = 0;
        _depth = 0;
        _walkable = new bool[0, 0];
        _height = new float[0, 0];
    }

    private NavGrid(int width, int depth, float cellSize, float originX, float originY, float maxStepHeight)
    {
        _cellSize = cellSize;
        _maxStepHeight = maxStepHeight;
        _originX = originX;
        _originY = originY;
        _width = width;
        _depth = depth;
        _walkable = new bool[width, depth];
        _height = new float[width, depth];
    }

    /// <summary>
    /// Build a navigation grid centered on the given position by sampling ground heights.
    /// </summary>
    public static NavGrid Build(PhysicsEngine physics, Vector3 center, float radius, float cellSize = 2.0f, float maxStepHeight = 1.5f)
    {
        int halfCells = (int)(radius / cellSize);
        int size = halfCells * 2 + 1;
        float originX = center.X - halfCells * cellSize;
        float originY = center.Y - halfCells * cellSize;

        var grid = new NavGrid(size, size, cellSize, originX, originY, maxStepHeight);

        // Sample ground heights
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float worldX = originX + x * cellSize;
                float worldY = originY + y * cellSize;
                var samplePos = new Vector3(worldX, worldY, center.Z);

                float? groundZ = physics.SampleGroundHeight(samplePos);
                if (groundZ.HasValue)
                {
                    grid._height[x, y] = groundZ.Value;
                    grid._walkable[x, y] = true;
                }
                else
                {
                    grid._height[x, y] = float.MinValue;
                    grid._walkable[x, y] = false; // No ground = void/cliff
                }
            }
        }

        // Mark cells with too-steep transitions as unwalkable
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (!grid._walkable[x, y]) continue;

                // Check if all neighbors have extreme height differences (isolated pillar)
                bool hasWalkableNeighbor = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                        if (!grid._walkable[nx, ny]) continue;

                        float heightDiff = MathF.Abs(grid._height[x, y] - grid._height[nx, ny]);
                        if (heightDiff <= maxStepHeight)
                        {
                            hasWalkableNeighbor = true;
                            break;
                        }
                    }
                    if (hasWalkableNeighbor) break;
                }

                if (!hasWalkableNeighbor)
                    grid._walkable[x, y] = false;
            }
        }

        grid.IsBuilt = true;
        int walkableCount = 0;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (grid._walkable[x, y]) walkableCount++;

        Console.WriteLine($"[NavGrid] Built {size}x{size} grid ({walkableCount} walkable cells) centered at ({center.X:F0},{center.Y:F0})");
        return grid;
    }

    /// <summary>
    /// Find a path from start to goal using A*. Returns empty list if no path found.
    /// </summary>
    public List<Vector3> FindPath(Vector3 start, Vector3 goal)
    {
        if (!IsBuilt) return new List<Vector3>();

        var startCell = WorldToCell(start);
        var goalCell = WorldToCell(goal);

        if (!IsValidCell(startCell.x, startCell.y) || !IsValidCell(goalCell.x, goalCell.y))
            return new List<Vector3>();

        // Snap to nearest walkable if start/goal are on unwalkable cells
        if (!_walkable[startCell.x, startCell.y])
            startCell = FindNearestWalkableCell(startCell.x, startCell.y);
        if (!_walkable[goalCell.x, goalCell.y])
            goalCell = FindNearestWalkableCell(goalCell.x, goalCell.y);

        if (startCell.x < 0 || goalCell.x < 0)
            return new List<Vector3>();

        if (startCell.x == goalCell.x && startCell.y == goalCell.y)
            return new List<Vector3> { CellToWorld(goalCell.x, goalCell.y) };

        // A* search
        var openSet = new PriorityQueue<(int x, int y), float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), float>();
        var closedSet = new HashSet<(int, int)>();

        var startKey = (startCell.x, startCell.y);
        var goalKey = (goalCell.x, goalCell.y);

        gScore[startKey] = 0;
        openSet.Enqueue(startKey, Heuristic(startCell.x, startCell.y, goalCell.x, goalCell.y));

        int maxIterations = _width * _depth; // Safety limit
        int iterations = 0;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            var current = openSet.Dequeue();

            if (current == goalKey)
            {
                // Reconstruct path
                return ReconstructPath(cameFrom, current);
            }

            closedSet.Add(current);

            // Check 8 neighbors
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = current.Item1 + dx;
                    int ny = current.Item2 + dy;
                    var neighborKey = (nx, ny);

                    if (!IsValidCell(nx, ny) || !_walkable[nx, ny] || closedSet.Contains(neighborKey))
                        continue;

                    // Check step height
                    float heightDiff = MathF.Abs(_height[current.Item1, current.Item2] - _height[nx, ny]);
                    if (heightDiff > _maxStepHeight)
                        continue;

                    // Diagonal movement costs more
                    float moveCost = (dx != 0 && dy != 0) ? 1.414f : 1.0f;
                    float tentativeG = gScore[current] + moveCost;

                    if (!gScore.TryGetValue(neighborKey, out var existingG) || tentativeG < existingG)
                    {
                        cameFrom[neighborKey] = current;
                        gScore[neighborKey] = tentativeG;
                        float fScore = tentativeG + Heuristic(nx, ny, goalCell.x, goalCell.y);
                        openSet.Enqueue(neighborKey, fScore);
                    }
                }
            }
        }

        // No path found
        return new List<Vector3>();
    }

    /// <summary>
    /// Get the nearest walkable world position to the given position.
    /// </summary>
    public Vector3? GetNearestWalkable(Vector3 pos)
    {
        if (!IsBuilt) return null;
        var cell = WorldToCell(pos);
        if (IsValidCell(cell.x, cell.y) && _walkable[cell.x, cell.y])
            return CellToWorld(cell.x, cell.y);

        var nearest = FindNearestWalkableCell(cell.x, cell.y);
        if (nearest.x < 0) return null;
        return CellToWorld(nearest.x, nearest.y);
    }

    /// <summary>
    /// Check if a position is on walkable ground.
    /// </summary>
    public bool IsWalkable(Vector3 pos)
    {
        if (!IsBuilt) return true; // Default to walkable if no grid
        var cell = WorldToCell(pos);
        return IsValidCell(cell.x, cell.y) && _walkable[cell.x, cell.y];
    }

    private (int x, int y) WorldToCell(Vector3 worldPos)
    {
        int x = (int)((worldPos.X - _originX) / _cellSize + 0.5f);
        int y = (int)((worldPos.Y - _originY) / _cellSize + 0.5f);
        return (x, y);
    }

    private Vector3 CellToWorld(int x, int y)
    {
        float worldX = _originX + x * _cellSize;
        float worldY = _originY + y * _cellSize;
        float worldZ = (IsValidCell(x, y) && _walkable[x, y]) ? _height[x, y] : 0f;
        return new Vector3(worldX, worldY, worldZ);
    }

    private bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < _width && y >= 0 && y < _depth;
    }

    private (int x, int y) FindNearestWalkableCell(int cx, int cy)
    {
        // Spiral outward search
        int maxRadius = Math.Max(_width, _depth) / 2;
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // Only check perimeter
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (IsValidCell(nx, ny) && _walkable[nx, ny])
                        return (nx, ny);
                }
            }
        }
        return (-1, -1);
    }

    private List<Vector3> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int, int) current)
    {
        var path = new List<Vector3>();
        path.Add(CellToWorld(current.Item1, current.Item2));

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(CellToWorld(current.Item1, current.Item2));
        }

        path.Reverse();

        // Simplify path — remove collinear intermediate waypoints
        if (path.Count > 2)
        {
            var simplified = new List<Vector3> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
            {
                var prev = simplified[^1];
                var next = path[i + 1];
                var dirA = Vector3.Normalize(path[i] - prev);
                var dirB = Vector3.Normalize(next - path[i]);
                // If direction changes significantly, keep the waypoint
                if (Vector3.Dot(dirA, dirB) < 0.95f)
                    simplified.Add(path[i]);
            }
            simplified.Add(path[^1]);
            return simplified;
        }

        return path;
    }

    private static float Heuristic(int x1, int y1, int x2, int y2)
    {
        // Octile distance (allows diagonal movement)
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
    }
}
