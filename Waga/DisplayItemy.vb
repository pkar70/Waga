

' po kolei, wszystkie elementy pokazywane (BMI, fat, etc.)

'Public Property bone As Double      ' juz
'Public Property fat As Double       ' juz
'Public Property metabol As Double   ' juz
'Public Property muscle As Double    ' juz
'Public Property viscera As Double   ' juz
'Public Property water As Double     ' juz

'Public Property protein As Double
'Public Property obesity As Double
'Public Property bodyage As Integer  ' juz
'Public Property lbm As Double       ' juz

' offset start - jak nie od zera, offset end (max value)
' ale wtedy uwzglednic ze value < min, i odpowiednio przesunac min? albo "below scale"
' linijka na górze, od min do max, z przedziałkiem
' kolorki
' linijka na górze, od min do max, z przedziałkiem

'float minVal = 10;
'if currVal < minVal then minVal = currVal-1
'float maxVal = 35
'if currVal > maxVal then maxVal = currVal+5

' float WyliczKoniec(float minVal, float prog, float maxVal)
' 100*(prog-minVal)/(maxVal-minVal)


' moze byc lista .json, wczytywana, kolejnych elementow wraz z progami, kolorami i nazwami?

' waga: progi wyliczone z wysokości i BMI, min BMI10, max BMI35
' BMI (wedle Wiki https://en.wikipedia.org/wiki/Body_mass_index; https://pl.wikipedia.org/wiki/Wska%C5%BAnik_masy_cia%C5%82a )
' WHO: <16, czerwony; < 18.5 żółty; < 25 zielony; <30 żółty; czerwony
' HK: < 18.5 żółty; < 23 zielony; <25 żółty; czerwony
' JP: < 18.5 żółty; < 25 zielony; <30 żółty; czerwony
' SG: < 18.5 żółty; < 23 zielony; <27.5 żółty; czerwony

' FAT: <12 low; <18 normal; <28 high; obese [%]
' Muscle: <49.4 insufficient; <59.4 healthy; excelent [kg]
' water: < 52.3 low; <55.6 healthy, excellent [%]
' https://en.wikipedia.org/wiki/Body_water
' viscfat: <1 low; <9 healthy, <14 high; obese [no unit]
' boneMass: <2.4 low; <3.1 healthy, excellent [kg]
' metabolism: <1282.5 low; <1417.5 healthy, high [no unit]
' protein: <16 low; <20 healty, high [%]
' ewentualnie link do wikipedii 

Public Module DisplayItemy

    Private Sub AddWskazowka(oStack As StackPanel, dMin As Double, dVal As Double, dMax As Double)
        ' zabezpiezcenie, powinno być niepotrzebne, bo CALLER powinien zadbać o przesunięcie
        If dMin > dVal Then Return
        If dMax < dVal Then Return

        Dim iPct As Integer = 100 * (dVal - dMin) / (dMax - dMin)

        Dim oGrid As Grid = New Grid

        Dim oColDef1 As ColumnDefinition = New ColumnDefinition
        oColDef1.Width = New GridLength(iPct, GridUnitType.Star)

        Dim oColDef2 As ColumnDefinition = New ColumnDefinition
        oColDef2.Width = New GridLength(3, GridUnitType.Pixel)

        Dim oColDef3 As ColumnDefinition = New ColumnDefinition
        oColDef3.Width = New GridLength(100 - iPct, GridUnitType.Star)

        oGrid.ColumnDefinitions.Add(oColDef1)
        oGrid.ColumnDefinitions.Add(oColDef2)
        oGrid.ColumnDefinitions.Add(oColDef3)

        Dim oBord As Border = New Border
        oBord.Background = New SolidColorBrush(Windows.UI.Colors.Gray)  ' 128,128,128
        Grid.SetColumn(oBord, 1)

        oGrid.Children.Add(oBord)

        oGrid.Height = 10 ' pixel

        oStack.Children.Add(oGrid)

    End Sub

    Private Sub AddKolorkiRYGYR(oStack As StackPanel, dMin As Double, dVal As Double, dMax As Double, dProgRY As Double, dProgYG As Double, dProgGY As Double, dProgYR As Double,
                                Optional oBrushColR1 As SolidColorBrush = Nothing,
                                Optional oBrushColY1 As SolidColorBrush = Nothing,
                                Optional oBrushColY2 As SolidColorBrush = Nothing,
                                Optional oBrushColR2 As SolidColorBrush = Nothing)
        ' do BMI, oraz do masy z BMI wyliczonej
        ' zabezpiezcenie, powinno być niepotrzebne, bo CALLER powinien zadbać o przesunięcie
        If dMin > dVal Then Return
        If dMax < dVal Then Return

        ' na razie bez wskazówki w polu kolorkowym
        ' (ignorowanie dVal)

        Dim oGrid As Grid = New Grid
        Dim iLastPct As Integer = 100

        Dim iPct As Integer = 100 * (dProgRY - dMin) / (dMax - dMin)
        iLastPct -= iPct
        Dim oColDef1 As ColumnDefinition = New ColumnDefinition
        oColDef1.Width = New GridLength(iPct, GridUnitType.Star)

        iPct = 100 * (dProgYG - dProgRY) / (dMax - dMin)
        iLastPct -= iPct
        Dim oColDef2 As ColumnDefinition = New ColumnDefinition
        oColDef2.Width = New GridLength(iPct, GridUnitType.Star)

        iPct = 100 * (dProgGY - dProgYG) / (dMax - dMin)
        iLastPct -= iPct
        Dim oColDef3 As ColumnDefinition = New ColumnDefinition
        oColDef3.Width = New GridLength(iPct, GridUnitType.Star)

        Dim oColDef4 As ColumnDefinition = New ColumnDefinition
        ' ale ostatniego przejscia moze juz nie byc (woda jest do 65 normalnie, a przejscie na czerwone dopiero przy 80)
        If dProgYR > dMax Then
            iPct = 0
        Else
            iPct = 100 * (dProgYR - dProgGY) / (dMax - dMin)
        End If
        iLastPct -= iPct
        oColDef4.Width = New GridLength(iPct, GridUnitType.Star)

        Dim oColDef5 As ColumnDefinition = New ColumnDefinition
        oColDef5.Width = New GridLength(iLastPct, GridUnitType.Star)

        oGrid.ColumnDefinitions.Add(oColDef1)
        oGrid.ColumnDefinitions.Add(oColDef2)
        oGrid.ColumnDefinitions.Add(oColDef3)
        oGrid.ColumnDefinitions.Add(oColDef4)
        oGrid.ColumnDefinitions.Add(oColDef5)

        If oBrushColR1 Is Nothing Then oBrushColR1 = New SolidColorBrush(Windows.UI.Colors.Red)
        Dim oBord As Border = New Border
        oBord.Background = oBrushColR1
        Grid.SetColumn(oBord, 0)
        oGrid.Children.Add(oBord)

        If oBrushColR2 Is Nothing Then oBrushColR2 = New SolidColorBrush(Windows.UI.Colors.Red)
        oBord = New Border
        oBord.Background = oBrushColR2
        Grid.SetColumn(oBord, 4)
        oGrid.Children.Add(oBord)

        oBord = New Border
        oBord.Background = New SolidColorBrush(Windows.UI.Colors.Green)
        Grid.SetColumn(oBord, 2)
        oGrid.Children.Add(oBord)

        If oBrushColY1 Is Nothing Then oBrushColY1 = New SolidColorBrush(Windows.UI.Colors.Yellow)
        oBord = New Border
        oBord.Background = oBrushColY1
        Grid.SetColumn(oBord, 1)
        oGrid.Children.Add(oBord)

        If oBrushColY2 Is Nothing Then oBrushColY2 = New SolidColorBrush(Windows.UI.Colors.Yellow)
        oBord = New Border
        oBord.Background = oBrushColY2
        Grid.SetColumn(oBord, 3)
        oGrid.Children.Add(oBord)

        ' próba - slider jest przesunięty względem "wskazówki"
        'Dim oSlider As Slider = New Slider
        'oSlider.Minimum = dMin
        'oSlider.Maximum = dMax
        'oSlider.Value = dVal
        'oSlider.Height = 10
        'oSlider.VerticalAlignment = VerticalAlignment.Center
        'Grid.SetColumnSpan(oSlider, 5)
        'oGrid.Children.Add(oSlider)


        oGrid.Height = 10 ' pixel

        oStack.Children.Add(oGrid)


    End Sub

    Private Sub AddHeader(oStack As StackPanel, sHdr As String, sVal As String)
        Dim oGrid As Grid = New Grid

        Dim oColDef1 As ColumnDefinition = New ColumnDefinition
        oColDef1.Width = New GridLength(1, GridUnitType.Star)

        Dim oColDef2 As ColumnDefinition = New ColumnDefinition
        oColDef2.Width = New GridLength(1, GridUnitType.Star)

        oGrid.ColumnDefinitions.Add(oColDef1)
        oGrid.ColumnDefinitions.Add(oColDef2)

        Dim oTBHdr As TextBlock = New TextBlock
        oTBHdr.Text = sHdr
        oTBHdr.FontSize = 16
        Grid.SetColumn(oTBHdr, 0)
        oGrid.Children.Add(oTBHdr)

        Dim oTVal As TextBox = New TextBox
        oTVal.IsReadOnly = True
        oTVal.BorderThickness = New Thickness(0)
        oTVal.Text = sVal
        oTVal.HorizontalAlignment = HorizontalAlignment.Right
        Grid.SetColumn(oTVal, 1)
        oGrid.Children.Add(oTVal)

        oStack.Children.Add(oGrid)

    End Sub


    Public Sub RysujBMI(oStack As StackPanel, dVal As Double)
        Dim dMin As Double
        Dim dMax As Double

        dMin = Math.Max(0, Math.Min(14, dVal - 2))
        dMax = Math.Max(35, dVal + 5)


        AddHeader(oStack, "BMI", dVal.ToString("#0.0"))
        AddWskazowka(oStack, dMin, dVal, dMax)

        AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 16, 18.5, 25, 30) ' WHO: <16, czerwony; < 18.5 żółty; < 25 zielony; <30 żółty; czerwony
        'AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 18.5, 18.5, 23, 25) ' HK: < 18.5 żółty; < 23 zielony; <25 żółty; czerwony
        'AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 18.5, 18.5, 25, 30) ' JP: < 18.5 żółty; < 25 zielony; <30 żółty; czerwony
        'AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 18.5, 18.5, 23, 27.5) ' SG: < 18.5 żółty; < 23 zielony; <27.5 żółty; czerwony

        AddWskazowka(oStack, dMin, dVal, dMax)

    End Sub

    Public Sub RysujFat(oStack As StackPanel, dVal As Double, bWoman As Boolean)
        ' FAT: <12 low; <18 normal; <28 high; obese [%]

        ' https://en.wikipedia.org/wiki/Body_fat_percentage

        AddHeader(oStack, GetLangString("msgParamFat", "fat"), dVal.ToString("#0.0") & " %")

        Dim dMin As Double
        Dim dMax As Double

        If bWoman Then
            dMin = Math.Max(0, Math.Min(8, dVal - 2))
            dMax = Math.Max(36, dVal + 5)
            AddWskazowka(oStack, dMin, dVal, dMax)
            AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 10, 20, 31, 35)
            AddWskazowka(oStack, dMin, dVal, dMax)
        Else
            dMin = Math.Max(0, Math.Min(1, dVal - 2))
            dMax = Math.Max(30, dVal + 5)
            AddWskazowka(oStack, dMin, dVal, dMax)
            AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 2, 13, 24, 28)
            AddWskazowka(oStack, dMin, dVal, dMax)
        End If

    End Sub

    Public Sub RysujWoda(oStack As StackPanel, dVal As Double)
        ' water: < 52.3 low; <55.6 healthy, excellent [%]

        Dim dMin As Double
        Dim dMax As Double

        dMin = Math.Max(0, Math.Min(40, dVal - 2))
        dMax = Math.Max(65, dVal + 2)

        AddHeader(oStack, GetLangString("msgParamWoda", "water"), dVal.ToString("#0.0") & " %")
        AddWskazowka(oStack, dMin, dVal, dMax)

        AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 45, 52.3, 55.6, 80,
                        Nothing, Nothing, New SolidColorBrush(Windows.UI.Colors.Blue), New SolidColorBrush(Windows.UI.Colors.Yellow))

        AddWskazowka(oStack, dMin, dVal, dMax)
    End Sub

    Public Sub RysujViscFat(oStack As StackPanel, dVal As Double)
        ' viscfat: <1 low; <9 healthy, <14 high; obese [no unit]

        Dim dMin As Double
        Dim dMax As Double

        dMin = 0
        dMax = 18

        AddHeader(oStack, GetLangString("msgParamVisce", "viscerea fat"), dVal.ToString("#0.0") & " %")
        AddWskazowka(oStack, dMin, dVal, dMax)

        AddKolorkiRYGYR(oStack, dMin, dVal, dMax, 1, 9, 14, 16)

        AddWskazowka(oStack, dMin, dVal, dMax)
    End Sub

    'Public Property bone As Double      ' juz
    ' boneMass: <2.4 low; <3.1 healthy, excellent [kg]

    'Public Property metabol As Double   ' juz

    'Public Property muscle As Double    ' juz
    ' Muscle: <49.4 insufficient; <59.4 healthy; excelent [kg]

    'Public Property protein As Double
    'Public Property obesity As Double
    'Public Property bodyage As Integer  ' juz
    'Public Property lbm As Double       ' juz
    ' metabolism: <1282.5 low; <1417.5 healthy, high [no unit]
    ' protein: <16 low; <20 healty, high [%]



End Module
