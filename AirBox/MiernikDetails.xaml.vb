Public NotInheritable Class MiernikDetails
    Inherits Page

    Private moTermo As JedenMiernik = Nothing

    Protected Overrides Sub onNavigatedTo(e As NavigationEventArgs)
        Dim uMac = CType(e.Parameter, ULong)

        If App.gmTermo Is Nothing Then Return

        moTermo = App.gmTermo.GetTermo(uMac)

    End Sub
    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs)

        uiName.Text = ""
        If moTermo Is Nothing Then Return
        uiName.Text = moTermo.sName

        uiMac.Text = "MAC: " & moTermo.uMAC.ToHexBytesString

        uiAdded.Text = "Added @" & moTermo.sTimeAdded

        uiDeltaTemp.Value = moTermo.dDeltaTemp
        uiDeltaHCOH.Value = moTermo.dDeltaHCHO
        uiDeltaCO2.Value = moTermo.dDeltaCO2
        uiDeltaTCOV.Value = moTermo.dDeltaTVOC

        uiSave.IsEnabled = False  ' podczas ustawiania powyżej zapewne się zmieniło na true :)
    End Sub
    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)

        If Not uiSave.IsEnabled Then Return

        If Not Await DialogBoxYNAsync("Czy cancel zmian?") Then Return
        uiSave_Click(Nothing, Nothing)
    End Sub

    Private Sub uiSliderChanged(sender As Object, e As RangeBaseValueChangedEventArgs)
        uiSave.IsEnabled = True
    End Sub

    Private Async Sub uiSave_Click(sender As Object, e As RoutedEventArgs)
        moTermo.dDeltaCO2 = uiDeltaCO2.Value
        moTermo.dDeltaHCHO = uiDeltaHCOH.Value
        moTermo.dDeltaTemp = uiDeltaTemp.Value
        moTermo.dDeltaTVOC = uiDeltaTCOV.Value

        Await App.gmTermo.SaveAsync ' jakby jakies zmiany były dokonane, to zapisz - raz, hurtem

        Me.Frame.GoBack()
    End Sub
End Class
