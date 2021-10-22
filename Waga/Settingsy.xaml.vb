' The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class Settingsy
    Inherits Page

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        GetAppVers(Nothing)

        GetSettingsBool(uiSettSaveRoam, True)
        GetSettingsBool(uiSettSaveDataLog, True)
        GetSettingsBool(uiSettSaveImportDataLog)
        GetSettingsBool(uiSettSaveDetailedDataLog)

    End Sub

    Private Async Sub uiSettSave_Click(sender As Object, e As RoutedEventArgs)

        If Not uiSettSaveDataLog.IsOn AndAlso Not uiSettSaveRoam.IsOn Then
            If Not Await DialogBoxResYNAsync("msgSettNoSaveAtAll") Then Return
        End If

        SetSettingsBool(uiSettSaveRoam)
        SetSettingsBool(uiSettSaveDataLog)
        GetSettingsBool(uiSettSaveDetailedDataLog)
        SetSettingsBool(uiSettSaveImportDataLog)
        Me.Frame.GoBack()
    End Sub

End Class
