using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Xml;
using Parking.Auxi;

namespace Parking2017_PLC
{
    public partial class FrmMain : Form
    {
        private Dictionary<int, Thread> dic_taskThread;
        private Dictionary<int, WorkFlow> dic_WorkFlows;

        private bool isStart;
        private Log log;
        private int plcRefresh;


        public FrmMain()
        {
            InitializeComponent();

            log = LogFactory.GetLogger("EqpService");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                string warehouse = XMLHelper.GetRootNodeValueByXpath("root", "PlcCount");
                int plcCount = string.IsNullOrEmpty(warehouse) ? 0 : Convert.ToInt32(warehouse);

                string prefresh = XMLHelper.GetRootNodeValueByXpath("root", "PLCRefresh");
                plcRefresh = string.IsNullOrEmpty(prefresh) ? 0 : Convert.ToInt32(prefresh);

                dic_WorkFlows = new Dictionary<int, WorkFlow>();
                dic_taskThread = new Dictionary<int, Thread>();

                log.Info("后台服务尝试启动...");

                if (plcCount < 1)
                {
                    log.Error("PlcCount=" + plcCount + " 配置出错！");
                }
                isStart = true;
                for (int i = 1; i < plcCount + 1; i++)
                {
                    XmlNode node = XMLHelper.GetPlcNodeByTagName("//root//setting", i.ToString(), "PlcIPAddress");
                    if (node != null)
                    {
                        string ipadrs = node.InnerText;  //plc ip地址

                        WorkFlow controller = new WorkFlow(ipadrs, i);
                        dic_WorkFlows.Add(i, controller);
                        //添加 S7 connection_1 连接项
                        XmlNode xnode = XMLHelper.GetPlcNodeByTagName("//root//setting", i.ToString(), "ConnectItem");
                        if (xnode != null)
                        {
                            string items = xnode.InnerText.Trim();
                            string[] array_items = items.Split(';');
                            if (array_items != null && array_items.Length > 4)
                            {
                                controller.S7_Connection_Items = new string[array_items.Length];
                                int te = 0;
                                foreach (string item in array_items)
                                {
                                    controller.S7_Connection_Items[te++] = item.Trim();
                                }
                                log.Info("S7_Connection 连接项");
                                int ik = 1;
                                string msg = "";
                                foreach (string item in controller.S7_Connection_Items)
                                {
                                    msg += (ik++).ToString() + "、" + item + Environment.NewLine;
                                }
                                log.Info(msg);
                            }
                        }
                    }
                }
                for (int i = 1; i < plcCount + 1; i++)
                {
                    //启动作业线程
                    Thread taskThread = new Thread(new ParameterizedThreadStart(DealMessage));
                    taskThread.Start(i);
                    dic_taskThread.Add(i, taskThread);
                }

                log.Info("后台服务启动中...");

                label1.Text = "服务启动中...";

                MessageBox.Show("success");
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        private void DealMessage(object wh)
        {
            int warehouse = Convert.ToInt32(wh);
            if (!dic_WorkFlows.ContainsKey(warehouse))
            {
                log.Error("dic_WorkFlows没有包含key-" + warehouse + " 系统无法启动！");
                return;
            }
            WorkFlow controller = dic_WorkFlows[warehouse];
            try
            {
                controller.ConnectPLC();
            }
            catch (Exception ex)
            {
                log.Error("连接PLC异常，无法打开连接！系统无法启动！" + ex.ToString());
                //return;
            }
            while (isStart)
            {
                try
                {
                    controller.DealFaultAlarmAndStatusWord();
                    controller.TaskAssign();
                    controller.ReceiveMessage();
                    controller.SendMessage();                    
                    Thread.Sleep(plcRefresh);
                }
                catch (Exception ec)
                {
                    log.Error("处理业务异常-" + ec.ToString());
                    Thread.Sleep(15000);
                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                log.Info("后台服务尝试停止...");

                isStart = false;
                foreach (KeyValuePair<int, WorkFlow> pair in dic_WorkFlows)
                {
                    WorkFlow controller = pair.Value;
                    controller.DisConnect();
                }

                foreach (KeyValuePair<int, Thread> pair in dic_taskThread)
                {
                    Thread mainThread = pair.Value;
                    mainThread.Abort();
                }

                dic_WorkFlows.Clear();

                dic_taskThread.Clear();               
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }

            label1.Text = "服务停止...";
        }
    }
}
