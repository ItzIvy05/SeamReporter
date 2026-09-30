using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Noggog;

namespace SeamReporter;

public class Program
{
    private const int Size = 33;
    private const int Last = 32;
    private const int CellUnits = 4096;
    private const int PointUnits = 128;
    private const float MinGapUnits = 8f;

    private sealed class LandCell
    {
        public required IModContext<ISkyrimMod, ISkyrimModGetter, ILandscape, ILandscapeGetter> Context {  get; set; }
        public required float[,] Heights { get; init; }
        public required float[,] OriginalHeights { get; init; }
    }

    private sealed record CellEntry(int X, int Y, string Plugin);
    private sealed record Point(float X, float y);
    private sealed record Gap(string? Worldspace, CellEntry CellA, CellEntry CellB, string Edge, int GappedPoints, float LargestGapUnits, Point LargestGapAt, string Console);
    public static async Task<int> Main(string[] args)
    {
        return await SynthesisPipeline.Instance.AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch).SetTypicalOpen(GameRelease.SkyrimSE, "SeamReport.esp").Run(args);
    }

    public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
    {

    }
    private static float[,] Decode(ILandscapeVertexHeightMapGetter vhgt)
    {
        var heights = new float[Size, Size];
        var rowStart = vhgt.Offset;

        for (var y = 0; y < Size; y++)
        {
            rowStart += vhgt.HeightMap[0, y];
            var height = rowStart;
            heights[0, y] = height * 8;

            for (var x = 1; x < Size; x++)
            {
                height += vhgt.HeightMap[x, y];
                heights[x, y] = height * 8;
            }
        }

        return heights;
    }

    private static Dictionary<FormKey, Dictionary<P2Int, LandCell>> CollectCells(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
    {
        var worldspaces = new Dictionary<FormKey, Dictionary<P2Int, LandCell>>();
        foreach (var context in state.LoadOrder.PriorityOrder.Landscape().WinningContextOverrides(state.LinkCache))
        {
            var vhgt = context.Record.VertexHeightMap;
            var originalVhgt = state.LinkCache.ResolveAll<ILandscapeGetter>(context.Record.FormKey).Last().VertexHeightMap;

            if (vhgt is null || originalVhgt is null) {  continue; }
            if (!context.TryGetParent<ICellGetter>(out var cell) || cell.Grid is null) { continue; }
            if (!context.TryGetParent<IWorldspaceGetter>(out var worldspace)) {  continue; }
            if (!worldspaces.TryGetValue(worldspace.FormKey, out var cells))
            {
                cells = new Dictionary<P2Int, LandCell>();
                worldspaces[worldspace.FormKey] = cells;
            }
            
            cells.TryAdd(cell.Grid.Point, new LandCell { Context = context, Heights = Decode(vhgt), OriginalHeights = Decode(originalVhgt) });
        }
        return worldspaces;
    }

    private static Gap? CompareEdge(string? worldspace, P2Int gridA, LandCell a, P2Int gridB, LandCell b, bool east)
    {
        var gappedPoints = 0;
        var largest = 0f;
        var largestAt = 0;
        for (var i = 0; i < Size; i++)
        {
            var heightA = east ? a.Heights[Last, i] : a.Heights[i, Last];
            var heightB = east ? b.Heights[0, i] : b.Heights[i, 0];
            var originalA = east ? a.OriginalHeights[Last, i] : a.OriginalHeights[i, Last];
            var originalB = east ? b.OriginalHeights[0, i] : b.OriginalHeights[i, 0];
            var gap = Math.Abs(heightA - heightB);
            if (gap < MinGapUnits || Math.Abs(originalA - originalB) >= MinGapUnits)
            {
                continue;
            }
            gappedPoints++;
            if (gap > largest)
            {
                largest = gap;
                largestAt = i;
            }
        }
        if (gappedPoints == 0)
        {
            return null;
        }
        var at = east ? new Point(gridB.X * CellUnits, gridA.Y * CellUnits + largestAt * PointUnits) : new Point(gridA.X * CellUnits + largestAt * PointUnits, gridB.Y * CellUnits);
        var cellA = new CellEntry(gridA.X, gridA.Y, a.Context.ModKey.FileName);
        var cellB = new CellEntry(gridB.X, gridB.Y, b.Context.ModKey.FileName);
        return new Gap(worldspace, cellA, cellB, east ? "East" : "North", gappedPoints, largest, at, $"cow {worldspace} {gridA.X} {gridA.Y}");
    }
}