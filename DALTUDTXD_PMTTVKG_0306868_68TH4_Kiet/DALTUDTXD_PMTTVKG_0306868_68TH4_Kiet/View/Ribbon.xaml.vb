Namespace ETABS_Stability
    Partial Public Class MainWindow
        Public Sub New()
            InitializeComponent()
        End Sub

        Private Sub DataGrid_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

        End Sub
    End Class

    ' Class cho bảng ETABS Data Import
    Public Class ETABSDataImportItem
        Public Property ElementID As String
        Public Property ElementType As String
        Public Property Section As String
        Public Property Length As String
        Public Property ETABSID As String
    End Class

    ' Class cho bảng Stability Check Results
    Public Class StabilityCheckResultItem
        Public Property CheckType As String
        Public Property Condition As String
        Public Property StatusText As String
        Public Property IsPass As Boolean
        Public Property IsInfoValue As Boolean
        Public Property IsSuccessIcon As Boolean
        Public Property IsErrorIcon As Boolean
        Public Property IsNormalText As Boolean
    End Class
End Namespace
