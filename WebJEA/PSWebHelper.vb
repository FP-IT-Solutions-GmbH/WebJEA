﻿Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Net.Sockets

Public Class PSWebHelper
    Private dlog As NLog.Logger = NLog.LogManager.GetCurrentClassLogger()


#Region "Output Formatting"

    Public Function ConvertToHTML(OutputData As Queue(Of PSEngine.OutputData))
        Const NLOGPREFIX As String = "WEBJEA:"
        Dim outputstr As String = ""
        'display web helper

        For Each line As PSEngine.OutputData In OutputData

            If line.Content.StartsWith(NLOGPREFIX) Then
                'script says to output to NLOG.INFO, so we don't output to screen.
                dlog.Info(line.Content.Substring(NLOGPREFIX.Length).Trim())
            Else

                Select Case line.OutputType
                    Case PSEngine.OutputType.Debug
                        outputstr += EncodeOutput("DEBUG: " & line.Content, "psdebug")
                    Case PSEngine.OutputType.Err
                        outputstr += EncodeOutput(line.Content, "pserror")
                    Case PSEngine.OutputType.Warn
                        outputstr += EncodeOutput("WARNING: " & line.Content, "pswarning")
                    Case PSEngine.OutputType.Info
                        outputstr += EncodeOutput(line.Content, "psoutput")
                    Case PSEngine.OutputType.Verbose
                        outputstr += EncodeOutput("VERBOSE: " & line.Content, "psverbose")
                    Case PSEngine.OutputType.Output
                        outputstr += EncodeOutput(line.Content, "psoutput")
                    Case Else
                        outputstr += EncodeOutput(line.Content, "psoutput")
                End Select
            End If

        Next

        Return outputstr

    End Function

    Private Function EncodeOutput(input As String, baseclass As String) As String

        Dim output As String = input
        'html encode for safety
        output = HttpContext.Current.Server.HtmlEncode(output)

        'remove CR to get consistent output, then replace LF w/ html BR
        'output = output.Replace(vbCr, "").Replace(vbLf, "<br/>")

        output = EncodeOutputTags(output)

        output = "<span class=""" & baseclass & """>" & output & "</span><br/>"
        Return output
    End Function

    Private Function EncodeOutputTags(ByVal input As String) As String
        Dim rexopt As RegexOptions = RegexOptions.IgnoreCase + RegexOptions.Multiline
        Const rexA As String = "\[\[a\|(.+?)\|(.+?)\]\]"
        Const repA As String = "<a href='$1'>$2</a>"
        Dim rgxA As New Regex(rexA, rexopt)
        Const rexSpan As String = "\[\[span\|(.+?)\|(.+?)\]\]"
        Const repSpan As String = "<span Class='$1'>$2</span>"
        Dim rgxSpan As New Regex(rexSpan, rexopt)
        Const rexImg As String = "\[\[img\|(.*?)\|(.+?)\]\]"
        Const repImg As String = "<img class='$1' src='$2' />"
        Dim rgxImg As New Regex(rexImg, rexopt)

        Dim idxx As Int32 = input.LastIndexOf("[[")
        Dim idyy As Int32 = input.LastIndexOf("]]")

        Dim tmpindex As String = ""
        Dim tmpindexnew As String = ""
        Dim intDelta As Int32 = 0

        'remove vbcrlf in TAGs -> Happens when you output an Object inside the TAGs
        While idxx > -1
            tmpindex = input.Substring(idxx, (idyy - idxx) + 2) 'wrong Substring
            tmpindexnew = tmpindex.Replace(vbCr, "").Replace(vbLf, "") 'correct Substring

            intDelta = tmpindex.Length - tmpindexnew.Length 'length delta of both Substrings after shortening

            input = input.Replace(tmpindex, tmpindexnew) 'replace the substring in the input string

            If (idxx > 0) Then
                idxx = input.LastIndexOf("[[", idxx - 1 - intDelta) 'set new position minus the delta
                idyy = input.LastIndexOf("]]", idyy - 1 - intDelta)
            Else
                idxx = -1
                idyy = -1
            End If

            tmpindex = ""
            tmpindexnew = ""
            intDelta = 0
        End While


        Dim idx As Int32 = input.LastIndexOf("[[")
        While idx > -1
            input = rgxA.Replace(input, repA, 1, idx)
            input = rgxSpan.Replace(input, repSpan, 1, idx)
            input = rgxImg.Replace(input, repImg, 1, idx)
            If (idx > 0) Then
                idx = input.LastIndexOf("[[", idx - 1)
            Else
                idx = -1
            End If

        End While

        Return input
    End Function


#End Region


#Region "BuildControls"

    Public Sub AddControls(controls As List(Of HtmlControl), form As HtmlForm, BeforeControl As HtmlGenericControl)

        'Dim startidx As Integer = form.Controls.IndexOf(BeforeControl)
        'Dim curidx As Integer = startidx

        For Each ctrl As HtmlControl In controls
            'form.Controls.AddAt(form.Controls.IndexOf(BeforeControl), ctrl)
            BeforeControl.Controls.Add(ctrl)

        Next
        form.Controls.AddAt(form.Controls.IndexOf(BeforeControl), New LiteralControl("<br />"))

    End Sub

    Public Function NewControl(pg As Page, params As List(Of PSCmdParam)) As List(Of HtmlControl)
        'this generates a control for each parameter, based on type
        Dim retcontrols As New List(Of HtmlControl)

        If Not IsNothing(params) Then
            For Each param As PSCmdParam In params
                If (param.Name.ToUpper().StartsWith("WEBJEA")) Then
                    'do nothing to display.  This will be handled internally.
                ElseIf param.IsSelect Then
                    retcontrols.Add(NewControlStringSelect(pg, param))
                Else
                    Select Case param.ParamType
                        Case PSCmdParam.ParameterType.PSString
                            retcontrols.Add(NewControlString(pg, param))
                        Case PSCmdParam.ParameterType.PSBoolean
                            retcontrols.Add(NewControlSwitch(pg, param))
                        Case PSCmdParam.ParameterType.PSDate
                            retcontrols.Add(NewControlDate(pg, param))
                        Case PSCmdParam.ParameterType.PSFloat
                            'TODO: add support for float field
                            'numbers are strings, we just need to add a bit of special validation and some files
                            retcontrols.Add(NewControlString(pg, param))
                        Case PSCmdParam.ParameterType.PSInt
                            'TODO: add support for int field
                            'numbers are strings, we just need to add a bit of special validation and some files
                            retcontrols.Add(NewControlString(pg, param))
                        Case PSCmdParam.ParameterType.PSButton
                            retcontrols.Add(NewControlButton(pg, param))
                        Case Else
                            dlog.Warn("PSWebHelper: Defaulting to string for unknown type: " & param.VarType & " paramname: " & param.Name)
                            retcontrols.Add(NewControlString(pg, param))
                    End Select
                End If

            Next
        End If

        Return retcontrols
    End Function


    Private Sub AddMessageHelp(messageString As String, parentObj As Control)
        'add the HelpMessage value from the [parameter()] field
        Dim helpmsgtag As HtmlGenericControl = NewControl("span", "help-message", messageString)
        parentObj.Controls.Add(helpmsgtag)
    End Sub
    Private Sub AddMessageHelpDetail(messageString As String, parentObj As Control)
        'add the HelpMessage value from the [parameter()] field
        Dim helpmsgtag As HtmlGenericControl = NewControl("p", "help-block", messageString)
        parentObj.Controls.Add(helpmsgtag)
    End Sub
    Private Sub AddMessageRequired(parentObj As Control)
        Dim objReqOpt As HtmlControl = NewControl("span", "reqopt", "Required")
        parentObj.Controls.Add(objReqOpt)
    End Sub


    Private Function NewControlString(pg As Page, param As PSCmdParam) As HtmlControl

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)



        Dim objControl As New TextBox
        objControl.ID = param.FieldName
        objControl.CssClass += " form-control"
        If param.IsMultiValued Or param.DirectiveMultiline Then
            objControl.TextMode = TextBoxMode.MultiLine
            objControl.Rows = 5
            objControl.Columns = 100
        End If
        objLabel.AssociatedControlID = objControl.ID

        'if there's a default value display it
        If param.IsMultiValued Then
            If Not (param.DefaultValue Is Nothing) Then
                Dim defval As List(Of String) = param.DefaultValue
                objControl.Text = String.Join(vbCrLf, defval)
            End If
        Else
            'If Not String.IsNullOrEmpty(param.DefaultValue) Then
            '    'objControl.Attributes.Add("value", param.DefaultValue)
            'End If
            objControl.Text = param.DefaultValue
        End If
        objControl.Text = ReadGetPost(pg, param.Name, objControl.Text)

        'label, reqopt, control into row
        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow

    End Function

    Protected Sub OnButtonPress(sender As Object, e As EventArgs)

        Dim FPIT_Path = WebJEA.My.Settings.configfile
        Dim tmpName = sender.clientid.ToString.Replace("psparam_", "")



        FPIT_Path = FPIT_Path.Replace("config.json", tmpName + ".ps1")
        Dim pscommand As String = FPIT_Path & "; exit $LASTEXITCODE"
        Dim cmd As String = "powershell.exe -noprofile -NonInteractive -WindowStyle hidden -command " & pscommand
        Dim shell = CreateObject("WScript.Shell")
        Dim executor = shell.Exec(cmd)
        executor.StdIn.Close

        'Ausgabe = executor.StdOut.ReadAll

    End Sub

    Private Function NewControlButton(pg As Page, param As PSCmdParam) As HtmlControl

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)

        Dim objControl As New Button
        objControl.ID = param.FieldName
        objControl.CssClass += " form-control"
        objControl.BorderStyle = BorderStyle.Ridge
        objControl.BackColor = System.Drawing.ColorTranslator.FromHtml("#e2f0dd")
        objControl.UseSubmitBehavior = False
        objLabel.AssociatedControlID = objControl.ID

        AddHandler objControl.Click, AddressOf OnButtonPress

        'Shown Text of Button
        objControl.Text = "Skript ausführen"

        'label, reqopt, control into row
        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        If param.DefaultValue_BT IsNot Nothing Then
            Dim objControl2 As New TextBox

            objControl2.TextMode = TextBoxMode.MultiLine
            objControl2.Rows = 5
            objControl2.Columns = 100
            objControl2.BorderColor = System.Drawing.ColorTranslator.FromHtml("#c4e3a6")
            objControl2.BorderWidth = 3
            objControl2.CssClass += " form-control"
            objControl2.Text = param.DefaultValue_BT
            objControl2.Enabled = False

            objRow.Controls.Add(objControl2)
        End If


        Return objRow

    End Function

    'Sobald ein Datum im Calendar ausgewählt wird
    Protected Sub OnSelectedDateChanged(sender As Object, e As EventArgs)
        'Dim tmpSender As New System.Web.UI.WebControls.Calendar
        'If (WebJEA._default.CachedFormValues.ContainsKey("REFRESH_" + sender.clientID)) Then
        'tmpSender = sender
        'Dim blubber As Date = tmpSender.SelectedDate

        'WebJEA._default.CachedFormValues.Remove("REFRESH_" + sender.clientID)
        '    sender = tmpSender
        'End If
    End Sub

    Private Function NewControlDate(pg As Page, param As PSCmdParam) As HtmlControl

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)

        Dim strReqOpt As String = "Required"
        Dim objReqOpt As HtmlControl = NewControl("span", "reqopt", strReqOpt)

        Dim objControl As New TextBox
        objControl.ID = param.FieldName
        objControl.CssClass += " form-control"
        objControl.BorderStyle = BorderStyle.Outset
        objControl.TextMode = TextBoxMode.Date

        'AddHandler objControl.Load, AddressOf OnLoadDate

        If param.DirectiveDateTime Then 'date and time input
            'client-side js will enforce date/time prompt
            objControl.Attributes.Add("data-type", "datetime")
        Else 'date only input
            objControl.Attributes.Add("data-type", "date")
        End If
        objLabel.AssociatedControlID = objControl.ID

        'if there's a default value display it
        If Not String.IsNullOrEmpty(param.DefaultValue) Then
            'objControl.Text = param.DefaultValue
        End If
        'objControl.Text = ReadGetPost(pg, param.Name, objControl.Text)

        'label, reqopt, control into row
        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow

    End Function

    Private Function NewControlStringSet(pg As Page, param As PSCmdParam) As HtmlControl

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)



        Dim objControl As New TextBox
        objControl.ID = param.FieldName
        objControl.CssClass += " form-control"
        If param.IsMultiValued Then
            objControl.TextMode = TextBoxMode.MultiLine
            objControl.Rows = 5
            objControl.Columns = 100
        End If
        objLabel.AssociatedControlID = objControl.ID

        'if there's a default value display it
        If Not String.IsNullOrEmpty(param.DefaultValue) Then
            objControl.Text = param.DefaultValue
        End If
        objControl.Text = ReadGetPost(pg, param.Name, objControl.Text)

        'label, reqopt, control into row
        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow

    End Function

    Private Function NewControlStringSelect(pg As Page, param As PSCmdParam) As HtmlControl
        'Abstract Listbox/Dropdown one level to simplify logic in NewControl
        If param.IsMultiValued Then
            Return NewControlStringListbox(pg, param)
        Else
            Return NewControlStringDropdown(pg, param)
        End If

    End Function

    'Protected Sub OnSelectedItemChange(sender As Object, e As EventArgs)
    'Dim blub As String
    '   blub = "blubber"
    'End Sub

    Private Function NewControlStringListbox(pg As Page, param As PSCmdParam) As HtmlControl

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)

        Dim strReqOpt As String = "Required"
        Dim objReqOpt As HtmlControl = NewControl("span", "reqopt", strReqOpt)


        Dim objControl As New ListBox
        objControl.ID = param.FieldName
        objControl.CssClass += " form-control"
        objControl.SelectionMode = ListSelectionMode.Single

        'objControl.Text = ReadGetPost(pg, param.Name, "")
        If param.IsMultiValued Then
            objControl.SelectionMode = ListSelectionMode.Multiple
            If param.AllowedValues.Count < 5 Then
                objControl.Rows = param.AllowedValues.Count
            Else
                objControl.Rows = 5
            End If
            'objControl.AutoPostBack = True
            'AddHandler objControl.SelectedIndexChanged, AddressOf OnSelectedItemChange
        End If
        objLabel.AssociatedControlID = objControl.ID


        Dim defval As New List(Of String)
        Dim postget As String = ReadGetPost(pg, param.Name, "")
        If Not (param.DefaultValue Is Nothing) Then defval = param.DefaultValue
        If postget <> "" Then
            defval = postget.Split(New String() {vbCrLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
        End If

        If param.IsMandatory = False Then
            If Not param.Name.Contains("FPITLS") Then
                Dim objLI As New ListItem
                If param.DefaultValue Is Nothing Then
                    objLI.Selected = True
                End If
                objLI.Text = "--Select--"
                objLI.Value = "--Select--"
                objControl.Items.Add(objLI)
            End If

        End If

        For Each allowedval As String In param.AllowedValues
            Dim objLI As New ListItem
            objLI.Value = allowedval
            objLI.Text = allowedval
            If defval.Contains(allowedval) Then objLI.Selected = True
            objControl.Items.Add(objLI)
        Next

        'label, reqopt, control into row
        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow

    End Function

    Protected Sub OnSelectedIndexChanged(sender As Object, e As EventArgs)
        Dim SessionID = HttpContext.Current.Session.SessionID

        Dim tmpSender As New System.Web.UI.WebControls.DropDownList
        If (WebJEA._default.SessionValues.Item(SessionID).ContainsKey("REFRESH_" + sender.clientID)) Then
            tmpSender = sender
            tmpSender.SelectedIndex = 0
            WebJEA._default.SessionValues.Item(SessionID).Remove("REFRESH_" + sender.clientID)
            sender = tmpSender
        End If
    End Sub
    Protected Sub OnSelectedIndexChanged2(sender As Object, e As EventArgs)
        Dim SessionID = HttpContext.Current.Session.SessionID

        Dim tmpSender As New System.Web.UI.WebControls.DropDownList
        If (WebJEA._default.SessionValues.Item(SessionID).ContainsKey("REFRESH_" + sender.clientID)) Then
            tmpSender = sender
            tmpSender.SelectedIndex = 0
            WebJEA._default.SessionValues.Item(SessionID).Remove("REFRESH_" + sender.clientID)
            sender = tmpSender
        End If
    End Sub
    Private Function NewControlStringDropdown(pg As Page, param As PSCmdParam) As HtmlControl
        Dim SessionID = HttpContext.Current.Session.SessionID

        Dim objRow As HtmlGenericControl = NewControl("div", "form-group")

        'generate string inputs
        Dim objLabel As Label = NewControlLabel(param.VisibleName)

        Dim strReqOpt As String = "Required"
        Dim objReqOpt As HtmlControl = NewControl("span", "reqopt", strReqOpt)

        Dim objControl As New DropDownList
        objControl.ID = param.FieldName
        objControl.CssClass += "form-control"

        'SetAutoPostBack for Object
        If param.AutoPostBack = True Then
            objControl.AutoPostBack = True
            AddHandler objControl.SelectedIndexChanged, AddressOf OnSelectedIndexChanged

        Else
            AddHandler objControl.Unload, AddressOf OnSelectedIndexChanged2
        End If

        'objControl.Text = ReadGetPost(pg, param.Name, "")
        objLabel.AssociatedControlID = objControl.ID
        'objControl.BorderStyle = BorderStyle.Ridge
        Dim defval As String = Nothing
        If Not String.IsNullOrEmpty(param.DefaultValue) Then
            defval = param.DefaultValue
        End If
        defval = ReadGetPost(pg, param.Name, defval)

        Dim showSelection As Boolean = True

        If True Then 'just to scope objli for now
            Dim objLI As New ListItem
            If (defval Is Nothing) Then
                objLI.Selected = True
            End If
            'Diffrent Text if in a FormGroup
            If param.FormGroup IsNot "" Then
                'objLI.Text = ""
                'objLI.Value = ""
                If (Not WebJEA._default.SessionValues.Item(SessionId).ContainsKey("psparam_" + param.FormGroup) Or (Not WebJEA._default.SessionValues.Item(SessionId).ContainsKey("psparam_" + param.BackLinkFormGroup) And Not param.BackLinkFormGroup = "")) Then
                    'DefaultValue = WebJEA._default.SessionValues.Item(SessionID).Item("psparam_" + FormGroup)
                    'psparam.FormGroup = FormGroup
                    objLI.Text = "Bitte zuerst einen Wert aus dem Feld " + param.PostBackVisibleName + " auswählen."
                    objLI.Value = ""
                    showSelection = False
                Else
                    If param.AllowedValues(0) = "Keine Daten" Then
                        objLI.Text = "Das Feld " + param.PostBackVisibleName + " hat keine Daten geliefert."
                        objLI.Value = ""
                        showSelection = False
                    Else
                        objLI.Text = "--Select--"
                        objLI.Value = ""
                    End If
                End If
            Else

                objLI.Text = "--Select--"
                objLI.Value = ""

            End If

            objControl.Items.Add(objLI)
        End If

        If showSelection Then
            For Each allowedval As String In param.AllowedValues
                Dim objLI As New ListItem
                objLI.Value = allowedval
                objLI.Text = allowedval
                If allowedval = defval Then
                    objLI.Selected = True
                End If
                objControl.Items.Add(objLI)
            Next
        End If

        'label, reqopt, control into row

        objRow.Controls.Add(objLabel)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objRow)
        If param.IsMandatory Then AddMessageRequired(objRow)
        objRow.Controls.Add(objControl)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow

    End Function

    Public Function NewControlSwitch(pg As Page, param As PSCmdParam) As HtmlControl

        'this is the output object, everything goes in it
        Dim objRow As HtmlGenericControl = NewControl("div", "checkbox")

        'generate the label
        Dim objLabel As New Label
        'objLabel.ID = "psparamlbl_" & param.Name

        'text to display in the label
        Dim strLabel As String = param.Name
        Dim objName As HtmlControl = NewControl("span", "badge", strLabel)

        'generate the actual control
        Dim objControl As New CheckBox
        objControl.ID = param.FieldName
        objLabel.AssociatedControlID = objControl.ID
        Dim testresult As Boolean
        If Boolean.TryParse(param.DefaultValue, testresult) Then
            objControl.Checked = testresult
        End If
        objControl.Checked = ReadGetPost(pg, param.Name, objControl.Checked)

        'Add the message
        'control and text go inside the label for checkboxes
        objLabel.Controls.Add(objControl)
        objLabel.Controls.Add(objName)
        If Not String.IsNullOrWhiteSpace(param.HelpMessage) Then AddMessageHelp(param.HelpMessage, objLabel)
        If param.IsMandatory Then AddMessageRequired(objLabel)

        'label goes inside row
        objRow.Controls.Add(objLabel)
        '   then help
        If Not String.IsNullOrEmpty(param.HelpDetail) Then AddMessageHelpDetail(param.HelpDetail, objRow)

        '   then validation
        Dim valctrls As List(Of WebControl) = GetControlValidations(param)
        For Each valctrl In valctrls
            objRow.Controls.Add(valctrl)
        Next


        Return objRow
    End Function

    Private Function NewControlLabel(Text As String) As Label
        Dim objLabel As New Label
        objLabel.Text = Text
        objLabel.CssClass = "badge"

        Return objLabel
    End Function

    Public Function NewControl(tag As String, cssClass As String, Optional innerText As String = "") As HtmlGenericControl
        Dim row As New HtmlGenericControl(tag)
        row.Attributes.Add("class", cssClass)
        If Not String.IsNullOrEmpty(innerText) Then
            row.InnerText = innerText
        End If

        Return row
    End Function


    Public Function GetControlValidations(param As PSCmdParam) As List(Of WebControl)
        'Mandatory: <asp:RequiredFieldValidator ID="RequiredFieldValidator1" runat="server" ErrorMessage="Mandatory" SetFocusOnError="True" ControlToValidate="X"></asp:RequiredFieldValidator>
        'ValidateRange: <asp:RangeValidator ID="RangeValidator1" runat="server" ErrorMessage="RangeValidator" MinimumValue="1" MaximumValue="2" ControlToValidate="txtxX" Text="Not within range (x, y)"></asp:RangeValidator>
        'ValidatePattern: <asp:RegularExpressionValidator ID="RegularExpressionValidator1" runat="server" ErrorMessage="RegularExpressionValidator" ControlToValidate="txtX" ValidationExpression=".+"></asp:RegularExpressionValidator>
        'ValidateLength: <asp:RegularExpressionValidator ID="RegularExpressionValidator1" runat="server" ErrorMessage="RegularExpressionValidator" ControlToValidate="txtX" ValidationExpression=".{3,30}"></asp:RegularExpressionValidator>
        'ValidateCount: <asp:CustomValidator ID="CustomValidator1" runat="server" ErrorMessage="CustomValidator" SetFocusOnError="True" ControlToValidate="xxx" ClientValidationFunction="valLb(obj,1,2)"></asp:CustomValidator>
        Dim retctrls As New List(Of WebControl)
        For Each valobj As PSCmdParamVal In param.ValidationObjects
            If valobj.Type = PSCmdParamVal.ValType.Mandatory And param.ParamType <> PSCmdParam.ParameterType.PSBoolean Then
                Dim valctrl As New RequiredFieldValidator()
                valctrl.ErrorMessage = "Required Field"
                valctrl.CssClass = "valmsg"
                valctrl.SetFocusOnError = True
                valctrl.ControlToValidate = param.FieldName
                retctrls.Add(valctrl)
            ElseIf valobj.Type = PSCmdParamVal.ValType.Mandatory And param.ParamType = PSCmdParam.ParameterType.PSBoolean Then
                'https://stackoverflow.com/questions/1228112/how-do-i-make-a-checkbox-required-on-an-asp-net-form
                'but caught via ps parameter validation at least

                Dim valctrl As New CustomValidator
                valctrl.ClientValidationFunction = "validateMandatoryCheckbox"
                valctrl.ErrorMessage = "You must check the box For " & param.Name & "."
                valctrl.CssClass = "valmsg"
                'valctrl.EnableClientScript = True
                valctrl.SetFocusOnError = True
                'you can't point a customvalidator directly at a checkbox, the checkbox can't be validated.
                'this stored the control in the data-control attribute and that is read and used to check the actual control.
                valctrl.Attributes("data-control") = param.FieldName
                retctrls.Add(valctrl)

            ElseIf valobj.Type = PSCmdParamVal.ValType.Length Then
                Dim valctrl As New RegularExpressionValidator()
                valctrl.ValidationExpression = "[\S\s]{" & valobj.LowerLimit & "," & valobj.UpperLimit & "}"
                valctrl.ErrorMessage = "Not In allowed length (" & valobj.LowerLimit & "-" & valobj.UpperLimit & ")"
                valctrl.CssClass = "valmsg"
                valctrl.SetFocusOnError = True
                valctrl.ControlToValidate = param.FieldName
                retctrls.Add(valctrl)
            ElseIf valobj.Type = PSCmdParamVal.ValType.Pattern Then
                Dim valctrl As New RegularExpressionValidator()
                valctrl.ValidationExpression = valobj.Pattern
                valctrl.ErrorMessage = "Did Not match pattern: " & valobj.Pattern
                valctrl.CssClass = "valmsg"
                valctrl.SetFocusOnError = True
                valctrl.ControlToValidate = param.FieldName
                retctrls.Add(valctrl)
            ElseIf valobj.Type = PSCmdParamVal.ValType.Range Then
                Dim valctrl As New RangeValidator()
                valctrl.MinimumValue = valobj.LowerLimit
                valctrl.MaximumValue = valobj.UpperLimit
                valctrl.ErrorMessage = "Not in allowed range (" & valobj.LowerLimit & "-" & valobj.UpperLimit & ")"
                valctrl.CssClass = "valmsg"
                If param.ParamType = PSCmdParam.ParameterType.PSInt Then
                    valctrl.Type = ValidationDataType.Integer
                ElseIf param.ParamType = PSCmdParam.ParameterType.PSFloat Then 'single/double
                    valctrl.Type = ValidationDataType.Double
                ElseIf param.ParamType = PSCmdParam.ParameterType.PSDate Then
                    valctrl.Type = ValidationDataType.Date
                End If
                valctrl.SetFocusOnError = True
                valctrl.ControlToValidate = param.FieldName
                retctrls.Add(valctrl)
            ElseIf valobj.Type = PSCmdParamVal.ValType.Count Then
                Dim valctrl As New CustomValidator
                valctrl.ClientValidationFunction = "validateCollection"
                valctrl.ErrorMessage = "Number of selected items not in allowed range (" & valobj.LowerLimit & "-" & valobj.UpperLimit & ")"
                valctrl.CssClass = "valmsg"
                valctrl.Attributes("data-min") = valobj.LowerLimit
                valctrl.Attributes("data-max") = valobj.UpperLimit
                'valctrl.EnableClientScript=true
                valctrl.SetFocusOnError = True
                valctrl.ControlToValidate = param.FieldName
                retctrls.Add(valctrl)
            ElseIf valobj.Type = PSCmdParamVal.ValType.SetCol Then
                'do nothing, this is handled by forcing a SELECT field
            Else
                dlog.Error("Unknown Validation Rule: " & valobj.Rule)

            End If
        Next


        Return retctrls
    End Function

#End Region

#Region "Get Inputs"

    Public Function getParameters(cmd As PSCmd, page As Page, ByRef uinfo As UserInfo) As Dictionary(Of String, Object)
        Dim params As New Dictionary(Of String, Object)

        If Not (cmd.Parameters Is Nothing) Then
            For Each param As PSCmdParam In cmd.Parameters
                Dim ctrl As WebControl = CType(page.FindControl(param.FieldName), WebControl)

                If (param.Name.ToUpper().StartsWith("WEBJEA")) Then
                    GetParameterInternal(param, page, params, uinfo)
                ElseIf (param.IsSelect) Then 'array
                    GetParameterSelect(param, page, params)
                ElseIf param.ParamType = PSCmdParam.ParameterType.PSBoolean Then
                    GetParameterCheckbox(param, page, params)
                ElseIf (param.ParamType = PSCmdParam.ParameterType.PSString) Then
                    GetParameterString(param, page, params)
                ElseIf (param.ParamType = PSCmdParam.ParameterType.PSFloat) Then
                    GetParameterString(param, page, params)
                ElseIf (param.ParamType = PSCmdParam.ParameterType.PSInt) Then
                    GetParameterString(param, page, params)
                ElseIf (param.ParamType = PSCmdParam.ParameterType.PSDate) Then
                    GetParameterString(param, page, params)
                ElseIf (param.ParamType = PSCmdParam.ParameterType.PSButton) Then
                    GetParameterButton(param, page, params)
                Else
                    dlog.Warn("Processing: " & param.Name & " as string, type (" & param.VarType & ") not expected")
                    GetParameterString(param, page, params)
                End If

            Next
        End If


        Return params
    End Function
    Private Sub GetParameterInternal(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object), ByRef uinfo As UserInfo)
        'These are special parameters that are not presented to the user, but are handled internally.
        If param.Name.ToUpper() = "WEBJEAUSERNAME" Then
            params.Add(param.Name, uinfo.UserName)
            'ElseIf param.Name.ToUpper() = "WEBJEAUPN" Then
            '    params.Add(param.Name, uinfo.UserPrincipalName)
        ElseIf param.Name.ToUpper() = "WEBJEAHOSTNAME" Then
            params.Add(param.Name, page.Request.UserHostName)
        Else
            dlog.Warn("Parameter Name '" & param.Name & "' is not a recognized internal parameter and cannot be used.  Parameters with WEBJEA prefixes are reserved.")
        End If

    End Sub
    Private Sub GetParameterSelect(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))
        If param.IsMultiValued Then
            GetParameterListbox(param, page, params)
        Else
            GetParameterDropdown(param, page, params)
        End If
    End Sub
    Private Sub GetParameterListbox(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))

        Dim ctrl As ListBox = page.FindControl(param.FieldName)
        If ctrl.GetSelectedIndices.Count > 0 Then 'only add the parameter if it is not empty

            Dim valset As New List(Of String)
            For Each idx As Integer In ctrl.GetSelectedIndices.ToList
                If ctrl.Items(idx).Value <> "" And ctrl.Items(idx).Value <> "--Select--" Then
                    valset.Add(ctrl.Items(idx).Value)
                End If
            Next
            params.Add(param.Name, valset.ToArray)

        End If

    End Sub

    Private Sub GetParameterDropdown(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))

        Dim ctrl As DropDownList = page.FindControl(param.FieldName)
        If ctrl.SelectedValue <> "" And ctrl.SelectedValue <> "--Select--" Then 'only add the parameter if it is not empty
            params.Add(param.Name, ctrl.SelectedValue)
        End If

    End Sub

    Private Sub GetParameterString(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))

        If param.IsMultiValued Then
            GetParameterStringSet(param, page, params)
        Else
            GetParameterStringSingle(param, page, params)
        End If

    End Sub

    Private Sub GetParameterStringSingle(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))
        Dim ctrl As TextBox = page.FindControl(param.FieldName)
        If ctrl.Text <> "" Then 'only add the parameter if it is not empty
            params.Add(param.Name, ctrl.Text)
        End If
    End Sub

    Private Sub GetParameterButton(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))
        Dim ctrl As Button = page.FindControl(param.FieldName)
        If ctrl.Text <> "" Then 'only add the parameter if it is not empty
            params.Add(param.Name, ctrl.Text)
        End If
    End Sub

    Private Sub GetParameterStringSet(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))
        'string field, multiline, freeform array of names (such as computernames)
        Dim trimchar As Char() = New Char() {vbCr, vbLf}
        Dim ctrl As TextBox = page.FindControl(param.FieldName)

        Dim strarr As String() = ctrl.Text.Split(vbLf)
        Dim strlist As New List(Of String)
        For Each item As String In strarr
            If item.Trim(trimchar).Trim <> "" Then
                strlist.Add(item.Trim(trimchar).Trim)
            End If
        Next
        If strlist.Count > 0 Then
            params.Add(param.Name, strlist.ToArray)
        End If

    End Sub

    Private Sub GetParameterCheckbox(param As PSCmdParam, page As Page, ByRef params As Dictionary(Of String, Object))

        Dim ctrl As CheckBox = page.FindControl(param.FieldName)
        params.Add(param.Name, ctrl.Checked)

    End Sub


    Private Function ReadGetPost(pg As Page, param As String, DefaultValue As String) As String
        'check both GET and POST for parameter, if not found, return defaultvalue
        'prefer post over get for security

        If pg.Request.Form(param) IsNot Nothing Then
            Return pg.Request.Form(param)
        ElseIf pg.Request.QueryString(param) IsNot Nothing Then
            Return pg.Request.QueryString(param)
        Else
            Return DefaultValue
        End If

    End Function
    Private Function ReadGetPost(pg As Page, param As String, DefaultValue As Boolean) As Boolean
        'check both GET and POST for parameter, if not found, return defaultvalue
        'prefer post over get for security

        If pg.Request.Form(param) IsNot Nothing Then
            Return pg.Request.Form(param)
        ElseIf pg.Request.QueryString(param) IsNot Nothing Then
            Return pg.Request.QueryString(param)
        Else
            Return DefaultValue
        End If

    End Function

#End Region



End Class
