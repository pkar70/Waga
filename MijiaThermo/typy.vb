Public Class JedenTermo
    Public Property uMAC As ULong
    'Public Property sMac As String
    Public Property sName As String
    Public Property sTimeAdded As String = DateTime.Now.ToString("yyyy.MM.dd")
    Public Property bEnabled As Boolean = True
    Public Property dDeltaTemp As Double = 0
    Public Property iDeltaHigro As Integer = 0

    Public Property dLastTimeStamp As DateTime
    Public Property dLastTemp As Double = -100  ' -100 oznacza brak danych
    Public Property dLastAppTemp As Double = -100
    Public Property iLastHigro As Integer = -100
    Public Property iLastBattMV As Integer = -100
    Public Property sHigroRange As String = ""

    Public Property sTempRange As String = ""

    Public Property dRangeData As DateTimeOffset

    ' cztery dane "mordki" - potrzebne na później, przy Toast
    Public Property dMinT As Double = -100
    Public Property dMaxT As Double = -100

    Public Property iMinH As Integer = -100
    Public Property iMaxH As Integer = -100

    Public Property bYearlyLog As Boolean = False  ' log miesięczny bądź roczny

    ' apropo alertów
    Public Property bAlertInclude As Boolean = False
    Public Property bAlertIncludeTemp As Boolean = False
    Public Property bAlertIncludeHigro As Boolean = False
    Public Property bAlertIncludeTApp As Boolean = False

    Public Property bAlertAlarmTLow As Boolean = False
    Public Property bAlertWarnTLow As Boolean = False
    Public Property bAlertWarnTHigh As Boolean = False
    Public Property bAlertAlarmTHigh As Boolean = False
    Public Property dAlertAlarmTLow As Double = 15
    Public Property dAlertWarnTLow As Double = 18
    Public Property dAlertWarnTHigh As Double = 28
    Public Property dAlertAlarmTHigh As Double = 36

    Public Property bAlertAlarmTALow As Boolean = False
    Public Property bAlertWarnTALow As Boolean = False
    Public Property bAlertWarnTAHigh As Boolean = False
    Public Property bAlertAlarmTAHigh As Boolean = False
    Public Property dAlertAlarmTALow As Double = 15
    Public Property dAlertWarnTALow As Double = 18
    Public Property dAlertWarnTAHigh As Double = 28
    Public Property dAlertAlarmTAHigh As Double = 36

    Public Property bAlertAlarmHLow As Boolean = False
    Public Property bAlertWarnHLow As Boolean = False
    Public Property bAlertWarnHHigh As Boolean = False
    Public Property bAlertAlarmHHigh As Boolean = False
    Public Property dAlertAlarmHLow As Double = 40
    Public Property dAlertWarnHLow As Double = 45
    Public Property dAlertWarnHHigh As Double = 60
    Public Property dAlertAlarmHHigh As Double = 65

    Public Property iAlertLastT As Integer = 0
    Public Property iAlertLastH As Integer = 0
    Public Property iAlertLastTA As Integer = 0
    Public Property iPrevBattMV As Integer = 3000
    Public Property bAlertIncludeRange As Boolean = False

    <Newtonsoft.Json.JsonIgnore>
    Public Property bToastInAccessible As Boolean = True

    '<Newtonsoft.Json.JsonIgnore>
    'Public Property sAllWarn As String = ""

    <Newtonsoft.Json.JsonIgnore>
    Public Property oGATTsvc As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService = Nothing

End Class


Public Class JedenPomiar
    Public Property uMAC As ULong
    Public Property dTimeStamp As DateTime
    Public Property dTemp As Double
    Public Property iHigro As Integer
    Public Property iBattMV As Integer

End Class

Public Class JedenRekord
    Public Property uMAC As ULong
    Public Property lInd As Long
    Public Property dDate As DateTimeOffset
    Public Property dMaxT As Double
    Public Property dMinT As Double
    Public Property iMaxH As Integer
    Public Property iMinH As Integer
    Public Property sRangeT As String = ""
    Public Property sRangeH As String = ""


End Class


Public Class TermoList
    Private mItems As ObservableCollection(Of JedenTermo) = Nothing
    Private bModified As Boolean = False
    Private Const sFileName As String = "termo.json"

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems = New ObservableCollection(Of JedenTermo)
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of JedenTermo)))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mItems.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JedenTermo) As Boolean
        If oNew Is Nothing Then Return False

        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenTermo)
        End If

        For Each oItem As JedenTermo In mItems
            If oItem.uMAC = oNew.uMAC Then Return False ' nie umiem updatować (na razie)
        Next

        bModified = True

        mItems.Add(oNew)

        Return True
    End Function

    Public Function IsLoaded() As Boolean
        If mItems Is Nothing Then Return False
        Return True
    End Function

    Public Function GetList() As ObservableCollection(Of JedenTermo)
        Return mItems
    End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetTermo(uMac As ULong) As JedenTermo
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenTermo In mItems
            If oItem.uMAC = uMac Then Return oItem
        Next

        Return Nothing
    End Function

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
