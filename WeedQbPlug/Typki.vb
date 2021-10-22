Public Class JedenSocket
    Public Property sName As String
    Public Property sAddr As String
    'Public Property uMAC As ULong
    Public Property sAddedAt As String
    Public Property bIsOn As Boolean

    '<Newtonsoft.Json.JsonIgnore>
    'Public Property sId As String

    Public Overrides Function ToString() As String
        Return "JedenSocket '" & sName & "', MAC " & sAddr & " added @" & sAddedAt & ", IsOn=" & bIsOn
    End Function

End Class

Public Class SocketsList
    Private mItems As ObservableCollection(Of JedenSocket) = Nothing
    Private bModified As Boolean = False
    Private Const sFileName As String = "devices.json"

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.RoamingFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems = New ObservableCollection(Of JedenSocket)
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of JedenSocket)))

        Return True

    End Function

    Public Async Function SaveAsync(bForce As Boolean) As Task(Of Boolean)
        If Not bModified AndAlso Not bForce Then Return False
        If mItems.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.RoamingFolder
        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await oFold.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JedenSocket) As Boolean
        If oNew Is Nothing Then Return False

        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenSocket)
        End If

        For Each oItem As JedenSocket In mItems
            If oItem.sAddr.ToLower = oNew.sAddr.ToLower Then Return False ' nie umiem updatować (na razie)
        Next

        bModified = True

        mItems.Add(oNew)

        Return True
    End Function

    Public Function IsLoaded() As Boolean
        If mItems Is Nothing Then Return False
        Return True
    End Function

    Public Function GetList() As ObservableCollection(Of JedenSocket)
        Return mItems
    End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetSocket(sAddr As String) As JedenSocket
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenSocket In mItems
            If oItem.sAddr.ToLower = sAddr.ToLower Then Return oItem
        Next

        Return Nothing
    End Function

    Public Function Delete(sAddr As String) As Boolean
        If Count() < 1 Then Return False

        For Each oItem As JedenSocket In mItems
            If oItem.sAddr.ToLower = sAddr.ToLower Then
                mItems.Remove(oItem)
                bModified = True
                Return True
            End If
        Next

        Return False
    End Function

    Public Sub MakeDirty()
        bModified = True
    End Sub

End Class

