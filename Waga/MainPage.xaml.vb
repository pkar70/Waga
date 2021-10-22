' strong points:
' 1) tarowanie
' 2) przeliczanie fat wedle BMI, jak nie ma resistance
' 3) przekazywanie danych ostatniego pomiaru miedzy urzadzeniami Windows / instalacjami app
' 4) zapis historii pomiarow w postaci eksportowalnej (do wykorzystania np. w Excel)
' 5) mozliwosc NIE zapisywania czegokolwiek
' 6) brak komunikacji z Internet (poza systemowym - zmienne Roaming)

'Imports System.Reflection  ' potrzebne do foreach(properties)


Public NotInheritable Class MainPage
    Inherits Page

    Private mLastOsoba As String = ""

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        ' kontrola Bluetooth, ale jako warningi raczej (na tym etapie przynajmniej)

        ProgRingInit(True, False)
        ProgRingShow(True)
        'GetAppVers(Nothing)

        ' sprawdza sobie Bluetooth, jednoczesnie wczytując pliki danych
#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        NetIsBTavailableAsync(True, True, "msgBtWarnDisabled", "msgBtErrNotFound") ' tylko pokaż komunikat w razie gdyby nie było BT
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed

        Await App.gLudzie.LoadAsync
        Await App.gWagi.LoadAsync

        WypelnComboLudzi()

        Await ProbaOdczytuRoaming()

        ' włącz domyślnie - potrzebne później
        SetSettingsBool("uiSettSaveDataLog", GetSettingsBool("uiSettSaveDataLog", True))

        ' ewentualnie odczyt ostatniego pomiaru (roaming), jesli nie starszy niż doba
        ' jesli to nie jest "własny" pomiar, to dopisz do historii
        ProgRingShow(False)

    End Sub



    Private Async Function ProbaOdczytuRoaming() As Task

        If Not Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("roamLastMeasure") Then return

        Dim sRoamDate As String = Windows.Storage.ApplicationData.Current.LocalSettings.Values("roamLastMeasure").ToString

        Dim sLocalDate As String = "1970"
        If Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("localLastMeasure") Then
            sLocalDate = Windows.Storage.ApplicationData.Current.LocalSettings.Values("localLastMeasure").ToString
        End If

        If sRoamDate <= sLocalDate Then Return

        If Not Await DialogBoxResYNAsync("msgWczytacRoaming") Then Return

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync("current.json")
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then Return

        moPomiar = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(JedenPomiar))

        uiButtonPomiar.Visibility = Visibility.Collapsed

        ' pokaz dane ludzika na ekranie

        ' musimy przygotować SettingsInt("currPerson", -1)
        For Each oItem As JednaOsoba In App.gLudzie.GetList
            ' przy okazji - przejscie przez nazwę, jakby ID mialo sie roznic (ale nie bedzie, bo to plik Roaming przeciez)
            If oItem.sName = moPomiar.sUserName Then
                SetSettingsInt("currPerson", oItem.id)
                Exit For
            End If
        Next
        ' moze niepotrzebnie, ale nie zajmie to duzo czasu - zostanie wybrana osoba z ustawionym ID
        ' wypelni to rowniez pola typu wiek etc.
        WypelnComboLudzi()

        ' podstawowe dane na ekran (poniekąd kopia z fromDispatchShow, ale tam wykorzystywany oFrame)
        uiRawWeight.Text = moPomiar.rawWeight.ToString("##0.0") & " kg"
        uiPomiarNo.Text = moPomiar.iMeasNum
        uiResist.Text = moPomiar.opor
        uiUseBio.IsOn = moPomiar.validBio

        uiMasa.Text = moPomiar.weight.ToString("##0.0") & " kg"

        ' oraz biozalezne
        PokazBio()

#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        SaveDataLog("uiSettSaveImportDataLog")  ' zapisz do DataLog, jeśli masz zgodę na zapis Roam do DataLog
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed

    End Function

    Private Async Function SaveOdczytRoaming() As Task

        If moPomiar Is Nothing Then Return

        Dim sLastSave As String = DateTime.Now.ToString("yyyyMMdd.HHmmss")
        SetSettingsString("localLastMeasure", sLastSave)

        If Not GetSettingsBool("uiSettSaveRoam", True) Then Return
        SetSettingsString("roamLastMeasure", sLastSave, True)

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(moPomiar, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync("current.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)


    End Function

    Private Async Function SaveDataLog(sSettName As String) As Task
        ' SaveDataLog("uiSettSaveImportDataLog")
        ' SaveDataLog("uiSettSaveDataLog")

        If Not GetSettingsBool(sSettName) Then Return

        Dim sTxt As String

        Dim oFold As Windows.Storage.StorageFolder
        ' append - zawsze
        oFold = Await GetLogFolderRootAsync()
        If oFold IsNot Nothing Then
            ' konwersja do CSV (Tab)

            Dim oFile As Windows.Storage.StorageFile = Await oFold.CreateFileAsync("all.csv", Windows.Storage.CreationCollisionOption.OpenIfExists)
            If oFile IsNot Nothing Then
                Dim oProp As Windows.Storage.FileProperties.BasicProperties = Await oFile.GetBasicPropertiesAsync
                If oProp IsNot Nothing Then
                    If oProp.Size < 10 Then
                        sTxt = moPomiar.GetCSVHdrLine(1)    ' 1: wersja protokolu, nie podaje wszystkich pól
                        Await oFile.AppendLineAsync(sTxt)
                    End If
                End If

                sTxt = moPomiar.ToCSVString(1)
                Await oFile.AppendLineAsync(sTxt)
            End If

        End If

        ' (sFileName As String, Optional bUseOwnFolderIfNotSD As Boolean = True)

        'DataLogs\ Waga \ yyyy \ mm \ pomiar.yyyymmddhhmm.json - po odczycie i przeliczeniu, bez pozniejszych zabaw
        'DataLogs\ Waga \ all.csv(Tab - separated) - j.w., do robienia ClipPut


        ' detailed - wedle dodatkowego przełącznika
        If GetSettingsBool("uiSettSaveDetailedDataLog") Then
            oFold = Await GetLogFolderMonthAsync()
            If oFold IsNot Nothing Then
                sTxt = Newtonsoft.Json.JsonConvert.SerializeObject(moPomiar, Newtonsoft.Json.Formatting.Indented)

                Dim sFileName As String = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss") & ".json"

                Await oFold.WriteAllTextToFileAsync("current.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)
            End If
        End If

    End Function

    Private Sub WypelnComboLudzi()

        uiCombo.Items.Clear()

        Dim oNew As ComboBoxItem
        Dim bWas As Boolean = False
        Dim iLastId As Integer = GetSettingsInt("currPerson", -1)

        If App.gLudzie.Count > 0 Then
            For Each oItem As JednaOsoba In From c In App.gLudzie.GetList Order By c.sName
                oNew = New ComboBoxItem
                oNew.Content = oItem.sName
                oNew.DataContext = oItem.id
                If oItem.id = iLastId Then
                    bWas = True
                    oNew.IsSelected = True
                End If
                uiCombo.Items.Add(oNew)
            Next
        End If

        ' oraz gosc, na początek
        oNew = New ComboBoxItem
        oNew.Content = "-guest"
        oNew.DataContext = -1
        If Not bWas Then oNew.IsSelected = True
        uiCombo.Items.Insert(0, oNew)

    End Sub

    Private Sub uiCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If e?.AddedItems Is Nothing Then Return
        If e?.AddedItems.Count < 1 Then Return

        Dim oCBItem As ComboBoxItem = e.AddedItems.ElementAt(0)
        If oCBItem Is Nothing Then Return
        Dim iID As Integer = oCBItem.DataContext

        If App.gLudzie.Count > 0 Then
            For Each oItem As JednaOsoba In App.gLudzie.GetList
                If oItem.id = iID Then

                    SetSettingsInt("currPerson", iID)

                    uiDatUr.Date = oItem.dDataUrodz
                    uiSex.IsOn = oItem.bWoman
                    mLastOsoba = oItem.sName
                    Try
                        uiSlider.Value = oItem.iCurrentWzrost
                    Catch ex As Exception
                        ' jakby bylo cos nie tak, czyli za malo lub za duzo, recznie wpisane do pliku...
                        uiSlider.Value = 100
                    End Try

                End If
            Next
        End If

    End Sub

    Private Sub ZmienioneDaneWejsciowe()
        ' gdy zmieniono datę urodzenia, płeć, lub wzrost

        ' ewentualnie seria "ignoruj, bo nie masz danych"
        ' If uiDatUr.Date.Date = DEFAULT then return

        If uiMasa Is Nothing Then Return    ' zanim bedzie wszystko na ekranie, juz jest wywolywane

        ' nie ma pomiaru, więc nie ma co przeliczać
        If uiMasa.Text = "" Then Return
        If moPomiar Is Nothing Then Return

        PrzeliczIpokaz()

    End Sub

    Private Sub uiZmianyWOsobie_Changed(sender As Object, e As RoutedEventArgs)
        ZmienioneDaneWejsciowe()
    End Sub

    Private Sub uiZmianyWOsobie_Changed(sender As Object, e As RangeBaseValueChangedEventArgs)
        ZmienioneDaneWejsciowe()
    End Sub
    Private Sub uiZmianyWOsobie_Changed(sender As Object, e As DatePickerValueChangedEventArgs)
        ZmienioneDaneWejsciowe()
    End Sub
    Private Sub uiUseBio_Toggled(sender As Object, e As RoutedEventArgs) Handles uiUseBio.Toggled
        ZmienioneDaneWejsciowe()
    End Sub

    Private Sub uiZwazTeraz_Click(sender As Object, e As RoutedEventArgs)
        StartScan()
    End Sub
    Private Sub uiGoAccounts_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(Ludziki))
    End Sub
    Private Sub uiGoPair_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(Wagi))
    End Sub
    Private Sub uiGoInfo_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(Settingsy))
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        StopScan()
        ProgRingShow(False, True)

        'If moPomiar Is Nothing Then Return
        'If Not mbHasPomiar Then Return
        ' Await DialogBoxAsync("alamakota")
        ' zapis danych - tylko czy tu jeszcze mozna zapytac?
        ' NIE musimy tego robic, bo zapisujemy wczesniej

    End Sub

    Dim moPomiar As JedenPomiar = Nothing
    Dim mbHasPomiar As Boolean = False

    ''' <summary>
    ''' Zamiana Frame na JedenPomiar, uwzględnia tarowanie, i przepisuje validBio
    ''' nie liczy fatów ani innych rzeczy
    ''' </summary>
    Private Function Frame2Pomiar(oFrame As chipseaBroadcastFrame) As JedenPomiar
        Dim oNew As JedenPomiar = New JedenPomiar
        oNew.opor = oFrame.resistance
        oNew.rawWeight = oFrame.weight
        oNew.uMac = oFrame.uMac
        oNew.iMeasNum = oFrame.measureSeqNo
        oNew.dData = DateTime.Now
        oNew.validBio = True    ' na razie załóżmy, może potem zmienimy

        oNew.weight = oFrame.weight
        If App.gWagi.Count > 0 Then
            For Each oItem As JednaWaga In App.gWagi.GetList
                If oItem.uMAC = oFrame.uMac Then
                    oNew.weight += oItem.dTara
                    oNew.validBio = oItem.bUseBio
                    Exit For
                End If
            Next
        End If

        Return oNew

    End Function

    ''' <summary>
    ''' działania na moPomiar
    ''' </summary>
    Private Sub PrzeliczIpokaz()
        ' ale nie raw, to juz jest zrobione; teraz BMI i tak dalej
        ' zarówno zaraz po pomiarze, jak i po zmianie tymczasowej (na ekranie)

        ' na wszelki wypadek zabezpieczenie
        If moPomiar Is Nothing Then Return

        ' ustaw wiek, wzrost, sex
        moPomiar.height = uiSlider.Value
        moPomiar.sex = uiSex.IsOn
        moPomiar.age = (DateTimeOffset.Now - uiDatUr.Date).TotalDays / 365
        moPomiar.sUserName = mLastOsoba

        ' przelicz
        CsAlgoBuilder(moPomiar, uiUseBio.IsOn)

        PokazBio()

    End Sub

    Private Sub PokazBio()
        ' skasuj poprzednie dane
        uiDisplayItems.Children.Clear()

        ' pokaz
        RysujBMI(uiDisplayItems, moPomiar.BMI)
        RysujFat(uiDisplayItems, moPomiar.fat, moPomiar.sex)    ' to bedzie albo z Oporu, albo z BMI policzone

        ' jesli waga oszukuje, to nie pokazujemy pozostałych danych
        'If Not moPomiar.validBio Then Return
        If Not uiUseBio.IsOn Then Return
        If moPomiar.opor < 1 Then Return ' =0 gdy nie jest ustawiony

        RysujWoda(uiDisplayItems, moPomiar.water)
        RysujViscFat(uiDisplayItems, moPomiar.viscera)

        ' reszta jest wyliczona, ale nie pokazuje na razie

    End Sub

#Region "Skanowanie BT"
    ' podobne jest tu, i w robieniu pomiaru - synchronizowac to jakos 
    ' a moze kiedys zrobic jako Class, z AddHandler przy moBLEWatcher.Received

    Public moBLEWatcher As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher = Nothing
    Private oTimer As DispatcherTimer
    Private iTimerCnt As Integer = 30
    Private msAllDevNames As String = ""


    Private Sub TimerTick(sender As Object, e As Object)
        'iTimerCnt = iTimerCnt - 1
        'If iTimerCnt < 1 Then StopScan()
        StopScan()
        'toDispatch()
    End Sub

    Private Async Sub StartScan()
        If Await NetIsBTavailableAsync(True, True, "msgBtErrDisabled", "msgBtErrNotFound") < 1 Then Return

        oTimer = New DispatcherTimer
        oTimer.Interval = New TimeSpan(0, 0, 15)    ' 15 sekund na szukanie

        oTimer.Start()

        ProgRingShow(True)

        uiButtonPomiar.Visibility = Visibility.Collapsed
        uiBarPomiar.IsEnabled = False

        ScanSinozeby()
    End Sub


    Private Sub StopScan()

        oTimer?.Stop()

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then

            moBLEWatcher.Stop()
        End If

        uiBarPomiar.IsEnabled = True
        ProgRingShow(False)
    End Sub

    Private Sub ScanSinozeby()
        ' https://stackoverflow.com/questions/40950482/search-for-devices-in-range-of-bluetooth-uwp
        'przekopiowane z RGBLed
        moBLEWatcher = New Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher
        moBLEWatcher.ScanningMode = 1   ' tylko czeka, 1: żąda wysłania adv
        AddHandler moBLEWatcher.Received, AddressOf BTwatch_Received
        moBLEWatcher.Start()
    End Sub

    Dim moFrame As chipseaBroadcastFrame

    Private Async Sub BTwatch_Received(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                       args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        DebugOut("New BT device, Mac=" & args.BluetoothAddress.ToHexBytesString & ", locName=" & args.Advertisement.LocalName)

        Dim iProto As Waga_protocol = GetProtocolFromAdv(args.Advertisement)

        If iProto = Waga_protocol.UNKNOWN Then
            DebugOut("nie rozpoznaję jako wagi, czekam dalej")
            Return
        End If

        DebugOut("chyba waga")

        Dim oPomiar As chipseaBroadcastFrame = PrzetworzDaneZWagi(args.Advertisement, args.BluetoothAddress)
        If oPomiar Is Nothing Then Return
        If oPomiar.weight = 0 Then Return   ' jeszcze nie mamy pomiaru, czekaj...

        Await uiPomiarDetails.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchStopScan)

        moFrame = oPomiar

        Await uiPomiarDetails.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShow)

    End Sub

    Public Sub fromDispatchShow()
        ' ustaw wedle moPomiar

        uiRawWeight.Text = moFrame.weight.ToString("##0.0") & " kg"
        uiPomiarNo.Text = moFrame.measureSeqNo
        uiResist.Text = moFrame.resistance

        moPomiar = Frame2Pomiar(moFrame)
        uiUseBio.IsOn = moPomiar.validBio

        uiMasa.Text = moPomiar.weight.ToString("##0.0") & " kg"

        PrzeliczIpokaz() ' BMI i tak dalej

#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        SaveOdczytRoaming() ' ewentualnie zapisz
        SaveDataLog("uiSettSaveDataLog")    ' oraz do DataLog, jesli jest na to zgoda
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed

    End Sub

    Public Sub fromDispatchStopScan()
        StopScan()
    End Sub



#End Region

End Class

Public Class KonwersjaVisibility
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim bTmp As String = CType(value, Boolean)
        If bTmp Then Return Visibility.Visible
        Return Visibility.Collapsed

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class