' 2021.03.28:
' * AccuMon na starcie jest w(y)łączany wedle Settings, a po w(y)łączeniu - Settings jest uaktulniane
' * blokada przed podwójnym wejściem do komunikacji BT
' * XAML:ContextMenu, gdy wersja z PKAR_CMDLINE ma pozycje dodatkową: command2clip
' * App.vb: obsługa wywołania z cmdline (ale jakby nie działała?)

Public NotInheritable Class MainPage
    Inherits Page


    ' moje niby: Bluetooth#Bluetooth00:1a:7d:da:71:0a-00:15:a6:00:e8:07
    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        ProgRingInit(True, True)
        GetSettingsBool(uiCreateLog, "LogEnabled")
        If Not IsFamilyMobile() Then uiRunExplorer.Visibility = Visibility.Visible
        Await InitAccuMon()

        Await WczytajDevicesy()
        Await TryImportPaired()
        ShowLista()
        CrashMessageInit()
        Await CrashMessageShowAsync()
    End Sub
    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        Await App.gmGniazdka.SaveAsync(False)
    End Sub

    Private Sub uiGoInfo_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(HelpPage))
    End Sub

    Private Sub AccuMonOnOff(bRequested As Boolean)
        If bRequested Then
            If Not IsTriggersRegistered("WedQBTimer") Then RegisterTimerTrigger("WedQBTimer", 30)
        Else
            UnregisterTriggers("WedQBTimer")
        End If
    End Sub

    Private Async Sub uiBattAutoOnOff_Click(sender As Object, e As RoutedEventArgs)
        ' skoro button jest Enabled, to znaczy że mamy dostęp do Background

        If uiBattAutoOnOff.IsChecked Then
            If Await DialogBoxResYNAsync("msgWantAccuTimer") Then
                AccuMonOnOff(True)
            Else
                uiBattAutoOnOff.IsChecked = False
            End If
        Else
            AccuMonOnOff(False)
        End If
        SetSettingsBool("AccuMon", uiBattAutoOnOff.IsChecked)
        ' *TODO* włącz TIMER godzinny, i jeśli power < 30 poweron, >95 off
    End Sub

    Private Async Sub uiGoPair_Click(sender As Object, e As RoutedEventArgs)
        If Not Await DialogBoxResYNAsync("msgScanBTnet") Then Return

        StartScan()
        ' po zakonczeniu skanowania jest wywoływane StopScan, i tam jest wpisanie do listy devicesów
    End Sub

    Private Sub uiCreateLog_Click(sender As Object, e As RoutedEventArgs) Handles uiCreateLog.Click
        SetSettingsBool(uiCreateLog, "LogEnabled")
    End Sub

    Private Async Sub uiGoEpl_Click(sender As Object, e As RoutedEventArgs) Handles uiRunExplorer.Click
        Dim oFold As Windows.Storage.StorageFolder = Await GetLogFolderYearAsync(True)
        If oFold Is Nothing Then Return
        oFold.OpenExplorer()
    End Sub

    Private Async Sub uiRename_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenSocket = TryCast(oMFI.DataContext, JedenSocket)
        If oTermo Is Nothing Then Return

        If String.IsNullOrEmpty(oTermo.sName) Then oTermo.sName = oTermo.sAddr

        Dim sNewName As String = Await DialogBoxInputAsync("msgEnterNewName", oTermo.sName, "msgRename")
        If sNewName = "" Then Return

        oTermo.sName = sNewName

        ShowLista()

        'App.gmGniazdka.MakeDirty()
        Await App.gmGniazdka.SaveAsync(True)
    End Sub

    Private Sub uiDelete_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenSocket = TryCast(oMFI.DataContext, JedenSocket)
        If oTermo Is Nothing Then Return

        App.gmGniazdka.Delete(oTermo.sAddr)

        ShowLista()
        App.gmGniazdka.SaveAsync(True) ' kontynuacja zapisu będzie w tle...
    End Sub



    Private Sub uiCopyCmd_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenSocket = TryCast(oMFI.DataContext, JedenSocket)
        If oTermo Is Nothing Then Return

        Dim sCmd As String = "WeedQbPlug " & oTermo.sAddr
        ClipPut(sCmd & " on-off")
        sCmd = GetLangString("msgCommandInClipHdr") & vbCrLf &
            "  " & sCmd & " on" & vbCrLf &
            GetLangString("msgCommandInClipOr") & vbCrLf &
            "  " & sCmd & " off" & vbCrLf &
            GetLangString("msgCommandInClipEnd")
        DialogBox(sCmd)
    End Sub

    Private Async Sub uiOnOff_Toggled(sender As Object, e As RoutedEventArgs)
        Dim oTS As ToggleSwitch = TryCast(sender, ToggleSwitch)
        If oTS Is Nothing Then Return

        Dim oGniazdko As JedenSocket = TryCast(oTS.DataContext, JedenSocket)
        If oGniazdko Is Nothing Then Return

        DebugOut("Gniazdko " & oGniazdko.sName & " mam zrobic " & oTS.IsOn)

        ProgRingShow(True)

        Await App.GniazdkoWlacz(oGniazdko.sAddr, oTS.IsOn, True)

        Dim oBackgr As SolidColorBrush
        If oTS.IsOn Then
            oBackgr = New SolidColorBrush(Windows.UI.Colors.LightGreen)
        Else
            oBackgr = New SolidColorBrush(Windows.UI.Colors.OrangeRed)
        End If
        oTS.Background = oBackgr

        ProgRingShow(False)

    End Sub


    Private Function ItemIdToAddr(sId As String) As String
        ' Id = "Bluetooth#Bluetooth00:1a:7d:da:71:0a-00:15:a6:00:e8:07"
        Dim sAddr As String = sId
        Dim iInd As Integer = sAddr.LastIndexOf("-")
        If iInd > 10 Then Return sAddr.Substring(iInd + 1)

        DebugOut("Unexpected form of device ID=" & sAddr)
        Return ""
    End Function

    Private Async Function ListAddNewDevices(oGniazdka As Windows.Devices.Enumeration.DeviceInformationCollection) As Task

        For Each oPilotDI As Windows.Devices.Enumeration.DeviceInformation In oGniazdka
            If oPilotDI.Name <> "BT18" Then Continue For

            ' Id = "Bluetooth#Bluetooth00:1a:7d:da:71:0a-00:15:a6:00:e8:07"
            Dim sAddr As String = ItemIdToAddr(oPilotDI.Id)
            If sAddr = "" Then
                DebugOut("Unexpected form of device ID=" & sAddr)
                Continue For
            End If

            Await AddNewDevice(sAddr)
        Next

        Await App.gmGniazdka.SaveAsync(False)

    End Function

    Private Async Function AddNewDevice(sMAC As String) As Task(Of Boolean)
        DebugOut("AddNewDevice(" & sMAC)
        sMAC = sMAC.ToUpper

        ' kontrola czy już go znamy
        Dim bBylo As Boolean = False
        For Each oItem As JedenSocket In App.gmGniazdka.GetList
            If oItem.sAddr.ToUpper = sMAC Then Return False
        Next
        DebugOut("AddNewDevice - nowy device")

        Dim bNew As JedenSocket = New JedenSocket
        bNew.sAddedAt = Date.Now.ToString("yyyy.MM.dd")
        'bNew.uMAC = 0
        bNew.sAddr = sMAC
        bNew.sName = Await DialogBoxInputAsync("msgFoundNew", sMAC, "msgRename")
        If bNew.sName = "" Then Return False

        App.gmGniazdka.Add(bNew)

        Return True

    End Function


    'Private Sub ListaDopiszId(oGniazdka As Windows.Devices.Enumeration.DeviceInformationCollection)
    '    DebugOut("ListaDopiszId() starting, oGniazdka.Count=" & oGniazdka.Count & ", App.gmGniazdka.Count=" & App.gmGniazdka.Count)

    '    For Each oPilotDI As Windows.Devices.Enumeration.DeviceInformation In oGniazdka
    '        If oPilotDI.Name <> "BT18" Then Continue For

    '        Dim sAddr As String = ItemIdToAddr(oPilotDI.Id)
    '        If sAddr = "" Then
    '            DebugOut("Unexpected form of device ID=" & sAddr)
    '            Continue For
    '        End If

    '        DebugOut("Mam BT18, addr: " & sAddr.ToLower)
    '        For Each oItem As JedenSocket In App.gmGniazdka.GetList
    '            DebugOut("Comparing with oItem addr: " & oItem.sAddr.ToLower)

    '            If oItem.sAddr.ToLower = sAddr.ToLower Then
    '                oItem.sId = oPilotDI.Id
    '                DebugOut("Adding id to " & oItem.sName & " = " & oPilotDI.Id)
    '                Exit For
    '            End If
    '        Next
    '    Next
    '    DebugOut("ListaDopiszId() ends")
    'End Sub

    Private Async Function TryImportPaired() As Task

        ProgRingShow(True)

        ' 2) sprawdź z Paired - jeśli są paired nie w liście, to dodaj; jeśli są unpaired, zapytaj czy usunąć - chyba że przegląd sparowanych za długo trwa
        Dim oGniazdka As Windows.Devices.Enumeration.DeviceInformationCollection
        oGniazdka = Await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(True))

        ' 2a0) dump dla debug
        DebugOut("Sparowane urządzenia:")
        For Each oItem As Windows.Devices.Enumeration.DeviceInformation In oGniazdka
            DebugOut(oItem.Name & " " & oItem.Kind.ToString)
        Next

        Await ListAddNewDevices(oGniazdka)

        ProgRingShow(False)

    End Function

    Private Async Function WczytajDevicesy() As Task

        ProgRingShow(True)

        ' 1) wczytaj listę z pliku
        If App.gmGniazdka.Count < 1 Then
            Await App.gmGniazdka.LoadAsync
        End If

        ProgRingShow(False)

    End Function

    Private Sub ShowLista()
        uiItems.ItemsSource = From c In App.gmGniazdka.GetList Order By c.sName
    End Sub

    Private Async Function InitAccuMon() As Task

        Dim oBattRep As Windows.Devices.Power.BatteryReport = Windows.Devices.Power.Battery.AggregateBattery.GetReport
        uiBattAutoOnOff.IsEnabled = (oBattRep.Status <> Windows.System.Power.BatteryStatus.NotPresent)
        If uiBattAutoOnOff.IsEnabled Then
            uiBattAutoOnOff.IsEnabled = Await CanRegisterTriggersAsync()
        End If

        AccuMonOnOff(GetSettingsBool("AccuMon"))
        ' jakby się nie udało włączyć... stan może być inny niż w Settings
        uiBattAutoOnOff.IsChecked = IsTriggersRegistered("WedQBTimer")
    End Function

#Region "QRcode scanning"
    '    Public Async Function TryScanBarCode(oDispatch As Windows.UI.Core.CoreDispatcher) As Task(Of ZXing.Result)

    '        Dim oScanner As ZXing.Mobile.MobileBarcodeScanner
    '        oScanner = New ZXing.Mobile.MobileBarcodeScanner(oDispatch)
    '        'Tell our scanner to use the default overlay 
    '        oScanner.UseCustomOverlay = False
    '        ' //We can customize the top And bottom text of our default overlay 
    '        oScanner.TopText = GetLangString("resMsgScannerTop") ' "Ustaw QRcode w polu widzenia" ' "Hold camera up to barcode"
    '        oScanner.BottomText = GetLangString("resMsgScannerBottom").Replace("\n", vbCrLf) ' "Kod zostanie rozpoznany automatycznie" & vbCrLf & "Użyj 'back' by anulować" ' "Camera will automatically scan barcode" & vbCrLf & "Press the 'Back' button to Cancel"
    '        Dim oRes As ZXing.Result = Await oScanner.Scan()

    '        If oRes Is Nothing Then Return Nothing

    '        If oRes.BarcodeFormat = ZXing.BarcodeFormat.QR_CODE Then Return oRes

    '        Await DialogBoxResAsync("msgNoQRCode")
    '        Return Nothing
    '    End Function

    '    Private Async Function Skanowanie() As Task(Of String)
    '        ' kod paskowy do fotografowania
    '        Dim oRes As ZXing.Result = Await TryScanBarCode(Me.Dispatcher)

    '        ' ominiecie bledu? ale wczesniej (WezPigulka) bylo dobrze? Teraz jest 0:MainPage 1:Details
    '        If Me.Frame.BackStack.Count > 0 Then
    '            If Me.Frame.BackStack.ElementAt(Me.Frame.BackStack.Count - 1).GetType Is Me.GetType Then
    '                Me.Frame.BackStack.RemoveAt(Me.Frame.BackStack.Count - 1)
    '            End If
    '        End If

    '        If oRes Is Nothing Then Return ""

    '        Return oRes.Text

    '    End Function

#End Region

#Region "BT scanning"

    Public moBLEWatcher As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher = Nothing
    Private moTimer As DispatcherTimer
    Private miTimerCnt As Integer = 30
    Private msAllDevNames As String = ""

    Private Sub TimerTick(sender As Object, e As Object)
        DebugOut("TimerTick(), miTimer = " & miTimerCnt)
        ProgRingInc()
        miTimerCnt -= 1
        If miTimerCnt > 0 Then Return

        DebugOut("TimerTick(), stopping...")
        StopScan()

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

        If moTimer IsNot Nothing Then
            DebugOut("StopScan - stopping Timer")
            moTimer.Stop()
        End If

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then
            DebugOut("StopScan - stopping moBLEWatcher")
            moBLEWatcher.Stop()
        End If

        If uiGoPair IsNot Nothing Then uiGoPair.IsEnabled = True

        ' dodanie znalezionych
        For Each sMAC As String In msAllDevNames.Split("|")
            If sMAC.Length < 10 Then Continue For  ' z podzialu mogą być jakieś empty, i tym podobne

            Await AddNewDevice(sMAC)
        Next

        Await App.gmGniazdka.SaveAsync(False)

        ' podczas OnUnload - już nie będzie czego wyłączać; choć powinno zadziałać zabezpieczenie w ProgRing
        If uiGoPair IsNot Nothing Then
            ShowLista()
            ProgRingShow(False)
        End If
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


    Private Async Sub BTwatch_Received(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                   args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        Dim sTxt As String = "New BT device, Mac=" & args.BluetoothAddress.ToHexBytesString & ", locName=" & args.Advertisement.LocalName

        ' sprawdzenie czy to pasuje do naszego urządzenia
        If Not args.Advertisement.LocalName = "BT18" Then
            Return
        End If

        DebugOut(sTxt)  ' dopiero tu, zeby ignorowal te inne cosiki BT co sie pojawiają (22:7D:4F:50:B2:E7, C4:98:5C:D4:2E:A7, 32:75:ED:BF:BC:A7)

        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection
        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToHexBytesString & "|"
        If msAllDevNames.Contains(sNewAddr) Then
            DebugOut("ale juz taki adres mam")
            Return
        End If
        msAllDevNames = msAllDevNames & sNewAddr

    End Sub



#End Region

End Class

Public Class KonwersjaTla
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim bTmp As Boolean = CType(value, Boolean)

        If bTmp Then
            Return New SolidColorBrush(Windows.UI.Colors.LightGreen)
        Else
            Return New SolidColorBrush(Windows.UI.Colors.OrangeRed)
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

Public Class KonwersjaVisibilCmdLine
    Implements IValueConverter

    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        If value Is Nothing Then Return False
#If PKAR_CMDLINE Then
        return True
#Else
        Return False
#End If
    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class