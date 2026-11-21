using System;
using Godot;

/// <summary>
/// Locale list and normalization. Translation tables stay with the game project
/// or the sibling GDS kit; runtime only switches <see cref="TranslationServer"/>.
/// </summary>
public static class Localization
{
    public static readonly string[] Locales =
    {
        "en",
        "zh_CN",
        "zh_TW",
        "ja",
        "ko",
        "es",
        "fr",
        "pt_BR",
        "de",
        "it",
        "ru",
        "tr",
        "pl",
    };

    public static bool IsSupported(string locale)
    {
        return IndexOf(locale) >= 0;
    }

    public static int IndexOf(string locale)
    {
        return Array.IndexOf(Locales, locale);
    }

    /// <summary>
    /// Collapse a system or saved locale into one of the thirteen supported codes.
    /// </summary>
    public static string ResolveLocale(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            raw = OS.GetLocale();
        }

        if (IsSupported(raw))
        {
            return raw;
        }

        string lang = raw.Replace('-', '_');
        if (IsSupported(lang))
        {
            return lang;
        }

        if (lang.StartsWith("zh_Hant", StringComparison.Ordinal)
            || lang.StartsWith("zh_TW", StringComparison.Ordinal)
            || lang.StartsWith("zh_HK", StringComparison.Ordinal)
            || lang.StartsWith("zh_MO", StringComparison.Ordinal))
        {
            return "zh_TW";
        }

        if (lang.StartsWith("zh", StringComparison.Ordinal))
        {
            return "zh_CN";
        }

        if (lang.StartsWith("pt", StringComparison.Ordinal))
        {
            return "pt_BR";
        }

        string shortLang = lang.Split('_')[0];
        if (IsSupported(shortLang))
        {
            return shortLang;
        }

        return "en";
    }

    public static string NativeName(string locale)
    {
        return locale switch
        {
            "en" => "English",
            "zh_CN" => "简体中文",
            "zh_TW" => "繁體中文",
            "ja" => "日本語",
            "ko" => "한국어",
            "es" => "Español",
            "fr" => "Français",
            "pt_BR" => "Português (Brasil)",
            "de" => "Deutsch",
            "it" => "Italiano",
            "ru" => "Русский",
            "tr" => "Türkçe",
            "pl" => "Polski",
            _ => locale,
        };
    }
}
