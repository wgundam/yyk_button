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
            ShowDBTimer.Interval = new TimeSpan(0, 0, 2);
            ShowDBTimer.Start();

        }

        //读取配置文件，调整COM口和数据口地址
        string COM = ConfigurationSettings.AppSettings["COM"];
        string Column = ConfigurationSettings.AppSettings["COLUMN"];
        string Rate = ConfigurationSettings.AppSettings["RATE"];
        
        SerialPort Port1 = new SerialPort();
        //int iRate = 2400;
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
            Port1.BaudRate = Convert.ToInt32(Rate);
            Port1.DataBits = bSize;
            Port1.Parity = myParity;
            Port1.StopBits = MyStopBits;
            Port1.ReadTimeout = iTimeout;
            Port1.Open();
            _keepReading = true;
            _readThread = new Thread(ReadPort);
            _readThread.Start();
            Init_Button.Content = "关闭按钮";
            textBox1.Text = "按钮已开启";
        }

        //串口按钮
        private void Init_Click(object sender, EventArgs e)
        {
            if(Port1.IsOpen)
            {
                Port1.Close();
                _keepReading = false;
                _readThread.Abort();
                Init_Button.Content = "开启按钮";
                textBox1.Text = "按钮已关闭";
            }
            else
            {
                Parity myParity = Parity.None;
                StopBits MyStopBits = StopBits.One;

                Port1.PortName = COM;
                Port1.BaudRate = Convert.ToInt32(Rate);
                Port1.DataBits = bSize;
                Port1.Parity = myParity;
                Port1.StopBits = MyStopBits;
                Port1.ReadTimeout = iTimeout;
                Port1.Open();
                _keepReading = true;
                _readThread = new Thread(ReadPort);
                _readThread.Start();
                Init_Button.Content = "关闭按钮";
                textBox1.Text = "按钮已开启";
            }


        }


        // 使用代理更改显示文字
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

        public void SetText3()
        {
            textBox2.Text = "栏目尚未开始！";
        }

        public void SetText4()
        {
            DBProcessed = false;
            textBox2.Text = "按钮失去连接.....";
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
                    Dispatcher.BeginInvoke(new delegate1(SetText4));
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

                // 获取栏目时间
                string Time = "select start_time from column_info where " + Column;
                MySqlCommand TimeCmd = new MySqlCommand(Time, Conn);
                MySqlDataAdapter Timeda = new MySqlDataAdapter(TimeCmd);
                DataTable Timedt = new DataTable();
                Timeda.Fill(Timedt);

                //比较当前时间和栏目时间，当前时间大于栏目时间，数据库才可以操作。
                DateTime ChannelTime = Convert.ToDateTime(Timedt.Rows[0][0].ToString());
                DateTime Now = Convert.ToDateTime(DateTime.Now.ToShortTimeString().ToString());

                if (DateTime.Compare(Now, ChannelTime) > 0)
                {

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
                }
                else
                {
                    Dispatcher.BeginInvoke(new delegate1(SetText3));
                }
                Conn.Close();
            }
            catch(Exception)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }

        //用于记录7个按钮所需信息，分别记录每个活动的ID，每个活动当前状态。
        string [,] ShakeDetail = new string[2,7];
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

                //为7个按钮赋值 
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    ShakeDetail[0, i] = dt.Rows[i][0].ToString();
                    ShakeDetail[1, i] = dt.Rows[i][4].ToString();
                }
                for (int i = dt.Rows.Count; i < 7; i++)
                {
                    ShakeDetail[0, i] = null;
                    ShakeDetail[1, i] = null;
                }

                //根据活动数目，显示按钮
                ShowButton(dt.Rows.Count);

                string status;
                string date;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (dt.Rows[i][4].ToString() == "0")
                    {
                        status = "成功开始";
                        ShowRec(i+1);
                    }
                    else
                    {
                        status = "等待开启";
                        HidRec(i + 1);
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

        //按钮开启，关闭
        private void ShakeStartOne(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 0] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 0];
                }
                else if (ShakeDetail[1, 0] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 0];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }
        private void ShakeStartTwo(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 1] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 1];
                }
                else if (ShakeDetail[1, 1] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 1];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }

        }
        private void ShakeStartThree(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 2] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 2];
                }
                else if (ShakeDetail[1, 2] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 2];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }
        private void ShakeStartFour(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 3] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 3];
                }
                else if (ShakeDetail[1, 3] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 3];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }
        private void ShakeStartFive(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 4] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 4];
                }
                else if (ShakeDetail[1, 4] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 4];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }            
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }

        }
        private void ShakeStartSix(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 5] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 5];
                }
                else if (ShakeDetail[1, 5] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 5];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }

        }
        private void ShakeStartSeven(object sender, EventArgs e)
        {
            try
            {
                string DB = ConfigurationSettings.AppSettings["DB"];
                string sql = "";
                MySqlConnection Conn = new MySqlConnection(DB);
                Conn.Open();
                //先判断当前活动状态
                if (ShakeDetail[1, 6] == "0")
                {
                    sql = "update shake_coin_info set stop_flag = 1 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 6];
                }
                else if (ShakeDetail[1, 6] == "1")
                {
                    sql = "update shake_coin_info set stop_flag = 0 where " + Column + " and date = curdate() and shake_coin_id = " + ShakeDetail[0, 6];
                }
                else
                {
                    return;
                }
                MySqlCommand Cmd = new MySqlCommand(sql, Conn);
                Cmd.ExecuteNonQuery();
            }            
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new delegate1(SetText2));
            }
        }

        //根据活动次数显示按钮
        private void ShowButton(int number)
        {
            if (number == 1)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
            }
            if (number == 2)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
            }
            if (number == 3)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeThreeButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
                ShakeThreeButton.Content = ButtonContentChange(ShakeDetail[1, 2]);
            }
            if (number == 4)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeThreeButton.Visibility = Visibility.Visible;
                ShakeFourButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
                ShakeThreeButton.Content = ButtonContentChange(ShakeDetail[1, 2]);
                ShakeFourButton.Content = ButtonContentChange(ShakeDetail[1, 3]);
            }
            if (number == 5)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeThreeButton.Visibility = Visibility.Visible;
                ShakeFourButton.Visibility = Visibility.Visible;
                ShakeFiveButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
                ShakeThreeButton.Content = ButtonContentChange(ShakeDetail[1, 2]);
                ShakeFourButton.Content = ButtonContentChange(ShakeDetail[1, 3]);
                ShakeFiveButton.Content = ButtonContentChange(ShakeDetail[1, 4]);

            }
            if (number == 6)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeThreeButton.Visibility = Visibility.Visible;
                ShakeFourButton.Visibility = Visibility.Visible;
                ShakeFiveButton.Visibility = Visibility.Visible;
                ShakeSixButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
                ShakeThreeButton.Content = ButtonContentChange(ShakeDetail[1, 2]);
                ShakeFourButton.Content = ButtonContentChange(ShakeDetail[1, 3]);
                ShakeFiveButton.Content = ButtonContentChange(ShakeDetail[1, 4]);
                ShakeSixButton.Content = ButtonContentChange(ShakeDetail[1, 5]);

            }
            if (number == 7)
            {
                ShakeOneButton.Visibility = Visibility.Visible;
                ShakeTwoButton.Visibility = Visibility.Visible;
                ShakeThreeButton.Visibility = Visibility.Visible;
                ShakeFourButton.Visibility = Visibility.Visible;
                ShakeFiveButton.Visibility = Visibility.Visible;
                ShakeSixButton.Visibility = Visibility.Visible;
                ShakeSevenButton.Visibility = Visibility.Visible;
                ShakeOneButton.Content = ButtonContentChange(ShakeDetail[1, 0]);
                ShakeTwoButton.Content = ButtonContentChange(ShakeDetail[1, 1]);
                ShakeThreeButton.Content = ButtonContentChange(ShakeDetail[1, 2]);
                ShakeFourButton.Content = ButtonContentChange(ShakeDetail[1, 3]);
                ShakeFiveButton.Content = ButtonContentChange(ShakeDetail[1, 4]);
                ShakeSixButton.Content = ButtonContentChange(ShakeDetail[1, 5]);
                ShakeSevenButton.Content = ButtonContentChange(ShakeDetail[1, 6]);
            }


        }
        //更改按钮上的显示内容
        public string ButtonContentChange(string status)
        {
            if (status == "0")
            {
                return "暂停";
            }
            else
            {
                return "开始";
            }
        }

        //已开启的活动背景加颜色
        private void ShowRec(int number)
        { 
            if (number == 1)
            {
                RecOne.Visibility = Visibility.Visible;
            }
            else if (number == 2)
            {
                RecTwo.Visibility = Visibility.Visible;
            }
            else if (number == 3)
            {
                RecThree.Visibility = Visibility.Visible;
            }
            else if (number == 4)
            {
                RecFour.Visibility = Visibility.Visible;
            }
            else if (number == 5)
            {
                RecFive.Visibility = Visibility.Visible;
            }
            else if (number == 6)
            {
                RecSix.Visibility = Visibility.Visible;
            }
            else if (number == 7)
            {
                RecSeven.Visibility = Visibility.Visible;
            }
            else
                return;
        }

        //未开启的活动不显示颜色
        private void HidRec(int number)
        {
            if (number == 1)
            {
                RecOne.Visibility = Visibility.Hidden;
            }
            else if (number == 2)
            {
                RecTwo.Visibility = Visibility.Hidden;
            }
            else if (number == 3)
            {
                RecThree.Visibility = Visibility.Hidden;
            }
            else if (number == 4)
            {
                RecFour.Visibility = Visibility.Hidden;
            }
            else if (number == 5)
            {
                RecFive.Visibility = Visibility.Hidden;
            }
            else if (number == 6)
            {
                RecSix.Visibility = Visibility.Hidden;
            }
            else if (number == 7)
            {
                RecSeven.Visibility = Visibility.Hidden;
            }
            else
                return;
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

