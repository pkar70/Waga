' KOPIA z MijaThermo jako start do AirBoxa

' 2021.05.10: MainPage, BT_received, już nie tworzy oDev (i tak był nieużywany)

' 2021.04.14: MainPage: pole z datą ostatniego pomiaru
' 2021.04.14: Explain: ":" pomiędzy tytułem pomiaru a wartością
' 2021.04.14: AirBox.vb:AirBoxSendCommand: dodaję oWriter.Dispose
' 2021.02.17: "warming up", nie podajemy wartości: do logu, Explain, i konwerter na ekran
' STORE 26 I 2021

Public NotInheritable Class MainPage
    Inherits Page
    Private Async Sub uiRescan_Click(sender As Object, e As RoutedEventArgs)

        If uiGoPair.IsChecked Then
            ' mamy do czynienia z prośbą o Rescan sieci
            If Not Await DialogBoxResYNAsync("msgScanBTnet") Then Return
            StartScan()
        Else
            ' ewentualnie dac tu odpytanie termometrów o aktualną temperaturę

            'For Each oItem As JedenTermo In App.gmTermo.GetList
            '    If Not oItem.bEnabled Then Continue For
            'Next

            ShowLista(App.gbShowAdditCols)

        End If

    End Sub

    Private Async Sub uiGoPair_Click(sender As Object, e As RoutedEventArgs)
        'Dim oAppBarToggleButton As AppBarToggleButton = TryCast(sender, AppBarToggleButton)
        'If oAppBarToggleButton Is Nothing Then Return

        Dim bShowAll As Boolean = uiGoPair.IsChecked

        If bShowAll Then
            uiRescan.Visibility = Visibility.Visible
        Else
            uiRescan.Visibility = Visibility.Collapsed
        End If

        ShowLista(bShowAll)

        If Not bShowAll Then Return

        If Not Await DialogBoxResYNAsync("msgScanBTnet") Then Return

        StartScan()

    End Sub
    Private Sub uiGoInfo_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(HelpPage))
    End Sub
    Private Async Sub uiRunExplorer_Click(sender As Object, e As RoutedEventArgs)
        Dim oFold As Windows.Storage.StorageFolder = Await GetLogFolderMonthAsync(True)
        If oFold Is Nothing Then Return
        oFold.OpenExplorer()
    End Sub

    Private Sub ShowLista(bAll As Boolean)
        App.gbShowAdditCols = bAll
        uiItems.ItemsSource = Nothing

        ' przez przekopiowanie omijam problem z przypisaniem listy do innego wątku
        If bAll Then
            uiItems.ItemsSource = From c In App.gmTermo.GetList Order By c.sName
        Else
            uiItems.ItemsSource = From c In App.gmTermo.GetList Where c.bEnabled = True Order By c.sName
        End If

    End Sub

    Private Sub ReadResStrings()
        SetSettingsString("msgCannotReach", GetLangString("msgCannotReach", "zniknięty"))
    End Sub
    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)

        ProgRingInit(True, True) ' ewnetualnie progbar też true, jeśli zrobimy na Timerze co sekunde tick do 30, i za każdym razem zwiększanie value,
        ProgRingShow(True)

        GetAppVers(Nothing)    ' dodaj TextBlock z wersją
        If Not IsFamilyMobile() Then uiRunExplorer.Visibility = Visibility.Visible

        GetSettingsBool(uiOnOff, "uiAlertsOnOff")
        RegisterTrigg(GetSettingsBool("uiAlertsOnOff"))

        ReadResStrings()
        Await App.gmTermo.LoadAsync()

        ' kontrola Bluetooth, ale jako warningi raczej (na tym etapie przynajmniej)
        NetIsBTavailableAsync(True, False, GetLangString("msgWarnNoBT")) ' tylko pokaż komunikat w razie gdyby nie było BT

        ProgRingShow(False)

        ShowLista(False)

    End Sub
    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        Await StopScan()
        ProgRingShow(False, True)
    End Sub

    Private Sub RegisterTrigg(bOn As Boolean)
        If bOn Then
            If Not IsTriggersRegistered("AirBoxSensor") Then
                RegisterTimerTrigger("AirBoxSensor_Timer", 60)
            End If
        Else
            UnregisterTriggers("AirBoxSensor")
        End If
    End Sub

    Private Sub uiGoDetails_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenMiernik = TryCast(oMFI.DataContext, JedenMiernik)
        If oTermo Is Nothing Then Return

        Me.Frame.Navigate(GetType(MiernikDetails), oTermo.uMAC)
    End Sub

    Private Sub uiGoHistory_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenMiernik = TryCast(oMFI.DataContext, JedenMiernik)
        If oTermo Is Nothing Then Return

        'Me.Frame.Navigate(GetType(DownloadHistory), oTermo.uMAC)
    End Sub
    Private Sub uiMakeTile_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenMiernik = TryCast(oMFI.DataContext, JedenMiernik)
        If oTermo Is Nothing Then Return

        ' *TODO* 
        ' nazwa małymi centrowana, pod nim temperatura, ewentualnie poniżej małymi wilgotność
    End Sub

    Private Async Sub uiRename_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenMiernik = TryCast(oMFI.DataContext, JedenMiernik)
        If oTermo Is Nothing Then Return

        Dim sNewName As String = Await DialogBoxInputResAsync("msgEnterNewName", oTermo.sName, "msgRename")
        If sNewName = "" Then Return

        If sNewName.Contains(":") OrElse sNewName.Contains("\") OrElse sNewName.Contains("/") Then
            sNewName = Await DialogBoxInputResAsync("msgEnterNewName1", oTermo.sName, "msgRename")
            If sNewName = "" Then Return
            If sNewName.Contains(":") OrElse sNewName.Contains("\") OrElse sNewName.Contains("/") Then Return
        End If

        oTermo.sName = sNewName

        ShowLista(False)

        App.gmTermo.SaveAsync() ' nie czekam, ale raczej nic się nie zdarzy w trakcie

    End Sub


    Private Sub uiOnOff_Click(sender As Object, e As RoutedEventArgs)
        RegisterTrigg(uiOnOff.IsChecked)
        SetSettingsBool(uiOnOff, "uiAlertsOnOff")
    End Sub

#Region "skanowanie sieci"
    Public moBLEWatcher As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher = Nothing
    Private moTimer As DispatcherTimer
    Private miTimerCnt As Integer = 30
    Private msAllDevNames As String = ""

    Private Sub TimerTick(sender As Object, e As Object)
        ProgRingInc()
        miTimerCnt -= 1
        If miTimerCnt > 0 Then Return

        StopScan()
        ShowLista(True)
    End Sub


    Private Async Sub StartScan()
        DebugOut("StartScan()")
        If Await NetIsBTavailableAsync(False) < 1 Then Return

        moTimer = New DispatcherTimer()
        AddHandler moTimer.Tick, AddressOf TimerTick
        moTimer.Interval = New TimeSpan(0, 0, 1)
        miTimerCnt = 10 ' sekund na szukanie, ale z progress bar
        moTimer.Start()

        ProgRingShow(True, False, 0, miTimerCnt)

        ' App.moDevicesy = New Collection(Of JedenDevice) - nieprawda! korzystamy z dotychczasowych danych!
        uiGoPair.IsEnabled = False
        ScanSinozeby()
    End Sub

    Private Async Function StopScan() As Task
        DebugOut("StopScan()")

        If moTimer IsNot Nothing Then moTimer.Stop()

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then
            DebugOut("StopScan - stopping moBLEWatcher")
            moBLEWatcher.Stop()
        End If

        If uiGoPair IsNot Nothing Then uiGoPair.IsEnabled = True
        Await App.gmTermo.SaveAsync()
        ' podczas OnUnload - już nie będzie czego wyłączać; choć powinno zadziałać zabezpieczenie w ProgRing
        If uiGoPair IsNot Nothing Then ProgRingShow(False)
    End Function

    Private Sub ScanSinozeby()
        DebugOut("ScanSinozeby() starting")
        ' https://stackoverflow.com/questions/40950482/search-for-devices-in-range-of-bluetooth-uwp
        'przekopiowane z RGBLed
        moBLEWatcher = New Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher
        moBLEWatcher.ScanningMode = 1   ' 1: żąda wysłania adv (czyli tryb aktywny)
        AddHandler moBLEWatcher.Received, AddressOf BTwatch_Received
        moBLEWatcher.Start()
    End Sub

    ' wersje debug, na początek - przy dodawaniu nowych rzeczy
#If False Then
    ''' <summary>
    ''' Wersja BTwatch_Received , używana jako pierwsza przy tworzeniu nowych app - do znalezienia adresu urządzenia
    ''' UWAGA: Manifest:Capabilities:Bluetooth musi być włączony!
    ''' </summary>
    Private Sub BTwatch_Received_Krok0(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                   args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        Dim sMAC As String = args.BluetoothAddress.ToHexBytesString
        Dim sTxt As String = "New BT device, Mac=" & sMAC & ", locName=" & args.Advertisement.LocalName
        ' DebugOut(sTxt)

        ' jeśli to coś co już jest obsługiwane, to nie ma sensu się nim zajmować

        If args.Advertisement.LocalName = "LYWSD03MMC" Then
            DebugOut("nazwa termometrowa")
            Return
        End If

        If sMAC.StartsWith("A4:C1:38") Then
            DebugOut("MAC termometrowy")
            Return
        End If

        If args.Advertisement.LocalName.StartsWith("LEDBLE") Then
            DebugOut("nazwa żarówkowa")
            Return
        End If

        If sMAC = "F8:1D:78:60:36:C2" Then
            DebugOut("MAC żarówkowy")
            Return
        End If


        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection
        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToString & "|"
        If msAllDevNames.Contains(sNewAddr) Then
            ' DebugOut(sTxt)  
            'DebugOut("ale juz taki adres mam")
            Return
        End If

        DebugOut("Nowy device: " & sTxt)
        msAllDevNames = msAllDevNames & sNewAddr

    End Sub


    ''' <summary>
    ''' Wersja BTwatch_Received , używana jako druga przy tworzeniu nowych app - gdy już znany jest adres
    ''' UWAGA: Manifest:Capabilities:Bluetooth musi być włączony!
    ''' </summary>
    Private Async Sub BTwatch_Received_Krok1(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                   args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        Dim sMAC As String = args.BluetoothAddress.ToHexBytesString
        Dim sTxt As String = "New BT device, Mac=" & sMAC & ", locName=" & args.Advertisement.LocalName
        ' DebugOut(sTxt)

        ' sprawdzenie czy to adres znaleziony w Krok0
        If Not args.Advertisement.LocalName.StartsWith("6003#") Then Return
        ' New BT device, Mac=60:03:03:93:C6:5D, locName=6003#060030393C65D

        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection
        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToString & "|"
        If msAllDevNames.Contains(sNewAddr) Then Return

        DebugOut("Nowy device: " & sTxt)
        msAllDevNames = msAllDevNames & sNewAddr

        DebugBTadvertisement(args.Advertisement)

        Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress)

        If oDev Is Nothing Then
            DebugOut("oDev null, cannot continue")
            Return
        End If

        Await DebugBTdeviceAsync(oDev)

    End Sub
#End If


    Private Async Sub BTwatch_Received(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                   args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        Dim sTxt As String = "New BT device, Mac=" & args.BluetoothAddress.ToHexBytesString & ", locName=" & args.Advertisement.LocalName

        ' sprawdzenie czy to pasuje do naszego urządzenia
        If Not args.Advertisement.LocalName.StartsWith("6003#") Then
            Return
        End If
        ' New BT device, Mac=60:03:03:93:C6:5D, locName=6003#060030393C65D

        DebugOut(sTxt)  ' dopiero tu, zeby ignorowal te inne cosiki BT co sie pojawiają (22:7D:4F:50:B2:E7, C4:98:5C:D4:2E:A7, 32:75:ED:BF:BC:A7)

        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection
        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToString & "|"
        If msAllDevNames.Contains(sNewAddr) Then
            DebugOut("ale juz taki adres mam")
            Return
        End If
        msAllDevNames = msAllDevNames & sNewAddr

        DebugOut("jeszcze go nie znam")

        'Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        'oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress)

        'If oDev Is Nothing Then
        '    DebugOut("oDev null, cannot continue")
        '    Return
        'End If

        Dim oNew As JedenMiernik = New JedenMiernik
        oNew.uMAC = args.BluetoothAddress
        oNew.sName = args.Advertisement.LocalName
        If SprobujDodac(oNew) Then
            Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
        End If

    End Sub
    Private Function SprobujDodac(oNew As JedenMiernik) As Boolean
        DebugOut("adding: " & oNew.uMAC.ToHexBytesString)
        Try
            App.gmTermo.Add(oNew)
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Shared bInside As Boolean = False

    Public Async Sub fromDispatchShowItems()
        If bInside Then
            Debug.WriteLine("czekam")
            For i As Integer = 1 To 10
                Await Task.Delay(10)
                If Not bInside Then Exit For
            Next
            bInside = True
        End If

        DebugOut("nowa lista, count=" & App.gmTermo.Count)
        'uiItems.ItemsSource = From c In App.gmTermo.GetList
        ShowLista(True)

        bInside = False
    End Sub

#End Region


    Private Async Sub uiTryRead_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oSensor As JedenMiernik = TryCast(oMFI.DataContext, JedenMiernik)
        If oSensor Is Nothing Then Return

        ' init jest w AirBoxGetCurrentDataFull
        'If Not Await AirBoxInit(oSensor.uMAC) Then
        '    Return
        'End If

        'Await TryReadData(oSensor.uMAC, True)

        ProgRingShow(True)

        If Await AirBoxGetCurrentDataFull(oSensor, True) Then ShowLista(False)

        ProgRingShow(False)

    End Sub

#Region "wczytanie danych"

    'Private moChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing
    'Private moPomiar As JedenPomiar = Nothing
    'Private muMAC As ULong = 0

    'Public Async Function TryReadData(uMAC As ULong, bMsg As Boolean) As Task(Of Boolean)

    '    If moChar IsNot Nothing Then Return False

    '    ProgRingShow(True)
    '    moPomiar = Nothing

    '    moChar = Await GetBTcharacterForFromMacAsync(uMAC, "0000fff0-0000-1000-8000-00805f9b34fb", "0000fff4-0000-1000-8000-00805f9b34fb", False, bMsg)
    '    If moChar Is Nothing Then
    '        ProgRingShow(False)
    '        Return False
    '    End If

    '    muMAC = uMAC
    '    AddHandler moChar.ValueChanged, AddressOf Gatt_ValueChangedAir

    '    Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
    '                                Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

    '    If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
    '        ProgRingShow(False)
    '        If bMsg Then Await DialogBoxAsync("Cannot subscribe for notifications")
    '        Return False
    '    End If

    '    DebugOut("Subscribed for notification")

    '    If Not Await AirBoxCmdGetData(uMAC, bMsg) Then
    '        fromDispatchHaveData()  ' w nim jest wyłączenie progringa
    '        Return False
    '    End If

    '    Return True
    'End Function

    'Private Sub Gatt_ValueChangedAir(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)

    '    DebugOut("Gatt_ValueChanged")
    '    Dim aArray As Byte() = args.CharacteristicValue.ToArray
    '    ' 0xA    0x15 0x1 0x15  0x12 0x3B 0x0   0xEB 0x8    0x0 0x0     0xB8 0x0 0x1C 0x1 0x0
    '    ' magic, 2021.1.21      18:59:0         22.83°C     unknown     184
    '    ' 2000, dDate.Month, dDate.Day, dDate.Hour, dDate.Minute, dDate.Second}
    '    'DebugBTprintArray(aArray, 4)
    '    moPomiar = AirBoxDecodePomiar(aArray)
    '    moPomiar.uMAC = muMAC ' sender.Service.Device.BluetoothAddress - ale to jest "obsolete"

    '    ' teraz głównie: wyłącz subskrypcję
    '    uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchHaveData)
    'End Sub

    'Private Sub Pomiar2Miernik()

    '    Global : moPomiar
    '    For Each oMiernik As JedenMiernik In App.gmTermo.GetList
    '        If oMiernik.uMAC = moPomiar.uMAC Then
    '            oMiernik.dLastTimeStamp = moPomiar.dTimeStamp
    '            oMiernik.dLastTemp = moPomiar.dTemp + oMiernik.dDeltaTemp
    '            oMiernik.dLastCO2 = moPomiar.dCO2 + oMiernik.dDeltaCO2
    '            oMiernik.dLastTVOC = moPomiar.dTVOC + oMiernik.dDeltaTVOC
    '            oMiernik.dLastHCHO = moPomiar.dHCHO + oMiernik.dDeltaHCHO
    '            Return
    '        End If
    '    Next
    'End Sub

    'Private Async Sub fromDispatchHaveData()
    '    DebugOut("fromDispatchHaveData() start")

    '    DebugOut("fromDispatchHaveData removing notifications")
    '    ' wyłączenie notification
    '    RemoveHandler moChar.ValueChanged, AddressOf Gatt_ValueChangedAir
    '    Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
    '                                Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
    '    DebugOut("fromDispatchHaveData, status From write.None: " & oResp.ToString)

    '    'Await SaveNewPomiar(moTermo, moNewPom)
    '    'mbModified = True

    '    If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
    '        DialogBox("Cannot de-subscribe for notifications")
    '    End If

    '    moChar = Nothing

    '    If moPomiar IsNot Nothing Then

    '        Pomiar2Miernik()
    '        ShowLista(False)
    '    End If

    '    ProgRingShow(False)
    'End Sub


#End Region
    Private Sub uiExplain_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        ShowDialogExplain(TryCast(oMFI.DataContext, JedenMiernik))

    End Sub

    Private Function GetStringDialogExplain(dCurrValue As Double, dMin As Double, dMax As Double, sResMsg As String)
        If dCurrValue < dMin Then Return ""
        If dCurrValue >= dMax Then Return ""
        Return " " & GetLangString(sResMsg)
    End Function

    Private Sub ShowDialogExplain(oMiernik As JedenMiernik)
        If oMiernik Is Nothing Then Return
        If oMiernik.dLastTemp < 0 Then Return

        Dim sTxt As String = oMiernik.sName & vbCrLf & vbCrLf

        sTxt = sTxt & GetLangString("msgExplainTemperature") & ": " & oMiernik.dLastTemp & " °C"
        sTxt &= GetStringDialogExplain(oMiernik.dLastTemp, -100, 5, "msgExplainTemperatureTooLow")
        sTxt &= GetStringDialogExplain(oMiernik.dLastTemp, 35, 100, "msgExplainTemperatureTooHigh")

        sTxt = sTxt & vbCrLf & "CO₂: " & oMiernik.dLastCO2 & " ppm"
        ' sTmp = ZrobToastString(oItem.dLastCO2, oItem.iAlertLastCO2, 900, 1900, 2000)
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, -100, 250, "msgExplainCO2VeryLow")
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, 250, 400, "msgExplainCO2outside")
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, 400, 1000, "msgExplainCO2indoor")
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, 1000, 2000, "msgExplainCO2poorair")
        ' dwa ponizsze poza zakresem sensora
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, 2000, 5000, "msgExplainCO2risk")
        sTxt &= GetStringDialogExplain(oMiernik.dLastCO2, 5000, 10000, "msgExplainCO2highRisk")

        ' 2021.02.17, "warming up"
        If oMiernik.dLastHCHO > 16 Then
            sTxt = sTxt & vbCrLf & "HCHO (formaldehyd) " & GetLangString("msgWarmingUp")
        Else
            sTxt = sTxt & vbCrLf & "HCHO (formaldehyd): " & oMiernik.dLastHCHO & " mg/m³"
            ' sTmp = ZrobToastString(oItem.dLastHCHO, oItem.iAlertLastHCHO, 0.123, 0.615, 1.23)
            sTxt &= GetStringDialogExplain(oMiernik.dLastHCHO, -1, 0.0123, "msgExplainHCHOgreat")
            sTxt &= GetStringDialogExplain(oMiernik.dLastHCHO, 0.0123, 0.615, "msgExplainHCHOallergic")
            sTxt &= GetStringDialogExplain(oMiernik.dLastHCHO, 0.615, 1.23, "msgExplainHCHOirritation")
            sTxt &= GetStringDialogExplain(oMiernik.dLastHCHO, 1.23, 3.684, "msgExplainHCHOcancer")
            ' ponizsze poza zakresem sensora
            sTxt &= GetStringDialogExplain(oMiernik.dLastHCHO, 3.684, 100, "msgExplainHCHOdamage")
        End If

        If oMiernik.dLastTVOC > 16 Then
            sTxt = sTxt & vbCrLf & "TVOC (total volatile organic compounds) " & GetLangString("msgWarmingUp")
        Else
            sTxt = sTxt & vbCrLf & "TVOC (total volatile organic compounds): " & oMiernik.dLastTVOC & " mg/m³"
            ' ZrobToastString(oItem.dLastTVOC, oItem.iAlertLastTVOC, 0.3, 0.5, 1)
            sTxt &= GetStringDialogExplain(oMiernik.dLastTVOC, -1, 0.3, "msgExplainHCHOgreat")  ' taki sam komunikat, że super
            sTxt &= GetStringDialogExplain(oMiernik.dLastTVOC, 0.3, 0.5, "msgExplainTVOCacceptable")
            sTxt &= GetStringDialogExplain(oMiernik.dLastTVOC, 0.5, 1, "msgExplainTVOCwarning")
            sTxt &= GetStringDialogExplain(oMiernik.dLastTVOC, 1, 10, "msgExplainTVOChigh")
        End If

        DialogBox(sTxt)

    End Sub

    Private Sub uiDevice_DoubleTapped(sender As Object, e As DoubleTappedRoutedEventArgs)
        Dim oMFI As Grid = TryCast(sender, Grid)
        If oMFI Is Nothing Then Return

        ShowDialogExplain(TryCast(oMFI.DataContext, JedenMiernik))

    End Sub
End Class

#Region "konwertery bind XAML"

Public Class KonwersjaHigro
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim iHigro As Integer = CType(value, Integer)
        If iHigro < 0 Then Return ""

        ' Return the value to pass to the target.
        Return iHigro.ToString & " %"

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class

Public Class KonwersjaTemp
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim dTemp As Double = CType(value, Double)
        If dTemp < -99 Then Return ""

        ' Return the value to pass to the target.
        Return dTemp.ToString("#0.00") & " °C"

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class

Public Class KonwersjaVisibility
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.Convert

        If App.gbShowAdditCols Then
            Return Visibility.Visible
        Else
            Return Visibility.Collapsed
        End If

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class


Public Class KonwersjaMg
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim dTemp As Double = CType(value, Double)
        If dTemp < 0 Then Return ""

        ' ' 2021.02.17: "warming up", nie podajemy wartości
        If dTemp > 16 Then Return "---"

        ' Return the value to pass to the target.
        Return dTemp.ToString("#0.000") & " mg/m³"

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class

Public Class KonwersjaPpm
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim dTemp As Double = CType(value, Double)
        If dTemp < -99 Then Return ""

        ' Return the value to pass to the target.
        Return dTemp.ToString("#0") & " ppm"

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class
Public Class KonwersjaDaty
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.Convert

        Dim dTemp As DateTime = CType(value, DateTime)

        Return dTemp.ToString("dd MMM, HH:mm")

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
    ByVal targetType As Type, ByVal parameter As Object,
    ByVal language As System.String) As Object _
    Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class
#End Region
