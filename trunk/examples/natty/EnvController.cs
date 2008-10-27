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
using System;
using System.Collections.Generic;

[ExportClass("EnvController", "NSObject", IVars = "sheet table")]
internal sealed class EnvController : NSObject
{
	private EnvController(IntPtr instance) : base(instance) 
	{
		m_sheet = new IVar<NSWindow>(this, "sheet");
		m_table = new IVar<NSTableView>(this, "table");
	}
						
	public void Open(Document doc, NSWindow window)
	{
		m_doc = doc;
		m_vars = new List<KeyValuePair<string, string>>(m_doc.Variables);

		m_sheet.Value.setDelegate(this);
		m_table.Value.setDataSource(this);
		NSApplication.sharedApplication().beginSheetModalForWindowModalDelegateDidEndSelectorContextInfo(
			m_sheet.Value, window, this, null, IntPtr.Zero);
    }

	#region Action Handlers
	public void envOK(NSObject sender)
	{
		NSApplication.sharedApplication().endSheet(m_sheet.Value);
		m_sheet.Value.orderOut(this);
		
		m_doc.Variables.Clear();
		m_doc.Variables.AddRange(m_vars);
    }

	public void envCancel(NSObject sender)
	{
		NSApplication.sharedApplication().endSheet(m_sheet.Value);
		m_sheet.Value.orderOut(this);
   	}
	#endregion
	
	#region Data Source
	public int numberOfRowsInTableView(NSTableView table)
	{
		return m_vars.Count;
	}

	[Register("tableView:objectValueForTableColumn:row:")]		
	public NSObject GetCell(NSTableView table, NSTableColumn column, int row)
	{
		KeyValuePair<string, string> entry = m_vars[row];
	
		if (column.identifier().ToString() == "1")
			return NSString.Create(entry.Key);
		else 
			return NSString.Create(entry.Value);
	}

	[Register("tableView:setObjectValue:forTableColumn:row:")]		
	public void SetCell(NSTableView table, NSObject v, NSTableColumn column, int row)
	{
		DBC.Pre(column.identifier().ToString() == "2", "id is {0}", column.identifier());
		
		string key = m_vars[row].Key;
		string value = new NSString(v).ToString();
		
		m_vars[row] = new KeyValuePair<string, string>(key, value);
	}
	#endregion
	
	private IVar<NSWindow> m_sheet;
	private IVar<NSTableView> m_table;
	private Document m_doc;
	private List<KeyValuePair<string, string>> m_vars = new List<KeyValuePair<string, string>>();
}