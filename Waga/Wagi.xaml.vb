' * lista zdefiniowanych devices - nazwa, MAC, data dodania
' * wysłanie emailem (albo do clip?) listy devices
' * lista devices ROAM/local

Public NotInheritable Class Wagi
    Inherits Page

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)

        ProgRingInit(True, True)

        ProgRingShow(True)
        If Not App.gWagi.IsLoaded Then Await App.gWagi.LoadAsync()

        oTimer = New DispatcherTimer()
        oTimer.Interval = New TimeSpan(0, 0, 1)
        AddHandler oTimer.Tick, AddressOf TimerTick

        PokazItemy()

        ProgRingShow(False)
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        StopScan()
        ProgRingShow(False, True)
    End Sub

    Private Async Sub uiAddWaga_Click(sender As Object, e As RoutedEventArgs)
        Await DialogBoxResAsync("msgWagiWejdz")
        StartScan()
    End Sub

    Private Async Sub uiSave_Click(sender As Object, e As RoutedEventArgs)
        Await App.gLudzie.SaveAsync(True)
        Me.Frame.GoBack()
    End Sub

    Private Sub PokazItemy()
        If App.gWagi.Count < 1 Then
            uiItems.ItemsSource = Nothing
        Else
            uiItems.ItemsSource = From c In App.gWagi.GetList() Order By c.sName
        End If

    End Sub

#Region "Skanowanie BT"
    ' podobne jest tu, i w robieniu pomiaru - synchronizowac to jakos 
    ' a moze kiedys zrobic jako Class, z AddHandler przy moBLEWatcher.Received

    Public moBLEWatcher As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher = Nothing
    Private oTimer As DispatcherTimer
    Private miTimerCnt As Integer = 30
    Private msAllDevNames As String = ""


    Private Sub TimerTick(sender As Object, e As Object)
        miTimerCnt = miTimerCnt - 1

        ProgRingInc()
        If miTimerCnt < 1 Then StopScan()

        'toDispatch()
    End Sub

    Private Async Sub StartScan()
        If Await NetIsBTavailableAsync(False) < 1 Then Return

        msAllDevNames = ""

        For Each oItem In App.gWagi.GetList
            msAllDevNames = msAllDevNames & "|" & oItem.uMAC.ToString & "|"
        Next

        oTimer.Interval = New TimeSpan(0, 0, 1)    ' 15 sekund na szukanie
        'iTimerCnt = 30
        miTimerCnt = 15
        oTimer.Start()

        ProgRingShow(True, False, 0, miTimerCnt)

        ' App.moDevicesy = New Collection(Of JedenDevice) - nieprawda! korzystamy z dotychczasowych danych!
        uiAddWaga.IsEnabled = False
        ScanSinozeby()
    End Sub


    Private Sub StopScan()

        oTimer.Stop()

        If moBLEWatcher IsNot Nothing AndAlso
            moBLEWatcher.Status <> Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopped Then

            moBLEWatcher.Stop()
        End If

#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        App.gWagi.SaveAsync()
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed


        uiAddWaga.IsEnabled = True
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

    Private mNewWaga As JednaWaga

    Private Async Sub BTwatch_Received(sender As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,
                                       args As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs)

        DebugOut("New BT device, Mac=" & args.BluetoothAddress.ToHexBytesString & ", locName=" & args.Advertisement.LocalName)

        Dim sMac As String = args.BluetoothAddress.ToHexBytesString
        If msAllDevNames.Contains(sMac) Then Return ' juz to sprawdzalismy
        msAllDevNames = msAllDevNames & "|" & sMac & "|"

        ' test czy to moja waga - do celów debug
        'If Not sMac.StartsWith("68:0C") Then Return

        Dim iProto As Waga_protocol = GetProtocolFromAdv(args.Advertisement)

        If iProto = Waga_protocol.UNKNOWN Then
            DebugOut("nie rozpoznaję jako wagi")
            Return
        End If

        DebugOut("chyba waga")

        Dim oNew As JednaWaga = New JednaWaga
        oNew.uMAC = args.BluetoothAddress
        oNew.sName = oNew.uMAC.ToHexBytesString
        oNew.sTimeAdded = DateTime.Now.ToString("D")    ' long date, z dniem tygodnia i nazwami
        oNew.iTypWagi = iProto

        Try
            App.gWagi.Add(oNew)
            Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
        Catch ex As Exception
        End Try


        ' teraz moze bedzie tu tylko jeden raz - o to chodzi przynajmniej...

        'Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        'oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress)

        'Await DebugBTdeviceAsync(oDev)
    End Sub


    Public Async Sub toDispatch()
        Await uiItems.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchShowItems)
    End Sub

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

        DebugOut("nowa lista, count=" & App.gWagi.Count)
        PokazItemy()

        bInside = False
    End Sub

#End Region


End Class

Public Class KonwersjaMAC
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim uMAC As ULong = CType(value, ULong)
        If uMAC = 0 Then Return ""

        Return uMAC.ToHexBytesString()

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

        ' value is the data from the source object.

        Dim sTmp As String = CType(value, String)
        Dim sRes As String = GetLangString("msgWagiAddedAt", "@")
        Return sRes & sTmp

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class

Public Class KonwersjaTypu
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.Convert

        ' value is the data from the source object.

        Dim iTmp As Integer = CType(value, Integer)
        Dim sRes As String = GetLangString("msgWagiTyp", "Typ")
        Return sRes & " " & iTmp

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
            ByVal targetType As Type, ByVal parameter As Object,
            ByVal language As System.String) As Object _
            Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class