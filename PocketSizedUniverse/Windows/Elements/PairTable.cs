using Dalamud.Bindings.ImGui;
using OtterGui.Table;
using PocketSizedUniverse.Windows.ViewModels;
using Syncthing.Models.Response;
using System.Numerics;

namespace PocketSizedUniverse.Windows.Elements;

public class PairTable : Table<Star>
{
    private static StarIdColumn _starIdColumn = new();
    private static NameColumn _nameColumn = new();
    private static StatusColumn _statusColumn = new();
    private static IntroducerColumn _introducerColumn = new();
    private static CompressionColumn _compressionColumn = new();
    
    public Star? SelectedItem { get; private set; }

    public PairTable(IReadOnlyCollection<Star> items) : base("Paired Stars", items, 
        _starIdColumn, _nameColumn, _statusColumn, _introducerColumn, _compressionColumn)
    {
        Flags = ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | 
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
    }
    
    public void HandleContextMenu()
    {
        // Context menu will be handled by the parent window for now
        // This can be expanded later when we understand the OtterGui API better
    }

    public class StarIdColumn : ColumnString<Star>
    {
        public StarIdColumn()
        {
            Label = "Star ID";
        }
        
        public override string ToName(Star item)
        {
            // Show truncated star ID for better readability
            var id = item.StarId;
            return id.Length > 16 ? id[..8] + "..." + id[^8..] : id;
        }
        
        public override float Width => 180f;
    }

    public class NameColumn : ColumnString<Star>
    {
        public NameColumn()
        {
            Label = "Name";
        }
        
        public override string ToName(Star item)
        {
            return string.IsNullOrEmpty(item.Name) ? "<Unnamed>" : item.Name;
        }
        
        public override void DrawColumn(Star item, int index)
        {
            var name = ToName(item);
            var color = string.IsNullOrEmpty(item.Name) 
                ? new Vector4(0.6f, 0.6f, 0.6f, 1.0f) 
                : Vector4.One;
            
            ImGui.TextColored(color, name);
        }
        
        public override float Width => 150f;
    }
    
    public class StatusColumn : Column<Star>
    {
        public StatusColumn()
        {
            Label = "Status";
        }
        
        public override void DrawColumn(Star item, int index)
        {
            var statusText = item.GetStatusText();
            var statusColor = item.GetStatusColor();
            
            ImGui.TextColored(statusColor, "●");
            ImGui.SameLine();
            ImGui.Text(statusText);
        }
        
        public override float Width => 80f;
        public override int Compare(Star lhs, Star rhs) => lhs.Paused.CompareTo(rhs.Paused);
    }
    
    public class IntroducerColumn : Column<Star>
    {
        public IntroducerColumn()
        {
            Label = "Introducer";
        }
        
        public override void DrawColumn(Star item, int index)
        {
            if (item.Introducer)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1.0f), "Yes");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No");
            }
        }
        
        public override float Width => 80f;
        public override int Compare(Star lhs, Star rhs) => lhs.Introducer.CompareTo(rhs.Introducer);
    }
    
    public class CompressionColumn : ColumnString<Star>
    {
        public CompressionColumn()
        {
            Label = "Compression";
        }
        
        public override string ToName(Star item)
        {
            return string.IsNullOrEmpty(item.Compression) ? "none" : item.Compression;
        }
        
        public override float Width => 100f;
    }
}
