
' 2021.05
' prośby francuskie:
' * But I can add this (=timestamp) for desktop only… (MainPage, last read)

' STORE

' 2021.05.18
' local command: debug scan (więcej info z bluetooth scanning)

' 2021.05.12:
' * CommandLine/ExecAlias
' * switch do logu danych: yearly/monthly (MijiaTermo, Alerts)
' * DebugOutFlush na Mainpage:Loaded
' * w TermoDetail
'       * więcej info do DebugOut, m.in. każda funkcja
'       * uiUnitsButton_Click, uiTimeButton_Click,uiHappyFaceButton_Click: oWriter.Dispose() 
' * w MijiaTermo, MijiaGetCurrentData, Gatt_ValueChangedMT, wiecej zabezpieczeń i debugout
' REQUEST: yearly log
' REQUEST: "request the app to read the details, which seem to work just fine. However, less than a second after I have the readings it closes without giving any error or warning."


' 2021.05.09: MainPage, BT_received, już nie tworzy oDev (i tak był nieużywany)


' 2021.04.18
' * ObsluzTermoAtBackgroundAsync, przy błędzie oItem.oGATTsvc.Dispose()
' * GetBTserviceFromMacAsync, przy błędzie oDev.Dispose()


' 2021.04.14
' * GetBTcharacterForMijiaAsync: oService.Dispose

' 2021.02.02
' * MijiaGetCurrentData, jesli sie nie udało "odpięcie" notification to próbuje to zrobić jeszcze raz
' * analogicznie, jesli się nie udalo w MijiaGetCurrentData uzyskać GetBTcharacterForMijiaAsync, to na desktop robi jeszcze raz ale bez cache (znacznie wydłuża to czas siedzenia w tle)

' STORE 2102.02
' 2021.01.26
' * poprawka w warning/alarm na plus (było odwrotnie 1 i 2)

' STORE

' 2021.01.23-26
' * Wyliczanie temp odczuwalnej (przy konwersji DeviceData->oTermo)
' * Page: Alerts
' * Historia - guzik kasowania jest zablokowany podczas ściągania
' * MijaTermo:MijiaGetCurrentData , z TaskCompletionSource - przygotowanie do Background
' * background: alerty (toasty) temp/higro/temp odczuwalna
' * background: ściąganie rekordów godzinowych
' * Page: Calculator (przeliczanie temp i wilg na temp odczuwalną)

' STORE
'
' 2020.12.11:
' * MijaThermo, MijiaDecodeRecord, zabezpieczenie przed "za krótkimi" danymi
' * MijaThermo, GetSaveFile - zmiana ":" na "-" w nazwie pliku, jak name=MAC
' * MainPage, Rename - weryfikacja newname na "/:\"
' * o dziwo, teraz NetIsBTavailableAsync wylatuje na Radios.RequestAccessAsync gdy nie ma deviceCap=radios? (bo userdenied)
' * więc dodaję do Manifest deviceCap=radios, pytanie jest o on/off...

' device pamieta przynajmniej miesiąc
' jak nie wczyta nic z historii, to odpowiedni komunikat - a nie empty (po wyczyszczeniu)
' sprwdzic pokazywanie odznaczonych termomentrow (mam jeden wszak ktory juz wyszedl)
' moze dodac jeszcze kasowanie termometru?


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

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        Await DebugOutFlush() ' zapisz to, co było w poprzednich uruchomieniach ale jeszcze jest w zmiennej

        ProgRingInit(True, True) ' ewnetualnie progbar też true, jeśli zrobimy na Timerze co sekunde tick do 30, i za każdym razem zwiększanie value,

        ProgRingShow(True)

        GetAppVers(Nothing)    ' dodaj TextBlock z wersją
        If Not IsFamilyMobile() Then uiRunExplorer.Visibility = Visibility.Visible
        ReadResStrings

        'GetSettingsBool(uiOnOff, "MonitorRunning")

        Await App.gmTermo.LoadAsync()

        ShowLista(False)

        moTimer = New DispatcherTimer()
        moTimer.Interval = New TimeSpan(0, 0, 1)
        AddHandler moTimer.Tick, AddressOf TimerTick

        ' kontrola Bluetooth, ale jako warningi raczej (na tym etapie przynajmniej)
        Await NetIsBTavailableAsync(True, False, GetLangString("msgWarnNoBT")) ' tylko pokaż komunikat w razie gdyby nie było BT

        If GetSettingsBool("uiAlertsOnOff") Then
            If Not IsTriggersRegistered("MijiaThermoTimer") Then
                If Not Await CanRegisterTriggersAsync() Then
                    DialogBoxRes("msgFailNoBackground")
                Else
                    RegisterTimerTrigger("MijiaThermoTimer", 15)
                End If
            End If
        End If

        ProgRingShow(False)

    End Sub

    Private Shared Sub ReadResStrings()
        SetSettingsString("msgToastAppTemp", GetLangString("msgToastAppTemp", "Todcz"))
        SetSettingsString("msgToastTemp", GetLangString("msgToastTemp", "Temp"))
        SetSettingsString("msgToastHigro", GetLangString("msgToastHigro", "Wilg"))
        SetSettingsString("msgCannotReach", GetLangString("msgCannotReach", "zniknięty"))

    End Sub

    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        Await StopScan()
        ProgRingShow(False, True)
    End Sub


    Private Sub uiGoDetails_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenTermo = TryCast(oMFI.DataContext, JedenTermo)
        If oTermo Is Nothing Then Return

        Me.Frame.Navigate(GetType(TermoDetail), oTermo.uMAC)
    End Sub

    Private Sub uiGoHistory_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenTermo = TryCast(oMFI.DataContext, JedenTermo)
        If oTermo Is Nothing Then Return

        Me.Frame.Navigate(GetType(DownloadHistory), oTermo.uMAC)
    End Sub
    Private Sub uiGoAlerts_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(Alerty))
    End Sub
    Private Sub uiMakeTile_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenTermo = TryCast(oMFI.DataContext, JedenTermo)
        If oTermo Is Nothing Then Return

        ' *TODO* 
        ' nazwa małymi centrowana, pod nim temperatura, ewentualnie poniżej małymi wilgotność
    End Sub

    Private Async Sub uiRename_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        If oMFI Is Nothing Then Return

        Dim oTermo As JedenTermo = TryCast(oMFI.DataContext, JedenTermo)
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

        Await App.gmTermo.SaveAsync() ' nie czekam, ale raczej nic się nie zdarzy w trakcie

    End Sub


    'Private Sub uiOnOff_Click(sender As Object, e As RoutedEventArgs)
    '    If uiOnOff.IsChecked Then
    '        If Not IsTriggersRegistered("MijiaThermo") Then
    '            RegisterTimerTrigger("MijiaThermo_Timer", 60)
    '        End If
    '    Else
    '        UnregisterTriggers("MijiaThermo")
    '    End If
    '    SetSettingsBool(uiOnOff, "MonitorRunning")
    'End Sub

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

    Private mbDebugScan As Boolean = False

    Private Async Sub StartScan()
        DebugOut("StartScan()")

        ' scan debug jest tylko jednokrotne!
        mbDebugScan = GetSettingsBool("debugScan", True)
        SetSettingsBool("debugScan", False)

        If Await NetIsBTavailableAsync(False) < 1 Then Return


        msAllDevNames = ""

        For Each oItem In App.gmTermo.GetList
            msAllDevNames = msAllDevNames & "|" & oItem.uMAC.ToString & "|"
        Next

        DebugOut("Known devices: " & msAllDevNames)


        moTimer.Interval = New TimeSpan(0, 0, 1)
        miTimerCnt = 15 ' sekund na szukanie, ale z progress bar
        moTimer.Start()

        ProgRingShow(True, False, 0, miTimerCnt)

        ' App.moDevicesy = New Collection(Of JedenDevice) - nieprawda! korzystamy z dotychczasowych danych!
        uiGoPair.IsEnabled = False
        ScanSinozeby()
    End Sub

    Private Async Function StopScan() As Task
        DebugOut("StopScan()")

        moTimer.Stop()

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then

            DebugOut("StopScan - stopping moBLEWatcher")
            RemoveHandler moBLEWatcher.Received, AddressOf BTwatch_Received
            moBLEWatcher.Stop()
            moBLEWatcher = Nothing
        End If

        uiGoPair.IsEnabled = True
        Await App.gmTermo.SaveAsync()
        ProgRingShow(False)
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

        Dim sTxt As String = "New BT device, Mac=" & args.BluetoothAddress.ToHexBytesString & ", locName='" & args.Advertisement.LocalName & "'"
        If mbDebugScan Then DebugOut(sTxt)

        '' tylko na czas prob uruchomienia app!
        ' Dim sMac As String = args.BluetoothAddress.ToHexBytesString
        'If Not sMac.StartsWith("A4:C1") Then Return ' termometr

        If args.Advertisement.LocalName <> "LYWSD03MMC" Then
            If mbDebugScan Then DebugOut("nazwa nie termometrowa")
            Return
        End If

        If Not mbDebugScan Then DebugOut(sTxt)  ' dopiero tu, zeby ignorowal te inne cosiki BT co sie pojawiają (22:7D:4F:50:B2:E7, C4:98:5C:D4:2E:A7, 32:75:ED:BF:BC:A7)

        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection
        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToString & "|"
        If msAllDevNames.Contains(sNewAddr) Then
            DebugOut("ale juz taki adres mam")
            Return
        End If
        msAllDevNames = msAllDevNames & sNewAddr

        DebugOut("jeszcze go nie znam")

        ' 2021.05.09: a po co to było?? nie mówiąc już o tym, że oDev trzeba by było Dispose!
        ' Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        'oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress)

        'If oDev Is Nothing Then
        '    DebugOut("oDev null, cannot continue")
        '    Return
        'End If

        Dim oNew As JedenTermo = New JedenTermo
        oNew.uMAC = args.BluetoothAddress
        'oNew.sMac = oNew.uMAC.ToHexBytesString
        oNew.sName = oNew.uMAC.ToHexBytesString.Substring(9, 8)
        If SprobujDodac(oNew) Then
            Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
        End If

    End Sub
    Private Shared Function SprobujDodac(oNew As JedenTermo) As Boolean
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


End Class

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
Public Class KonwersjaAppTemp
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim dTemp As Double = CType(value, Double)
        If dTemp < -99 Then Return ""   ' nie ma podanej/wyliczonej

        ' Return the value to pass to the target.
        Return "(" & dTemp.ToString("#0.00") & " °C)"

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

