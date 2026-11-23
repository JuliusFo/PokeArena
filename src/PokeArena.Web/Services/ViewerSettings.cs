using Microsoft.JSInterop;

namespace PokeArena.Web.Services;

/// <summary>
/// What a viewer picked on „Einstellungen“, kept only in their own browser (privacy page): the look (light, dark or the
/// device's) and the fight log's view — plus the Pokédex filter, picked on the trainer page itself. Storage may be blocked (private windows, strict settings): every call catches
/// <see cref="JSException"/>, reading then gives the default and a pick holds until the page is left.
/// </summary>
public static class ViewerSettings
{
    /// <summary>localStorage key of the fight log's view; „text“ or „balken“.</summary>
    public const string LogViewKey = "pokearena.kampfverlauf";

    /// <summary>localStorage key of the Pokédex filter on the trainer page; „gefangen“ or „alle“.</summary>
    public const string PokedexViewKey = "pokearena.pokedex";

    /// <summary>The look follows the device (default).</summary>
    public const string ThemeAuto = "auto";

    public const string ThemeLight = "hell";

    public const string ThemeDark = "dunkel";

    /// <summary>
    /// The script at the top of index.html sets the theme before the first paint and keeps the pick
    /// (<c>localStorage</c> „pokearena.aussehen“); the page only asks it.
    /// </summary>
    public const string ThemeGet = "pokeArenaTheme.get";

    public const string ThemeSet = "pokeArenaTheme.set";

    /// <summary>Whether the viewer picked the text view of the fight log; bars when nothing or nothing readable is stored.</summary>
    public static async Task<bool> TextViewAsync(IJSRuntime js)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", LogViewKey) == "text";
        }
        catch (JSException)
        {
            return false;
        }
    }

    public static async Task SaveLogViewAsync(IJSRuntime js, bool textView)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", LogViewKey, textView ? "text" : "balken");
        }
        catch (JSException)
        {
            // Storage blocked: the pick holds for this page only.
        }
    }

    /// <summary>Whether the viewer picked „Nur gefangene“ in the Pokédex; „Alle“ when nothing or nothing readable is stored.</summary>
    public static async Task<bool> OnlyCaughtAsync(IJSRuntime js)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", PokedexViewKey) == "gefangen";
        }
        catch (JSException)
        {
            return false;
        }
    }

    public static async Task SavePokedexViewAsync(IJSRuntime js, bool onlyCaught)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", PokedexViewKey, onlyCaught ? "gefangen" : "alle");
        }
        catch (JSException)
        {
            // Storage blocked: the pick holds for this page only.
        }
    }

    /// <summary>The viewer's look:<see cref="ThemeAuto"/>, <see cref="ThemeLight"/> or <see cref="ThemeDark"/>.</summary>
    public static async Task<string> ThemeAsync(IJSRuntime js)
    {
        try
        {
            var theme = await js.InvokeAsync<string?>(ThemeGet);
            return theme is ThemeLight or ThemeDark ? theme : ThemeAuto;
        }
        catch (JSException)
        {
            return ThemeAuto;
        }
    }

    /// <summary>Applies the look at once and stores it (the script swallows a blocked storage itself).</summary>
    public static async Task SaveThemeAsync(IJSRuntime js, string theme)
    {
        try
        {
            await js.InvokeVoidAsync(ThemeSet, theme);
        }
        catch (JSException)
        {
            // Script missing (a stale cached index.html): the device decides until the page is reloaded.
        }
    }
}
