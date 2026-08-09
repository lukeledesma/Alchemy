Attribute VB_Name = "Module1"
Option Explicit

' =============================================================================
' Column / Row constants (adjust here if your layout changes)
' =============================================================================
Private Const ROW_FIRST As Long = 8
Private Const COL_GROUP As Long = 1        ' Raw group/name column used for Folder grouping
Private Const COL_DATATYPE As Long = 4     ' "Tag_Data[DataType]"
Private Const COL_MODBUSREG As Long = 6    ' "Tag_Data[Modbus Register]" (X.Y for bit-of-int)
Private Const COL_ADDRSTART As Long = 6    ' Fallback ADDRSTART (currently same column)
Private Const COL_TAGNAME As Long = 14     ' Endpoint/Tag name

' =============================================================================
' Utilities
' =============================================================================

' Splits a Modbus Register of the form "X.Y" into parts.
' Returns True on success, False on any parsing issue.
Private Function TryParseModbusRegister(ByVal reg As String, ByRef baseAddr As Long, ByRef bitIndex As Long) As Boolean
    Dim parts() As String
    TryParseModbusRegister = False

    If Len(Trim$(reg)) = 0 Then Exit Function

    parts = Split(reg, ".")
    If UBound(parts) <> 1 Then Exit Function

    If IsNumeric(parts(0)) And IsNumeric(parts(1)) Then
        baseAddr = CLng(parts(0))
        bitIndex = CLng(parts(1))
        TryParseModbusRegister = True
    End If
End Function

' Returns True if a 2D Variant array is empty (no elements).
Private Function ArrayIsEmpty(ByVal arr As Variant) As Boolean
    On Error GoTo ErrHandler
    If IsArray(arr) Then
        ArrayIsEmpty = (UBound(arr, 1) < LBound(arr, 1))
    Else
        ArrayIsEmpty = True
    End If
    Exit Function
ErrHandler:
    ArrayIsEmpty = True
End Function

' Converts "my tag group" -> "My_Tag_Group", defaults blank -> "PLC"
Function FormatTagGroup(ByVal raw As String) As String
    Dim parts() As String, i As Long, w As String
    raw = Trim(raw)
    If raw = "" Then raw = "PLC"
    parts = Split(raw, " ")
    For i = LBound(parts) To UBound(parts)
        w = parts(i)
        parts(i) = UCase(Left(w, 1)) & LCase(Mid(w, 2))
    Next i
    FormatTagGroup = Join(parts, "_")
End Function

Function getSheetTYPE(ByVal cellValue As String) As String
    Select Case cellValue
        Case "Ethernet": getSheetTYPE = "TCP"
        Case "Serial":   getSheetTYPE = "RTU"
        Case Else:       getSheetTYPE = cellValue
    End Select
End Function

Function getSERIAL(ByVal cellValue As String) As String
    Select Case cellValue
        Case "Ethernet": getSERIAL = "remote"
        Case "Serial":   getSERIAL = "port1"
        Case Else:       getSERIAL = cellValue
    End Select
End Function

' =============================================================================
' Mapping helpers
' =============================================================================

' Determine Modbus function code based on DataType text.
'   - "BOOL" -> "01"
'   - "BOOL (Bit of INT)" -> "03" (bit-of-int reads words)
'   - else -> "03"
Function getRowFUNCCODE(ByVal dataType As String) As String
    If StrComp(Trim$(dataType), "BOOL", vbTextCompare) = 0 Then
        getRowFUNCCODE = "01"
    ElseIf StrComp(Trim$(dataType), "BOOL (Bit of INT)", vbTextCompare) = 0 Then
        getRowFUNCCODE = "03"
    Else
        getRowFUNCCODE = "03"
    End If
End Function

' Return data length string:
'   - If DataType = "BOOL (Bit of INT)": "1[bit]" using Modbus Register bit
'   - Else: "2" if DataType contains DINT or REAL (partial match), else "1"
Function getRowDATALENGTH(ByVal dataType As String, ByVal modbusRegister As String) As String
    Dim baseAddr As Long, bitIdx As Long

    If StrComp(Trim$(dataType), "BOOL (Bit of INT)", vbTextCompare) = 0 Then
        If TryParseModbusRegister(modbusRegister, baseAddr, bitIdx) Then
            getRowDATALENGTH = "1[" & CStr(bitIdx) & "]"
        Else
            getRowDATALENGTH = "1" ' fallback
        End If
        Exit Function
    End If

    If InStr(1, dataType, "DINT", vbTextCompare) > 0 _
       Or InStr(1, dataType, "REAL", vbTextCompare) > 0 Then
        getRowDATALENGTH = "2"
    Else
        getRowDATALENGTH = "1"
    End If
End Function

' Compute ADDRSTART:
'   - If DataType = "BOOL (Bit of INT)": integer part before dot in Modbus Register
'   - Else: return supplied fallback (your previous column value)
Function getRowADDRSTART(ByVal dataType As String, ByVal modbusRegister As String, ByVal fallbackAddr As String) As String
    Dim baseAddr As Long, bitIdx As Long

    If StrComp(Trim$(dataType), "BOOL (Bit of INT)", vbTextCompare) = 0 Then
        If TryParseModbusRegister(modbusRegister, baseAddr, bitIdx) Then
            getRowADDRSTART = CStr(baseAddr)
        Else
            getRowADDRSTART = fallbackAddr
        End If
    Else
        getRowADDRSTART = fallbackAddr
    End If
End Function

Function getDATATYPE(ByVal cellValue As String) As String
    Dim datatypeSheet As Worksheet
    Dim datatypeTable As ListObject
    Dim foundRow As Range

    Set datatypeSheet = ThisWorkbook.Worksheets("DataTypes")
    Set datatypeTable = datatypeSheet.ListObjects("DataTypes")
    Set foundRow = datatypeTable.ListColumns("PLC Data Type").DataBodyRange.Find(What:=cellValue, LookAt:=xlWhole)

    If Not foundRow Is Nothing Then
        getDATATYPE = datatypeTable.ListColumns("Uticor DATATYPE Value").DataBodyRange.Cells( _
                      foundRow.Row - datatypeTable.DataBodyRange.Row + 1, 1).Value
    Else
        getDATATYPE = "0"
    End If
End Function

Function getENCODE(ByVal cellValue As String, ByVal writeValue As String) As String
    Dim datatypeSheet As Worksheet
    Dim datatypeTable As ListObject
    Dim foundRow As Range

    Set datatypeSheet = ThisWorkbook.Worksheets("DataTypes")
    Set datatypeTable = datatypeSheet.ListObjects("DataTypes")
    Set foundRow = datatypeTable.ListColumns("PLC Data Type").DataBodyRange.Find(What:=cellValue, LookAt:=xlWhole)

    If Not foundRow Is Nothing Then
        getENCODE = datatypeTable.ListColumns("Uticor ENCODE Value").DataBodyRange.Cells( _
                    foundRow.Row - datatypeTable.DataBodyRange.Row + 1, 1).Value
    Else
        getENCODE = "255"
    End If
End Function

Function getEXPR(ByVal cellValue As String, ByVal writeValue As String) As String
    If writeValue = "Read+Write" Then
        getEXPR = "1"
    Else
        Select Case cellValue
            Case "1":     getEXPR = "1"
            Case "10":    getEXPR = "0.1"
            Case "100":   getEXPR = "0.01"
            Case "1000":  getEXPR = "0.001"
            Case "10000": getEXPR = "0.0001"
            Case Else:    getEXPR = "1"
        End Select
    End If
End Function

Function getSUBSCRIBE(ByVal cellValue As String) As String
    Select Case cellValue
        Case "Read Only":  getSUBSCRIBE = "off"
        Case "Read+Write": getSUBSCRIBE = "on"
        Case Else:         getSUBSCRIBE = cellValue
    End Select
End Function

Function getIgnitionDataType(ByVal cellValue As String) As String
    Dim datatypeSheet As Worksheet
    Dim datatypeTable As ListObject
    Dim foundRow As Range

    Set datatypeSheet = ThisWorkbook.Worksheets("DataTypes")
    Set datatypeTable = datatypeSheet.ListObjects("DataTypes")
    Set foundRow = datatypeTable.ListColumns("PLC Data Type").DataBodyRange.Find(What:=cellValue, LookAt:=xlWhole)

    If Not foundRow Is Nothing Then
        getIgnitionDataType = datatypeTable.ListColumns("Ignition Data Type").DataBodyRange.Cells( _
                              foundRow.Row - datatypeTable.DataBodyRange.Row + 1, 1).Value
    Else
        getIgnitionDataType = "Int4"
    End If
End Function

' =============================================================================
' Preload sections computation (gap-aware + per-chunk merge)
' =============================================================================

' Calculates preload windows (start, length) for a given FUNCCODE ("03" or "01").
' Steps:
'   1) Gather and sort unique effective addresses for the func code.
'   2) Build contiguous clusters; extend each cluster end by pad (+1 bits, +2 words).
'   3) Project clusters onto 100-aligned chunks, but MERGE by chunk:
'        one window per chunk = [chunkStart .. maxEndNeededInThisChunk]
'   4) Emit sections in ascending chunk order.
Function calculatePreloadSections(ByVal data As Variant, ByVal funcCode As String) As Variant
    Dim i As Long

    ' --- Gather unique numeric effective addresses ---
    Dim addrSet As Object: Set addrSet = CreateObject("Scripting.Dictionary")
    Dim effAddr As String, a As Long

    For i = ROW_FIRST To UBound(data, 1)
        If Trim(CStr(data(i, 2))) = "" Then Exit For
        If getRowFUNCCODE(CStr(data(i, COL_DATATYPE))) = funcCode Then
            effAddr = getRowADDRSTART( _
                        CStr(data(i, COL_DATATYPE)), _
                        CStr(data(i, COL_MODBUSREG)), _
                        CStr(data(i, COL_ADDRSTART)) _
                      )
            If IsNumeric(effAddr) Then
                a = CLng(effAddr)
                If Not addrSet.Exists(a) Then addrSet.Add a, True
            End If
        End If
    Next i

    If addrSet.Count = 0 Then
        calculatePreloadSections = Array()
        Exit Function
    End If

    ' --- Copy/sort addresses ---
    Dim used() As Long, k As Long
    ReDim used(0 To addrSet.Count - 1)
    For k = 0 To addrSet.Count - 1
        used(k) = CLng(addrSet.keys()(k))
    Next k
    QuickSortLong used, LBound(used), UBound(used)

    ' --- Build clusters with padding ---
    Dim pad As Long: pad = IIf(funcCode = "01", 1, 2)
    Dim clusters As Collection: Set clusters = New Collection

    Dim cStart As Long, cEnd As Long
    cStart = used(0): cEnd = used(0)

    For k = 1 To UBound(used)
        If used(k) <= cEnd + 1 Then
            cEnd = used(k)
        Else
            clusters.Add Array(cStart, cEnd + pad) ' close previous cluster (+pad)
            cStart = used(k): cEnd = used(k)
        End If
    Next k
    clusters.Add Array(cStart, cEnd + pad) ' close last cluster

    ' --- Project clusters onto 100-chunks; MERGE per chunk by max end ---
    Dim chunkEnd As Object: Set chunkEnd = CreateObject("Scripting.Dictionary")
    Dim cl() As Variant, cS As Long, cE As Long
    Dim chunk As Long, startChunk As Long, endChunk As Long, thisEnd As Long

    For i = 1 To clusters.Count
        cl = clusters(i)
        cS = CLng(cl(0)): cE = CLng(cl(1))
        If cE < cS Then cE = cS

        startChunk = (cS \ 100) * 100
        endChunk = (cE \ 100) * 100

        For chunk = startChunk To endChunk Step 100
            If chunk < endChunk Then
                thisEnd = chunk + 99
            Else
                thisEnd = cE
            End If
            If Not chunkEnd.Exists(chunk) Or thisEnd > CLng(chunkEnd(chunk)) Then
                chunkEnd(chunk) = thisEnd
            End If
        Next chunk
    Next i

    ' --- Emit sections in ascending chunk order ---
    Dim keys As Variant: keys = chunkEnd.keys
    ' Copy to Long() for sorting
    Dim chunks() As Long: ReDim chunks(0 To UBound(keys))
    For i = 0 To UBound(keys)
        chunks(i) = CLng(keys(i))
    Next i
    QuickSortLong chunks, LBound(chunks), UBound(chunks)

    Dim sections() As Variant
    ReDim sections(1 To UBound(chunks) + 1, 1 To 2)
    For i = 0 To UBound(chunks)
        sections(i + 1, 1) = chunks(i)
        sections(i + 1, 2) = CLng(chunkEnd(chunks(i))) - chunks(i) + 1
    Next i

    calculatePreloadSections = sections
End Function

' QuickSort for Long arrays
Private Sub QuickSortLong(arr() As Long, ByVal first As Long, ByVal last As Long)
    Dim i As Long, j As Long, pivot As Long, tmp As Long
    i = first: j = last
    pivot = arr((first + last) \ 2)
    Do While i <= j
        Do While arr(i) < pivot: i = i + 1: Loop
        Do While arr(j) > pivot: j = j - 1: Loop
        If i <= j Then
            tmp = arr(i): arr(i) = arr(j): arr(j) = tmp
            i = i + 1: j = j - 1
        End If
    Loop
    If first < j Then QuickSortLong arr, first, j
    If i < last Then QuickSortLong arr, i, last
End Sub

' =============================================================================
' Export: XML for Uticor
' =============================================================================
Sub exportAsXmlForUticor()
    Dim sheet As Worksheet: Set sheet = ActiveSheet
    Dim sheetName As String: sheetName = sheet.Name
    Dim dateStr As String: dateStr = Format(Now(), "yyyyMMdd-HHmm")
    Dim defaultFileName As String: defaultFileName = sheetName & "_" & dateStr & ".xml"

    Dim data As Variant: data = sheet.UsedRange.Value

    Dim propTYPE As String: propTYPE = getSheetTYPE(sheet.Range("B3").Value)
    Dim propIP As String:   propIP = sheet.Range("B4").Value
    Dim propPORT As String: propPORT = sheet.Range("B5").Value

    Dim preloadWordsSections As Variant: preloadWordsSections = calculatePreloadSections(data, "03")
    Dim preloadBitsSections As Variant:  preloadBitsSections = calculatePreloadSections(data, "01")

    Dim xml As String
    xml = "<?xml version=""1.11"" encoding=""UTF-8""?>" & vbCrLf
    xml = xml & "<GLOBAL>" & vbCrLf & "  <XML>" & vbCrLf

    Dim j As Long, sectionStart As Long, sectionEnd As Long

    ' Generate Preload Words sections (Function Code 03)
    If Not ArrayIsEmpty(preloadWordsSections) Then
        For j = LBound(preloadWordsSections, 1) To UBound(preloadWordsSections, 1)
            sectionStart = preloadWordsSections(j, 1)
            sectionEnd = sectionStart + preloadWordsSections(j, 2) - 1
            xml = xml & "    <Preload_Words_" & sectionStart & "_" & sectionEnd & ">" & vbCrLf
            xml = xml & "      <TYPE type=""STRING"">""" & propTYPE & """</TYPE>" & vbCrLf
            xml = xml & "      <DEVICEID type=""STRING"">""1""</DEVICEID>" & vbCrLf
            xml = xml & "      <FUNCCODE type=""STRING"">""03""</FUNCCODE>" & vbCrLf
            xml = xml & "      <ADDRSTART type=""STRING"">""" & sectionStart & """</ADDRSTART>" & vbCrLf
            xml = xml & "      <DATALENGTH type=""STRING"">" & preloadWordsSections(j, 2) & "</DATALENGTH>" & vbCrLf
            xml = xml & "      <ALIAS type=""STRING"">""none""</ALIAS>" & vbCrLf
            xml = xml & "      <NODEID type=""STRING"">""Preload""</NODEID>" & vbCrLf
            xml = xml & "      <SERIAL type=""STRING"">""" & getSERIAL(sheet.Range("B3").Value) & """</SERIAL>" & vbCrLf
            xml = xml & "      <IP type=""STRING"">""" & propIP & """</IP>" & vbCrLf
            xml = xml & "      <PORT type=""STRING"">""" & propPORT & """</PORT>" & vbCrLf
            xml = xml & "      <OID type=""STRING"">""none""</OID>" & vbCrLf
            xml = xml & "      <CMMSTR_R type=""STRING"">""public""</CMMSTR_R>" & vbCrLf
            xml = xml & "      <CMMSTR_W type=""STRING"">""public""</CMMSTR_W>" & vbCrLf
            xml = xml & "      <TRIGGER type=""STRING"">""none""</TRIGGER>" & vbCrLf
            xml = xml & "      <PRELOAD type=""STRING"">""none""</PRELOAD>" & vbCrLf
            xml = xml & "      <VERIFY type=""STRING"">""254""</VERIFY>" & vbCrLf
            xml = xml & "      <THRESHOLD type=""STRING"">""0""</THRESHOLD>" & vbCrLf
            xml = xml & "      <DATATYPE type=""STRING"">""103""</DATATYPE>" & vbCrLf
            xml = xml & "      <ENCODE type=""STRING"">""255""</ENCODE>" & vbCrLf
            xml = xml & "      <EXPR type=""STRING"">""1.0""</EXPR>" & vbCrLf
            xml = xml & "      <SUBSCRIBE type=""STRING"">""off""</SUBSCRIBE>" & vbCrLf
            xml = xml & "      <POLL type=""STRING"">""on""</POLL>" & vbCrLf
            xml = xml & "    </Preload_Words_" & sectionStart & "_" & sectionEnd & ">" & vbCrLf
        Next j
    End If

    ' Generate Preload Bits sections (Function Code 01)
    If Not ArrayIsEmpty(preloadBitsSections) Then
        For j = LBound(preloadBitsSections, 1) To UBound(preloadBitsSections, 1)
            sectionStart = preloadBitsSections(j, 1)
            sectionEnd = sectionStart + preloadBitsSections(j, 2) - 1
            xml = xml & "    <Preload_Bits_" & sectionStart & "_" & sectionEnd & ">" & vbCrLf
            xml = xml & "      <TYPE type=""STRING"">""" & propTYPE & """</TYPE>" & vbCrLf
            xml = xml & "      <DEVICEID type=""STRING"">""1""</DEVICEID>" & vbCrLf
            xml = xml & "      <FUNCCODE type=""STRING"">""01""</FUNCCODE>" & vbCrLf
            xml = xml & "      <ADDRSTART type=""STRING"">""" & sectionStart & """</ADDRSTART>" & vbCrLf
            xml = xml & "      <DATALENGTH type=""STRING"">" & preloadBitsSections(j, 2) & "</DATALENGTH>" & vbCrLf
            xml = xml & "      <ALIAS type=""STRING"">""none""</ALIAS>" & vbCrLf
            xml = xml & "      <NODEID type=""STRING"">""Preload""</NODEID>" & vbCrLf
            xml = xml & "      <SERIAL type=""STRING"">""" & getSERIAL(sheet.Range("B3").Value) & """</SERIAL>" & vbCrLf
            xml = xml & "      <IP type=""STRING"">""" & propIP & """</IP>" & vbCrLf
            xml = xml & "      <PORT type=""STRING"">""" & propPORT & """</PORT>" & vbCrLf
            xml = xml & "      <OID type=""STRING"">""none""</OID>" & vbCrLf
            xml = xml & "      <CMMSTR_R type=""STRING"">""public""</CMMSTR_R>" & vbCrLf
            xml = xml & "      <CMMSTR_W type=""STRING"">""public""</CMMSTR_W>" & vbCrLf
            xml = xml & "      <TRIGGER type=""STRING"">""none""</TRIGGER>" & vbCrLf
            xml = xml & "      <PRELOAD type=""STRING"">""none""</PRELOAD>" & vbCrLf
            xml = xml & "      <VERIFY type=""STRING"">""254""</VERIFY>" & vbCrLf
            xml = xml & "      <THRESHOLD type=""STRING"">""0""</THRESHOLD>" & vbCrLf
            xml = xml & "      <DATATYPE type=""STRING"">""103""</DATATYPE>" & vbCrLf
            xml = xml & "      <ENCODE type=""STRING"">""255""</ENCODE>" & vbCrLf
            xml = xml & "      <EXPR type=""STRING"">""1.0""</EXPR>" & vbCrLf
            xml = xml & "      <SUBSCRIBE type=""STRING"">""off""</SUBSCRIBE>" & vbCrLf
            xml = xml & "      <POLL type=""STRING"">""on""</POLL>" & vbCrLf
            xml = xml & "    </Preload_Bits_" & sectionStart & "_" & sectionEnd & ">" & vbCrLf
        Next j
    End If

    ' Endpoints
    Dim i As Long, addrStart As Long
    Dim rawGroup As String, tagGroup As String
    Dim propEndPointName As String, preloadSection As String
    Dim dataType As String, modbusRegister As String, priorAddr As String
    Dim dt As String, mr As String, pa As String

    For i = ROW_FIRST To UBound(data, 1)
        If Trim(CStr(data(i, 2))) = "" Then Exit For

        ' Pull row values AFTER i is valid
        dt = CStr(data(i, COL_DATATYPE))
        mr = CStr(data(i, COL_MODBUSREG))   ' "X.Y" for BOOL (Bit of INT)
        pa = CStr(data(i, COL_ADDRSTART))   ' legacy numeric ADDRSTART (fallback)

        dataType = dt
        modbusRegister = mr
        priorAddr = pa

        rawGroup = CStr(data(i, COL_GROUP))
        tagGroup = FormatTagGroup(rawGroup)

        propEndPointName = CStr(data(i, COL_TAGNAME))

        ' Compute the effective ADDRSTART for THIS ROW (respects "BOOL (Bit of INT)")
        Dim effectiveAddrStart As String
        effectiveAddrStart = getRowADDRSTART(dataType, modbusRegister, priorAddr)

        ' Use numeric form for range checks, falling back safely
        If IsNumeric(effectiveAddrStart) Then
            addrStart = CLng(effectiveAddrStart)
        ElseIf IsNumeric(priorAddr) Then
            addrStart = CLng(priorAddr)
        Else
            addrStart = -1 ' unable to determine; we'll still emit the tag
        End If

        ' Pick the preload section based on FUNCCODE
        preloadSection = ""
        If getRowFUNCCODE(dataType) = "03" Then
            If Not ArrayIsEmpty(preloadWordsSections) Then
                For j = LBound(preloadWordsSections, 1) To UBound(preloadWordsSections, 1)
                    If addrStart >= preloadWordsSections(j, 1) And _
                       addrStart <= preloadWordsSections(j, 1) + preloadWordsSections(j, 2) - 1 Then
                        preloadSection = "Preload_Words_" & preloadWordsSections(j, 1) & "_" & _
                                         (preloadWordsSections(j, 1) + preloadWordsSections(j, 2) - 1)
                        Exit For
                    End If
                Next j
            End If
        ElseIf getRowFUNCCODE(dataType) = "01" Then
            If Not ArrayIsEmpty(preloadBitsSections) Then
                Dim k2 As Long
                For k2 = LBound(preloadBitsSections, 1) To UBound(preloadBitsSections, 1)
                    If addrStart >= preloadBitsSections(k2, 1) And _
                       addrStart <= preloadBitsSections(k2, 1) + preloadBitsSections(k2, 2) - 1 Then
                        preloadSection = "Preload_Bits_" & preloadBitsSections(k2, 1) & "_" & _
                                         (preloadBitsSections(k2, 1) + preloadBitsSections(k2, 2) - 1)
                        Exit For
                    End If
                Next k2
            End If
        End If

        ' Emit endpoint node
        Dim verifyValue As String
        If data(i, 9) = "On Change" Then
            verifyValue = "7"
        ElseIf data(i, 9) = "On Scan-Rate" Or data(i, 8) = "Read+Write" Or data(i, 10) = True Then
            verifyValue = "0"
        Else
            verifyValue = "7"
        End If

        xml = xml & "    <" & propEndPointName & ">" & vbCrLf
        xml = xml & "      <TYPE type=""STRING"">""" & propTYPE & """</TYPE>" & vbCrLf
        xml = xml & "      <DEVICEID type=""STRING"">""1""</DEVICEID>" & vbCrLf
        xml = xml & "      <FUNCCODE type=""STRING"">""" & getRowFUNCCODE(dataType) & """</FUNCCODE>" & vbCrLf
        xml = xml & "      <ADDRSTART type=""STRING"">""" & effectiveAddrStart & """</ADDRSTART>" & vbCrLf
        xml = xml & "      <DATALENGTH type=""STRING"">""" & getRowDATALENGTH(dataType, modbusRegister) & """</DATALENGTH>" & vbCrLf
        xml = xml & "      <ALIAS type=""STRING"">""none""</ALIAS>" & vbCrLf
        xml = xml & "      <NODEID type=""STRING"">""" & tagGroup & """</NODEID>" & vbCrLf
        xml = xml & "      <SERIAL type=""STRING"">""" & getSERIAL(sheet.Range("B3").Value) & """</SERIAL>" & vbCrLf
        xml = xml & "      <IP type=""STRING"">""" & propIP & """</IP>" & vbCrLf
        xml = xml & "      <PORT type=""STRING"">""" & propPORT & """</PORT>" & vbCrLf
        xml = xml & "      <OID type=""STRING"">""none""</OID>" & vbCrLf
        xml = xml & "      <CMMSTR_R type=""STRING"">""public""</CMMSTR_R>" & vbCrLf
        xml = xml & "      <CMMSTR_W type=""STRING"">""public""</CMMSTR_W>" & vbCrLf
        xml = xml & "      <TRIGGER type=""STRING"">""none""</TRIGGER>" & vbCrLf
        xml = xml & "      <PRELOAD type=""STRING"">""" & preloadSection & """</PRELOAD>" & vbCrLf
        xml = xml & "      <ENCODE type=""STRING"">""" & getENCODE(data(i, COL_DATATYPE), data(i, 8)) & """</ENCODE>" & vbCrLf
        xml = xml & "      <VERIFY type=""STRING"">""" & verifyValue & """</VERIFY>" & vbCrLf
        xml = xml & "      <THRESHOLD type=""STRING"">""0""</THRESHOLD>" & vbCrLf
        xml = xml & "      <DATATYPE type=""STRING"">""" & getDATATYPE(data(i, COL_DATATYPE)) & """</DATATYPE>" & vbCrLf
        xml = xml & "      <EXPR type=""STRING"">""" & getEXPR(data(i, 7), data(i, 8)) & """</EXPR>" & vbCrLf
        xml = xml & "      <SUBSCRIBE type=""STRING"">""" & getSUBSCRIBE(data(i, 8)) & """</SUBSCRIBE>" & vbCrLf
        xml = xml & "      <POLL type=""STRING"">""on""</POLL>" & vbCrLf
        xml = xml & "    </" & propEndPointName & ">" & vbCrLf
    Next i

    xml = xml & "  </XML>" & vbCrLf & "</GLOBAL>"

    ' Save file
    Dim filePath As Variant
    filePath = Application.GetSaveAsFilename(InitialFileName:=defaultFileName, _
                                             FileFilter:="XML Files (*.xml), *.xml", _
                                             Title:="Save XML File")
    If filePath = False Then
        MsgBox "File save canceled."
        Exit Sub
    End If

    Dim fileNum As Integer: fileNum = FreeFile
    Open filePath For Output As #fileNum
    Print #fileNum, xml
    Close #fileNum

    MsgBox "XML file saved as " & filePath
End Sub

' =============================================================================
' Export: JSON for Ignition
' =============================================================================
Sub exportAsJsonForIgnition()
    Dim sheet As Worksheet: Set sheet = ActiveSheet
    Dim sheetName As String: sheetName = sheet.Name
    Dim dateStr As String: dateStr = Format(Now(), "yyyyMMdd-HHmm")
    Dim defaultFileName As String: defaultFileName = sheetName & "_Ignition_" & dateStr & ".json"

    Dim data As Variant: data = sheet.UsedRange.Value
    Dim plcName As String: plcName = sheet.Range("B2").Value
    If Left(plcName, 1) <> "[" Then plcName = "[" & plcName
    If Right(plcName, 1) <> "]" Then plcName = plcName & "]"

    ' Create a dictionary of tagGroups under the sheetName
    Dim groupDict As Object
    Set groupDict = CreateObject("Scripting.Dictionary")

    Dim i As Long, rawGroup As String, tagGroup As String
    For i = ROW_FIRST To UBound(data, 1)
        If Trim(CStr(data(i, 2))) = "" Then Exit For

        rawGroup = CStr(data(i, COL_GROUP))
        tagGroup = FormatTagGroup(rawGroup)

        Dim tagName As String, tagDescription As String, tagAddress As String, dataType As String
        Dim opcItemPath As String, tagJson As String

        tagName = CStr(data(i, COL_TAGNAME))
        tagDescription = CStr(data(i, 3))
        tagAddress = CStr(data(i, 5))
        dataType = getIgnitionDataType(CStr(data(i, COL_DATATYPE)))
        opcItemPath = "ns=1;s=" & plcName & tagAddress

        tagJson = "            {" & vbCrLf & _
                  "              ""name"": """ & tagName & """," & vbCrLf & _
                  "              ""tagType"": ""AtomicTag""," & vbCrLf & _
                  "              ""documentation"": """ & tagDescription & """," & vbCrLf & _
                  "              ""tooltip"": """ & tagAddress & """," & vbCrLf & _
                  "              ""valueSource"": ""opc""," & vbCrLf & _
                  "              ""dataType"": """ & dataType & """," & vbCrLf & _
                  "              ""opcItemPath"": """ & opcItemPath & """," & vbCrLf & _
                  "              ""opcServer"": ""Ignition OPC UA Server""" & vbCrLf & _
                  "            }"

        If Not groupDict.Exists(tagGroup) Then
            groupDict.Add tagGroup, New Collection
        End If
        groupDict(tagGroup).Add tagJson
    Next i

    ' Build full JSON structure
    Dim jsonContent As String
    jsonContent = "{" & vbCrLf
    jsonContent = jsonContent & "  ""name"": """ & sheetName & """," & vbCrLf
    jsonContent = jsonContent & "  ""tagType"": ""Folder""," & vbCrLf
    jsonContent = jsonContent & "  ""tags"": [" & vbCrLf

    Dim groupName As Variant, tagBlock As Collection, firstGroup As Boolean
    firstGroup = True
    For Each groupName In groupDict.keys
        If Not firstGroup Then jsonContent = jsonContent & "," & vbCrLf
        jsonContent = jsonContent & "    {" & vbCrLf
        jsonContent = jsonContent & "      ""name"": """ & groupName & """," & vbCrLf
        jsonContent = jsonContent & "      ""tagType"": ""Folder""," & vbCrLf
        jsonContent = jsonContent & "      ""tags"": [" & vbCrLf

        Set tagBlock = groupDict(groupName)
        Dim j As Long
        For j = 1 To tagBlock.Count
            jsonContent = jsonContent & tagBlock(j)
            If j < tagBlock.Count Then jsonContent = jsonContent & ","
            jsonContent = jsonContent & vbCrLf
        Next j

        jsonContent = jsonContent & "      ]" & vbCrLf & "    }"
        firstGroup = False
    Next groupName

    jsonContent = jsonContent & vbCrLf & "  ]" & vbCrLf & "}"

    ' Save to file
    Dim filePath As Variant
    filePath = Application.GetSaveAsFilename(InitialFileName:=defaultFileName, _
                                             FileFilter:="JSON Files (*.json), *.json", _
                                             Title:="Save JSON File")
    If filePath = False Then
        MsgBox "File save canceled."
        Exit Sub
    End If

    Dim fileNum As Integer: fileNum = FreeFile
    Open filePath For Output As #fileNum
    Print #fileNum, jsonContent
    Close #fileNum

    MsgBox "JSON file saved as " & filePath
End Sub

