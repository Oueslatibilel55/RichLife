namespace RichLife.Domain.Store;

/// <summary>
/// A profile avatar: an emoji on a gradient (<see cref="From"/> → <see cref="To"/>, CSS colours).
/// <see cref="Name"/> is English; clients translate by id. A price of 0 means free for everyone.
/// </summary>
public sealed record AvatarDefinition(string Id, string Icon, string Name, string From, string To, int Price)
{
    public bool IsFree => Price == 0;
    public string Rarity => StoreRarity.For(Price);
}

/// <summary>
/// The avatar store, in display order. Rules, not content — like badges, they live in code.
/// Ids are stored once bought or chosen: never rename or reuse one.
/// </summary>
public static class AvatarCatalog
{
    public static readonly IReadOnlyList<AvatarDefinition> All =
    [
        new("smile",     "😀", "Smiley",     "#FDE68A", "#F59E0B", 0),
        new("cool",      "😎", "Cool",       "#BAE6FD", "#0284C7", 0),
        new("cat",       "🐱", "Cat",        "#FBCFE8", "#DB2777", 0),
        new("dog",       "🐶", "Dog",        "#D9F99D", "#65A30D", 0),
        new("fox",       "🦊", "Fox",        "#FED7AA", "#EA580C", 30),
        new("panda",     "🐼", "Panda",      "#E2E8F0", "#475569", 30),
        new("koala",     "🐨", "Koala",      "#CBD5E1", "#64748B", 30),
        new("penguin",   "🐧", "Penguin",    "#BFDBFE", "#1D4ED8", 30),
        new("frog",      "🐸", "Frog",       "#BBF7D0", "#16A34A", 30),
        new("tiger",     "🐯", "Tiger",      "#FDBA74", "#C2410C", 60),
        new("owl",       "🦉", "Owl",        "#D6D3D1", "#78716C", 60),
        new("octopus",   "🐙", "Octopus",    "#F5D0FE", "#A21CAF", 60),
        new("robot",     "🤖", "Robot",      "#A5F3FC", "#0E7490", 60),
        new("astronaut", "🧑‍🚀", "Astronaut", "#C7D2FE", "#4338CA", 60),
        new("cowboy",    "🤠", "Cowboy",     "#FDE68A", "#A16207", 120),
        new("ninja",     "🥷", "Ninja",      "#94A3B8", "#0F172A", 120),
        new("wizard",    "🧙", "Wizard",     "#DDD6FE", "#6D28D9", 120),
        new("vampire",   "🧛", "Vampire",    "#FECACA", "#991B1B", 120),
        new("genie",     "🧞", "Genie",      "#99F6E4", "#0F766E", 250),
        new("superhero", "🦸", "Superhero",  "#FECDD3", "#BE123C", 250),
        new("king",      "🤴", "King",       "#FEF08A", "#CA8A04", 250),
        new("queen",     "👸", "Queen",      "#FBCFE8", "#9D174D", 250),
    ];

    public static AvatarDefinition? Find(string? id) => All.FirstOrDefault(a => a.Id == id);
}

/// <summary>The rarity scale shared by badges and avatars — derived from the price.</summary>
public static class StoreRarity
{
    public static string For(int price) => price switch
    {
        >= 250 => "legendary",
        >= 120 => "epic",
        >= 50 => "rare",
        _ => "common",
    };
}
