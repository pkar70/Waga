
Public NotInheritable Class Ludziki
    Inherits Page

    ' * lista zdefiniowanych osób
    ' * do każdego definiowanie wzrostu
    ' * oglądanie historii pomiarów osoby
    ' * wysłanie emailem historii
    ' * pomiar ma mieć zapisany device z którego korzystał! (MAC, ewentualnie nazwa - nazwa może ulegać zmianie...
    ' * wysłanie emailem (albo do clip?) listy osób
    ' * lista osób ROAM/local
    ' * pomiary pamiętane LOCAL

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        If Not App.gLudzie.IsLoaded Then Await App.gLudzie.LoadAsync()
        PokazItemy()
    End Sub

    Private Async Sub uiSave_Click(sender As Object, e As RoutedEventArgs)
        Await App.gLudzie.SaveAsync(True)
        Me.Frame.GoBack()
    End Sub
    Private Async Sub uiAddUser_Click(sender As Object, e As RoutedEventArgs)
        Dim sNewUser As String = Await DialogBoxInputAsync("msgEnterName")
        If String.IsNullOrEmpty(sNewUser) Then Return

        sNewUser = sNewUser.Substring(0, 1).ToUpper & sNewUser.Substring(1)
        If Not App.gLudzie.Add(sNewUser) Then Return

        PokazItemy()
    End Sub

    Private Async Sub uiExport_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As JednaOsoba = oMFI.DataContext


        ' open plik
        Dim oFold As Windows.Storage.StorageFolder = Await GetLogFolderRootAsync()
        If oFold Is Nothing Then
            DialogBox("FAIL: cannot open LogFolderRoot")
            Return
        End If

        Dim oFile As Windows.Storage.StorageFile = Await oFold.CreateFileAsync("all.csv", Windows.Storage.CreationCollisionOption.OpenIfExists)
        If oFile Is Nothing Then
            DialogBox("FAIL: cannot open data file")
            Return
        End If


        ' tu zbieramy wszystko co trzeba wyeksportowac
        Dim sOutput As String = ""

        ' ReadLinesAsync na empty file daje null
        Dim oProp As Windows.Storage.FileProperties.BasicProperties = Await oFile.GetBasicPropertiesAsync
        If oProp IsNot Nothing Then
            If oProp.Size < 10 Then
                DialogBoxRes("msgNoDataToExportEmpty")
                Return
            End If
        End If

        ' filtrowanie
        Dim aLines As List(Of String) = Await Windows.Storage.FileIO.ReadLinesAsync(oFile)
        If aLines.Count < 2 Then
            DialogBoxRes("msgNoDataToExportEmpty")
            Return
        End If

        For iLoop As Integer = 1 To aLines.Count - 1
            'sLine = sLine & opor & sSep
            'sLine = sLine & sUserName & sSep
            If aLines(iLoop).Contains(oItem.sName & vbTab) Then sOutput = sOutput & aLines(iLoop) & vbCrLf
        Next

        If sOutput = "" Then
            DialogBoxRes("msgNoDataToExportUser")
            Return
        End If

        ' koniec - eksportujemy

        ClipPut(aLines(0) & vbCrLf & sOutput)
        DialogBoxRes("msgDataInClip")


    End Sub


    Private Sub PokazItemy()
        If App.gLudzie.Count < 1 Then
            uiItems.ItemsSource = Nothing
        Else
            uiItems.ItemsSource = From c In App.gLudzie.GetList() Order By c.sName
        End If

    End Sub

    Private Sub uiItems_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles uiItems.SelectionChanged
        If e.AddedItems.Count < 1 Then Return

        Dim oOs As JednaOsoba = e.AddedItems.ElementAt(0)
        If oOs Is Nothing Then Return

        SetSettingsInt("currPerson", oOs.id)
    End Sub
End Class
