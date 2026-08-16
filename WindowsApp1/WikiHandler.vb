Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.RegularExpressions

Public Class WikiHandler
    ' Cache of the wiki database, downloaded once per session.
    Shared InputFromGitHub() As String

    Private Const WikiDbUrl As String = "https://raw.githubusercontent.com/Endymi0n74/compactWindows/main/Wiki/WikiDB_Games"

    Private Shared ReadOnly WikiHttpClient As New HttpClient With {.Timeout = TimeSpan.FromSeconds(15)}


    'Parses the locally selected folder name and matches it against the wiki database
    'to produce the compression estimate popup. The uncompressed folder size is passed
    'in so the tree is not enumerated a second time, and the download is asynchronous
    'so the UI stays responsive.
    Public Shared Async Sub localFolderParse(wdString As String, DIwDString As DirectoryInfo, rawSizeBytes As Long)

        Dim wnpatch As String = Regex.Replace(DIwDString.Name.ToString, "[^\p{L}a-zA-Z0-90]", "").ToLower
        Dim workingname As String

        If wnpatch.Contains("callofduty") Then
            Dim interm = wnpatch.Replace("callofduty", "cod")
            If interm.Trim().EndsWith("modernwarfare") Then
                workingname = "cod4"
            Else
                workingname = interm
            End If
        ElseIf wnpatch.Contains("gameoftheyear") Then
            workingname = wnpatch.Replace("gameoftheyear", "goty")
        ElseIf wnpatch.Contains("shadowofmordor") Then
            workingname = "middleearthshadowofmordor"
        ElseIf wnpatch.Contains("age2hd") Then
            workingname = "ageofempiresiihd"
        Else
            workingname = wnpatch
        End If

        Dim folderSizeraw As String = GetOutputSize(rawSizeBytes, True)
        Dim folderSize As Decimal
        Dim suffix As String
        Try
            folderSize = Math.Round(Decimal.Parse(folderSizeraw.Split(" ")(0)), 2)
            suffix = folderSizeraw.Split(" ")(1)
        Catch ex As Exception
            folderSize = 0
            suffix = ""
        End Try

        Try
            Form2.wkPreSizeVal.Text = Math.Round(folderSize, 1)
            Form2.wkPreSizeUnit.Text = suffix
            Form2.wkPreSizeUnit.Location = New Point(Form2.wkPreSizeVal.Location.X + Form2.wkPreSizeVal.Size.Width - 10, Form2.wkPreSizeVal.Location.Y + 10)
        Catch ex As Exception
            Form2.wkPreSizeVal.Text = "?"
            Form2.wkPreSizeUnit.Text = ""
            Form2.wkPreSizeUnit.Location = New Point(Form2.wkPreSizeVal.Location.X + Form2.wkPreSizeVal.Size.Width, Form2.wkPreSizeVal.Location.Y)
        End Try

        Try
            Await WikiParserAsync(workingname, folderSize, suffix)
        Catch ex As Exception
            ShowNoInternetError()
        End Try

    End Sub


    Private Shared Async Function WikiParserAsync(workingname As String, folderSize As Decimal, suffix As String) As Task
        Try
            If InputFromGitHub Is Nothing Then
                'Download and cache the wiki database (async so the UI is not blocked).
                Try
                    Dim bytes As Byte() = Await WikiHttpClient.GetByteArrayAsync(WikiDbUrl)
                    InputFromGitHub = Encoding.UTF8.GetString(bytes).TrimEnd().Split(vbLf)
                Catch ex As Exception
                    ShowNoInternetError()
                    Return
                End Try
            End If

            Dim gameName As New List(Of String)

            For Each s As String In InputFromGitHub
                Try
                    gameName.Add(s.Split("|")(2))
                Catch ex As Exception
                End Try
            Next

            Dim strippedgameName As New List(Of String)

            For Each s In gameName
                Dim n = Regex.Replace(s, "[^\p{L}a-zA-Z0-90]", "")
                strippedgameName.Add(n.ToLower)
            Next

            Dim i = 0
            Dim gcount As New List(Of Integer)
            For Each a In strippedgameName
                If a.ToString.StartsWith(workingname) Then
                    gcount.Add(i)
                End If
                i += 1
            Next

            BuildTableHeader()

            Dim ratioavg As Decimal = 1
            For Each n In gcount
                If InputFromGitHub(n).Split("|").Length < 7 Then
                    Continue For
                End If

                FillTable(n)

                Try
                    ratioavg += Decimal.Parse(InputFromGitHub(n).Split("|")(6))
                Catch ex As Exception
                End Try

                If InputFromGitHub(n).Split("|")(7).Contains("*") Then
                    Form2.lblCompactIssues.Visible = True
                    Form2.lblCompactIssues.Text = "! Game has issues"
                Else
                    Form2.lblCompactIssues.Visible = False
                End If
            Next

            Try
                ratioavg = (ratioavg - 1) / gcount.Count

                Form2.wkPostSizeVal.Text = Math.Round(folderSize * ratioavg, 1)
                Form2.wkPostSizeUnit.Text = suffix
                Form2.wkPostSizeUnit.Location = New Point(Form2.wkPostSizeVal.Location.X + Form2.wkPostSizeVal.Size.Width - 10, Form2.wkPostSizeVal.Location.Y + 10)
                Form2.wkPostSizeUnit.Visible = True
            Catch ex As System.DivideByZeroException
                Form2.wkPostSizeVal.Text = "?"
                Form2.wkPostSizeUnit.Text = ""
                Form2.wkPostSizeUnit.Location = New Point(Form2.wkPostSizeVal.Location.X + Form2.wkPostSizeVal.Size.Width, Form2.wkPostSizeVal.Location.Y)
            Catch ex As Exception
                Form2.wkPostSizeVal.Text = "?"
                Form2.wkPostSizeUnit.Text = ""
            End Try

            Form2.GamesTable.Visible = True

        Catch ex As Exception
            ShowNoInternetError()
        End Try

    End Function


    Private Shared Sub BuildTableHeader()
        Form2.GamesTable.Visible = False
        Form2.GamesTable.Controls.Clear()
        Form2.GamesTable.RowCount = 0

        Dim GName As New Label
        GName.Text = "Game"

        Dim GSizeU As New Label
        GSizeU.Text = "Before"

        Dim GSizeC As New Label
        GSizeC.Text = "After"

        Dim GCompR As New Label
        GCompR.Text = "Ratio"

        Dim GCompAlg As New Label
        GCompAlg.Text = "Algorithm"

        Form2.GamesTable.RowStyles.Add(New RowStyle(SizeType.Absolute, 35))
        Form2.GamesTable.RowCount += 1
        Form2.GamesTable.Controls.Add(GName, 0, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GSizeU, 1, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GSizeC, 2, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GCompR, 3, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GCompAlg, 4, Form2.GamesTable.RowCount - 1)

        For Each WikiHeader As Label In Form2.GamesTable.Controls
            WikiHeader.Font = New Font("Segoe UI", 11, FontStyle.Bold)
            WikiHeader.Dock = DockStyle.Right
        Next

        GName.Dock = DockStyle.Left
    End Sub


    Private Shared Sub ShowNoInternetError()
        Form2.lblCompactIssues.Text = "! No Internet Connection"
        Form2.lblCompactIssues.Visible = True
        Form2.wkPostSizeVal.Text = "?"
        Form2.wkPostSizeUnit.Text = ""
        Form2.wkPostSizeUnit.Location = New Point(Form2.wkPostSizeVal.Location.X + Form2.wkPostSizeVal.Size.Width, Form2.wkPostSizeVal.Location.Y)
    End Sub


    Private Shared Sub FillTable(ps As Integer)

        Dim GName As New Label
        GName.Text = InputFromGitHub(ps).Split("|")(2)
        GName.Dock = DockStyle.Left
        GName.Font = New Font("Segoe UI", 11, FontStyle.Regular)

        Dim GSizeU As New Label
        GSizeU.Text = InputFromGitHub(ps).Split("|")(3)
        GSizeU.Dock = DockStyle.Right
        GSizeU.Font = New Font("Segoe UI", 10, FontStyle.Regular)
        Dim GSizeC As New Label
        GSizeC.Text = InputFromGitHub(ps).Split("|")(4)
        GSizeC.Dock = DockStyle.Right
        GSizeC.Font = New Font("Segoe UI", 10, FontStyle.Regular)
        Dim GCompR As New Label
        GCompR.Text = InputFromGitHub(ps).Split("|")(6)
        GCompR.Dock = DockStyle.Right
        GCompR.Font = New Font("Segoe UI", 10, FontStyle.Regular)
        Dim GCompAlg As New Label
        GCompAlg.Text = InputFromGitHub(ps).Split("|")(1)
        GCompAlg.Dock = DockStyle.Right
        GCompAlg.Font = New Font("Segoe UI", 10, FontStyle.Regular)

        Form2.GamesTable.RowStyles.Add(New RowStyle(SizeType.Absolute, 35))
        Form2.GamesTable.RowCount += 1
        Form2.GamesTable.Controls.Add(GName, 0, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GSizeU, 1, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GSizeC, 2, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GCompR, 3, Form2.GamesTable.RowCount - 1)
        Form2.GamesTable.Controls.Add(GCompAlg, 4, Form2.GamesTable.RowCount - 1)

        For Each ConC As Label In Form2.GamesTable.Controls
            ConC.AutoSize = True
            ConC.Padding = New Padding(2, 4, 0, 0)
        Next

    End Sub


    Public Shared Sub showWikiRes()

        Dim screenpos As Point = Compact.PointToScreen(New Point(Compact.seecompest.Location.X - 1, Compact.seecompest.Location.Y + 12))

        Form2.StartPosition = FormStartPosition.Manual

        If Form2.GamesTable.Width < 450 Then
            If Form2.GamesTable.RowCount > 1 Then
                Form2.SetBounds(screenpos.X, screenpos.Y, Form2.GamesTable.Width + 35, Form2.GamesTable.Height + 200)
            Else
                Form2.SetBounds(screenpos.X, screenpos.Y, 450, 130)
            End If
        Else
            Form2.SetBounds(screenpos.X, screenpos.Y, Form2.GamesTable.Width + 35, Form2.GamesTable.Height + 200)
        End If

        FadeTransition.FadeForm(Form2, 0, 0.96, 200)

    End Sub


    Public Shared Function GetOutputSize(ByVal inputsize As Decimal, Optional ByVal showSizeType As Boolean = False) As String            'Function for converting from Bytes into various units
        Dim sizeType As String = ""
        If inputsize < 1024 Then
            sizeType = " B"
        Else
            If inputsize < (1024 ^ 3) Then
                If inputsize < (1024 ^ 2) Then
                    sizeType = " KB"
                    inputsize = inputsize / 1024
                Else
                    sizeType = " MB"
                    inputsize = inputsize / 1024 ^ 2
                End If
            Else
                sizeType = " GB"
                inputsize = inputsize / 1024 ^ 3
            End If
        End If

        If showSizeType = True Then
            Return inputsize & sizeType
        Else
            Return inputsize
        End If

    End Function

End Class
