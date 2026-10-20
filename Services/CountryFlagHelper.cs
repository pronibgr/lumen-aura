using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace aura.Services;

public static class CountryFlagHelper
{
    private static readonly Dictionary<string, (string emoji, string name)> IsoToCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        { "RU", ("🇷🇺", "Россия") },
        { "NL", ("🇳🇱", "Нидерланды") },
        { "DE", ("🇩🇪", "Германия") },
        { "US", ("🇺🇸", "США") },
        { "FI", ("🇫🇮", "Финляндия") },
        { "FR", ("🇫🇷", "Франция") },
        { "GB", ("🇬🇧", "Великобритания") },
        { "JP", ("🇯🇵", "Япония") },
        { "SG", ("🇸🇬", "Сингапур") },
        { "TR", ("🇹🇷", "Турция") },
        { "CH", ("🇨🇭", "Швейцария") },
        { "KZ", ("🇰🇿", "Казахстан") },
        { "UA", ("🇺🇦", "Украина") },
        { "PL", ("🇵🇱", "Польша") },
        { "AT", ("🇦🇹", "Австрия") },
        { "ES", ("🇪🇸", "Испания") },
        { "IT", ("🇮🇹", "Италия") },
        { "SE", ("🇸🇪", "Швеция") },
        { "NO", ("🇳🇴", "Норвегия") },
        { "CZ", ("🇨🇿", "Чехия") },
        { "CA", ("🇨🇦", "Канада") },
        { "AU", ("🇦🇺", "Австралия") },
        { "BR", ("🇧🇷", "Бразилия") },
        { "HK", ("🇭🇰", "Гонконг") },
        { "IN", ("🇮🇳", "Индия") },
        { "IL", ("🇮🇱", "Израиль") },
        { "KR", ("🇰🇷", "Южная Корея") },
        { "AE", ("🇦🇪", "ОАЭ") },
        { "EE", ("🇪🇪", "Эстония") },
        { "LV", ("🇱🇻", "Латвия") },
        { "LT", ("🇱🇹", "Литва") },
        { "RO", ("🇷🇴", "Румыния") },
        { "BG", ("🇧🇬", "Болгария") },
        { "RS", ("🇷🇸", "Сербия") },
        { "GE", ("🇬🇪", "Грузия") },
        { "AM", ("🇦🇲", "Армения") },
        { "MD", ("🇲🇩", "Молдова") }
    };

    public static (string flagEmoji, string countryCode, string countryName) ParseServerFlag(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return ("🌐", "GL", "Global");

        string trimmed = rawName.Trim();

        // 1. Check if name starts with regional indicator symbols (standard flag emoji: 🇷🇺, 🇳🇱, 🇵🇱, etc.)
        var flagMatch = Regex.Match(trimmed, @"^(\uD83C[\uDDE6-\uDDFF]){2}");
        if (flagMatch.Success)
        {
            string flag = flagMatch.Value;
            char c1 = (char)('A' + (flag[1] - 0xDDE6));
            char c2 = (char)('A' + (flag[3] - 0xDDE6));
            string code = $"{c1}{c2}".ToUpperInvariant();
            string country = IsoToCountry.TryGetValue(code, out var info) ? info.name : code;
            return (flag, code, country);
        }

        // 2. Check if name starts with any generic single or surrogate emoji (e.g. ⚡, 🚀, 🛡️, 🌐, 🔒)
        if (char.IsSurrogatePair(trimmed, 0))
        {
            string leadEmoji = trimmed.Substring(0, 2);
            var keywordResult = DetectCountryFromKeywords(trimmed.Substring(2));
            if (keywordResult.countryCode != "GL")
                return keywordResult;

            return (leadEmoji, "VPN", "Proxy Node");
        }

        // 3. Detect from keywords in server name
        return DetectCountryFromKeywords(trimmed);
    }

    public static string CleanServerName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        string text = rawName;

        // 1. Replace non-standard whitespace / tabs with standard space
        text = Regex.Replace(text, @"[\t\r\n\u00A0\u2000-\u200B\u202F\u205F\u3000]+", " ");

        // 2. Strip any leading emoji (surrogate pairs, regional indicator flags), variation selectors, zero-width characters, symbols, and separators
        text = Regex.Replace(text, @"^[\uD83C-\uDBFF\uDC00-\uDFFF\uFE00-\uFE0F\u200B-\u200F\u202A-\u202E\uFEFF\u2600-\u27BF\s\-\|\:\—\–\•\·]+", "");

        // 3. Clean leading punctuation
        text = text.TrimStart('-', '|', ':', '—', '–', '•', '·', '/', '\\', ' ').Trim();

        // 4. Collapse multiple internal spaces
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(text) ? rawName.Trim() : text;
    }

    public static string GetFlagEmojiFromCode(string isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode) || isoCode.Length < 2)
            return "🌐";

        string code = isoCode.Trim().ToUpperInvariant();
        if (code.Length >= 2 && code[0] >= 'A' && code[0] <= 'Z' && code[1] >= 'A' && code[1] <= 'Z')
        {
            return $"{(char)0xD83C}{(char)(0xDDE6 + (code[0] - 'A'))}{(char)0xD83C}{(char)(0xDDE6 + (code[1] - 'A'))}";
        }

        return "🌐";
    }

    private static (string flagEmoji, string countryCode, string countryName) DetectCountryFromKeywords(string text)
    {
        string upper = text.ToUpperInvariant();

        if (Regex.IsMatch(upper, @"\b(RU|RUS|RUSSIA|РОССИЯ|РФ|МОСКВА|MOSCOW|SPB|ПЕТЕРБУРГ)\b"))
            return ("🇷🇺", "RU", "Россия");

        if (Regex.IsMatch(upper, @"(ROMANIA|РУМЫНИЯ|БУХАРЕСТ|BUCHAREST|\bRO[0-9_\-]|ROU)"))
            return ("🇷🇴", "RO", "Румыния");

        if (Regex.IsMatch(upper, @"(NETHERL|NETHERLANDS|НИДЕРЛАНДЫ|ГОЛЛАНДИЯ|AMSTERDAM|АМСТЕРДАМ|\bNL[0-9_\-])"))
            return ("🇳🇱", "NL", "Нидерланды");

        if (Regex.IsMatch(upper, @"(GERMANY|ГЕРМАНИЯ|FRANKFURT|ФРАНКФУРТ|BERLIN|БЕРЛИН|\bDE[0-9_\-]|DEU)"))
            return ("🇩🇪", "DE", "Германия");

        if (Regex.IsMatch(upper, @"(UNITED STATES|США|АМЕРИКА|SILICON|NEW YORK|MIAMI|LOS ANGELES|USA|\bUS[0-9_\-])"))
            return ("🇺🇸", "US", "США");

        if (Regex.IsMatch(upper, @"\b(FI|FIN|FINLAND|ФИНЛЯНДИЯ|HELSINKI|ХЕЛЬСИНКИ)\b"))
            return ("🇫🇮", "FI", "Финляндия");

        if (Regex.IsMatch(upper, @"\b(FR|FRA|FRANCE|ФРАНЦИЯ|PARIS|ПАРИЖ)\b"))
            return ("🇫🇷", "FR", "Франция");

        if (Regex.IsMatch(upper, @"\b(GB|UK|BRITAIN|ВЕЛИКОБРИТАНИЯ|АНГЛИЯ|LONDON|ЛОНДОН)\b"))
            return ("🇬🇧", "GB", "Великобритания");

        if (Regex.IsMatch(upper, @"\b(JP|JPN|JAPAN|ЯПОНИЯ|TOKYO|ТОКИО)\b"))
            return ("🇯🇵", "JP", "Япония");

        if (Regex.IsMatch(upper, @"\b(SG|SGP|SINGAPORE|СИНГАПУР)\b"))
            return ("🇸🇬", "SG", "Сингапур");

        if (Regex.IsMatch(upper, @"\b(TR|TUR|TURKEY|ТУРЦИЯ|ISTANBUL|СТАМБУЛ)\b"))
            return ("🇹🇷", "TR", "Турция");

        if (Regex.IsMatch(upper, @"\b(CH|CHE|SWITZERLAND|ШВЕЙЦАРИЯ|ZURICH|ЦЮРИХ)\b"))
            return ("🇨🇭", "CH", "Швейцария");

        if (Regex.IsMatch(upper, @"\b(KZ|KAZ|KAZAKHSTAN|КАЗАХСТАН|ALMATY|АЛМАТЫ|ASTANA|АСТАНА)\b"))
            return ("🇰🇿", "KZ", "Казахстан");

        if (Regex.IsMatch(upper, @"\b(UA|UKR|UKRAINE|УКРАИНА|KYIV|КИЕВ)\b"))
            return ("🇺🇦", "UA", "Украина");

        if (Regex.IsMatch(upper, @"\b(PL|POL|POLAND|ПОЛЬША|WARSAW|ВАРШАВА)\b"))
            return ("🇵🇱", "PL", "Польша");

        if (Regex.IsMatch(upper, @"\b(AT|AUT|AUSTRIA|АВСТРИЯ|VIENNA|ВЕНА)\b"))
            return ("🇦🇹", "AT", "Австрия");

        if (Regex.IsMatch(upper, @"\b(ES|ESP|SPAIN|ИСПАНИЯ|MADRID|МАДРИД)\b"))
            return ("🇪🇸", "ES", "Испания");

        if (Regex.IsMatch(upper, @"\b(IT|ITA|ITALY|ИТАЛИЯ|MILAN|МИЛАН|ROME|РИМ)\b"))
            return ("🇮🇹", "IT", "Италия");

        if (Regex.IsMatch(upper, @"\b(SE|SWE|SWEDEN|ШВЕЦИЯ|STOCKHOLM|СТОКГОЛЬМ)\b"))
            return ("🇸🇪", "SE", "Швеция");

        if (Regex.IsMatch(upper, @"\b(NO|NOR|NORWAY|НОРВЕГИЯ|OSLO|ОСЛО)\b"))
            return ("🇳🇴", "NO", "Норвегия");

        if (Regex.IsMatch(upper, @"\b(CZ|CZE|CZECH|ЧЕХИЯ|PRAGUE|ПРАГА)\b"))
            return ("🇨🇿", "CZ", "Чехия");

        if (Regex.IsMatch(upper, @"\b(CA|CAN|CANADA|КАНАДА|TORONTO|ТОРОНТО)\b"))
            return ("🇨🇦", "CA", "Канада");

        if (Regex.IsMatch(upper, @"(ROMANIA|РУМЫНИЯ|BUCHAREST|БУХАРЕСТ|\bRO[0-9_\-]|ROU)"))
            return ("🇷🇴", "RO", "Румыния");

        return ("🌐", "GL", "Global");
    }
}
