#Region "ZnaneBT"

Public Class ZnaneBT
    Public mItems As ObservableCollection(Of JedenZnany)

    Private _sFileName As String
    Private _bRoam As Boolean

    Private _bModified As Boolean = False

    Public Sub New(Optional bRoam As Boolean = True, Optional sFilename As String = "known.json")
        _bRoam = bRoam
        _sFileName = sFilename
        mItems = New ObservableCollection(Of JedenZnany)
    End Sub

    Private bModified As Boolean = False

    Private Function GetFolder() As Windows.Storage.StorageFolder
        If _bRoam Then Return Windows.Storage.ApplicationData.Current.RoamingFolder
        Return Windows.Storage.ApplicationData.Current.LocalFolder
    End Function

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If Count() > 0 AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await GetFolder.ReadAllTextFromFileAsync(_sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems.Clear()
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, mItems.GetType)    ' (ObservableCollection(Of JedenZnany))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mItems.Count < 1 Then Return False

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await GetFolder.WriteAllTextToFileAsync(_sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JedenZnany) As Boolean
        If oNew Is Nothing Then Return False

        ' nie ma teraz takiej mozliwosci, ale na wszelki wypadek gdyby cos przerabiac
        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenZnany)
        End If

        For Each oItem As JedenZnany In mItems
            If oItem.uMAC = oNew.uMAC Then Return False ' nie umiem updatować (na razie)
        Next

        bModified = True

        mItems.Add(oNew)

        Return True
    End Function

    'Public Function IsLoaded() As Boolean
    '    If mItems Is Nothing Then Return False
    '    Return True
    'End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetTermo(uMac As ULong) As JedenZnany
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenZnany In mItems
            If oItem.uMAC = uMac Then Return oItem
        Next

        Return Nothing
    End Function

End Class


Public Class JedenZnany
        Public Property uMAC As ULong
    Public Property sName As String
    Public Sub New(pMAC As ULong, pName As String)
        uMAC = pMAC
        sName = pName
    End Sub
End Class


#End Region


#Region "nowe BT"



Public Class JedenBT
    Public Property uMAC As ULong
    Public Property sName As String
    Public Property oAdvert As Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisement
    Public Property dTimeStamp As DateTimeOffset
End Class

Public Class NoweBT
    Public mItems As ObservableCollection(Of JedenBT)

    Private _sFileName As String
    Private _bRoam As Boolean

    Private _bModified As Boolean = False

    Public Sub New(Optional bRoam As Boolean = True, Optional sFilename As String = "new.json")
        _bRoam = bRoam
        _sFileName = sFilename
        mItems = New ObservableCollection(Of JedenBT)
    End Sub

    Private bModified As Boolean = False

#If False Then
    Private Function GetFolder() As Windows.Storage.StorageFolder
        If _bRoam Then Return Windows.Storage.ApplicationData.Current.RoamingFolder
        Return Windows.Storage.ApplicationData.Current.LocalFolder
    End Function

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await GetFolder.ReadAllTextFromFileAsync(_sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems.Clear()
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, mItems.GetType)    ' (ObservableCollection(Of JedenZnany))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mItems.Count < 1 Then Return False

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await GetFolder.WriteAllTextToFileAsync(_sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function
#End If

    Public Function Add(oNew As JedenBT) As Boolean
        If oNew Is Nothing Then Return False

        ' nie ma teraz takiej mozliwosci, ale na wszelki wypadek gdyby cos przerabiac
        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenBT)
        End If

        For Each oItem As JedenBT In mItems
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

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetTermo(uMac As ULong) As JedenBT
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenBT In mItems
            If oItem.uMAC = uMac Then Return oItem
        Next

        Return Nothing
    End Function

End Class
#End Region


#If False Then
Public Class MojaLista
    Public mItems As ObservableCollection(Of Object)

    Private _sFileName As String
    Private _bRoam As Boolean
    Private _oType As Type

    Private _bModified As Boolean = False

    Public Sub New(oType As Type, Optional bRoam As Boolean = True, Optional sFilename As String = "items.json")
        _oType = oType
        _bRoam = bRoam
        _sFileName = sFilename
        mItems = New ObservableCollection(Of oType)
    End Sub


    Private Function GetFolder() As Windows.Storage.StorageFolder
        If bRoam Then Return Windows.Storage.ApplicationData.Current.RoamingFolder
        Return Windows.Storage.ApplicationData.Current.LocalFolder
    End Function

    Public Async Function LoadAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If IsLoaded() AndAlso Not bForce Then Return True

        bModified = False

        Dim sTxt As String = Await GetFolder.ReadAllTextFromFileAsync(sFileName)
        If sTxt Is Nothing OrElse sTxt.Length < 5 Then
            mItems = New ObservableCollection(Of JedenZnany)
            Return False
        End If

        mItems = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, mItems.GetType)    ' (ObservableCollection(Of JedenZnany))

        Return True

    End Function

    Public Async Function SaveAsync(Optional bForce As Boolean = False) As Task(Of Boolean)
        If mItems.Count < 1 Then Return False

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(mItems, Newtonsoft.Json.Formatting.Indented)

        Await GetFolder.WriteAllTextToFileAsync(sFileName, sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        bModified = False

        Return True

    End Function

    Public Function Add(oNew As JedenZnany) As Boolean
        If oNew Is Nothing Then Return False

        If mItems Is Nothing Then
            mItems = New ObservableCollection(Of JedenZnany)
        End If

        For Each oItem As JedenZnany In mItems
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

    Public Function GetList() As ObservableCollection(Of JedenZnany)
        Return mItems
    End Function

    Public Function Count() As Integer
        If mItems Is Nothing Then Return -1
        Return mItems.Count
    End Function

    Public Function GetTermo(uMac As ULong) As JedenZnany
        If Count() < 1 Then Return Nothing

        For Each oItem As JedenZnany In mItems
            If oItem.uMAC = uMac Then Return oItem
        Next

        Return Nothing
    End Function

End Class

#End If

