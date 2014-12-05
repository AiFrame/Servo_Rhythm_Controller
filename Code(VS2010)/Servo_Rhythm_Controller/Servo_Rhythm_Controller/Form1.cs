using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace SerialportSample
{
    public partial class SerialportSampleForm : Form
    {
        #region 串口
        //初始化串口的一个新实例
        private SerialPort comm = new SerialPort();

        private StringBuilder builder = new StringBuilder();//避免在事件处理方法中反复的创建，定义到外面。

        private long received_count = 0;//接收计数
        private long send_count = 0;//发送计数

        private bool Listening = false;//是否没有执行完invoke相关操作  
        private bool Closing_new = false;//是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke  
        private const string frameheader = "#";
        private const string frameTail = "!";
        private bool Cmd_Read_AD = false;//read AD flag
        private bool Cmd_READ = false;//读动作组示记
        private bool Cmd_DOWN = false;//下载动作组示记
        private bool Cmd_Clear = false;//擦除标记记
        private bool RoundRunflag = false;//循环运行标记
        private bool Runflag = false;//运行标记
        private bool ReplaceTimeflag = false;//改时间标记此时不能发送。
        private int listBoxSendnum = 0;//发送动作计数。
        public SerialportSampleForm()//构造函数
        {
            InitializeComponent();
            //初始化下拉串口名称列表框
            //获取当前计算机串口的名称数组
            string[] ports = SerialPort.GetPortNames();
            comboPortName.Items.AddRange(ports);
            //设置端口选定项索引
            comboPortName.SelectedIndex = comboPortName.Items.Count > 0 ? 0 : -1;
            comboBaudrate.SelectedIndex = comboBaudrate.Items.IndexOf("115200");
            comboBoxParity.SelectedIndex = comboBoxParity.Items.IndexOf("None");
            comboBox_AD_port.SelectedIndex = comboBox_AD_port.Items.IndexOf("0");
            //初始化SerialPort对象
            comm.NewLine = "/r/n";
            comm.RtsEnable = false;//根据实际情况吧。
            //添加事件注册
            comm.DataReceived += comm_DataReceived;

            //设置发送按钮的状态
            // buttonSend.Enabled = comm.IsOpen;
            //buttonOpenClose.Focus();

            timerCheckComPorts.Enabled = true;
            txtS1.Text = Convert.ToString(tra_S1.Value);
        }
        #endregion

        void comm_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (Closing_new) return;// 
            try
            {
                Listening = true;// 
                int n = comm.BytesToRead;// 
                byte[] buf = new byte[n];// 
                received_count += n;// 
                comm.Read(buf, 0, n);// 
                builder.Clear();// 
                // 
                this.Invoke((EventHandler)(delegate
                {
                    // 
                    if (checkBoxHexView.Checked)
                    {
                        // 
                        foreach (byte b in buf)
                        {  // 
                            builder.Append(b.ToString("X2") + " ");
                        }
                    }
                    else
                    {
                        // 
                        builder.Append(Encoding.ASCII.GetString(buf));
                        String tempS = builder.ToString();
                        // 
                        this.txGet.AppendText(tempS);

                        if (Cmd_READ)
                        {
                            int temp;
                            if (isNumberic(tempS, out temp))
                            {
                                comboBoxTAnum.Items.Clear();
                                for (int i = 0; i < temp; i++)
                                    comboBoxTAnum.Items.Add(i + 1);
                                if (comboBoxTAnum.Items.Count > 0) comboBoxTAnum.SelectedIndex = comboBoxTAnum.Items.Count - 1;
                            }
                            Cmd_READ = false;
                        }
                        if (Cmd_DOWN)// 
                        {
                            if (builder.ToString() == "A")// 
                            {
                                if (listBoxSendnum < listBoxSend.Items.Count)
                                {
                                    //comm.Write(listBoxSend.Items[listBoxSendnum].ToString());
                                    listBoxSend.SelectedIndex = listBoxSendnum;
                                    listBoxSendnum++;

                                }
                                else
                                {
                                    Cmd_DOWN = false;
                                    listBoxSendnum = 0;
                                    comm.Write("STOP!");// 
                                    MessageBox.Show("Download complete！！");
                                }
                            }

                        }
                        if (Cmd_Clear)
                        {
                            if (builder.ToString() == "U") { MessageBox.Show("Erase FLASH complete！！"); Cmd_Clear = false; }
                        }
                        if (Cmd_Read_AD)
                        {
                            textBox_AD.Text = "";
                            this.textBox_AD.AppendText(builder.ToString());
                            Cmd_Read_AD = false;
                        }
                    }


                    // 
                    labelGetCount.Text = "Receive:" + received_count.ToString();
                }));
            }
            finally
            {
                Listening = false;//  
            }

        }

        private void buttonOpenClose_Click_1(object sender, EventArgs e)
        {
            // 
            //  
            if (comm.IsOpen)
            {
                // 
                comm.Close();
            }
            else
            {
                // 
                comm.PortName = comboPortName.Text;
                comm.BaudRate = int.Parse(comboBaudrate.Text);
                // 
                comm.Parity = (Parity)Enum.Parse(typeof(Parity), comboBoxParity.SelectedItem.ToString());
                try
                {
                    comm.Open(); // 
                }
                catch (Exception ex)
                {
                    // 
                    comm = new SerialPort();
                    // 
                    MessageBox.Show(ex.Message);
                }
            }
            // 
            buttonOpenClose.Text = comm.IsOpen ? "Close" : "Open";
            buttonSend.Enabled = comm.IsOpen;

        }

        // 
        private void checkBoxNewlineGet_CheckedChanged(object sender, EventArgs e)
        {
            txGet.WordWrap = checkBoxNewlineGet.Checked;
        }

        private void buttonReset_Click_1(object sender, EventArgs e)
        {
            // 
            send_count = received_count = 0;
            labelGetCount.Text = "Receive:0";
            labelSendCount.Text = "Send:0";
            txGet.Text = "";
        }

        private void buttonSend_Click_1(object sender, EventArgs e)
        {
            // 
            int n = 0;
            // 
            if (checkBoxHexSend.Checked)
            {
                // 
                MatchCollection mc = Regex.Matches(txSend.Text, @"(?i)[0-9a-f]{2}");

                List<byte> buf = new List<byte>();// 
                // 
                foreach (Match m in mc)
                {
                    buf.Add(byte.Parse(m.Value));
                }
                // 

                comm.Write(buf.ToArray(), 0, buf.Count);
                // 
                n = buf.Count;
            }
            else// 
            {
                // 
                if (checkBoxNewlineSend.Checked)
                {
                    comm.WriteLine(txSend.Text);
                    n = txSend.Text.Length + 2;
                }
                else// 
                {
                    comm.Write(txSend.Text);
                    n = txSend.Text.Length;
                }
            }
            send_count += n;// 
            labelSendCount.Text = "Send:" + send_count.ToString();// 
        }

        private void SerialportSampleForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (comm.IsOpen)
            {
                // 
                comm.Close();
            }
            // 
            buttonOpenClose.Text = comm.IsOpen ? "Close" : "Open";
            buttonSend.Enabled = comm.IsOpen;


             

        }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerCheckComPorts_Tick(object sender, EventArgs e)
        {
            // 
            // 
            string[] ports = SerialPort.GetPortNames();
            // 
            if (comboPortName.Items.Count != ports.Length)
            {
                // 
                comboPortName.Items.Clear();
                // 
                comboPortName.Items.AddRange(ports);
                // 
                comboPortName.SelectedIndex = comboPortName.Items.Count > 0 ? 0 : -1;
            }
        }
        private void commWrite(string strText, string num)
        {
            // 
            if (comm.IsOpen) comm.Write(frameheader + num + strText + "T100" + frameTail);
        }

        #region

        //所有的滑动条程序
        private void tra_S1_Scroll(object sender, EventArgs e)
        {
            txtS1.Text = Convert.ToString(tra_S1.Value);
            commWrite(txtS1.Text, "1P");
        }

        private void tra_S2_Scroll(object sender, EventArgs e)
        {
            txtS2.Text = Convert.ToString(tra_S2.Value);
            commWrite(txtS2.Text, "2P");
        }

        private void tra_S3_Scroll(object sender, EventArgs e)
        {
            txtS3.Text = Convert.ToString(tra_S3.Value);
            commWrite(txtS3.Text, "3P");
        }

        private void tra_S4_Scroll(object sender, EventArgs e)
        {
            txtS4.Text = Convert.ToString(tra_S4.Value);
            commWrite(txtS4.Text, "4P");
        }

        private void tra_S5_Scroll(object sender, EventArgs e)
        {
            txtS5.Text = Convert.ToString(tra_S5.Value);
            commWrite(txtS5.Text, "5P");
        }
        private void tra_S6_Scroll(object sender, EventArgs e)
        {
            txtS6.Text = Convert.ToString(tra_S6.Value);
            commWrite(txtS6.Text, "6P");
        }
        private void tra_S7_Scroll(object sender, EventArgs e)
        {
            txtS7.Text = Convert.ToString(tra_S7.Value);
            commWrite(txtS7.Text, "7P");
        }

        private void tra_S8_Scroll(object sender, EventArgs e)
        {
            txtS8.Text = Convert.ToString(tra_S8.Value);
            commWrite(txtS8.Text, "8P");
        }

        private void tra_S9_Scroll(object sender, EventArgs e)
        {
            txtS9.Text = Convert.ToString(tra_S9.Value);
            commWrite(txtS9.Text, "9P");
        }

        private void tra_S10_Scroll(object sender, EventArgs e)
        {
            txtS10.Text = Convert.ToString(tra_S10.Value);
            commWrite(txtS10.Text, "10P");
        }

        private void tra_S11_Scroll(object sender, EventArgs e)
        {
            txtS11.Text = Convert.ToString(tra_S11.Value);
            commWrite(txtS11.Text, "11P");
        }

        private void tra_S12_Scroll(object sender, EventArgs e)
        {
            txtS12.Text = Convert.ToString(tra_S12.Value);
            commWrite(txtS12.Text, "12P");
        }

        private void tra_S13_Scroll(object sender, EventArgs e)
        {
            txtS13.Text = Convert.ToString(tra_S13.Value);
            commWrite(txtS13.Text, "13P");
        }

        private void tra_S14_Scroll(object sender, EventArgs e)
        {
            txtS14.Text = Convert.ToString(tra_S14.Value);
            commWrite(txtS14.Text, "14P");
        }

        private void tra_S15_Scroll(object sender, EventArgs e)
        {
            txtS15.Text = Convert.ToString(tra_S15.Value);
            commWrite(txtS15.Text, "15P");
        }

        private void tra_S16_Scroll(object sender, EventArgs e)
        {
            txtS16.Text = Convert.ToString(tra_S16.Value);
            commWrite(txtS16.Text, "16P");
        }

        private void tra_S17_Scroll(object sender, EventArgs e)
        {
            txtS17.Text = Convert.ToString(tra_S17.Value);
            commWrite(txtS17.Text, "17P");
        }

        private void tra_S18_Scroll(object sender, EventArgs e)
        {
            txtS18.Text = Convert.ToString(tra_S18.Value);
            commWrite(txtS18.Text, "18P");
        }

        private void tra_S19_Scroll(object sender, EventArgs e)
        {
            txtS19.Text = Convert.ToString(tra_S19.Value);
            commWrite(txtS19.Text, "19P");
        }

        private void tra_S20_Scroll(object sender, EventArgs e)
        {
            txtS20.Text = Convert.ToString(tra_S20.Value);
            commWrite(txtS20.Text, "20P");
        }

        private void tra_S21_Scroll(object sender, EventArgs e)
        {
            txtS21.Text = Convert.ToString(tra_S21.Value);
            commWrite(txtS21.Text, "21P");
        }

        private void tra_S22_Scroll(object sender, EventArgs e)
        {
            txtS22.Text = Convert.ToString(tra_S22.Value);
            commWrite(txtS22.Text, "22P");
        }

        private void tra_S23_Scroll(object sender, EventArgs e)
        {
            txtS23.Text = Convert.ToString(tra_S23.Value);
            commWrite(txtS23.Text, "23P");
        }

        private void tra_S24_Scroll(object sender, EventArgs e)
        {
            txtS24.Text = Convert.ToString(tra_S24.Value);
            commWrite(txtS24.Text, "24P");
        }

        private void tra_S25_Scroll(object sender, EventArgs e)
        {
            txtS25.Text = Convert.ToString(tra_S25.Value);
            commWrite(txtS25.Text, "25P");
        }

        private void tra_S26_Scroll(object sender, EventArgs e)
        {
            txtS26.Text = Convert.ToString(tra_S26.Value);
            commWrite(txtS26.Text, "26P");
        }

        private void tra_S27_Scroll(object sender, EventArgs e)
        {
            txtS27.Text = Convert.ToString(tra_S27.Value);
            commWrite(txtS27.Text, "27P");
        }

        private void tra_S28_Scroll(object sender, EventArgs e)
        {
            txtS28.Text = Convert.ToString(tra_S28.Value);
            commWrite(txtS28.Text, "28P");
        }

        private void tra_S29_Scroll(object sender, EventArgs e)
        {
            txtS29.Text = Convert.ToString(tra_S29.Value);
            commWrite(txtS29.Text, "29P");
        }

        private void tra_S30_Scroll(object sender, EventArgs e)
        {
            txtS30.Text = Convert.ToString(tra_S30.Value);
            commWrite(txtS30.Text, "30P");
        }

        private void tra_S31_Scroll(object sender, EventArgs e)
        {
            txtS31.Text = Convert.ToString(tra_S31.Value);
            commWrite(txtS31.Text, "31P");
        }

        private void tra_S32_Scroll(object sender, EventArgs e)
        {
            txtS32.Text = Convert.ToString(tra_S32.Value);
            commWrite(txtS32.Text, "32P");
        }
        #endregion
        #region
        // 
        private void buttonS1_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS1.Text);
            tra_S1.Value = i;
            commWrite(txtS1.Text, "1P");
        }

        private void buttonS2_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS2.Text);
            tra_S2.Value = i;
            commWrite(txtS2.Text, "2P");
        }

        private void buttonS3_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS3.Text);
            tra_S3.Value = i;
            commWrite(txtS3.Text, "3P");
        }

        private void buttonS4_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS4.Text);
            tra_S4.Value = i;
            commWrite(txtS4.Text, "4P");
        }

        private void buttonS5_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS5.Text);
            tra_S5.Value = i;
            commWrite(txtS5.Text, "5P");
        }

        private void buttonS6_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS6.Text);
            tra_S6.Value = i;
            commWrite(txtS6.Text, "6P");
        }

        private void buttonS7_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS7.Text);
            tra_S7.Value = i;
            commWrite(txtS7.Text, "7P");
        }

        private void buttonS8_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS8.Text);
            tra_S8.Value = i;
            commWrite(txtS8.Text, "8P");
        }

        private void buttonS9_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS9.Text);
            tra_S9.Value = i;
            commWrite(txtS9.Text, "9P");
        }

        private void buttonS10_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS10.Text);
            tra_S10.Value = i;
            commWrite(txtS10.Text, "10P");
        }

        private void buttonS11_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS11.Text);
            tra_S11.Value = i;
            commWrite(txtS11.Text, "11P");
        }

        private void buttonS12_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS12.Text);
            tra_S12.Value = i;
            commWrite(txtS12.Text, "12P");
        }

        private void buttonS13_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS13.Text);
            tra_S13.Value = i;
            commWrite(txtS13.Text, "13P");
        }

        private void buttonS14_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS14.Text);
            tra_S14.Value = i;
            commWrite(txtS14.Text, "14P");
        }

        private void buttonS15_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS15.Text);
            tra_S15.Value = i;
            commWrite(txtS15.Text, "15P");
        }

        private void buttonS16_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS16.Text);
            tra_S16.Value = i;
            commWrite(txtS16.Text, "16P");
        }

        private void buttonS17_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS17.Text);
            tra_S17.Value = i;
            commWrite(txtS17.Text, "17P");
        }

        private void buttonS18_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS18.Text);
            tra_S18.Value = i;
            commWrite(txtS18.Text, "18P");
        }

        private void buttonS19_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS19.Text);
            tra_S19.Value = i;
            commWrite(txtS19.Text, "19P");
        }

        private void buttonS20_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS20.Text);
            tra_S20.Value = i;
            commWrite(txtS20.Text, "20P");
        }

        private void buttonS21_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS21.Text);
            tra_S21.Value = i;
            commWrite(txtS21.Text, "21P");
        }

        private void buttonS22_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS22.Text);
            tra_S22.Value = i;
            commWrite(txtS22.Text, "22P");
        }

        private void buttonS23_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS23.Text);
            tra_S23.Value = i;
            commWrite(txtS23.Text, "23P");
        }

        private void buttonS24_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS24.Text);
            tra_S24.Value = i;
            commWrite(txtS24.Text, "24P");
        }

        private void buttonS25_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS25.Text);
            tra_S25.Value = i;
            commWrite(txtS25.Text, "25P");
        }

        private void buttonS26_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS26.Text);
            tra_S26.Value = i;
            commWrite(txtS26.Text, "26P");
        }

        private void buttonS27_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS27.Text);
            tra_S27.Value = i;
            commWrite(txtS27.Text, "27P");
        }

        private void buttonS28_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS28.Text);
            tra_S28.Value = i;
            commWrite(txtS28.Text, "28P");
        }

        private void buttonS29_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS29.Text);
            tra_S29.Value = i;
            commWrite(txtS29.Text, "29P");
        }

        private void buttonS30_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS30.Text);
            tra_S30.Value = i;
            commWrite(txtS30.Text, "30P");
        }

        private void buttonS31_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS31.Text);
            tra_S31.Value = i;
            commWrite(txtS31.Text, "31P");
        }

        private void buttonS32_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtS32.Text);
            tra_S32.Value = i;
            commWrite(txtS32.Text, "32P");
        }
        #endregion

        private string listItems(string strText, string num)
        {
            // 
            return frameheader + num + strText;
        }
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            listBoxSend.Items.Add(listItems(txtS1.Text, "1P") +
                                  listItems(txtS2.Text, "2P") +
                                  listItems(txtS3.Text, "3P") +
                                  listItems(txtS4.Text, "4P") +
                                  listItems(txtS5.Text, "5P") +
                                  listItems(txtS6.Text, "6P") +
                                  listItems(txtS7.Text, "7P") +
                                  listItems(txtS8.Text, "8P") +
                                  listItems(txtS9.Text, "9P") +
                                  listItems(txtS10.Text, "10P") +
                                  listItems(txtS11.Text, "11P") +
                                  listItems(txtS12.Text, "12P") +
                                  listItems(txtS13.Text, "13P") +
                                  listItems(txtS14.Text, "14P") +
                                  listItems(txtS15.Text, "15P") +
                                  listItems(txtS16.Text, "16P") +
                                  listItems(txtS17.Text, "17P") +
                                  listItems(txtS18.Text, "18P") +
                                  listItems(txtS19.Text, "19P") +
                                  listItems(txtS20.Text, "20P") +
                                  listItems(txtS21.Text, "21P") +
                                  listItems(txtS22.Text, "22P") +
                                  listItems(txtS23.Text, "23P") +
                                  listItems(txtS24.Text, "24P") +
                                  listItems(txtS25.Text, "25P") +
                                  listItems(txtS26.Text, "26P") +
                                  listItems(txtS27.Text, "27P") +
                                  listItems(txtS28.Text, "28P") +
                                  listItems(txtS29.Text, "29P") +
                                  listItems(txtS30.Text, "30P") +
                                  listItems(txtS31.Text, "31P") +
                                  listItems(txtS32.Text, "32P") +
                                  "T" + txtTime.Text + frameTail
                                  );

        }

        private void buttonDel_Click(object sender, EventArgs e)
        {
            if (listBoxSend.SelectedIndex >= 0)
            {
                listBoxSend.Items.RemoveAt(listBoxSend.SelectedIndex);
            }
        }

        private void buttonClean_Click(object sender, EventArgs e)
        {
            listBoxSend.Items.Clear();
        }

        private void buttonCutin_Click(object sender, EventArgs e)
        {
            listBoxSend.Items.Insert(listBoxSend.SelectedIndex + 1,
                                  listItems(txtS1.Text, "1P") +
                                  listItems(txtS2.Text, "2P") +
                                  listItems(txtS3.Text, "3P") +
                                  listItems(txtS4.Text, "4P") +
                                  listItems(txtS5.Text, "5P") +
                                  listItems(txtS6.Text, "6P") +
                                  listItems(txtS7.Text, "7P") +
                                  listItems(txtS8.Text, "8P") +
                                  listItems(txtS9.Text, "9P") +
                                  listItems(txtS10.Text, "10P") +
                                  listItems(txtS11.Text, "11P") +
                                  listItems(txtS12.Text, "12P") +
                                  listItems(txtS13.Text, "13P") +
                                  listItems(txtS14.Text, "14P") +
                                  listItems(txtS15.Text, "15P") +
                                  listItems(txtS16.Text, "16P") +
                                  listItems(txtS17.Text, "17P") +
                                  listItems(txtS18.Text, "18P") +
                                  listItems(txtS19.Text, "19P") +
                                  listItems(txtS20.Text, "20P") +
                                  listItems(txtS21.Text, "21P") +
                                  listItems(txtS22.Text, "22P") +
                                  listItems(txtS23.Text, "23P") +
                                  listItems(txtS24.Text, "24P") +
                                  listItems(txtS25.Text, "25P") +
                                  listItems(txtS26.Text, "26P") +
                                  listItems(txtS27.Text, "27P") +
                                  listItems(txtS28.Text, "28P") +
                                  listItems(txtS29.Text, "29P") +
                                  listItems(txtS30.Text, "30P") +
                                  listItems(txtS31.Text, "31P") +
                                  listItems(txtS32.Text, "32P") +
                                  "T" + txtTime.Text + frameTail
                                  );
        }

        private void listBoxSend_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSend.SelectedItem == null) { }
            else
            {
                if ((comm.IsOpen) && (!ReplaceTimeflag)) comm.Write(listBoxSend.SelectedItem.ToString());
                translate_command(System.Text.Encoding.ASCII.GetBytes(listBoxSend.SelectedItem.ToString()));
            }
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            int temp = 0;
            temp = int.Parse(txtTime.Text);
            if ((listBoxSend.Items.Count > 0) && (comm.IsOpen))
            {
                //if (temp<500)
                Runflag = true;
                timerRunTime.Interval = temp + 50;
                timerRunTime.Enabled = true;
            }
        }

        private void buttonCmd_Clear_Click(object sender, EventArgs e)
        {
            if (comm.IsOpen)
            {
                Runflag = false;
                RoundRunflag = false;
                if (MessageBox.Show("This will clear all data!Are you sure？", "Erase FLASH!", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    comm.Write("CLEAR!");// 
                    Cmd_Clear = true;
                    comboBoxTAnum.Items.Clear();
                }

            }
        }

        private void buttonCmd_Enable_Click(object sender, EventArgs e)
        {
            if ((buttonCmd_Enable.Text == "Disable"))
            {
                if (comm.IsOpen) comm.Write("EN0!");// 
                buttonCmd_Enable.Text = "Enable";
            }
            else
            {
                if (comboBoxTAnum.Text != "")
                {
                    if (comm.IsOpen) comm.Write("EN" + comboBoxTAnum.Text + "!");// 
                    buttonCmd_Enable.Text = "Disable";
                }
            }
        }

        private void buttonCmd_Go_Click(object sender, EventArgs e)
        {
            if (comm.IsOpen) comm.Write("GO!");// 
            buttonCmd_Enable.Text = "Disable";
        }

        private void buttonCmd_READ_Click(object sender, EventArgs e)
        {
            if (comm.IsOpen) { comm.Write("READ!"); Cmd_READ = true; }// 
        }

        private void saveFileDialogSave_FileOk(object sender, CancelEventArgs e)
        {
            string names = saveFileDialogSave.FileName;
            StreamWriter writer = new StreamWriter(names);
            int i = listBoxSend.Items.Count;
            for (int j = 0; j < i; j++)
            {
                writer.WriteLine(listBoxSend.Items[j].ToString());
            }
            writer.Close();
            MessageBox.Show("Data saved！！");
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            saveFileDialogSave.Filter = "Text(*.txt)|*.txt";
            saveFileDialogSave.FilterIndex = 1;
            saveFileDialogSave.InitialDirectory = Application.StartupPath;
            saveFileDialogSave.ShowDialog();
        }
        private void openFileDialogIn_FileOk(object sender, CancelEventArgs e)
        {
            string str = null;
            string names = openFileDialogIn.FileName;
            StreamReader sr = new StreamReader(names);
            while ((str = sr.ReadLine()) != null)// 
            {
                listBoxSend.Items.Add(str);
            }
            //  MessageBox.Show("OK");
        }
        private void buttonIn_Click(object sender, EventArgs e)
        {

            openFileDialogIn.Filter = "Text(*.txt)|*.txt";
            openFileDialogIn.FilterIndex = 1;
            openFileDialogIn.InitialDirectory = Application.StartupPath;
            openFileDialogIn.ShowDialog();

        }

        private void buttonCmd_DOWN_Click(object sender, EventArgs e)
        {
            Runflag = false;
            RoundRunflag = false;
            if ((listBoxSend.Items.Count > 0) && (comm.IsOpen))
            {
                listBoxSend.SelectedIndex = -1;
                comm.Write("DOWN0!"); Cmd_DOWN = true;
            }
        }

        private void timerRunTime_Tick(object sender, EventArgs e)
        {

            if ((comm.IsOpen) && (Runflag || RoundRunflag))
            {
                if (listBoxSendnum < listBoxSend.Items.Count)
                {
                    //comm.Write(listBoxSend.Items[listBoxSendnum].ToString());
                    // 
                    buttonReplaceTime.Enabled = false;
                    listBoxSend.SelectedIndex = listBoxSendnum;
                    listBoxSendnum++;
                }
                else
                {
                    listBoxSendnum = 0;
                    timerRunTime.Enabled = RoundRunflag;
                    if (RoundRunflag) buttonReplaceTime.Enabled = false;
                    else buttonReplaceTime.Enabled = true;
                }
            }
        }

        private void buttonRoundRun_Click(object sender, EventArgs e)
        {
            buttonRun_Click(null, null);
            //comm.Write("RUN!");
            RoundRunflag = true;
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Runflag = false;
            RoundRunflag = false;
            timerRunTime.Enabled = false;
            listBoxSendnum = 0;
            buttonReplaceTime.Enabled = true;
            Runflag = false;

        }

        private void buttonReplaceTime_Click(object sender, EventArgs e)
        {
            //buttonStop_Click(null,null);
            string temp = "";
            Runflag = false;
            RoundRunflag = false;
            ReplaceTimeflag = true;
            // listBoxSend.SelectedIndex = -1;
            for (int i = 0; i < listBoxSend.Items.Count; i++)
            {
                temp = "";
                temp = listBoxSend.Items[i].ToString();
                int j = temp.IndexOf("T");
                temp = temp.Remove(j, temp.Length - j) + "T" + txtTime.Text + "!";
                listBoxSend.Items.RemoveAt(i);
                listBoxSend.Items.Insert(i, temp);
            }
            ReplaceTimeflag = false;
        }

        private void button_CENTER_ALL_Click(object sender, EventArgs e)
        {
            txtS1.Text = "1500";
            tra_S1.Value = 1500;
            txtS2.Text = "1500";
            tra_S2.Value = 1500;
            txtS3.Text = "1500";
            tra_S3.Value = 1500;
            txtS4.Text = "1500";
            tra_S4.Value = 1500;
            txtS5.Text = "1500";
            tra_S5.Value = 1500;
            txtS6.Text = "1500";
            tra_S6.Value = 1500;
            txtS7.Text = "1500";
            tra_S7.Value = 1500;
            txtS8.Text = "1500";
            tra_S8.Value = 1500;
            txtS9.Text = "1500";
            tra_S9.Value = 1500;
            txtS10.Text = "1500";
            tra_S10.Value = 1500;
            txtS11.Text = "1500";
            tra_S11.Value = 1500;
            txtS12.Text = "1500";
            tra_S12.Value = 1500;
            txtS13.Text = "1500";
            tra_S13.Value = 1500;
            txtS14.Text = "1500";
            tra_S14.Value = 1500;
            txtS15.Text = "1500";
            tra_S15.Value = 1500;
            txtS16.Text = "1500";
            tra_S16.Value = 1500;
            txtS17.Text = "1500";
            tra_S17.Value = 1500;
            txtS18.Text = "1500";
            tra_S18.Value = 1500;
            txtS19.Text = "1500";
            tra_S19.Value = 1500;
            txtS20.Text = "1500";
            tra_S20.Value = 1500;
            txtS21.Text = "1500";
            tra_S21.Value = 1500;
            txtS22.Text = "1500";
            tra_S22.Value = 1500;
            txtS23.Text = "1500";
            tra_S23.Value = 1500;
            txtS24.Text = "1500";
            tra_S24.Value = 1500;
            txtS25.Text = "1500";
            tra_S25.Value = 1500;
            txtS26.Text = "1500";
            tra_S26.Value = 1500;
            txtS27.Text = "1500";
            tra_S27.Value = 1500;
            txtS28.Text = "1500";
            tra_S28.Value = 1500;
            txtS29.Text = "1500";
            tra_S29.Value = 1500;
            txtS30.Text = "1500";
            tra_S30.Value = 1500;
            txtS31.Text = "1500";
            tra_S31.Value = 1500;
            txtS32.Text = "1500";
            tra_S32.Value = 1500;
            txtSAll.Text = "1500";
            track_all_signal.Value = 1500;
            //if (comm.IsOpen) comm.Write("#1P1500#2P1500#3P1500#4P1500#5P1500#6P1500#7P1500#8P1500#9P1500#10P1500#11P1500#12P1500#13P1500#14P1500#15P1500#16P1500#17P1500#18P1500#19P1500#20P1500#21P1500#22P1500#23P1500#24P1500#25P1500#26P1500#27P1500#28P1500#29P1500#30P1500#31P1500#32P1500T100!");
        }

        private void button_Read_AD_Click(object sender, EventArgs e)
        {
            if (comboBox_AD_port.Text != "")
            {
                if (comm.IsOpen) comm.Write("AD" + comboBox_AD_port.Text + "!");//read the reference AD
                Cmd_Read_AD = true;
            }
        }

        private void track_all_signal_Scroll(object sender, EventArgs e)
        {
            txtSAll.Text = Convert.ToString(track_all_signal.Value);
            txtS1.Text = txtSAll.Text;
            tra_S1.Value = track_all_signal.Value;
            txtS2.Text = txtSAll.Text;
            tra_S2.Value = track_all_signal.Value;
            txtS3.Text = txtSAll.Text;
            tra_S3.Value = track_all_signal.Value;
            txtS4.Text = txtSAll.Text;
            tra_S4.Value = track_all_signal.Value;
            txtS5.Text = txtSAll.Text;
            tra_S5.Value = track_all_signal.Value;
            txtS6.Text = txtSAll.Text;
            tra_S6.Value = track_all_signal.Value;
            txtS7.Text = txtSAll.Text;
            tra_S7.Value = track_all_signal.Value;
            txtS8.Text = txtSAll.Text;
            tra_S8.Value = track_all_signal.Value;
            txtS9.Text = txtSAll.Text;
            tra_S9.Value = track_all_signal.Value;
            txtS10.Text = txtSAll.Text;
            tra_S10.Value = track_all_signal.Value;
            txtS11.Text = txtSAll.Text;
            tra_S11.Value = track_all_signal.Value;
            txtS12.Text = txtSAll.Text;
            tra_S12.Value = track_all_signal.Value;
            txtS13.Text = txtSAll.Text;
            tra_S13.Value = track_all_signal.Value;
            txtS14.Text = txtSAll.Text;
            tra_S14.Value = track_all_signal.Value;
            txtS15.Text = txtSAll.Text;
            tra_S15.Value = track_all_signal.Value;
            txtS16.Text = txtSAll.Text;
            tra_S16.Value = track_all_signal.Value;
            txtS17.Text = txtSAll.Text;
            tra_S17.Value = track_all_signal.Value;
            txtS18.Text = txtSAll.Text;
            tra_S18.Value = track_all_signal.Value;
            txtS19.Text = txtSAll.Text;
            tra_S19.Value = track_all_signal.Value;
            txtS20.Text = txtSAll.Text;
            tra_S20.Value = track_all_signal.Value;
            txtS21.Text = txtSAll.Text;
            tra_S21.Value = track_all_signal.Value;
            txtS22.Text = txtSAll.Text;
            tra_S22.Value = track_all_signal.Value;
            txtS23.Text = txtSAll.Text;
            tra_S23.Value = track_all_signal.Value;
            txtS24.Text = txtSAll.Text;
            tra_S24.Value = track_all_signal.Value;
            txtS25.Text = txtSAll.Text;
            tra_S25.Value = track_all_signal.Value;
            txtS26.Text = txtSAll.Text;
            tra_S26.Value = track_all_signal.Value;
            txtS27.Text = txtSAll.Text;
            tra_S27.Value = track_all_signal.Value;
            txtS28.Text = txtSAll.Text;
            tra_S28.Value = track_all_signal.Value;
            txtS29.Text = txtSAll.Text;
            tra_S29.Value = track_all_signal.Value;
            txtS30.Text = txtSAll.Text;
            tra_S30.Value = track_all_signal.Value;
            txtS31.Text = txtSAll.Text;
            tra_S31.Value = track_all_signal.Value;
            txtS32.Text = txtSAll.Text;
            tra_S32.Value = track_all_signal.Value;
            if (comm.IsOpen) comm.Write(listItems(txtS1.Text, "1P") +
                                  listItems(txtS2.Text, "2P") +
                                  listItems(txtS3.Text, "3P") +
                                  listItems(txtS4.Text, "4P") +
                                  listItems(txtS5.Text, "5P") +
                                  listItems(txtS6.Text, "6P") +
                                  listItems(txtS7.Text, "7P") +
                                  listItems(txtS8.Text, "8P") +
                                  listItems(txtS9.Text, "9P") +
                                  listItems(txtS10.Text, "10P") +
                                  listItems(txtS11.Text, "11P") +
                                  listItems(txtS12.Text, "12P") +
                                  listItems(txtS13.Text, "13P") +
                                  listItems(txtS14.Text, "14P") +
                                  listItems(txtS15.Text, "15P") +
                                  listItems(txtS16.Text, "16P") +
                                  listItems(txtS17.Text, "17P") +
                                  listItems(txtS18.Text, "18P") +
                                  listItems(txtS19.Text, "19P") +
                                  listItems(txtS20.Text, "20P") +
                                  listItems(txtS21.Text, "21P") +
                                  listItems(txtS22.Text, "22P") +
                                  listItems(txtS23.Text, "23P") +
                                  listItems(txtS24.Text, "24P") +
                                  listItems(txtS25.Text, "25P") +
                                  listItems(txtS26.Text, "26P") +
                                  listItems(txtS27.Text, "27P") +
                                  listItems(txtS28.Text, "28P") +
                                  listItems(txtS29.Text, "29P") +
                                  listItems(txtS30.Text, "30P") +
                                  listItems(txtS31.Text, "31P") +
                                  listItems(txtS32.Text, "32P") +
                                  "T" + "100" + frameTail);
        }

        private void button_All_GO_Click(object sender, EventArgs e)
        {
            UInt16 i = 0;
            i = Convert.ToUInt16(txtSAll.Text);
            txtS1.Text = txtSAll.Text;
            tra_S1.Value = i;
            txtS2.Text = txtSAll.Text;
            tra_S2.Value = i;
            txtS3.Text = txtSAll.Text;
            tra_S3.Value = i;
            txtS4.Text = txtSAll.Text;
            tra_S4.Value = i;
            txtS5.Text = txtSAll.Text;
            tra_S5.Value = i;
            txtS6.Text = txtSAll.Text;
            tra_S6.Value = i;
            txtS7.Text = txtSAll.Text;
            tra_S7.Value = i;
            txtS8.Text = txtSAll.Text;
            tra_S8.Value = i;
            txtS9.Text = txtSAll.Text;
            tra_S9.Value = i;
            txtS10.Text = txtSAll.Text;
            tra_S10.Value = i;
            txtS11.Text = txtSAll.Text;
            tra_S11.Value = i;
            txtS12.Text = txtSAll.Text;
            tra_S12.Value = i;
            txtS13.Text = txtSAll.Text;
            tra_S13.Value = i;
            txtS14.Text = txtSAll.Text;
            tra_S14.Value = i;
            txtS15.Text = txtSAll.Text;
            tra_S15.Value = i;
            txtS16.Text = txtSAll.Text;
            tra_S16.Value = i;
            txtS17.Text = txtSAll.Text;
            tra_S17.Value = i;
            txtS18.Text = txtSAll.Text;
            tra_S18.Value = i;
            txtS19.Text = txtSAll.Text;
            tra_S19.Value = i;
            txtS20.Text = txtSAll.Text;
            tra_S20.Value = i;
            txtS21.Text = txtSAll.Text;
            tra_S21.Value = i;
            txtS22.Text = txtSAll.Text;
            tra_S22.Value = i;
            txtS23.Text = txtSAll.Text;
            tra_S23.Value = i;
            txtS24.Text = txtSAll.Text;
            tra_S24.Value = i;
            txtS25.Text = txtSAll.Text;
            tra_S25.Value = i;
            txtS26.Text = txtSAll.Text;
            tra_S26.Value = i;
            txtS27.Text = txtSAll.Text;
            tra_S27.Value = i;
            txtS28.Text = txtSAll.Text;
            tra_S28.Value = i;
            txtS29.Text = txtSAll.Text;
            tra_S29.Value = i;
            txtS30.Text = txtSAll.Text;
            tra_S30.Value = i;
            txtS31.Text = txtSAll.Text;
            tra_S31.Value = i;
            txtS32.Text = txtSAll.Text;
            tra_S32.Value = i;
            if (comm.IsOpen) comm.Write(listItems(txtS1.Text, "1P") +
                                  listItems(txtS2.Text, "2P") +
                                  listItems(txtS3.Text, "3P") +
                                  listItems(txtS4.Text, "4P") +
                                  listItems(txtS5.Text, "5P") +
                                  listItems(txtS6.Text, "6P") +
                                  listItems(txtS7.Text, "7P") +
                                  listItems(txtS8.Text, "8P") +
                                  listItems(txtS9.Text, "9P") +
                                  listItems(txtS10.Text, "10P") +
                                  listItems(txtS11.Text, "11P") +
                                  listItems(txtS12.Text, "12P") +
                                  listItems(txtS13.Text, "13P") +
                                  listItems(txtS14.Text, "14P") +
                                  listItems(txtS15.Text, "15P") +
                                  listItems(txtS16.Text, "16P") +
                                  listItems(txtS17.Text, "17P") +
                                  listItems(txtS18.Text, "18P") +
                                  listItems(txtS19.Text, "19P") +
                                  listItems(txtS20.Text, "20P") +
                                  listItems(txtS21.Text, "21P") +
                                  listItems(txtS22.Text, "22P") +
                                  listItems(txtS23.Text, "23P") +
                                  listItems(txtS24.Text, "24P") +
                                  listItems(txtS25.Text, "25P") +
                                  listItems(txtS26.Text, "26P") +
                                  listItems(txtS27.Text, "27P") +
                                  listItems(txtS28.Text, "28P") +
                                  listItems(txtS29.Text, "29P") +
                                  listItems(txtS30.Text, "30P") +
                                  listItems(txtS31.Text, "31P") +
                                  listItems(txtS32.Text, "32P") +
                                  "T" + "100" + frameTail);
        }

        protected bool isNumberic(string message, out int result)
        {
            System.Text.RegularExpressions.Regex rex =
            new System.Text.RegularExpressions.Regex(@"^\d+$");
            result = -1;
            if (rex.IsMatch(message))
            {
                result = int.Parse(message);
                return true;
            }
            else
                return false;

        }

        protected byte ASC_To_Valu(byte asc)
        {	
	        byte valu=0;
	        switch(asc)
	        {
		        case 0x30:valu=0;break;
		        case 0x31:valu=1;break;
		        case 0x32:valu=2;break;
		        case 0x33:valu=3;break;
		        case 0x34:valu=4;break;
		        case 0x35:valu=5;break;
		        case 0x36:valu=6;break;
		        case 0x37:valu=7;break;
		        case 0x38:valu=8;break;
		        case 0x39:valu=9;break;
	        }
	        return valu;
        }

        protected void translate_command(byte[] str)
        {
            UInt16[] servo_value = new UInt16[33];
            byte motor_num = 0;		   // 
            UInt16 motor_jidu = 0;	   // 
            UInt16 motor_time = 0;	   // 
            byte num_now = 0;		   // 
            byte PWM_now = 0;		   // 
            byte time_now = 0;		   // 
            byte flag_num = 0;		   // 
            byte flag_jidu = 0;		   // 
            byte flag_time = 0;		   // 
            UInt16 i = 0;				   // 

            while (str[i] != '!')
            {
                if (flag_num == 1)	 				// 
                {
                    if (str[i] != 'P')				// 
                    {
                        num_now = ASC_To_Valu(str[i]);// 
                        motor_num = (byte)(motor_num * 10 + num_now);
                    }
                    else  						// 
                        flag_num = 0;
                }
                else
                {
                    
                }

                if (flag_jidu == 1)				// 
                {
                    if ((str[i] != 'T') & (str[i] != '#'))
                    {							// 
                        PWM_now = ASC_To_Valu(str[i]);// 
                        motor_jidu = (UInt16)(motor_jidu * 10 + PWM_now);
                    }
                    else  						// 
                    {
                        flag_jidu = 0;
                        if (motor_jidu > 2500)
                            motor_jidu = 2500;
                        if (motor_jidu < 500)
                            motor_jidu = 500;
                        servo_value[motor_num] = motor_jidu;
                        motor_jidu = 0;
                        motor_num = 0;
                    }
                }

                if (flag_time == 1)				// 
                {
                    time_now = ASC_To_Valu(str[i]);// 
                    motor_time = (UInt16)(motor_time * 10 + time_now);
                    servo_value[0] = motor_time;	   	// 
                    //Uart1_PutChar(UartRec[0]);

                    if (str[i + 1] == '!')
                    {
                        txtS1.Text = Convert.ToString(servo_value[1]);
                        tra_S1.Value = servo_value[1];
                        txtS2.Text = Convert.ToString(servo_value[2]);
                        tra_S2.Value = servo_value[2];
                        txtS3.Text = Convert.ToString(servo_value[3]);
                        tra_S3.Value = servo_value[3];
                        txtS4.Text = Convert.ToString(servo_value[4]);
                        tra_S4.Value = servo_value[4];
                        txtS5.Text = Convert.ToString(servo_value[5]);
                        tra_S5.Value = servo_value[5];
                        txtS6.Text = Convert.ToString(servo_value[6]);
                        tra_S6.Value = servo_value[6];
                        txtS7.Text = Convert.ToString(servo_value[7]);
                        tra_S7.Value = servo_value[7];
                        txtS8.Text = Convert.ToString(servo_value[8]);
                        tra_S8.Value = servo_value[8];
                        txtS9.Text = Convert.ToString(servo_value[9]);
                        tra_S9.Value = servo_value[9];
                        txtS10.Text = Convert.ToString(servo_value[10]);
                        tra_S10.Value = servo_value[10];
                        txtS11.Text = Convert.ToString(servo_value[11]);
                        tra_S11.Value = servo_value[11];
                        txtS12.Text = Convert.ToString(servo_value[12]);
                        tra_S12.Value = servo_value[12];
                        txtS13.Text = Convert.ToString(servo_value[13]);
                        tra_S13.Value = servo_value[13];
                        txtS14.Text = Convert.ToString(servo_value[14]);
                        tra_S14.Value = servo_value[14];
                        txtS15.Text = Convert.ToString(servo_value[15]);
                        tra_S15.Value = servo_value[15];
                        txtS16.Text = Convert.ToString(servo_value[16]);
                        tra_S16.Value = servo_value[16];
                        txtS17.Text = Convert.ToString(servo_value[17]);
                        tra_S17.Value = servo_value[17];
                        txtS18.Text = Convert.ToString(servo_value[18]);
                        tra_S18.Value = servo_value[18];
                        txtS19.Text = Convert.ToString(servo_value[19]);
                        tra_S19.Value = servo_value[19];
                        txtS20.Text = Convert.ToString(servo_value[20]);
                        tra_S20.Value = servo_value[20];
                        txtS21.Text = Convert.ToString(servo_value[21]);
                        tra_S21.Value = servo_value[21];
                        txtS22.Text = Convert.ToString(servo_value[22]);
                        tra_S22.Value = servo_value[22];
                        txtS23.Text = Convert.ToString(servo_value[23]);
                        tra_S23.Value = servo_value[23];
                        txtS24.Text = Convert.ToString(servo_value[24]);
                        tra_S24.Value = servo_value[24];
                        txtS25.Text = Convert.ToString(servo_value[25]);
                        tra_S25.Value = servo_value[25];
                        txtS26.Text = Convert.ToString(servo_value[26]);
                        tra_S26.Value = servo_value[26];
                        txtS27.Text = Convert.ToString(servo_value[27]);
                        tra_S27.Value = servo_value[27];
                        txtS28.Text = Convert.ToString(servo_value[28]);
                        tra_S28.Value = servo_value[28];
                        txtS29.Text = Convert.ToString(servo_value[29]);
                        tra_S29.Value = servo_value[29];
                        txtS30.Text = Convert.ToString(servo_value[30]);
                        tra_S30.Value = servo_value[30];
                        txtS31.Text = Convert.ToString(servo_value[31]);
                        tra_S31.Value = servo_value[31];
                        txtS32.Text = Convert.ToString(servo_value[32]);
                        tra_S32.Value = servo_value[32];
                    }
                }

                if (str[i] == '#')
                    flag_num = 1;
                if (str[i] == 'P')
                    flag_jidu = 1;
                if (str[i] == 'T')
                    flag_time = 1;

                i++;
            }
        }

        private void buttonCmd_STANDBY_Click(object sender, EventArgs e)
        {
            Runflag = false;
            RoundRunflag = false;
            if (listBoxSend.Items.Count > 0)
            {
                listBoxSend.Items.Clear();
            }
            listBoxSend.Items.Add(listItems(txtS1.Text, "1P") +
                                  listItems(txtS2.Text, "2P") +
                                  listItems(txtS3.Text, "3P") +
                                  listItems(txtS4.Text, "4P") +
                                  listItems(txtS5.Text, "5P") +
                                  listItems(txtS6.Text, "6P") +
                                  listItems(txtS7.Text, "7P") +
                                  listItems(txtS8.Text, "8P") +
                                  listItems(txtS9.Text, "9P") +
                                  listItems(txtS10.Text, "10P") +
                                  listItems(txtS11.Text, "11P") +
                                  listItems(txtS12.Text, "12P") +
                                  listItems(txtS13.Text, "13P") +
                                  listItems(txtS14.Text, "14P") +
                                  listItems(txtS15.Text, "15P") +
                                  listItems(txtS16.Text, "16P") +
                                  listItems(txtS17.Text, "17P") +
                                  listItems(txtS18.Text, "18P") +
                                  listItems(txtS19.Text, "19P") +
                                  listItems(txtS20.Text, "20P") +
                                  listItems(txtS21.Text, "21P") +
                                  listItems(txtS22.Text, "22P") +
                                  listItems(txtS23.Text, "23P") +
                                  listItems(txtS24.Text, "24P") +
                                  listItems(txtS25.Text, "25P") +
                                  listItems(txtS26.Text, "26P") +
                                  listItems(txtS27.Text, "27P") +
                                  listItems(txtS28.Text, "28P") +
                                  listItems(txtS29.Text, "29P") +
                                  listItems(txtS30.Text, "30P") +
                                  listItems(txtS31.Text, "31P") +
                                  listItems(txtS32.Text, "32P") +
                                  "T" + txtTime.Text + frameTail
                                  );
            if ((listBoxSend.Items.Count > 0) && (comm.IsOpen))
            {
                listBoxSend.SelectedIndex = -1;
                comm.Write("DOWN1!"); Cmd_DOWN = true;
            }
        }

        private void button_exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
