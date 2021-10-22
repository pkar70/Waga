Public NotInheritable Class DownloadHistory
    Inherits Page

    Private moTermo As JedenTermo = Nothing

    Protected Overrides Sub onNavigatedTo(e As NavigationEventArgs)
        Dim uMac = CType(e.Parameter, ULong)

        If App.gmTermo Is Nothing Then Return

        moTermo = App.gmTermo.GetTermo(uMac)

    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)


        ProgRingInit(True, False)

        uiTitle.Text = ""

        If moTermo Is Nothing Then Return

        uiTitle.Text = moTermo.sName

        If Not Await DialogBoxResYNAsync("msgHistoryWantRead") Then
            uiHistoryClear.IsEnabled = True
            Return
        End If

        ' wczytanie historii pełnej, przekonwertowanie jej do listy JedenDisplayRecord
        ' z zapisaniem na dysk, oraz przesunięciem

        Await StartReadingData()


    End Sub

    Private Function RecordsToString() As String
        ' dla email, oraz clipPut
        Dim sOut As String = ""
        For Each oItem As JedenRekord In mlRecList
            sOut = sOut & GetRecordsLogLine(oItem) & vbCrLf
        Next

        Return sOut

    End Function

    Private Async Sub uiSendEmail_Click(sender As Object, e As RoutedEventArgs)

        Dim oMsg As Email.EmailMessage = New Windows.ApplicationModel.Email.EmailMessage()

        oMsg.Subject = "Log from " & moTermo.sName & " thermometer/higrometer"
        oMsg.Body = GetLangString("msgHistoryMailBodyHdr") & " " & DateTime.Now.ToString("g") & vbCrLf & vbCrLf &
            CalculateMinMaxStat() & vbCrLf & vbCrLf &
            RecordsToString()

        ' załączniki działają tylko w default windows mail app
        'Dim oStream = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(oFile)
        'Dim oAttch = New Email.EmailAttachment("rezultat.txt", oStream)
        'oMsg.Attachments.Add(oAttch)

        Await Email.EmailManager.ShowComposeNewEmailAsync(oMsg)

    End Sub

    Private Sub uiSendClip_Click(sender As Object, e As RoutedEventArgs)
        ClipPut(RecordsToString)
        DialogBoxRes("msgHistoryLogClip")
    End Sub
    Private Async Sub uiClearLog_Click(sender As Object, e As RoutedEventArgs)
        If Not Await DialogBoxResYNAsync("msgHistorySureClear") Then Return

        ProgRingShow(True)

        Try
            Dim oChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic =
                Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccd1-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)
            If oChar Is Nothing Then Return

            If Not oChar.CharacteristicProperties.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Write) Then
                DebugOut("Nie ma prawa zapisu!!")
                Await DialogBoxAsync("No permission to write to device - protocol has changed?")
                Return
            End If

            Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter
            oWriter.WriteByte(1)

            Dim oRes As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus =
                Await oChar.WriteValueAsync(oWriter.DetachBuffer)

            If oRes <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Bad write to device")
                Return
            End If

        Finally
            ProgRingShow(False)
        End Try

    End Sub

    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        StopClock()
        Await StopReadingData(True)
        ' Await SaveDownloadedData()
    End Sub

    Private Function CalculateMinMaxStat() As String
        If mlRecList Is Nothing Then Return ""

        Dim dMinT As Double = 100
        Dim dMaxT As Double = -100
        Dim iMinH As Integer = 100
        Dim iMaxH As Integer = -100
        Dim dMinD As DateTimeOffset = DateTimeOffset.Now.AddDays(100)
        Dim dMaxD As DateTimeOffset
        'Dim bDatyByly As Boolean = False

        For Each oRec As JedenRekord In mlRecList
            If dMinT > oRec.dMinT Then dMinT = oRec.dMinT
            If dMaxT < oRec.dMaxT Then dMaxT = oRec.dMaxT
            If iMinH > oRec.iMinH Then iMinH = oRec.iMinH
            If iMaxH < oRec.iMaxH Then iMaxH = oRec.iMaxH

            If dMaxD < oRec.dDate Then dMaxD = oRec.dDate

            ' minimalna data: o ile nie jest to data 1970 :)
            If oRec.dDate.Year > 2000 Then
                If dMinD > oRec.dDate Then dMinD = oRec.dDate
            End If

        Next

        ' jeśli czegoś nie było, to ignorujemy cokolwiek
        If dMinT = 100 Then Return ""
        If dMaxT = -100 Then Return ""
        If iMinH = 100 Then Return ""
        If iMaxH = -100 Then Return ""

        Dim sMsg As String = GetLangString("msgHistoryStatDaty") & " " & dMinD.ToString("g") & " and " & dMaxD.ToString("g") & vbCrLf & vbCrLf &
            GetLangString("msgHistoryStatMinT") & " " & dMinT.ToString("#0.0") & vbCr &
            GetLangString("msgHistoryStatMaxT") & " " & dMaxT.ToString("#0.0") & vbCr &
            GetLangString("msgHistoryStatMinH") & " " & iMinH.ToString & vbCr &
            GetLangString("msgHistoryStatMaxH") & " " & iMaxH.ToString

        Return sMsg
    End Function


#Region "Reading data"

    Private moChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing
    Private moTimer As DispatcherTimer
    'Private mlLogData As List(Of String)
    Private mlRecList As ObservableCollection(Of JedenRekord)

    Private Async Function StartReadingData() As Task

        ProgRingShow(True)

        moChar = Await GetBTcharacterForMijiaAsync(moTermo, "ebe0ccbc-7a0a-4b0c-8a1a-6ff2997da3a6", False, True)   ' "Data Notify"
        If moChar Is Nothing Then
            ProgRingShow(False)
            Return
        End If


        'mlLogData = New List(Of String)
        mlRecList = New ObservableCollection(Of JedenRekord)
        uiItems.ItemsSource = mlRecList

        moTimer = New DispatcherTimer()
        moTimer.Interval = New TimeSpan(0, 0, 1)
        AddHandler moTimer.Tick, AddressOf TimerTick
        moTimer.Start()

        AddHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged

        Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            Await DialogBoxAsync("Cannot subscribe for notifications")
            ProgRingShow(False)
            Return
        End If

    End Function
    Private Async Sub TimerTick(sender As Object, e As Object)

        If moNewRecDate.AddSeconds(4) > DateTimeOffset.Now Then Return
        moTimer.Stop()

        DebugOut("TimerTick: tickłem 2 sekundy po ostatnim pakiecie - wyłączam czytanie")
        Await StopReadingData(False)
        Await SaveDownloadedData()

        ' pokaz na ekranie (powinno byc pokazane juz w trakcie...
        uiItems.ItemsSource = mlRecList

        ProgRingShow(False)

    End Sub


    Private Sub StopClock()
        If moTimer Is Nothing Then Return
        moTimer.Stop()
        RemoveHandler moTimer.Tick, AddressOf TimerTick
    End Sub

    Private Async Function SaveDownloadedData() As Task
        ' zapis na dysk


        ' przygotuj do zapisu (tylko to, czego jeszcze w logu nie ma)

        Dim oFile As Windows.Storage.StorageFile = Await GetSaveFile(moTermo, True)
        If oFile Is Nothing Then Return
        Dim sWholeFile As String = Await oFile.ReadAllTextAsync


        Dim sAddLines As String = ""
        For Each oItem As JedenRekord In mlRecList
            Dim sLine As String = GetRecordsLogLine(oItem)
            If Not sWholeFile.Contains(sLine) Then  ' nie ma sensu zapisywać jak już to mamy (czyli dwa razy tego samego rekordu)
                sAddLines = sAddLines & sLine & vbCrLf
            End If
        Next

        ' i zapisz jednym odwołaniem
        Await oFile.AppendLineAsync(sAddLines)

    End Function

    Private Async Function StopReadingData(bUnloading As Boolean) As Task
        If moChar Is Nothing Then Return

        RemoveHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged
        Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)

        DebugOut("fromDispatchHaveData, status From write.None: " & oResp.ToString)

        moChar = Nothing

        If bUnloading Then Return

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            DialogBox("Cannot de-subscribe for notifications")
        End If

        uiSendEmail.IsEnabled = True
        uiSendClip.IsEnabled = True
        uiHistoryClear.IsEnabled = True

        Dim sMsg As String = CalculateMinMaxStat()
        DialogBox(sMsg)

    End Function

    Private moNewRec As JedenRekord = New JedenRekord
    Private moNewRecDate As DateTimeOffset = DateTimeOffset.Now

    Private Sub Gatt_ValueChanged(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)

        Dim aArray As Byte() = args.CharacteristicValue.ToArray
        moNewRec = MijiaDecodeRecord(aArray)
        moNewRec.uMAC = moTermo.uMAC

        uiTitle.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchHaveData)
    End Sub

    Private Async Sub fromDispatchHaveData()
        DebugOut("fromDispatchHaveData() start")

        moNewRecDate = DateTimeOffset.Now

        ' przesuniecie moNewRec o Delta 
        DeltaRekord(moTermo, moNewRec)

        mlRecList.Add(moNewRec)

    End Sub

#End Region

End Class



'Public Class JedenDisplayRecord
'    Public Property sHigroRange As String = ""
'    Public Property sTempRange As String = ""
'    Public Property dRangeData As DateTimeOffset

'End Class

Public Class KonwersjaRangeData
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.Convert

        Dim dDate As DateTimeOffset = CType(value, DateTimeOffset)
        Return dDate.ToString("yyyy.MM.dd HH:mm")
    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class

