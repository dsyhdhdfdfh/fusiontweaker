﻿using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO;

namespace FusionTweaker
{
	public partial class Form1 : Form
	{
		private class PowerScheme
		{
			public Guid Guid;
			public string Name;

			public override string ToString()
			{
				return Name;
			}
		}


		private static readonly bool _useWindowsPowerSchemes = (Environment.OSVersion.Version.Major >= 6);

        private static readonly int numCores = System.Environment.ProcessorCount;
        
        private readonly int numPstates = -1;
		private readonly int numBoostedPstates = 0;
        //public readonly int family = -1;
        
        private static int[] currentPStateCore = new int[numCores];
        //Brazos merge 
        //private static readonly int processBarSteps = numPstates + 1;
        private readonly int processBarSteps = 0;
        private readonly int processBarPerc = 0;

        private static bool monitorPstates = true;
        
        //public static readonly int family = K10Manager.GetFamily();
        public static int family = new int();
        public static int clock = new int();
        public static int[] freq = new int[10];

        public Form1()
		{
            
            InitializeComponent();

            family = K10Manager.GetFamily();
            numPstates = K10Manager.GetHighestPState();
            clock = K10Manager.GetBIOSBusSpeed();
            //Brazos merge next line removed in BT
            //numBoostedPstates = K10Manager.GetNumBoostedStates();
            numBoostedPstates = 0;
            processBarSteps = numPstates + numBoostedPstates + 1;
            processBarPerc = 100 / processBarSteps;

			if ((family != 12) && (family != 14) && (family != 16))
            {
                MessageBox.Show("Your CPU/APU from AMD family: " + family + "h is not supported!");
            }

            //needed to reduces flickering
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            
            if (numCores == 3)
            {
                this.Controls.Remove(this.pstateLabel4);
                this.Controls.Remove(this.core4label);
                this.Controls.Remove(this.cpu4Bar);
                ShiftTable(-15);
            }
            else if (numCores == 2)
            {
                this.Controls.Remove(this.pstateLabel4);
                this.Controls.Remove(this.core4label);
                this.Controls.Remove(this.cpu4Bar);
                this.Controls.Remove(this.pstateLabel3);
                this.Controls.Remove(this.core3label);
                this.Controls.Remove(this.cpu3Bar);
                ShiftTable(-30);
            }
            else if (numCores == 1)
            {
                this.Controls.Remove(this.pstateLabel4);
                this.Controls.Remove(this.core4label);
                this.Controls.Remove(this.cpu4Bar);
                this.Controls.Remove(this.pstateLabel3);
                this.Controls.Remove(this.core3label);
                this.Controls.Remove(this.cpu3Bar); 
                this.Controls.Remove(this.pstateLabel2);
                this.Controls.Remove(this.core2label);
                this.Controls.Remove(this.cpu2Bar);
                ShiftTable(-50);
            }

			notifyIcon.Icon = this.Icon;
			notifyIcon.ContextMenuStrip = new ContextMenuStrip();
			notifyIcon.Visible = true;

            if (family == 16)
            {
                //MessageBox.Show("Jetzt wird ein Log für den Editor erstellt!"); 
                //log_now();
            }
			//Brazos merge next line was active in BT
			//this.Width += p0StateControl.GetDeltaOptimalWidth();
            
			//Brazos merge p3 trough p7 inactive in BT
			//BT also provides integer value to Load for PState, which shouldn't be needed

            //MessageBox.Show("Jetzt werden die Register der GPU gelesen!");
            nbp0StateControl.LoadFromHardware();
            nbp1StateControl.LoadFromHardware();
            //MessageBox.Show("Jetzt werden zusätzliche Register gelesen!");
            statusinfo.LoadFromHardware();
            
            //MessageBox.Show("Jetzt werden die Register der CPU gelesen!");
            p0StateControl.LoadFromHardware();
            p1StateControl.LoadFromHardware();
            p2StateControl.LoadFromHardware();
            p3StateControl.LoadFromHardware();
            p4StateControl.LoadFromHardware();
            p5StateControl.LoadFromHardware();
            p6StateControl.LoadFromHardware();
            p7StateControl.LoadFromHardware();

            //MessageBox.Show("Alle Register gelesen!");
            
            if (!_useWindowsPowerSchemes)
			{
				// use FusionTweaker's power schemes (via the registry)
				powerSchemesComboBox.Items.Add(new PowerScheme() { Name = "Balanced" });
				powerSchemesComboBox.Items.Add(new PowerScheme() { Name = "High performance" });
				powerSchemesComboBox.Items.Add(new PowerScheme() { Name = "Power saver" });

				var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\FusionTweaker");

				if (key == null)
					powerSchemesComboBox.SelectedIndex = 0;
				else
				{
					powerSchemesComboBox.SelectedIndex = (int)key.GetValue("PowerScheme", 0);
					key.Close();
				}

				InitializeNotifyIconContextMenu();

				powerSchemesComboBox.SelectedIndexChanged += (s, e) =>
				{
					var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"Software\FusionTweaker");
					k.SetValue("PowerScheme", powerSchemesComboBox.SelectedIndex);
					k.Close();
					SynchronizeNotifyIconContextMenu();
				};

				return;
			}

			int guidSize = 16;
			IntPtr guid = Marshal.AllocHGlobal(guidSize);

			// get the GUID of the current power scheme
			IntPtr activeGuidPointer;
			if (PowerGetActiveScheme(IntPtr.Zero, out activeGuidPointer) != 0)
				throw new Exception("PowerGetActiveScheme()");
			Guid activeGuid = (Guid)Marshal.PtrToStructure(activeGuidPointer, typeof(Guid));
			LocalFree(activeGuidPointer);

			// iterate over all power schemes
			for (int i = 0; true; i++)
			{
				if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0x10, i, guid, ref guidSize) != 0)
					break;

				// get the required buffer size
				int size = 0;
                if (PowerReadFriendlyName(IntPtr.Zero, guid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size) != 0)
                    break;//throw new Exception("PowerReadFriendlyName()");

				IntPtr stringBuffer = Marshal.AllocHGlobal(size);

				// get the scheme name
				if (PowerReadFriendlyName(IntPtr.Zero, guid, IntPtr.Zero, IntPtr.Zero, stringBuffer, ref size) != 0)
					throw new Exception("PowerReadFriendlyName()");

				var item = new PowerScheme()
				{
					Guid = (Guid)Marshal.PtrToStructure(guid, typeof(Guid)),
					Name = Marshal.PtrToStringUni(stringBuffer)
				};

				Marshal.FreeHGlobal(stringBuffer);

				powerSchemesComboBox.Items.Add(item);

				if (item.Guid == activeGuid)
					powerSchemesComboBox.SelectedIndex = i;
			}

			Marshal.FreeHGlobal(guid);

			InitializeNotifyIconContextMenu();

			powerSchemesComboBox.SelectedIndexChanged += (s, e) =>
			{
				var item = (PowerScheme)powerSchemesComboBox.SelectedItem;
				if (PowerSetActiveScheme(IntPtr.Zero, ref item.Guid) != 0)
					throw new Exception("PowerSetActiveScheme()");
				SynchronizeNotifyIconContextMenu();
			};
		}


		#region Power schemes API (Vista+).

		[DllImport("PowrProf.dll")]
		private static extern uint PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid,
			IntPtr SubGroupOfPowerSettingsGuid, int AccessFlags, int Index,
			IntPtr Buffer, ref int BufferSize);

		[DllImport("PowrProf.dll")]
		private static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, IntPtr SchemeGuid,
			IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid,
			IntPtr Buffer, ref int BufferSize);

		[DllImport("PowrProf.dll")]
		private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr SchemeGuid);

		[DllImport("PowrProf.dll")]
		private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

		[DllImport("Kernel32.dll")]
		private static extern IntPtr LocalFree(IntPtr hMem);

		#endregion


		/// <summary>
		/// Copies the power schemes combox box items to the tray icon's
		/// context menu.
		/// </summary>
		private void InitializeNotifyIconContextMenu()
		{
			int selectedIndex = powerSchemesComboBox.SelectedIndex;

			for (int i = 0; i < powerSchemesComboBox.Items.Count; i++)
			{
				var item = (PowerScheme)powerSchemesComboBox.Items[i];

				var menuItem = new ToolStripMenuItem(item.Name);
				menuItem.Checked = (i == selectedIndex);
				// a click causes the selection of another scheme in the combo box,
				// thereby switching to the clicked scheme
				int copy = i; // we _must_ not use i in the lambda expression, because it is incremented up to powerSchemesComboBox.Items.Count later on
				              // therefore use a private copy
				menuItem.Click += (s, e) => powerSchemesComboBox.SelectedIndex = copy;

				notifyIcon.ContextMenuStrip.Items.Add(menuItem);
			}

			// separator
			notifyIcon.ContextMenuStrip.Items.Add("-");

			var exitItem = new ToolStripMenuItem("Exit");
			exitItem.Click += (s, e) => this.Close();
			notifyIcon.ContextMenuStrip.Items.Add(exitItem);
		}

		/// <summary>
		/// Marks the tray icon's context menu item which represents the
		/// currently selected item in the power schemes combo box.
		/// </summary>
		private void SynchronizeNotifyIconContextMenu()
		{
			int selectedIndex = powerSchemesComboBox.SelectedIndex;

			for (int i = 0; i < powerSchemesComboBox.Items.Count; i++)
			{
				var item = (ToolStripMenuItem)notifyIcon.ContextMenuStrip.Items[i];
				item.Checked = (i == selectedIndex);
			}
		}


		protected override void WndProc(ref Message m)
		{
			const int WM_SIZE = 5;
			const int SIZE_MINIMIZED = 1;

			// hide the form when it is minimized and stop the timer
			// the form is re-shown by left-clicking the notify icon
			if (m.Msg == WM_SIZE && m.WParam.ToInt32() == SIZE_MINIMIZED)
			{
				timer1.Enabled = false;
                timer2.Enabled = false;
				Hide();
			}

			base.WndProc(ref m);
		}


		private void applyButton_Click(object sender, EventArgs e)
		{
            //Brazos merge 
			//var controls = new PStateControl[5] { p0StateControl, p1StateControl, p2StateControl, nbp0StateControl, nbp1StateControl };

			var controls = new PStateControl[10] { p0StateControl, p1StateControl, p2StateControl, p3StateControl, p4StateControl, p5StateControl, 
                p6StateControl, p7StateControl, nbp0StateControl, nbp1StateControl };
            var statuscontrols = new StatusControl[1] { statusinfo };

			int lastModifiedControl = Array.FindLastIndex(controls, (c) => { return c.IsModified; } );
			if (lastModifiedControl < 0)
				return; // no control is modified

            //Brazos merge 
			/*if (lastModifiedControl > 2)
            {
                lastModifiedControl = 2; //checking CPU P-States only -> skip NB P-States
            }*/

			if (lastModifiedControl > 7)
            {
                lastModifiedControl = 7; //checking CPU P-States only -> skip NB P-States
            }
			for (int i = 1; i <= lastModifiedControl; i++)
			{
				// make sure the neighboring P-state on the left specifies a >= VID
				if (controls[i - 1].Vid < controls[i].Vid)
				{
					MessageBox.Show(string.Format("P{0}'s VID is greater than P{1}'s.", i, i - 1), "Invalid P-state settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
					tabControl1.SelectedIndex = i;
					return;
				}
            }

			timer1.Enabled = false;
            timer2.Enabled = false;

			//Brazos merge next five lines are commented out in BT
			// try to temporarily set the number of boosted (Turbo) P-states to 0
			bool turboEnabled = K10Manager.IsTurboEnabled();
			int boostedStates = K10Manager.GetNumBoostedStates();
			if (boostedStates != 0)
				K10Manager.SetTurbo(false);
			
			//Brazos merge 
			//for (int i = 0; i < 5; i++)
			for (int i = 0; i < 10; i++)
				controls[i].Save();

			//Brazos merge next two lines are commented out in BT
			if (turboEnabled)
				K10Manager.SetTurbo(true);

			//Brazos merge 
			//for (int i = 0; i < 5; i++)
			// refresh the P-states
            for (int i = 0; i < 10; i++)
                controls[i].LoadFromHardware();
                

            statuscontrols[0].LoadFromHardware();

			timer1.Enabled = true;
            timer2.Enabled = true;
		}

		private void serviceButton_Click(object sender, EventArgs e)
		{
			using (ServiceDialog dialog = new ServiceDialog(this.Icon))
			{
				dialog.ShowDialog();

				// refresh the P-states
				if (dialog.Applied)
				{
					//Brazos merge p3 trough p7 inactive in BT
					//BT also provides integer value to Load for PState, which shouldn't be needed
					p0StateControl.LoadFromHardware();
					p1StateControl.LoadFromHardware();
					p2StateControl.LoadFromHardware();
                    p3StateControl.LoadFromHardware();
                    p4StateControl.LoadFromHardware();
                    p5StateControl.LoadFromHardware();
                    p6StateControl.LoadFromHardware();
                    p7StateControl.LoadFromHardware();
                    nbp0StateControl.LoadFromHardware();
					nbp1StateControl.LoadFromHardware();
                    statusinfo.LoadFromHardware();
				}
			}
		}

        private void paypal_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KDVJC4359EN64");
        }

		private void timer1_Tick(object sender, EventArgs e)
		{
            if (monitorPstates)
            {
                //K10Manager.ResetEffFreq();
                int currentNbPState = K10Manager.GetNbPState();
                nbBar.Value = (2 - currentNbPState) * 50;
                nbPstateLabel.Text = currentNbPState.ToString() + " - " + freq[currentNbPState + 8].ToString() + "MHz";
                
                // get the current P-state of the first core
                for (int i = 0; i < numCores; i++)
                {
                    currentPStateCore[i] = K10Manager.GetCurrentPState(i);
                    if (i == 0)
                    {
                        cpu1Bar.Value = (processBarSteps - currentPStateCore[i]) * (processBarPerc);
                        pstateLabel1.Text = currentPStateCore[i].ToString() + " - " + freq[currentPStateCore[i]].ToString() + "MHz";
                    }
                    else if (i == 1)
                    {
                        cpu2Bar.Value = (processBarSteps - currentPStateCore[i]) * (processBarPerc);
                        pstateLabel2.Text = currentPStateCore[i].ToString() + " - " + freq[currentPStateCore[i]].ToString() + "MHz";
                    }
                    else if (i == 2)
                    {
                        cpu3Bar.Value = (processBarSteps - currentPStateCore[i]) * (processBarPerc);
                        pstateLabel3.Text = currentPStateCore[i].ToString() + " - " + freq[currentPStateCore[i]].ToString() + "MHz";
                    }
                    else if (i == 3)
                    {
                        cpu4Bar.Value = (processBarSteps - currentPStateCore[i]) * (processBarPerc);
                        pstateLabel4.Text = currentPStateCore[i].ToString() + " - " + freq[currentPStateCore[i]].ToString() + "MHz";
                    }
                }
                //RealFreq.SuspendLayout();
                //RealFreq.Text = K10Manager.EffFreq().ToString();
                //RealFreq.ResumeLayout();

            }
		}

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (monitorPstates)
            {
                ecread.SuspendLayout();
                ecread.Text = statusinfo.GetECreadings();
                ecread.ResumeLayout();

                nbCfgTemp.SuspendLayout();
                nbCfgTemp.Text = K10Manager.GetTemp().ToString() + "°C";
                nbCfgTemp.ResumeLayout();
            }
             
            //tabControl1.SuspendLayout();
            //statusinfo.LoadFromHardware();
            //tabControl1.ResumeLayout();
        }

		private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && !this.Visible)
			{
				this.Show();
				this.WindowState = FormWindowState.Normal;

				// refresh the current P-state
				timer1_Tick(timer1, EventArgs.Empty);

                // refresh the temps
                timer2_Tick(timer2, EventArgs.Empty);

				// odd, but necessary
				Refresh();

                // keep refreshing the PStates
                timer1.Enabled = true;

				// keep refreshing the temps
				timer2.Enabled = true;
			}
		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			Application.Exit();
		}

        private void monitorCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (monitorCheckBox.Checked)
            {
                monitorPstates = true;
            }
            else
            {
                monitorPstates = false;
                cpu1Bar.Value = 0;
                cpu2Bar.Value = 0;
                cpu3Bar.Value = 0;
                cpu4Bar.Value = 0;
                nbBar.Value = 0;
                pstateLabel1.Text = "";
                pstateLabel2.Text = "";
                pstateLabel3.Text = "";
                pstateLabel4.Text = "";
                nbPstateLabel.Text = "Family " + family + "h";
            }
        }

        private void ShiftTable(int shifty)
        {
            //Brazos merge next three lines are active in BT
			//this.tabControl1.Location = new System.Drawing.Point(12, 130 + shifty);
            //this.tabControl1.Size = new System.Drawing.Size(350, 345 - shifty);
            //this.MinimumSize = new System.Drawing.Size(225, 345 + shifty);
        }

        private void alwaysOnTopCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (alwaysOnTopCheck.Checked)
            {
                this.TopMost = true;
            }
            else
            {
                this.TopMost = false;
            }
        }

        private void log_now ()
        {
            //create new file or just overwrite the old one
            TextWriter htmlwrite = new StreamWriter("FusionTweaker.log", false);

            htmlwrite.WriteLine("Family: " + family + "h");
            htmlwrite.WriteLine("Bit numbering\t63   59   55   51   47   43   39   35   31   27   23   19   15   11   7    3  0\n"
                            + "COFVID 0071\t\t" + statusinfo.COFVidString() + "\n"
                            + "P-State0 0064\t" + statusinfo.CPUPstate0() + "\n"
                            + "P-State0 0065\t" + statusinfo.CPUPstate1() + "\n"
                            + "P-State0 0066\t" + statusinfo.CPUPstate2() + "\n"
                            + "P-State0 0067\t" + statusinfo.CPUPstate3() + "\n"
                            + "P-State0 0068\t" + statusinfo.CPUPstate4() + "\n"
                            + "P-State0 0069\t" + statusinfo.CPUPstate5() + "\n"
                            + "P-State0 006A\t" + statusinfo.CPUPstate6() + "\n"
                            + "P-State0 006B\t" + statusinfo.CPUPstate7() + "\n"
                //Brazos merge next two lines not in BT
                            + statusinfo.COFVidStringConv() + "\n"
                            + statusinfo.CPUPstateConv() + "\n"
                            );

            htmlwrite.WriteLine("Bit numbering\t\t\t31   27   23   19   15   11   7    3  0\n"
                            + "NB P-State0\t" + statusinfo.NBPstate0() + "\n"
                            + "NB P-State1\t" + statusinfo.NBPstate1() + "\n"
                
            //Brazos merge next two lines not in BT
                            //+ "Advanced Power Mgmnt\t" + statusinfo.APMI() + "\n"
                            //+ "Core Perf Boost Ctrl\t" + statusinfo.CorePerfBoostControl() + "\n"
                            //+ "ClockTiming D18F3xD4\t" + statusinfo.ClockTiming() + "\n"
                            //+ "BIOSClock D0F0xE4_x0130_80F1\t" + statusinfo.BIOSClock());
            //htmlwrite.WriteLine("D18F3x15C\t" + statusinfo.VoltageControl() + "\n"
                //Brazos merge next line not in BT
                            //+ statusinfo.CorePerfBoostControlConv() + "\n"
                            //+ "D0 00\tD1F0 90\tSMBus A0\tD18 C0\n" + statusinfo.DebugOutput() + "\n"
                            + "MSRC001_0061 P-State" + statusinfo.MaxPstate() + "\n"
                            + "BIOS vendor\tBIOS version\tMoBo vendor\tMoBo name\n" + statusinfo.GetReport());

            htmlwrite.Close();
            System.Diagnostics.Process.Start("FusionTweaker.log");
        }

        private void logButton_Click(object sender, EventArgs e)
        {
            //create new file or just overwrite the old one
            TextWriter htmlwrite = new StreamWriter("FusionTweaker.log", false);

            htmlwrite.WriteLine("Family: " + family + "h");
            htmlwrite.WriteLine("Bit numbering\t63   59   55   51   47   43   39   35   31   27   23   19   15   11   7    3  0\n"
                            + "COFVID 0071\t\t" + statusinfo.COFVidString() + "\n" 
                            + "P-State0 0064\t" + statusinfo.CPUPstate0() + "\n"
                            + "P-State0 0065\t" + statusinfo.CPUPstate1() + "\n"
                            + "P-State0 0066\t" + statusinfo.CPUPstate2() + "\n"
                            + "P-State0 0067\t" + statusinfo.CPUPstate3() + "\n"
                            + "P-State0 0068\t" + statusinfo.CPUPstate4() + "\n"
                            + "P-State0 0069\t" + statusinfo.CPUPstate5() + "\n"
                            + "P-State0 006A\t" + statusinfo.CPUPstate6() + "\n"
                            + "P-State0 006B\t" + statusinfo.CPUPstate7() + "\n"
                            //Brazos merge next two lines not in BT
							+ statusinfo.COFVidStringConv() + "\n"
                            + statusinfo.CPUPstateConv() + "\n"
                            );

            htmlwrite.WriteLine("Bit numbering\t\t\t31   27   23   19   15   11   7    3  0\n"
                            + "NB P-State0 D18F3xDC\t" + statusinfo.NBPstate0() + "\n"
                            + "NB P-State1 D18F6x90\t" + statusinfo.NBPstate1() + "\n"
                            //Brazos merge next two lines not in BT
							+ "Advanced Power Mgmnt\t" + statusinfo.APMI() + "\n"
                            + "Core Perf Boost Ctrl\t" + statusinfo.CorePerfBoostControl() + "\n"
                            + "ClockTiming D18F3xD4\t" + statusinfo.ClockTiming() + "\n"
                            + "BIOSClock D0F0xE4_x0130_80F1\t" + statusinfo.BIOSClock());
            htmlwrite.WriteLine("D18F3x15C\t" + statusinfo.VoltageControl() + "\n"
                            //Brazos merge next line not in BT
							+ statusinfo.CorePerfBoostControlConv() + "\n"
                            + "D0 00\tD1F0 90\tSMBus A0\tD18 C0\n" + statusinfo.DebugOutput() + "\n"
                            + "MSRC001_0061 P-State" + statusinfo.MaxPstate() + "\n"
                            + "BIOS vendor\tBIOS version\tMoBo vendor\tMoBo name\n" + statusinfo.GetReport());

            htmlwrite.Close();
            System.Diagnostics.Process.Start("FusionTweaker.log");
        }
    }
}
