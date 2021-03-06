using System;
using System.Diagnostics;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CommandBars;
using Microsoft.VisualStudio.OLE.Interop;
using ParaEngine.Tools.Lua.CodeDom;
using ParaEngine.Tools.Lua.Refactoring.UndoManager;
using ParaEngine.NPLLanguageService;

namespace ParaEngine.Tools.Lua.VsEditor
{
	/// <summary>
	/// The IOleCommandTarget interface enables objects and their containers 
	/// to dispatch commands to each other.
	/// </summary>
	public class LuaCommandFilter : IOleCommandTarget
	{
		private readonly LanguageService languageService;
		private static readonly object syncLock = new object();
		private static LuaCommandFilter commandFilter;

		/// <summary>
		/// Initializes a new instance of the <see cref="LuaCommandFilter"/> class.
		/// </summary>
		/// <param name="languageService">The Lua LanguageService.</param>
		public LuaCommandFilter(LanguageService languageService)
		{
			this.languageService = languageService;
		}

		/// <summary>
		/// Creates LuaCommandFilter singleton instance.
		/// </summary>
		/// <param name="languageService">The language service.</param>
		/// <returns></returns>
		public static LuaCommandFilter GetCommandFilter(LanguageService languageService)
		{
			lock (syncLock)
			{
				if (commandFilter == null)
					commandFilter = new LuaCommandFilter(languageService);
				return commandFilter;
			}
		}

		/// <summary>
		/// Gets or sets CommandFilter from editor.
		/// </summary>
		public IOleCommandTarget VsCommandFilter { get; set; }

		#region IOleCommandTarget Members

		/// <summary>
		/// Queries the object for the status of one or more commands generated by user interface events.
		/// </summary>
		/// <param name="pguidCmdGroup">Unique identifier of the command group; can be NULL to specify the standard group. All the commands that are passed in the prgCmds array must belong to the group specified by pguidCmdGroup.</param>
		/// <param name="cCmds">The number of commands in the prgCmds array.</param>
		/// <param name="prgCmds">A caller-allocated array of OLECMD structures that indicate the commands for which the caller needs status information. This method fills the cmdf member of each structure with values taken from the OLECMDF enumeration.</param>
		/// <param name="pCmdText">Pointer to an OLECMDTEXT structure in which to return name and/or status information of a single command. Can be NULL to indicate that the caller does not need this information.</param>
		/// <returns>This method supports the standard return values E_FAIL and E_UNEXPECTED, as well as the following: S_OK, E_POINTER, OLECMDERR_E_UNKNOWNGROUP</returns>
		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			return VsCommandFilter.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		/// <summary>
		/// Executes a specified command or displays help for a command.
		/// </summary>
		/// <param name="pguidCmdGroupRef">Pointer to unique identifier of the command group; can be NULL to specify the standard group.</param>
		/// <param name="nCmdID">The command to be executed. This command must belong to the group specified with pguidCmdGroup.</param>
		/// <param name="nCmdexecopt">Values taken from the OLECMDEXECOPT enumeration, which describe how the object should execute the command.</param>
		/// <param name="pvaIn">Pointer to a VARIANTARG structure containing input arguments. Can be NULL.</param>
		/// <param name="pvaOut">Pointer to a VARIANTARG structure to receive command output. Can be NULL.</param>
		/// <returns>This method supports the standard return values E_FAIL and E_UNEXPECTED, as well as the following:
		///            S_OK
		///                The command was executed successfully.
		///            OLECMDERR_E_UNKNOWNGROUP
		///                The pguidCmdGroup parameter is not NULL but does not specify a recognized command group.
		///            OLECMDERR_E_NOTSUPPORTED
		///                The nCmdID parameter is not a valid command in the group identified by pguidCmdGroup.
		///            OLECMDERR_E_DISABLED
		///                The command identified by nCmdID is currently disabled and cannot be executed.
		///            OLECMDERR_E_NOHELP
		///                The caller has asked for help on the command identified by nCmdID, but no help is available.
		///            OLECMDERR_E_CANCELED
		///                The user canceled the execution of the command.</returns>
		public int Exec(ref Guid pguidCmdGroupRef, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			const int retval = VSConstants.S_OK;
			string commandId = VSIDECommands.GetCommandId(pguidCmdGroupRef, nCmdID);

			if (!string.IsNullOrEmpty(commandId))
			{
				//Refactor command
				if (VSIDECommands.IsRightClick(pguidCmdGroupRef, nCmdID))
				{
					//SetRefactorMenuBars();
					return ExecVsHandler(ref pguidCmdGroupRef, nCmdID, nCmdexecopt, pvaIn, pvaOut);
				}

				//Undo command
				if (commandId == "cmdidUndo")
				{
					var luaUndoService = languageService.GetService(typeof (ILuaUndoService)) as ILuaUndoService;
					if (luaUndoService != null)
						luaUndoService.Undo();

					return ExecVsHandler(ref pguidCmdGroupRef, nCmdID, nCmdexecopt, pvaIn, pvaOut);
				}

				return retval;
			}

			return ExecVsHandler(ref pguidCmdGroupRef, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		#endregion

		/// <summary>
		/// Sets the refactor menu bars.
		/// </summary>
		private void SetRefactorMenuBars()
		{
			try
			{
				CommandBarControl renameCommand = null;
				var commandBarControl = GetCommandBarControl(Resources.RefactoringContextMenuName) as CommandBarPopup;

				if (commandBarControl != null)
					foreach (CommandBarControl subMenuItem in commandBarControl.Controls)
					{
						if (subMenuItem.Caption == Resources.RenameCommandName)
						{
							renameCommand = subMenuItem;
							break;
						}
					}
				if (renameCommand != null)
					renameCommand.Enabled = IsRefactorableItemSelected();
			}
			catch (Exception e)
			{
				Trace.WriteLine(e);
			}
		}

		/// <summary>
		/// Gets the command bar control.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		private CommandBarControl GetCommandBarControl(string name)
		{
			var dte = languageService.GetService(typeof (DTE)) as DTE2;
			var commandBars = (_CommandBars) dte.CommandBars;
			var commandBar = commandBars[Resources.CodeWindowCommandBarName];
			foreach (CommandBarControl ctrl in commandBar.Controls)
			{
				if (ctrl.Caption == name)
					return ctrl;
			}
			return null;
		}


		/// <summary>
		/// Determines whether [is refactorable item selected].
		/// </summary>
		/// <returns>
		/// 	<c>true</c> if [is refactorable item selected]; otherwise, <c>false</c>.
		/// </returns>
		private bool IsRefactorableItemSelected()
		{
			LuaFileCodeModel codeModel = languageService.GetFileCodeModel();
			CodeElement element = codeModel.GetElementByEditPoint();
			return element != null;
		}

		/// <summary>
		/// Execs the vs handler.
		/// </summary>
		/// <param name="pguidCmdGroupRef">The pguid CMD group ref.</param>
		/// <param name="nCmdID">The n CMD ID.</param>
		/// <param name="nCmdexecopt">The n cmdexecopt.</param>
		/// <param name="pvaIn">The pva in.</param>
		/// <param name="pvaOut">The pva out.</param>
		/// <returns></returns>
		private int ExecVsHandler(ref Guid pguidCmdGroupRef, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			return VsCommandFilter.Exec(ref pguidCmdGroupRef, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}
	}
}