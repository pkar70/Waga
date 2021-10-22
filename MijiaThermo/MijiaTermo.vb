
Public Module pkarBTmijia

    Public Function MijiaDecodeRecord(aArray As Byte()) As JedenRekord

        Dim oRecords As JedenRekord = New JedenRekord
        If aArray.Length < 14 Then Return oRecords

        oRecords.lInd = aArray.ElementAt(3)
        oRecords.lInd = 256 * oRecords.lInd + aArray.ElementAt(2)
        oRecords.lInd = 256 * oRecords.lInd + aArray.ElementAt(1)
        oRecords.lInd = 256 * oRecords.lInd + aArray.ElementAt(0)


        Dim lUnixTicks As ULong = aArray.ElementAt(7)
        lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(6)
        lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(5)
        lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(4)
        oRecords.dDate = DateTimeOffset.FromUnixTimeSeconds(lUnixTicks)

        oRecords.dMaxT = (aArray.ElementAt(9) * 256 + aArray.ElementAt(8)) / 10
        oRecords.iMaxH = aArray.ElementAt(10)

        oRecords.dMinT = (aArray.ElementAt(12) * 256 + aArray.ElementAt(11)) / 10
        oRecords.iMinH = aArray.ElementAt(13)

        Return oRecords
    End Function

    Public Function GetRecordsLogLine(oRec As JedenRekord) As String
        Return oRec.uMAC.ToHexBytesString & vbTab & oRec.lInd.ToString("00000") & vbTab &
            oRec.dDate.ToString("yyyy.MM.dd HH:mm") & vbTab &
            oRec.dMinT.ToString("#0.00") & vbTab & oRec.dMaxT.ToString("#0.00") & vbTab & oRec.iMinH & vbTab & oRec.iMaxH
        'Return oRec.uMAC.ToHexBytesString & "|" & oRec.lInd.ToString("00000") &
        '    oRec.dDate.ToString("yyyy.MM.dd HH:mm") & "|" &
        '    oRec.dMinT.ToString("#0.00") & "|" & oRec.dMaxT.ToString("#0.00") & "|" & oRec.iMinH & "|" & oRec.iMaxH
    End Function

    Public Function MijiaDecodePomiar(aArray As Byte()) As JedenPomiar

        Dim oNew As JedenPomiar = New JedenPomiar
        oNew.dTimeStamp = DateTime.Now
        oNew.iHigro = aArray.ElementAt(2)
        oNew.dTemp = (aArray.ElementAt(1) * 256 + aArray.ElementAt(0)) / 100
        oNew.iBattMV = aArray.ElementAt(4) * 256 + aArray.ElementAt(3)
        Return oNew
    End Function

    Public Async Function GetBTserviceFromMacAsync(uMac As ULong, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService)
        DebugOut("GetBTserviceFromMacAsync(" & uMac.ToHexBytesString & ", bForce=" & bForce & ", bMsg=" & bMsg & ") starting")

        If uMac = 0 Then Return Nothing

        If App.gmTermo Is Nothing Then
            DebugOut("GetBTserviceFromMacAsync, and no App.gmTermo")
            Return Nothing
        End If

        Dim oItem As JedenTermo = App.gmTermo.GetTermo(uMac)
        If oItem Is Nothing Then
            DebugOut("GetBTserviceFromMacAsync, cannot find termo with this MAC")
            Return Nothing
        End If

        If oItem.oGATTsvc IsNot Nothing AndAlso Not bForce Then
            DebugOut("GetBTserviceFromMacAsync, returning remembered oGATTsvc")
            Return oItem.oGATTsvc
        End If

        Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(oItem.uMAC)
        If oDev Is Nothing Then
            DebugOut("GetBTserviceFromMacAsync, oDev null, cannot continue")
            If bMsg Then Await DialogBoxResAsync("msgNoDeviceTryRescan")
            Return Nothing
        End If

        Dim oTemp As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult = Nothing

        ' pętla podobnie jak w RGBLed - tam jest wokół oTemp.Services.Count, nie Status
        For i As Integer = 1 To 10
            If bForce Then
                DebugOut("Request bez cache")
                oTemp = Await oDev.GetGattServicesForUuidAsync(New Guid("ebe0ccb0-7a0a-4b0c-8a1a-6ff2997da3a6"),
                                Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached)
            Else
                oTemp = Await oDev.GetGattServicesForUuidAsync(New Guid("ebe0ccb0-7a0a-4b0c-8a1a-6ff2997da3a6"))
            End If
            bForce = False  ' kolejny raz już nie resetujemy...

            If oTemp Is Nothing Then
                DebugOut("GetBTserviceFromMacAsync, null from GetGattServicesForUuidAsync")
                Return Nothing
            End If

            If oTemp.Status = Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then Exit For
            If oTemp.Status = Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.AccessDenied Then Exit For

            DebugOut("GetBTserviceFromMacAsync, Cannot get GATT service: status=" & oTemp.Status.ToString & ", looping")
            Await Task.Delay(100)
        Next

        If oTemp.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            If bMsg Then Await DialogBoxAsync("Cannot get GATT service")
            DebugOut("GetBTserviceFromMacAsync, Cannot get GATT service: status=" & oTemp.Status.ToString)
            oDev.Dispose()
            Return Nothing
        End If

        If oTemp.Services.Count < 1 Then
            DebugOut("GetBTserviceFromMacAsync, zero services from GetGattServicesForUuidAsync")
            oDev.Dispose()
            Return Nothing
        End If

        oItem.oGATTsvc = oTemp.Services.ElementAt(0)

        Return oItem.oGATTsvc

    End Function


    Public Async Function GetBTcharacterForMijiaAsync(uMAC As ULong, sGuid As String, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic)

        DebugOut("GetBTcharacterForMijiaAsync(" & uMAC.ToHexBytesString & ", " & sGuid & ", bForce=" & bForce & ", bMsg=" & bMsg & ") starting")

        Dim oGattSvc As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService =
                Await GetBTserviceFromMacAsync(uMAC, bForce, bMsg)
        If oGattSvc Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync, oGattSvc Is Nothing")
            Return Nothing
        End If

        Dim oTemp = Await oGattSvc.GetCharacteristicsForUuidAsync(New Guid(sGuid))
        If oTemp Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync, null from GetGattServicesForUuidAsync")
            oGattSvc.Dispose()
            Return Nothing
        End If

        If oTemp.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            If bMsg Then Await DialogBoxAsync("Cannot get GATT characteristic")
            DebugOut("GetBTcharacterForMijiaAsync, Cannot get GATT characteristic: status=" & oTemp.Status.ToString)
            oGattSvc.Dispose()
            Return Nothing
        End If

        If oTemp.Characteristics.Count < 1 Then
            DebugOut("GetBTcharacterForMijiaAsync, zero services from GetGattServicesForUuidAsync")
            oGattSvc.Dispose()
            Return Nothing
        End If

        DebugOut("GetBTcharacterForMijiaAsync, got Characteristic")
        Return oTemp.Characteristics.ElementAt(0)

    End Function
    Public Async Function GetBTcharacterForMijiaAsync(oTermo As JedenTermo, sGuid As String, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic)
        DebugOut("GetBTcharacterForMijiaAsync(" & oTermo.sName & ", " & sGuid & ", bForce=" & bForce & ", bMsg=" & bMsg & ")  starting")
        If oTermo Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync,  oTermo Is Nothing")
            Return Nothing
        End If

        Return Await GetBTcharacterForMijiaAsync(oTermo.uMAC, sGuid, bForce, bMsg)
    End Function

    Public Async Function MijiaGetRecordsDataAsync(oTermo As JedenTermo, bForce As Boolean, bMsg As Boolean) As Task(Of JedenRekord)

        DebugOut("MijiaGetRecordsDataAsync(" & oTermo.sName & ", bForce=" & bForce & ", bMsg=" & bMsg & ") start")

        Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
            Await GetBTcharacterForMijiaAsync(oTermo, "ebe0ccbb-7a0a-4b0c-8a1a-6ff2997da3a6", bForce, bMsg)

        If oChar Is Nothing Then
            DebugOut("MijiaGetRecordsDataAsync, oChar Is Nothing")
            Return Nothing
        End If

        Dim oValue = Await oChar.ReadValueAsync
        If oValue.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            DebugOut("MijiaGetRecordsDataAsync: Cannot get GATT value")
            If bMsg Then Await DialogBoxResAsync("msgCannotGetGATTvalue")
            oChar.Service.Dispose()
            Return Nothing
        End If

        If oValue.Value.Length < 14 Then
            DebugOut("MijiaGetRecordsDataAsync: No data returned from device")
            If bMsg Then Await DialogBoxResAsync("msgNoDataFromDevice")
            oChar.Service.Dispose()
            Return Nothing
        End If

        DebugOut("MijiaGetRecordsDataAsync: got data")

        Dim aArray As Byte() = oValue.Value.ToArray

        Dim oRecord As JedenRekord = MijiaDecodeRecord(aArray)

        DeltaRekord(oTermo, oRecord)

        oTermo.sTempRange = oRecord.sRangeT ' .dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
        oTermo.sHigroRange = oRecord.sRangeH ' .iMinH & " - " & oRecords.iMaxH & " %"

        Await SaveNewRekord(oTermo, oRecord)

        DebugOut("MijiaGetRecordsDataAsync: sTempRange=" & oTermo.sTempRange)
        DebugOut("MijiaGetRecordsDataAsync: sHigroRange=" & oTermo.sHigroRange)

        Return oRecord

    End Function

    Public Async Function GetSaveFile(oTermo As JedenTermo, bRekordy As Boolean) As Task(Of Windows.Storage.StorageFile)

        If oTermo Is Nothing Then Return Nothing

        Dim oFold As Windows.Storage.StorageFolder
        If oTermo.bYearlyLog Then
            oFold = Await GetLogFolderYearAsync(True)
        Else
            oFold = Await GetLogFolderMonthAsync(True)
        End If
        If oFold Is Nothing Then Return Nothing

        Dim sFileName As String = oTermo.sName ' oTermo.uMAC.ToHexBytesString.Substring(9, 8).Replace(":", "-")
        If bRekordy Then
            sFileName &= "-minmax"
        Else
            sFileName &= "-meas"
        End If

        sFileName &= ".txt"

        sFileName = sFileName.Replace(":", "-")  ' podmiana, gdy plik jest nienazwany (znaczy jest MAC address)
        Return Await oFold.CreateFileAsync(sFileName, Windows.Storage.CreationCollisionOption.OpenIfExists)
    End Function

    ''' <summary>
    ''' przy okazji do oTermo wpisuje Last, z danymi już PO delta; liczy AppTemp.
    ''' </summary>
    Public Async Function SaveNewPomiar(oTermo As JedenTermo, oPomiar As JedenPomiar) As Task
        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(oTermo, False)
        If oFile Is Nothing Then Return

        'Await DebugLogFileOutAsync("SaveNewPomiar, dane pomiar tostring()=" & oPomiar.ToString())
        'Await DebugLogFileOutAsync("oPomiar.dTimeStamp =" & oPomiar.dTimeStamp)
        'Await DebugLogFileOutAsync("oPomiar.dTemp =" & oPomiar.dTemp)
        'Await DebugLogFileOutAsync("oPomiar.iHigro =" & oPomiar.iHigro)
        'Await DebugLogFileOutAsync("oPomiar.iBattMV =" & oPomiar.iBattMV)

        'Await DebugLogFileOutAsync("SaveNewPomiar, dane oTermo tostring()=" & oTermo.ToString())
        'Await DebugLogFileOutAsync("oTermo.dDeltaTemp =" & oTermo.dDeltaTemp)
        'Await DebugLogFileOutAsync("oTermo.iDeltaHigro =" & oTermo.iDeltaHigro)
        'Await DebugLogFileOutAsync("oTermo.dLastTimeStamp =" & oTermo.dLastTimeStamp)
        'Await DebugLogFileOutAsync("oTermo.dLastTemp =" & oTermo.dLastTemp)
        'Await DebugLogFileOutAsync("oTermo.iLastHigro =" & oTermo.iLastHigro)


        ' przesuniecie o Delta
        oTermo.dLastTimeStamp = oPomiar.dTimeStamp
        oTermo.dLastTemp = oPomiar.dTemp + oTermo.dDeltaTemp
        oTermo.iLastHigro = oPomiar.iHigro + oTermo.iDeltaHigro
        oTermo.iLastBattMV = oPomiar.iBattMV
        oTermo.dLastAppTemp = TempHigro2AppTemp(oTermo.dLastTemp, oTermo.iLastHigro)

        Dim sLine As String = oTermo.uMAC.ToHexBytesString & vbTab & oTermo.dLastTimeStamp.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & oTermo.dLastTemp.ToString("#0.00") & vbTab & oTermo.iLastHigro & vbTab & oTermo.iLastBattMV
        Await oFile.AppendLineAsync(sLine)

    End Function

    Public Function TempHigro2AppTemp(dLastTemp As Double, iLastHigro As Integer) As Double
        ' http://www.bom.gov.au/info/thermal_stress/#apparent
        ' czyli Source: Norms of apparent temperature in Australia, Aust. Met. Mag., 1994, Vol 43, 1-16
        Dim dWP As Double ' water pressure, hPa
        Dim dWind As Double = 0 ' wind speed, na wysok 10 m, w m/s

        dWP = iLastHigro / 100 * 6.105 * Math.Exp((17.27 * dLastTemp) / (237.7 + dLastTemp))
        Return Math.Round(dLastTemp + 0.33 * dWP - 0.7 * dWind - 4, 2)
        ' uwaga: dla wersji z naslonecznieniem jest inaczej

        ' https://planetcalc.com/2089/ ma inaczej - to jest z tego z nasłonecznieniem
        ' dLastTemp + 0.348 * dWP - 0.7 * dWind - 4.25

    End Function

    Public Sub DeltaRekord(oTermo As JedenTermo, oRecords As JedenRekord)
        oRecords.dMinT += oTermo.dDeltaTemp
        oRecords.dMaxT += oTermo.dDeltaTemp
        oRecords.iMinH += oTermo.iDeltaHigro
        oRecords.iMaxH += oTermo.iDeltaHigro

        oRecords.sRangeT = oRecords.dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
        oRecords.sRangeH = oRecords.iMinH & " - " & oRecords.iMaxH & " %"
    End Sub

    ''' <summary>
    ''' zapisuje do pliku rekord 
    ''' </summary>
    Public Async Function SaveNewRekord(oTermo As JedenTermo, oRecords As JedenRekord) As Task
        If oTermo Is Nothing Then Return
        If oRecords Is Nothing Then Return
        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(oTermo, True)
        If oFile Is Nothing Then Return

        Await oFile.AppendLineAsync(GetRecordsLogLine(oRecords))

    End Function


#Region "odczytanie wartości"
    Private moMTChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing
    Private moMTCompletionSource As TaskCompletionSource(Of JedenPomiar) = Nothing
    Private moMTuMAC As Long = 0


    Public Async Function MijiaGetCurrentData(oTermo As JedenTermo, bForce As Boolean, bMsg As Boolean) As Task(Of JedenPomiar)
        DebugOut(1, "MijiaGetCurrentData(" & oTermo.sName)

        If moMTChar IsNot Nothing Then Return Nothing
        'If moMTCompletionSource IsNot Nothing Then Return Nothing

        moMTChar = Await GetBTcharacterForMijiaAsync(oTermo, "ebe0ccc1-7a0a-4b0c-8a1a-6ff2997da3a6", False, bMsg)   ' "Temperature and H"
        If moMTChar Is Nothing Then
            ' 2021.02.02 ponowna próba, tym razem bez użycia cache - ale tylko dla desktop, gdy można długo siedzieć w background
            If IsFamilyDesktop() Then
                moMTChar = Await GetBTcharacterForMijiaAsync(oTermo, "ebe0ccc1-7a0a-4b0c-8a1a-6ff2997da3a6", True, False)   ' "Temperature and H"
            End If
            If moMTChar Is Nothing Then

                ' jeśli ma nie pokazywać, to znaczy że jesteśmy w tle - toasty
                If Not bMsg Then
                    If oTermo.bToastInAccessible Then MakeToast("Sensor " & oTermo.sName & " " & GetSettingsString("msgCannotReach"))
                    oTermo.bToastInAccessible = False
                End If
                Return Nothing
            End If
        End If
        ' zaznacz że ostatnio był dostęp do sensora
        oTermo.bToastInAccessible = True

        moMTCompletionSource = New TaskCompletionSource(Of JedenPomiar)

        AddHandler moMTChar.ValueChanged, AddressOf Gatt_ValueChangedMT

        Dim oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

        ' 2021.02.02
        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)
            If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                If bMsg Then Await DialogBoxAsync("Cannot subscribe for notifications")
                moMTCompletionSource = Nothing
                Return Nothing
            End If
        End If

        moMTuMAC = oTermo.uMAC
        Return Await moMTCompletionSource.Task
    End Function

    Private Async Sub Gatt_ValueChangedMT(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)
        DebugOut(1, "Gatt_ValueChangedMT()")

        If moMTChar Is Nothing Then
            DebugOut("2, Gatt_ValueChangedMT: moMTChar is null, skipping handler")
            Return
        End If

        If moMTCompletionSource Is Nothing Then
            DebugOut("2, Gatt_ValueChangedMT: moMTCompletionSource is null, skipping handler")
            Return
        End If

        Dim aArray As Byte() = args.CharacteristicValue.ToArray
        Dim oNewPom As JedenPomiar = MijiaDecodePomiar(aArray)
        DebugOut("Gatt_ValueChangedMT - oNewPom created from aArray")
        oNewPom.uMAC = moMTuMAC

        Try
            RemoveHandler moMTChar.ValueChanged, AddressOf Gatt_ValueChangedMT
        Catch ex As Exception
            DebugOut(2, "Gatt_ValueChangedMT: cannot RemoveHandler")
            ' na wypadek podwójnego wywołania - nie da się dwa razy wyrejestrować :)
            ' a przy debug tu ustawionym, może się zdarzyć iż pojawi się ponowny Event
        End Try

        Dim oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
        DebugOut(3, "Gatt_ValueChangedMT, status From write.None: " & oResp.ToString)

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            Await Task.Delay(100)
            oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
            DebugOut(3, "Gatt_ValueChangedMT, status From write.None (second): " & oResp.ToString)

            If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                DebugOut("2, Gatt_ValueChangedMT: Cannot de-subscribe for notifications")
            End If
        End If

        moMTChar = Nothing
        moMTCompletionSource.TrySetResult(oNewPom)

    End Sub
#End Region

    Public Async Function ObsluzBackgroundAsync() As Task
        DebugOut("ObsluzBackground start")

        If (Await NetIsBTavailableAsync(False)) < 1 Then Return ' *TODO* włączenie sobie Bluetooth

        If App.gmTermo Is Nothing Then
            DebugOut("ObsluzBackground, ale App.gmTermo Is Nothing??")
            Return
        End If

        If Not GetSettingsBool("uiAlertsOnOff") Then Return

        ' wywoływanie co 15 minut, ale nie każdy timer robi odczyt danych
        Dim iCnt As Integer = GetSettingsBool("alertCounter", 0) - 1
        If iCnt > 0 Then
            SetSettingsBool("alertCounter", iCnt)
            'Await DebugLogFileOutAsync("ObsluzBackgroundAsync: Current tick: " & iCnt)
            Return
        End If

        ' na pewno mamy odczytac

        ' ustaw counter - po ilu TimerTick ma odczytać dane
        Dim sTmp As String = GetSettingsString("uiAlertsTimer", "30 min")
        Select Case sTmp.Substring(0, 1)
            Case 1
                SetSettingsBool("alertCounter", 1)
            Case 3
                SetSettingsBool("alertCounter", 2)
            Case 6
                SetSettingsBool("alertCounter", 4)
        End Select


        ' jesli nie mamy wczytanego, to wczytaj; ale jesli puste, to idź sobie
        If App.gmTermo.Count < 1 Then
            'Await DebugLogFileOutAsync("ObsluzBackgroundAsync: reading App.gmTermo.LoadAsync ")
            Await App.gmTermo.LoadAsync()
        End If
        If App.gmTermo.Count < 1 Then Return

        Dim iJakiesZmiany As Integer = 0
        For Each oItem As JedenTermo In App.gmTermo.GetList
            If oItem.bAlertInclude Then
                iJakiesZmiany += Await ObsluzTermoAtBackgroundAsync(oItem)
            End If
        Next

        If iJakiesZmiany > 0 Then
            ' zrob toasty
            DebugOut("ObsluzBackground saving data")
            'Await DebugLogFileOutAsync("ObsluzBackgroundAsync: saving data ")
            Await App.gmTermo.SaveAsync
        Else
            DebugOut("ObsluzBackground koniec - nie ma zmian, nie zapisuję")
        End If
    End Function

    Public Async Function ObsluzTermoAtBackgroundAsync(oItem As JedenTermo) As Task(Of Integer)
        DebugOut("------------")
        DebugOut("ObsluzTermoAtBackgroundAsync(" & oItem.sName)
        'Await DebugLogFileOutAsync("ObsluzTermoAtBackgroundAsync(" & oItem.sName)

        If oItem.bAlertIncludeTemp Or oItem.bAlertIncludeTApp Or oItem.bAlertIncludeHigro Then

            Dim oPomiar As JedenPomiar = Await MijiaGetCurrentData(oItem, False, False)

            ' zapewne włączyło, więc wyłączamy - nastepny raz znowu sobie otworzymy; bedzie niby dluzej, ale nie bedzie utrzymywane połączenie - oszczędzanie bateryjki w termometrze
            If oItem.oGATTsvc IsNot Nothing Then
                DebugOut("ObsluzTermoAtBackgroundAsync(" & oItem.sName & ") removing GATTsvc")
                'Await DebugLogFileOutAsync("ObsluzTermoAtBackgroundAsync(" & oItem.sName & ") removing GATTsvc")
                ' oItem.oGATTsvc.Device.Dispose() - to zakładam że będzie automatem w poniższym:
                oItem.oGATTsvc.Dispose()
                oItem.oGATTsvc = Nothing
            End If

            If oPomiar Is Nothing Then
                DebugOut("ObsluzTermoAtBackgroundAsync(" & oItem.sName & "), no data returned")
                'Await DebugLogFileOutAsync("ObsluzTermoAtBackgroundAsync(" & oItem.sName & "), no data returned")
                Return 0 ' bez zmian
            End If

            DebugOut("Termo " & oItem.sName & ", temp " & oPomiar.dTemp & ", higro " & oPomiar.iHigro & ", batt " & oPomiar.iBattMV)
            'Await DebugLogFileOutAsync("Termo " & oItem.sName & ", temp " & oPomiar.dTemp & ", higro " & oPomiar.iHigro & ", batt " & oPomiar.iBattMV)

            Await SaveNewPomiar(oItem, oPomiar)

            ZrobToasty(oItem)
        End If

        If oItem.bAlertIncludeRange Then
            ' odczyt Range
            Await MijiaGetRecordsDataAsync(oItem, False, False)
        End If

        Return 1    ' zaszła zmiana
    End Function
#Region "Toasty"

    Private Function ZrobToastString(dCurr As Double, iLastAlert As Integer,
                                     bAlL As Boolean, dAlL As Double,
                                     bWaL As Boolean, dWaL As Double,
                                     bWaH As Boolean, dWaH As Double,
                                     bAlH As Boolean, dAlH As Double
                                     ) As String


        'Await DebugOut("ZrobToastString(dCurr=" & dCurr & ", iLast=" & iLastAlert &
        '                           ", bAlL=" & bAlL & ",dAll" & dAlL & ",/ bwal" & bWaL & ", dWal=" & dWaL &
        '                           ",/ bwah=" & bWaH & ",dwah=" & dWaH & "./ balh=" & bAlH & ", dalh=" & dAlH)

        If bAlL Then
            DebugOut("ZrobToastString: bAlL")
            If dCurr <= dAlL Then
                If iLastAlert <> -2 Then Return "(<!!)"
                Return ""
            End If
        End If

        If bWaL Then
            DebugOut("ZrobToastString: bWaL")
            If dCurr <= dWaL Then
                If iLastAlert <> -1 Then Return "(<!)"
                Return ""
            End If
        End If

        If bAlH Then
            DebugOut("ZrobToastString: bWaH")
            If dCurr >= dAlH Then
                If iLastAlert <> 1 Then Return "(>!)"
                Return ""
            End If
        End If

        If bWaH Then
            DebugOut("ZrobToastString: bAlH")
            If dCurr >= dWaH Then
                If iLastAlert <> 2 Then Return "(>!!)"
                Return ""
            End If
        End If

        If iLastAlert <> 0 Then
            If dCurr > dWaL AndAlso dCurr < dWaH Then Return "(ok)"
        End If

        Return ""
    End Function

    Private Function ToasString2AlertLast(sTxt As String) As Integer
        If sTxt.Contains("ok") Then Return 0
        Dim iRet As Integer
        If sTxt.Contains("!!") Then
            iRet = 2
        Else
            iRet = 1
        End If

        If sTxt.Contains("<") Then iRet = -iRet

        Return iRet

    End Function
    Public Sub ZrobToasty(oItem As JedenTermo)
        DebugOut("ZrobToasty(" & oItem.sName)

        Dim sNewWarn As String = ""

        Dim sTmp As String

        If oItem.bAlertIncludeTemp Then
            DebugOut("ZrobToasty(" & oItem.sName & ") checking temp")
            'Await DebugLogFileOutAsync("ZrobToasty(" & oItem.sName & ") checking temp")
            sTmp = ZrobToastString(oItem.dLastTemp, oItem.iAlertLastT,
                        oItem.bAlertAlarmTLow, oItem.dAlertAlarmTLow,
                        oItem.bAlertWarnTLow, oItem.dAlertWarnTLow,
                        oItem.bAlertWarnTHigh, oItem.dAlertWarnTHigh,
                        oItem.bAlertAlarmTHigh, oItem.dAlertAlarmTHigh)
            If sTmp <> "" Then
                oItem.iAlertLastT = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & GetSettingsString("msgToastTemp", "Temp") & " " & oItem.dLastTemp.ToString("#0.00") & " °C " & sTmp & vbCrLf
                'Await DebugLogFileOutAsync("New toast string: '" & sNewWarn & "'")
            End If
        End If

        If oItem.bAlertIncludeTApp Then
            DebugOut("ZrobToasty(" & oItem.sName & ") checking AppTemp")
            'Await DebugLogFileOutAsync("ZrobToasty(" & oItem.sName & ") checking AppTemp")
            sTmp = ZrobToastString(oItem.dLastAppTemp, oItem.iAlertLastTA,
                        oItem.bAlertAlarmTALow, oItem.dAlertAlarmTALow,
                        oItem.bAlertWarnTALow, oItem.dAlertWarnTALow,
                        oItem.bAlertWarnTAHigh, oItem.dAlertWarnTAHigh,
                        oItem.bAlertAlarmTAHigh, oItem.dAlertAlarmTAHigh)
            If sTmp <> "" Then
                oItem.iAlertLastTA = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & GetSettingsString("msgToastAppTemp", "Temp odczuw") & " " & oItem.dLastAppTemp.ToString("#0.00") & " °C " & sTmp & vbCrLf
                'Await DebugLogFileOutAsync("New toast string: '" & sNewWarn & "'")
            End If
        End If

        If oItem.bAlertIncludeHigro Then
            DebugOut("ZrobToasty(" & oItem.sName & ") checking higro")
            'Await DebugLogFileOutAsync("ZrobToasty(" & oItem.sName & ") checking higro")
            sTmp = ZrobToastString(oItem.iLastHigro, oItem.iAlertLastH,
                        oItem.bAlertAlarmHLow, oItem.dAlertAlarmHLow,
                        oItem.bAlertWarnHLow, oItem.dAlertWarnHLow,
                        oItem.bAlertWarnHHigh, oItem.dAlertWarnHHigh,
                        oItem.bAlertAlarmHHigh, oItem.dAlertAlarmHHigh)
            If sTmp <> "" Then
                oItem.iAlertLastH = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & GetSettingsString("msgToastHigro", "Wilg") & " " & oItem.iLastHigro & " % " & sTmp & vbCrLf
                'Await DebugLogFileOutAsync("New toast string: '" & sNewWarn & "'")
            End If
        End If

        ' bateryjka
        ' normalnie: 2100 pokazuje symbol slabej bateryjki, 2090 sensor sie wyłącza
        DebugOut("ZrobToasty(" & oItem.sName & ") checking battery")
        ' Await DebugLogFileOutAsync("ZrobToasty(" & oItem.sName & ") checking battery")

        If oItem.iLastBattMV < 2300 Then
            'Await DebugLogFileOutAsync("current: " & oItem.iLastBattMV & " mV, previous: " & oItem.iPrevBattMV)
            If oItem.iLastBattMV + 50 < oItem.iPrevBattMV Then
                ' czyli pokazemy warningi 2250,2200,2150,2100
                sNewWarn = sNewWarn & GetSettingsString("msgToastBattery", "Battery low")
                ' Await DebugLogFileOutAsync("New toast string: '" & sNewWarn & "'")
                oItem.iPrevBattMV = oItem.iLastBattMV
            End If
            oItem.iPrevBattMV = oItem.iLastBattMV
        Else
            ' teraz batt jest OK, moze po wymianie bateryjki jest?
            If oItem.iPrevBattMV < 2300 Then oItem.iPrevBattMV = 3000
        End If


        ' sNewWarn = sNewWarn.Replace(vbCr, "").Replace(vbLf, "") ' bo Trim nie scina newline? 2021.09.26: nie tak, bo tak usuwa też ze środka
        sNewWarn = sNewWarn.TrimStart(vbCrLf, vbCr, vbLf) ' 2021.09.26 - tylko tak, bo tak usuwa tylko Start/End
        sNewWarn = sNewWarn.TrimEnd(vbCrLf, vbCr, vbLf)
        sNewWarn = sNewWarn.Trim
        'Await DebugLogFileOutAsync("New toast string: '" & sNewWarn & "'")
        If sNewWarn = "" Then Return

        MakeToast("Sensor " & oItem.sName, sNewWarn)

    End Sub

#End Region

End Module
