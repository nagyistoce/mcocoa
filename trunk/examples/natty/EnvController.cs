// Copyright (C) 2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using MCocoa;
using MObjc;
using MObjc.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

[ExportClass("EnvController", "NSObject", Outlets = "sheet table")]
internal sealed class EnvController : NSObject
{
	private EnvController(IntPtr instance) : base(instance)
	{
		m_sheet = this["sheet"].To<NSWindow>();
		m_table = this["table"].To<NSTableView>();
	}
	
	public void Open(Document doc, NSWindow window)
	{
		m_doc = doc;
		m_vars = new List<EnvVar>(m_doc.Variables);
		
		m_sheet.setDelegate(this);
		m_table.setDataSource(this);
		NSApplication.sharedApplication().beginSheet_modalForWindow_modalDelegate_didEndSelector_contextInfo(
			m_sheet, window, this, null, IntPtr.Zero);
	}
	
	#region Action Handlers
	public void envOK(NSObject sender)
	{
		NSApplication.sharedApplication().endSheet(m_sheet);
		m_sheet.orderOut(this);
		
		m_doc.Variables.Clear();
		m_doc.Variables.AddRange(m_vars);
		m_doc.SavePrefs();
	}

	public void envCancel(NSObject sender)
	{
		NSApplication.sharedApplication().endSheet(m_sheet);
		m_sheet.orderOut(this);
	}
	
	public void restoreDefaults(NSObject sender)
	{
		foreach (EnvVar v in m_vars)
			v.Value = v.DefaultValue;
		
		m_table.reloadData();
	}
	#endregion
	
	#region Data Source
	public int numberOfRowsInTableView(NSTableView table)
	{
		return m_vars.Count;
	}
	
	public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn column, int row)
	{
		EnvVar variable = m_vars[row];
		
		if (column.identifier().ToString() == "1")
			return NSString.Create(variable.Name);
		else
			if (variable.Value.Length > 0)
				return NSString.Create(variable.Value);
			else
				return NSString.Create(variable.DefaultValue);
	}
	
	public void tableView_setObjectValue_forTableColumn_row(NSTableView table, NSObject v, NSTableColumn column, int row)
	{
		if ("1" == column.identifier().ToString())
			m_vars[row].Name = v.ToString();
		
		else if ("2" == column.identifier().ToString())
			m_vars[row].Value = v.ToString();
			
		else
			Contract.Assert(false, "how did we get identifier: " + column.identifier());
	}
	#endregion
	
	#region Fields
	private NSWindow m_sheet;
	private NSTableView m_table;
	private Document m_doc;
	private List<EnvVar> m_vars = new List<EnvVar>();
	#endregion
}
