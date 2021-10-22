Public Class JedenMiernik
    Public Property uMAC As ULong
    'Public Property sMac As String
    Public Property sName As String
    Public Property sTimeAdded As String = DateTime.Now.ToString("yyyy.MM.dd")
    Public Property bEnabled As Boolean = True
    Public Property dDeltaTemp As Double = 0
    Public Property dDeltaCO2 As Double = 0
    Public Property dDeltaHCHO As Double = 0
    Public Property dDeltaTVOC As Double = 0

    Public Property dLastTimeStamp As DateTime
    Public Property dLastTemp As Double = -100  ' -100 oznacza brak danych
    Public Property dLastCO2 As Double = -100  ' -100 oznacza brak danych
    Public Property dLastTVOC As Double = -100  ' -100 oznacza brak danych
    Public Property dLastHCHO As Double = -100  ' -100 oznacza brak danych

    Public Property bCannotAccess As Boolean = False

    Public Property iAlertLastCO2 As Integer
    Public Property iAlertLastHCHO As Integer
    Public Property iAlertLastTVOC As Integer

    <Newtonsoft.Json.JsonIgnore>
    Public Property oGATTsvc As Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService = Nothing

End Class


Public Class JedenPomiar
    Public Property uMAC As ULong
    Public Property dTimeStamp As DateTime
    Public Property dCO2 As Double = -1
    Public Property dHCHO As Double = -1
    Public Property dTVOC As Double = -1
    Public Property dTemp As Double = -1

    Public Overrides Function ToString() As String
        Return "JedenPomiar: uMAC" & uMAC.ToHexBytesString & ", time=" & dTimeStamp.ToString("yyyy.MM.dd HH:mm:ss") &
        ", temp=" & dTemp & ", CO2=" & dCO2 & ", dHCHO=" & dHCHO.ToString("#0.000") & ", TVOC=" & dTVOC.ToString("#0.000")
    End Function

End Class



Public Class TermoList
    Private mItems As ObservableCollection(Of JedenMiernik) = Nothing
    Private bModified As Boolean = False
    Private Const sFileName As String = "devices.json"

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems = New ObservableCollection(Of JedenMiernik)
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of JedenMiernik)))

        Return True

    End Function

    Public Async Function SaveAsync() As Task(Of Boolean)
        If mItems.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JedenMiernik) As Boolean
        If oNew Is Nothing Then Return False

        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenMiernik)
        End If

        For Each oItem As JedenMiernik In mItems
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

    Public Function GetList() As ObservableCollection(Of JedenMiernik)
        Return mItems
    End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetTermo(uMac As ULong) As JedenMiernik
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenMiernik In mItems
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
