
' początek pracy: 2021.09.29
' cel: sprawdzenie sensowności app "znajdzKumpla"
' cel: ale także zabawa w wykrywanie w czasie jazdy tramwajem różnych rzeczy

' C:\Users\pkar\AppData\Local\Packages\bbda6454-3375-44a7-b1dc-f12e50d460c7_y9hvt3b1p7jrj\RoamingState

Public NotInheritable Class MainPage
    Inherits Page

    Private mbInScanning As Boolean = False
    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        GetAppVers(Nothing) ' dodaj TextBlock z wersją
        ProgRingInit(True, False)

        ' kontrola Bluetooth, ale jako warningi raczej (na tym etapie przynajmniej)
        Await NetIsBTavailableAsync(True, False) ' tylko pokaż komunikat w razie gdyby nie było BT

        ' jakby coś było, to pokaż
        ShowLista(False)

        Await App.glZnane.LoadAsync

    End Sub

    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        Await StopScan()
        Await App.glZnane.SaveAsync
        ProgRingShow(False, True)
    End Sub

    Private Async Sub uiStartScan_Click(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        If Not mbInScanning Then
            ' włącz
            If (Await NetIsBTavailableAsync(True, False)) <> 1 Then Return
            uiStartScan.Content = "Stop"
            mbInScanning = True
            StartScan()
        Else
            ' wyłącz
            uiStartScan.Content = "Start!"
            mbInScanning = False
            Await StopScan()
            uiItems.ItemsSource = From c In App.glNowe.mItems Order By c.dTimeStamp Descending
        End If
    End Sub

    Private Sub ShowLista(bAll As Boolean)
        DumpCurrMethod()
        uiItems.ItemsSource = Nothing

        If App.glNowe.Count < 1 Then Return

        ' przez przekopiowanie omijam problem z przypisaniem listy do innego wątku
        uiItems.ItemsSource = From c In App.glNowe.mItems Order By c.dTimeStamp Descending

    End Sub


    Private Async Function SaveDevicesAsync() As Task
        DumpCurrMethod()
        Dim sFilename As String = Date.Now.ToString("yyyyMMdd-HHmmss") & ".txt"
        Dim oFold As Windows.Storage.StorageFile = Await GetLogFileDailyAsync(sFilename, True)
        If oFold Is Nothing Then
            DialogBox("ERROR: GetLogFileDailyAsync failed")
            Return
        End If

    End Function

#Region "skanowanie sieci"
    Public moBLEWatcher As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher = Nothing

    Private msAllDevNames As String = ""

    Private Async Sub StartScan()
        DumpCurrMethod()

        msAllDevNames = ""
        ' raczej pomińmy te, co są znane (np. termometry)
        If uiFilterKnown.IsChecked Then
            For Each oItem As JedenZnany In App.glZnane.mItems
                msAllDevNames = msAllDevNames & "|" & oItem.uMAC.ToString & "|"
            Next
            DumpMessage("All known devices: " & msAllDevNames)
        End If

        If Await NetIsBTavailableAsync(False) < 1 Then Return

        App.glNowe.mItems.Clear()

        ProgRingShow(True, False)

        ScanSinozeby()
    End Sub

    Private Async Function StopScan() As Task
        DumpCurrMethod()

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then

            DumpMessage("StopScan - stopping moBLEWatcher")
            RemoveHandler moBLEWatcher.Received, AddressOf BTwatch_Received
            moBLEWatcher.Stop()
            moBLEWatcher = Nothing
        End If

        Await SaveDevicesAsync()
        ProgRingShow(False)
    End Function

    Private Sub ScanSinozeby()
        DumpCurrMethod()
        ' https://stackoverflow.com/questions/40950482/search-for-devices-in-range-of-bluetooth-uwp
        'przekopiowane z RGBLed
        moBLEWatcher = New Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher
        moBLEWatcher.ScanningMode = Windows.Devices.Bluetooth.Advertisement.BluetoothLEScanningMode.Passive
        If uiActivePassive.IsChecked Then
            moBLEWatcher.ScanningMode = Windows.Devices.Bluetooth.Advertisement.BluetoothLEScanningMode.Active
        End If
        AddHandler moBLEWatcher.Received, AddressOf BTwatch_Received

        moBLEWatcher.Start()
    End Sub


    Private Async Sub BTwatch_Received(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                   args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        ' wewnetrzne zabezpieczenie przed powtorkami - bo czesto wyskakuje blad przy ForEach, ze sie zmienila Collection

        Dim sNewAddr As String = "|" & args.BluetoothAddress.ToString & "|"

        If msAllDevNames.Contains(sNewAddr) Then
            DumpCurrMethod(sNewAddr & ") - ale juz taki adres mam")
            If String.IsNullOrEmpty(args.Advertisement.LocalName) Then Return

            If SprobujDodacNazwe(args.BluetoothAddress, args.Advertisement.LocalName) Then
                Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
            End If
            Return
        End If

        msAllDevNames = msAllDevNames & sNewAddr

        DumpCurrMethod(sNewAddr & "=" & args.BluetoothAddress.ToHexBytesString & ") - NOWY")

        Dim oNew As New JedenBT
        oNew.uMAC = args.BluetoothAddress
        oNew.sName = args.Advertisement.LocalName
        DumpMessage("adding with localname=" & args.Advertisement.LocalName)
        If String.IsNullOrEmpty(oNew.sName) Then oNew.sName = oNew.uMAC.ToHexBytesString
        oNew.oAdvert = args.Advertisement
        oNew.dTimeStamp = args.Timestamp

        If SprobujDodac(oNew) Then
            Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
        End If

    End Sub
    Private Shared Function SprobujDodac(oNew As JedenBT) As Boolean
        DumpCurrMethod(oNew.uMAC.ToHexBytesString)

        Try
            App.glNowe.mItems.Add(oNew)
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Shared Function SprobujDodacNazwe(uMAC As ULong, sName As String) As Boolean
        DumpCurrMethod(uMAC.ToHexBytesString & "," & sName)

        If String.IsNullOrEmpty(sName) Then Return True

        Try
            For Each oItem As JedenBT In App.glNowe.mItems
                If oItem.uMAC = uMAC Then
                    If oItem.sName <> sName Then
                        oItem.sName = sName
                        Return True
                    End If
                    Return False
                End If
            Next
            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function


    Private Shared bInside As Boolean = False

    Public Async Sub fromDispatchShowItems()
        DumpCurrMethod()

        If bInside Then
            Debug.WriteLine("czekam")
            For i As Integer = 1 To 10
                Await Task.Delay(10)
                If Not bInside Then Exit For
            Next
            bInside = True
        End If

        DumpMessage("nowa lista, count=" & App.glNowe.Count)

        ShowLista(True)

        bInside = False
    End Sub
#End Region

    'Shared gmDevices As List(Of JedenBT)

    Private Sub uiGoKnownList_Click(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        Me.Frame.Navigate(GetType(ListaZnanych))
    End Sub

    Private Sub uiGetDetails_Click(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()

        Dim oFEl As FrameworkElement = sender
        Dim oItem As JedenBT = oFEl.DataContext

        'Dim oMFI As MenuFlyoutItem = TryCast(sender, MenuFlyoutItem)
        'If oMFI IsNot Nothing Then
        '    oItem = oMFI.DataContext
        'Else
        '    Dim oButt As Button = TryCast(sender, Button)
        '    If oButt Is Nothing Then Return
        '    oItem = oButt.DataContext
        'End If


        Me.Frame.Navigate(GetType(DetailsBT), oItem.uMAC)
    End Sub

    Private Async Sub uiAddKnown_Click(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As JedenBT = oMFI.DataContext

        Dim sName As String = oItem.sName
        If String.IsNullOrEmpty(sName) Then sName = oItem.uMAC.ToHexBytesString
        sName = Await DialogBoxInputDirectAsync("Pod jaką nazwą zapisać?", sName)
        If String.IsNullOrEmpty(sName) Then Return

        App.glZnane.Add(New JedenZnany(oItem.uMAC, sName))

    End Sub
End Class

