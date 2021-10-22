Public Class JednaWaga
    Public Property uMAC As ULong
    Public Property sName As String
    Public Property sTimeAdded As String = DateTime.Now.ToString("yyyy.MM.dd")
    Public Property dTara As Double
    Public Property iTypWagi As Integer
    Public Property bUseBio As Boolean = True

End Class

Public Class JednaOsoba
    Public Property sName As String
    Public Property id As Integer
    Public Property iCurrentWzrost As Integer
    Public Property dDataUrodz As DateTimeOffset
    Public Property bWoman As Boolean ' OKOK to bierze, w USA ma znaczenie do tabelki obesity
    ' Public Property lWzrosty As List(Of JedenWzrost)
End Class

'Public Class JedenWzrost
'    Public Property iWzrost As Integer
'    Public Property dDataOd As DateTime
'End Class

'Public Class JedenPomiar
'    Public Property iPersonId As Integer
'    Public Property sWagaMAC As String
'    Public Property dTimeStamp As DateTime
'    Public Property iWzrost As Integer ' wzrost z momentu przypisania osobie (jak osoba rośnie)
'    Public Property dTemp As Double ' na wadze pokazywana jest też temperatura
'    Public Property dBMI As Double  ' albo wyliczac za kazdym razem?
'End Class

Public Class WagiList
    Private mWagi As ObservableCollection(Of JednaWaga) = Nothing
    Private bModified As Boolean = False
    Private Const sFileName As String = "wagi.json"

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mWagi = New ObservableCollection(Of JednaWaga)
            Return False
        End If

        mWagi = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of JednaWaga)))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mWagi Is Nothing Then Return False
        If mWagi.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mWagi, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JednaWaga) As Boolean
        If oNew Is Nothing Then Return False

        If mWagi Is Nothing Then
            mWagi = New ObservableCollection(Of JednaWaga)
        End If

        For Each oItem As JednaWaga In mWagi
            If oItem.uMAC = oNew.uMAC Then Return False ' nie umiem updatować (na razie)
        Next

        bModified = True

        mWagi.Add(oNew)
        Return True
    End Function

    Public Function IsLoaded() As Boolean
        If mWagi Is Nothing Then Return False
        Return True
    End Function

    Public Function GetList() As ObservableCollection(Of JednaWaga)
        Return mWagi
    End Function

    Public Function Count() As Integer
        If mWagi Is Nothing Then Return -1
        Return mWagi.Count
    End Function

End Class


Public Class LudzieList
    Private mItems As ObservableCollection(Of JednaOsoba) = Nothing
    Private bModified As Boolean = False
    Private Const sFileName As String = "ludzie.json"

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems = New ObservableCollection(Of JednaOsoba)
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of JednaOsoba)))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mItems Is Nothing Then Return False
        If mItems.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Private Function Add(oNew As JednaOsoba) As Boolean
        If oNew Is Nothing Then Return False

        Dim iMax As Integer = 0

        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JednaOsoba)
        Else
            For Each oItem In mItems
                iMax = Math.Max(iMax, oItem.id)
            Next
            iMax += 1
        End If

        oNew.id = iMax
        bModified = True

        mItems.Add(oNew)
        Return True
    End Function

    Public Function Add(sName As String) As Boolean
        If String.IsNullOrEmpty(sName) Then Return False

        Dim oNew As JednaOsoba = New JednaOsoba
        oNew.sName = sName
        oNew.dDataUrodz = DateTimeOffset.Now.AddYears(-30)
        oNew.iCurrentWzrost = 100

        Return Add(oNew)
    End Function
    Public Function IsLoaded() As Boolean
        If mItems Is Nothing Then Return False
        Return True
    End Function

    Public Function GetList() As ObservableCollection(Of JednaOsoba)
        Return mItems
    End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

End Class