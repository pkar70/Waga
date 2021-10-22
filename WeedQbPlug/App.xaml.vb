''' <summary>
''' Provides application-specific behavior to supplement the default Application class.
''' </summary>
NotInheritable Class App
    Inherits Application

#Region "wizard"
    ''' <summary>
    ''' Invoked when the application is launched normally by the end user.  Other entry points
    ''' will be used when the application is launched to open a specific file, to display
    ''' search results, and so forth.
    ''' </summary>
    ''' <param name="e">Details about the launch request and process.</param>
    Protected Overrides Sub OnLaunched(e As Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)
        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        ' Do not repeat app initialization when the Window already has content,
        ' just ensure that the window is active

        If rootFrame Is Nothing Then
            ' Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = New Frame()

            AddHandler rootFrame.NavigationFailed, AddressOf OnNavigationFailed

            ' PKAR added wedle https://stackoverflow.com/questions/39262926/uwp-hardware-back-press-work-correctly-in-mobile-but-error-with-pc
            AddHandler rootFrame.Navigated, AddressOf OnNavigatedAddBackButton
            AddHandler Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested, AddressOf OnBackButtonPressed


            'If e.PreviousExecutionState = ApplicationExecutionState.Terminated Then
            '    ' TODO: Load state from previously suspended application
            'End If
            ' Place the frame in the current Window
            Window.Current.Content = rootFrame
        End If

        ' nothing: wskok z OnActivated cmdline
        If e Is Nothing OrElse e.PrelaunchActivated = False Then
            If rootFrame.Content Is Nothing Then
                ' When the navigation stack isn't restored navigate to the first page,
                ' configuring the new page by passing required information as a navigation
                ' parameter
                If e Is Nothing Then
                    rootFrame.Navigate(GetType(MainPage))
                Else
                    rootFrame.Navigate(GetType(MainPage), e.Arguments)
                End If
            End If

            ' Ensure the current window is active
            Window.Current.Activate()
        End If
    End Sub

    ''' <summary>
    ''' Invoked when Navigation to a certain page fails
    ''' </summary>
    ''' <param name="sender">The Frame which failed navigation</param>
    ''' <param name="e">Details about the navigation failure</param>
    Private Sub OnNavigationFailed(sender As Object, e As NavigationFailedEventArgs)
        Throw New Exception("Failed to load Page " + e.SourcePageType.FullName)
    End Sub

    ''' <summary>
    ''' Invoked when application execution is being suspended.  Application state is saved
    ''' without knowing whether the application will be terminated or resumed with the contents
    ''' of memory still intact.
    ''' </summary>
    ''' <param name="sender">The source of the suspend request.</param>
    ''' <param name="e">Details about the suspend request.</param>
    Private Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        ' TODO: Save application state and stop any background activity
        deferral.Complete()
    End Sub
#End Region

    Public Shared gmGniazdka As SocketsList = New SocketsList
    Private Shared gbInCommand As Boolean = False

#Region "sending command"

    Private Shared Async Function SendBufToSocket(oSocket As Windows.Networking.Sockets.StreamSocket, oService As Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService, oBuff As Windows.Storage.Streams.IBuffer) As Task(Of Boolean)
        Try
            Await oSocket.ConnectAsync(
                oService.ConnectionHostName,
                oService.ConnectionServiceName,
                Windows.Networking.Sockets.SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication)
        Catch ex As Exception
            CrashMessageAdd("SendAsPodejscie3, ConnectAsync", ex, True)
            Return False
        End Try

        DebugOut("SendAsPodejscie3, Writing...")
        Try
            Await oSocket.OutputStream.WriteAsync(oBuff)
        Catch ex As Exception
            CrashMessageAdd("SendAsPodejscie3, WriteAsync", ex, True)
            Return False
        End Try

        Return True
    End Function

    Private Shared Async Function SendBufToSvcRes(oSvcRes As Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceServicesResult, oBuff As Windows.Storage.Streams.IBuffer) As Task(Of Boolean)
        If oSvcRes Is Nothing Then
            DebugOut("SendAsPodejscie3, oSvcRes NULL")
            Return False
        End If

        If oSvcRes.Services Is Nothing Then
            DebugOut("SendAsPodejscie3, oSvcRes.Services NULL")
            Return False
        End If

        If oSvcRes.Services.Count < 1 Then
            DebugOut("SendAsPodejscie3, GetRfcommServicesForIdAsync.Count < 1")
            Return False
        End If

        Dim oService As Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService = oSvcRes.Services.ElementAt(0)
        If oService Is Nothing Then
            DebugOut("SendAsPodejscie3, oSvcRes.Services.ElementAt(0) is NULL")
            Return False
        End If

        Dim oSocket As Windows.Networking.Sockets.StreamSocket = New Windows.Networking.Sockets.StreamSocket
        Dim bRet As Boolean = Await SendBufToSocket(oSocket, oService, oBuff)
        oSocket.Dispose()

        oService.Dispose()

        Return bRet

    End Function

    Private Shared Async Function SendBufToDevice(oDevice As Windows.Devices.Bluetooth.BluetoothDevice, oBuff As Windows.Storage.Streams.IBuffer) As Task(Of Boolean)
        If oDevice Is Nothing Then
            DebugOut("SendAsPodejscie3, BluetoothDevice.FromBluetoothAddressAsync() returns NULL")
            Return False
        End If

        If Not oDevice.DeviceInformation.Pairing.IsPaired Then
            DebugOut("SendAsPodejscie3, UnPaired")
            Try
                Await oDevice.DeviceInformation.Pairing.PairAsync()
            Catch ex As Exception
                MakeToast("pairing failed!")
                CrashMessageAdd("SendAsPodejscie3, pairing", ex, True)
                Return False
            End Try
        End If

        Dim oSvcRes As Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceServicesResult = Nothing
        Try
            oSvcRes = Await oDevice.GetRfcommServicesForIdAsync(Windows.Devices.Bluetooth.Rfcomm.RfcommServiceId.SerialPort)
        Catch ex As Exception
            DebugOut("SendAsPodejscie3, GetRfcommServicesForIdAsync exception")
            oSvcRes = Nothing
        End Try

        Return Await SendBufToSvcRes(oSvcRes, oBuff)

    End Function

    Private Shared Async Function SendAsPodejscie3(sMAC As String, oBuff As Windows.Storage.Streams.IBuffer, bMsg As Boolean) As Task(Of Boolean)
        DebugOut("SendAsPodejscie3(sMac=" & sMAC)

        Dim uMAC As ULong = MacStringToULong(sMAC)
        Dim oDevice As Windows.Devices.Bluetooth.BluetoothDevice = Nothing

        ' kolejne TRY jako zabezpieczenie przed CRASH przy jednoczesnym wywołaniu z UI i z Timer
        Try
            oDevice = Await Windows.Devices.Bluetooth.BluetoothDevice.FromBluetoothAddressAsync(uMAC) ' .FromIdAsync(sId)
        Catch ex As Exception
            CrashMessageAdd("SendAsPodejscie3, BluetoothDevice.FromBluetoothAddressAsync()", ex, True)
            oDevice = Nothing
        End Try

        Dim bRet As Boolean = Await SendBufToDevice(oDevice, oBuff)

        oDevice.Dispose()

        Return bRet

    End Function

    Public Shared Async Function GniazdkoWlacz(sMAC As String, bOn As Boolean, bMsg As Boolean) As Task
        If String.IsNullOrEmpty(sMAC) Then
            DebugOut("GniazdkoWlacz(sId null or empty")
            Return
        End If
        DebugOut("GniazdkoWlacz(sMAC=" & sMAC & ", bOn=" & bOn & ", bMsg=" & bMsg)


        If GetSettingsBool("LogEnabled") Then
            Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
            Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & sMAC & vbTab & bOn.ToString)
        End If

        Dim oWriter As Windows.Storage.Streams.DataWriter = New Windows.Storage.Streams.DataWriter
        oWriter.WriteByte(255)
        If bOn Then
            oWriter.WriteByte(1)
            oWriter.WriteByte(1)
        Else
            oWriter.WriteByte(0)
            oWriter.WriteByte(0)
        End If
        oWriter.WriteByte(255)

        If gbInCommand Then
            If bMsg Then DialogBoxRes("msgAnotherInstance")
            Return
        End If
        gbInCommand = True

        Try
            Await SendAsPodejscie3(sMAC, oWriter.DetachBuffer, bMsg)
        Catch ex As Exception
            ' tylko po to, żeby na pewno odblokować gbInCommand
        End Try

        gbInCommand = False

        oWriter.Dispose()

    End Function
#End Region

    Private moTimerDeferal As Background.BackgroundTaskDeferral = Nothing

    Protected Overrides Async Sub OnBackgroundActivated(args As BackgroundActivatedEventArgs)
        moTimerDeferal = args.TaskInstance.GetDeferral()

        Await SprawdzStanBateryjki()

        moTimerDeferal.Complete()
    End Sub

    Public Shared Async Function SprawdzStanBateryjki() As Task
        Dim oBattRep As Windows.Devices.Power.BatteryReport = Windows.Devices.Power.Battery.AggregateBattery.GetReport
        If oBattRep Is Nothing Then Return
        If oBattRep.Status = Windows.System.Power.BatteryStatus.NotPresent Then Return

        If gmGniazdka.Count < 1 Then Await gmGniazdka.LoadAsync
        If gmGniazdka.Count < 1 Then Return

        Dim dPercent As Double = oBattRep.RemainingCapacityInMilliwattHours / oBattRep.FullChargeCapacityInMilliwattHours

#If DEBUG Then
        If GetSettingsBool("LogEnabled") Then
            Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
            Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & (dPercent * 100).ToString("00") & vbTab & oBattRep.RemainingCapacityInMilliwattHours & vbTab & oBattRep.FullChargeCapacityInMilliwattHours)
        End If
#End If

#If DEBUG Then
        If GetSettingsBool("LogEnabled") Then
            Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
            Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & vbTab & (dPercent * 100).ToString("0.00") & "%")
        End If
#End If


        If dPercent > 0.98 Then
            If oBattRep.Status = Windows.System.Power.BatteryStatus.Charging OrElse
                    oBattRep.Status = Windows.System.Power.BatteryStatus.Idle Then
#If DEBUG Then
                If GetSettingsBool("LogEnabled") Then
                    Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
                    Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & " chcę wyłączyć")
                End If
#End If
                Await GniazdkoWlacz(gmGniazdka.GetList.ElementAt(0).sAddr, False, False)
                Return
            End If
        End If

        If dPercent < 0.3 Then
            If oBattRep.Status <> Windows.System.Power.BatteryStatus.Charging Then
#If DEBUG Then
                If GetSettingsBool("LogEnabled") Then
                    Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
                    Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & " chcę włączyć (bo <0.3)")
                End If
#End If
                Await GniazdkoWlacz(gmGniazdka.GetList.ElementAt(0).sAddr, True, False)
                Return
            End If
        End If

        If Windows.System.Power.PowerManager.RemainingDischargeTime < TimeSpan.FromMinutes(90) Then ' 3× TimerTrigger
            If oBattRep.Status <> Windows.System.Power.BatteryStatus.Charging Then
#If DEBUG Then
                If GetSettingsBool("LogEnabled") Then
                    Dim oFile As Windows.Storage.StorageFile = Await GetLogFileMonthlyAsync("", "")
                    Await oFile.AppendLineAsync(Date.Now.ToString("yyyy.MM.dd HH:mm:ss") & " chcę włączyć (bo czas)")
                End If
#End If
                Await GniazdkoWlacz(gmGniazdka.GetList.ElementAt(0).sAddr, True, False)
                Return
            End If
        End If

    End Function

#Region "commandline"

    ' blokada, bo: https://docs.microsoft.com/en-us/windows/uwp/launch-resume/console-uwp
    ' ze tylko Desktop target, a moja app ma rozne targety

    ' https://www.c-sharpcorner.com/article/launch-uwp-app-via-commandline/
    Protected Overrides Async Sub OnActivated(args As IActivatedEventArgs)
        ' to jest m.in. dla Toast i tak dalej?
        ' w kazdym razie nie jest to normalne uruchomienie App

        Await DebugLogFileOutAsync("OnActivated", True)

        Dim strArgs As String = ""

        ' próba czy to commandline (w tym czy 16299)
        If IsFromCmdLine(args) Then
            Await DebugLogFileOutAsync("tak, to CommandLine", True)
            ' WYMAGA Win 16299,  = Win 1709, ale w tym IF na pewno jest to taka wersja
            Dim commandLine As CommandLineActivatedEventArgs = TryCast(args, CommandLineActivatedEventArgs)

            Dim operation As CommandLineActivationOperation = Nothing
            If commandLine IsNot Nothing Then
                Try
                    operation = commandLine.Operation
                Catch
                    operation = Nothing
                End Try
            Else
                Await DebugLogFileOutAsync("commandLine is NULL", True)
            End If

            If operation IsNot Nothing Then
                Try
                    strArgs = operation.Arguments
                Catch ex As Exception
                    strArgs = ""
                End Try
            Else
                Await DebugLogFileOutAsync("operation is NULL", True)
            End If

            If strArgs <> "" Then
                Await ObsluzParam(strArgs, 1)
            Else
                Await DebugLogFileOutAsync("strArgs is NULL", True)
                ' no to normalnie to pokaż - przez przeskok do OnLaunch?
                OnLaunched(Nothing)
                ' MyBase.OnActivated(args)
            End If

        Else
            MyBase.OnActivated(args)
        End If

    End Sub

    Private Function GetRequestedDeviceItem(sParam0 As String) As JedenSocket

        Dim sTmpAddr As String = sParam0.Replace("-", ":").Replace(".", ":")
        'Await DebugLogFileOutAsync("Param " & sParam0 & " should be address")
        Dim aAddr As String() = sTmpAddr.Split(":")
        If aAddr.Count <> 6 Then
            'Await DebugLogFileOutAsync("Cannot decompose address, count=" & aAddr.Count)
            Return Nothing
        End If

        For Each oTmpItem As JedenSocket In App.gmGniazdka.GetList
            If oTmpItem.sAddr = sTmpAddr Then Return oTmpItem
        Next

        'Await DebugLogFileOutAsync("Cannot find such address")

        Return Nothing

    End Function

    Private Async Function ObsluzParamMain(sParam As String) As Task
        ' nic nie mozemy zrobic, bo nie mamy w ogóle zapamiętanych devicesów
        If App.gmGniazdka.Count < 1 Then Return

        Dim aParams As String() = sParam.Split(" ")
        If aParams.Count <> 2 Then
            Await DebugLogFileOutAsync("Too many or too little paramaters",true)
            Return
        End If

        Dim oItem As JedenSocket = Nothing
        If aParams.ElementAt(0).ToLowerInvariant = "default" Then
            ' default: pierwszy zarejestrowany
            oItem = App.gmGniazdka.GetList.ElementAt(0)
        Else
            oItem = GetRequestedDeviceItem(aParams.ElementAt(0))
        End If
        If oItem Is Nothing Then
            Await DebugLogFileOutAsync("Nieudane oItem", True)
            Return
        End If

        'Await DebugLogFileOutAsync("Found: name=" & oItem.sName & ", iTyp=" & oItem.iTyp)

        Dim bReqStatus as Boolean
        Select Case aParams.ElementAt(1).tolower
            Case "on"
                bReqStatus = True
            Case "off"
                bReqStatus = False
            Case Else
                Await DebugLogFileOutAsync("Second parameter should be on/off, but is '" & aParams.ElementAt(1) & "'", True)
                Return
        End Select

        Await GniazdkoWlacz(oItem.sAddr, bReqStatus, False)

    End Function

    Private Async Function ObsluzParam(sParam As String, iTyp As Integer) As Task
        ' iTyp= 1: OnActivated , 2: OnLaunched

        Await DebugLogFileOutAsync("Uruchomienie z cmdline, sParam=" & sParam & ", iTyp=" & iTyp,true)

        ' nie mozna zadzialac jak jest app uruchomiona - bo może być Conflict na BlueTooth!
        'If App.gmGniazdka.Count > 0 Then
        '    'Await DebugLogFileOutAsync("FAIL: app działa, więc idę sobie")
        '    Return
        'End If

        If App.gmGniazdka.Count < 1 Then Await App.gmGniazdka.LoadAsync
        Await DebugLogFileOutAsync("Devices.Count=" & App.gmGniazdka.Count,true)

        Await ObsluzParamMain(sParam)

        'Application.Current.Exit()

    End Function

#End Region

End Class
