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
}