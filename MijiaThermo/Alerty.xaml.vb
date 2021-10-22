Public NotInheritable Class Alerty
    Inherits Page

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)

        ProgRingInit(True, False)
        ProgRingShow(True)
        Await App.gmTermo.LoadAsync()

        uiItems.ItemsSource = From c In App.gmTermo.GetList Where c.bEnabled Order By c.sName

        GetSettingsBool(uiAlertsOnOff)
        SelectComboInterval

        ProgRingShow(False)

    End Sub

    Private Async Sub uiAlertsOnOff_Checked(sender As Object, e As RoutedEventArgs) Handles uiAlertsOnOff.Checked
        SetSettingsBool(uiAlertsOnOff)

        If GetSettingsBool("uiAlertsOnOff") Then
            If Not IsTriggersRegistered("MijiaThermoTimer") Then
                If Not Await CanRegisterTriggersAsync() Then
                    DialogBoxRes("msgFailNoBackground")
                Else
                    RegisterTimerTrigger("MijiaThermoTimer", 15)
                End If
            End If
        Else
            UnregisterTriggers("MijiaThermoTimer")
        End If
    End Sub

    Private mbInAutoSelect As Boolean = False
    Private Sub SelectComboInterval()

        mbInAutoSelect = True

        Dim sTmp As String = GetSettingsString("uiAlertsTimer", "30 min")
        For Each oItem As ComboBoxItem In uiAlertsTimer.Items
            If TryCast(oItem.Content, String) = sTmp Then
                uiAlertsTimer.SelectedItem = oItem
                'oItem.IsSelected = True
                Exit For
            End If
        Next

        mbInAutoSelect = False

    End Sub

    Private Sub uiAlertsTimer_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles uiAlertsTimer.SelectionChanged

        If mbInAutoSelect Then Return ' jesli sam zmieniam, to się tym nie zajmujemy

        If e?.AddedItems Is Nothing Then Return
        If e.AddedItems.Count < 1 Then Return
        Dim oItem As ComboBoxItem = e.AddedItems.ElementAt(0)

        Dim sTmp As String = TryCast(oItem.Content, String)
        SetSettingsString("uiAlertsTimer", sTmp)

    End Sub

    Private Async Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        Await App.gmTermo.SaveAsync() ' i tak nie mam jak sprawdzić czy były zmiany
    End Sub

    Private Sub uiAppTemp_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(Calculator))
    End Sub
End Class


Public Class KonwersjaVisibilityFromBool
    Implements IValueConverter

    ' Define the Convert method to change a DateTime object to
    ' a month string.
    Public Function Convert(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.Convert

        Dim dTemp As Boolean = CType(value, Boolean)

        If dTemp Then
            Return Visibility.Visible
        Else
            Return Visibility.Collapsed
        End If

    End Function

    ' ConvertBack is not implemented for a OneWay binding.
    Public Function ConvertBack(ByVal value As Object,
        ByVal targetType As Type, ByVal parameter As Object,
        ByVal language As System.String) As Object _
        Implements IValueConverter.ConvertBack

        Throw New NotImplementedException

    End Function
End Class
