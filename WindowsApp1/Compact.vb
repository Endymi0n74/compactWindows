Imports System.Collections.Concurrent
Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports Ookii.Dialogs                                                                          'Uses Ookii Dialogs for the non-archaic filebrowser dialog. http://www.ookii.org/Software/Dialogs


Public Class Compact
    Dim version As String = My.Application.Info.Version.ToString(3)

    'Cancellation token for the currently running compact.exe operation.
    Private operationCts As CancellationTokenSource

    'Console output is produced by compact.exe on reader threads. Instead of calling
    'Control.Invoke for every line and inserting into a ListBox at index 0 (both O(n^2)
    'worst case), lines are queued here and drained in batches on the UI timer thread.
    Private Const MAX_CONSOLE_LINES As Integer = 2000
    Private Const CONSOLE_REBUILD_EVERY As Integer = 500
    Private Const CONSOLE_DRAIN_PER_TICK As Integer = 1000
    Private ReadOnly consoleQueue As New ConcurrentQueue(Of String)
    Private ReadOnly consoleLogBuffer As New List(Of String)
    Private consoleDirtyLines As Integer = 0


    Private Shared Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(CompactGUI.Compact)
    End Sub






    'Status Monitors
    Dim isQueryMode = 0
    Dim isQueryCalledByCompact = 0
    Dim isActive = 0
    Dim byteComparisonRaw As String = ""
    Dim byteComparisonRawFilesCompressed As String = ""
    Dim dirCountProgress As Int64
    Dim dirCountTotal As Int64
    Dim fileCountTotal As Int64 = 0
    Dim fileCountProgress As Int64
    Dim fileCountOutputCompressed As Int64
    Dim QdirCountProgress As Int64







    Private Const MSG_INDEX_TOTALFILES As String = "%1"
    Private Const MSG_INDEX_TOTALDIRECTORIES As String = "%2"
    Private Const MSG_INDEX_FILESCOMPRESSEDCOUNT As String = "%3"
    Private Const MSG_INDEX_FILESNOTCOMPRESSEDCOUNT As String = "%4"
    Private Const MSG_INDEX_TOTALBYTESUNCOMPRESSED As String = "%5"
    Private Const MSG_INDEX_TOTALBYTESCOMPRESSED As String = "%6"
    Private Const MSG_INDEX_COMPRESSIONRATIO As String = "%7"



    'FormatMessage substrings   
    Dim fmt8 As String = GetMessageFromModule("compact.exe", 8)   'Analysis Endlines
    Dim fmt7 As String = GetMessageFromModule("compact.exe", 7)   'Listing[] Lines - directory count
    Dim fmt1 As String = GetMessageFromModule("compact.exe", 1)   '[OK] Line - file count
    Dim fmt10 As String = GetMessageFromModule("compact.exe", &H10) 'Uncompression finished line
    Dim fmtC As String = GetMessageFromModule("compact.exe", &HC)   'Compression finished Endlines

    Dim fixedfmt8 = fmt8.Trim()                            'removes the two leading spaces before the analysis endstring so that formatting works. 
    Dim fixedfmt7 = fmt7.Trim()
    Dim fixedfmt1 = fmt1.Trim()
    Dim fixedfmt10 = fmt10.Trim()
    Dim fixedfmtc = fmtC.Trim()


    Dim FMT_ANALYSIS_MSG As String() = fixedfmt8.Split(vbCrLf)                   'splits the lone message into its four components (see the above ANALYSIS OUTPUT comment
    Dim FMT_LISTING_MSG As String = fixedfmt7.split(vbCrLf)(0)
    Dim FMT_UNCOMPRESSED_MSG As String = fixedfmt10.split(vbCrLf)(0)
    Dim FMT_COMPRESSED_MSG As String() = fixedfmtc.split(vbCrLf)

    'These aren't currently used
    Dim FMT_FILESWITHINDIRECTORIES1 As String = Before(fmt8.Replace(vbCrLf, ""), MSG_INDEX_TOTALFILES)
    Dim FMT_FILESWITHINDIRECTORIES2 As String = Between(fmt8.Replace(vbCrLf, ""), MSG_INDEX_TOTALFILES, MSG_INDEX_TOTALDIRECTORIES)
    Dim FMT_FILESCOMPRESSED As String = ""

    'Gets the relevant lines from the FMT_XXX_MSG Arrays



    'Index values that are found while parsing the console output. 
    Dim CON_INDEX_TOTALFILES As Integer = 1
    Dim CON_INDEX_TOTALDIRECTORIES As Integer = 1
    Dim CON_INDEX_FILESCOMPRESSEDCOUNT As Integer = 1
    Dim CON_INDEX_FILESNOTCOMPRESSEDCOUNT As Integer = 1
    Dim CON_INDEX_TOTALBYTESCOMPRESSED As Integer = 1
    Dim CON_INDEX_TOTALBYTESNOTCOMPRESSED As Integer = 1
    Dim CON_INDEX_COMPRESSIONRATIO As Integer = 1

    'e.Data Output Strings from the console - each of these is one of the four lines at the end of the console output. 
    Dim CON_FILESWITHINDIRECTORIESLINE
    Dim CON_FILESCOMPRESSEDLINE
    Dim CON_TOTALBYTESLINE
    Dim CON_COMPRATIO


    'Output Arrays - These start of with the %n values in them from the MUI tables, but when the console output is parsed the %n is replaced with the actual data, and the index of that is stored in CON_INDEX variables above
    Dim ARR_FILESWITHINDIRECTORIES As String() = fixedfmt8.split()
    Dim ARR_FILESCOMPRESSED As String() = fixedfmt8.split()
    Dim ARR_TOTALBYTES As String() = fixedfmt8.split()
    Dim ARR_COMPRATIO As String() = fixedfmt8.split()
    Dim ARR_LISTING As String() = fixedfmt7.split()



    'Counts up from the first results line until it find four lines
    Dim OutputlineIndex = 0
    Dim canProceed = 0



    Dim REGEX_NUMBERFORMATTER As New Regex("(?<=\d+)\s+(?=\d+)")



    Public Function CALC_OUTPUT(edata As String)
        Dim CONINPUTDATA As String() = REGEX_NUMBERFORMATTER.Replace(edata, "").Trim().Split(" ")

        Dim FMTFilesWithin As String() = FMT_ANALYSIS_MSG(0).Split(" ")
        Dim FMTCompNotComp As String() = FMT_ANALYSIS_MSG(1).Trim(vbCrLf).Split(" ")
        Dim FMTTotalBytes As String() = (FMT_ANALYSIS_MSG(2).Trim(vbCrLf)).Split(" ")
        Dim FMTCompRatio As String() = FMT_ANALYSIS_MSG(3).Trim(vbCrLf).Split(" ")
        Dim FMTListing As String() = FMT_LISTING_MSG.Trim().Split(" ")
        Dim FMTUncompressed As String() = FMT_UNCOMPRESSED_MSG.Split(" ")
        Dim FMTCompressFinished As String() = FMT_COMPRESSED_MSG(FMT_COMPRESSED_MSG.Count - 1).Trim(vbCrLf).Split(" ")


        'LISTING - DIRECTORY COUNT
        If FMTListing(0) = CONINPUTDATA(0) Then
            QdirCountProgress += 1
        End If


        'OK - FILE COUNT
        If edata.EndsWith(fixedfmt1) Then
            fileCountProgress += 1
        End If


        'Uncompressed - Checks if uncompression is finished
        If FMTUncompressed.Count = CONINPUTDATA.Count _
            And (FMTUncompressed(FMTUncompressed.Count - 1) = CONINPUTDATA(CONINPUTDATA.Count - 1) _
                Or FMTUncompressed(FMTUncompressed.Count - 1).Contains("%2")) Then

            dirCountProgress = 0
            fileCountProgress = fileCountTotal

        End If


        'Compress Finished Ratio - Checks if compression is finished
        If FMTCompressFinished.Count = CONINPUTDATA.Count _
            And OutputlineIndex = 0 _
            And (FMTCompressFinished(FMTCompressFinished.Count - 1) = CONINPUTDATA(CONINPUTDATA.Count - 1) _
                Or CONINPUTDATA(CONINPUTDATA.Count - 1).Contains("1.")) Then

            dirCountProgress = dirCountTotal
            fileCountProgress = fileCountTotal

        End If


        'Analysis Complete - Gets the lines when analysing a folder is completed
        If FMTFilesWithin.Count = CONINPUTDATA.Count _
            And (FMTFilesWithin(0) = CONINPUTDATA(0) _
                Or FMTFilesWithin(0).Contains("%1")) Then

            Return OutputLines(FMTFilesWithin, CONINPUTDATA, CON_INDEX_TOTALFILES, CON_INDEX_TOTALDIRECTORIES, ARR_FILESWITHINDIRECTORIES, "%1", "%2")

        End If

        If OutputlineIndex = 1 Then
            Return OutputLines(FMTCompNotComp, CONINPUTDATA, CON_INDEX_FILESCOMPRESSEDCOUNT, CON_INDEX_FILESNOTCOMPRESSEDCOUNT, ARR_FILESCOMPRESSED, "%3", "%4")
        End If
        If OutputlineIndex = 2 Then
            Return OutputLines(FMTTotalBytes, CONINPUTDATA, CON_INDEX_TOTALBYTESNOTCOMPRESSED, CON_INDEX_TOTALBYTESCOMPRESSED, ARR_TOTALBYTES, "%5", "%6")
        End If
        If OutputlineIndex = 3 Then
            Return OutputLines(FMTCompRatio, CONINPUTDATA, CON_INDEX_COMPRESSIONRATIO, CON_INDEX_COMPRESSIONRATIO, ARR_COMPRATIO, "%7")
        End If


        Return ("Nothing")

    End Function


    Public Function OutputLines(ByRef FMTVal As Object, ByRef CONVal As Object, ByRef CON_Index1 As Object, ByRef CON_Index2 As Object, ByRef ARRVal As Object, ByRef Val1 As String, Optional ByRef Val2 As String = "%xnull")

        Dim i = 0
        For Each c In FMTVal
            If c.Contains(Val1) Then
                FMTVal(i) = CONVal(i)
                CON_Index1 = i

            ElseIf c.Contains(Val2) Then
                FMTVal(i) = CONVal(i)
                CON_Index2 = i
            End If
            i += 1
        Next

        Dim builder As New StringBuilder
        Dim b = 0
        For Each c In FMTVal
            builder.Append(FMTVal(b))
            builder.Append(" ")
            b += 1
        Next
        ARRVal = FMTVal
        Return builder.ToString

    End Function





    Private Sub ProcessOutputLine(line As String)
        If line Is Nothing Then Return

        AppendOutputText(vbCrLf & line)                                                               'Sends output to the embedded console


        If line.Contains(CALC_OUTPUT(line).ToString.Trim(" ")) And canProceed = 0 Then               'If the output line of the console is the "%files within" line then do stuff. Trim gets rid of the spaces before and after some lines
            CON_FILESWITHINDIRECTORIESLINE = line.Trim(" ")                                              ' This variable can't get set if the first criteria fails. This means that the console output is not parsing the russian properly. 
            Console.WriteLine("Files: " +
                ARR_FILESWITHINDIRECTORIES(CON_INDEX_TOTALFILES) + " Directories: " +
                ARR_FILESWITHINDIRECTORIES(CON_INDEX_TOTALDIRECTORIES))
            canProceed = 1
        End If


        If OutputlineIndex = 1 And canProceed = 1 Then                                                  ' These all run after the one above is met, since if the one above is met then it means there's only 3 lines left. 
            CON_FILESCOMPRESSEDLINE = line
            CALC_OUTPUT(line)
            byteComparisonRawFilesCompressed = line
            Console.WriteLine("Compressed: " +
              ARR_FILESCOMPRESSED(CON_INDEX_FILESCOMPRESSEDCOUNT) + " Not Compressed: " +
              ARR_FILESCOMPRESSED(CON_INDEX_FILESNOTCOMPRESSEDCOUNT))
        End If


        If OutputlineIndex = 2 Then
            CON_TOTALBYTESLINE = line
            CALC_OUTPUT(line)
            byteComparisonRaw = line
            Console.WriteLine("Bytes Compressed: " +
                ARR_TOTALBYTES(CON_INDEX_TOTALBYTESNOTCOMPRESSED) + " In Total Bytes: " +
                ARR_TOTALBYTES(CON_INDEX_TOTALBYTESCOMPRESSED))
        End If

        If OutputlineIndex = 3 Then
            CON_COMPRATIO = line
            CALC_OUTPUT(line)

            Console.WriteLine("Ratio: " +
                ARR_COMPRATIO(CON_INDEX_COMPRESSIONRATIO) + " to 1.")

            dirCountProgress = dirCountTotal
            fileCountProgress = fileCountTotal

            canProceed = 0
            OutputlineIndex = 0
        End If


        If canProceed = 1 Then
            OutputlineIndex += 1
        End If

    End Sub






    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        loadFromSettings()
        With dirChooser
            .LinkBehavior = LinkBehavior.HoverUnderline

            .LinkColor = Color.FromArgb(37, 110, 196)
        End With

        comboChooseShutdown.SelectedItem = comboChooseShutdown.Items.Item(0)

        RCMenu.WriteLocRegistry()

        SetupScanSpinner()

        progressTimer.Start()                                                                   'Starts a timer that keeps track of changes during any operation.

        For Each arg In My.Application.CommandLineArgs
            If arg.ToString IsNot Nothing Then
                Await SelectFolder(arg, "cmdlineargs")
            End If
        Next

    End Sub


    Private Sub SetupScanSpinner()
        scanStatusLabel = New Label With {
            .AutoSize = False,
            .Location = New Point(59, 99),
            .Size = New Size(374, 16),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.25F),
            .ForeColor = Color.FromArgb(52, 152, 219),
            .Text = "Scanning folder…",
            .Visible = False
        }
        InputPage.Controls.Add(scanStatusLabel)
    End Sub


    Private Sub UpdateScanStatusLabel()
        scanStatusLabel.Text = "Scanning folder… " & scanFileCount.ToString("N0") & " files, " & scanDirCount.ToString("N0") & " folders"
    End Sub




    Private Sub ShowInfoPopup_Click(sender As Object, e As EventArgs) Handles showinfopopup.Click
        Info.semVersion.Text = "V " + version
        Info.Show()

    End Sub




    Private Sub ProgressTimer_Tick(sender As Object, e As EventArgs) Handles progressTimer.Tick

        DrainConsoleQueue()

        If isScanning Then
            UpdateScanStatusLabel()
        End If

        If fileCountTotal <> 0 Then                                                                         'Makes sure that there are actually files being counted before attempting a calculation

            If isQueryMode = 0 Then


                Try
                    If compactprogressbar.Value >= 101 Then                                                 'Avoids a /r/softwaregore scenario
                        compactprogressbar.Value = 1
                        progresspercent.Text = "100 %"
                    Else
                        compactprogressbar.Value = Math.Round _
                            ((fileCountProgress / fileCountTotal * 100), 0)

                        progresspercent.Text = Math.Round _
                            ((fileCountProgress / fileCountTotal * 100), 0).ToString + " %"                 'Generates an estimate of progress based on how many files have been processed out of the total. 

                    End If
                Catch ex As Exception
                End Try

            ElseIf isQueryMode = 1 Then


                Try
                    If compactprogressbar.Value >= 101 Then                                                 'Avoids a /r/softwaregore scenario
                        compactprogressbar.Value = 1
                        progresspercent.Text = "100 %"
                    Else
                        compactprogressbar.Value = Math.Round _
                            ((QdirCountProgress / dirCountTotal * 100), 0)

                        progresspercent.Text = Math.Round _
                            ((QdirCountProgress / dirCountTotal * 100), 0).ToString + " %"                  'Generates an estimate of progress for the Query command.

                    End If
                Catch ex As Exception
                End Try

            End If


        End If

    End Sub




    Private Sub AppendOutputText(ByVal text As String)                                           'Queue console output for the UI timer to drain
        If text <> vbCrLf Then
            consoleQueue.Enqueue(text)
        End If
    End Sub


    Private Sub DrainConsoleQueue()
        If consoleQueue.IsEmpty Then Return

        Dim line As String = Nothing
        Dim drained As Integer = 0
        While drained < CONSOLE_DRAIN_PER_TICK AndAlso consoleQueue.TryDequeue(line)
            consoleLogBuffer.Add(line)
            If checkShowConOut.Checked Then
                consoleDirtyLines += 1
            End If
            drained += 1
        End While

        If checkShowConOut.Checked AndAlso consoleDirtyLines >= CONSOLE_REBUILD_EVERY Then
            RebuildConsoleWindow()
            consoleDirtyLines = 0
        End If
    End Sub


    Private Sub RebuildConsoleWindow()
        conOut.BeginUpdate()
        Try
            conOut.Items.Clear()
            Dim start As Integer = Math.Max(0, consoleLogBuffer.Count - MAX_CONSOLE_LINES)
            For i As Integer = start To consoleLogBuffer.Count - 1
                conOut.Items.Add(consoleLogBuffer(i))
            Next
        Finally
            conOut.EndUpdate()
        End Try

        If conOut.Items.Count > 0 Then
            conOut.TopIndex = conOut.Items.Count - 1
        End If
    End Sub


    Private Sub ClearConsole()
        Dim line As String = Nothing
        While consoleQueue.TryDequeue(line)
        End While
        consoleLogBuffer.Clear()
        conOut.Items.Clear()
        consoleDirtyLines = 0
    End Sub




    'Set variables for the advanced compression checkboxes. 
    Dim workingDir As String = ""
    Dim recursiveScan As String = ""
    Dim hiddenFiles As String = ""
    Dim forceCompression As String = ""                                                         'Not actually implemented - yet


    'Set variables for minor security and error handling
    Dim overrideCompressFolderButton = 0
    Dim directorysizeexceptionCount = 0                                                         'Used in MeasureFolder() to ensure the permission error only shows up once, even if multiple UnauthorizedAccessException errors get thrown


    Dim uncompressedfoldersize

    'Folder-scan state: the scan runs on a background thread so the UI stays responsive.
    'The status label shows live file/folder counts as the walk progresses.
    Private WithEvents scanStatusLabel As Label
    Private isScanning As Boolean = False
    Private scanFileCount As Long = 0
    Private scanDirCount As Long = 0
    Private scanCts As CancellationTokenSource

    Private Class FolderMetrics
        Public TotalSize As Long
        Public FileCount As Long
        Public DirCount As Long
    End Class


    Private Async Sub SelectFolderToCompress _
        (sender As Object, e As EventArgs) Handles dirChooser.LinkClicked, chosenDirDisplay.Click

        overrideCompressFolderButton = 0

        Dim folderChoice As New VistaFolderBrowserDialog

        folderChoice.ShowDialog()

        Await SelectFolder(folderChoice.SelectedPath, "button")

        folderChoice.Dispose()

    End Sub

    Dim dirLabelResults As String = ""

    Private Async Function SelectFolder(selectedDir As String, senderID As String) As Task
        Dim wDString = selectedDir

        If wDString.Contains("C:\Windows") Then                                                 'Makes sure you're not trying to compact the Windows directory. I should Regex this to catch all possible drives hey?

            MsgBox("Compressing items in the Windows folder using this program " _
                    & "is not recommended. Please use the command line if you still " _
                    & "wish to compress the Windows folder")
            Return

        ElseIf wDString.EndsWith(":\") Then

            MsgBox("Compressing an entire drive with this tool is not allowed")
            Return

        End If

        If wDString.Length < 3 Then                                                     'Makes sure the chosen folder isn't a null value or an exception
            If senderID = "button" Then Console.Write("No folder selected")
            Return
        End If

        'Abort any in-flight scan so a newly chosen folder takes over immediately.
        If scanCts IsNot Nothing Then
            scanCts.Cancel()
        End If
        scanCts = New CancellationTokenSource
        Dim ct = scanCts.Token

        Dim DIwDString = New DirectoryInfo(wDString)
        directorysizeexceptionCount = 0
        workingDir = wDString.ToString()

        'Indicate that the folder is being scanned without blocking the message loop.
        isScanning = True
        scanFileCount = 0
        scanDirCount = 0
        Me.UseWaitCursor = True
        chosenDirDisplay.Text = DIwDString.Parent.ToString + " ❯ " + DIwDString.Name.ToString
        scanStatusLabel.Text = "Scanning folder… 0 files, 0 folders"
        scanStatusLabel.Visible = True
        buttonCompress.Enabled = False
        buttonQueryCompact.Visible = False
        seecompest.Visible = False

        Dim metrics As FolderMetrics
        Try
            'Walk the tree once on a background thread and reuse the result everywhere.
            'The original code enumerated the folder five separate times on the UI thread,
            'which froze the window for several seconds on large folders.
            metrics = Await Task.Run(Function() MeasureFolderMetrics(DIwDString, ct))
        Catch ex As Exception
            metrics = New FolderMetrics
        End Try

        'Back on the UI thread. Stop here if the window closed or a newer scan
        'cancelled this one (the newer scan owns the status label now).
        If Me.IsDisposed OrElse Me.Disposing Then Return
        If ct.IsCancellationRequested Then Return

        Me.UseWaitCursor = False
        scanStatusLabel.Visible = False
        isScanning = False

        uncompressedfoldersize = metrics.TotalSize
        fileCountTotal = metrics.FileCount
        dirCountTotal = metrics.DirCount + 1

        preSize.Text = "Uncompressed Size: " + GetOutputSize(metrics.TotalSize, True)

        dirLabelResults = DIwDString.Name.ToString

        'preSize.Visible = True
        seecompest.Visible = True
        buttonQueryCompact.Visible = True

        Form2.lblCompactIssues.Visible = False
        WikiHandler.localFolderParse(wDString, DIwDString, metrics.TotalSize)

        If overrideCompressFolderButton = 0 Then                                        'Used as a security measure to stop accidental compression of folders that should not be compressed - even though the compact.exe process will throw an error if you try, I'd prefer to catch it here anyway. 
            buttonCompress.Enabled = True
        Else
            buttonCompress.Enabled = False
        End If
    End Function


    Private Async Sub CompressFolder_Click(sender As System.Object, e As System.EventArgs) Handles buttonCompress.Click
        ClearConsole()
        Await RunCompressionAsync()
    End Sub
    Private Async Sub buttonQueryCompact_Click(sender As Object, e As EventArgs) Handles buttonQueryCompact.Click
        ClearConsole()
        Await RunQueryAsync()
    End Sub




    'Runs one compact.exe operation on a background thread, waiting for completion
    'and returning False when the operation was cancelled.
    Private Async Function RunOperationAsync(operation As String) As Task(Of Boolean)
        operationCts = New CancellationTokenSource
        Dim ct = operationCts.Token
        Try
            Return Await Task.Run(Function() ExecuteCompactOperation(operation, ct))
        Catch ex As Exception
            'Cancellation kills the process and ends its pipes; that is expected and
            'not an error. Only surface genuinely unexpected failures to the user.
            If Not ct.IsCancellationRequested Then
                MessageBox.Show("Could not run compact.exe: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
            Return False
        Finally
            operationCts = Nothing
        End Try
    End Function


    'Launches compact.exe directly (no CMD wrapper), reads stdout/stderr on the calling
    'thread and cancels by killing the process tree via the CancellationToken.
    Private Function ExecuteCompactOperation(operation As String, ct As CancellationToken) As Boolean
        Dim args = BuildCompactArgs(operation)
        Dim oemEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)

        Dim proc = New Process()
        With proc.StartInfo
            .FileName = "compact.exe"
            .Arguments = args
            .UseShellExecute = False
            .CreateNoWindow = True
            .RedirectStandardOutput = True
            .RedirectStandardError = True
            .WorkingDirectory = workingDir
            .StandardOutputEncoding = oemEncoding
            .StandardErrorEncoding = oemEncoding
        End With

        Using reg = ct.Register(Sub() KillProcess(proc))
            If ct.IsCancellationRequested Then
                KillProcess(proc)
                Return False
            End If

            proc.Start()
            Try
                proc.PriorityClass = ProcessPriorityClass.BelowNormal
            Catch ex As Exception
            End Try

            'Drain stderr on a separate thread so compact.exe never blocks on a full error buffer.
            Dim stderrTask As Task = Task.Run(Sub()
                                                  Dim errLine As String
                                                  Do
                                                      errLine = proc.StandardError.ReadLine()
                                                      If errLine Is Nothing Then Exit Do
                                                      AppendOutputText(vbCrLf & errLine)
                                                  Loop
                                              End Sub)

            Dim line As String
            Do
                line = proc.StandardOutput.ReadLine()
                If line Is Nothing Then Exit Do
                ProcessOutputLine(line)
            Loop

            Try
                proc.WaitForExit()
            Catch ex As Exception
            End Try

            Try
                stderrTask.Wait(TimeSpan.FromSeconds(2))
            Catch ex As Exception
            End Try
        End Using

        Return Not ct.IsCancellationRequested
    End Function


    Private Sub KillProcess(proc As Process)
        Try
            If proc IsNot Nothing AndAlso Not proc.HasExited Then
                proc.Kill()
            End If
        Catch ex As Exception
        End Try
    End Sub


    Private Function BuildCompactArgs(operation As String) As String
        Select Case operation
            Case "compact"
                Dim args = "/C"
                If checkRecursiveScan.Checked Then args += " /S"
                If checkForceCompression.Checked Then args += " /F"
                If checkHiddenFiles.Checked Then args += " /A"
                If compressX4.Checked Then args += " /EXE:XPRESS4K"
                If compressX8.Checked Then args += " /EXE:XPRESS8K"
                If compressX16.Checked Then args += " /EXE:XPRESS16K"
                If compressLZX.Checked Then args += " /EXE:LZX"
                Return args
            Case "uncompact"
                Dim args = "/U /S /EXE"
                If checkForceCompression.Checked Then args += " /F"
                If checkHiddenFiles.Checked Then args += " /A"
                Return args
            Case "query"
                Return "/S /Q /EXE"
            Case Else
                Return ""
        End Select
    End Function


    Private Sub ResetOperationProgress()
        fileCountProgress = 0
        dirCountProgress = 0
        QdirCountProgress = 0
        OutputlineIndex = 0
        canProceed = 0
    End Sub


    Private Async Function RunCompressionAsync() As Task
        isQueryMode = 0
        isQueryCalledByCompact = 1
        hasqueryfinished = 0
        isActive = 1
        ResetOperationProgress()
        progresspercent.Visible = True
        CompResultsPanel.Visible = False
        buttonRevert.Visible = False
        returnArrow.Visible = False
        progressPageLabel.Text = "Compressing, Please Wait"
        TabControl1.SelectedTab = ProgressPage

        If Not Await RunOperationAsync("compact") Then
            AbortOperation()
            Return
        End If

        'Compression finished: offer the shutdown countdown while the post-compress query runs.
        If checkShutdownOnCompletion.Checked Then
            ShutdownDialog.SDProcIntent.Text = comboChooseShutdown.Text
            FadeTransition.FadeForm(ShutdownDialog, 0, 0.98, 300, True)
        End If

        isQueryMode = 1
        ResetOperationProgress()
        progressPageLabel.Text = "Analyzing..."

        If Not Await RunOperationAsync("query") Then
            AbortOperation()
            Return
        End If

        isActive = 0
        hasqueryfinished = 1
        buttonRevert.Visible = True
        returnArrow.Visible = True
        CalculateSaving()
        QdirCountProgress = 0
    End Function


    Private Async Function RunQueryAsync() As Task
        isQueryMode = 1
        isQueryCalledByCompact = 0
        hasqueryfinished = 0
        isActive = 0
        ResetOperationProgress()
        progresspercent.Visible = True
        progressPageLabel.Text = "Analyzing"
        TabControl1.SelectedTab = ProgressPage

        If Not Await RunOperationAsync("query") Then
            AbortOperation()
            Return
        End If

        progresspercent.Visible = False
        buttonRevert.Visible = True
        returnArrow.Visible = True
        CalculateSaving()
        QdirCountProgress = 0
    End Function


    Private Async Function RunUncompressAsync() As Task
        isQueryMode = 0
        isQueryCalledByCompact = 0
        hasqueryfinished = 0
        isActive = 1
        ResetOperationProgress()
        fileCountProgress = 0
        dirCountProgress = 0
        progresspercent.Visible = True
        CompResultsPanel.Visible = False
        buttonRevert.Visible = False
        returnArrow.Visible = False
        progressPageLabel.Text = "Reverting Changes, Please Wait"
        TabControl1.SelectedTab = ProgressPage

        If Not Await RunOperationAsync("uncompact") Then
            AbortOperation()
            Return
        End If

        isActive = 0
        buttonCompress.Visible = True
        buttonRevert.Visible = False
        progressPageLabel.Text = "Folder Uncompressed."
        returnArrow.Visible = True

        If checkShutdownOnCompletion.Checked Then
            ShutdownDialog.SDProcIntent.Text = comboChooseShutdown.Text
            FadeTransition.FadeForm(ShutdownDialog, 0, 0.98, 200, True)
        End If
    End Function


    Private Sub AbortOperation()
        isActive = 0
        isQueryMode = 0
        isQueryCalledByCompact = 0
        hasqueryfinished = 0
        buttonRevert.Visible = False
        returnArrow.Visible = False
        CompResultsPanel.Visible = False
        progresspercent.Visible = False
        TabControl1.SelectedTab = InputPage
    End Sub


    Private Sub CancelOperation()
        If operationCts IsNot Nothing Then
            operationCts.Cancel()
        End If
    End Sub


    Private Sub CancelScan()
        If scanCts IsNot Nothing Then
            scanCts.Cancel()
        End If
    End Sub


    Private Async Sub ButtonRevert_Click(sender As Object, e As EventArgs) Handles buttonRevert.Click             'Handles uncompressing. For now, uncompressing can only be done through the program only to revert a compression that's just been done.
        ClearConsole()
        Await RunUncompressAsync()
    End Sub




    Private Sub compressLZX_CheckedChanged(sender As Object, e As EventArgs) Handles compressLZX.CheckedChanged     'Cautions the user if they're about to use LZX compression

        If compressLZX.Checked = True Then

            If MsgBox("LZX is recommended only for folders that are not going to be used very often. Do not use this on program or game folders!" _
                      & vbCrLf & vbCrLf & "Do you wish to continue?", MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.No Then

                compressX8.Checked = True

            End If

        End If
    End Sub




    Private Sub ReturnArrow_Click(sender As Object, e As EventArgs) Handles returnArrow.Click                       'Returns you to the first screen and cleans up some stuff

        returnArrow.Visible = False
        buttonRevert.Visible = False
        CompResultsPanel.Visible = False
        checkShutdownOnCompletion.Checked = False
        TabControl1.SelectedTab = InputPage
        dirCountProgress = 0
        fileCountProgress = 0
        isQueryCalledByCompact = 0
        CancelOperation()
    End Sub




    Private Sub CheckShowConOut_CheckedChanged(sender As Object, e As EventArgs) Handles checkShowConOut.CheckedChanged     'Handles showing the embedded console
        If checkShowConOut.Checked Then
            conOut.Visible = True
            saveconlog.Visible = True
            RebuildConsoleWindow()
            consoleDirtyLines = 0
        Else
            conOut.Visible = False
            saveconlog.Visible = False
        End If
    End Sub





    Private Sub MyForm_Closing(ByVal sender As System.Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        If isActive = 1 Then

            If MessageBox.Show _
                ("Are you sure you want to exit?" & vbCrLf & vbCrLf & "Quitting while the Compact function is running is potentially dangerous." _
                 & "Continuing to close could lead to one of your files becoming stuck in a semi-compressed state." _
                 & vbCrLf & vbCrLf &
                 "If you do decide to force quit now, you can potentially fix any unreadable files by running Compact again," _
                 & "selecting the 'Force Compression' Checkbox and then running uncompress on the folder." & vbCrLf & "Click Yes to continue exiting the program.",
                 "Warning!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then

                e.Cancel = True

            Else
                CancelOperation()
                CancelScan()
            End If
        Else
            CancelOperation()
            CancelScan()
        End If

    End Sub




    Private Sub dirChooser_MouseEnter(sender As Object, e As EventArgs) Handles dirChooser.MouseEnter
        dirChooser.LinkColor = Color.FromArgb(10, 80, 150)
    End Sub
    Private Sub dirChooser_MouseLeave(sender As Object, e As EventArgs) Handles dirChooser.MouseLeave
        dirChooser.LinkColor = Color.FromArgb(37, 110, 196)
    End Sub







    '/////////////FUNCTIONS//////////////

    Private Sub CalculateSaving()   'Calculations for all the relevant information after compression is completed. All the data is parsed from the console ouput using basic strings, but because that occurs on a different thread, information is stored to variables first (The Status Monitors at the top) then those values are used. 

        Dim numberFilesCompressed = 0
        Dim querySize As Int64 = 0


        'If isQueryMode = 0 Then querySize = Long.Parse(Regex.Replace(ARR_TOTALBYTES(CON_INDEX_TOTALBYTESNOTCOMPRESSED), "[^\d]", ""))


        Dim oldFolderSize As Long = 999999999

        Dim newFolderSize As Long = 999999999
        Try
            oldFolderSize = Long.Parse(Regex.Replace(ARR_TOTALBYTES(CON_INDEX_TOTALBYTESNOTCOMPRESSED), "[^\d]", ""))
        Catch ex As Exception

        End Try

        Try
            newFolderSize = Long.Parse(Regex.Replace(ARR_TOTALBYTES(CON_INDEX_TOTALBYTESCOMPRESSED), "[^\d]", ""))
        Catch ex As Exception

        End Try


        Try
            numberFilesCompressed = Long.Parse(Regex.Replace(ARR_FILESCOMPRESSED(CON_INDEX_FILESCOMPRESSEDCOUNT), "[^\d]", ""))
        Catch ex As Exception
        End Try

        If GetOutputSize((oldFolderSize - newFolderSize), False) = "0" And isQueryMode = 1 Then

            progressPageLabel.Text = "Folder is not compressed"
            buttonRevert.Visible = False
            isQueryCalledByCompact = 0

        Else

            progressPageLabel.Text = "Folder is compressed"

            If isQueryMode = 1 And isQueryCalledByCompact = 0 Then
                origSizeLabel.Text = GetOutputSize(oldFolderSize, True)
            Else
                origSizeLabel.Text = GetOutputSize(uncompressedfoldersize, True)
            End If

            compressedSizeLabel.Text = GetOutputSize _
                (uncompressedfoldersize - (oldFolderSize - newFolderSize), True)

            compRatioLabel.Text = Math.Round _
                (uncompressedfoldersize / (uncompressedfoldersize - (oldFolderSize - newFolderSize)), 1)

            spaceSavedLabel.Text = GetOutputSize _
                ((oldFolderSize - newFolderSize), True) + " Saved"

            dirChosenLabel.Text = "❯ In " + dirLabelResults

            labelFilesCompressed.Text =
                numberFilesCompressed.ToString + " / " + fileCountTotal.ToString + " files compressed"

            Try

                compressedSizeVisual.Width = CInt(368 / compRatioLabel.Text)

                If hasqueryfinished = 1 Then
                    isQueryCalledByCompact = 0
                    isQueryMode = 0
                    buttonRevert.Visible = True
                End If

            Catch ex As System.OverflowException
                compressedSizeVisual.Width = 368
            End Try

            If isQueryCalledByCompact = 0 Then

                CompResultsPanel.Visible = True


            ElseIf isQueryCalledByCompact = 1 Then

                progressPageLabel.Text = "Analyzing..."

                buttonRevert.Visible = False
                CompResultsPanel.Visible = False


            End If

        End If

        If isQueryCalledByCompact = 0 Then isQueryMode = 0

    End Sub


    Dim hasqueryfinished = 0




    Public Function GetOutputSize(ByVal inputsize As Decimal, Optional ByVal showSizeType As Boolean = False) As String            'Function for converting from Bytes into various units
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
            Return Math.Round(inputsize, 1) & sizeType
        Else
            Return Math.Round(inputsize, 1)
        End If

    End Function



    'Walks a directory tree once, accumulating total size, file count and directory
    'count in a single pass. Inaccessible subfolders are skipped (and reported once)
    'rather than aborting the whole scan as Directory.GetFiles/GetDirectories did.
    Private Sub MeasureFolder(ByVal dInfo As IO.DirectoryInfo, ByRef totalSize As Long, ByRef fileCount As Long, ByRef dirCount As Long, ct As CancellationToken)
        If ct.IsCancellationRequested Then Return

        Try
            For Each file As FileInfo In dInfo.EnumerateFiles()
                If ct.IsCancellationRequested Then Return
                totalSize += file.Length
                fileCount += 1
                'Publish a throttled live snapshot while a single directory is being counted.
                If (fileCount And 511) = 0 Then
                    scanFileCount = fileCount
                    scanDirCount = dirCount
                End If
            Next
        Catch ex As UnauthorizedAccessException
            ReportFolderAccessError()
            Return
        Catch ex As Exception
            Return
        End Try

        Try
            For Each dir As DirectoryInfo In dInfo.EnumerateDirectories()
                If ct.IsCancellationRequested Then Return
                dirCount += 1
                MeasureFolder(dir, totalSize, fileCount, dirCount, ct)
            Next
        Catch ex As UnauthorizedAccessException
            ReportFolderAccessError()
        Catch ex As Exception
        End Try

        'Publish a live snapshot so the UI timer can show the running counts.
        If Not ct.IsCancellationRequested Then
            scanFileCount = fileCount
            scanDirCount = dirCount
        End If
    End Sub


    Private Function MeasureFolderMetrics(ByVal dInfo As IO.DirectoryInfo, ct As CancellationToken) As FolderMetrics
        Dim m As New FolderMetrics
        MeasureFolder(dInfo, m.TotalSize, m.FileCount, m.DirCount, ct)
        Return m
    End Function


    'Called from the background scan thread; the dialog itself is marshalled to the UI thread.
    Private Sub ReportFolderAccessError()
        directorysizeexceptionCount += 1

        If directorysizeexceptionCount = 1 Then

            overrideCompressFolderButton = 1
            directorysizeexceptionCount += 1

            If Me.InvokeRequired Then
                Me.BeginInvoke(Sub() ShowFolderAccessDialog())
            Else
                ShowFolderAccessDialog()
            End If

        End If
    End Sub


    Private Sub ShowFolderAccessDialog()
        If My.User.IsInRole(ApplicationServices.BuiltInRole.Administrator) = False Then
            If MessageBox.Show("This directory contains a subfolder that you do not have permission to access. Please try running the program again as an Administrator." _
                 & vbCrLf & vbCrLf & "If the problem persists, the subfolder is most likely protected by the System, and by design this program will refuse to let you proceed." _
                & vbCrLf & vbCrLf & " Would you like to restart the program as an Administrator?", "Permission Error", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) = DialogResult.Yes Then

                RCMenu.RunAsAdmin()
                Me.Close()

            End If

        Else
            MsgBox("This directory contains a subfolder that you do not have permission To access." _
               & vbCrLf & vbCrLf & "The subfolder is most likely protected by the System, and by design this program will refuse to let you proceed.")
        End If
    End Sub




    '//////////////FORMAT MESSAGES FROM MUITABLE FOR LOCALISATION///////////////////////////////////////////

    <DllImport("Kernel32.dll", EntryPoint:="FormatMessageW",
               SetLastError:=True, CharSet:=CharSet.Unicode, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function FormatMessage(
        ByVal dwFlags As Integer,
        ByVal lpSource As Integer,
        ByVal dwMessageId As Integer,
        ByVal dwLanguageId As Integer,
        <MarshalAs(UnmanagedType.LPWStr)> ByRef lpBuffer As String,
        ByVal nSize As Integer,
        ByRef Arguments As IntPtr) As Integer
    End Function


    <DllImport("kernel32.dll")>
    Private Shared Function LoadLibraryEx(
        lpFileName As String,
        hReservedNull As IntPtr,
        dwFlags As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function LoadLibraryA(
        lpFileName As String) As IntPtr
    End Function

    Private Const FORMAT_MESSAGE_FROM_HMODULE As Long = &H800


    Public Function GetMessageFromModule(
        ByRef strModuleName As String,
        ByVal msgID As Long) As String

        Dim rt As Long
        Dim bufferStr As String
        Dim hModule As Integer

        'hModule = LoadLibraryEx("kernel32.dll", IntPtr.Zero, &H2)
        hModule = LoadLibraryA(strModuleName)

        If hModule <> 0 Then
            bufferStr = Space(12)
            Try
                rt = FormatMessage(FORMAT_MESSAGE_FROM_HMODULE Or &H100 Or &H200,
                       hModule, msgID, 0&, bufferStr, Len(bufferStr), 0&)
            Catch ex As Exception
            End Try

            If rt Then
                bufferStr = Microsoft.VisualBasic.Left$(bufferStr, rt)
                Return bufferStr
            End If

        End If

        Return String.Empty

    End Function


    '/////END FORMAT MESSAGES FROM MUI///////////////////////////////////////////


    '///////EXTRA FUNCTIONS/////////////

    Function Between(value As String, a As String, b As String) As String

        Dim posA As Integer = value.IndexOf(a)
        Dim posB As Integer = value.LastIndexOf(b)
        If posA = -1 Then
            Return ""
        End If
        If posB = -1 Then
            Return ""
        End If

        Dim adjustedPosA As Integer = posA + a.Length
        If adjustedPosA >= posB Then
            Return ""
        End If

        Return value.Substring(adjustedPosA, posB - adjustedPosA)

    End Function


    Function Before(value As String, a As String) As String

        Dim posA As Integer = value.IndexOf(a)
        If posA = -1 Then
            Return ""
        End If
        Return value.Substring(0, posA)
    End Function


    Function After(value As String, a As String) As String

        Dim posA As Integer = value.LastIndexOf(a)
        If posA = -1 Then
            Return ""
        End If
        Dim adjustedPosA As Integer = posA + a.Length
        If adjustedPosA >= value.Length Then
            Return ""
        End If
        Return value.Substring(adjustedPosA)
    End Function



    '////////////////////TESTING////////////////////



    Private Sub Saveconlog_Click(sender As Object, e As EventArgs) Handles saveconlog.Click
        If MsgBox("Save console output?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
            Dim sb As New System.Text.StringBuilder()

            sb.AppendLine("CompactGUI: Log at " & System.DateTime.Now & vbCrLf _
                          & "//////////////////////////////////////////////////////////////////////////////////" _
                          & "//////////////////////////////////////////////////////////////////////////////////")

            For Each ln As String In consoleLogBuffer
                sb.AppendLine(ln)
            Next

            sb.AppendLine("End Log at " & System.DateTime.Now & vbCrLf _
                          & "//////////////////////////////////////////////////////////////////////////////////" _
                          & "//////////////////////////////////////////////////////////////////////////////////" & vbCrLf & vbCrLf)

            System.IO.File.WriteAllText(Application.StartupPath & "\CompactGUILog.txt", sb.ToString)

            MsgBox("Saved log to " & Application.StartupPath & "\CompactGUILog.txt")
        End If
    End Sub




    Private Sub seecompest_MouseHover(sender As Object, e As EventArgs) Handles seecompest.MouseHover
        WikiHandler.showWikiRes()
        isAlreadyFading = 0

    End Sub

    Dim isAlreadyFading = 2
    Private Sub hideWikiRes(sender As Object, e As EventArgs) Handles MyBase.MouseEnter, TabControl1.MouseEnter,
                                InputPage.MouseEnter, FlowLayoutPanel1.MouseEnter, Panel3.MouseEnter, Panel4.MouseEnter
        If isAlreadyFading = 0 Then
            FadeTransition.FadeForm(Form2, 0.96, 0, 200)
            isAlreadyFading = 1
        End If




    End Sub


    Private Sub submitToWiki_Click(sender As Object, e As EventArgs) Handles submitToWiki.Click
        Process.Start("https://github.com/Endymi0n74/compactWindows/issues")
    End Sub

    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles comboChooseShutdown.SelectedIndexChanged
        InputPage.Focus()

    End Sub

    Private Sub ComboBox1_MouseLeave(sender As Object, e As EventArgs) Handles comboChooseShutdown.MouseLeave
        InputPage.Focus()

    End Sub

    Private Sub compressX8_CheckedChanged(sender As Object, e As EventArgs) Handles compressX4.Click, compressX8.Click, compressX16.Click, compressLZX.Click

        If compressX4.Checked Then My.Settings.SavedCompressionOption = 0
        If compressX8.Checked Then My.Settings.SavedCompressionOption = 1
        If compressX16.Checked Then My.Settings.SavedCompressionOption = 2
        If compressLZX.Checked Then My.Settings.SavedCompressionOption = 3

    End Sub

    Private Sub loadFromSettings()

        If My.Settings.SavedCompressionOption = 0 Then compressX4.Checked = True
        If My.Settings.SavedCompressionOption = 1 Then compressX8.Checked = True
        If My.Settings.SavedCompressionOption = 2 Then compressX16.Checked = True
        If My.Settings.SavedCompressionOption = 3 Then compressLZX.Checked = True

    End Sub



End Class
