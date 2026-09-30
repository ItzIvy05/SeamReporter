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

    private sealed class LandCell
    {
        public required IModContext<ISkyrimMod, ISkyrimModGetter, ILandscape, ILandscapeGetter> Context {  get; set; }
        public required float[,] Heights { get; init; }
        public required float[,] OriginalHeights { get; init; }
    }

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
}