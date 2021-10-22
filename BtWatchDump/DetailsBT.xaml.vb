' Navigate(typ, uMAC)
' ma pokazac dane z urzadzenia, czyli podłączyć sie do niego i zdumpowac

' mozliwosc dodania do KNOWN (z nazwą)

Public NotInheritable Class DetailsBT
    Inherits Page

    Private mItem As JedenBT = Nothing

    Protected Overrides Sub onNavigatedTo(e As NavigationEventArgs)
        Dim uMAC = CType(e.Parameter, ULong)
        DumpCurrMethod(uMAC.ToHexBytesString)
        For Each oItem As JedenBT In App.glNowe.mItems
            If oItem.uMAC = uMAC Then
                mItem = oItem
                Return
            End If
        Next

        DumpMessage("ERROR: nie znalazłem muMAC!")
    End Sub


    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        DumpCurrMethod()
        ProgRingInit(True, False)

        If mItem Is Nothing Then
            DialogBox("Cos nie tak, mItem nie ustawiony?")
            Return
        End If

        uiHeader.Text = "Dumping " & mItem.uMAC.ToHexBytesString

        Dim sTxt As String = mItem.oAdvert.ToDebugString & vbCrLf
        uiDump.Text = sTxt

        If Not Await DialogBoxYNAsync("Podłączyć się?") Then Return

        Dim oDev As Windows.Devices.Bluetooth.BluetoothLEDevice
        ProgRingShow(True)
        oDev = Await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(mItem.uMAC)
        ProgRingShow(False)

        If oDev Is Nothing Then
            DebugOut("")
            uiDump.Text = sTxt & "oDev null, cannot continue"
            Return
        End If

        ProgRingShow(True)
        uiDump.Text = sTxt & Await oDev.ToDebusStringAsync
        ProgRingShow(False)

    End Sub
End Class
