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
using System.Diagnostics;
using System.IO;
using System.Linq;

[ExportClass("DocWindowController", "NSWindowController", Outlets = "flagsSheet prefSheet envSheet targets build cancel status output outputWindow errorWindow errors")]
internal sealed class DocWindowController : NSWindowController
{
	public DocWindowController(IntPtr instance) : base(instance)
	{
	}

	public static DocWindowController Create(string nibName)
	{
		DocWindowController result = new Class("DocWindowController").Call("alloc").Call("initWithWindowNibName:", NSString.Create(nibName)).To<DocWindowController>();
		
		result.m_env = new IBOutlet<EnvController>(result, "envSheet");
		result.m_flags = new IBOutlet<FlagsController>(result, "flagsSheet");
		result.m_prefs = new IBOutlet<NSWindow>(result, "prefSheet");

		result.m_errorWindow = new IBOutlet<NSWindow>(result, "errorWindow");
		result.m_transcriptWindow = new IBOutlet<NSWindow>(result, "outputWindow");	
		result.m_errorTable = new IBOutlet<ErrorTable>(result, "errors");
		result.m_statusLabel = new IBOutlet<NSTextField>(result, "status");
		result.m_buildBtn = new IBOutlet<NSButton>(result, "build");
		result.m_cancelBtn = new IBOutlet<NSButton>(result, "cancel");
		result.m_outputView = new IBOutlet<NSTextView>(result, "output");
	
		result.autorelease();
		
		return result;
	}
						
	#region Action Handlers ---------------------------------------------------
	public void targetChanged(NSPopUpButton sender)
	{
		m_doc.Target = sender.titleOfSelectedItem().ToString();
	}

	public void build(NSObject sender)
	{
		m_doc.Build();
	}

	public void cancel(NSObject sender)
	{
		m_doc.Cancel();
	}

	public void clearTranscript(NSObject sender)
	{
		m_outputView.Value.textStorage().mutableString().setString(NSString.Empty);
	}

	public void environment(NSObject sender)
	{
		if (NSObject.IsNullOrNil(m_env.Value))
			NSBundle.loadNibNamed_owner(NSString.Create("Environment"), this);
		Trace.Assert(!NSObject.IsNullOrNil(m_env.Value), "nib didn't set envSheet");
	
		m_env.Value.Open(m_doc, window());
    }

	public void flags(NSObject sender)
	{
		if (NSObject.IsNullOrNil(m_flags.Value))
			NSBundle.loadNibNamed_owner(NSString.Create("Flags"), this);
		Trace.Assert(!NSObject.IsNullOrNil(m_flags.Value), "nib didn't set flagsSheet");
	
		m_flags.Value.Open(m_doc, window());
    }

	public void showPrefs(NSObject sender)
	{		
		NSBundle.loadNibNamed_owner(NSString.Create("Preferences"), this);
		Trace.Assert(!NSObject.IsNullOrNil(m_prefs.Value), "nib didn't set prefSheet");

		m_prefs.Value.makeKeyAndOrderFront(this);
	}
		
	public void rebuildTargets()
	{
		string[] ignored = m_doc.IgnoredTargets().Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
		var targets = m_doc.Targets.Except(ignored);
		
		NSPopUpButton popup = new NSPopUpButton(this["targets"]);
		popup.removeAllItems();	
		popup.addItemsWithTitles(NSArray.Create(targets.ToArray()));
						
		m_buildBtn.Value.setEnabled(targets.Any());

		if (m_doc.Target != null && targets.Contains(m_doc.Target))
			popup.selectItemWithTitle(NSString.Create(m_doc.Target));			
	}	
	#endregion
	
	#region Errors Data Source ------------------------------------------------
	public int numberOfRowsInTableView(NSTableView table)
	{
		return m_errors.Count;
	}

	[Register("tableView:objectValueForTableColumn:row:")]		
	public NSObject TableObjectForCell(NSTableView table, NSTableColumn column, int row)
	{
		Error error = m_errors[row];
		if (column.identifier().ToString() == "1")
			return NSString.Create(error.Message);
		else if (column.identifier().ToString() == "2")
			return NSString.Create(error.File);
		else
			return NSString.Create(error.Line);
	}

	public void openSelection(NSObject sender)
	{
		bool opened = false;
		
		NSRange range = m_outputView.Value.selectedRange();
		if (range.location != Enums.NSNotFound)
		{
			string text = m_outputView.Value.textStorage().ToString();
			range = DoExtendSelection(text, range);
			string file = text.Substring(range.location, range.length);
			
			string line = "0";
			if (range.location + range.length < text.Length && (text[range.location + range.length] == '(' || text[range.location + range.length] == ':'))
			{
				int i = range.location + range.length + 1;
				if (i < text.Length && char.IsDigit(text[i]))
				{
					int j = i;
					while (j < text.Length && char.IsDigit(text[j]))
						++j;
					line = text.Substring(i, j - i);
				}
			}
			
			opened = DoTryOpenFile(file, line);
		}
		
		if (!opened)
			Functions.NSBeep();
	}
	
	private NSRange DoExtendSelection(string text, NSRange range)
	{
		while (DoIsFileChar(text, range.location - 1))
		{
			--range.location;
			++range.length;
		}

		while (DoIsFileChar(text, range.location + range.length))
			++range.length;
			
		return range;
	}
	
	private bool DoIsFileChar(string text, int index)
	{
		if (index >= 0 && index < text.Length)
		{
			if (text[index] == '\n' ||
				text[index] == '\r' ||
				text[index] == ' ' ||
				text[index] == ':' ||
				text[index] == '\'' ||
				text[index] == '"' ||
				text[index] == '(')
				return false;
			
			return true;
		}
		
		return false;
	}

	public void openFile(int row)
	{
		DoTryOpenFile(m_errors[row].File, m_errors[row].Line);
	}

	public bool DoTryOpenFile(string file, string line)
	{
		string path = Path.GetDirectoryName(m_doc.Path);
		if (DoOpenFile(path, file, line))
			return true;
			
		foreach (string root in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
		{
			if (!root.Contains("/.") && !root.Contains(".nib") && !root.Contains("/bin/") && !root.EndsWith("/bin"))
			{
				if (DoOpenFile(root, file, line))
					return true;
			}
		}

		return false;
	}

	public bool DoOpenFile(string root, string file, string line)
	{
		string path = Path.Combine(root, file);
		
		if (File.Exists(path))
		{
			string command = m_doc.Editor();
			string args = null;
			
			int i = command.IndexOf(' ');
			if (i >= 0)
			{
				args = command.Substring(i + 1);
				command = command.Substring(0, i);
			}
			
			try
			{
				ProcessStartInfo info = new ProcessStartInfo();
				info.FileName = command;
				info.UseShellExecute = false;
		
				if (args != null)
					info.Arguments = string.Format(args, path, line);
	
				Process.Start(info);
			}
			catch (Exception e)
			{
				NSAlert alert = NSAlert.Create();
				alert.setMessageText(NSString.Create("Couldn't open the file."));
				alert.setInformativeText(NSString.Create(e.Message));
				alert.runModal();
			}
		}
		
		return File.Exists(path);
	}
	#endregion
	
	public bool validateMenuItem(NSMenuItem item)
	{		
		if (item.action().Name == "build:")
			return m_doc.State != State.Building;
		
		return true;
	}
	
	#region Overrides ---------------------------------------------------------
	public new NSString windowTitleForDocumentDisplayName(NSString displayName)
	{		
		return NSString.Create(DoGetTitle());
	}
	
	public new void windowDidLoad()
	{		
		try
		{
			m_doc = Call("document").To<Document>();	
				
			m_outputView.Value.textStorage().mutableString().setString(NSString.Empty);
			m_errorTable.Value.setDataSource(this);
			
			setShouldCascadeWindows(false);
			m_transcriptWindow.Value.windowController().setShouldCascadeWindows(false);
			m_errorWindow.Value.windowController().setShouldCascadeWindows(false);
			
			window().setFrameAutosaveName(NSString.Create(m_doc.Path + "-build window"));
			m_transcriptWindow.Value.setFrameAutosaveName(NSString.Create(m_doc.Path + "-transcript window"));
			m_errorWindow.Value.setFrameAutosaveName(NSString.Create(m_doc.Path + "-error window"));
			
			m_transcriptWindow.Value.windowController().setNextResponder(this);
			m_errorWindow.Value.windowController().setNextResponder(this);
							
			m_doc.StateChanged += this.DoDocChanged;
			m_doc.CommandData += this.DoGotCommand;
			m_doc.StdoutData += this.DoGotStdout;
			m_doc.StderrData += this.DoGotStderr;
			
			rebuildTargets();
		}
		catch (Exception e)
		{	
			// By default cocoa just logs a lame message to the console...
			NSAlert alert = NSAlert.Create();
			alert.setMessageText(NSString.Create("Couldn't open the document."));
			alert.setInformativeText(NSString.Create(e.Message));
			alert.runModal();
			throw;
		}
	}
	#endregion	
	
	private void DoDocChanged(object sender, EventArgs e)
	{
		switch (m_doc.State)
		{
			case State.Building:
				m_startTime = DateTime.Now;
				m_statusLabel.Value.setStringValue(NSString.Create("building..."));
				m_statusLabel.Value.setTextColor(NSColor.controlTextColor());
				m_buildBtn.Value.setEnabled(false);
				m_cancelBtn.Value.setEnabled(true);
				DoShowOutput();
				m_errors.Clear();
				m_errorTable.Value.noteNumberOfRowsChanged();
				m_errorWindow.Value.orderOut(this);
				break;

			case State.Built:
				DoSetBuiltStatus();
				m_buildBtn.Value.setEnabled(true);
				m_cancelBtn.Value.setEnabled(false);
				DoShowOutput();
				m_outputView.Value.textStorage().appendAttributedString(NSAttributedString.Create(Environment.NewLine, ms_stdoutAttrs));
				if (m_doc.ExitCode != 0 && m_errors.Count > 0)
					m_errorWindow.Value.makeKeyAndOrderFront(this);
				break;

			case State.Canceled:
				m_statusLabel.Value.setStringValue(NSString.Create("canceled"));
				m_statusLabel.Value.setTextColor(NSColor.orangeColor());
				m_buildBtn.Value.setEnabled(true);
				m_cancelBtn.Value.setEnabled(false);
				m_outputView.Value.textStorage().appendAttributedString(NSAttributedString.Create(Environment.NewLine, ms_stdoutAttrs));
				m_errorWindow.Value.orderOut(this);
				m_errors.Clear();
				m_errorTable.Value.noteNumberOfRowsChanged();
				break;
				
			default:
				Trace.Fail(string.Format("unknown state: {0}", m_doc.State));
				break;
		}
	}
	
	private void DoSetBuiltStatus()
	{
		if (m_errors.Count > 0)
		{
			int numErrors = 0, numWarnings = 0;
			foreach (Error err in m_errors)
				if (err.IsError)
					++numErrors;
				else
					++numWarnings;
			if (numErrors == 1 && numWarnings == 0)
				m_statusLabel.Value.setStringValue(NSString.Create("one error"));
			else if (numErrors == 0 && numWarnings == 1)
				m_statusLabel.Value.setStringValue(NSString.Create("one warning"));
			else if (numErrors > 0 && numWarnings == 0)
				m_statusLabel.Value.setStringValue(NSString.Create(string.Format("{0} errors", numErrors)));
			else if (numErrors == 0 && numWarnings > 0)
				m_statusLabel.Value.setStringValue(NSString.Create(string.Format("{0} warnings", numWarnings)));
			else
				m_statusLabel.Value.setStringValue(NSString.Create(string.Format("{0} errors, {1} warnings", numErrors, numWarnings)));

			if (numErrors > 0)
				m_statusLabel.Value.setTextColor(NSColor.redColor());
			else
				m_statusLabel.Value.setTextColor(NSColor.orangeColor());
		}
		else if (m_doc.ExitCode == 0)
		{
			TimeSpan elapsed = DateTime.Now - m_startTime;
			m_statusLabel.Value.setStringValue(NSString.Create(string.Format("built in {0:0.0} secs", elapsed.TotalSeconds)));
			m_statusLabel.Value.setTextColor(NSColor.controlTextColor());
		}
		else
		{
			m_statusLabel.Value.setStringValue(NSString.Create("failed with with exit code " + m_doc.ExitCode));
			m_statusLabel.Value.setTextColor(NSColor.redColor());
		}
	}
	
	private void DoGotCommand(object sender, string data)
	{
		m_outputView.Value.textStorage().appendAttributedString(NSAttributedString.Create(data, ms_commandAttrs));
		DoScrollOutputToEnd();
	}
	
	private void DoGotStdout(object sender, string data)
	{
		m_outputView.Value.textStorage().appendAttributedString(NSAttributedString.Create(data, ms_stdoutAttrs));
		DoScrollOutputToEnd();
	}
	
	private void DoGotStderr(object sender, string data)
	{
		Error error = ErrorParser.Parse(data);
		if (error != null)
		{
			m_errors.Add(error);
			m_errorTable.Value.noteNumberOfRowsChanged();
			
			if (!m_errorWindow.Value.isVisible())
			{
				m_errorWindow.Value.setTitle(NSString.Create(string.Format("{0} {1} Errors", DoGetTitle(), m_doc.Target)));
				m_errorWindow.Value.makeKeyAndOrderFront(this);
				m_errorTable.Value.selectRowIndexes_byExtendingSelection(NSIndexSet.indexSetWithIndex(0), false);
			}
		}
		
		m_outputView.Value.textStorage().appendAttributedString(NSAttributedString.Create(data, ms_stderrAttrs));
		DoScrollOutputToEnd();
	}
	
	private void DoScrollOutputToEnd()
	{
		int len = (int) m_outputView.Value.string_().length();
		NSRange range = new NSRange(len, 0);
		m_outputView.Value.scrollRangeToVisible(range);
	}
	
	private void DoShowOutput()
	{
		m_transcriptWindow.Value.makeKeyAndOrderFront(this);
		m_transcriptWindow.Value.setTitle(NSString.Create(DoGetTitle() + " Transcript"));
	}

	private string DoGetTitle()
	{
		NSURL url = m_doc.fileURL();
		string path = url.path().ToString();
		
		string s = Path.GetDirectoryName(path);
		s = Path.GetFileName(s);
		
		return s;
	}
		
	private static NSDictionary DoCreateCommandAttrs()
	{
		NSMutableDictionary dict = NSMutableDictionary.Create();
		dict.setObject_forKey(NSFont.fontWithName_size(NSString.Create(ms_fontName + "-Bold"), 12.0f), Externs.NSFontAttributeName);
		dict.retain();
		return dict;
	}

	private static NSDictionary DoCreateStdoutAttrs()
	{
		NSMutableDictionary dict = NSMutableDictionary.Create();
		dict.setObject_forKey(NSFont.fontWithName_size(NSString.Create(ms_fontName), 12.0f), Externs.NSFontAttributeName);
		dict.retain();
		return dict;
	}

	private static NSDictionary DoCreateStderrAttrs()
	{
		NSMutableDictionary dict = NSMutableDictionary.Create();
		dict.setObject_forKey(NSFont.fontWithName_size(NSString.Create(ms_fontName), 12.0f), Externs.NSFontAttributeName);
		dict.setObject_forKey(NSColor.redColor(), Externs.NSForegroundColorAttributeName);
		dict.retain();
		return dict;
	}

	#region Fields ------------------------------------------------------------
	private Document m_doc;
	private IBOutlet<EnvController> m_env;
	private IBOutlet<FlagsController> m_flags;
	private IBOutlet<NSWindow> m_prefs;
	private IBOutlet<NSTextField> m_statusLabel;
	private IBOutlet<NSButton> m_buildBtn;
	private IBOutlet<NSButton> m_cancelBtn;
	private IBOutlet<NSTextView> m_outputView;
	private IBOutlet<ErrorTable> m_errorTable;
	private IBOutlet<NSWindow> m_transcriptWindow;
	private IBOutlet<NSWindow> m_errorWindow;
	private List<Error> m_errors = new List<Error>();
	private DateTime m_startTime;
	
	private static string ms_fontName = "Verdana";
	private static readonly NSDictionary ms_commandAttrs = DoCreateCommandAttrs();
	private static readonly NSDictionary ms_stdoutAttrs = DoCreateStdoutAttrs();
	private static readonly NSDictionary ms_stderrAttrs = DoCreateStderrAttrs();
	#endregion
}