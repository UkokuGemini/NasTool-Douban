Imports System.IO
Imports System.Net
Imports System.Reflection.Emit
Imports System.Runtime.CompilerServices.RuntimeHelpers
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Xml
Imports Microsoft.VisualBasic.Devices
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Newtonsoft.Json.Serialization

Public Class MainForm
    Structure MInfos
        Dim M_Name As String
        Dim M_ID As String
    End Structure
    Structure OutputInfos
        Dim ID As Integer
        Dim NAME As String
        Dim YEAR As String
        Dim TMDBID As String
        Dim IMAGE As String
        Dim NOTE As String
    End Structure
    Dim Config_DB_Path As String = Directory.GetCurrentDirectory & "\config\user.db"
    Dim Config_DoubanID As String
    Dim Config_TMDB_API As String
    Dim Config_Douban_SynDays As Integer
    Dim Config_RandomSleep As Boolean
    Dim Douban_Deadline As Date
    Dim DownLoad_Arr, Rss_Arr, RssID_Arr As New ArrayList
    Dim NewMovieAddArr As New ArrayList
    Dim GoGetThread As Thread
    Dim DoubanList As New ArrayList
    Dim NumMark As Integer = 0
    Dim RandomTMDBID As Integer = 575602
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = My.Application.Info.AssemblyName & "[" & My.Application.Info.Version.ToString & "]"
        Me.CenterToScreen()
        CheckForIllegalCrossThreadCalls = False
        System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = False
        ReadSettingXml()
        DataBaseConnection.ConnectionString = "Data Source=" & Config_DB_Path
        ReadDB(Config_DB_Path)
    End Sub
    Sub CheckWebSite()
        RichTextBox_Log.AppendText(vbCrLf & TestWebsite("https://movie.douban.com/", "👁️‍🗨️[连通性测试]豆瓣网页:"))
        RichTextBox_Log.AppendText(TestWebsite("https://mouban.mythsman.com/guest/check_user?id=" & Config_DoubanID, "👁️‍🗨️[连通性测试]豆瓣API:"))
        RichTextBox_Log.AppendText(TestWebsite("https://www.themoviedb.org/search?query=" & Config_TMDB_API, "👁️‍🗨️[连通性测试]TMDB_Search:"))
        RichTextBox_Log.AppendText(TestWebsite("https://api.themoviedb.org/3/movie/" & RandomTMDBID & "?api_key=" & Config_TMDB_API, "👁️‍🗨️[连通性测试]TMDB_API:"))
        ToolStripStatusLabel1.Text = "连通性检测完毕."
    End Sub
    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Try
            GoGetThread.Abort()
            System.Environment.Exit(0)
        Catch ex As Exception
        End Try
    End Sub
    Private Sub ToolStripLabel1_Click(sender As Object, e As EventArgs) Handles ToolStripLabel1.Click
        NumMark = 0
        NewMovieAddArr.Clear()
        DoubanList = FreshDoubanWish(Config_DoubanID)
        GoGetThread = New Thread(AddressOf GoGet)
        GoGetThread.Start()
    End Sub
    Private Sub ToolStripLabel2_Click(sender As Object, e As EventArgs) Handles ToolStripLabel2.Click
        ToolStripLabel2.Enabled = False
        If NewMovieAddArr.Count > 0 Then
            Dim FailNum As Integer = 0
            For i = 0 To NewMovieAddArr.Count - 1
                Dim Temp_Out As OutputInfos = NewMovieAddArr(i)
                Dim sqlcommandStr As String = "INSERT INTO RSS_MOVIES VALUES(" &
                    GetMinId() & ",'" &
                  Temp_Out.NAME & "','" &
                  Temp_Out.YEAR & "','','" &
                  Temp_Out.TMDBID & "','" &
                  Temp_Out.IMAGE & "','[]','[]',0,'','','','','','','',0,'S','','" &
                  Temp_Out.NOTE & "')"
                Try
                    SQLDataBaseQeury(sqlcommandStr, DataBaseConnection)
                    If ResRQeury = "" Then
                    Else
                        RichTextBox_Log.AppendText("⛔添加订阅错误" & ResRQeury.ToString & vbCrLf)
                        FailNum += 1
                    End If
                Catch ex As Exception
                    FailNum += 1
                End Try
            Next
            ToolStripStatusLabel1.Text = "添加订阅完成:总计:" & NewMovieAddArr.Count & "[成功" & NewMovieAddArr.Count - FailNum & "/失败:" & FailNum & "]"
            RichTextBox_Log.AppendText(vbCrLf & ToolStripStatusLabel1.Text & vbCrLf)
            ReadDB(Config_DB_Path)
            NewMovieAddArr.Clear()
        Else
            ToolStripStatusLabel1.Text = "没有要添加订阅的项目."
        End If
    End Sub
    Private Sub ToolStripLabel3_Click(sender As Object, e As EventArgs) Handles ToolStripLabel3.Click
        Diagnostics.Process.Start(System.Environment.CurrentDirectory & "\")
    End Sub
    Private Sub ToolStripLabel4_Click(sender As Object, e As EventArgs) Handles ToolStripLabel4.Click
        If GoGetThread.IsAlive Then
            GoGetThread.Abort()
            ToolStripLabel4.Text = "继续解析[" & NumMark & "/" & DoubanList.Count & "]"
        Else
            GoGetThread = New Thread(AddressOf GoGet)
            GoGetThread.Start()
            ToolStripLabel4.Text = "暂停解析."
        End If
    End Sub
#Region "获取网页源码"
    Function GetWebCode(ByVal strURL As String) As String
        Dim httpReq As System.Net.HttpWebRequest
        Dim httpResp As System.Net.HttpWebResponse
        Dim httpURL As New System.Uri(strURL)
        Dim ioS As System.IO.Stream, charSet As String, tCode As String
        Dim k() As Byte
        ReDim k(0)
        Dim dataQue As New Queue(Of Byte)
        httpReq = CType(WebRequest.Create(httpURL), HttpWebRequest)
        Dim sTime As Date = CDate("1990-09-21 00:00:00")
        httpReq.IfModifiedSince = sTime
        httpReq.Method = "GET"
        httpReq.Timeout = 7000
        Try
            httpResp = CType(httpReq.GetResponse(), HttpWebResponse)
        Catch
            GetWebCode = "<Error:Nothing>" : Exit Function
        End Try
        '以上为网络数据获取
        ioS = CType(httpResp.GetResponseStream, Stream)
        Do While ioS.CanRead = True
            Try
                dataQue.Enqueue(ioS.ReadByte)
            Catch
                Exit Do
            End Try
        Loop
        ReDim k(dataQue.Count - 1)
        For j As Integer = 0 To dataQue.Count - 1
            k(j) = dataQue.Dequeue
        Next
        '以上，为获取流中的的二进制数据
        tCode = Encoding.GetEncoding("UTF-8").GetString(k) '获取特定编码下的情况，毕竟UTF-8支持英文正常的显示
        charSet = Replace(GetByDiv2(tCode, "charset=", """"), """", "") '进行编码类型识别
        '以上，获取编码类型
        If charSet = "" Then 'defalt
            If httpResp.CharacterSet = "" Then
                tCode = Encoding.GetEncoding("UTF-8").GetString(k)
            Else
                tCode = Encoding.GetEncoding(httpResp.CharacterSet).GetString(k)
            End If
        Else
            tCode = Encoding.GetEncoding(charSet).GetString(k)
        End If
        Debug.Print(charSet)
        'Stop
        '以上，按照获得的编码类型进行数据转换
        '将得到的内容进行最后处理，比如判断是不是有出现字符串为空的情况
        GetWebCode = tCode
        If tCode = "" Then GetWebCode = "<Error:Nothing>"
    End Function 'Tools
    Function GetByDiv2(ByVal code As String, ByVal divBegin As String, ByVal divEnd As String)  '获取分隔符所夹的内容[完成，未测试]
        '仅用于获取编码数据
        Dim lgStart As Integer
        Dim lens As Integer
        Dim lgEnd As Integer
        lens = Len(divBegin)
        If InStr(1, code, divBegin) = 0 Then GetByDiv2 = "" : Exit Function
        lgStart = InStr(1, code, divBegin) + CInt(lens)

        lgEnd = InStr(lgStart + 1, code, divEnd)
        If lgEnd = 0 Then GetByDiv2 = "" : Exit Function
        GetByDiv2 = Mid(code, lgStart, lgEnd - lgStart)
    End Function
#End Region
#Region "SQL"
    Dim ResRQeury As String
    Friend DataBaseConnection As New SQLite.SQLiteConnection
    Friend DataBaseDispose As Boolean = False
    Private ReadOnly DataBaseCommand As New SQLite.SQLiteCommand
    Public Function SQLDataBaseQeury(ByVal SQLCommand As String, ByVal DataBaseConnection As SQLite.SQLiteConnection) As DataSet
        ResRQeury = ""
        If DataBaseDispose = False Then
            Dim DataSetTemp As New DataSet
            Try
                If DataBaseConnection.State = System.Data.ConnectionState.Closed Then
                    DataBaseConnection.Open()
                End If
                DataBaseCommand.Connection = DataBaseConnection
                DataBaseCommand.CommandText = SQLCommand
                Dim DataBaseAdapter As New SQLite.SQLiteDataAdapter(DataBaseCommand)
                DataBaseAdapter.Fill(DataSetTemp)
                If DataBaseConnection.State = System.Data.ConnectionState.Open Then
                    DataBaseConnection.Close()
                End If
                Return DataSetTemp
                DataSetTemp.Dispose()
            Catch Ex As Exception
                Dim NullDataSet As New DataSet
                Return NullDataSet
                'If DataBaseConnection.State = System.Data.ConnectionState.Open Then
                '    DataBaseConnection.Close()
                'End If
                'Return DataSetTemp
                ResRQeury &= Ex.Message.ToString
            End Try
        Else
            Dim NullDataSet As New DataSet
            Return NullDataSet
        End If
    End Function '数据库查询
    Public Function SQLDataBaseExecute(ByVal SQLCommand As String, ByVal DataBaseConnection As SQLite.SQLiteConnection) As String
        Dim Res As String = ""
        If DataBaseDispose = False Then
            Try
                If DataBaseConnection.State = System.Data.ConnectionState.Closed Then
                    DataBaseConnection.Open()
                End If
                DataBaseCommand.Connection = DataBaseConnection
                DataBaseCommand.CommandText = SQLCommand
                DataBaseCommand.ExecuteNonQuery()
            Catch Ex As Exception
                Res = Ex.Message.ToString
            End Try
            If DataBaseConnection.State = System.Data.ConnectionState.Open Then
                DataBaseConnection.Close()
            End If
        End If
        Return Res
    End Function '数据库操作指令
#End Region
    Function TestWebsite(ByVal UrlStr As String, ByVal RespondStr As String) As String
        Dim UrlCode As String = GetWebCode(UrlStr)
        If UrlCode = "" OrElse UrlCode = "<Error:Nothing>" Then
            Return RespondStr & "❌" & “【“ & UrlStr & ”】” & vbCrLf
        Else
            Return RespondStr & "✔️" & “【“ & UrlStr & ”】” & vbCrLf
        End If
    End Function
    Sub ReadSettingXml()
        RichTextBox_Log.AppendText(Format(Now, "yyyy/MM/dd HH:mm:ss") & vbCrLf & "💠配置:" & vbCrLf)
        Dim SettingPath As String = Directory.GetCurrentDirectory & "\NasTool-Douban_Setting.Xml"
        Try
            Dim SettingXml As String = ""
            If IO.File.Exists(SettingPath) Then
                SettingXml = IO.File.ReadAllText(SettingPath)
            End If
            If SettingXml.Length > 0 Then
                Dim xmlDoc As New XmlDocument()
                xmlDoc.Load(SettingPath)
                Config_DB_Path = CType(xmlDoc.SelectSingleNode("NasTool-Douban_Setting").SelectSingleNode("DB_Path"), XmlElement).InnerText
                If IO.File.Exists(Config_DB_Path) = False Then
                    Config_DB_Path = Directory.GetCurrentDirectory & "\config\user.db"
                End If
                RichTextBox_Log.AppendText("#Config_DB_Path:" & Config_DB_Path & vbCrLf)
                Config_DoubanID = CType(xmlDoc.SelectSingleNode("NasTool-Douban_Setting").SelectSingleNode("Douban_Id"), XmlElement).InnerText
                RichTextBox_Log.AppendText("#Config_DoubanID:" & Config_DoubanID & vbCrLf)
                Config_TMDB_API = CType(xmlDoc.SelectSingleNode("NasTool-Douban_Setting").SelectSingleNode("TMDB_API"), XmlElement).InnerText
                RichTextBox_Log.AppendText("#Config_TMDB_API:" & Config_TMDB_API & vbCrLf)
                Dim DayStr As String = CType(xmlDoc.SelectSingleNode("NasTool-Douban_Setting").SelectSingleNode("Douban_SynDays"), XmlElement).InnerText
                If IsNumeric(DayStr) Then
                    Config_Douban_SynDays = Convert.ToInt32(DayStr)
                Else
                    Config_Douban_SynDays = -1
                End If
                If IsNothing(Config_Douban_SynDays) OrElse Config_Douban_SynDays < 0 Then
                    Config_Douban_SynDays = -1
                    RichTextBox_Log.AppendText("#Config_Douban_SynDays:" & Config_Douban_SynDays & "(无限制)" & vbCrLf)
                Else
                    Douban_Deadline = DateAdd(DateInterval.Day, 0 - Config_Douban_SynDays, Now)
                    RichTextBox_Log.AppendText("#Config_Douban_SynDays:" & Config_Douban_SynDays & "(" & Format(Douban_Deadline, "yyyy/MM/dd HH:mm:ss") & "之后)" & vbCrLf)
                End If
                Try
                    Config_RandomSleep = Convert.ToBoolean(CType(xmlDoc.SelectSingleNode("NasTool-Douban_Setting").SelectSingleNode("Random_Sleep"), XmlElement).InnerText)
                Catch ex As Exception
                    Config_RandomSleep = True
                End Try
                RichTextBox_Log.AppendText("#Config_Random_Sleep:" & Config_RandomSleep & vbCrLf)
            End If
        Catch ex As Exception
            MsgBox(ex.Message.ToString)
        End Try
    End Sub
    Sub ReadDB(ByVal DB_Path As String)
        RichTextBox_Log.AppendText(vbCrLf & "♻️更新数据库:" & vbCrLf)
        Dim tempDataSet As DataSet = SQLDataBaseQeury("SELECT TMDBID,ID FROM RSS_MOVIES", DataBaseConnection)
        If tempDataSet.Tables(0).Rows.Count > 0 Then
            RandomTMDBID = Convert.ToInt32(tempDataSet.Tables(0).Rows(Int(Rnd() * tempDataSet.Tables(0).Rows.Count)).Item("TMDBID"))
        End If
        Rss_Arr.Clear()
        For i = 0 To tempDataSet.Tables(0).Rows.Count - 1
            Rss_Arr.Add(tempDataSet.Tables(0).Rows(i).Item("TMDBID").ToString)
            RssID_Arr.Add(tempDataSet.Tables(0).Rows(i).Item("ID").ToString)
        Next
        RichTextBox_Log.AppendText(" >读取已订阅项目:" & Rss_Arr.Count & vbCrLf)
        '
        tempDataSet = SQLDataBaseQeury("SELECT TMDBID FROM DOWNLOAD_HISTORY", DataBaseConnection)
        If tempDataSet.Tables(0).Rows.Count > 0 Then
            RandomTMDBID = Convert.ToInt32(tempDataSet.Tables(0).Rows(Int(Rnd() * tempDataSet.Tables(0).Rows.Count)).Item("TMDBID"))
        End If
        DownLoad_Arr.Clear()
        For i = 0 To tempDataSet.Tables(0).Rows.Count - 1
            DownLoad_Arr.Add(tempDataSet.Tables(0).Rows(i).Item("TMDBID"))
        Next
        RichTextBox_Log.AppendText(" >已下载项目:" & DownLoad_Arr.Count & vbCrLf)
    End Sub
    Function GetPage_Douban_IMDB(ByVal DoubanID As String) As String
        Dim UrlCode As String = GetWebCode("https://movie.douban.com/subject/" & DoubanID & "/")
        Dim Strlist As String() = Split(UrlCode, "IMDb:</span>") ' tt15398776<br>
        Dim IMDB As String = ""
        If Strlist.Length > 0 Then
            For i = 1 To Strlist.Length - 1
                Dim IMDBstr As String = Split(Strlist(i), "<br>")(0)
                If IMDBstr.Contains("tt") Then
                    IMDB = IMDBstr
                    Exit For
                End If
            Next
        End If
        Return IMDB.Trim
    End Function
    Function GetPage_TMDB(ByVal KeyWord As String) As ArrayList
        ToolStripStatusLabel1.Text = "正在TMDB上搜索:" & KeyWord
        Dim UrlCode As String = GetWebCode("https://www.themoviedb.org/search?query=" & KeyWord)
        Dim Strlist As String() = Split(UrlCode, "class=" & Chr(34) & "result" & Chr(34) & " href=" & Chr(34) & "/movie/") ' tt15398776<br>
        Dim SearchArr As New ArrayList
        RichTextBox_Log.AppendText("<" & KeyWord & ">找到TMDB结果:" & Strlist.Count & vbCrLf)
        If Strlist.Length > 0 Then
            For i = 1 To Strlist.Length - 1
                Dim TMDBstr As String = Split(Strlist(i), Chr(34) & "><h2>")(0)
                If IsNumeric(TMDBstr) AndAlso SearchArr.Contains(TMDBstr) = False Then
                    SearchArr.Add(TMDBstr)
                    'RichTextBox_Log.AppendText( "匹配<" & KeyWord & ">正确TMDB_ID:" & TMDBstr & vbCrLf
                End If
            Next
        End If
        Return SearchArr
    End Function
    Function GetPage_TMDB_IMDB(ByVal TMDB As String) As String
        Dim UrlCode As String = GetWebCode("https://api.themoviedb.org/3/movie/" & TMDB & "?api_key=" & Config_TMDB_API)
        If UrlCode = "" OrElse UrlCode = "<Error:Nothing>" Then
            Return Nothing
        Else
            Dim JsonObj As New With {.imdb_id = ""}
            JsonObj = JsonConvert.DeserializeAnonymousType(UrlCode, JsonObj)
            Return JsonObj.imdb_id
        End If
    End Function
    Function CompareRight_TMDB(ByVal DoubanInfo As MInfos) As String
        ToolStripStatusLabel1.Text = "正在匹配<" & DoubanInfo.M_Name & ">正确TMDB_ID"
        Dim resarr As ArrayList = GetPage_TMDB(DoubanInfo.M_Name)
        Dim ImdbId As String = GetPage_Douban_IMDB(DoubanInfo.M_ID)
        Dim TMDBID As String = ""
        Dim CompareSuccuessFlag As Boolean = False
        For i = 0 To resarr.Count - 1
            Dim Tmdb_ImdbStr As String = GetPage_TMDB_IMDB(resarr(i))
            If IsNothing(Tmdb_ImdbStr) = False AndAlso Tmdb_ImdbStr = ImdbId Then
                TMDBID = resarr(i)
                RichTextBox_Log.AppendText("✔️" & vbTab & "匹配<" & DoubanInfo.M_Name & ">正确TMDB_ID:" & TMDBID & vbCrLf)
                CompareSuccuessFlag = True
                Exit For
            End If
        Next
        If CompareSuccuessFlag = False Then
            RichTextBox_Log.AppendText("❌" & vbTab & "未找到匹配<" & DoubanInfo.M_Name & ">的TMDB_ID." & vbCrLf)
        End If
        Return TMDBID
    End Function
    Function FreshDoubanWish(ByVal DoubanId As String) As ArrayList
        ToolStripStatusLabel1.Text = "正在读取ID<" & Config_DoubanID & ">用户Movie_Wish列表"
        Dim CheckRepeatList As New ArrayList
        Dim DoubanWishList As New ArrayList
        Dim sum As Integer = 0
        Try
            Dim UrlCode As String = GetWebCode("https://mouban.mythsman.com/guest/user_movie?id=" & DoubanId & "&action=wish")
            If UrlCode = "<Error:Nothing>" Then
                RichTextBox_Log.AppendText(vbCrLf & "🔰读取ID<" & Config_DoubanID & ">用户Movie_Wish列表有效项目:失败" & vbCrLf)
            End If
            Dim JsonObj As JObject = JsonConvert.DeserializeObject(Replace(UrlCode, vbCrLf, ""))
            If Convert.ToBoolean(JsonObj("success")) Then
                JsonObj = JsonConvert.DeserializeObject(JsonObj("result").ToString())
                Dim JArr As JArray = JsonConvert.DeserializeObject(JsonObj("comment").ToString())
                For i = 0 To JArr.Count - 1
                    If Config_Douban_SynDays = -1 OrElse Convert.ToDateTime(JArr(i)("mark_date")) > Douban_Deadline Then
                        Dim JItem As JObject = JsonConvert.DeserializeObject(JArr(i)("item").ToString())
                        Dim DoubanInfo As MInfos
                        DoubanInfo.M_ID = JItem("douban_id")
                        DoubanInfo.M_Name = JItem("title")
                        If CheckRepeatList.Contains(DoubanInfo.M_ID) = False Then
                            DoubanWishList.Add(DoubanInfo)
                            CheckRepeatList.Add(DoubanInfo.M_ID)
                            sum += 1
                        End If
                    End If
                Next
                RichTextBox_Log.AppendText(vbCrLf & "🔰读取ID<" & Config_DoubanID & ">用户Movie_Wish列表有效项目:" & sum & vbCrLf)
            Else
                RichTextBox_Log.AppendText(vbCrLf & "🔰读取ID<" & Config_DoubanID & ">用户Movie_Wish列表有效项目:失败" & vbCrLf)
            End If
        Catch ex As Exception
        End Try
        Return DoubanWishList
    End Function
    Sub GoGet()
        ToolStripLabel4.Visible = True
        If DoubanList.Count > 0 Then
            RichTextBox_Log.AppendText(vbCrLf & "🛃" & "开始匹配TMDB_ID:" & vbCrLf)
        End If
        NumMark = Math.Max(NumMark, 0)
        NumMark = Math.Min(NumMark, Math.Max(0, DoubanList.Count - 1))
        Dim StartNum = NumMark
        For i = StartNum To DoubanList.Count - 1
            NumMark = i
            Dim doubaninfo_input As MInfos = DoubanList(i)
            Dim NewMovie_TMDB As String = CompareRight_TMDB(doubaninfo_input)
            If DownLoad_Arr.Contains(NewMovie_TMDB) Then
                RichTextBox_Log.AppendText("⚠️" & vbTab & "<" & doubaninfo_input.M_Name & ">已下载." & vbCrLf)
            ElseIf Rss_Arr.Contains(NewMovie_TMDB) Then
                RichTextBox_Log.AppendText("⚠️" & vbTab & "<" & doubaninfo_input.M_Name & ">已订阅." & vbCrLf)
            Else
                Dim TempInfo As OutputInfos
                TempInfo.NAME = doubaninfo_input.M_Name
                TempInfo.TMDBID = NewMovie_TMDB
                If IsDBNull(NewMovie_TMDB) = False AndAlso NewMovie_TMDB.Length > 0 Then
                    Dim RCheck As OutputInfos = GetPage_TMDB_Moreinfo(TempInfo)
                    If RCheck.TMDBID.Length > 0 Then
                        NewMovieAddArr.Add(RCheck)
                        RichTextBox_Log.AppendText("✔️" & vbTab & "<" & doubaninfo_input.M_Name & ">TMDB信息获取成功." & vbCrLf)
                    Else
                        RichTextBox_Log.AppendText("❌" & vbTab & "<" & doubaninfo_input.M_Name & ">TMDB信息不全." & vbCrLf)
                    End If
                Else
                    RichTextBox_Log.AppendText("❌" & vbTab & "<" & doubaninfo_input.M_Name & ">获取TMDB失败." & vbCrLf)
                End If
            End If
            If Config_RandomSleep Then
                Randomize()
                Threading.Thread.Sleep(500 + Rnd() * 10000)
            End If
        Next
        If NewMovieAddArr.Count > 0 Then
            ToolStripStatusLabel1.Text = "检测豆瓣完毕:可以添加" & CType(NewMovieAddArr(0), OutputInfos).NAME & "等" & NewMovieAddArr.Count & "个项目."
            ToolStripLabel2.Enabled = True
        Else
            ToolStripStatusLabel1.Text = "检测豆瓣完毕:" & "没有项目需要添加."
        End If
        RichTextBox_Log.AppendText(vbCrLf & ToolStripStatusLabel1.Text & vbCrLf)
        ToolStripLabel4.Visible = False
        GoGetThread.Abort()
    End Sub
    Function GetPage_TMDB_Moreinfo(ByVal Out_M As OutputInfos) As OutputInfos
        Dim UrlCode As String = GetWebCode("https://api.themoviedb.org/3/movie/" & Out_M.TMDBID & "?api_key=" & Config_TMDB_API)
        If UrlCode = "<Error:Nothing>" Then
            Out_M.TMDBID = ""
        Else
            Dim JsonObj As JObject = JsonConvert.DeserializeObject(UrlCode)
            Out_M.IMAGE = "https://image.tmdb.org/t/p/w500" & JsonObj("poster_path").ToString
            Out_M.YEAR = Convert.ToDateTime(JsonObj("release_date").ToString).Year
            Out_M.NOTE = "{" & Chr(34) & "poster" & Chr(34) & " :  " & Chr(34) & "https://image.tmdb.org/t/p/w500/" &
            JsonObj("backdrop_path").ToString & Chr(34) & ", " & Chr(34) & "release_date" & Chr(34) & ": " & Chr(34) &
            JsonObj("release_date").ToString & Chr(34) & ", " & Chr(34) & "vote" & Chr(34) & ": " &
             JsonObj("vote_average").ToString & "}"
        End If
        Return Out_M
    End Function

    Private Sub ToolStripLabel5_Click(sender As Object, e As EventArgs) Handles ToolStripLabel5.Click
        ToolStripStatusLabel1.Text = "检测连通性..."
        CheckWebSite()
    End Sub

    Private Sub RichTextBox_Log_TextChanged(sender As Object, e As EventArgs) Handles RichTextBox_Log.TextChanged
        RichTextBox_Log.SelectionStart = RichTextBox_Log.TextLength
        RichTextBox_Log.ScrollToCaret()
    End Sub
    Function GetMinId() As Integer
        Dim resNum As Integer = RssID_Arr.Count - 1
        Dim LittleNumAvilableFlag As Boolean = False
        For i = RssID_Arr.Count - 1 To 0 Step -1
            If RssID_Arr.Contains(i.ToString) = False Then
                LittleNumAvilableFlag = True
                resNum = i
                Exit For
            End If
        Next
        If LittleNumAvilableFlag = False Then
            Do
                resNum += 1
            Loop While RssID_Arr.Contains(resNum.ToString)
        End If
        RssID_Arr.Add(resNum)
        Return resNum
    End Function
End Class
