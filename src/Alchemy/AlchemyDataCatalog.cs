using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Alchemy;

internal sealed class AlchemyDataCatalog
{
    private static readonly Lazy<AlchemyDataCatalog> Instance = new(Load);

    private readonly Dictionary<string, string> _uticorLabels;
    private readonly Dictionary<(string DatatypeCode, string EncodeCode), string>
        _plcDatatypeLabels;
    private readonly HashSet<(string DatatypeCode, string EncodeCode)>
        _plcDatatypeExceptionKeys;
    private readonly Dictionary<string, string> _boolTagTypeLabels;

    private AlchemyDataCatalog(
        Dictionary<string, string> uticorLabels,
        Dictionary<(string DatatypeCode, string EncodeCode), string> plcDatatypeLabels,
        HashSet<(string DatatypeCode, string EncodeCode)> plcDatatypeExceptionKeys,
        Dictionary<string, string> boolTagTypeLabels)
    {
        _uticorLabels = uticorLabels;
        _plcDatatypeLabels = plcDatatypeLabels;
        _plcDatatypeExceptionKeys = plcDatatypeExceptionKeys;
        _boolTagTypeLabels = boolTagTypeLabels;
    }

    public static AlchemyDataCatalog Current => Instance.Value;

    public bool TryResolveUticorCode(string code, out string label)
    {
        return _uticorLabels.TryGetValue(Normalize(code), out label!);
    }

    public bool TryResolvePlcDatatype(
        string datatypeCode,
        string encodeCode,
        out string label)
    {
        return _plcDatatypeLabels.TryGetValue(
            (Normalize(datatypeCode), Normalize(encodeCode)),
            out label!);
    }

    public bool IsPlcDatatypeException(string datatypeCode, string encodeCode)
    {
        return _plcDatatypeExceptionKeys.Contains(
            (Normalize(datatypeCode), Normalize(encodeCode)));
    }

    public bool TryResolveBoolTagTypeLabel(string registerKind, out string label)
    {
        return _boolTagTypeLabels.TryGetValue(NormalizeRegisterKind(registerKind), out label!);
    }

    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var normalized = trimmed.TrimStart('0');
        return normalized.Length == 0
            ? "0"
            : normalized;
    }

    private static AlchemyDataCatalog Load()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        var dictionaryText = Path.Combine(dataDirectory, "Uticor Dictionary.txt");

        var (uticorLabels, plcDatatypeLabels, plcDatatypeExceptionKeys, boolTagTypeLabels) =
            LoadDictionary(dictionaryText);

        return new AlchemyDataCatalog(
            uticorLabels,
            plcDatatypeLabels,
            plcDatatypeExceptionKeys,
            boolTagTypeLabels);
    }

    private static (Dictionary<string, string> UticorLabels,
        Dictionary<(string DatatypeCode, string EncodeCode), string> PlcDatatypeLabels,
        HashSet<(string DatatypeCode, string EncodeCode)> PlcDatatypeExceptionKeys,
        Dictionary<string, string> BoolTagTypeLabels)
        LoadDictionary(string path)
    {
        var uticorLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var plcDatatypeLabels = new Dictionary<(string DatatypeCode, string EncodeCode), string>();
        var plcDatatypeExceptionKeys = new HashSet<(string DatatypeCode, string EncodeCode)>();
        var boolTagTypeLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = DictionarySection.None;

        foreach (var rawLine in ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("UTICOR DATATYPE/ENCODE DICTIONARY", StringComparison.OrdinalIgnoreCase))
            {
                section = DictionarySection.Uticor;
                continue;
            }

            if (line.StartsWith("VALID PLC DATATYPES", StringComparison.OrdinalIgnoreCase))
            {
                section = DictionarySection.Plc;
                continue;
            }

            if (line.StartsWith("UTICOR VERIFY DICTIONARY", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("UTICOR FUNCODE DICTIONARY", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("PRELOAD DEFINITION", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("NOTE TO SELF", StringComparison.OrdinalIgnoreCase))
            {
                // These confirmed reference sections live beside the datatype
                // catalog, but their overlapping numeric codes must not be
                // interpreted as datatype/encode labels.
                section = DictionarySection.ReferenceOnly;
                continue;
            }

            if (line.StartsWith("NEW PLC DATATYPE EXCEPTIONS", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("PLC DATATYPE EXCEPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                section = DictionarySection.PlcException;
                continue;
            }

            if (line.StartsWith("BOOL TAG TYPE", StringComparison.OrdinalIgnoreCase))
            {
                section = DictionarySection.BoolTagType;
                continue;
            }

            switch (section)
            {
                case DictionarySection.Uticor:
                    if (TryParseCodeLabel(line, out var code, out var label))
                    {
                        uticorLabels[Normalize(code)] = label;
                    }

                    break;
                case DictionarySection.Plc:
                case DictionarySection.PlcException:
                    if (TryParsePlcDatatype(line, out var datatypeCode, out var encodeCode, out var plcLabel))
                    {
                        var key = (Normalize(datatypeCode), Normalize(encodeCode));
                        if (!plcDatatypeLabels.ContainsKey(key))
                        {
                            plcDatatypeLabels[key] = plcLabel;
                        }

                        if (section == DictionarySection.PlcException)
                        {
                            plcDatatypeExceptionKeys.Add(key);
                        }
                    }

                    break;
                case DictionarySection.BoolTagType:
                    if (TryParseBoolTagType(line, out var registerKind, out var boolTagLabel))
                    {
                        boolTagTypeLabels[NormalizeRegisterKind(registerKind)] = boolTagLabel;
                    }

                    break;
            }
        }

        return (uticorLabels, plcDatatypeLabels, plcDatatypeExceptionKeys, boolTagTypeLabels);
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path))
        {
            yield return line;
        }
    }

    private static bool TryParseCodeLabel(
        string line,
        out string code,
        out string label)
    {
        var match = Regex.Match(line, @"^(?<code>\S+)\s+(?<label>.+)$");
        if (!match.Success)
        {
            code = string.Empty;
            label = string.Empty;
            return false;
        }

        code = match.Groups["code"].Value;
        label = match.Groups["label"].Value.Trim();
        return code.Length > 0 && label.Length > 0;
    }

    private static bool TryParsePlcDatatype(
        string line,
        out string datatypeCode,
        out string encodeCode,
        out string label)
    {
        var match = Regex.Match(
            line,
            @"^Datatype:(?<datatype>\d+)\s*\+\s*Encode\s*:?[\s]*(?<encode>\d+)\s*=\s*(?<label>.+)$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            datatypeCode = string.Empty;
            encodeCode = string.Empty;
            label = string.Empty;
            return false;
        }

        datatypeCode = match.Groups["datatype"].Value;
        encodeCode = match.Groups["encode"].Value;
        label = match.Groups["label"].Value.Trim();
        return datatypeCode.Length > 0 && encodeCode.Length > 0 && label.Length > 0;
    }

    private static bool TryParseBoolTagType(
        string line,
        out string registerKind,
        out string label)
    {
        var match = Regex.Match(line, @"^\[(?<kind>[^\]]+)\]\s*=\s*(?<label>.+)$");
        if (!match.Success)
        {
            registerKind = string.Empty;
            label = string.Empty;
            return false;
        }

        registerKind = match.Groups["kind"].Value.Trim();
        label = match.Groups["label"].Value.Trim();
        return registerKind.Length > 0 && label.Length > 0;
    }

    public static string NormalizeRegisterKind(string registerKind)
    {
        var normalized = registerKind.Trim();
        if (normalized.Contains("coil", StringComparison.OrdinalIgnoreCase))
        {
            return "coil";
        }

        if (normalized.Contains("holding", StringComparison.OrdinalIgnoreCase))
        {
            return "holding";
        }

        if (normalized.Length == 0)
        {
            return "none";
        }

        return normalized.ToLowerInvariant();
    }

    private enum DictionarySection
    {
        None,
        Uticor,
        Plc,
        PlcException,
        BoolTagType,
        ReferenceOnly
    }
}
