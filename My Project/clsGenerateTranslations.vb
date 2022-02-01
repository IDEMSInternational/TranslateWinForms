﻿
Imports System.ComponentModel
Imports System.Data.SQLite
' IDEMS International
' Copyright (C) 2021
'
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License 
' along with this program.  If not, see <http://www.gnu.org/licenses/>.
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

'''------------------------------------------------------------------------------------------------
''' <summary>   
''' Provides utility functions to translate the text in WinForm objects (e.g. menu items, forms and 
''' controls) to a different natural language (e.g. to French). 
''' <para>
''' This class uses an SQLite database to translate text items to a new language. The database must 
''' contain the following tables:
''' <code>
''' CREATE TABLE "form_controls" (
'''	"form_name"	TEXT,
'''	"control_name"	TEXT,
'''	"id_text"	TEXT NOT NULL,
'''	PRIMARY KEY("form_name", "control_name")
''' )
''' </code><code>
''' CREATE TABLE "translations" (
'''	"id_text"	TEXT,
'''	"language_code"	TEXT,
'''	"translation"	TEXT NOT NULL,
'''	PRIMARY KEY("id_text", "language_code")
''' )
''' </code></para><para>
''' For example, if the 'form_controls' table contains a row with the values 
''' {'frmMain', 'mnuFile', 'File'}, 
''' then the 'translations' table should have a row for each supported language, e.g. 
''' {'File', 'en', 'File'}, {'File', 'fr', 'Fichier'}.
''' </para><para>
''' Note: This class is intended to be used solely as a 'static' class (i.e. contains only shared 
''' members, cannot be instantiated and cannot be inherited from).
''' In order to enforce this (and prevent developers from using this class in an unintended way), 
''' the class is declared as 'NotInheritable` and the constructor is declared as 'Private'.</para>
''' </summary>
'''------------------------------------------------------------------------------------------------

Public NotInheritable Class clsGenerateTranslations

    '''--------------------------------------------------------------------------------------------
    ''' <summary> 
    ''' Declare constructor 'Private' to prevent instantiation of this class (see class comments 
    ''' for more details). 
    ''' </summary>
    '''--------------------------------------------------------------------------------------------
    Private Sub New()
    End Sub


    '''--------------------------------------------------------------------------------------------
    ''' <summary>
    ''' Updates the 'form_controls' database table with controls in the passed datatable
    ''' </summary>
    ''' <param name="strDatabaseFilePath">The file path to the sqlite database file</param>
    ''' <param name="datatableControls">The form controls datatable with 3 columns; form_name, control_name, id_text.</param>
    ''' <param name="strTranslateIgnoreFilePath">Optional. The file path to the translate ignore file. If passed translate ignore controls will also be processed.</param>
    ''' <returns>The Number of successful updates.</returns>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function UpdateFormControlsTable(strDatabaseFilePath As String, datatableControls As DataTable, Optional strTranslateIgnoreFilePath As String = "") As Integer
        Dim iRowsUpdated As Integer = 0
        Try
            'connect to the database and execute the SQL command 
            Dim clsBuilder As New SQLiteConnectionStringBuilder With {
                    .FailIfMissing = True,
                    .DataSource = strDatabaseFilePath}
            Using clsConnection As New SQLiteConnection(clsBuilder.ConnectionString)
                clsConnection.Open()
                'todo. do batch execution for optimal performance
                For Each row As DataRow In datatableControls.Rows

                    Dim paramFormName As New SQLiteParameter("form_name", row.Field(Of String)(0))
                    Dim paramControlName As New SQLiteParameter("control_name", row.Field(Of String)(1))
                    Dim paramIdText As New SQLiteParameter("id_text", row.Field(Of String)(2))

                    'delete record if exists first 
                    Dim sqlDeleteCommand As String = "DELETE FROM form_controls WHERE form_name = @form_name AND control_name=@control_name"
                    Using cmdDelete As New SQLiteCommand(sqlDeleteCommand, clsConnection)
                        cmdDelete.Parameters.Add(paramFormName)
                        cmdDelete.Parameters.Add(paramControlName)
                        cmdDelete.ExecuteNonQuery()
                    End Using

                    'insert the new record
                    Dim sqlInsertCommand As String = "INSERT INTO form_controls (form_name,control_name,id_text) VALUES (@form_name,@control_name,@id_text)"
                    Using cmdInsert As New SQLiteCommand(sqlInsertCommand, clsConnection)
                        cmdInsert.Parameters.Add(paramFormName)
                        cmdInsert.Parameters.Add(paramControlName)
                        cmdInsert.Parameters.Add(paramIdText)
                        iRowsUpdated += cmdInsert.ExecuteNonQuery()
                    End Using
                Next

                clsConnection.Close()
            End Using

        Catch e As Exception
            Throw New Exception("Error: Could NOT update the form_controls database table", e)
        End Try

        If iRowsUpdated <> datatableControls.Rows.Count Then
            Throw New Exception("Error: Could NOT save all form controls to the form_controls table. Rows saved: " & iRowsUpdated)
        End If

        If Not String.IsNullOrEmpty(strTranslateIgnoreFilePath) Then
            SetFormControlsToTranslateIgnore(strDatabaseFilePath, strTranslateIgnoreFilePath)
        End If

        Return iRowsUpdated
    End Function

    '''--------------------------------------------------------------------------------------------
    ''' <summary>
    ''' Updates the 'translations' database table with the texts from controls in the passed datatable
    ''' </summary>   
    ''' <param name="strDatabasePath">The file path to the sqlite database</param>
    ''' <param name="datatableTranslations">The translations datatable with 3 columns; id_text, language_code, translation.</param>
    ''' <returns>The Number of successful updates.</returns>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function UpdateTranslationsTable(strDatabasePath As String, datatableTranslations As DataTable) As Integer
        Dim iRowsUpdated As Integer = 0
        Try
            Dim clsBuilder As New SQLiteConnectionStringBuilder With {
                    .FailIfMissing = True,
                    .DataSource = strDatabasePath}
            Using clsConnection As New SQLiteConnection(clsBuilder.ConnectionString)
                clsConnection.Open()

                'todo. do batch execution for optimal performance
                For Each row As DataRow In datatableTranslations.Rows
                    Dim paramIdText As New SQLiteParameter("id_text", row.Field(Of String)(0))
                    Dim paramLangcode As New SQLiteParameter("language_code", row.Field(Of String)(1))
                    Dim paramTranslation As New SQLiteParameter("translation", row.Field(Of String)(2))

                    'delete record if exists first 
                    Dim sqlDeleteCommand As String = "DELETE FROM translations WHERE id_text = @id_text AND language_code=@language_code"
                    Using cmdDelete As New SQLiteCommand(sqlDeleteCommand, clsConnection)
                        cmdDelete.Parameters.Add(paramIdText)
                        cmdDelete.Parameters.Add(paramLangcode)
                        cmdDelete.ExecuteNonQuery()
                    End Using

                    'insert the new record
                    Dim sqlInsertCommand As String = "INSERT INTO translations (id_text,language_code,translation) VALUES (@id_text,@language_code,@translation)"
                    Using cmdInsert As New SQLiteCommand(sqlInsertCommand, clsConnection)
                        cmdInsert.Parameters.Add(paramIdText)
                        cmdInsert.Parameters.Add(paramLangcode)
                        cmdInsert.Parameters.Add(paramTranslation)
                        iRowsUpdated += cmdInsert.ExecuteNonQuery()
                    End Using
                Next

                clsConnection.Close()
            End Using
        Catch e As Exception
            Throw New Exception("Error: Could NOT update the translations database table", e)
        End Try

        If iRowsUpdated <> datatableTranslations.Rows.Count Then
            Throw New Exception("Error: Could NOT save all form id texts to the translations table. Rows saved: " & iRowsUpdated)
        End If

        Return iRowsUpdated
    End Function

    Public Shared Sub UpdateTranslationsTableFromControls(strDatabasePath As String, datatableControls As DataTable)
        Dim datatableTranslations As DataTable = GetTranslationTextsFromControls(datatableControls)
        UpdateTranslationsTable(strDatabasePath, datatableTranslations)
    End Sub

    '''--------------------------------------------------------------------------------------------
    ''' <summary>
    ''' Gets a datatable with all form controls from the list of forms passed
    ''' </summary>
    ''' <param name="lstForms">The forms to get the controls for translations</param>
    ''' <returns>A datatable with 3 columns; form_name, control_name, id_text. </returns>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function GetControlsDatatable(lstForms As List(Of Form)) As DataTable
        Dim datatableControls As New DataTable
        datatableControls.Columns.Add("form_name", GetType(String))
        datatableControls.Columns.Add("control_name", GetType(String))
        datatableControls.Columns.Add("id_text", GetType(String))

        For Each frm As Form In lstForms
            Dim dctComponents As Dictionary(Of String, Component) = New Dictionary(Of String, Component)
            clsWinformsComponents.FillDctComponentsFromControl(frm, dctComponents)

            For Each clsComponent In dctComponents
                Dim idText As String

                If TypeOf clsComponent.Value Is Control Then
                    idText = GetActualTranslationText(DirectCast(clsComponent.Value, Control).Text)
                ElseIf TypeOf clsComponent.Value Is ToolStripItem Then
                    idText = GetActualTranslationText(DirectCast(clsComponent.Value, ToolStripItem).Text)
                Else
                    Throw New Exception("Developer Error: Translation dictionary entry (" & frm.Name & "," & clsComponent.Key & ") contained unexpected value type.")
                    Exit For
                End If
                'add row of form_name, control_name, id_text
                datatableControls.Rows.Add(frm.Name, clsComponent.Key, idText)
            Next

            'Special case for radio buttons in panels: 
            '  Before the dialog is shown, each radio button is a direct child of the dialog 
            '  (e.g. 'dlg_Augment_rdoNewDataframe'). After the dialog is shown, the raio button becomes 
            '  a direct child of its parent panel.
            '  Therefore, we need to show the dialog before we traverse the dialog's control hierarchy.
            '  Unfortunately showing the dialog means that it has to be manually closed. So we only 
            '  show the dialog for this special case to save the developer from having to manually 
            '  close too many dialogs.
            '  TODO: launch each dialog in a new thread to avoid need for manual close?
            'If strTemp.ToLower().Contains("pnl") AndAlso strTemp.ToLower().Contains("rdo") Then
            '    'frmTemp.ShowDialog()
            '    frmTemp.Show()
            '    strTemp = GetControls(frmTemp)
            '    frmTemp.Close()
            'End If
        Next

        Return datatableControls
    End Function

    'todo. can probably be improved futher to include "DoNotTranslate".
    Private Shared Function GetActualTranslationText(strText As String) As String
        If String.IsNullOrEmpty(strText) OrElse
            strText.Contains(vbCr) OrElse    'multiline text
            strText.Contains(vbLf) OrElse Not Regex.IsMatch(strText, "[a-zA-Z]") Then
            'Regex.IsMatch(strText, "CheckBox\d+$") OrElse 'CheckBox1, CheckBox2 etc. normally indicates dynamic translation
            'Regex.IsMatch(strText, "Label\d+$") OrElse 'Label1, Label2 etc. normally indicates dynamic translation

            'text that doesn't contain any letters (e.g. number strings)
            Return "ReplaceWithDynamicTranslation"
        End If
        Return strText
    End Function

    '''--------------------------------------------------------------------------------------------
    ''' <summary>
    ''' Gets the translation texts from a datatable that has the forms controls texts
    ''' </summary>
    ''' <param name="datatableControls">The form controls datatable; form_name, control_name, id_text. </param>
    ''' <param name="langCode">The translations texts language code</param>
    ''' <returns>The translations datatable with 3 columns; id_text, language_code, translation.</returns>
    '''--------------------------------------------------------------------------------------------
    Private Shared Function GetTranslationTextsFromControls(datatableControls As DataTable, Optional langCode As String = "en") As DataTable
        'Fill translations table from the form controls table
        Dim datatableTranslations As New DataTable
        ' Create 3 columns in the DataTable.
        datatableTranslations.Columns.Add("id_text", GetType(String))
        datatableTranslations.Columns.Add("language_code", GetType(String))
        datatableTranslations.Columns.Add("translation", GetType(String))
        For Each row As DataRow In datatableControls.Rows
            'ignore "ReplaceWithDynamicTranslation" id text
            If row.Field(Of String)(2) = "ReplaceWithDynamicTranslation" Then
                Continue For
            End If
            'add id_text, language_code, translation
            datatableTranslations.Rows.Add(row.Field(Of String)(2), langCode, row.Field(Of String)(2))
        Next
        Return datatableTranslations
    End Function



    '''--------------------------------------------------------------------------------------------
    ''' <summary>   
    '''    Updates the `TranslateWinForm` database based on the specifications in the 
    '''    'translateIgnore.txt' file. This file provides a way to ignore specified WinForm 
    '''    controls when the application or dialog is translated into a different language.
    '''    <para>
    '''    For example, this file can be used to ensure that text that references pre-existing data 
    '''    or meta data (e.g. a file name, data frame name, column name, cell value etc.) stays the 
    '''    same, even when the rest of the dialog is translated into French or Portuguese.
    '''    </para><para>
    '''    This sub should be executed prior to each release to ensure that the `TranslateWinForm` 
    '''    database specifies all the controls to ignore during the translation.  </para> 
    ''' <param name="strDatabaseFilePath">The database file path</param>
    ''' <param name="strTranslateIgnoreFilePath">The translate ignore file path</param>
    ''' <returns>The number of successful updates.</returns> 
    ''' </summary>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function SetFormControlsToTranslateIgnore(strDatabaseFilePath As String, strTranslateIgnoreFilePath As String) As Integer
        Dim iRowsUpdated As Integer = 0
        Dim lstIgnore As New List(Of String)
        Dim lstIgnoreNegations As New List(Of String)

        Try
            'For each line in the ignore file 
            Using clsReader As New StreamReader(strTranslateIgnoreFilePath)
                Do While clsReader.Peek() >= 0
                    Dim strIgnoreFileLine = clsReader.ReadLine().Trim()
                    If String.IsNullOrEmpty(strIgnoreFileLine) Then
                        Continue Do
                    End If

                    Select Case strIgnoreFileLine(0)
                        Case "#"
                        'Ignore comment lines
                        Case "!"
                            'Add negation pattern to negation list
                            lstIgnoreNegations.Add(strIgnoreFileLine.Substring(1)) 'remove leading '!'
                        Case Else
                            'Add pattern to ignore list
                            lstIgnore.Add(strIgnoreFileLine)
                    End Select
                Loop
            End Using
        Catch e As Exception
            Throw New Exception("Error: Could NOT process ignore file: " & strTranslateIgnoreFilePath, e)
        End Try
        'If the ignore file didn't contain any specifications, then it's probably an error
        'please note its not expected that the product developer will run this function
        'if no ignore specifications are defined in the file
        If lstIgnore.Count <= 0 AndAlso lstIgnoreNegations.Count <= 0 Then
            Throw New Exception("Error: The " & strTranslateIgnoreFilePath & " ignore file was processed. No ignore specifications were found. " &
                   "The database was not updated.")
            Return iRowsUpdated
        End If

        'create the SQL command to update the database
        Dim strSqlUpdate As String = "UPDATE form_controls SET id_text = 'DoNotTranslate' WHERE "

        If lstIgnore.Count > 0 Then
            strSqlUpdate &= "("
            For iListPos As Integer = 0 To lstIgnore.Count - 1
                strSqlUpdate &= If(iListPos > 0, " OR ", "")
                strSqlUpdate &= "control_name LIKE '" & lstIgnore.Item(iListPos) & "'"
            Next iListPos
            strSqlUpdate &= ")"
        End If

        If lstIgnoreNegations.Count > 0 Then
            strSqlUpdate &= If(lstIgnore.Count > 0, " AND ", "")
            strSqlUpdate &= "NOT ("
            For iListPos As Integer = 0 To lstIgnoreNegations.Count - 1
                strSqlUpdate &= If(iListPos > 0, " OR ", "")
                strSqlUpdate &= "control_name LIKE '" & lstIgnoreNegations.Item(iListPos) & "'"
            Next iListPos
            strSqlUpdate &= ")"
        End If

        Try
            'connect to the database and execute the SQL command
            Dim clsBuilder As New SQLiteConnectionStringBuilder With {
                    .FailIfMissing = True,
                    .DataSource = strDatabaseFilePath}
            Using clsConnection As New SQLiteConnection(clsBuilder.ConnectionString)
                Using clsSqliteCmd As New SQLiteCommand(strSqlUpdate, clsConnection)
                    clsConnection.Open()
                    iRowsUpdated = clsSqliteCmd.ExecuteNonQuery()
                    clsConnection.Close()
                End Using
            End Using
        Catch e As Exception
            Throw New Exception("Error:Could NOT update translate ignore in the form_controls database table", e)
        End Try

        Return iRowsUpdated
    End Function


    Public Shared Function UpdateTranslationsTableFromCrowdInJSONFile(strDatabaseFilePath As String, strJsonFilePath As String, strLangCode As String) As Integer
        Dim iRowsUpdated As Integer = 0
        'Fill translations table from the form controls table
        Dim datatableTranslations As New DataTable



        Using reader As New System.IO.StreamReader("c:\person.json")
            Dim o As JObject = JToken.ReadFrom(New JsonTextReader(reader))

            Dim str As String = o.ToString
            'todo.

        End Using

        Return iRowsUpdated
    End Function


    Private Shared Function WriteTranslationsToCrowdInJSONFile(strDatabaseFilePath As String, strSaveFolderPath As String, strLangCode As String) As Boolean
        'todo. implementation
        Return True
    End Function


    '''--------------------------------------------------------------------------------------------
    ''' <summary>   
    '''     Recursively traverses the <paramref name="clsControl"/> control hierarchy and returns a
    '''     string containing the parent, name and associated text of each control. The string is 
    '''     formatted as a comma-separated list suitable for importing into a database.
    ''' </summary>
    '''
    ''' <param name="clsControl">   The control to process (it's children and sub-children shall 
    '''                             also be processed recursively). </param>
    '''
    ''' <returns>   
    '''     A string containing the parent, name and associated text of each control in the 
    '''     hierarchy. The string is formatted as a comma-separated list suitable for importing 
    '''     into a database. </returns>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function GetControlsAsCsv(clsControl As Control) As String
        If IsNothing(clsControl) Then
            Return ""
        End If

        Dim dctComponents As Dictionary(Of String, Component) = New Dictionary(Of String, Component)
        clsWinformsComponents.FillDctComponentsFromControl(clsControl, dctComponents)

        Dim strControlsAsCsv As String = ""
        For Each clsComponent In dctComponents
            If TypeOf clsComponent.Value Is Control Then
                Dim clsTmpControl As Control = DirectCast(clsComponent.Value, Control)
                strControlsAsCsv &= clsControl.Name & "," & clsComponent.Key & "," & GetCsvText(clsTmpControl.Text) & vbCrLf
            ElseIf TypeOf clsComponent.Value Is ToolStripItem Then
                Dim clsMenuItem As ToolStripItem = DirectCast(clsComponent.Value, ToolStripItem)
                strControlsAsCsv &= clsControl.Name & "," & clsComponent.Key & "," & GetCsvText(clsMenuItem.Text) & vbCrLf
            Else
                Throw New Exception("Developer Error: Translation dictionary entry (" & clsControl.Name & "," & clsComponent.Key & ") contained unexpected value type.")
            End If
        Next

        Return strControlsAsCsv
    End Function

    '''--------------------------------------------------------------------------------------------
    ''' <summary>   
    '''     Recursively traverses the <paramref name="clsMenuItems"/> menu hierarchy and returns a 
    '''     string containing the parent, name and associated text of each (sub)menu option in 
    '''     <paramref name="clsMenuItems"/>. The string is formatted as a comma-separated list 
    '''     suitable for importing into a database.
    ''' </summary>
    '''
    ''' <param name="clsControl">        The WinForm control that is the parent of the menu. </param>
    ''' <param name="clsMenuItems">     The WinForm menu items to add to the return string. </param>
    '''
    ''' <returns>   
    '''     A string containing the parent and name of each (sub)menu option in
    '''     <paramref name="clsMenuItems"/>. The string is formatted as a comma-separated list
    '''     suitable for importing into a database. </returns>
    '''--------------------------------------------------------------------------------------------
    Public Shared Function GetMenuItemsAsCsv(clsControl As Control, clsMenuItems As ToolStripItemCollection) As String
        If IsNothing(clsControl) OrElse IsNothing(clsMenuItems) Then
            Return ""
        End If

        Dim dctComponents As Dictionary(Of String, Component) = New Dictionary(Of String, Component)
        clsWinformsComponents.FillDctComponentsFromMenuItems(clsMenuItems, dctComponents)

        Dim strMenuItemsAsCsv As String = ""
        For Each clsComponent In dctComponents

            If TypeOf clsComponent.Value Is ToolStripItem Then
                Dim clsMenuItem As ToolStripItem = DirectCast(clsComponent.Value, ToolStripItem)
                strMenuItemsAsCsv &= clsControl.Name & "," & clsComponent.Key & "," & GetCsvText(clsMenuItem.Text) & vbCrLf
            Else
                Throw New Exception("Developer Error: Translation dictionary entry (" & clsControl.Name & "," & clsComponent.Key & ") contained unexpected value type.")
            End If

        Next
        Return strMenuItemsAsCsv
    End Function

    'todo. expressions checked in this function need to be defined at the product level
    '''--------------------------------------------------------------------------------------------
    ''' <summary>   
    '''    Decides whether <paramref name="strText"/> is likely to be changed during execution of 
    '''    the software. If no, then returns <paramref name="strText"/>. If yes, then returns 
    '''    'ReplaceWithDynamicTranslation'. It makes the decision based upon a set of heuristics.
    '''    <para>
    '''    This function is normally only used when creating a comma-separated list suitable for 
    '''    importing into a database. During program execution, the 'ReplaceWithDynamicTranslation'
    '''    text tells the library to dynamically try and translate the current text, rather than
    '''    looking up the static text associated with the control.</para></summary>
    '''
    ''' <param name="strText">  The text to assess. </param>
    '''
    ''' <returns>   Decides whether <paramref name="strText"/> is likely to be changed during 
    '''             execution of the software. If no, then returns <paramref name="strText"/>. 
    '''             If yes, then returns'ReplaceWithDynamicTranslation'. </returns>
    '''--------------------------------------------------------------------------------------------
    Private Shared Function GetCsvText(strText As String) As String
        If String.IsNullOrEmpty(strText) OrElse
                strText.Contains(vbCr) OrElse strText.Contains(vbLf) OrElse 'multiline text
                Regex.IsMatch(strText, "CheckBox\d+$") OrElse 'CheckBox1, CheckBox2 etc. normally indicates dynamic translation
                Regex.IsMatch(strText, "Label\d+$") OrElse 'Label1, Label2 etc. normally indicates dynamic translation
                Regex.IsMatch(strText, "ToolStrip\d+$") OrElse 'ToolStripSplitButton1, ToolStripSplitButton2 etc. normally indicates dynamic translation
                Not Regex.IsMatch(strText, "[a-zA-Z]") Then 'text that doesn't contain any letters (e.g. number strings)
            Return "ReplaceWithDynamicTranslation"
        End If
        Return strText
    End Function





End Class