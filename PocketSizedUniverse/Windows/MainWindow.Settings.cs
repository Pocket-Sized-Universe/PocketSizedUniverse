using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private void DrawSettings()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "To Be Implemented");
        ImGui.Spacing();
        ImGui.TextWrapped("This tab will contain configuration options for the Pocket Sized Universe plugin.");
    }
}
