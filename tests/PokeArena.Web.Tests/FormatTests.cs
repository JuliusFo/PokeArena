using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Shared;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

/// <summary>Small formatting helpers.</summary>
public class FormatTests
{
    [Theory]
    [InlineData("Käfer", "type-kaefer")]
    [InlineData("Psycho", "type-psycho")]
    public void Type_names_become_css_classes(string type, string css) => Assert.Equal(css, Format.TypeClass(type));
}
