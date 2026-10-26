using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DentalLabOS.Web.Infrastructure;

/// <summary><c>&lt;icon name="users" /&gt;</c> → inline reference to the shared SVG sprite (same markup as SharedUI's Icon component).</summary>
[HtmlTargetElement("icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    public string Name { get; set; } = default!;

    public string? Class { get; set; }

    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", string.IsNullOrEmpty(Class) ? "icon" : $"icon {Class}");
        output.Attributes.SetAttribute("focusable", "false");
        if (Label is null)
        {
            output.Attributes.SetAttribute("aria-hidden", "true");
        }
        else
        {
            output.Attributes.SetAttribute("role", "img");
            output.Attributes.SetAttribute("aria-label", Label);
        }

        output.Content.SetHtmlContent($"<use href=\"{SharedUI.Components.Icon.SpriteUrl}#i-{System.Net.WebUtility.HtmlEncode(Name)}\"></use>");
    }
}

/// <summary>The DentalLab OS logo mark (stylized tooth).</summary>
[HtmlTargetElement("brand-mark", TagStructure = TagStructure.WithoutEndTag)]
public sealed class BrandMarkTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "brand-mark");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Content.SetHtmlContent(
            """<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M7.5 3.5c-2.4 0-4 1.9-4 4.6 0 2.1.8 3.6 1.5 5.2.6 1.4.8 3 1.1 4.6.3 1.6.8 2.6 1.8 2.6 1.3 0 1.5-1.6 1.9-3.4.3-1.4.8-2.6 2.2-2.6s1.9 1.2 2.2 2.6c.4 1.8.6 3.4 1.9 3.4 1 0 1.5-1 1.8-2.6.3-1.6.5-3.2 1.1-4.6.7-1.6 1.5-3.1 1.5-5.2 0-2.7-1.6-4.6-4-4.6-1.7 0-2.6.9-4.5.9s-2.8-.9-4.5-.9Z"/><path d="M9 7.5c1 .5 2 .7 3 .7" opacity=".7"/></svg>""");
    }
}
