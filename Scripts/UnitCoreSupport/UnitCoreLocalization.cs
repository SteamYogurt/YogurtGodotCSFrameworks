using Godot;

public static class UnitCoreLocalization
{
    public static string Tr(string key) => TranslationServer.Translate(key);
}
