
Public NotInheritable Class Calculator
    Inherits Page

    Private Sub uiRecalculateTA_Click(sender As Object, e As RangeBaseValueChangedEventArgs)
        Dim dTemp As Double = TempHigro2AppTemp(uiCalcSliderT.Value, uiCalcSliderH.Value)
        ' jako textbox: to co wyszło z przeliczenia
        uiCalcTxtTA.Text = dTemp.ToString("#0.0")
        ' zakres slider TA ma odpowiadac zakresowi slidera T, a Tapp moze wyjsc wieksza niz maximum slidera
        uiCalcSliderTA.Value = Math.Max(uiCalcSliderTA.Minimum, Math.Min(dTemp, uiCalcSliderTA.Maximum))
    End Sub
End Class
