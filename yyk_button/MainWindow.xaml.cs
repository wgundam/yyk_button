using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Ports;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Data;
using System.Configuration;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows.Threading;

namespace yyk_button
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            dataGrid.DataContext = memberData;
            SuperGrid.DataContext = SuperPrizeData;
            Init_Com();
            ShowDB();
        }

        //存储数据库信息，显示在DataGrid
        ObservableCollection<Member> memberData = new ObservableCollection<Member>();
        //超级大奖信息
        ObservableCollection<SuperPrize> SuperPrizeData = new ObservableCollection<SuperPrize>();
            
        //每2S刷新一次数据库
        private DispatcherTimer ShowDBTimer;
        public void ShowDB()
        {
            ShowDBTimer = new System.Windows.Threading.DispatcherTimer();
            ShowDBTimer.Tick += new EventHandler(DB_Check);
            ShowDBTimer.Interval = new TimeSpan(0, 0, 5);
            ShowDBTimer.Start();

        }

        //读取配置文件，调整COM口和数据口地址
        string COM = ConfigurationSettings.AppSettings["COM"];
        string Column = ConfigurationSettings.AppSettings["COLUMN"];
        
        SerialPort Port1 = new SerialPort();
        int iRate = 2400;
        byte bSize = 8;
        public int iTimeout = 1000;
        Thread _readThread;
        bool _keepReading;
        bool DBProcessed = false;
       
        //串口自动开启

        public void Init_Com()
        {
            if (Column == "column_id=5")
            {
            Channel.Text = "社会传真摇摇看启动器";
            }
            else if (Column == "column_id=6")
            {
            Channel.Text = "新闻夜班车摇摇看启动器";
            }

            Parity myParity = Parity.None;
            StopBits MyStopBits = StopBits.One;

            Port1.PortName = COM;
            Port1.BaudRate = iRate;
            Port1.DataBits = bSize;
            Port1.Parity = myParity;
            Port1.StopBits = MyStopBits;
            Port1.ReadTimeout = iTimeout;
            Port1.Open();
            _keepReading = true;
            _readThread = new Thread(ReadPort);
            _readThread.Start();
            Init_Button.Content = "关闭串口";
            textBox1.Text = "串口已开启";
        }

        //串口按钮
        private void Init_Click(object sender, EventArgs e)
        {
            if(Port1.IsOpen)
            {
                Port1.Close();
                _keepReading = false;
                _readThread.Abort();
                Init_Button.Content = "开启串口";
                textBox1.Text = "串口已关闭";
            }
            else
            {
                Parity myParity = Parity.None;
                StopBits MyStopBits = StopBits.One;

                Port1.PortName = COM;
                Port1.BaudRate = iRate;
                Port1.DataBits = bSize;
                Port1.Parity = myParity;
                Port1.StopBits = MyStopBits;
                Port1.ReadTimeout = iTimeout;
                Port1.Open();
                _keepReading = true;
                _readThread = new Thread(ReadPort);
                _readThread.Start();
                Init_Button.Content = "关闭串口";
                textBox1.Text = "串口已开启";
            }


        }

        public delegate void delegate1();
      
        public void SetText ()
        {
            textBox2.Text = "摇摇看开启成功！";
            if (DBProcessed == false)
            {
                DB_Process();
                DBProcessed = true;     
            }

        }
        public void SetText1()
        {
            DBProcessed = false;
            textBox2.Text = "摇摇看等待开启.....";
        }
        public void SetText2()
        {
            textBox2.Text = "数据库连接失败！";
        }

        //串口读取，判断是否开启程序
        public void ReadPort()
        {
            while (_keepReading)
            {
                if (Port1.IsOpen) 
                {
                    byte[] readBuffer = new byte[Port1.ReadBufferSize + 1];
                    try
                    {
                        
                        int count = Port1.Read(readBuffer, 0, Port1.ReadBufferSize);
                        string SerialIn = string.Join("", readBuffer.Select(t => t.ToString()).ToArray());
                        if (SerialIn.Contains("0"))
                        {
                            if (SerialIn.Contains("255"))
                            {
                                Dispatcher.BeginInvoke(new delegate1(SetText));
                            }
                            else
                                Dispatcher.BeginInvoke(new delegate1(SetText1));
                        }
                    }
                    catch (Exception) { }
                }
                else
                {
                    TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 50);
                    Thread.Sleep(waitTime);
                }

            }
        }

        //当关闭程序时，退出所有环境
        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }


        //数据库改位操作
        public void DB_Process()
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();

                string sql = "select shake_coin_id from shake_coin_info where date=curdate() and stop_flag=0 and " + Column;
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                MySqlDataAdapter da = new MySqlDataAdapter(Cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    string update = "update shake_coin_info set stop_flag = 0 where " + Column + " and date=curdate() limit 1";
                    MySqlCommand Cmd1 = new MySqlCommand(update, Conn);
                    Cmd1.ExecuteNonQuery();
                }
                else
                {
                    string update = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id > " + dt.Rows[0][0].ToString() + " limit 1";
                    string update1 = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + dt.Rows[0][0];
                    MySqlCommand Cmd1 = new MySqlCommand(update, Conn);
                    Cmd1.ExecuteNonQuery();
                    MySqlCommand Cmd2 = new MySqlCommand(update1, Conn);
                    Cmd2.ExecuteNonQuery();
                }
                Conn.Close();
            }
            catch(Exception)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }

        //数据库读取
        public void DB_Check(object sender, EventArgs e)
        {
            memberData.Clear();
            SuperPrizeData.Clear();
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();

                string sql = "select shake_coin_id, date, coin_no, code, stop_flag from shake_coin_info where date=curdate() and " + Column + " order by shake_coin_id";
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                MySqlDataAdapter da = new MySqlDataAdapter(Cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                string status;
                string date;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (dt.Rows[i][4].ToString() == "0")
                    {
                        status = "成功开始";
                    }
                    else
                    {
                        status = "等待开启";
                    }
                    if (dt.Rows[i][1].ToString().Substring(6, 1) != "/")
                        date = dt.Rows[i][1].ToString().Substring(0, 10);
                    else
                        date = dt.Rows[i][1].ToString().Substring(0, 9);
                    memberData.Add(new Member()
                    {
                        ID = dt.Rows[i][0].ToString(),
                        Date = date,
                        Number = dt.Rows[i][2].ToString(),
                        Code = dt.Rows[i][3].ToString(),
                        Status = status,
                    });
                }
                // 查询超级大奖User_ID号
                string SuperPrice = "select user_id from yyk_shake_super_get_record where get_date>curdate()";
                MySqlCommand SuperCmd = new MySqlCommand(SuperPrice, Conn);
                MySqlDataAdapter SuperData = new MySqlDataAdapter(SuperCmd);
                DataTable SuperDt = new DataTable();
                SuperData.Fill(SuperDt);
                string UserID;
                //string 
                // 每一超级大奖得主的具体信息
                for (int i = 0; i < SuperDt.Rows.Count;i++)
                {
                    UserID = SuperDt.Rows[i][0].ToString();
                    //是当前栏目，刷新超级大奖得主
                        string Info = "select nike_name, real_name, phone_no, idcard_no from register_user_info where user_id = " + UserID;
                        MySqlCommand InfoCmd = new MySqlCommand(Info, Conn);
                        MySqlDataAdapter InfoData = new MySqlDataAdapter(InfoCmd);
                        DataTable InfoDt = new DataTable();
                        InfoData.Fill(InfoDt);
                        SuperPrizeData.Add(new SuperPrize()
                        {
                            UserID = UserID,
                            NickName = InfoDt.Rows[0][0].ToString(),
                            RealName = InfoDt.Rows[0][1].ToString(),
                            Phone = InfoDt.Rows[0][2].ToString(),
                            IDCard = InfoDt.Rows[0][3].ToString(),
                        });
                    }
                
                    Conn.Close();
            }
            catch(Exception)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }
        }
        
        

    // 摇摇看状态信息
    public class Member
    {
        public string ID { get; set; }
        public string Date { get; set; }
        public string Number { get; set; }
        public string Code { get; set; }
        public string Status { get; set; }

    }

    // 超级大奖信息
    public class SuperPrize
    {
        public string UserID { get; set; }
        public string RealName { get; set; }
        public string NickName { get; set;}
        public string Phone { get; set;}
        public string IDCard { get; set; }
    }
        
    }

