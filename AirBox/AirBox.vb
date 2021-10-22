Module pkarAirBox
    ' https://translate.google.com/translate?sl=auto&tl=en&u=https://www.zukeran.org/shin/d/2020/12/19/co2-sensor-2/

    Public Function AirBoxDecodePomiar(aArray As Byte()) As JedenPomiar
        DebugOut("AirBoxDecodePomiar")
        DebugBTprintArray(aArray, 4)

        If aArray.ElementAt(0) <> &HA Then Return Nothing

        Dim oNew As JedenPomiar = New JedenPomiar
        oNew.dTimeStamp = New DateTime(2000 + aArray.ElementAt(1), aArray.ElementAt(2), aArray.ElementAt(3),
                                       aArray.ElementAt(4), aArray.ElementAt(5), 0)

        oNew.dTemp = (aArray.ElementAt(6) * 256 + aArray.ElementAt(7)) / 10
        If oNew.dTemp > 100 Then oNew.dTemp /= 10    ' wedle strony mnoznik jest ×10, ale mi wychodzi ×100?

        ' aArray.ElementAt(8), aArray.ElementAt(9) - unknown
        oNew.dTVOC = (aArray.ElementAt(10) * 256 + aArray.ElementAt(11)) / 1000
        oNew.dHCHO = (aArray.ElementAt(12) * 256 + aArray.ElementAt(13)) / 1000
        ' aArray.ElementAt(14), aArray.ElementAt(15) - unknown = 0x100
        oNew.dCO2 = (aArray.ElementAt(16) * 256 + aArray.ElementAt(17))

        Return oNew
    End Function

    Public Async Function GetBTserviceFromMacAsync(uMac As ULong, sSvcUid As String, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService)
        DebugOut("GetBTserviceFromMacAsync(" & uMac.ToHexBytesString & ", bForce=" & bForce & ", bMsg=" & bMsg & ") starting")

        If uMac = 0 Then Return Nothing

        If App.gmTermo Is Nothing Then
            DebugOut("GetBTserviceFromMacAsync, and no App.gmTermo")
            Return Nothing
        End If

        Dim oItem As JedenMiernik = App.gmTermo.GetTermo(uMac)
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
                oTemp = Await oDev.GetGattServicesForUuidAsync(New Guid(sSvcUid),
                                Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached)
            Else
                oTemp = Await oDev.GetGattServicesForUuidAsync(New Guid(sSvcUid))
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
            Return Nothing
        End If

        If oTemp.Services.Count < 1 Then
            DebugOut("GetBTserviceFromMacAsync, zero services from GetGattServicesForUuidAsync")
            Return Nothing
        End If

        oItem.oGATTsvc = oTemp.Services.ElementAt(0)

        Return oItem.oGATTsvc

    End Function

    Public Async Function GetBTcharacterForFromMacAsync(uMAC As ULong, sSvcUid As String, sCharGuid As String, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic)

        DebugOut("GetBTcharacterForMijiaAsync(" & uMAC.ToHexBytesString & ", " & sCharGuid & ", bForce=" & bForce & ", bMsg=" & bMsg & ") starting")

        Dim oGattSvc As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService =
                Await GetBTserviceFromMacAsync(uMAC, sSvcUid, bForce, bMsg)
        If oGattSvc Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync, oGattSvc Is Nothing")
            Return Nothing
        End If

        Dim oTemp = Await oGattSvc.GetCharacteristicsForUuidAsync(New Guid(sCharGuid))
        If oTemp Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync, null from GetGattServicesForUuidAsync")
            Return Nothing
        End If

        If oTemp.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            If bMsg Then Await DialogBoxAsync("Cannot get GATT characteristic")
            DebugOut("GetBTcharacterForMijiaAsync, Cannot get GATT characteristic: status=" & oTemp.Status.ToString)
            Return Nothing
        End If

        If oTemp.Characteristics.Count < 1 Then
            DebugOut("GetBTcharacterForMijiaAsync, zero services from GetGattServicesForUuidAsync")
            Return Nothing
        End If

        DebugOut("GetBTcharacterForMijiaAsync, got Characteristic")
        Return oTemp.Characteristics.ElementAt(0)

    End Function

    Public Async Function AirBoxSendCommand(uMAC As ULong, aArr As Byte(), bMsg As Boolean) As Task(Of Boolean)
        If aArr Is Nothing Then Return False
        If aArr.Length < 1 Then Return False

        Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic
        oChar = Await GetBTcharacterForFromMacAsync(uMAC, "0000fff0-0000-1000-8000-00805f9b34fb", "0000fff1-0000-1000-8000-00805f9b34fb", False, bMsg)
        If oChar Is Nothing Then Return False

        Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter

        For iLoop = 0 To aArr.Length - 1
            oWriter.WriteByte(aArr(iLoop))
        Next

        Dim oResp As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus
        Try
            oResp = Await oChar.WriteValueAsync(oWriter.DetachBuffer, Windows.Devices.Bluetooth.GenericAttributeProfile.GattWriteOption.WriteWithoutResponse)
        Catch ex As Exception
            ' nie moze byc NULL!
            oResp = Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.AccessDenied
        End Try
        oWriter.Dispose()

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then Return False

        Return True
    End Function



    Public Async Function AirBoxCmdReset(uMAC As ULong, bMsg As Boolean) As Task(Of Boolean)
        ' ale tego nie powinienem na początek robić na pewno
        Dim aArr As Byte() = {&HEE}
        Return Await AirBoxSendCommand(uMAC, aArr, bMsg)
    End Function

    Public Async Function AirBoxCmdSetTime(uMAC As ULong, bMsg As Boolean) As Task(Of Boolean)
        Dim dDate As DateTime = DateTime.Now
        Dim aArr As Byte() = {&HAA,
            dDate.Year - 2000, dDate.Month, dDate.Day, dDate.Hour, dDate.Minute, dDate.Second}
        Return Await AirBoxSendCommand(uMAC, aArr, bMsg)
    End Function
    Public Async Function AirBoxCmdSetInterval(uMAC As ULong, bMsg As Boolean, iEveryMinutes As Byte) As Task(Of Boolean)
        If iEveryMinutes < 0 Then Return False

        ' ale tego nie powinienem na początek robić na pewno
        If iEveryMinutes = 0 Then
            Dim aArr As Byte() = {&HAE, 2}
            Return Await AirBoxSendCommand(uMAC, aArr, bMsg)
        Else
            Dim aArr As Byte() = {&HAE, 1, iEveryMinutes}
            Return Await AirBoxSendCommand(uMAC, aArr, bMsg)

        End If

    End Function

    Public Async Function AirBoxCmdGetData(uMAC As ULong, bMsg As Boolean) As Task(Of Boolean)
        Dim aArr As Byte() = {&HAB}
        Return Await AirBoxSendCommand(uMAC, aArr, bMsg)
    End Function

    Public Async Function AirBoxCmdRunCalibration(uMAC As ULong, bMsg As Boolean) As Task(Of Boolean)
        ' ale tego nie powinienem na początek robić na pewno
        Dim aArr As Byte() = {&HAD}
        Return Await AirBoxSendCommand(uMAC, aArr, bMsg)
    End Function

    Public Async Function AirBoxInit(uMAC As ULong) As Task(Of Boolean)
        If Not Await AirBoxCmdReset(uMAC, True) Then Return False
        If Not Await AirBoxCmdSetTime(uMAC, True) Then Return False
        If Not Await AirBoxCmdSetInterval(uMAC, 0, True) Then Return False

        Return True

    End Function

#Region "odczytanie wartości"

    Private moMTChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing
    Private moMTCompletionSource As TaskCompletionSource(Of JedenPomiar) = Nothing
    Private moMTuMAC As Long = 0

    ''' <summary>
    ''' odczytuje dane z urządzenia oSensor, i wstawia rezultat pomiaru do oSensor (uwzględniając delty)
    ''' </summary>
    Public Async Function AirBoxGetCurrentDataFull(oSensor As JedenMiernik, bMsg As Boolean) As Task(Of Boolean)

        If Not Await AirBoxInit(oSensor.uMAC) Then Return False

        Dim oPomiar As JedenPomiar = Await AirBoxGetCurrentDataBT(oSensor, False, bMsg)
        'Await AirBoxCmdSetInterval(oSensor.uMAC, False, 60) 'co 60 minut, ale i tak powinno się rozłączać - tylko jak??

        ' nastepny raz znowu sobie otworzymy; bedzie niby dluzej, ale nie bedzie utrzymywane połączenie
        If oSensor.oGATTsvc IsNot Nothing Then
            DebugOut("ObsluzTermoAtBackgroundAsync(" & oSensor.sName & ") removing GATTsvc")
            oSensor.oGATTsvc.Dispose()
            oSensor.oGATTsvc = Nothing
        End If


        If oPomiar Is Nothing Then Return False

        oSensor.dLastTimeStamp = oPomiar.dTimeStamp
        oSensor.dLastTemp = oPomiar.dTemp + oSensor.dDeltaTemp
        oSensor.dLastCO2 = oPomiar.dCO2 + oSensor.dDeltaCO2
        oSensor.dLastTVOC = oPomiar.dTVOC + oSensor.dDeltaTVOC
        oSensor.dLastHCHO = oPomiar.dHCHO + oSensor.dDeltaHCHO

        Await SaveNewPomiar(oSensor)

        oSensor.bCannotAccess = False

        Return True
    End Function

    Public Async Function AirBoxGetCurrentDataBT(oTermo As JedenMiernik, bForce As Boolean, bMsg As Boolean) As Task(Of JedenPomiar)
        DebugOut("MijiaGetCurrentData() - start")

        If moMTChar IsNot Nothing Then Return Nothing
        'If moMTCompletionSource IsNot Nothing Then Return Nothing

        moMTChar = Await GetBTcharacterForFromMacAsync(oTermo.uMAC, "0000fff0-0000-1000-8000-00805f9b34fb", "0000fff4-0000-1000-8000-00805f9b34fb", False, bMsg)
        If moMTChar Is Nothing Then
            ' jeśli ma nie pokazywać, to znaczy że jesteśmy w tle - toasty
            If Not bMsg Then
                If Not oTermo.bCannotAccess Then MakeToast("Sensor " & oTermo.sName & " " & GetSettingsString("msgCannotReach"))
                oTermo.bCannotAccess = False
            End If
            Return Nothing
        End If

        ' zaznacz że ostatnio był dostęp do sensora
        oTermo.bCannotAccess = False

        moMTCompletionSource = New TaskCompletionSource(Of JedenPomiar)

        AddHandler moMTChar.ValueChanged, AddressOf Gatt_ValueChangedAB

        Dim oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            If bMsg Then Await DialogBoxAsync("Cannot subscribe for notifications")
            moMTCompletionSource = Nothing
            Return Nothing
        End If

        moMTuMAC = oTermo.uMAC


        If Not Await AirBoxCmdGetData(oTermo.uMAC, False) Then
            DebugOut("Nieudane AirBoxCmdGetData, tymczasowy return (powinno być zwalnianie zasobów)")
            Return Nothing
        End If

        DebugOut("MijiaGetCurrentData() - returning Task, please wait for Gatt_ValueChangedAB")

        Return Await moMTCompletionSource.Task
    End Function

    Private Async Sub Gatt_ValueChangedAB(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)
        DebugOut("Gatt_ValueChangedAB() - start")

        Try
            RemoveHandler moMTChar.ValueChanged, AddressOf Gatt_ValueChangedAB
        Catch ex As Exception
            ' na wypadek podwójnego wywołania - nie da się dwa razy wyrejestrować :)
            ' a przy debug tu ustawionym, może się zdarzyć iż pojawi się ponowny Event
        End Try

        Dim oResp = Await moMTChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
        'Await DebugLogFileOutAsync("Gatt_ValueChangedAB, status From write.None: " & oResp.ToString, App.gbDebugFile)

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            'Await DebugLogFileOutAsync("Cannot de-subscribe for notifications", App.gbDebugFile)
        End If

        Dim aArray As Byte() = args.CharacteristicValue.ToArray
        Dim oNewPom As JedenPomiar = AirBoxDecodePomiar(aArray)
        'Await DebugLogFileOutAsync("Gatt_ValueChangedAB - oNewPom created from aArray", App.gbDebugFile)
        If oNewPom Is Nothing Then
            'Await DebugLogFileOutAsync("Gatt_ValueChangedAB - oNewPom is NULL!", App.gbDebugFile)
        Else
            oNewPom.uMAC = moMTuMAC
            'Await DebugLogFileOutAsync(oNewPom.ToString(), App.gbDebugFile)
        End If

        moMTChar = Nothing
        moMTCompletionSource.SetResult(oNewPom)

    End Sub

#End Region

    Public Async Function ObsluzBackgroundAsync() As Task
        Dim sTmp As String = "ObsluzBackground start @" & DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
        DebugOut(sTmp)

        If (Await NetIsBTavailableAsync(False)) < 1 Then Return ' *TODO* włączenie sobie Bluetooth

        If App.gmTermo Is Nothing Then
            DebugOut("ObsluzBackground, ale App.gmTermo Is Nothing??")
            Return
        End If

        If Not GetSettingsBool("uiAlertsOnOff") Then Return

        ' jesli nie mamy wczytanego, to wczytaj; ale jesli puste, to idź sobie
        If App.gmTermo.Count < 1 Then
            DebugOut("ObsluzBackgroundAsync: reading App.gmTermo.LoadAsync ")
            Await App.gmTermo.LoadAsync()
        End If
        If App.gmTermo.Count < 1 Then Return

        Dim iJakiesZmiany As Integer = 0
        For Each oItem As JedenMiernik In App.gmTermo.GetList
            'If oItem.bAlertInclude Then
            iJakiesZmiany += Await ObsluzTermoAtBackgroundAsync(oItem)
            'End If
        Next

        If iJakiesZmiany > 0 Then
            ' zrob toasty
            DebugOut("ObsluzBackground saving data")
            Await App.gmTermo.SaveAsync
        Else
            DebugOut("ObsluzBackground koniec - nie ma zmian, nie zapisuję")
        End If
    End Function

    Public Async Function ObsluzTermoAtBackgroundAsync(oItem As JedenMiernik) As Task(Of Integer)
        DebugOut("------------")
        DebugOut("ObsluzTermoAtBackgroundAsync(" & oItem.sName)

        Dim bOk As Boolean = Await AirBoxGetCurrentDataFull(oItem, False)

        If Not bOk Then
            DebugOut("ObsluzTermoAtBackgroundAsync(" & oItem.sName & "), no data returned")
            Return 0 ' bez zmian
        End If

        'Dim sTmp As String = "Sensor " & oItem.sName & vbTab & oItem.uMAC.ToHexBytesString & vbTab & oItem.dLastTimeStamp.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & oTermo.dLastTemp & vbTab & oTermo.dLastCO2 & vbTab & oTermo.dLastHCHO & vbTab & oTermo.dLastTVOC
        'Await DebugLogFileOutAsync(sTmp)

        'Await SaveNewPomiar(oItem)

        Await ZrobToasty(oItem)

        Return 1    ' zaszła zmiana
    End Function
    Private Function ZrobToastString(dCurr As Double, iLastAlert As Integer,
                                     dLevel1 As Double, dLevel2 As Double, dLevel3 As Double) As String


        If dCurr >= dLevel3 Then
            If iLastAlert <> 3 Then Return "(>!!!)"
            Return ""
        End If

        If dCurr >= dLevel2 Then
            If iLastAlert <> 2 Then Return "(>!!)"
            Return ""
        End If

        If dCurr >= dLevel1 Then
            If iLastAlert <> 1 Then Return "(>!)"
            Return ""
        End If

        If iLastAlert <> 0 Then
            If dCurr < dLevel1 Then Return "(ok)"
        End If

        Return ""
    End Function
    Private Function ToasString2AlertLast(sTxt As String) As Integer
        If sTxt.Contains("ok") Then Return 0
        Dim iRet As Integer
        If sTxt.Contains("!!!") Then
            iRet = 3
        ElseIf sTxt.Contains("!!") Then
            iRet = 2
        Else
            iRet = 1
        End If

        If sTxt.Contains("<") Then iRet = -iRet

        Return iRet

    End Function

    Public Async Function ZrobToasty(oItem As JedenMiernik) As Task
        DebugOut("ZrobToasty(" & oItem.sName)

        Dim sNewWarn As String = ""

        Dim sTmp As String

        ' temp: nie robimy Toast

        ' CO2: zakres pomiarowy 400-2000 ppm
        'https://www.kane.co.uk/knowledge-centre/what-are-safe-levels-of-co-And-co2-in-rooms
        '250-400ppm	Normal background concentration in outdoor ambient air
        '400-1,000ppm	Concentrations typical of occupied indoor spaces with good air exchange
        '1,000-2,000ppm	Complaints of drowsiness And poor air.
        '2,000-5,000 ppm	Headaches, sleepiness And stagnant, stale, stuffy air. Poor concentration, loss of attention, increased heart rate And slight nausea may also be present.
        '5,000	Workplace exposure limit (as 8-hour TWA) in most jurisdictions.
        '>40,000 ppm	Exposure may lead to serious oxygen deprivation resulting in permanent brain damage, coma, even death.
        sTmp = ZrobToastString(oItem.dLastCO2, oItem.iAlertLastCO2, 900, 1900, 2000)
            If sTmp <> "" Then
                oItem.iAlertLastCO2 = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & "CO₂ " & oItem.dLastTemp & " ppm " & sTmp & vbCrLf
            End If

        If oItem.dLastHCHO < 16 Then
            ' HCOH: zakres pomiarowy 0..1.999 mg/m³
            'https://inspectapedia.com/indoor_air_quality/Formaldehyde_Gas_Exposure_Limits.php
            '> .01 ppm  	mild irritation Or allergic sensitization in some people	[>0.0123 mg/M3]
            '> 0.5 ppm  	irritation to eyes & mucous membranes	[>0.615 mg/M3] (blona sluzowa)
            '> 1.0 ppm 	possible nasopharyngeal cancer	[>1.23 mg/M3]
            '3.0 ppm	respiratory impairment And damage	[ 3.684 mg/M3 ]
            sTmp = ZrobToastString(oItem.dLastHCHO, oItem.iAlertLastHCHO, 0.123, 0.615, 1.23)
            If sTmp <> "" Then
                oItem.iAlertLastHCHO = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & "HCHO " & oItem.dLastHCHO & " mg/m³ " & sTmp & vbCrLf
            End If
        End If

        If oItem.dLastTVOC < 16 Then
            ' TVOC: zakres pomiarowy 0..9.999 mg/m³
            'https://www.tecamgroup.com/acceptable-voc-levels/
            'TVOC Level mg/m3	   Level of Concern
            'Less than 0.3 mg/m3	   Low
            '0.3 to 0.5 mg/m3	   Acceptable
            '0.5 to 1 mg/m3	   Marginal
            '1 to 3 mg/m3	   High
            'https://www.epa.gov/indoor-air-quality-iaq/volatile-organic-compounds-impact-indoor-air-quality
            sTmp = ZrobToastString(oItem.dLastTVOC, oItem.iAlertLastTVOC, 0.3, 0.5, 1)
            If sTmp <> "" Then
                oItem.iAlertLastTVOC = ToasString2AlertLast(sTmp)
                sNewWarn = sNewWarn & "TVOC " & oItem.dLastTVOC & " mg/m³ " & sTmp & vbCrLf
            End If
        End If


        ' bo Trim nie scina newline?
        ' ale nie takie replace, bo takie scina też ze srodka!
        'sNewWarn = sNewWarn.Replace(vbCr, "").Replace(vbLf, "") ' 
        sNewWarn = sNewWarn.Trim(vbCr)
        sNewWarn = sNewWarn.Trim(vbLf)
        DebugOut("New toast string: '" & sNewWarn & "'")
        If sNewWarn = "" Then Return

        MakeToast("Sensor " & oItem.sName, sNewWarn)

    End Function
    Public Async Function GetSaveFile(oTermo As JedenMiernik) As Task(Of Windows.Storage.StorageFile)

        If oTermo Is Nothing Then Return Nothing

        Dim oFold As Windows.Storage.StorageFolder = Await GetLogFolderMonthAsync(True)
        If oFold Is Nothing Then Return Nothing

        Dim sFileName As String = oTermo.sName ' oTermo.uMAC.ToHexBytesString.Substring(9, 8).Replace(":", "-")
        sFileName &= ".txt"
        sFileName = sFileName.Replace(":", "-")  ' podmiana, gdy plik jest nienazwany (znaczy jest MAC address)
        Return Await oFold.CreateFileAsync(sFileName, Windows.Storage.CreationCollisionOption.OpenIfExists)
    End Function

    Public Async Function SaveNewPomiar(oTermo As JedenMiernik) As Task
        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(oTermo)
        If oFile Is Nothing Then Return

        Dim sLine As String = oTermo.uMAC.ToHexBytesString & vbTab & oTermo.dLastTimeStamp.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & oTermo.dLastTemp & vbTab & oTermo.dLastCO2 & vbTab

        ' 2021.02.17: "warming up", nie podajemy wartości
        If oTermo.dLastHCHO > 16 Then
            sLine &= "---"
        Else
            sLine &= oTermo.dLastHCHO
        End If
        sLine &= vbTab

        If oTermo.dLastTVOC > 16 Then
            sLine &= "---"
        Else
            sLine &= oTermo.dLastTVOC
        End If

        Await oFile.AppendLineAsync(sLine)

    End Function


#If False Then
    Public Function GetRecordsLogLine(oRec As JedenPomiar) As String
        'Return oRec.uMAC.ToHexBytesString & vbTab & oRec.lInd.ToString("00000") & vbTab &
        '    oRec.dDate.ToString("yyyy.MM.dd HH:mm") & vbTab &
        '    oRec.dMinT.ToString("#0.00") & vbTab & oRec.dMaxT.ToString("#0.00") & vbTab & oRec.iMinH & vbTab & oRec.iMaxH
        ''Return oRec.uMAC.ToHexBytesString & "|" & oRec.lInd.ToString("00000") &
        ''    oRec.dDate.ToString("yyyy.MM.dd HH:mm") & "|" &
        ''    oRec.dMinT.ToString("#0.00") & "|" & oRec.dMaxT.ToString("#0.00") & "|" & oRec.iMinH & "|" & oRec.iMaxH
        Return "nowa linia do logu"
    End Function





    Public Async Function GetBTcharacterForMijiaAsync(oTermo As JedenMiernik, sCharGuid As String, bForce As Boolean, bMsg As Boolean) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic)
        DebugOut("GetBTcharacterForMijiaAsync(" & oTermo.sName & ", " & sCharGuid & ", bForce=" & bForce & ", bMsg=" & bMsg & ")  starting")
        If oTermo Is Nothing Then
            DebugOut("GetBTcharacterForMijiaAsync,  oTermo Is Nothing")
            Return Nothing
        End If

        Return Await GetBTcharacterForMijiaAsync(oTermo.uMAC, sCharGuid, bForce, bMsg)
    End Function

    Public Async Function MijiaGetRecordsDataAsync(oTermo As JedenMiernik, bForce As Boolean, bMsg As Boolean) As Task(Of JedenRekord)

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
            Return Nothing
        End If

        If oValue.Value.Length < 14 Then
            DebugOut("MijiaGetRecordsDataAsync: No data returned from device")
            If bMsg Then Await DialogBoxResAsync("msgNoDataFromDevice")
            Return Nothing
        End If

        DebugOut("MijiaGetRecordsDataAsync: got data")

        Dim aArray As Byte() = oValue.Value.ToArray

        Dim oRecord As JedenPomiar = MijiaDecodeRecord(aArray)

        DeltaRekord(oTermo, oRecord)

        'oTermo.sTempRange = oRecord.sRangeT ' .dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
        'oTermo.sHigroRange = oRecord.sRangeH ' .iMinH & " - " & oRecords.iMaxH & " %"

        'Await SaveNewRekord(oTermo, oRecord)

        'DebugOut("MijiaGetRecordsDataAsync: sTempRange=" & oTermo.sTempRange)
        'DebugOut("MijiaGetRecordsDataAsync: sHigroRange=" & oTermo.sHigroRange)

        Return oRecord

    End Function

    Public Async Function GetSaveFile(oTermo As JedenMiernik, bRekordy As Boolean) As Task(Of Windows.Storage.StorageFile)

        If oTermo Is Nothing Then Return Nothing

        Dim oFold As Windows.Storage.StorageFolder = Await GetLogFolderMonthAsync(True)
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
    ''' przy okazji do oTermo wpisuje Last, z danymi już PO delta
    ''' </summary>
    Public Async Function SaveNewPomiar(oTermo As JedenMiernik, oPomiar As JedenPomiar) As Task
        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(oTermo, False)
        If oFile Is Nothing Then Return

        ' przesuniecie o Delta
        oTermo.dLastTimeStamp = oPomiar.dTimeStamp
        oTermo.dLastTemp = oPomiar.dTemp + oTermo.dDeltaTemp
        'oTermo.iLastHigro = oPomiar.iHigro + oTermo.iDeltaHigro
        'oTermo.iLastBattMV = oPomiar.iBattMV

        'Dim sLine As String = oTermo.uMAC.ToHexBytesString & vbTab & oTermo.dLastTimeStamp.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & oTermo.dLastTemp & vbTab & oTermo.iLastHigro & vbTab & oTermo.iLastBattMV
        'Await oFile.AppendLineAsync(sLine)

    End Function

    Public Sub DeltaRekord(oTermo As JedenMiernik, oRecords As JedenPomiar)
        'oRecords.dMinT += oTermo.dDeltaTemp
        'oRecords.dMaxT += oTermo.dDeltaTemp
        'oRecords.iMinH += oTermo.iDeltaHigro
        'oRecords.iMaxH += oTermo.iDeltaHigro

        'oRecords.sRangeT = oRecords.dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
        'oRecords.sRangeH = oRecords.iMinH & " - " & oRecords.iMaxH & " %"
    End Sub

    ''' <summary>
    ''' zapisuje do pliku rekord 
    ''' </summary>
    Public Async Function SaveNewRekord(oTermo As JedenMiernik, oRecords As JedenPomiar) As Task
        If oTermo Is Nothing Then Return
        If oRecords Is Nothing Then Return
        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(oTermo, True)
        If oFile Is Nothing Then Return

        Await oFile.AppendLineAsync(GetRecordsLogLine(oRecords))

    End Function

    Public Async Function ObsluzBackgroundAsync() As Task
        DebugOut("ObsluzBackground start")

        If App.gmTermo Is Nothing Then
            DebugOut("ObsluzBackground, ale App.gmTermo Is Nothing")
            Return
        End If

        For Each oItem As JedenMiernik In App.gmTermo.GetList
            Await ObsluzTermoAtBackgroundAsync(oItem)
        Next

        DebugOut("ObsluzBackground saving data")

        Await App.gmTermo.SaveAsync
    End Function

    Public Async Function ObsluzTermoAtBackgroundAsync(oItem As JedenMiernik) As Task
        DebugOut("ObsluzTermo(" & oItem.sName)
        Dim oRecords As JedenPomiar = Await MijiaGetRecordsDataAsync(oItem, False, False)
        If oRecords Is Nothing Then
            DebugOut("ObsluzTermo - oRecords Is null")
        Else
            DebugOut("ObsluzTermo - saving record")
            DeltaRekord(oItem, oRecords)
            Await SaveNewRekord(oItem, oRecords)
        End If
    End Function

    Private Function AddToastString(sNewWarn As String, sAllWarn As String, sMsg As String)
        If sAllWarn.Contains(sMsg) Then Return sNewWarn
        Return sNewWarn & sMsg & vbCrLf
    End Function

    Public Sub ZrobToasty(oItem As JedenMiernik)

        Dim sNewWarn As String = ""
        Dim sAllWarn As String = ""

        ' **TODO** bez powtarzania tej samej informacji?
        'If oItem.dMaxT < oItem.dLastTemp Then
        '    sAllWarn = sAllWarn & "Temp too high" & vbCrLf
        '    sNewWarn = AddToastString(sNewWarn, oItem.sAllWarn, GetLangString("msgToastTemp") & " " & GetLangString("msgToastTooHigh"))
        'End If
        'If oItem.dMinT > oItem.dLastTemp Then
        '    sAllWarn = sAllWarn & "Temp too low" & vbCrLf
        '    sNewWarn = AddToastString(sNewWarn, oItem.sAllWarn, GetLangString("msgToastTemp") & " " & GetLangString("msgToastTooLow"))
        'End If

        If sAllWarn = "" Then Return
        oItem.sAllWarn = sAllWarn
        MakeToast("Sensor " & oItem.sName, sNewWarn)

        ' oItem porównuje z min/max - ale one też muszą być zapisane w oItem!
    End Sub

#End If
End Module


