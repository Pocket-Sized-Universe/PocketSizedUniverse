namespace PocketSizedUniverse.Windows.ViewModels;

public static class Adjectives
{
    public static string GetRandom() => List[new Random().Next(List.Count)];
    public static List<string> List =>
    [
        "Awesome",
        "Cool",
        "Great",
        "Fantastic",
        "Wonderful",
        "Spectacular",
        "Stunning",
        "Amazing",
        "Marvelous",
        "Excellent",
        "Spherical",
        "Radiant",
        "Gravitational",
        "Magnetic",
        "Electric",
        "Incredible",
        "Comical",
        "Fantastical",
        "Fascinating",
        "Frumpy",
        "Fuzzy",
        "Freaky",
        "Funky",
        "Delicious",
        "Delightful",
        "Delighted",
        "Stanky",
        "Monetary",
        "Fancified",
        "Fanciful",
        "Flaccid",
        "Flippant",
    ];
}