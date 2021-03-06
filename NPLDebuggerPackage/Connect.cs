using System;
using EnvDTE;
using EnvDTE80;
using System.Resources;
using System.Reflection;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ParaEngine.NPLDebuggerPackage
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class NPLDebuggerConnect
	{
        public bool IsConnected = false;
        private NPLDebuggerPackage package;

        public NPLDebuggerConnect(object application, NPLDebuggerPackage package_)
		{
            _applicationObject = (DTE2)application;
            package = package_;

            CheckRegisterDebugEngine();
		}

        /// <summary>
        /// register the NPL debug engine if not. 
        /// </summary>
        public void CheckRegisterDebugEngine()
        {
            bool bIsNPLEngineRegistered = false;
            String sNPLDebuggerKey = "Software\\Microsoft\\VisualStudio\\10.0\\AD7Metrics\\Engine\\{D951924A-4999-42a0-9217-1EB5233D1D5A}";
            using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(sNPLDebuggerKey))
            {
                if (setupKey != null)
                {
                    bIsNPLEngineRegistered = true;
                }
            }
            if(!bIsNPLEngineRegistered)
            {
                System.Diagnostics.Process.Start("regsvr32", "\"" + NPLDebuggerPackage.PackageRootPath + "Microsoft.VisualStudio.Debugger.SampleEngineWorker.dll\" /s");
                MessageBox.Show("Successfully registered NPL debugger. Please restart Visual Studio");
            }
        }

        /// <summary>
        /// Launch the project selection form for the addin. Called from the Exec method above.
        /// </summary>
        public void DisplayLaunchForm()
        {
            // Show the form.
            LaunchForm lf = new LaunchForm(_applicationObject);
            System.Windows.Forms.DialogResult result = lf.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // The user clicked on Ok in the form, so launch the file using the sample debug engine.
                LaunchDebugTarget(lf.Command, lf.CommandArguments, lf.WorkingDir);
            }
            else if (result == System.Windows.Forms.DialogResult.Yes)
            {
                AttachDebugTarget(lf.SelectedProcess);
            }
        }

        /// <summary>
        /// Attach to a process. 
        /// </summary>
        /// <param name="sProcessName"></param>
        public void AttachDebugTarget(string sProcessName)
        {
            Microsoft.VisualStudio.Shell.ServiceProvider sp =
                new Microsoft.VisualStudio.Shell.ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_applicationObject);
            IVsDebugger dbg = (IVsDebugger)sp.GetService(typeof(SVsShellDebugger));
            
            foreach (Process lLocalProcess in _applicationObject.Debugger.LocalProcesses)
            {
                if (lLocalProcess.Name.IndexOf(sProcessName) >= 0)
                {
                    (lLocalProcess as Process2).Attach2("NPL Debug Engine");
                    break;
                }
            }
        }

        /// <summary>
        /// Launch an executable using the sample debug engine.
        /// </summary>
        public void LaunchDebugTarget(string command, string arguments, string workingDir)
        {
           Microsoft.VisualStudio.Shell.ServiceProvider sp =
                new Microsoft.VisualStudio.Shell.ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_applicationObject);

            IVsDebugger dbg = (IVsDebugger)sp.GetService(typeof(SVsShellDebugger));

            VsDebugTargetInfo info = new VsDebugTargetInfo();
            info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
            info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

            info.bstrExe = command;
            info.bstrCurDir = String.IsNullOrEmpty(workingDir) ? System.IO.Path.GetDirectoryName(info.bstrExe) : workingDir;
            info.bstrArg = arguments; // command line parameters
            info.bstrRemoteMachine = null; // debug locally
            info.fSendStdoutToOutputWindow = 0; // Let stdout stay with the application.
            info.clsidCustom = new Guid("{D951924A-4999-42a0-9217-1EB5233D1D5A}"); // Set the launching engine the sample engine guid
            info.grfLaunch = 0;

            IntPtr pInfo = System.Runtime.InteropServices.Marshal.AllocCoTaskMem((int)info.cbSize);
            System.Runtime.InteropServices.Marshal.StructureToPtr(info, pInfo, false);

            try
            {
                dbg.LaunchDebugTargets(1, pInfo);
            }
            finally
            {
                if (pInfo != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pInfo);
                }
            }

        }
		private DTE2 _applicationObject;
	}
}