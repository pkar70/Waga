
Public NotInheritable Class ListaZnanych
    Inherits Page

    Private Sub uiDelZnane_Click(sender As Object, e As RoutedEventArgs)
        DialogBox("Jeszcze nie umiem, zmień sobie w pliku")
    End Sub

    Private Async Sub uiSaveOk_Click(sender As Object, e As RoutedEventArgs)
        Await App.glZnane.SaveAsync(True)
        Me.Frame.GoBack()
    End Sub

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        uiItems.ItemsSource = App.glZnane.mItems
    End Sub

End Class

