using GcodeViewer.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using static GcodeViewer.Geometry.CfsGeometry;

namespace GcodeViewer.Spiral;

internal sealed class CfsFamily
{
    internal List<LineString> Contours { get; } = new();
    internal List<CfsFamily> Branches { get; } = new();
}

internal static class CfsContourTree
{
    private readonly record struct Node(int Level, int Index) : IComparable<Node>
    {
        public int CompareTo(Node b) => Level == b.Level ? Index.CompareTo(b.Index) : Level.CompareTo(b.Level);
    }
    internal static CfsFamily Build(Polygon polygon, double distance, CancellationToken token, out int contourCount)
    {
        var levels = Levels(polygon, distance, token);
        contourCount = levels.Values.Sum(x => x.Count);
        var map = levels.SelectMany(l => l.Value.Select((r, i) => (Node: new Node(l.Key, i), Ring: r)))
            .ToDictionary(x => x.Node, x => x.Ring);
        if (map.Count == 0) return new CfsFamily();

        var edges = new Dictionary<Node, List<(Node Next, double Weight)>>();
        void Edge(Node a, Node b, double w)
        {
            if (!edges.ContainsKey(a)) edges[a] = new();
            if (!edges.ContainsKey(b)) edges[b] = new();
            edges[a].Add((b, w)); edges[b].Add((a, w));
        }

        // When two contours at the same distance-field level approach at the
        // medial axis (most notably an exterior contour approaching a hole),
        // they are neighbours in the contour graph as well.  The reference
        // implementation tests sharing parts in both directions.
        foreach (int level in levels.Keys.Order())
        {
            var rings = levels[level];
            for (int i = 0; i < rings.Count; i++)
                for (int j = i + 1; j < rings.Count; j++)
                {
                    token.ThrowIfCancellationRequested();
                    if (rings[i].Distance(rings[j]) > distance * 1.05) continue;
                    double first = SharedLength(rings[i], rings[j], distance);
                    double second = SharedLength(rings[j], rings[i], distance);
                    if (first > 1e-8 && second > 1e-8)
                        Edge(new Node(level, i), new Node(level, j), first + second);
                }
        }

        foreach (int level in levels.Keys.Order())
        {
            if (!levels.TryGetValue(level + 1, out var inner)) continue;
            for (int i = 0; i < levels[level].Count; i++)
                for (int j = 0; j < inner.Count; j++)
                {
                    token.ThrowIfCancellationRequested();
                    var outerRing = levels[level][i];
                    var innerRing = inner[j];
                    if (outerRing.Distance(innerRing) > distance * 1.05) continue;
                    double first = SharedLength(outerRing, innerRing, distance);
                    double second = SharedLength(innerRing, outerRing, distance);
                    if (first > 1e-8 && second > 1e-8)
                        Edge(new(level, i), new(level + 1, j), first + second);
                }
        }

        // The reference code assigns zero cost contribution to ordinary
        // degree-two chains and, at junctions, converts shared length to
        // (maximumLength - sharedLength).  Therefore its minimum spanning tree
        // is a maximum-shared-length tree at branches.
        double EffectiveWeight(Node a, Node b, double sharedLength)
            => edges.GetValueOrDefault(a)?.Count > 2 || edges.GetValueOrDefault(b)?.Count > 2
                ? sharedLength
                : 0;
        var unseen = map.Keys.ToHashSet();
        CfsFamily? result = null;
        while (unseen.Count > 0)
        {
            var root = unseen.Min();
            var visited = new HashSet<Node> { root };
            var treeEdges = new List<(Node First, Node Second, double Weight)>();
            var queue = new PriorityQueue<(Node From, Node To, double Weight), (double, Node, Node)>();
            void Enqueue(Node n)
            {
                if (edges.TryGetValue(n, out var neighbours))
                    foreach (var e in neighbours.Where(e => !visited.Contains(e.Next)))
                    {
                        double weight = EffectiveWeight(n, e.Next, e.Weight);
                        queue.Enqueue((n, e.Next, weight), (-weight, n, e.Next));
                    }
            }
            Enqueue(root);
            while (queue.TryDequeue(out var edge, out _))
            {
                if (!visited.Add(edge.To)) continue;
                treeEdges.Add((edge.From, edge.To, edge.Weight));
                Enqueue(edge.To);
            }
            unseen.ExceptWith(visited);

            var tree = visited.ToDictionary(node => node,
                _ => new List<(Node Next, double Weight)>());
            foreach (var edge in treeEdges)
            {
                tree[edge.First].Add((edge.Second, edge.Weight));
                tree[edge.Second].Add((edge.First, edge.Weight));
            }

            // Root an MST component at an endpoint.  Every degree-two node is
            // then part of one spiralable chain; only degree-three-or-higher
            // junctions create child families, as in the paper decomposition.
            var endpoints = visited.Where(node => tree[node].Count <= 1).Order().ToArray();
            var familyRoot = endpoints.Length > 0 ? endpoints[0] : root;
            CfsFamily BuildFamily(Node node, Node? parent)
            {
                var f = new CfsFamily();
                f.Contours.Add(map[node]);
                var list = tree[node].Where(edge => parent == null || edge.Next != parent.Value).ToArray();
                if (list.Length == 0) return f;
                var ordered = list.OrderByDescending(x => x.Weight).ThenByDescending(x => x.Next).ToArray();
                var main = BuildFamily(ordered[0].Next, node);
                f.Contours.AddRange(main.Contours); f.Branches.AddRange(main.Branches);
                foreach (var branch in ordered.Skip(1)) f.Branches.Add(BuildFamily(branch.Next, node));
                return f;
            }
            var family = BuildFamily(familyRoot, null);
            // A disconnected component rooted on an interior boundary is
            // discovered from the hole outwards. Reverse that complete chain;
            // do not reverse recursive pieces of a connected MST because a
            // valid outer-to-hole chain naturally changes level direction at
            // the medial axis.
            if (result != null && family.Contours.Count > 1 &&
                RingArea(family.Contours[0]) < RingArea(family.Contours[^1]))
                family.Contours.Reverse();
            if (result == null) result = family;
            else result.Branches.Add(family);
        }
        return result ?? new CfsFamily();
    }

    private static double SharedLength(LineString source, LineString target, double distance)
    {
        int samples = Math.Max(64, (int)Math.Ceiling(source.Length / Math.Max(distance * .25, 1e-6)));
        int shared = 0;
        var targetGeometry = (NetTopologySuite.Geometries.Geometry)target;
        for (int i = 0; i < samples; i++)
        {
            var point = Factory.CreatePoint(At(source, source.Length * i / samples));
            if (targetGeometry.Distance(point) <= distance * 1.05) shared++;
        }
        return source.Length * shared / samples;
    }

    private static double RingArea(LineString ring)
    {
        try { return Math.Abs(Factory.CreatePolygon(Factory.CreateLinearRing(ring.Coordinates)).Area); }
        catch (ArgumentException) { return 0; }
    }

    internal static Dictionary<int, List<LineString>> Levels(Polygon polygon, double distance, CancellationToken token)
    {
        var levels = new Dictionary<int, List<LineString>>();
        // Zhao et al., Sec. 4: d(boundary(R), c_i,j) = (i - 0.5) * w.
        // The first tool-path centreline therefore lies half a spacing inside
        // the material, and every following level is exactly one spacing apart.
        var current = Polygons(Buffer(polygon, -distance * .5, 8)).ToList();
        for (int level = 0; level <= 100 && current.Count > 0; level++)
        {
            token.ThrowIfCancellationRequested();
            var rings = new List<LineString>();
            var next = new List<Polygon>();
            foreach (var p in current)
            {
                rings.Add(p.ExteriorRing);
                for (int i = 0; i < p.NumInteriorRings; i++)
                    rings.Add(p.GetInteriorRingN(i));
                if (level == 100) continue;
                next.AddRange(Polygons(Buffer(p, -distance, 8)));
            }
            if (rings.Count > 0) levels[level] = rings;
            current = next;
        }
        return levels;
    }

    // Port of distance_transform_diff, including the source's two erosion steps.
    internal static CfsFamily Standard(Polygon polygon, double distance, CancellationToken token)
    {
        var family = new CfsFamily(); family.Contours.Add(polygon.ExteriorRing);
        void Shrink(Polygon p, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth <= 0) return;
            var diff = p.Difference(Buffer(p.ExteriorRing, distance, 16, JoinStyle.Mitre));
            var parts = Polygons(diff).ToList();
            if (parts.Count > 1) { foreach (var part in parts) Shrink(part, depth - 1); return; }
            if (parts.Count == 0) return;
            foreach (var part in Polygons(Buffer(parts[0], -distance)))
            {
                family.Contours.Add(part.ExteriorRing); Shrink(part, depth - 1);
            }
        }
        Shrink(polygon, 100);
        return family;
    }
}
