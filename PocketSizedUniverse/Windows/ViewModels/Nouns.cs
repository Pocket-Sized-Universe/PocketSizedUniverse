namespace PocketSizedUniverse.Windows.ViewModels;

public static class Nouns
{
    public static string GetRandom() => List[new Random().Next(List.Count)];
    public static List<string> List =>
    [
        "Star System",
        "Escalator",
        "Piano",
        "Planet",
        "Moon",
        "Rocket",
        "Rocket Engine",
        "Rocket Fuel",
        "Telephone",
        "Television",
        "Computer",
        "Shrimp",
        "Egg",
        "Pizza",
        "Porcupine",
        "Stink",
        "Paw",
        "Witch",
        "Hat",
        "Camp",
        "Campfire",
        "Falsehood",
        "Truth",
        "Pioneer",
        "Scramble"
    ];
}