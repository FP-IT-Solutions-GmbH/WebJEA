﻿Imports System.Management.Automation
Imports System.Management.Automation.Language

Public Class _default
    Inherits System.Web.UI.Page
    Dim cmdid As String

    'TODO: 7- Add ParameterSet support?
    Private dlog As NLog.Logger = NLog.LogManager.GetCurrentClassLogger()
    Private uinfo As New UserInfo(Page.User)
    Private objTelemetry As New Telemetry
    Public grpfinder As New GroupFinder
    Public cfg As WebJEA.Config
    ' Public Shared CachedFormValues As New Hashtable()
    Public Shared SessionValues As New Hashtable()
    'advanced functions should be able to retrieve the get-help and parameter data, then permit overriding

    'cache is the same format, and might contain a bit more, but it also includes the stuff we've calculated from other inputs (say by looking at parameters from advanced functions)
    '-probably has a lifetime in it, like 30 minutes or an hour
    'will need to have credential specified in app pool

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        dlog.Trace("Page: Start")
        If SessionValues.ContainsKey(Session.SessionID) Then
            '''''
        Else
            Dim CachedFormValuesSession As New Hashtable
            SessionValues.Add(Session.SessionID, CachedFormValuesSession)
            SessionValues.Item(Session.SessionID).Add("WEBJEAUsername", uinfo.UserName)
        End If
        'Dim sessiontmp = (SessionValues.Item(Session.SessionID)).add("asd", "dsa")

        'CachedFormValues.Clear()

        Dim CachedValuesRemove As New List(Of String)

        For Each CachedItem As DictionaryEntry In SessionValues.Item(Session.SessionID)
            If CachedItem.Key.ToString.Substring(0, 7) = "UPDATE_" Then
                CachedValuesRemove.Add(CachedItem.Key.ToString)
            End If
        Next

        For Each ModKey As String In CachedValuesRemove
            'CachedFormValues.Remove(ModKey)
            SessionValues.Item(Session.SessionID).remove(ModKey)
        Next



        'Get Variables from PostBack and save them in Hashtable
        If Page.IsPostBack Then

            For Each key As String In Request.Form.AllKeys

                'Creates an Entry for Button Presses so another Function can execute an extra script
                If key.Equals("__EVENTTARGET") Then
                    If Request.Form.GetValues(key).GetValue(0) IsNot "" And Not (Request.Form.GetValues(key).GetValue(0).ToString.Length() = 0) Then
                        If (Request.Form.GetValues(key).GetValue(0)).ToString.Substring(0, 14) = "psparam_FPITBT" Then
                            Dim tmpEvent As String = Request.Form.GetValues(key).GetValue(0)
                            tmpEvent = tmpEvent.Replace("psparam_", "")

                            If SessionValues.Item(Session.SessionID).ContainsKey("EXEC_" + tmpEvent) Then
                                SessionValues.Item(Session.SessionID).Remove("EXEC_" + tmpEvent)
                            End If

                            SessionValues.Item(Session.SessionID).Add("EXEC_" + tmpEvent, "")
                        End If
                    End If
                End If
                ''''''''''
                If key.Contains("psparam_FPIT") And Request.Form.GetValues(key).GetValue(0) IsNot Nothing Then 'Es werden nur FPIT Felder Zwischengespeichert
                    Dim keyvalue
                    If key.Contains("psparam_FPITLS") Then '(Request.Form.GetValues(key).Length > 1) And 

                        Dim arr() As String = Request.Form.GetValues(key)

                        Dim list As New List(Of String)(arr)

                        keyvalue = list
                    Else
                        If Not Request.Form.GetValues(key).GetValue(0) = "" Then
                            keyvalue = Request.Form.GetValues(key).GetValue(0) 'Er Zieht sich aus dem PostBack die Feldvariable
                        End If
                    End If
                    If Not keyvalue Is Nothing Then 'Falls das Feld leer war dann speichert er auch nichts ab
                        If SessionValues.Item(Session.SessionID).ContainsKey(key) Then 'Falls es den Key bereits in der Hashtable gibt dann....

                            If keyvalue.GetType() Is GetType(System.Collections.Generic.List(Of String)) Then

                                If CompareKeys(SessionValues.Item(Session.SessionID).Item(key), keyvalue) Then
                                    SessionValues.Item(Session.SessionID).Item(key) = keyvalue
                                    SessionValues.Item(Session.SessionID).Add("UPDATE_" + key, "")
                                End If
                            Else
                                If Not SessionValues.Item(Session.SessionID).Item(key) = keyvalue Then '...Prüfe ob sich der Wert geändert hat...
                                    SessionValues.Item(Session.SessionID).Item(key) = keyvalue '... Wenn er sich geändert hat dann mache ein Update vom Wert und....
                                    SessionValues.Item(Session.SessionID).Add("UPDATE_" + key, "") '... erstelle einen zusätzlichen Key damit erkennbar ist das sich was geändert hat.
                                End If
                            End If
                        Else
                            SessionValues.Item(Session.SessionID).Add(key, keyvalue) 'Falls es keinen Key gibt dann erstelle einen
                        End If
                    Else 'Wenn der Rückgabewert leer ist aber im Cache noch ein alter eintrag ist dann muss der raus...
                        If SessionValues.Item(Session.SessionID).ContainsKey(key) Then
                            SessionValues.Item(Session.SessionID).Remove(key)
                        End If
                    End If
                    keyvalue = Nothing
                End If
            Next
        End If

        If (Page.Request.QueryString("cmdid") <> Page.User.Identity.Name.Substring(Page.User.Identity.Name.IndexOf("\") + 1)) Then
            dlog.Trace("!!!!!cmdid," + Page.Request.QueryString("cmdid") + ",name," + Page.User.Identity.Name.Substring(Page.User.Identity.Name.IndexOf("\") + 1))
        End If


        objTelemetry.Add("sessionid", StringHash256(Session.SessionID)) 'to correlate one user's activities
        objTelemetry.Add("requestid", StringHash256(Guid.NewGuid().ToString())) 'to correlate multiple telemetry from the same page request

        Dim psweb = New PSWebHelper

        'read the webjea config
        Dim configid As String = "configfile"
        dlog.Debug("Looking for web.config/applicationSettings/WebJEA.My.MySettings/settings=" & configid)
        Dim configstr As String = GetFileContent(WebJEA.My.Settings(configid))
        Try
            cfg = JsonConvert.DeserializeObject(Of WebJEA.Config)(configstr)
            objTelemetry.Add("CommandCount", cfg.Commands.Count)
            objTelemetry.Add("PermGlobalCount", cfg.PermittedGroups.Count)
        Catch ex As Exception
            Throw New Exception("Could not read config file")
        End Try

        'TODO: 9 - Improve JSON read process.  The current system is a hack, but it does work.

        'TODO: 5 - consider, using cached config, and then check for changes, and reload if appropriate

        'parse group info
        Try
            cfg.InitGroups(grpfinder)
        Catch
            Throw New Exception("Could not initialize groups")
        End Try

        dlog.Trace("IsGlobalUser: " & cfg.IsGlobalUser(uinfo))
        objTelemetry.Add("IsGlobalUser", cfg.IsGlobalUser(uinfo))

        'determine which cmds the user has access to
        Dim menuitems As List(Of MenuItem) = cfg.GetMenu(uinfo)

        'check to see if the user requests a specific command
        cmdid = ReadGetPost("cmdid", cfg.DefaultCommandId) 'doesn't matter if they have access here
        'if the user requested a page they don't have access to, send them to defaultid
        'user should always have access to defaultid.
        'If you don't want them to have access to defaultid, restrict access using ntfs perms
        If Not cfg.IsCommandAvailable(uinfo, cmdid) Then
            dlog.Warn("User " & uinfo.UserName & " requested page they don't have access to " & cmdid & ". redirecting to " & cfg.DefaultCommandId)
            cmdid = cfg.DefaultCommandId
        End If
        cfg.Init(cmdid) 'json's deserialize doesn't can't call new with parameters, so we do all the stuff we should do during the deserialize process.

        'build display page
        lblTitle.Text = cfg.Title

        'build menu
        lvMenu.DataSource = cfg.GetMenuDataTable(uinfo, cmdid)
        lvMenu.DataBind()

        'add version to display
        Try
            lblVersion.Text = " v" + System.Reflection.Assembly.GetExecutingAssembly.GetName().Version.ToString(4) + ".5"
            objTelemetry.Add("appedition", "CE")
            objTelemetry.Add("appversion", System.Reflection.Assembly.GetExecutingAssembly.GetName().Version.ToString(4))
        Catch ex As Exception
        End Try


        'if the user has access to the requested command, build the display, otherwise display nothing
        Dim cmd As PSCmd = cfg.GetCommand(uinfo, cmdid)

        If cmd Is Nothing Then
            objTelemetry.AddIDs(uinfo.DomainSID, uinfo.DomainDNSRoot, cmdid, uinfo.UserName, Permitted:=False)

            divCmdBody.InnerText = "You do not have access to this command."
            dlog.Error("User " & uinfo.UserName & " requested cmdid " & cmdid & " that does not exist (or they don't have access to)")
        Else
            objTelemetry.AddIDs(uinfo.DomainSID, uinfo.DomainDNSRoot, cmd.ID, uinfo.UserName)
            objTelemetry.Add("PermCount", cmd.PermittedGroups.Count)
            objTelemetry.Add("ParamCount", cmd.Parameters.Count)

            'build display
            lblCmdTitle.Text = cmd.DisplayName
            lblcmdUsername.Text = uinfo.UserName
            If cmd.Synopsis <> "" Then
                lblCmdSynopsis.Text = cmd.Synopsis
                lblCmdDescription.Text = cmd.Description
            Else
                lblCmdSynopsis.Text = cmd.Description
            End If
            If cmd.Description = "" Or cmd.Synopsis = "" Then
                btnMore.Visible = False
            End If

            Dim pscontrols As List(Of HtmlControl) = psweb.NewControl(Page, cmd.Parameters)

            'psweb.AddControls(pscontrols, frmMain, btnRun)
            psweb.AddControls(pscontrols, frmMain, divParameters)


            If String.IsNullOrEmpty(cmd.OnloadScript) Then
                'hide the onload section
                panelOnload.Attributes("class") = panelOnload.Attributes("class") & " collapse"
            Else 'run the script and display it
                Dim ps As New PSEngine
                Dim pswebonload As New PSWebHelper
                ps.Script = cmd.OnloadScript
                ps.LogParameters = cmd.LogParameters
                'pass in parameters to onload script
                ps.Parameters = pswebonload.getParameters(cmd, Page, uinfo)
                ps.Run()
                objTelemetry.AddRuntime(ps.Runtime)
                objTelemetry.AddIsOnload(True)

                consoleOnload.InnerHtml = pswebonload.ConvertToHTML(ps.getOutputData)
                ps = Nothing

            End If

            If (String.IsNullOrEmpty(cmd.Script)) Then
                panelInput.Visible = False
            End If

            'hide output until submit
            panelOutput.Attributes("class") = panelOutput.Attributes("class") & " collapse"


        End If


        dlog.Trace("Page: End")

    End Sub

    Public Function CompareKeys(List1 As List(Of String), List2 As List(Of String)) As Boolean
        Dim hs1 = New HashSet(Of String)(List1)
        Dim hs2 = New HashSet(Of String)(List2)

        If Not (hs1.Count = hs2.Count) Then
            Return False
        End If

        For Each value In hs2
            Dim isKeyEqual As Boolean = False
            isKeyEqual = hs1.Contains(value)

            If Not isKeyEqual Then
                Return False
            End If
        Next

        Return True
    End Function

    Protected Sub btnRun_Click(sender As Object, e As EventArgs) Handles btnRun.Click

        'dlog.Trace("Timeout: " & HttpContext.Current.Server.ScriptTimeout)
        'HttpContext.Current.Server.ScriptTimeout = 6
        'dlog.Trace("Timeout: " & HttpContext.Current.Server.ScriptTimeout)

        'display the output panel now and set focus
        panelOutput.Attributes("class") = panelOutput.Attributes("class").Replace("collapse", "")
        ClientScript.RegisterStartupScript(Page.GetType(), "hash", "location.hash='#panelOutput';", True)

        'TODO: verify user has access to this command

        Dim psweb As New PSWebHelper
        Dim ps As New PSEngine
        Dim cmd As PSCmd

        'verify the user has access to the cmd they want to use
        If Not cfg.IsCommandAvailable(uinfo, cmdid) Then
            dlog.Warn("User " & uinfo.UserName & " tried submitting a page they don't have access to " & cmdid & ".")
            consoleOutput.Text = "You do not have access to the page you requested."
            Return
        End If
        'get the script config
        cmd = cfg.GetCommand(uinfo, cmdid)

        'TODO: validate if there is a script to run, fail if not
        ps.Script = cmd.Script
        ps.LogParameters = cmd.LogParameters
        ps.Parameters = psweb.getParameters(cmd, Page, uinfo)
        objTelemetry.Add("ParamUsed", ps.Parameters.Count)

        ps.Run()
        objTelemetry.AddRuntime(ps.Runtime)
        objTelemetry.AddIsOnload(False)

        consoleOutput.Text = psweb.ConvertToHTML(ps.getOutputData)
        ps = Nothing
    End Sub


    Private Function ReadGetPost(param As String, DefaultValue As String) As String
        'check both GET and POST for parameter, if not found, return defaultvalue
        'prefer post over get for security
        'Dim httpcont As System.Web.HttpContext = System.Web.HttpContext.Current
        If Page.Request.Form(param) IsNot Nothing Then
            Return Page.Request.Form(param)
        ElseIf Page.Request.QueryString(param) IsNot Nothing Then
            Return Page.Request.QueryString(param)
        Else
            Return DefaultValue
        End If

    End Function

    Private Sub _default_Init(sender As Object, e As EventArgs) Handles Me.Init
        ViewStateUserKey = Session.SessionID
    End Sub

    Private Sub _default_LoadComplete(sender As Object, e As EventArgs) Handles Me.LoadComplete
        If cfg.SendTelemetry Then
            objTelemetry.SendTelemetry()
        End If
    End Sub
End Class