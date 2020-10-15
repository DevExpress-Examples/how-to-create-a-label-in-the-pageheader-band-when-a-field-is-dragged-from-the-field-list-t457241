﻿Imports DevExpress.Data.Browsing
Imports DevExpress.XtraPrinting
Imports DevExpress.XtraReports.Design
Imports DevExpress.XtraReports.Expressions.Native
Imports DevExpress.XtraReports.UI
Imports DevExpress.XtraReports.UserDesigner
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Namespace T457241

    Public Class CustomFieldListDragDropService
        Implements IFieldListDragDropService

        Private host As IDesignerHost
        Private panel As XRDesignPanel

        Public Sub New(ByVal host As IDesignerHost, ByVal panel As XRDesignPanel)
            Me.host = host
            Me.panel = panel
        End Sub

        Public Function GetDragHandler() As IDragHandler Implements IFieldListDragDropService.GetDragHandler
            Return New CustomFieldDragHandler(host, panel)
        End Function
    End Class

    Public Class CustomFieldDragHandler
        Inherits FieldDragHandler

        Private panel As XRDesignPanel
        Private droppedControl As XRControl

        Public Sub New(ByVal host As IDesignerHost, ByVal panel As XRDesignPanel)
            MyBase.New(host)
            Me.host = host
            Me.panel = panel
        End Sub

        Public Overrides Sub HandleDragDrop(ByVal sender As Object, ByVal e As DragEventArgs)
            Dim droppedData() As DataInfo = TryCast(e.Data.GetData(GetType(DataInfo())), DataInfo())
            Dim parentControl As XRControl = bandViewSvc.GetControlByScreenPoint(New System.Drawing.Point(e.X, e.Y))

            Dim selectSvc As ISelectionService = TryCast(host.GetService(GetType(ISelectionService)), ISelectionService)

            If ((TypeOf parentControl Is XRPanel) OrElse (TypeOf parentControl Is Band)) AndAlso ((droppedData.Length = 1)) Then
                AddSingleField(e, droppedData, parentControl, selectSvc)
            ElseIf ((TypeOf parentControl Is XRPanel) OrElse (TypeOf parentControl Is Band)) AndAlso ((droppedData.Length > 1)) Then
                AddMultipleFields(e, droppedData, parentControl, selectSvc)
            Else
                MyBase.HandleDragDrop(sender, e)
            End If
        End Sub

        Private Sub AddMultipleFields(ByVal e As DragEventArgs, ByVal droppedData() As DataInfo, ByVal parentControl As XRControl, ByVal selectSvc As ISelectionService)
            Me.AdornerService.ResetSnapping()
            Me.RulerService.HideShadows()
            Dim headerCell As XRTableCell
            Dim parent As XRControl = bandViewSvc.GetControlByScreenPoint(New System.Drawing.Point(e.X, e.Y))
            If parent Is Nothing Then
                Return
            End If
            Dim size As New SizeF(100.0F * droppedData.Length, 25.0F)

            If TypeOf parentControl Is DetailBand Then
                size.Width = CalculateWidth(parentControl)
            End If

            Dim detailTable As New XRTable() With {.Name = "DetailTable"}
            detailTable.BeginInit()
            Dim detailRow As New XRTableRow()
            detailTable.Rows.Add(detailRow)

            Me.droppedControl = detailTable
            detailTable.SizeF = size

            host.Container.Add(detailTable)
            host.Container.Add(detailRow)

            For i As Integer = 0 To droppedData.Length - 1
                Dim cell As New XRTableCell()
                cell.ExpressionBindings.Add(New ExpressionBinding("Text", ExpressionBindingHelper.NormalizeDataMember(droppedData(i).Member, parentControl.Report.DataMember)))
                detailRow.Cells.Add(cell)
                host.Container.Add(cell)
            Next i
            detailTable.EndInit()

            selectSvc.SetSelectedComponents(New XRControl() {detailTable})

            Dim dropPoint As PointF = GetDragDropLocation(e, detailTable, parentControl)
            Me.DropXRControl(parentControl, New PointF(0, dropPoint.Y))

            If (TypeOf parentControl Is DetailBand) Then
                Dim band As PageHeaderBand = Nothing
                If (TryCast(parentControl, DetailBand)).Report.Bands.OfType(Of PageHeaderBand)().FirstOrDefault() IsNot Nothing Then
                    band = TryCast((TryCast(parentControl, DetailBand)).Report.Bands(BandKind.PageHeader), PageHeaderBand)
                Else
                    band = New PageHeaderBand()
                    TryCast(parentControl, DetailBand).Report.Bands.Add(band)
                    host.Container.Add(band)
                End If

                Dim headerTable As New XRTable() With {.Name = "HeaderTable"}
                headerTable.BeginInit()

                Dim headerRow As New XRTableRow()
                headerTable.Rows.Add(headerRow)

                headerTable.SizeF = size

                host.Container.Add(headerTable)
                host.Container.Add(headerRow)

                For i As Integer = 0 To droppedData.Length - 1
                    headerCell = CreateTableCell(host, headerRow, droppedData(i).DisplayName)
                Next i
                headerTable.Borders = BorderSide.All
                headerTable.EndInit()

                band.Controls.Add(headerTable)
                headerTable.LocationF = droppedControl.LocationF
            End If
        End Sub

        Private Function CreateTableCell(ByVal host As IDesignerHost, ByVal row As XRTableRow, ByVal cellText As String) As XRTableCell
            Dim headerCell As New XRTableCell()
            headerCell.Text = cellText
            headerCell.BackColor = Color.Green
            headerCell.ForeColor = Color.Yellow
            headerCell.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter
            headerCell.Font = New Font("Calibry", 11, FontStyle.Bold)
            row.Cells.Add(headerCell)
            host.Container.Add(headerCell)
            Return headerCell
        End Function


        Private Function GetDragDropLocation(ByVal e As DragEventArgs, ByVal control As XRControl, ByVal parent As XRControl) As PointF
            Dim bandPoint As PointF = EvalBandPoint(e, parent.Band)
            bandPoint = bandViewSvc.SnapBandPoint(bandPoint, parent.Band, control, New XRControl() {control})
            Dim screenPoint As PointF = bandViewSvc.ControlViewToScreen(bandPoint, parent.Band)
            Return bandViewSvc.ScreenToControl(New RectangleF(screenPoint, SizeF.Empty), parent).Location
        End Function


        Private Function CalculateWidth(ByVal control As XRControl) As Single
            Dim report As XtraReport = control.RootReport
            Return GraphicsUnitConverter.Convert(report.PageWidth - report.Margins.Left - report.Margins.Right, report.Dpi, GraphicsDpi.HundredthsOfAnInch)
        End Function

        Private Sub AddSingleField(ByVal e As DragEventArgs, ByVal droppedData() As DataInfo, ByVal parentControl As XRControl, ByVal selectSvc As ISelectionService)
            Me.AdornerService.ResetSnapping()
            Me.RulerService.HideShadows()

            Dim size As New SizeF(100.0F, 25.0F)

            Dim detailLabel As New XRLabel()
            Me.droppedControl = detailLabel
            detailLabel.SizeF = size

            host.Container.Add(detailLabel)
            Dim dropPoint As PointF = GetDragDropLocation(e, detailLabel, parentControl)
            detailLabel.ExpressionBindings.Add(New ExpressionBinding("Text", ExpressionBindingHelper.NormalizeDataMember(droppedData(0).Member, parentControl.Report.DataMember)))

            selectSvc.SetSelectedComponents(New XRControl() {detailLabel})

            Me.DropXRControl(parentControl, dropPoint)

            If (TypeOf parentControl Is DetailBand) Then
                Dim band As PageHeaderBand = Nothing
                If panel.Report.Bands.OfType(Of PageHeaderBand)().FirstOrDefault() IsNot Nothing Then
                    band = TryCast(panel.Report.Bands(BandKind.PageHeader), PageHeaderBand)
                Else
                    band = New PageHeaderBand()
                    panel.Report.Bands.Add(band)
                    host.Container.Add(band)
                End If
                Dim headerLabel As XRLabel = CreateLabel(droppedControl.LocationF, size, droppedData(0).DisplayName)
                host.Container.Add(headerLabel)
                band.Controls.Add(headerLabel)
            End If
        End Sub

        Private Function CreateLabel(ByVal location As PointF, ByVal size As SizeF, ByVal labelText As String) As XRLabel
            Dim headerLabel As New XRLabel()
            headerLabel.SizeF = size
            headerLabel.LocationF = location
            headerLabel.Text = labelText
            headerLabel.BackColor = Color.Green
            headerLabel.ForeColor = Color.Yellow
            headerLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter
            headerLabel.Font = New Font("Calibry", 11, FontStyle.Bold)
            Return headerLabel
        End Function

        Private Sub DropXRControl(ByVal parent As XRControl, ByVal dropPoint As PointF)
            Dim screenPoint As PointF = dropPoint
            parent.Controls.Add(droppedControl)
            droppedControl.LocationF = dropPoint
        End Sub
    End Class
End Namespace