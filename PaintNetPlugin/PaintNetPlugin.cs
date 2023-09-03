using PaintDotNet;
using PaintDotNet.Collections;
using PaintDotNet.Effects;
using PaintDotNet.Effects.Gpu;
using PaintDotNet.Imaging;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;

namespace PaintNetPlugin;

public class PaintNetPlugin : PropertyBasedBitmapEffect
{
    public PaintNetPlugin() : base("Color Channel Copy", "Tools", BitmapEffectOptions.Create() with
    {
        IsConfigurable = true
    })
    {
        
    }


    private static int PreviousIndex = -1;
    private static bool
        PreviousCopyR = false,
        PreviousCopyG = false,
        PreviousCopyB = false,
        PreviousCopyA = true;

    private bool
        CopyR = false,
        CopyG = false,
        CopyB = false,
        CopyA = true;

    private IBitmapSource<ColorBgra32> CurrentLayerBitmapSource;
    private IBitmapSource<ColorBgra32> SelectedLayerBitmapSource;
    private string[] LayerList = new string[0];

    private enum PropertyNames { 
        Layer,
        CopyR,
        CopyG,
        CopyB,
        CopyA
    }

    protected override PropertyCollection OnCreatePropertyCollection()
    {
        List<Property> properties = new List<Property>();
        LayerList = new string[this.Environment.Document.Layers.Count];
        for (int i = 0; i < Environment.Document.Layers.Count; i++)
        {
            if(i==Environment.SourceLayerIndex)
                LayerList[i] = $"[{i+1}] [Current] {Environment.Document.Layers[i].Name}";
            else
                LayerList[i] = $"[{i+1}] {Environment.Document.Layers[i].Name}";
        }
        properties.Add(new StaticListChoiceProperty(PropertyNames.Layer, LayerList, Environment.SourceLayerIndex));
        properties.Add(new BooleanProperty(PropertyNames.CopyR, PreviousCopyR));
        properties.Add(new BooleanProperty(PropertyNames.CopyG, PreviousCopyG));
        properties.Add(new BooleanProperty(PropertyNames.CopyB, PreviousCopyB));
        properties.Add(new BooleanProperty(PropertyNames.CopyA, PreviousCopyA));
        return new PropertyCollection(properties);
    }

    private int NameToIndex(string name)
    {
        int indexOfSqbc = name.IndexOf(']');
        if (indexOfSqbc == -1)
            return -1;
        return int.Parse(name.Substring(1, indexOfSqbc - 1)) -1;
    }

    protected override void OnSetToken(PropertyBasedEffectConfigToken? newToken)
    {
        var index = NameToIndex((string)newToken!.GetProperty<StaticListChoiceProperty>(PropertyNames.Layer).Value);
        if (index == -1)
            index = PreviousIndex;
        if(index > LayerList.Length)
            index = LayerList.Length - 1;
        PreviousCopyR = CopyR = newToken!.GetProperty<BooleanProperty>(PropertyNames.CopyR).Value;
        PreviousCopyG = CopyG = newToken!.GetProperty<BooleanProperty>(PropertyNames.CopyG).Value;
        PreviousCopyB = CopyB = newToken!.GetProperty<BooleanProperty>(PropertyNames.CopyB).Value;
        PreviousCopyA = CopyA = newToken!.GetProperty<BooleanProperty>(PropertyNames.CopyA).Value;
        SelectedLayerBitmapSource = Environment.Document.Layers[index].GetBitmapBgra32();
        PreviousIndex = index;
        base.OnSetToken(newToken);
    }

    protected override void OnInitializeRenderInfo(IBitmapEffectRenderInfo renderInfo)
    {
        CurrentLayerBitmapSource = Environment.GetSourceBitmapBgra32();
        base.OnInitializeRenderInfo(renderInfo);
    }

    protected override void OnRender(IBitmapEffectOutput output)
    {
        using IBitmapLock<ColorBgra32> outputLock = output.LockBgra32();
        using IBitmap<ColorBgra32> cSourceTile = this.CurrentLayerBitmapSource!
            .CreateClipper(output.Bounds, BitmapExtendMode.Clamp)
            .ToBitmap();
        using IBitmap<ColorBgra32> sSourceTile = this.SelectedLayerBitmapSource!
            .CreateClipper(output.Bounds, BitmapExtendMode.Clamp)
            .ToBitmap();
        using var cSourceRegionLock = cSourceTile.Lock(BitmapLockOptions.Read);
        using var sSourceRegionLock = sSourceTile.Lock(BitmapLockOptions.Read);
        RegionPtr<ColorBgra32> outputRegion = outputLock.AsRegionPtr();
        RegionPtr<ColorBgra32> cSRegion = cSourceRegionLock.AsRegionPtr();
        RegionPtr<ColorBgra32> sSRegion = sSourceRegionLock.AsRegionPtr();
        for (int y = 0; y < output.Bounds.Height; y++)
            for (int x = 0; x < output.Bounds.Width; x++)
                outputRegion[x, y] = cSRegion[x, y] with { 
                    A = CopyA ? sSRegion[x, y].A : cSRegion[x,y].A,
                    R = CopyR ? sSRegion[x, y].R : cSRegion[x,y].R,
                    G = CopyG ? sSRegion[x, y].G : cSRegion[x,y].G,
                    B = CopyB ? sSRegion[x, y].B : cSRegion[x,y].B
                };

    }
}