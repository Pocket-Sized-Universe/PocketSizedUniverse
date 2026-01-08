using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace PocketSizedUniverse.GUI;

public static class GuiUtils
{
    public static bool GenericEnumCombo<T>(string label, float width, T current, out T newValue,
        IEnumerable<T> options, Func<T, string>? toString = null) where T : struct, Enum
    {
        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo(label, toString?.Invoke(current) ?? current.ToString());
        if (combo)
            foreach (var data in options)
            {
                var name = toString?.Invoke(data) ?? data.ToString();
                if (name.Length == 0 || !ImGui.Selectable(name, data.Equals(current)) || data.Equals(current))
                    continue;

                newValue = data;
                return true;
            }

        newValue = current;
        return false;
    }

    public static bool IsWine()
    {
        return Environment.GetEnvironmentVariable("WINEPREFIX") != null;
    }
}