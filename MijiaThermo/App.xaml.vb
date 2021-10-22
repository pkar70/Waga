NotInheritable Class App
    Inherits Application

    Public Shared gmTermo As TermoList = New TermoList
    Public Shared gbShowAdditCols As Boolean = False

#Region "wizard"

    Protected Function OnLaunchFragment(aes As ApplicationExecutionState) As Frame
        Dim mRootFrame As Frame = TryCast(Window.Current.Content, Frame)

        ' Do not repeat app initialization when the Window already has content,
        ' just ensure that the window is active

        If mRootFrame Is Nothing Then
            ' Create a Frame to act as the navigation context and navigate to the first page
            mRootFrame = New Frame()

            AddHandler mRootFrame.NavigationFailed, AddressOf OnNavigationFailed

            ' PKAR added wedle https://stackoverflow.com/questions/39262926/uwp-hardware-back-press-work-correctly-in-mobile-but-error-with-pc
            AddHandler mRootFrame.Navigated, AddressOf OnNavigatedAddBackButton
            AddHandler Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested, AddressOf OnBackButtonPressed

            ' Place the frame in the current Window
            Window.Current.Content = mRootFrame
        End If

        Return mRootFrame
    End Function


    ''' <summary>
    ''' Invoked when the application is launched normally by the end user.  Other entry points
    ''' will be used when the application is launched to open a specific file, to display
    ''' search results, and so forth.
    ''' </summary>
    ''' <param name="e">Details about the launch request and process.</param>
    Protected Overrides Sub OnLaunched(e As Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)
        Dim RootFrame As Frame = OnLaunchFragment(e.PreviousExecutionState)

        If e.PrelaunchActivated = False Then
            If RootFrame.Content Is Nothing Then
                ' When the navigation stack isn't restored navigate to the first page,
                ' configuring the new page by passing required information as a navigation
                ' parameter
                RootFrame.Navigate(GetType(MainPage), e.Arguments)
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

    ' CommandLine, Toasts
    Protected Overrides Async Sub OnActivated(args As IActivatedEventArgs)
        ' to jest m.in. dla Toast i tak dalej?

        ' próba czy to commandline
        If args.Kind = ActivationKind.CommandLineLaunch Then

            Dim commandLine As CommandLineActivatedEventArgs = TryCast(args, CommandLineActivatedEventArgs)
            Dim operation As CommandLineActivationOperation = commandLine?.Operation
            Dim strArgs As String = operation?.Arguments

            If Not String.IsNullOrEmpty(strArgs) Then
                Await ObsluzCommandLine(strArgs)
                Window.Current.Close()
                Return
            End If
        End If

        ' jesli nie cmdline (a np. toast), albo cmdline bez parametrow, to pokazujemy okno
        Dim rootFrame As Frame = OnLaunchFragment(args.PreviousExecutionState)

        rootFrame.Navigate(GetType(MainPage))

        Window.Current.Activate()

    End Sub
#End Region

    'Private moTimerDeferal As Background.BackgroundTaskDeferral = Nothing

    Private Async Function AppServiceLocalCommand(sCommand As String) As Task(Of String)
        If sCommand = "debug scan" Then
            SetSettingsBool("debugScan", True)
            Return "DONE"
        End If
        Return ""
    End Function

    Protected Overrides Async Sub OnBackgroundActivated(args As BackgroundActivatedEventArgs)
        DebugOut("Starting OnBackgroundActivated")

        moTaskDeferal = args.TaskInstance.GetDeferral()
        Dim bNoComplete = RemSysInit(args, "debug scan" & vbTab & "more info from Bluetooth scanning")

        'Await DebugLogFileOutAsync("Starting OnBackgroundActivated")
        If Not bNoComplete Then Await ObsluzBackgroundAsync()   ' w MijiaThermo

        DebugOut("Ending OnBackgroundActivated")

        If Not bNoComplete Then moTaskDeferal.Complete()
    End Sub

#If False Then


    Private moChar As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic = Nothing

    Public Async Function DisplayTempHigroBatt2() As Task

        If moChar IsNot Nothing Then Return

        ProgRingShow(True)

        Try

            If Not Await InitBTserviceAsync() Then Return

            moChar = Await GetBTcharacterAsync("ebe0ccc1-7a0a-4b0c-8a1a-6ff2997da3a6")   ' "Temperature and H"
            If moChar Is Nothing Then Return

            AddHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged

            Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify)

            If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
                Await DialogBoxAsync("Cannot subscribe for notifications")
                Return
            End If

        Finally
            ProgRingShow(False)
        End Try

    End Function

    Private moNewPom As JedenPomiar = New JedenPomiar


    Private Sub Gatt_ValueChanged(sender As Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, args As Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs)

        Dim aArray As Byte() = args.CharacteristicValue.ToArray

        moNewPom.uMAC = muMac
        moNewPom.dTimeStamp = DateTime.Now
        moNewPom.iHigro = aArray.ElementAt(2)
        moNewPom.dTemp = (aArray.ElementAt(1) * 256 + aArray.ElementAt(0)) / 100
        moNewPom.iBattMV = aArray.ElementAt(4) * 256 + aArray.ElementAt(3)

        uiLastWhen.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, AddressOf fromDispatchHaveData)
    End Sub

    Private Async Sub fromDispatchHaveData()
        uiLastWhen.Text = moNewPom.dTimeStamp.ToString("G")
        uiLastTemp.Text = moNewPom.dTemp.ToString("#0.00") & " °C"
        uiLastHigro.Text = moNewPom.iHigro & " %"
        uiLastBatt.Text = (moNewPom.iBattMV / 1000).ToString("0.000") & " V"

        ' ZAPISANIE DANYCH GDZIES

        ' wyłączenie notification
        RemoveHandler moChar.ValueChanged, AddressOf Gatt_ValueChanged
        Dim oResp = Await moChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)

        If oResp <> Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success Then
            DialogBox("Cannot de-subscribe for notifications")
            Return
        End If

        moChar = Nothing

    End Sub
#End If

End Class
