using System.Reflection;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow : Window
{
    public MainWindow() : base("Pocket Sized Universe " + Assembly.GetExecutingAssembly().GetName().Version)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }
    
    public override void Draw()
    {
        DrawTopStatus();
        ImGui.Separator();
        DrawTabs();
    }

    public override bool DrawConditions() => PsuPlugin.Configuration.SetupComplete;
}
