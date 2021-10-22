

Public NotInheritable Class TermoDetail
    Inherits Page

    'Private msMac As String
    ' Private muMac As ULong = 0
    'Private moService As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService = Nothing
    ' Private mbInBTComm As Boolean = False
    Private moTermo As JedenTermo = Nothing
    Private mbModified As Boolean = False

    Protected Overrides Sub onNavigatedTo(e As NavigationEventArgs)
        DebugOut(1, "TermoDetail:onNavigatedTo")
        Dim uMac = CType(e.Parameter, ULong)

        If App.gmTermo Is Nothing Then Return

        moTermo = App.gmTermo.GetTermo(uMac)

    End Sub

    Private Sub TermoRangeToDisplay(bAlways As Boolean)
        DebugOut(1, "TermoDetail:TermoRangeToDisplay(" & bAlways)

        If moTermo.dRangeData.AddYears(1) < DateTimeOffset.Now AndAlso Not bAlways Then Return

        uiRangeTemp.Text = moTermo.sTempRange ' oRecords.dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
        uiRangeHigro.Text = moTermo.sHigroRange ' oRecords.iMinH & " - " & oRecords.iMaxH & " %"
        uiDevRecords.Text = GetLangString("resHourlyRange") & " @ " & moTermo.dRangeData.ToString("G")
    End Sub
    Private Sub TermoHigroToDisplay(bAlways As Boolean)
        DebugOut(1, "TermoDetail:TermoHigroToDisplay(" & bAlways)

        If moTermo.dLastTimeStamp.AddYears(1) < DateTimeOffset.Now AndAlso Not bAlways Then Return

        If moTermo.dLastTemp < -90 Then Return 'empty data

        uiLastTemp.Text = moTermo.dLastTemp
        uiLastWhen.Text = moTermo.dLastTimeStamp.ToString("G")

        uiLastTemp.Text = moTermo.dLastTemp & " °C"
        uiLastHigro.Text = moTermo.iLastHigro & " %"

        uiLastBatt.Text = (moTermo.iLastBattMV / 1000).ToString("0.000") & " V"

    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        DebugOut(1, "TermoDetail:Page_Loaded")

        ProgRingInit(True, True)    ' progressbar przy czytaniu wszystkich parametrow sie przyda

        uiTitle.Text = ""

        If moTermo Is Nothing Then Return

        uiTitle.Text = moTermo.sName
        uiMac.Text = moTermo.uMAC.ToHexBytesString
        uiAdded.Text = moTermo.sTimeAdded

        TermoHigroToDisplay(False)

        uiDeltaTemp.Text = moTermo.dDeltaTemp
        If moTermo.dDeltaTemp = 0 Then uiDeltaTemp.Text = "0"   ' żeby jednak coś było, bo chyba zdarza się puste?

        uiDeltaHigro.Text = moTermo.iDeltaHigro
        If moTermo.iDeltaHigro = 0 Then uiDeltaHigro.Text = "0"

        TermoRangeToDisplay(False)

        If Not Await DialogBoxResYNAsync("msgReadAllFromDevice") Then Return
        DebugOut(2, "Reading all data from thermo")

        ProgRingShow(True, False, 0, 5)

        Await DisplayUnitsAsync()
        ProgRingInc()
        Await DisplayTimeAsync()
        ProgRingInc()
        Await DisplayHappyFace()
        ProgRingInc()
        Await DisplayRecordsAsync()
        ProgRingInc()
        Await DisplayTempHigroBatt2()
        ProgRingInc()
        'Await DisplayCommInterval()

        ProgRingShow(False)

    End Sub

    Private Sub uiDeltaT_Click(sender As Object, e As RoutedEventArgs)
        DebugOut(2, "uiDeltaT_Click")

        Dim iTemp As Integer
        If Not Integer.TryParse(uiDeltaHigro.Text, iTemp) Then
            DialogBoxRes("msgOnlyNumbersHere")
            Return
        End If

        If Math.Abs(iTemp) > 20 Then
            DialogBoxRes("resMsgDeltaTooBig")
            Return
        End If

        If moTermo Is Nothing Then Return

        moTermo.iDeltaHigro = iTemp
        mbModified = True

        DebugOut(2, "uiDeltaT_Click, new value=" & iTemp)

        ' Await App.gmTermo.SaveAsync()

    End Sub

    Private Sub uiDeltaH_Click(sender As Object, e As RoutedEventArgs)
        DebugOut(2, "uiDeltaH_Click")

        Dim dTemp As Double
        If Not Double.TryParse(uiDeltaTemp.Text, dTemp) Then
            DialogBoxRes("msgOnlyNumbersHere")
            Return
        End If

        If Math.Abs(dTemp) > 20 Then
            DialogBoxRes("resMsgDeltaTooBig")
            Return
        End If

        If moTermo Is Nothing Then Return

        moTermo.dDeltaTemp = dTemp
        mbModified = True
        ' Await App.gmTermo.SaveAsync()

        DebugOut(2, "uiDeltaH_Click, new value=" & dTemp)

    End Sub
#If False Then

#Region "BT helpers"

    Private Async Function InitaBTserviceAsync() As Task(Of Boolean)

        ProgRingShow(True)

        For Each oItem As JedenTermo In App.gmTermo.GetList
            If oItem.uMAC = muMac Then

                If oItem.oGATTsvc Is Nothing Then
                    oItem.oGATTsvc = Await GetBTserviceFromMacAsync(oItem.uMAC, True, True)
                End If

                If oItem.oGATTsvc Is Nothing Then
                    DebugOut("MijiaGetRecords and cannot get oGATTsvc")
                    Return Nothing
                End If

            End If
        Next

        ProgRingShow(False)

        'If moService Is Nothing Then Return False
        Return True

    End Function

    Private Async Function GetBTcharacteraAsync(sGuid As String) As Task(Of Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic)

        ProgRingShow(True)



        Try
            'If Not Await InitBTserviceAsync() Then Return Nothing

            ' Return Await GetBTcharacterForServiceAsync(moService, sGuid, True)
        Finally
            ProgRingShow(False)
        End Try

    End Function
#End Region
#End If

#Region "device display units"

    Private miDevUnits As Integer
    Private Async Function DisplayUnitsAsync() As Task
        DebugOut(2, "DisplayUnitsAsync")

        ProgRingShow(True)

        Try

            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
            Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccbe-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "Temperature Uint"
            If oChar Is Nothing Then Return

            Dim oValue = Await oChar.ReadValueAsync
            If oValue.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Cannot get GATT value")
                Return
            End If

            If oValue.Value.Length < 1 Then
                Await DialogBoxAsync("No data returned from device")
                Return
            End If

            miDevUnits = oValue.Value.ToArray.ElementAt(0)

            Select Case miDevUnits
                Case 0
                    uiUnits.Text = "Celsius"
                    uiUnitsButton.IsEnabled = True
                Case 1
                    uiUnits.Text = "Fahrenheit"
                    uiUnitsButton.IsEnabled = True
                Case Else
                    uiUnits.Text = "(unknown)"
            End Select
        Finally
            ProgRingShow(False)
        End Try

    End Function

    Private Async Sub uiUnitsButton_Click(sender As Object, e As RoutedEventArgs) Handles uiUnitsButton.Click
        DebugOut(2, "uiUnitsButton_Click")

        If Not Await DialogBoxResYNAsync("msgSwitchUnit") Then Return

        Dim iValue As Integer = 0
        If miDevUnits = 0 Then iValue = 1

        ProgRingShow(True)

        Try
            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
                Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccbe-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)
            If oChar Is Nothing Then Return

            If Not oChar.CharacteristicProperties.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Write) Then
                DebugOut("Nie ma prawa zapisu!!")
                Await DialogBoxAsync("No permission to write to device - protocol has changed?")
                Return
            End If

            Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter
            oWriter.WriteByte(iValue)

            Dim oRes As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus =
                Await oChar.WriteValueAsync(oWriter.DetachBuffer)
            oWriter.Dispose()   ' 2021.05.12

            If oRes <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Bad write to device")
                Return
            End If

            Await DisplayUnitsAsync()   ' odczytaj jak jest teraz

        Finally
            ProgRingShow(False)
        End Try

    End Sub

    Private Async Sub uiUnits_DoubleTapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DebugOut(2, "uiUnits_DoubleTapped")

        Await DisplayUnitsAsync()
    End Sub
#End Region

#Region "device time"

    Private Async Function DisplayTimeAsync() As Task
        DebugOut(2, "DisplayTimeAsync")

        ProgRingShow(True)

        Try

            ' If Not Await InitBTserviceAsync() Then Return

            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
            Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccb7-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "Time"
            If oChar Is Nothing Then Return

            Dim oValue = Await oChar.ReadValueAsync
            If oValue.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Cannot get GATT value")
                Return
            End If

            If oValue.Value.Length < 4 Then
                Await DialogBoxAsync("No data returned from device")
                Return
            End If

            Dim aArray As Byte() = oValue.Value.ToArray
            Dim lUnixTicks As ULong = aArray.ElementAt(3)
            lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(2)
            lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(1)
            lUnixTicks = 256 * lUnixTicks + aArray.ElementAt(0)

            Dim oDevDate As DateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(lUnixTicks)
            uiDevTime.Text = oDevDate.ToString("G")

            uiTimeButton.IsEnabled = True
        Finally
            ProgRingShow(False)
        End Try

    End Function

    Private Async Sub uiTimeButton_Click(sender As Object, e As RoutedEventArgs) Handles uiTimeButton.Click
        DebugOut(2, "uiTimeButton_Click")

        If Not Await DialogBoxResYNAsync("msgSetClock") Then Return

        Dim uUnixTicks As Long = DateTimeOffset.Now.ToUnixTimeSeconds
        Dim aBytes As Byte() = BitConverter.GetBytes(uUnixTicks)

        ProgRingShow(True)

        Try
            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
                Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccb7-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)
            If oChar Is Nothing Then Return

            If Not oChar.CharacteristicProperties.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Write) Then
                DebugOut("Nie ma prawa zapisu!!")
                Await DialogBoxAsync("No permission to write to device - protocol has changed?")
                Return
            End If

            Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter

            If BitConverter.IsLittleEndian Then
                oWriter.WriteByte(aBytes.ElementAt(0))
                oWriter.WriteByte(aBytes.ElementAt(1))
                oWriter.WriteByte(aBytes.ElementAt(2))
                oWriter.WriteByte(aBytes.ElementAt(3))
            Else
                oWriter.WriteByte(aBytes.ElementAt(7))
                oWriter.WriteByte(aBytes.ElementAt(6))
                oWriter.WriteByte(aBytes.ElementAt(5))
                oWriter.WriteByte(aBytes.ElementAt(4))
            End If


            Dim oRes As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus =
                Await oChar.WriteValueAsync(oWriter.DetachBuffer)
            oWriter.Dispose()

            If oRes <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Bad write to device")
                Return
            End If

            Await DisplayTimeAsync()   ' odczytaj jak jest teraz

        Finally
            ProgRingShow(False)
        End Try

    End Sub

    Private Async Sub uiDevTime_DoubleTapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DebugOut(2, "uiDevTime_DoubleTapped")

        Await DisplayTimeAsync()
    End Sub



#End Region

#Region "happy face"
    Private Async Function DisplayHappyFace() As Task
        DebugOut(2, "DisplayHappyFace")


        ProgRingShow(True)

        Try

            'If Not Await InitBTserviceAsync() Then Return

            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
            Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccd7-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "comfortable temp"
            If oChar Is Nothing Then Return

            Dim oValue = Await oChar.ReadValueAsync
            If oValue.Status <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Cannot get GATT value")
                Return
            End If

            If oValue.Value.Length < 6 Then
                Await DialogBoxAsync("No data returned from device")
                Return
            End If

            Dim aArray As Byte() = oValue.Value.ToArray

            uiHappyMinH.Text = aArray.ElementAt(5)
            uiHappyMaxH.Text = aArray.ElementAt(4)

            uiHappyMinT.Text = ((aArray.ElementAt(3) * 256 + aArray.ElementAt(2)) / 100).ToString("#0.0")
            uiHappyMaxT.Text = ((aArray.ElementAt(1) * 256 + aArray.ElementAt(0)) / 100).ToString("#0.0")

            uiHappyFaceButton.IsEnabled = True
        Finally
            ProgRingShow(False)
        End Try

    End Function
    Private Async Sub uiHappyFaceButton_Click(sender As Object, e As RoutedEventArgs) Handles uiHappyFaceButton.Click
        DebugOut(2, "uiHappyFaceButton_Click")

        Dim dTemp As Double
        If Not Double.TryParse(uiHappyMinT.Text, dTemp) Then
            DialogBox(GetLangString("msgOnlyNumberFor") & " Tmin")
            Return
        End If

        Dim iMinT As Integer = CInt(dTemp * 100)

        If Not Double.TryParse(uiHappyMaxT.Text, dTemp) Then
            DialogBox(GetLangString("msgOnlyNumberFor") & " Tmax")
            Return
        End If

        Dim iMaxT As Integer = CInt(dTemp * 100)

        If iMaxT <= iMinT Then
            DialogBoxRes("msgTmaxGreaterTmin")
            Return
        End If

        If iMaxT - iMinT < 200 Then
            DialogBoxRes("msgTmaxDeltaTmin")
            Return
        End If

        Dim iMinH As Integer
        If Not Integer.TryParse(uiHappyMinH.Text, iMinH) Then
            DialogBox(GetLangString("msgOnlyNumberFor") & " Hmin")
            Return
        End If

        If iMinH < 10 Then
            DialogBox("Hmin " & GetLangString("msgValueTooLow"))
            Return
        End If

        Dim iMaxH As Integer
        If Not Integer.TryParse(uiHappyMaxH.Text, iMaxH) Then
            DialogBox(GetLangString("msgOnlyNumberFor") & " Hmax")
            Return
        End If


        If iMaxH <= iMinH Then
            DialogBoxRes("msgHmaxGreaterHmin")
            Return
        End If

        If iMaxH - iMinH < 5 Then
            DialogBoxRes("msgHmaxDeltaHmin")
            Return
        End If

        If iMaxH > 90 Then
            DialogBox("Hmax " & GetLangString("msgValueTooLow"))
            Return
        End If



        ProgRingShow(True)

        ' zapisz na później - będzie potrzebne przy robieniu Toastów
        moTermo.dMaxT = iMaxT / 100
        moTermo.dMinT = iMinT / 100
        moTermo.iMaxH = iMaxH
        moTermo.iMinH = iMinH

        Try
            'If Not Await InitBTserviceAsync() Then Return

            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
            Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccd7-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "comfortable temp"
            If oChar Is Nothing Then Return

            If Not oChar.CharacteristicProperties.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Write) Then
                DebugOut("Nie ma prawa zapisu!!")
                Await DialogBoxAsync("No permission to write to device - protocol has changed?")
                Return
            End If

            Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter

            Dim aArrMax As Byte() = BitConverter.GetBytes(iMaxT)
            Dim aArrMin As Byte() = BitConverter.GetBytes(iMinT)

            If BitConverter.IsLittleEndian Then
                oWriter.WriteByte(aArrMax.ElementAt(0))
                oWriter.WriteByte(aArrMax.ElementAt(1))
                oWriter.WriteByte(aArrMin.ElementAt(0))
                oWriter.WriteByte(aArrMin.ElementAt(1))
            Else
                oWriter.WriteByte(aArrMax.ElementAt(3))
                oWriter.WriteByte(aArrMax.ElementAt(2))
                oWriter.WriteByte(aArrMin.ElementAt(3))
                oWriter.WriteByte(aArrMin.ElementAt(2))
            End If

            oWriter.WriteByte(iMaxH)
            oWriter.WriteByte(iMinH)

            Dim oRes As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus =
                Await oChar.WriteValueAsync(oWriter.DetachBuffer)
            oWriter.Dispose()

            If oRes <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Bad write to device")
                Return
            End If

            Await DisplayHappyFace()
        Finally
            ProgRingShow(False)
        End Try

    End Sub



    Private Async Sub uiDevHappyFace_DoubleTapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DebugOut(2, "uiDevHappyFace_DoubleTapped")

        Await DisplayHappyFace()
    End Sub
#End Region

#Region "last hour records"

    Private Async Function DisplayRecordsAsync() As Task
        DebugOut(2, "DisplayRecordsAsync")

        ProgRingShow(True)

        Try

            Dim oRecords As JedenRekord = Await MijiaGetRecordsDataAsync(moTermo, False, True)
            If oRecords IsNot Nothing Then
                ' jeśli nie ma błędu, to w oItem jest zmienione wszystko
                uiRangeTemp.Text = moTermo.sTempRange ' oRecords.dMinT.ToString("#0.00") & " - " & oRecords.dMaxT.ToString("#0.00") & " °C"
                uiRangeHigro.Text = moTermo.sHigroRange ' oRecords.iMinH & " - " & oRecords.iMaxH & " %"
                uiDevRecords.Text = GetLangString("resHourlyRange") & " @ " & moTermo.dRangeData.ToString("yyyy.MM.dd HH:mm")
                ' Await SaveNewRekord(moTermo, oRecords) - to juz jest w MijiaGetRecordsDataAsync
                mbModified = True
            End If

            TermoRangeToDisplay(True)
        Finally
            ProgRingShow(False)
        End Try

    End Function

    Private Async Sub uiDevRecords_DoubleTapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DebugOut(2, "uiDevRecords_DoubleTapped")

        Await DisplayRecordsAsync()
    End Sub



#End Region

#Region "temp / higro - starsza wersja (działa)"
#If False Then


    Private moChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing

    Public Async Function DisplayTempHigroBatt2() As Task

        If moChar IsNot Nothing Then Return

        ProgRingShow(True)

        Try

            'If Not Await InitBTserviceAsync() Then Return

            moChar = Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccc1-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "Temperature and H"
            If moChar Is Nothing Then Return

            AddHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged

            Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

            If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Cannot subscribe for notifications")
                Return
            End If

        Finally
            ProgRingShow(False)
        End Try

    End Function

    Private moNewPom As JedenPomiar = New JedenPomiar


    Private Sub Gatt_ValueChanged(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)

        Dim aArray As Byte() = args.CharacteristicValue.ToArray
        moNewPom = MijiaDecodePomiar(aArray)
        moNewPom.uMAC = moTermo.uMAC
        'moNewPom.dTimeStamp = DateTime.Now
        'moNewPom.iHigro = aArray.ElementAt(2)
        'moNewPom.dTemp = (aArray.ElementAt(1) * 256 + aArray.ElementAt(0)) / 100
        'moNewPom.iBattMV = aArray.ElementAt(4) * 256 + aArray.ElementAt(3)

        uiLastTemp.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchHaveData)
    End Sub

    Private Async Sub fromDispatchHaveData()
        DebugOut("fromDispatchHaveData() start")

        TermoHigroToDisplay(True)
        ''uiLastWhen.Text = moNewPom.dTimeStamp.ToString("G")
        'uiLastTemp.Text = moNewPom.dTemp.ToString("#0.00") & " °C"
        'uiLastHigro.Text = moNewPom.iHigro & " %"
        'uiLastBatt.Text = (moNewPom.iBattMV / 1000).ToString("0.000") & " V"

        DebugOut("fromDispatchHaveData removing notifications")
        ' wyłączenie notification
        RemoveHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged
        Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
        DebugOut("fromDispatchHaveData, status From write.None: " & oResp.ToString)

        Await SaveNewPomiar(moTermo, moNewPom)
        mbModified = True

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            DialogBox("Cannot de-subscribe for notifications")
        End If

        moChar = Nothing

    End Sub

    Private Sub uiTryGetTemp_Tapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DisplayTempHigroBatt2()
    End Sub
#End If
#End Region

#Region "temp / higro - nowsza wersja (test)"


    Public Async Function DisplayTempHigroBatt2() As Task
        DebugOut(2, "DisplayTempHigroBatt2")

        ProgRingShow(True)
        Dim oPomiar As JedenPomiar = Await MijiaGetCurrentData(moTermo, False, True)
        If oPomiar Is Nothing Then
            ' nie udało się
        Else
            DebugOut("zapis oraz display danych")
            Await SaveNewPomiar(moTermo, oPomiar)   ' zapis pomiaru, aktualizacja danych moTermo
            TermoHigroToDisplay(True)
        End If
        ProgRingShow(False)

        mbModified = True

    End Function

    Private Async Sub uiTryGetTemp_Tapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        DebugOut(2, "uiTryGetTemp_Tapped")

        Await DisplayTempHigroBatt2()
    End Sub
#End Region

    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        DebugOut(2, "Page_Unloaded")

        If mbModified Then Await App.gmTermo.SaveAsync ' jakby jakies zmiany były dokonane, to zapisz - raz, hurtem

        Try
            moTermo.oGATTsvc.Dispose()
            moTermo.oGATTsvc = Nothing
        Catch
        End Try
    End Sub


    ' to jest tylko zapis, nie ma odczytywania wartości
    '            <TextBlock Text = "Comm interval (ms)" Grid.Row="15" Grid.Column="0" />
    '            <TextBlock x :  Name = "uiCommInt" Grid.Row="15" Grid.Column="1" />
    '            <Button Content = "Change" Grid.Row="15" Grid.Column="2" IsEnabled="false" />


End Class
