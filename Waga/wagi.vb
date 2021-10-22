Module wagiPoBluetooth

    Public Enum Waga_protocol
        OKOK
        JD
        ALIBABA
        OKOKCloud
        OKOKCloudV4
        LEXIN
        LEAONE
        UNKNOWN
    End Enum

    Private Function GetProtocolFromSection(oSect As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementDataSection) As Waga_protocol
        Dim aArr As Byte() = oSect.Data.ToArray
        If aArr.Length < 10 Then Return Waga_protocol.UNKNOWN

        If aArr.ElementAt(0) = &HCA Or aArr.ElementAt(0) = &HC0 Then Return Waga_protocol.OKOK
        If aArr.ElementAt(0) = &HA8 And aArr.ElementAt(1) = &H1 Then Return Waga_protocol.ALIBABA

        If aArr.ElementAt(0) = &HFF And aArr.ElementAt(1) = &HF0 Then
            If aArr.ElementAt(2) = &H10 Or aArr.ElementAt(2) = &H11 Then Return Waga_protocol.OKOKCloudV4
            Return Waga_protocol.OKOKCloud
        End If

        Return Waga_protocol.UNKNOWN
    End Function

    Public Function GetProtocolFromAdv(oAdv As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisement) As Waga_protocol

        ' część wedle name
        If oAdv.LocalName.ToLower = "lnv_111" Or oAdv.LocalName.ToLower = "lnv_112" Then Return Waga_protocol.LEAONE

        ' część wedle services
        ' If svcId[1] == 0x18 || svcId[2] == 0xC6  Return r0 z com.chipsea.btlib.util.CsBtUtil_v11.Protocal_Type.JD
        ' If svcId[1] == 0x02 || svcId[2] == 0xA6  Return r0 z com.chipsea.btlib.util.CsBtUtil_v11.Protocal_Type.LEXIN

        ' a część wedle payload
        If oAdv.DataSections IsNot Nothing Then
            DebugOut("GetProtocolFromAdv, DataSections count = " & oAdv.DataSections.Count)

            For Each oItem As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementDataSection In oAdv.DataSections
                Dim iProto As Waga_protocol = GetProtocolFromSection(oItem)
                If iProto <> Waga_protocol.UNKNOWN Then Return iProto
            Next
        End If

        Return Waga_protocol.UNKNOWN
    End Function

    Public Function PrzetworzDaneZWagi(oAdv As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisement, uMac As ULong) As chipseaBroadcastFrame
        DebugOut("PrzetworzDaneZWagi(..., " & uMac.ToHexBytesString())

        Dim protoc As Waga_protocol = GetProtocolFromAdv(oAdv)

        DebugOut("PrzetworzDaneZWagi, protokol " & protoc)

        If protoc = Waga_protocol.UNKNOWN Then Return Nothing
        If protoc = Waga_protocol.LEXIN Then Return Nothing
        If protoc = Waga_protocol.LEAONE Then Return Nothing
        If protoc = Waga_protocol.ALIBABA Then Return Nothing
        If protoc = Waga_protocol.JD Then Return Nothing

        ' czyli musi byc OKOK, ale w kilku wersjahc mozliwe

        ' to jest juz w rozpoznawaniu protokolu - wtedy zwroci UNKNOWN
        'If oAdv.DataSections Is Nothing Then
        '    DebugOut("PrzetworzDaneZWagi: DataSections is null, idę sobie")
        '    Return Nothing
        'End If
        'If oAdv.DataSections.Count < 1 Then
        '    DebugOut("PrzetworzDaneZWagi: DataSections.Count < 1 , idę sobie")
        '    Return Nothing
        'End If
        'If oAdv.DataSections.Count > 1 Then DebugOut("PrzetworzDaneZWagi: DataSections.Count = " & oAdv.DataSections.Count & ", korzystam z pierwszej")

        Dim oFrame As Byte() = Nothing

        If oAdv.DataSections IsNot Nothing Then
            DebugOut("PrzetworzDaneZWagi, DataSections count = " & oAdv.DataSections.Count)

            ' sekcja pasujaca do wagi (zabezpieczenie, jakby bylo kilka sekcji - teoretycznie powinno zadzialac po prostu ElementAt(0)
            For Each oItem As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementDataSection In oAdv.DataSections
                Dim iProto As Waga_protocol = GetProtocolFromSection(oItem)
                oFrame = oItem.Data.ToArray
                Exit For
            Next
        End If

        If oFrame Is Nothing Then
            DebugOut("PrzetworzDaneZWagi, ale nie ma rozpoznanej sekcji?")
            Return Nothing
        End If

        Return New chipseaBroadcastFrame(oFrame, uMac)

    End Function

    'Dim bArr3 As Byte() = oFrame

    '    If bArr3 Is Nothing Then
    '        Throw New Exception("帧格式错误 -- 帧为空")
    '    ElseIf bArr3.ElementAt(0) = &HCA Then

    '    Byte b = bArr3[1];
    '    Byte b2 = bArr3[2];
    '    If (b == 16) Then {
    '        Byte b3 = bArr3[4];
    '        BytesUtil.subBytes(bArr3, 5, 2);
    '        Byte[] subBytes = BytesUtil.subBytes(bArr3, 7, 2);
    '        Byte[] subBytes2 = BytesUtil.subBytes(bArr3, 9, 2);
    '        Byte[] subBytes3 = BytesUtil.subBytes(bArr3, 11, 2);
    '        Byte[] subBytes4 = BytesUtil.subBytes(bArr3, 13, 2);
    '        Byte[] subBytes5 = BytesUtil.subBytes(bArr3, 15, 2);
    '        Byte[] subBytes6 = BytesUtil.subBytes(bArr3, 17, 1);
    '        this.fatScale = New CsFatScale();
    '        this.fatScale.setHistoryData(False);
    '        Byte cmdId = BytesUtil.getCmdId(b3);
    '        this.fatScale.setLockFlag(cmdId);
    '        WeightParserReturn Parser = WeightUnitUtil.Parser(bArr3[5], bArr3[6], b3, False);
    '        Byte[] bArr4 = subBytes4;
    '        this.fatScale.setWeight(Parser.kgWeight * 10D);
    '        this.fatScale.setScaleWeight(Parser.scaleWeight);
    '        this.fatScale.setScaleProperty(b3);
    '        If (cmdId > 0) Then
    '        this.fatScale.setAxunge((Double) BytesUtil.bytesToInt(subBytes));
    '            this.fatScale.setWater((Double) BytesUtil.bytesToInt(subBytes2));
    '            this.fatScale.setMuscle((Double) BytesUtil.bytesToInt(subBytes3));
    '            this.fatScale.setBmr((Double) BytesUtil.bytesToInt(bArr4));
    '            this.fatScale.setVisceral_fat((Double) BytesUtil.bytesToInt(subBytes5));
    '            this.fatScale.setBone((Double) BytesUtil.bytesToInt(subBytes6));
    '        End If
    '    Return enumProcessResult.Received_Scale_Data;
    '    } else if (b != 17) {
    '        Return enumprocessresult;
    '    } else {
    '        Byte b4 = bArr3[3];
    '        switch(b4) {
    '            Case 0
    '    Case 1
    '    Byte[] subBytes7 = BytesUtil.subBytes(bArr3, 5, 2);
    '                this.fatScale = New CsFatScale();
    '                this.fatScale.setHistoryData(False);
    '                this.fatScale.setLockFlag(b4);
    '                this.fatScale.setScaleProperty(bArr3[11]);
    '                WeightParserReturn Parser2 = WeightUnitUtil.Parser(subBytes7[0], subBytes7[1], this.fatScale.getScaleProperty(), False);
    '                this.fatScale.setWeight(Parser2.kgWeight * 10D);
    '                this.fatScale.setScaleWeight(Parser2.scaleWeight);
    '                Return enumProcessResult.Received_Scale_Data;
    '            Default
    '                switch(b4) {
    '                    Case 18
    '    Case 19
    '    If (!packageProcess(bArr)) Then {
    '                            Return enumProcessResult.Wait_Scale_Data;
    '                        }
    '                        If (b4 == 18) Then {
    '                            bArr2 = ConvertMap2ByteArray(False);
    '                        } else {
    '                            bArr2 = ConvertMap2ByteArray(True);
    '                        }
    '                        this.fatScale = New CsFatScale();
    '                        If (b4 == 18) Then {
    '                            this.fatScale.setHistoryData(False);
    '                        } else {
    '                            this.fatScale.setWeighingDate(New Date(((Long) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 0, 4))) * 1000));
    '                            this.fatScale.setHistoryData(True);
    '                        }
    '                        Byte b5 = bArr2[4];
    '                        this.fatScale.setRoleId(BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 6, 4)));
    '                        Byte[] subBytes8 = BytesUtil.subBytes(bArr2, 10, 2);
    '                        this.fatScale.setScaleProperty(b5);
    '                        WeightParserReturn Parser3 = WeightUnitUtil.Parser(subBytes8[0], subBytes8[1], b5, False);
    '                        this.fatScale.setWeight(Parser3.kgWeight * 10D);
    '                        this.fatScale.setScaleWeight(Parser3.scaleWeight);
    '                        this.fatScale.setAxunge((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 12, 2)));
    '                        this.fatScale.setWater((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 14, 2)));
    '                        this.fatScale.setMuscle((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 16, 2)));
    '                        this.fatScale.setBmr((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 18, 2)));
    '                        this.fatScale.setVisceral_fat((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 20, 2)));
    '                        this.fatScale.setBone((Double) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr2, 22, 1)));
    '                        Double muscle = (this.fatScale.getMuscle() / this.fatScale.getWeight()) * 100D;
    '                        If (muscle >= 50D) Then {
    '                            muscle = this.fatScale.getMuscle();
    '                            If (muscle >= 50D) Then {
    '                                muscle = 50D;
    '                            }
    '                        }
    '                        this.fatScale.setMuscle((Double)((int)(muscle * 10D)));
    '                        this.fatScale.setLockFlag((Byte) 1);
    '                        If (b4 == 18) Then {
    '                            this.lstBuffer.clear();
    '                            this.lstBuffer = null;
    '                        } else {
    '                            this.lstHistoryBuffer.clear();
    '                            this.lstHistoryBuffer = null;
    '                        }
    '                        Return enumProcessResult.Received_Scale_Data;
    '                    Case 20
    '    Byte[] splitPackInfo = splitPackInfo(bArr3[4]);
    '                        int i2 = b2 - 2;
    '                        If (this.lstRoleBuffer == null) Then {
    '                            this.lstRoleBuffer = New ArrayList <> ();
    '                        }
    '                        For (int i3 = 0; i3 < i2; i3++) {
    '                            this.lstRoleBuffer.add(Byte.valueOf(bArr3[i3 + 5]));
    '                        }
    '                        If (splitPackInfo[0] != splitPackInfo[1]) {
    '                            Return enumProcessResult.Wait_Scale_Data;
    '                        }
    '                        Byte[] ConverList2ByteArray = ConverList2ByteArray(this.lstRoleBuffer);
    '                        this.fatConfirm = New CsFatConfirm();
    '                        Byte[] subBytes9 = BytesUtil.subBytes(ConverList2ByteArray, 0, 2);
    '                        Byte b6 = ConverList2ByteArray[2];
    '                        WeightParserReturn Parser4 = WeightUnitUtil.Parser(subBytes9[0], subBytes9[1], b6, False);
    '                        this.fatConfirm.setScaleProperty(b6);
    '                        this.fatConfirm.setWeight(Parser4.kgWeight * 10D);
    '                        this.fatConfirm.setScaleWeight(Parser4.scaleWeight);
    '                        ArrayList arrayList = New ArrayList();
    '                        For (int i4 = 3; i4 < ConverList2ByteArray.length; i4 += 4) {
    '                            arrayList.add(Integer.valueOf(BytesUtil.bytesToInt(BytesUtil.subBytes(ConverList2ByteArray, i4, 4))));
    '                        }
    '                        this.fatConfirm.setMatchedRoleList(arrayList);
    '                        this.lstRoleBuffer.clear();
    '                        this.lstRoleBuffer = null;
    '                        Return enumProcessResult.Match_User_Msg;
    '                    Default
    '                        Return enumprocessresult;
    '                }
    '        }
    '    }
    'Else
    '        Throw New Exception("帧格式错误 -- 不是正确的帧头")
    '    End If

    '    Return True

    'End Function


End Module



Public Class chipseaBroadcastFrame


    Public cmdId As Byte
    'Private Byte[] data;
    Public dataLength As Byte
    Public deviceType As Byte
    'Public mac As String
    Public uMac As ULong
    Public measureSeqNo As Integer
    'Public String procotalType;
    Public productId As Integer

    '/* renamed from r1 */
    Public resistance As Double = -1
    Public scaleProperty As Byte ' ORIG: float
    'Public scaleWeight As String
    Public version As Byte
    Public weight As Double

    'Public String toString() {
    '    Return "chipseaBroadcastFrame{version=" + this.version + ", deviceType=" + this.deviceType + ", cmdId=" + this.cmdId + ", weight=" + this.weight + ", productId=" + this.productId + ", mac='" + this.mac + '\'' + ", procotalType='" + this.procotalType + '\'' + ", scaleWeight='" + this.scaleWeight + '\'' + ", scaleProperty=" + this.scaleProperty + ", measureSeqNo=" + this.measureSeqNo + ", r1=" + this.f490r1 + '}';
    '}

    'Public chipseaBroadcastFrame(String str) {
    '    this.version = 0;
    '    this.deviceType = 1;
    '    this.cmdId = 0;
    '    this.weight = 0.0d;
    '    this.productId = 0;
    '    this.mac = str;
    '}

    Private Sub okokParser(bArr As Byte())
        version = bArr.ElementAt(1)
        dataLength = bArr.ElementAt(2)

        Select Case version
            Case 16
                deviceType = bArr.ElementAt(3)
                cmdId = bArr.ElementAt(4)
                scaleProperty = cmdId
            Case 17
                scaleProperty = bArr.ElementAt(3)
                deviceType = bArr.ElementAt(4)
        End Select

        If deviceType = 0 Then
            Select Case version
                Case 16
                    weight = (bArr.ElementAt(8) * 256 + bArr.ElementAt(9)) / 10.0
                Case 17
                    cmdId = BytesUtil.getCmdId(scaleProperty)

                    weight = WeightParserReturn_Parser(bArr.ElementAt(5), bArr.ElementAt(6), scaleProperty, False)
                    'this.scaleWeight = Parser.scaleWeight;
                    productId = ((bArr.ElementAt(7) * 256 + bArr.ElementAt(8)) * 256 + bArr.ElementAt(9)) * 256 + bArr.ElementAt(10)
                Case Else
            End Select
        Else
            'BytesUtil.subBytes(bArr, 5, 2);
            cmdId = BytesUtil.getCmdId(scaleProperty)
            ' WeightParserReturn Parser2 = WeightUnitUtil.Parser(bArr[5], bArr[6], this.scaleProperty, False);
            weight = WeightParserReturn_Parser(bArr.ElementAt(5), bArr.ElementAt(6), scaleProperty, False)
            'this.scaleWeight = Parser2.scaleWeight;
            productId = ((bArr.ElementAt(7) * 256 + bArr.ElementAt(8)) * 256 + bArr.ElementAt(9)) * 256 + bArr.ElementAt(10)
        End If

    End Sub


    Private Sub okokBroadcastParser(bArr As Byte())
        version = bArr.ElementAt(1)
        dataLength = bArr.ElementAt(2)
        productId = ((((bArr.ElementAt(3) * 256) + bArr.ElementAt(4)) * 256) + bArr.ElementAt(5)) * 256 + bArr.ElementAt(6)
        If bArr.ElementAt(7) = 0 Then
            deviceType = 0
        Else
            deviceType = 2
        End If

        scaleProperty = bArr.ElementAt(8)
        cmdId = BytesUtil.getCmdId(scaleProperty)

        If cmdId = 0 Then
            measureSeqNo = 0
            weight = 0.0
            'scaleWeight = ""
            resistance = 0.0
        Else
            measureSeqNo = bArr.ElementAt(9) '  & DataType.EXTEND;
            weight = WeightParserReturn_Parser(bArr.ElementAt(10), bArr.ElementAt(11), scaleProperty, False)
            '            this.scaleWeight = Parser.scaleWeight;
            resistance = (bArr.ElementAt(12) * 256 + bArr.ElementAt(13)) / 10.0 ' ((float) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr, 4, 2))) / 10.0f;
        End If

    End Sub


    Private Sub okokCloudParser(bArr As Byte())
        ' WeightParserReturn weightParserReturn;
        deviceType = 1
        version = bArr.ElementAt(2)
        scaleProperty = bArr.ElementAt(3)
        cmdId = 0
        'Byte[] subBytes = BytesUtil.subBytes(bArr, 6, 4);
        If bArr.ElementAt(6) = &HFA And bArr.ElementAt(7) = &HFB And bArr.ElementAt(8) = &HFC And bArr.ElementAt(8) = &HFD Then
            productId = 997264800
            scaleProperty = 4
        Else
            productId = ((bArr.ElementAt(6) * 256 + bArr.ElementAt(7)) * 256 + bArr.ElementAt(8)) * 256 + bArr.ElementAt(9)
            If productId = 1104000001 Then scaleProperty = 4
        End If

        If bArr.ElementAt(2) = 16 Or bArr.ElementAt(2) = 17 Then
            weight = WeightParserReturn_Parser(bArr.ElementAt(5), bArr.ElementAt(4), scaleProperty)
        Else
            weight = WeightParserReturn_Parser(bArr.ElementAt(5), bArr.ElementAt(4), scaleProperty, True)
        End If
        'this.weight = weightParserReturn.kgWeight;
        'this.scaleWeight = weightParserReturn.scaleWeight;
        scaleProperty = BytesUtil.changeCloudProperty2Standard(scaleProperty)
    End Sub

    Private Sub okokBroadcasetV2Parser(bArr As Byte())

        version = 1
        dataLength = 9

        ' 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E
        ' C0 10 20 8D 13 88 08 08 25 68 0C A5 92 7E C2

        ' 0: start magic = C0
        ' 1: sequence
        ' 2,3: waga (w jednostkach ustawionych w wadze?)
        ' 4,5: r1
        ' 6,7: productId
        ' 8: scaleProperty
        '  b0 (string 7): cmdId (1) 0: no lock, 1:lock (czyli są dane)
        '  b1,2 (str5,2): digits (00)
        '  b3,4 (str3,2): unit (01)
        ' 9..E: unknown

        ' bitno 7654 3210
        ' 25 =  0010 1001
        ' chrno 0123 4567


        If bArr.Length >= 9 Then

            scaleProperty = bArr.ElementAt(8)
            cmdId = BytesUtil.getCmdId(scaleProperty)   ' dwa najmlodsze bity, ale to dziwne, bo z HEX robi INT (tzn. 0x11 jako 11dec)
            DebugOut("okokBroadcasetV2Parser: cmdId = " & cmdId)
            productId = bArr.ElementAt(6) * 256 + bArr.ElementAt(7)
            DebugOut("okokBroadcasetV2Parser: productId = " & String.Format("{0:X}", productId))

            If cmdId = 0 Then
                measureSeqNo = 0
                weight = 0.0
                'scaleWeight = ""
                resistance = 0.0
            Else
                measureSeqNo = bArr.ElementAt(1) '  & DataType.EXTEND;
                weight = WeightParserReturn_Parser(bArr.ElementAt(2), bArr.ElementAt(3), scaleProperty, False)
                '            this.scaleWeight = Parser.scaleWeight;
                resistance = (bArr.ElementAt(4) * 256 + bArr.ElementAt(5)) / 10.0 ' ((float) BytesUtil.bytesToInt(BytesUtil.subBytes(bArr, 4, 2))) / 10.0f;

                DebugOut("data from frame: seq = " & measureSeqNo & ", masa = " & weight & ", opor = " & resistance)
            End If


            If BytesUtil.getDeviceTpe4BV2(scaleProperty) = 0 Then
                deviceType = 0
            Else
                deviceType = 2
            End If
        Else
            DebugOut("okokBroadcasetV2Parser - frame too short")
        End If
    End Sub

    Public Sub New(bArr As Byte(), uMACAddr As ULong)

        DebugOut("chipseaBroadcastFrame:New")

        uMac = uMACAddr

        If bArr Is Nothing Then
            DebugOut("chipseaBroadcastFrame(null)")
            Return
        End If

        Select Case bArr.ElementAt(0)
            Case &HCA
                If bArr.ElementAt(1) = &H20 Then
                    DebugOut("chipseaBroadcastFrame:New, recognized okokBroadcastParser")
                    okokBroadcastParser(bArr)
                Else
                    okokParser(bArr)  ' to nie ma oporu
                End If
            Case &HFF
                okokCloudParser(bArr)  ' to nie ma oporu
            Case &HC0
                DebugOut("chipseaBroadcastFrame:New, recognized okokBroadcasetV2Parser")
                okokBroadcasetV2Parser(bArr)
            Case Else
                DebugOut("chipseaBroadcastFrame() unrecognized frame")
        End Select

    End Sub

End Class


Public Module BytesUtil

    Public Enum Weight_Unit
        KG
        JIN
        LB
        ST
    End Enum

    Public Function byteToBit(b As Byte) As String
        '  return "" + ((byte) ((b >> 7) & 1)) + ((byte) ((b >> 6) & 1)) + ((byte) ((b >> 5) & 1)) + ((byte) ((b >> 4) & 1)) + ((byte) ((b >> 3) & 1)) + ((byte) ((b >> 2) & 1)) + ((byte) ((b >> 1) & 1)) + ((byte) ((b >> 0) & 1));

        Dim sTmp As String = ""
        If (b And &H80) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H40) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H20) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H10) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H8) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H4) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H2) > 0 Then sTmp &= "1" Else sTmp &= "0"
        If (b And &H1) > 0 Then sTmp &= "1" Else sTmp &= "0"

        Return sTmp
    End Function

    Public Function getCmdId(b As Byte) As Byte
        ' return Byte.parseByte(byteToBit(b).substring(7, 8));
        ' Return Byte.Parse(byteToBit(b).Substring(7, 1)) ' UWAGA: Java ma start/end index! , a nie LEN
        Return (b And &H1)
    End Function

    Public Function bytesToInt(bArr As Byte()) As Integer
        Dim iRet As Integer = 0
        For i As Integer = 0 To bArr.Length - 1
            iRet = iRet * 256
            iRet = iRet + bArr(i)
        Next
        '    Return Integer.parseInt(bytesToHexString(bArr), 16);
        '} catch (NumberFormatException unused) {
        Return iRet
    End Function

    Public Function getDeviceTpe4BV2(b As Byte) As Byte
        Return Byte.Parse(byteToBit(b).Substring(2, 1))
    End Function

    Public Function getDigit(b As Byte) As Integer
        Dim byteToBit As String = BytesUtil.byteToBit(b)
        If byteToBit.Substring(5, 2) = "01" Then Return 0
        If byteToBit.Substring(5, 2) = "10" Then Return 2
        Return 1
    End Function
    Public Function getCloudV4Digit(b As Byte) As Integer
        Dim weight_Digit As Integer = 2
        Dim byteToBit As String = BytesUtil.byteToBit(b)
        If byteToBit.Substring(1, 2) = "01" Then Return 0
        If byteToBit.Substring(1, 2) = "10" Then Return 1
        Return 2
    End Function

    Public Function getCloudDigit(b As Byte) As Integer
        Dim byteToBit As String = BytesUtil.byteToBit(b)
        If byteToBit.Substring(5, 2) = "01" Then Return 0
        If byteToBit.Substring(5, 2) = "10" Then Return 1
        Return 2
    End Function


    Public Function getUnit(b As Byte) As Weight_Unit
        Dim byteToBit As String = BytesUtil.byteToBit(b)
        If byteToBit.Substring(3, 2) = "01" Then Return Weight_Unit.JIN
        If byteToBit.Substring(3, 2) = "10" Then Return Weight_Unit.LB
        If byteToBit.Substring(3, 2) = "11" Then Return Weight_Unit.ST
        Return Weight_Unit.KG
    End Function


    Public Function changeCloudProperty2Standard(b As Byte) As Byte
        Dim unit As Weight_Unit = getUnit(b)
        Dim cloudDigit As Integer = getCloudDigit(b)
        Dim retVal As Integer = 0

        Select Case unit
            Case Weight_Unit.JIN
                ' str = "000" & "01"
                retVal = 8
            Case Weight_Unit.LB
                'str = "000" & "10"
                retVal = 16
            Case Weight_Unit.ST
                retVal = 24
                'str = "000" & "11"
            Case Else
                retVal = 0
                'str = "000" & "00"
        End Select

        Select Case cloudDigit
            Case 1
                retVal += 1
                ' str2 = str + "001"
            Case 2
                retVal += 5
                ' 101
            Case Else
                ' 011
                retVal += 3
        End Select

        Return retVal
    End Function


End Module

Public Module WeightUnitUtil

    Public Function WeightParserReturn_Parser(b As Byte, b2 As Byte, b3 As Byte, z As Boolean) As Double

        Dim weight_Digit As Integer
        If z Then
            weight_Digit = BytesUtil.getCloudDigit(b3)
        Else
            weight_Digit = BytesUtil.getDigit(b3)
        End If
        Return getParserReturn(b, b2, b3, weight_Digit)

    End Function
    Public Function WeightParserReturn_Parser(b As Byte, b2 As Byte, b3 As Byte)
        Return getParserReturn(b, b2, b3, BytesUtil.getCloudV4Digit(b3))
    End Function

    Private Function getParserReturn(b As Byte, b2 As Byte, b3 As Byte, weight_Digit As Integer) As Double

        Dim f As Double ' float f;
        Dim unit As Weight_Unit = BytesUtil.getUnit(b3)

        f = b * 256 + b2
        DebugOut("getParserReturn(masa = " & f & ", digits=" & weight_Digit & "), unit=" & unit.ToString)

        ' WeightParserReturn weightParserReturn = New WeightParserReturn();
        Dim i As Integer = weight_Digit
        Select Case weight_Digit
            Case 1
                f = f / 10
            Case 2
                f = f / 100
        End Select

        ' fragment przerabiający na string, w zależności od unit oraz lang
        'DecimalFormatSymbols decimalFormatSymbols = New DecimalFormatSymbols();
        'decimalFormatSymbols.setDecimalSeparator('.');
        'float floatValue = New BigDecimal((Double) f).setScale(i, 4).floatValue();
        'LogUtil.m680i(TAG, "+++scaleTmpWeight+++" + floatValue);
        'If (unit == CsBtUtil_v11.Weight_Unit.ST) Then {
        '    weightParserReturn.scaleWeight = (b & DataType.EXTEND) + ":" + (((float)(b2 & DataType.EXTEND)) / 10.0F);
        '} else if (i == 0) {
        '    weightParserReturn.scaleWeight = "" + ((int) floatValue);
        '} else if (i == 1) {
        '    If (isArOrIw()) Then { // czy jezyk jest AR albo IW
        '        weightParserReturn.scaleWeight = getOneFloat(floatValue) + "";
        '    } else {
        '        weightParserReturn.scaleWeight = New DecimalFormat("#0.0", decimalFormatSymbols).format((Double) floatValue);
        '    }
        '} else if (i == 2) {
        '    If (isArOrIw()) Then {
        '        weightParserReturn.scaleWeight = getTwoFloat(floatValue) + "";
        '    } else {
        '        weightParserReturn.scaleWeight = New DecimalFormat("#0.00", decimalFormatSymbols).format((Double) floatValue);
        '    }
        '}

        Select Case unit
            Case Weight_Unit.JIN
                Return f * 0.5
            Case Weight_Unit.LB
                Return f * 0.4535924
            Case Weight_Unit.ST
                Return (14 * b + b2) * 0.4535924
            Case Else
                Return f
        End Select

    End Function

    Public Sub CSFatScaleLimits(oItem As JedenPomiar)
        ' "E:\Temp\Dekompile\Okok\sources\com\chipsea\btlib\model\device\CsFatScale.java"
        ' property setters przerobione na kontrolę


        '    Public void setAxunge(Double d) {
        '    Double d2 = 0.0D;
        '    If (d >= 65535D) Then {
        '        this.axunge = 0.0D;
        '        Return;
        '    }
        '    Double d3 = d / 10D;
        '    If (d3 >= 5D && d3 <= 45D) Then {
        '        d2 = d3;
        '    }
        '    this.axunge = d2;
        '}

        'Public void setWater(Double d) {
        '    Double d2 = d / 10D;
        '    Double d3 = 0.0D;
        '    If (d2 < 100D && d2 >= 20D && d2 <= 85D) Then {
        '        d3 = d2;
        '    }
        '    this.water = d3;
        '}

        'Public void setMuscle(Double d) {
        '    Double d2 = d / 10D;
        '    If (d2 >= 100D) Then {
        '        d2 = 0.0D;
        '    }
        '    this.muscle = d2;
        '}

        'Public void setBmr(Double d) {
        '    If (d >= 65535D) Then {
        '        this.bmr = 0.0D;
        '        Return;
        '    }
        '    If (d >= 5000D) Then {
        '        d /= 10D;
        '    }
        '    this.bmr = d;
        '}

        'Public void setVisceral_fat(Double d) {
        '    this.ori_visceral_fat = d;
        '    Double d2 = d / 10D;
        '    Double d3 = 0.0D;
        '    If (d2 < 100D && d2 <= 59D) Then {
        '        d3 = d2;
        '    }
        '    this.visceral_fat = d3;
        '}

        'Public void setBone(Double d) {
        '    If (d >= 65535D) Then {
        '        this.bone = 0.0D;
        '        Return;
        '    }
        '    Double d2 = d / 10D;
        '    If (d2 < 1D) Then {
        '        d2 = 0.0D;
        '    } else if (d2 > 4.0d) {
        '        d2 = 4D;
        '    }
        '    this.bone = d2;
        '}

    End Sub

    Public Sub CsAlgoBuilder(oItem As JedenPomiar, bUseBio As Boolean)
        ' "E:\Temp\Dekompile\Okok\sources\com\chipsea\code\algorithm\CsAlgoBuilder.java"
        ' ale inaczej zrobione niż tam. Wyliczamy resztę z podanych danych

        Dim dTemp As Double ' do roznych obliczen

        DebugOut("CsAlgoBuilder - wyliczanka, mam z pomiaru wage " & oItem.weight & " oraz opor " & oItem.opor)
        DebugOut("oraz wiek = " & oItem.age & ", wzrost = " & oItem.height & ", sex=" & oItem.sex)

        ' body mass index (no unit)

        ' Return (this.f511Wt / (this.f510H * this.f510H)) * 100.0F * 100.0F;
        oItem.BMI = (oItem.weight / (oItem.height * oItem.height)) * 100 * 100  ' z cm na metry
        DebugOut("CsAlgoBuilder: BMI = " & oItem.BMI)


        ' body fat rate (%)  (takze znane jako Axunge)
        If bUseBio AndAlso oItem.opor > 1 Then
            ' Public getBFR_Raw()
            If oItem.sex Then
                ' return ((((((this.f510H * -0.3315f) + (this.f511Wt * 0.6216f)) + ((((float) this.Age) * 1.0f) * 0.0183f)) + (this.f512Z * 0.0085f)) + 22.554f) / this.f511Wt) * 100.0f;
                dTemp = ((((((oItem.height * -0.3315F) + (oItem.weight * 0.6216F)) + (oItem.age * 0.0183F)) + (oItem.opor * 0.0085F)) + 22.554F) / oItem.weight) * 100.0
            Else
                dTemp = ((((((oItem.height * -0.3332F) + (oItem.weight * 0.7509F)) + (oItem.age * 0.0196F)) + (oItem.opor * 0.0072F)) + 22.7193F) / oItem.weight) * 100.0
            End If
            DebugOut("CsAlgoBuilder: fat przed minmax = " & dTemp)

            ' Public float getBFR()
            dTemp = dTemp.MinMax(5, 45)
            oItem.fat = dTemp
        Else
            DebugOut("CsAlgoBuilder: fat wedle Wiki, liczone z BMI")
            If oItem.age < 15 Then
                oItem.fat = (1.51 * oItem.BMI) - (0.7 * oItem.age) + 1.4
                If Not oItem.sex Then oItem.fat = oItem.fat - 3.6
            Else
                oItem.fat = (1.39 * oItem.BMI) + (0.16 * oItem.age) - 9
                If Not oItem.sex Then oItem.fat = oItem.fat - 10.34
            End If
        End If
        DebugOut("CsAlgoBuilder: fat ostateczny = " & oItem.fat)


        ' viscereal fat (no unit)
        ' Public float getVFR() {
        If oItem.age <= 17 Then
            DebugOut("CsAlgoBuilder: visceral, ale skoro ponizej 17 lat, to VFR = 0")
            oItem.viscera = 0
        Else
            If oItem.sex Then
                dTemp = (oItem.height * -0.2675F) + (oItem.weight * 0.42F) + (oItem.age * 0.1462F) + (oItem.opor * 0.0123F) + 13.9871F
            Else
                dTemp = (oItem.height * -0.1651F) + (oItem.weight * 0.2628F) + (oItem.age * 0.0649F) + (oItem.opor * 0.0024F) + 12.3445F
            End If
            DebugOut("CsAlgoBuilder: visceral dTemp = " & dTemp)

            Dim f2 As Double
            f2 = Math.Round(dTemp)  ' od razu z zaokrągleniem, w stronę parzystego
            ' float f2 = (float)((int) f);
            f2 = dTemp.MinMax(1, 45)
            oItem.viscera = f2
            DebugOut("CsAlgoBuilder: viscereal ostateczny = " & oItem.viscera)
        End If



        ' water (%)
        ' weightEntity.setWater(csAlgoBuilder.getTFR());

        ' Public float getTFR() {
        If oItem.age <= 17 Then
            DebugOut("CsAlgoBuilder: water, ale skoro ponizej 17 lat, to TFR = 0")
            oItem.water = 0
        Else
            If oItem.sex Then
                dTemp = ((((((oItem.height * 0.0939F) + (oItem.weight * 0.3758F)) - (oItem.age * 0.0032F)) - (oItem.opor * 0.006925F)) + 0.097F) / oItem.weight) * 100.0F
            Else
                dTemp = ((((((oItem.height * 0.0877F) + (oItem.weight * 0.2973F)) + (oItem.age * 0.0128F)) - (oItem.opor * 0.00603F)) + 0.5175F) / oItem.weight) * 100.0F
            End If
            DebugOut("CsAlgoBuilder: water po wyliczeniu = " & dTemp)

            dTemp = dTemp.MinMax(20, 85)

            DebugOut("CsAlgoBuilder: water po minmax = " & dTemp)
            oItem.water = dTemp
        End If


        ' muscle (kg)
        ' rivate float getSLM_Raw() {

        If oItem.fat = 0 Then
            ' tylko na wszelki wypadek, jako ze fat jest liczony powyzej 
            DebugOut("CsAlgoBuilder: muscle. Najpierw musi być policzony FAT! Prosze nie zmieniac kodu!")
        End If

        If oItem.fat >= 45 Then
            ' zapewne było więcej i zostało ścięte na max
            DebugOut("CsAlgoBuilder: muscle - skoro tak duzo tluszczu, to miesni by bylo ze wcale...")
            oItem.muscle = (oItem.weight * 0.55) - 4
        ElseIf oItem.fat <= 5 Then
            DebugOut("CsAlgoBuilder: muscle - skoro tak malo tluszczu, to by byly same miesnie...")
            oItem.muscle = (oItem.weight * 0.95) - 1
        Else
            If oItem.sex Then
                dTemp = ((((oItem.height * 0.2867F) + (oItem.weight * 0.3894F)) - (oItem.age * 0.0408F)) - (oItem.opor * 0.01235F)) - 15.7665F
            Else
                dTemp = ((((oItem.height * 0.3186F) + (oItem.weight * 0.1934F)) - (oItem.age * 0.0206F)) - (oItem.opor * 0.0132F)) - 16.4556F
            End If
        End If
        DebugOut("CsAlgoBuilder: muscle - wyliczone = " & dTemp)
        dTemp = dTemp.MinMax(20, 70)
        oItem.muscle = dTemp



        ' bone (kg)
        ' Public float getMSW() {
        If oItem.muscle = 0 Then
            ' tylko na wszelki wypadek, jako ze muscle jest liczony powyzej (i wymaga fat)
            DebugOut("CsAlgoBuilder: bone. Najpierw musi być policzony muscle! Prosze nie zmieniac kodu!")
        End If
        dTemp = oItem.weight - oItem.weight * oItem.fat - oItem.muscle  ' bo fat jest w procentach!
        DebugOut("CsAlgoBuilder: bone - wyliczone = " & dTemp)
        dTemp = dTemp.MinMax(1, 4)
        DebugOut("CsAlgoBuilder: bone - po minmax = " & dTemp)
        oItem.bone = dTemp


        ' metabolism
        ' weightEntity.setMetabolism(csAlgoBuilder.getBMR());
        ' Public float getBMR() {
        If oItem.sex Then
            dTemp = ((((oItem.height * 7.5037F) + (oItem.weight * 13.1523F)) - (oItem.age * 4.3376F)) - (oItem.opor * 0.3486F)) - 311.7751F
        Else
            dTemp = ((((oItem.height * 7.5432F) + (oItem.weight * 9.9474F)) - (oItem.age * 3.4382F)) - (oItem.opor * 0.309F)) - 288.2821F
        End If
        DebugOut("CsAlgoBuilder: metabolism = " & dTemp)
        oItem.metabol = dTemp


        ' body age
        ' Public int getBodyAge() {

        If oItem.age <= 17 Then
            DebugOut("CsAlgoBuilder: getBodyAge, ale skoro ponizej 17 lat, to VFR = 0")
            oItem.bodyage = 0
        Else
            If oItem.sex Then
                dTemp = (oItem.height * -0.7471F) + (oItem.weight * 0.9161F) + (oItem.age * 0.4184F) + (oItem.opor * 0.0517F) + 54.2267F
            Else
                dTemp = (oItem.height * -1.1165F) + (oItem.weight * 1.5784F) + (oItem.age * 0.4615F) + (oItem.opor * 0.0415F) + 83.2548F
            End If
            DebugOut("CsAlgoBuilder: bodyage dTemp = " & dTemp)

            If dTemp - oItem.age > 10 Then
                dTemp = oItem.age + 10
            ElseIf oItem.age - dTemp > 10 Then
                dTemp = oItem.age - 10
            End If

            DebugOut("CsAlgoBuilder: bodyage, po korekcie roznicy = " & dTemp)

            dTemp = dTemp.MinMax(18, 80)

            DebugOut("CsAlgoBuilder: bodyage, po minmax = " & dTemp)
            oItem.bodyage = dTemp
        End If


        ' lean body mass - wszystko poza tluszczem
        ' a .fat jest w procentach
        oItem.lbm = oItem.weight - ((oItem.fat / 100) * oItem.weight)
        DebugOut("CsAlgoBuilder: LBM = " & oItem.lbm)

        ' protein
        ' wedle "E:\Temp\Dekompile\Okok\sources\com\chipsea\code\engine\DataEngine.java"
        ' muscle: kg, water: %, protein: %
        ' poniekad odpowiada getPM, tylko jednostki są inne
        oItem.protein = ((oItem.muscle - oItem.water * oItem.weight) / oItem.weight) * 100
        DebugOut("CsAlgoBuilder: protein = " & oItem.protein)
        ' uwaga: glupie, bo woda jest chyba nie tylko w muscle?

        ' nie wiem co liczenie:

        'Public Static float getBW(Byte b, float f)
        Dim getBW As Double
        If oItem.sex Then
            getBW = (oItem.height - 80) * 0.7
        Else
            getBW = (oItem.height - 70) * 0.6
        End If
        DebugOut("Nie wiem co: getBW=" & getBW)

        '' Public float getWC() {
        'Dim getWC As Double = getBW - oItem.weight
        'DebugOut("Nie wiem co: getWC=" & getWC)

        '' Public float getBM() {
        'Dim getBM As Double
        'If oItem.sex Then
        '    getBM = oItem.weight * 0.77F
        'Else
        '    getBM = oItem.weight * 0.735F
        'End If
        'DebugOut("Nie wiem co: getBM=" & getBM)

        '' public float getMC() {
        'Dim getMC As Double = getBM - oItem.muscle
        'DebugOut("Nie wiem co: getMC=" & getMC)

        '' Public float getFC() {
        'Dim getFC As Double
        'If oItem.weight < getBW Then
        '    getFC = getWC - getMC
        'Else
        '    If oItem.sex Then
        '        getFC = (((oItem.weight + getMC) * 0.15F) - oItem.fat) / 0.85F
        '    Else
        '        getFC = (((oItem.weight + getMC) * 0.2F) - oItem.fat) / 0.8F
        '    End If
        'End If
        'DebugOut("Nie wiem co: getFC=" & getFC)

        ' Public float getOD() { - to jest obesity :)
        Dim getOD As Double = ((oItem.weight - getBW) / getBW) * 100.0
        oItem.obesity = getOD
        DebugOut("CsAlgoBuilder: obesity=" & getOD)


    End Sub

End Module


' to moje, nie z deasembacji
Public Class JedenPomiar
    Public Property dData As DateTime
    Public Property uMac As ULong
    Public Property iMeasNum As Integer
    Public Property weight As Double
    Public Property rawWeight As Double

    Public Property validBio As Boolean
    Public Property opor As Double = -1

    Public Property sUserName As String
    Public Property age As Double
    Public Property height As Double
    Public Property sex As Boolean

    Public Property BMI As Double      ' juz
    Public Property bone As Double      ' juz
    Public Property fat As Double       ' juz
    Public Property metabol As Double   ' juz
    Public Property muscle As Double    ' juz
    Public Property viscera As Double   ' juz
    Public Property water As Double     ' juz

    Public Property protein As Double
    Public Property obesity As Double
    Public Property bodyage As Integer  ' juz
    Public Property lbm As Double       ' juz

    Public Function ToCSVString(iVersion As Integer, Optional sSep As String = vbTab) As String
        'For Each p As System.Reflection.PropertyInfo In moPomiar.GetType.GetProperties
        '    If p.CanRead Then
        '        sLine = sLine & p.GetValue(moPomiar, Nothing) & vbTab
        '    End If
        'Next

        Dim sLine As String = ""
        sLine = sLine & dData.ToString("yyyy.MM.dd-HH.mm.ss") & sSep
        sLine = sLine & uMac.ToHexBytesString & sSep
        sLine = sLine & iMeasNum & sSep
        sLine = sLine & weight & sSep
        sLine = sLine & rawWeight & sSep

        sLine = sLine & validBio & sSep
        sLine = sLine & opor & sSep

        sLine = sLine & sUserName & sSep
        sLine = sLine & age & sSep
        sLine = sLine & height & sSep
        sLine = sLine & sex & sSep

        sLine = sLine & BMI & sSep
        sLine = sLine & fat & sSep

        sLine = sLine & viscera & sSep
        sLine = sLine & water

        If iVersion < 2 Then Return sLine
        sLine = sLine & sSep

        sLine = sLine & bone & sSep
        sLine = sLine & metabol & sSep
        sLine = sLine & muscle & sSep

        sLine = sLine & protein & sSep
        sLine = sLine & obesity & sSep
        sLine = sLine & bodyage & sSep
        sLine = sLine & lbm

        Return sLine
    End Function

    Public Function GetCSVHdrLine(iVersion As Integer, Optional sSep As String = vbTab) As String
        Dim sLine As String = ""

        sLine = sLine & "date" & sSep
        sLine = sLine & "MAC" & sSep
        sLine = sLine & "seqNo" & sSep
        sLine = sLine & "weight" & sSep
        sLine = sLine & "rawWeight" & sSep

        sLine = sLine & "validBio" & sSep
        sLine = sLine & "bioresistance" & sSep

        sLine = sLine & "userName" & sSep
        sLine = sLine & "age" & sSep
        sLine = sLine & "height" & sSep
        sLine = sLine & "sex " & sSep

        sLine = sLine & "BMI" & sSep
        sLine = sLine & "fat" & sSep

        sLine = sLine & "viscera" & sSep
        sLine = sLine & "water"

        If iVersion < 2 Then Return sLine
        sLine = sLine & sSep

        sLine = sLine & "bone" & sSep
        sLine = sLine & "metabolism" & sSep
        sLine = sLine & "muscle" & sSep

        sLine = sLine & "protein" & sSep
        sLine = sLine & "obesity" & sSep
        sLine = sLine & "bodyage" & sSep
        sLine = sLine & "lbm"

        Return sLine

    End Function

End Class