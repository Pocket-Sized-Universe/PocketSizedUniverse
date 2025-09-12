using Dalamud.Bindings.ImGui;
using OtterGui.Table;
using PocketSizedUniverse.Windows.ViewModels;
using Syncthing.Models.Response;
using System.Numerics;

namespace PocketSizedUniverse.Windows.Elements;

public class GalaxyTable : Table<DataPack>
{
    private static NameColumn _nameColumn = new();
    private static PathColumn _pathColumn = new();
    private static TypeColumn _typeColumn = new();
    private static StarCountColumn _starCountColumn = new();
    private static RescanColumn _rescanColumn = new();
    
    public DataPack? SelectedItem { get; private set; }

    public GalaxyTable(IReadOnlyCollection<DataPack> items) : base("Galaxies", items,
        _nameColumn, _pathColumn, _typeColumn, _starCountColumn, _rescanColumn)
    {
        Flags = ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | 
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
    }
    
    public void HandleContextMenu()
    {
        // Context menu will be handled by the parent window for now
        // This can be expanded later when we understand the OtterGui API better
    }
    
    public class NameColumn : ColumnString<DataPack>
    {
        public NameColumn()
        {
            Label = "Name";
        }

        public override string ToName(DataPack item)
        {
            return string.IsNullOrEmpty(item.Name) ? item.Id.ToString() : item.Name;
        }
        
        public override void DrawColumn(DataPack item, int index)
        {
            var name = ToName(item);
            var color = string.IsNullOrEmpty(item.Name) 
                ? new Vector4(0.6f, 0.6f, 0.6f, 1.0f) 
                : Vector4.One;
            
            ImGui.TextColored(color, name);
        }
        
        public override float Width => 150f;
    }
    
    public class PathColumn : ColumnString<DataPack>
    {
        public PathColumn()
        {
            Label = "Path";
        }
        
        public override string ToName(DataPack item)
        {
            return UIHelpers.FormatPath(item.Path, 40);
        }
        
        public override float Width => 200f;
    }
    
    public class TypeColumn : Column<DataPack>
    {
        public TypeColumn()
        {
            Label = "Type";
        }
        
        public override void DrawColumn(DataPack item, int index)
        {
            var typeText = item.GetTypeText();
            var typeColor = item.GetTypeColor();
            
            ImGui.TextColored(typeColor, typeText);
        }
        
        public override float Width => 120f;
        public override int Compare(DataPack lhs, DataPack rhs) => lhs.Type.CompareTo(rhs.Type);
    }
    
    public class StarCountColumn : Column<DataPack>
    {
        public StarCountColumn()
        {
            Label = "Stars";
        }
        
        public override void DrawColumn(DataPack item, int index)
        {
            var starCount = item.GetStarCount();
            var color = starCount > 0 
                ? new Vector4(0.4f, 0.8f, 0.4f, 1.0f)
                : new Vector4(0.8f, 0.6f, 0.4f, 1.0f);
            
            ImGui.TextColored(color, starCount.ToString());
        }
        
        public override float Width => 70f;
        public override int Compare(DataPack lhs, DataPack rhs) => lhs.GetStarCount().CompareTo(rhs.GetStarCount());
    }
    
    public class RescanColumn : Column<DataPack>
    {
        public RescanColumn()
        {
            Label = "Rescan (s)";
        }
        
        public override void DrawColumn(DataPack item, int index)
        {
            var interval = item.RescanIntervalS;
            var text = interval == 0 ? "Disabled" : interval.ToString();
            var color = interval == 0 
                ? new Vector4(0.6f, 0.6f, 0.6f, 1.0f)
                : Vector4.One;
            
            ImGui.TextColored(color, text);
        }
        
        public override float Width => 90f;
        public override int Compare(DataPack lhs, DataPack rhs) => lhs.RescanIntervalS.CompareTo(rhs.RescanIntervalS);
    }
}
