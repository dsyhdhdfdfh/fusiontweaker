﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace FusionTweaker
{
    /// <summary>
    /// Represents a set all interesting registers to show.
    /// </summary>
    public sealed partial class StatusControl : UserControl
    {
        private int _optimalWidth;
        private bool _modified;

        private int _index = 0; // 0

        SMBIOS smbios = new SMBIOS();

        public string GetVendor()
        {
            string vendor = "Unknown";
            if (smbios.Board != null)
            {
                vendor = smbios.BIOS.Vendor.ToString();
            }
            return vendor;
        }

        public string GetMobo()
        {
            string mobo = "Unknown";
            if (smbios.Board != null)
            {
                mobo = smbios.Board.ProductName.ToString();
            }
            return mobo;
        }

        public string GetReport()
        {
            StringBuilder output = new StringBuilder();
            if (smbios.Board != null)
            {
                output.AppendLine(smbios.BIOS.Vendor.ToString());
                output.AppendLine(smbios.BIOS.Version.ToString());
                output.AppendLine(smbios.Board.Manufacturer.ToString());
                output.AppendLine(smbios.Board.ProductName.ToString());
            }
            return output.ToString();
            //return test;
        }

        /// <summary>
        /// Gets or sets the associated hardware P-state index (0-9).
        /// </summary>
        public int StatusIndex
        {
            get { return _index; }
            set
            {
                if (value < 0 || value > 9)
                    throw new ArgumentOutOfRangeException("StatusIndex");

                _index = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the control has been modified by the user
        /// since the last load/save operation.
        /// </summary>
        public bool IsModified
        {
            get { return _modified; }
        }

        public StatusControl()
        {
            InitializeComponent();

            _optimalWidth = 270;
            _modified = true;
            refreshButton.TabIndex = 0;
            refreshButton.Click += (s, e) => LoadFromHardware();
        }

        /// <summary>
        /// Returns the delta for the optimal width, based upon the number of cores.
        /// </summary>
        public int GetDeltaOptimalWidth()
        {
            return (_optimalWidth - this.Width);
        }

        /// <summary>
        /// Loads the P-state settings from each core's MSR.
        /// </summary>
        public void LoadFromHardware()
        {
            Reg64CPU.Text = "63     59     55     51     47     43     39     35     31     27     23     19     15     11     7       3    0\n"
                + COFVidString() + "\n" + CPUPstate0() + "\n" + CPUPstate1() + "\n" + CPUPstate2() + "\n" + CPUPstate3() + "\n" + CPUPstate4() + "\n" + CPUPstate5() + "\n" + CPUPstate6() + "\n" + CPUPstate7();
            Reg32NB.Text = "31     27     23     19     15     11     7       3    0\n" + NBPstate0() + "\n" + NBPstate1() + "\n" + ClockTiming() + "\n" + BIOSClock();
            PCIDevices.Text = VoltageControl() + "\n" + DebugOutput() + "\n" + MaxPstate() + "\n";
            PStateReg1.Text = "";
            PStateReg2.Text = "";
            NbPStateReg1.Text = "";
            ClockReg.Text = "";
            BIOSReg.Text = "";
            RegLabel64CPU.Text = "Bit numbering\nCOFVID 0071\nP-State0 0064\nP-State1 0065\nP-State2 0066\nP-State3 0067\nP-State4 0068\nP-State5 0069\nP-State6 006A\nP-State7 006B";
            RegLabel32NB.Text = "Bit numbering\nNB P-State0 D18F3xDC\nNB P-State1 D18F6x90\nClockTiming D18F3xD4\nBIOSClock D0F0xE4_x0130_80F1";
            PCIDevicesLabel.Text = "D18F3x15C\nD0 00\nD1F0 90\nSMBus A0\nD18 C0\nMSRC001_0061 P-State";
            RegLabel4.Text = "";
            RegLabel5.Text = "";
            RegLabel12.Text = "";
            RegLabel13.Text = "";
            RegLabel6.Text = "";
            RegLabel7.Text = "";
            RegLabel8.Text = "";
            RegLabel9.Text = "";
            RegLabel10.Text = "";
            RegLabel11.Text = "";
            _modified = false;
        }

        private string Convert32IntToHex(uint Value)
        {
            string[] tmp = new string[8];
            string conv = "";
            for (int i = 0; i < 8; i++)
            {
                uint tmpvalue = ((Value >> (i * 4)) & 0xF);
                if (tmpvalue < 10) tmp[i] = tmpvalue.ToString();
                else if (tmpvalue == 10) tmp[i] = "A";
                else if (tmpvalue == 11) tmp[i] = "B";
                else if (tmpvalue == 12) tmp[i] = "C";
                else if (tmpvalue == 13) tmp[i] = "D";
                else if (tmpvalue == 14) tmp[i] = "E";
                else if (tmpvalue == 15) tmp[i] = "F";
                else tmp[i] = "X";
            }
                
            for (int i = 0; i < 8; i++)
            {
                conv += tmp[7 - i];
                if ((i + 1) % 4 == 0) conv += " ";
            }
            conv += "h"; 
            return conv;
        }

        private uint ReadWord(ushort regPort, ushort valPort, byte regIndex)
        {
            Program.Ols.WriteIoPortByte(regPort, regIndex);
            uint value = (uint)Program.Ols.ReadIoPortByte(valPort) << 8;
            regIndex++;
            Program.Ols.WriteIoPortByte(regPort, regIndex);
            value |= Program.Ols.ReadIoPortByte(valPort);

            return value;
        }

        private void WriteByte(ushort regPort, ushort valPort, byte regIndex, byte value)
        {
            Program.Ols.WriteIoPortByte(regPort, regIndex);
            Program.Ols.WriteIoPortByte(valPort, value);
        }

        public string IOInterface()
        {
            byte superIOReg = 0x2E; // port for the SuperIO register index
		    byte superIOVal = 0x2F; // port for the SuperIO register value 
            uint _isaAddress;
		    // enter the MB PnP mode
		    Program.Ols.WriteIoPortByte(superIOReg, 0x87);
            Program.Ols.WriteIoPortByte(superIOReg, 0x01);
            Program.Ols.WriteIoPortByte(superIOReg, 0x55);
            Program.Ols.WriteIoPortByte(superIOReg, 0x55);

		    // check the device ID in registers 0x20 and 0x21
		    uint deviceID = ReadWord(superIOReg, superIOVal, 0x20);
		    //if ((deviceID >> 8) != 0x87)
            //    throw new ArgumentOutOfRangeException("No IT87xxF chip found.");
		    // select logical device #4, the environment controller, by writing 0x04 to register 0x07
            WriteByte(superIOReg, superIOVal, 0x07, 0x04);

		    // read the environment controller's ISA address from registers 0x60 and 0x61
		    // the register port for the controller is _isaAddress + 0x05, the value port _isaAddress + 0x06
            _isaAddress = ReadWord(superIOReg, superIOVal, 0x60);

		    // exit the MB PnP mode
            WriteByte(superIOReg, superIOVal, 0x02, 0x02);
	    return "Check";
        }

        public string DebugOutput()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0x00, 0x00);
            text += Convert32IntToHex(settings) + "\n"; 
            settings = Program.Ols.ReadPciConfig(0x90, 0x00);
            text += Convert32IntToHex(settings) + "\n";
            settings = Program.Ols.ReadPciConfig(0xA0, 0x00);
            text += Convert32IntToHex(settings) + "\n";
            settings = Program.Ols.ReadPciConfig(0xC0, 0x00);
            text += Convert32IntToHex(settings);
            return text;
        }

        public string VoltageControl()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC3, 0x15C);
            double vidl0 = 1.55 - ((settings >> 0 & 0x7F) * 0.0125);
            double vidl1 = 1.55 - ((settings >> 8 & 0x7F) * 0.0125);
            double vidl2 = 1.55 - ((settings >> 16 & 0x7F) * 0.0125);
            double vidl3 = 1.55 - ((settings >> 24 & 0x7F) * 0.0125);

            text += "VidL0 " + vidl0 + "V VidL1 " + vidl1 + "V VidL2 " + vidl2 + "V VidL3 " + vidl3 + "V";
            return text;
        }

        public string MaxPstate()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010061u);
            uint maxP = (uint)(msr >> 4 & 0x7);
            uint minP = (uint)(msr & 0x7);
            text += "MaxPState: " + maxP + " MinPState: " + minP;
            return text;
        }
        
        public string COFVidString()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010071u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string COFVidStringConv()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010071u);
            text += "MainPllOpFreqIdMax: " + ((msr >> 49) & 0x3F)
                    + " StartupPstate: " + ((msr >> 32) & 0x7)
                    + " MinVid(V): " + (1.55 - ((msr >> 42) & 0x7F) * 0.0125) 
                    + " MaxVid(V): " + (1.55 - ((msr >> 35) & 0x7F) * 0.0125) + "\n"
                    + " CurPstateLimit: " + ((msr >> 56) & 0x7) 
                    + " CurPstate: " + ((msr >> 16) & 0x7)
                    + " PstateInProgress: " + ((msr >> 20) & 0x1) + "\n"
                    + " CurCpuVid(V): " + (1.55 - ((msr >> 9) & 0x7F) * 0.0125)
                    + " CurNbVid(V): " + (1.55 - ((msr >> 25) & 0x7F) * 0.0125) + "\n"
                    + " CurCpuFid: " + ((msr >> 4) & 0x1F)
                    + " CurCpuDid: " + (msr & 0xF);
            return text;
        }

        public string CPUPstateConv()
        {
            string text = "";
            ulong[] msr = new ulong[8];
            msr[0] = Program.Ols.ReadMsr(0xC0010064u);
            msr[1] = Program.Ols.ReadMsr(0xC0010065u);
            msr[2]= Program.Ols.ReadMsr(0xC0010066u);
            msr[3] = Program.Ols.ReadMsr(0xC0010067u);
            msr[4] = Program.Ols.ReadMsr(0xC0010068u);
            msr[5] = Program.Ols.ReadMsr(0xC0010069u);
            msr[6] = Program.Ols.ReadMsr(0xC001006Au);
            msr[7] = Program.Ols.ReadMsr(0xC001006Bu);

            for (int i = 0; i < 8; i++)
            {
                text += "Pstate: P" + i + " PStateEn: " + ((msr[i] >> 63) & 0x1)
                    + " IddDiv: " + ((msr[i] >> 41) & 0x1) + ((msr[i] >> 40) & 0x1)
                    + " IddValue: " + ((msr[i] >> 32) & 0xFF) + "\n"
                    + "CpuVid(V): " + (1.55 - ((msr[i] >> 9) & 0x7F) * 0.0125)
                    + " CpuFid: " + ((msr[i] >> 4) & 0x1F)
                    + " CpuDid: " + (msr[i] & 0xF) + "\n";
            }

            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            Program.Ols.Cpuid(0x80000007u, ref eax, ref ebx, ref ecx, ref edx);
            text += "CPB: " + ((edx >> 9) & 0x1) + " HwPstate: " + ((edx >> 7) & 0x1) + "\n";
            return text;
        }

        public string CorePerfBoostControl()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC3, 0x15C);
            for (int i = 0; i < 32; i++)
            {
                text += (settings >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CorePerfBoostControlConv()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC3, 0x15C);
            text += "BoostEnAllCore: " + ((settings >> 29) & 0x1) + " IgnoreBoostThresh: " + ((settings >> 28) & 0x1)
                + " NumBoostStates: " + ((settings >> 2) & 0x3) + " BoostSrc: " + ((settings >> 1) & 0x1) + ((settings >> 0) & 0x1);
            return text;
        }

        public string APMI()
        {
            string text = "";
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            Program.Ols.Cpuid(0x80000007u, ref eax, ref ebx, ref ecx, ref edx);
            for (int i = 0; i < 32; i++)
            {
                text += (edx >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate0()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010064u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate1()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010065u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate2()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010066u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate3()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010067u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate4()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010068u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate5()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC0010069u);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate6()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC001006Au);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string CPUPstate7()
        {
            string text = "";
            ulong msr = Program.Ols.ReadMsr(0xC001006Bu);
            for (int i = 0; i < 64; i++)
            {
                text += (msr >> (63 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string NBPstate0()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC3, 0xDC);
            for (int i = 0; i < 32; i++)
            {
                text += (settings >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string NBPstate1()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC6, 0x90);
            for (int i = 0; i < 32; i++)
            {
                text += (settings >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

        public string ClockTiming()
        {
            string text = "";
            uint settings = Program.Ols.ReadPciConfig(0xC3, 0xD4);
            for (int i = 0; i < 32; i++)
            {
                text += (settings >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }

         public string BIOSClock()
        {
            string text = "";
            Program.Ols.WritePciConfig(0x00, 0xE0, 0x013080F1);
            // value of interest: D0F0xE4_x0130_80F1
            uint settings = Program.Ols.ReadPciConfig(0x00, 0xE4); 
            for (int i = 0; i < 32; i++)
            {
                text += (settings >> (31 - i) & 0x1).ToString();
                if ((i + 1) % 4 == 0) text += " ";
            }
            text += "";
            return text;
        }
        
    }
}