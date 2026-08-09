using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alchemy;

public partial class AlchemyWindow
{
    private static List<AlchemyTagRow> ParseTagRows(string content)
    {
        var rows = new List<AlchemyTagRow>();
        var xmlMatch = Regex.Match(
            content,
            "<XML>\\s*(?<body>.*)\\s*</XML>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!xmlMatch.Success)
        {
            return rows;
        }

        var xmlBody = xmlMatch.Groups["body"].Value;
        var entryPattern =
            new Regex(
                "<(?<name>\"[^\"]+\"|[A-Za-z0-9_]+)>\\s*(?<body>.*?)\\s*</\\k<name>>",
                RegexOptions.Singleline);

        var sourceIndex = 0;
        foreach (Match entry in entryPattern.Matches(xmlBody))
        {
            var body = entry.Groups["body"].Value;
            if (!body.Contains("<TYPE", StringComparison.OrdinalIgnoreCase) ||
                !body.Contains("<NODEID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawName = entry.Groups["name"].Value;
            var tagName = TrimQuotes(rawName);

            var nodeId = ReadField(body, "NODEID");
            var typeField = ReadField(body, "TYPE");
            var address = ReadField(body, "ADDRSTART");
            var sourceDataLength = ReadField(body, "DATALENGTH");
            var dataTypeCode = ReadField(body, "DATATYPE");
            var encode = ReadField(body, "ENCODE");
            var expr = ReadField(body, "EXPR");
            var subscribe = ReadField(body, "SUBSCRIBE");
            var verify = ReadField(body, "VERIFY");
            var preloadReference = ReadFirstField(
                body,
                "PRELOAD",
                "PRELOADTAG",
                "PRELOAD_TAG",
                "PRELOADNAME",
                "PRELOAD_NAME",
                "READBLOCK",
                "READBLOCKNAME",
                "READ_BLOCK",
                "READ_WORDS",
                "READ_BITS");
            if (string.IsNullOrWhiteSpace(preloadReference))
            {
                preloadReference = ReadFieldContaining(body, "PRELOAD");
            }

            var registerKind = InferRegisterKind(nodeId, typeField, dataTypeCode);

            var resolved = ResolveDataType(dataTypeCode, encode, registerKind);
            var dataType = resolved.PlcDatatypeName;
            var isPlcDatatypeException = DataCatalog.IsPlcDatatypeException(
                dataTypeCode,
                encode);
            var scaling = InferScaling(expr);
            var readWrite = string.Equals(
                    subscribe,
                    "on",
                    StringComparison.OrdinalIgnoreCase)
                ? "Read+Write"
                : "Read Only";
            var updateData = string.IsNullOrWhiteSpace(verify)
                ? string.Empty
                : string.Equals(verify.Trim(), "0", StringComparison.Ordinal)
                    ? "On Scan-Rate"
                    : string.Equals(verify.Trim(), "254", StringComparison.Ordinal)
                        ? string.Empty
                        : "On Change";

            rows.Add(
                new AlchemyTagRow(
                    TagGroup: nodeId,
                    TagName: tagName,
                    DataType: dataType,
                    UticorDatatypeCode: resolved.DataTypeCode,
                    UticorDatatype: resolved.UticorDatatype,
                    UticorEncodeCode: resolved.Encode,
                    UticorEncode: resolved.UticorEncode,
                    SourceDataLength: sourceDataLength,
                    AddressStart: address,
                    Scaling: scaling,
                    ReadWrite: readWrite,
                    UpdateData: updateData,
                    RegisterKind: registerKind,
                    HasAddressConflict: false,
                    HasTagNameConflict: false,
                    IsPreload: false,
                    IsPlcDatatypeException: isPlcDatatypeException,
                    VerifyCode: verify,
                    PreloadReference: preloadReference,
                    PreloadSortKind: "none",
                    SourceIndex: sourceIndex));

            sourceIndex++;
        }

        var tagged = MarkPreloadRows(rows);
        return AnnotateAddressConflicts(tagged);
    }

    private static ConnectionMetadata? ParseConnectionMetadata(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var xmlMatch = Regex.Match(
            content,
            "<XML>\\s*(?<body>.*)\\s*</XML>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!xmlMatch.Success)
        {
            return null;
        }

        var xmlBody = xmlMatch.Groups["body"].Value;
        var entryPattern =
            new Regex(
                "<(?<name>\"[^\"]+\"|[A-Za-z0-9_]+)>\\s*(?<body>.*?)\\s*</\\k<name>>",
                RegexOptions.Singleline);

        foreach (Match entry in entryPattern.Matches(xmlBody))
        {
            var body = entry.Groups["body"].Value;
            if (!body.Contains("<TYPE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var typeField = ReadField(body, "TYPE");
            var ipField = ReadField(body, "IP");
            var portField = ReadField(body, "PORT");
            if (string.IsNullOrWhiteSpace(typeField) &&
                string.IsNullOrWhiteSpace(ipField) &&
                string.IsNullOrWhiteSpace(portField))
            {
                continue;
            }

            return new ConnectionMetadata(
                ConnectionLabel: MapConnectionTypeLabel(typeField),
                IpAddress: string.IsNullOrWhiteSpace(ipField) ? null : ipField,
                Port: string.IsNullOrWhiteSpace(portField) ? null : portField);
        }

        return null;
    }

    private static string? MapConnectionTypeLabel(string typeField)
    {
        if (string.IsNullOrWhiteSpace(typeField))
        {
            return null;
        }

        var normalized = typeField.Trim().ToUpperInvariant();
        return normalized switch
        {
            "TCP" => "TCP",
            "RTU" => "RTU",
            _ => normalized
        };
    }

    private static List<AlchemyTagRow> MarkPreloadRows(IReadOnlyList<AlchemyTagRow> rows)
    {
        var indexByTagName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var tagName = rows[index].TagName;
            if (!indexByTagName.ContainsKey(tagName))
            {
                indexByTagName[tagName] = index;
            }
        }

        var referencedPreloadIndexes = new HashSet<int>();
        var preloadReferencers = new Dictionary<int, List<AlchemyTagRow>>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.PreloadReference))
            {
                continue;
            }

            if (indexByTagName.TryGetValue(row.PreloadReference.Trim(), out var referencedIndex))
            {
                referencedPreloadIndexes.Add(referencedIndex);
                if (!preloadReferencers.TryGetValue(referencedIndex, out var referencers))
                {
                    referencers = [];
                    preloadReferencers[referencedIndex] = referencers;
                }

                referencers.Add(row);
            }
        }

        var markedRows = new List<AlchemyTagRow>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var isReferencedPreload = referencedPreloadIndexes.Contains(index);
            var isDummyPreload = string.Equals(
                                     AlchemyDataCatalog.Normalize(row.UticorDatatypeCode),
                                     "103",
                                     StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(
                                     row.UticorDatatype,
                                     "Dummy",
                                     StringComparison.OrdinalIgnoreCase);
            var isNoPublishPreload = string.Equals(
                AlchemyDataCatalog.Normalize(row.VerifyCode),
                "254",
                StringComparison.OrdinalIgnoreCase);

            var isPreload = isReferencedPreload ||
                            isDummyPreload ||
                            isNoPublishPreload ||
                            IsLegacyPreloadName(row.TagName);

            var preloadDisplayType = row.DataType;
            var preloadSortKind = "none";
            if (isPreload &&
                string.Equals(row.DataType, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(AlchemyDataCatalog.Normalize(row.UticorDatatypeCode), "103", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(AlchemyDataCatalog.Normalize(row.UticorEncodeCode), "255", StringComparison.OrdinalIgnoreCase))
            {
                preloadDisplayType = "Dummy";
                preloadSortKind = ResolvePreloadSortKind(
                    row,
                    preloadReferencers.TryGetValue(index, out var referencers)
                        ? referencers
                        : null);
            }
            else if (isPreload)
            {
                preloadSortKind = AlchemyDataCatalog.NormalizeRegisterKind(row.RegisterKind);
            }

            markedRows.Add(
                row with
                {
                    IsPreload = isPreload,
                    DataType = preloadDisplayType,
                    UpdateData = isPreload ? string.Empty : row.UpdateData,
                    PreloadSortKind = preloadSortKind
                });
        }

        return markedRows;
    }

    private static string ResolvePreloadSortKind(
        AlchemyTagRow preloadRow,
        IReadOnlyList<AlchemyTagRow>? referencers)
    {
        var hasCoilReferencer = referencers?.Any(
            row => string.Equals(
                AlchemyDataCatalog.NormalizeRegisterKind(row.RegisterKind),
                "coil",
                StringComparison.OrdinalIgnoreCase)) == true;
        var hasHoldingReferencer = referencers?.Any(
            row => string.Equals(
                AlchemyDataCatalog.NormalizeRegisterKind(row.RegisterKind),
                "holding",
                StringComparison.OrdinalIgnoreCase)) == true;

        if (hasCoilReferencer && !hasHoldingReferencer)
        {
            return "coil";
        }

        if (hasHoldingReferencer && !hasCoilReferencer)
        {
            return "holding";
        }

        if (preloadRow.TagName.StartsWith("Preload_Bits", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preloadRow.TagName, "Read_Bits", StringComparison.OrdinalIgnoreCase))
        {
            return "coil";
        }

        if (preloadRow.TagName.StartsWith("Preload_Words", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preloadRow.TagName, "Read_Words", StringComparison.OrdinalIgnoreCase))
        {
            return "holding";
        }

        return string.Equals(
            AlchemyDataCatalog.NormalizeRegisterKind(preloadRow.RegisterKind),
            "coil",
            StringComparison.OrdinalIgnoreCase)
            ? "coil"
            : "holding";
    }

    private static bool IsLegacyPreloadName(string tagName)
    {
        return tagName.StartsWith("Preload_Words", StringComparison.OrdinalIgnoreCase) ||
               tagName.StartsWith("Preload_Bits", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tagName, "Read_Words", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tagName, "Read_Bits", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadFirstField(string body, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var value = ReadField(body, fieldName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ReadFieldContaining(string body, string fieldContains)
    {
        var pattern = $"<(?<field>[A-Za-z0-9_]*{Regex.Escape(fieldContains)}[A-Za-z0-9_]*)[^>]*>(?<value>.*?)</\\k<field>>";
        var match = Regex.Match(
            body,
            pattern,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return match.Success
            ? TrimQuotes(match.Groups["value"].Value.Trim())
            : string.Empty;
    }

    private static bool IsDefaultScaling(string scaling)
    {
        return double.TryParse(
                   scaling,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var value) &&
               (Math.Abs(value - 1d) < 0.0000001 ||
                Math.Abs(value - 10d) < 0.0000001 ||
                Math.Abs(value - 100d) < 0.0000001 ||
                Math.Abs(value - 1000d) < 0.0000001);
    }

    private IBrush? GetDatatypeCellBrush(AlchemyTagRow row)
    {
        if (HasDataLengthMismatch(row))
        {
            return _datatypeMismatchBrush;
        }

        if (row.IsPlcDatatypeException)
        {
            return _datatypeExceptionBrush;
        }

        return row.DataType == "Unknown"
            ? _datatypeUnknownBrush
            : null;
    }

    private static bool HasDataLengthMismatch(AlchemyTagRow row)
    {
        if (row.IsPreload)
        {
            return false;
        }

        if (!TryGetNumericDataLength(row.SourceDataLength, out var sourceLength) ||
            !TryGetExcelOutputDataLength(row.DataType, out var outputLength))
        {
            return false;
        }

        return sourceLength != outputLength;
    }

    private static bool TryGetNumericDataLength(string text, out int length)
    {
        var match = Regex.Match(text ?? string.Empty, @"^\s*(?<len>\d+)");
        if (!match.Success ||
            !int.TryParse(match.Groups["len"].Value, out length))
        {
            length = 0;
            return false;
        }

        return length > 0;
    }

    private static bool TryGetExcelOutputDataLength(string dataType, out int length)
    {
        var normalized = dataType.Trim();
        if (normalized.Length == 0 ||
            string.Equals(normalized, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            length = 0;
            return false;
        }

        length = (normalized.Contains("DINT", StringComparison.OrdinalIgnoreCase) ||
                  normalized.Contains("REAL", StringComparison.OrdinalIgnoreCase))
            ? 2
            : 1;
        return true;
    }

    private Control BuildDatatypeTooltipContent(AlchemyTagRow row)
    {
        var stack = new StackPanel
        {
            Spacing = 4
        };

        stack.Children.Add(CreateTooltipLine(
            $"Datatype - {row.UticorDatatypeCode}: {row.UticorDatatype}"));
        stack.Children.Add(CreateTooltipLine(
            $"Encode - {row.UticorEncodeCode}: {row.UticorEncode}"));
        var currentLength = string.IsNullOrWhiteSpace(row.SourceDataLength)
            ? "Unknown"
            : row.SourceDataLength.Trim();
        stack.Children.Add(CreateTooltipLine($"Datalength - {currentLength}"));

        var hasDatatypeRepair = TryGetRepairedTooltipLines(
            row,
            out var repairedDatatypeLine,
            out var repairedEncodeLine);
        var hasDataLengthRepair = HasDataLengthMismatch(row);
        if (hasDatatypeRepair || hasDataLengthRepair)
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(0, 2, 0, 2),
                Background = _dividerBrush
            });

            if (hasDatatypeRepair && !string.IsNullOrWhiteSpace(repairedDatatypeLine))
            {
                stack.Children.Add(CreateTooltipLine(repairedDatatypeLine, _datatypeExceptionBrush));
            }

            if (hasDatatypeRepair && !string.IsNullOrWhiteSpace(repairedEncodeLine))
            {
                stack.Children.Add(CreateTooltipLine(repairedEncodeLine, _datatypeExceptionBrush));
            }

            if (hasDataLengthRepair)
            {
                var outputLength = GetExcelOutputDataLengthDisplay(row.DataType);
                stack.Children.Add(CreateTooltipLine(
                    $"Datalength - {outputLength}",
                    _addressConflictBrush));
            }
        }

        if (_isEditMode && IsEditableFieldChanged(row, AlchemyEditableField.DataType))
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(0, 2, 0, 2),
                Background = _dividerBrush
            });
            if (_editBaselineRows.TryGetValue(row.SourceIndex, out var baseline))
            {
                stack.Children.Add(CreateTooltipLine(
                    $"Original datatype - {baseline.UticorDatatypeCode}: {baseline.UticorDatatype}"));
                stack.Children.Add(CreateTooltipLine(
                    $"Original encode - {baseline.UticorEncodeCode}: {baseline.UticorEncode}"));
                stack.Children.Add(CreateTooltipLine(
                    $"Original datalength - {(string.IsNullOrWhiteSpace(baseline.SourceDataLength) ? "Unknown" : baseline.SourceDataLength)}"));
            }
            else
            {
                stack.Children.Add(CreateTooltipLine("Original: (new row)"));
            }
        }

        return stack;
    }

    private static TextBlock CreateTooltipLine(string text, IBrush? foreground = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 11,
            MaxWidth = 860,
            TextWrapping = TextWrapping.Wrap
        };

        if (foreground is not null)
        {
            block.Foreground = foreground;
        }

        return block;
    }

    private bool TryGetRepairedTooltipLines(
        AlchemyTagRow row,
        out string? datatypeLine,
        out string? encodeLine)
    {
        datatypeLine = null;
        encodeLine = null;

        if (!row.IsPlcDatatypeException ||
            !TryGetExcelOutputUticorPair(row.DataType, out var repairedDatatype, out var repairedEncode))
        {
            return false;
        }

        var hasDatatypeChange =
            !string.Equals(
                AlchemyDataCatalog.Normalize(row.UticorDatatypeCode),
                repairedDatatype,
                StringComparison.Ordinal);
        var hasEncodeChange =
            !string.Equals(
                AlchemyDataCatalog.Normalize(row.UticorEncodeCode),
                repairedEncode,
                StringComparison.Ordinal);

        if (!hasDatatypeChange && !hasEncodeChange)
        {
            return false;
        }

        var datatypeLabel = DataCatalog.TryResolveUticorCode(repairedDatatype, out var resolvedDatatypeLabel)
            ? resolvedDatatypeLabel
            : "Unknown";
        var encodeLabel = DataCatalog.TryResolveUticorCode(repairedEncode, out var resolvedEncodeLabel)
            ? resolvedEncodeLabel
            : "Unknown";

        if (hasDatatypeChange)
        {
            datatypeLine = $"Datatype - {repairedDatatype}: {datatypeLabel}";
        }

        if (hasEncodeChange)
        {
            encodeLine = $"Encode - {repairedEncode}: {encodeLabel}";
        }

        return true;
    }

    private static bool TryGetExcelOutputUticorPair(
        string dataType,
        out string datatype,
        out string encode)
    {
        var normalized = dataType.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "BOOL":
            case "BOOL (BIT OF INT)":
                datatype = "107";
                encode = "255";
                return true;
            case "INT":
                datatype = "0";
                encode = "255";
                return true;
            case "UINT":
                datatype = "1";
                encode = "255";
                return true;
            case "INT (SCALED)":
                datatype = "0";
                encode = "102";
                return true;
            case "UINT (SCALED)":
                datatype = "1";
                encode = "102";
                return true;
            case "DINT (SCALED)":
                datatype = "4";
                encode = "32";
                return true;
            case "DINT (SCALED, W/BYTE SWAP)":
                datatype = "7";
                encode = "32";
                return true;
            case "UDINT (SCALED)":
                datatype = "8";
                encode = "32";
                return true;
            case "UDINT (SCALED, W/BYTE SWAP)":
                datatype = "17";
                encode = "32";
                return true;
            case "DINT":
                datatype = "4";
                encode = "255";
                return true;
            case "DINT (W/BYTE SWAP)":
                datatype = "7";
                encode = "4";
                return true;
            case "UDINT":
                datatype = "8";
                encode = "255";
                return true;
            case "UDINT (W/BYTE SWAP)":
                datatype = "17";
                encode = "8";
                return true;
            case "REAL":
                datatype = "32";
                encode = "255";
                return true;
            case "REAL (W/BYTE SWAP)":
                datatype = "35";
                encode = "32";
                return true;
            default:
                datatype = string.Empty;
                encode = string.Empty;
                return false;
        }
    }

    private static string GetExcelOutputDataLengthDisplay(string dataType)
    {
        if (string.Equals(dataType.Trim(), "BOOL (Bit of INT)", StringComparison.OrdinalIgnoreCase))
        {
            return "1[bit]";
        }

        return TryGetExcelOutputDataLength(dataType, out var outputLength)
            ? outputLength.ToString(CultureInfo.InvariantCulture)
            : "Unknown";
    }

    private static List<AlchemyTagRow> AnnotateAddressConflicts(
        IReadOnlyList<AlchemyTagRow> rows)
    {
        var addressBuckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var tagNameBuckets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.IsPreload)
            {
                continue;
            }

            var tagName = CanonicalTagNameKey(row.TagName);
            if (!string.IsNullOrWhiteSpace(tagName))
            {
                if (!tagNameBuckets.TryGetValue(tagName, out var tagNameIndexes))
                {
                    tagNameIndexes = [];
                    tagNameBuckets[tagName] = tagNameIndexes;
                }

                tagNameIndexes.Add(index);
            }

            if (string.IsNullOrWhiteSpace(row.AddressStart))
            {
                continue;
            }

            if (!string.Equals(row.RegisterKind, "coil", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.RegisterKind, "holding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rowScope = row.IsPreload
                ? "preload"
                : "tag";
            var key = $"{rowScope}:{row.RegisterKind}:{row.AddressStart.Trim()}";
            if (!addressBuckets.TryGetValue(key, out var indices))
            {
                indices = [];
                addressBuckets[key] = indices;
            }

            indices.Add(index);
        }

        var addressConflictIndexes = new HashSet<int>();
        foreach (var pair in addressBuckets)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            foreach (var index in pair.Value)
            {
                addressConflictIndexes.Add(index);
            }
        }

        var tagNameConflictIndexes = new HashSet<int>();
        foreach (var pair in tagNameBuckets)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            foreach (var index in pair.Value)
            {
                tagNameConflictIndexes.Add(index);
            }
        }

        var annotated = new List<AlchemyTagRow>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            annotated.Add(
                row with
                {
                    HasAddressConflict = addressConflictIndexes.Contains(index),
                    HasTagNameConflict = tagNameConflictIndexes.Contains(index)
                });
        }

        return annotated;
    }

    private static string CanonicalTagNameKey(string tagName)
    {
        return TrimQuotes(tagName).Trim();
    }

    private static string ReadField(string body, string field)
    {
        var match = Regex.Match(
            body,
            $"<{field}[^>]*>(?<value>.*?)</{field}>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return match.Success
            ? TrimQuotes(match.Groups["value"].Value.Trim())
            : string.Empty;
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        return value;
    }

    private static AlchemyDatatypeRecord ResolveDataType(
        string dataTypeCode,
        string encode,
        string registerKind)
    {
        var normalizedDataTypeCode = AlchemyDataCatalog.Normalize(dataTypeCode);
        var normalizedEncode = AlchemyDataCatalog.Normalize(encode);
        var normalizedRegisterKind = AlchemyDataCatalog.NormalizeRegisterKind(registerKind);

        if (!DataCatalog.TryResolvePlcDatatype(
                normalizedDataTypeCode,
                normalizedEncode,
                out var plcDatatypeName))
        {
            plcDatatypeName = "Unknown";
        }

        if (normalizedDataTypeCode == "107" &&
            (normalizedEncode == "255" || normalizedEncode == "107") &&
            DataCatalog.TryResolveBoolTagTypeLabel(normalizedRegisterKind, out var boolTagLabel))
        {
            plcDatatypeName = boolTagLabel;
        }

        if (!DataCatalog.TryResolveUticorCode(normalizedDataTypeCode, out var uticorDatatype))
        {
            uticorDatatype = "Unknown";
        }

        if (!DataCatalog.TryResolveUticorCode(normalizedEncode, out var uticorEncode))
        {
            uticorEncode = "Unknown";
        }

        return new AlchemyDatatypeRecord(
            normalizedDataTypeCode,
            normalizedEncode,
            plcDatatypeName,
            uticorDatatype,
            uticorEncode,
            normalizedRegisterKind);
    }

    private static string InferRegisterKind(
        string nodeId,
        string typeField,
        string dataTypeCode)
    {
        var nodeKind = AlchemyDataCatalog.NormalizeRegisterKind(nodeId);
        if (nodeKind == "coil" || nodeKind == "holding")
        {
            return nodeKind;
        }

        var typeKind = AlchemyDataCatalog.NormalizeRegisterKind(typeField);
        if (typeKind == "coil" || typeKind == "holding")
        {
            return typeKind;
        }

        var normalizedDataTypeCode = AlchemyDataCatalog.Normalize(dataTypeCode);
        return normalizedDataTypeCode switch
        {
            "107" => "coil",
            "103" => "none",
            "255" => "none",
            _ => "holding"
        };
    }

    private static string InferScaling(string expr)
    {
        if (!double.TryParse(
                expr,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ||
            Math.Abs(value) < 0.0000001)
        {
            return "1";
        }

        var scaling = 1d / value;
        if (Math.Abs(scaling - Math.Round(scaling)) < 0.0000001)
        {
            return Math.Round(scaling).ToString(CultureInfo.InvariantCulture);
        }

        return scaling.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed record AlchemyTagRow(
        string TagGroup,
        string TagName,
        string DataType,
        string UticorDatatypeCode,
        string UticorDatatype,
        string UticorEncodeCode,
        string UticorEncode,
        string SourceDataLength,
        string AddressStart,
        string Scaling,
        string ReadWrite,
        string UpdateData,
        string RegisterKind,
        bool HasAddressConflict,
        bool HasTagNameConflict,
        bool IsPreload,
        bool IsPlcDatatypeException,
        string VerifyCode,
        string PreloadReference,
        string PreloadSortKind,
        int SourceIndex);

    private enum AlchemyEditableField
    {
        TagGroup,
        TagName,
        DataType,
        AddressStart,
        Scaling,
        ReadWrite,
        UpdateData
    }

    private sealed record AlchemyCellEditTarget(
        AlchemyTagRow OriginalRow,
        AlchemyEditableField Field,
        Grid? HostGrid = null,
        int Column = -1);

    private sealed record AlchemyCellEditRequest(
        int SourceIndex,
        AlchemyEditableField Field,
        int Column);

    private enum EditExitChoice
    {
        Cancel,
        Save,
        SaveAs,
        Discard
    }

    private enum AlchemySaveFormat
    {
        Xml,
        Csv,
        XmlTar
    }

    private sealed record AlchemyEditSnapshot(
        IReadOnlyList<AlchemyTagRow> BeforeRows,
        IReadOnlyList<AlchemyTagRow> AfterRows,
        ConnectionMetadata? BeforeConnection = null,
        ConnectionMetadata? AfterConnection = null);

    private sealed record AlchemyDatatypeRecord(
        string DataTypeCode,
        string Encode,
        string PlcDatatypeName,
        string UticorDatatype,
        string UticorEncode,
        string RegisterKind);

    private sealed record RowVisual(
        AlchemyTagRow Row,
        Border Border,
        int VisualIndex);

    private sealed record ConnectionMetadata(
        string? ConnectionLabel,
        string? IpAddress,
        string? Port);

    private sealed record PreloadSection(int Start, int End);

    private sealed record PanelFileDiagnostics(
        int AddressConflictCount,
        int TagNameConflictCount,
        int UnknownDatatypeCount,
        int RepairedDatatypeCount,
        int OddScalingCount);

    private sealed record PanelFileDiagnosticsCacheEntry(
        DateTime LastWriteTimeUtc,
        long Length,
        PanelFileDiagnostics Diagnostics);
}
