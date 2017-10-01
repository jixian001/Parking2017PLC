using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parking.Auxi;

namespace Parking2017_PLC

{
    public class WorkFlow
    {
        private int warehouse;
        private string ipAddrs;
        private S7NetPlus plcAccess;
        private string[] s7_Connection_Items = null;

        private Int16 messageID = 0;

        private int traceOff = 135;
        private int isAvailHall = 297;
        private int isAvailTV = 297;

        private static int totalSpace = 0;
        private static int tempSpace = 0;

        private WorkFlow()
        {
            #region
            try
            {
                string trace = XMLHelper.GetRootNodeValueByXpath("root", "TraceOut");
                if (!string.IsNullOrEmpty(trace))
                {
                    int.TryParse(trace, out traceOff);
                }
                string hallavail = XMLHelper.GetRootNodeValueByXpath("root", "IsAvailHall");
                if (!string.IsNullOrEmpty(hallavail))
                {
                    int.TryParse(hallavail, out isAvailHall);
                }
                string tvavail = XMLHelper.GetRootNodeValueByXpath("root", "IsAvailTV");
                if (!string.IsNullOrEmpty(tvavail))
                {
                    int.TryParse(tvavail, out isAvailTV);
                }
            }
            catch
            { }
            #endregion
        }

        public WorkFlow(string ipaddrs, int wh)
            : this()
        {
            ipAddrs = ipaddrs;
            warehouse = wh;
            plcAccess = new S7NetPlus(ipaddrs);

            messageID = (short)(new Random()).Next(1, 4000);

        }

        public string[] S7_Connection_Items
        {
            get { return s7_Connection_Items; }
            set { s7_Connection_Items = value; }
        }

        public bool ConnectPLC()
        {
            if (plcAccess != null)
            {
                return plcAccess.ConnectPLC();
            }
            return false;
        }

        public void DisConnect()
        {
            if (plcAccess != null)
            {
                plcAccess.DisConnectPLC();
            }
        }

        /// <summary>
        /// 队列下发
        /// </summary>
        public void TaskAssign()
        {
            Log log = LogFactory.GetLogger("WorkFlow.TaskAssign");
            try
            {
                CProxy.MyProxy.ReleaseDeviceTaskIDButNoTask(warehouse);

                List<WorkTask> queueList = CProxy.MyProxy.FindQueueList(warehouse);
                if (queueList == null || queueList.Count == 0)
                {
                    CProxy.MyProxy.DealTempLocOccupy(warehouse);
                    return;
                }
                //优先发送是报文(子作业)的队列
                List<WorkTask> lstWaitTelegram = queueList.FindAll(ls => ls.IsMaster == 1);
                #region 优先发送是避让的队列
                List<WorkTask> avoidTelegram = lstWaitTelegram.FindAll(ls => ls.MasterType == EnmTaskType.Avoid);
                for (int i = 0; i < avoidTelegram.Count; i++)
                {
                    WorkTask queue = avoidTelegram[i];
                    Device dev = CProxy.MyProxy.FindDevice(queue.Warehouse, queue.DeviceCode);
                    if (dev == null)
                    {
                        log.Error("避让队列，找不到执行的设备-" + queue.DeviceCode + " 库区-" + queue.Warehouse);
                        continue;
                    }
                    if (dev.Type != EnmSMGType.ETV)
                    {
                        log.Error("避让队列，但执行的设备-" + queue.DeviceCode + " 不是TV");
                        continue;
                    }
                    //如果TV空闲可用，则允许下发
                    if (dev.IsAble == 1 && dev.IsAvailabe == 1)
                    {
                        if (dev.TaskID == 0)
                        {
                            //当前TV没有作业，则绑定设备,执行避让
                            CProxy.MyProxy.CreateAvoidTaskByQueue(queue.ID);
                        }
                        else
                        {
                            //当前作业不为空，查询当前作业状态
                            //处于等待卸载时，也允许下发避让
                            ImplementTask itask = CProxy.MyProxy.FindITask(dev.TaskID);
                            if (itask != null)
                            {
                                if (itask.IsComplete == 0 && itask.Status == EnmTaskStatus.WillWaitForUnload)
                                {
                                    //允许避让
                                    CProxy.MyProxy.CreateAvoidTaskByQueue(queue.ID);
                                }
                            }
                            else
                            {
                                log.Info("当前避让队列，对应的设备-" + dev.DeviceCode + "  TaskID-" + dev.TaskID + " 找不到对应的执行队列！");
                            }
                        }
                    }
                }
                #endregion              
                #region 处理其他报文               
                for (int i = 0; i < lstWaitTelegram.Count; i++)
                {
                    WorkTask queue = lstWaitTelegram[i];
                    Device dev = CProxy.MyProxy.FindDevice(queue.Warehouse, queue.DeviceCode);
                    if (dev == null)
                    {
                        log.Error("执行队列时，找不到执行的设备-" + queue.DeviceCode + " 库区-" + queue.Warehouse);
                        continue;
                    }
                    if (dev.IsAble == 1 && dev.IsAvailabe == 1)
                    {
                        if (dev.Type == EnmSMGType.Hall)
                        {
                            if (dev.TaskID == 0)
                            {
                                CProxy.MyProxy.CreateDeviceTaskByQueue(queue.ID, dev.Warehouse, dev.DeviceCode);
                            }
                        }
                        else if (dev.Type == EnmSMGType.ETV)
                        {
                            if (dev.TaskID == 0)
                            {
                                if (CProxy.MyProxy.DealAvoid(queue.ID, dev.Warehouse, dev.DeviceCode))
                                {
                                    CProxy.MyProxy.CreateDeviceTaskByQueue(queue.ID, dev.Warehouse, dev.DeviceCode);
                                }
                            }
                            else //处理卸载指令
                            {
                                ImplementTask itask = CProxy.MyProxy.FindITask(dev.TaskID);
                                if (itask != null)
                                {
                                    //下发卸载指令,主作业一致的
                                    if (itask.Status == EnmTaskStatus.WillWaitForUnload)
                                    {
                                        if (itask.ICCardCode == queue.ICCardCode &&
                                            itask.Type == queue.MasterType &&
                                            queue.TelegramType == 14 &&
                                            queue.SubTelegramType == 1)
                                        {
                                            if (CProxy.MyProxy.DealAvoid(queue.ID, dev.Warehouse, dev.DeviceCode))
                                            {
                                                CProxy.MyProxy.DealTVUnloadTask(itask.ID, queue.ID);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion
                #region 处理取车队列
                List<WorkTask> getCarQueueList = queueList.FindAll(ls => ls.IsMaster == 2);
                for (int i = 0; i < getCarQueueList.Count; i++)
                {
                    WorkTask queue = getCarQueueList[i];
                    Device hall = CProxy.MyProxy.FindDevice(queue.Warehouse, queue.DeviceCode);
                    if (hall == null)
                    {
                        log.Error("执行取车队列时，找不到车厅-" + queue.DeviceCode + " 库区-" + queue.Warehouse);
                        continue;
                    }
                    Location lctn = CProxy.MyProxy.FindLocation(queue.ICCardCode);
                    if (lctn == null)
                    {
                        log.Error("执行取车队列时，找不到存车车位，删除队列，iccode-" + queue.ICCardCode);
                        CProxy.MyProxy.DeleteQueue(queue.ID);
                        continue;
                    }
                    if (hall.TaskID == 0)
                    {
                        //车厅没有作业
                        if (hall.IsAble == 1 && hall.IsAvailabe == 1)
                        {
                            //发送车厅报文，同时查看TV状态，如果OK,则下发TV报文
                            CProxy.MyProxy.SendHallTelegramAndBuildTV(queue.ID, hall.Warehouse, lctn.Address, hall.DeviceCode);
                        }
                    }
                    else
                    {
                        //是否要进行提前装载
                        ImplementTask hallTask = CProxy.MyProxy.FindITask(hall.TaskID);
                        if (hallTask == null)
                        {
                            log.Error("依TaskID-" + hall.TaskID + " 找不到对应的作业！");
                            continue;
                        }
                        if (hallTask.Type == EnmTaskType.GetCar)
                        {
                            if (hallTask.Status == EnmTaskStatus.OWaitforEVUp ||
                                hallTask.Status == EnmTaskStatus.OCarOutWaitforDriveaway ||
                                hallTask.Status == EnmTaskStatus.OHallFinishing)
                            {
                                //保证只有一个作业提前下发
                                //防止不同巷道的取车同时下发
                                WorkTask hallWillCommit = queueList.Find(tsk => tsk.DeviceCode == hall.DeviceCode &&
                                                                                    tsk.IsMaster == 1 &&
                                                                                    tsk.MasterType == EnmTaskType.GetCar);
                                if (hallWillCommit != null)
                                {
                                    continue;
                                }
                                CProxy.MyProxy.AheadTvTelegramAndBuildHall(queue.ID, hall.Warehouse, lctn.Address, hall.DeviceCode);
                            }
                        }
                    }
                }
                #endregion

                #region 查看，是否需要更改取车车厅
                CProxy.MyProxy.MaintainWorkQueue(warehouse);
                #endregion
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 报文发送
        /// </summary>
        public void SendMessage()
        {
            Log log = LogFactory.GetLogger("WorkFlow.SendMessage");
            try
            {
                List<ImplementTask> taskLst = CProxy.MyProxy.FindITaskLst(warehouse);
                if (taskLst == null || taskLst.Count == 0)
                {
                    return;
                }
                for (int i = 0; i < taskLst.Count; i++)
                {
                    ImplementTask task = taskLst[i];
                    Device smg = CProxy.MyProxy.FindDevice(task.Warehouse, task.DeviceCode);
                    if (smg == null)
                    {
                        log.Error("当前执行作业,绑定的设备号-" + task.DeviceCode + "  库区-" + task.Warehouse + " 不是系统");
                        continue;
                    }
                    if (smg.Type == EnmSMGType.Hall)
                    {
                        #region 车厅
                        if (smg.IsAble == 1 && smg.TaskID == task.ID &&
                            (task.SendStatusDetail == EnmTaskStatusDetail.NoSend ||
                            (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk && DateTime.Compare(DateTime.Now, task.SendDtime.AddSeconds(12)) > 0)))
                        {
                            #region 存车
                            if (task.Status == EnmTaskStatus.ISecondSwipedWaitforCheckSize)
                            {
                                bool nback = this.sendData(this.packageMessage(1, 9, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.ISecondSwipedWaitforEVDown)
                            {
                                bool nback = this.sendData(this.packageMessage(1, 1, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.IEVDownFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(1, 54, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.IHallFinishing) //异常退出
                            {
                                bool nback = this.sendData(this.packageMessage(1, 55, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.ISecondSwipedWaitforCarLeave) //找不到合适车位
                            {
                                bool nback = this.sendData(this.packageMessage(1, 2, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.ICheckCarFail) //检测失败
                            {

                            }
                            #endregion
                            #region 取车
                            else if (task.Status == EnmTaskStatus.OWaitforEVDown)
                            {
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(3, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                                else
                                {
                                    log.Info("取车时，车厅不可接收新指令。iccard-" + task.ICCardCode + "  hallID-" + smg.DeviceCode + " address-" + task.FromLctAddress);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.OEVDownFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(3, 54, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.OCarOutWaitforDriveaway)
                            {
                                bool nback = this.sendData(this.packageMessage(3, 2, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.OHallFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(3, 55, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            #endregion
                            #region 取物
                            else if (task.Status == EnmTaskStatus.TempWaitforEVDown)
                            {
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(2, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                                else
                                {
                                    log.Info("取物时，车厅不可接收新指令. iccard-" + task.ICCardCode + "  hallID-" + smg.DeviceCode + " address-" + task.FromLctAddress);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.TempEVDownFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(2, 54, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.TempOCarOutWaitforDrive)
                            {
                                bool nback = this.sendData(this.packageMessage(2, 2, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.TempHallFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(2, 55, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            #endregion
                        }
                        #endregion
                    }
                    else if (smg.Type == EnmSMGType.ETV)
                    {
                        #region ETV
                        if (smg.IsAble == 1 && smg.TaskID == task.ID &&
                            (task.SendStatusDetail == EnmTaskStatusDetail.NoSend ||
                            (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk && task.SendDtime.AddSeconds(10) < DateTime.Now)))
                        {
                            #region 装载
                            if (task.Status == EnmTaskStatus.TWaitforLoad)
                            {
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(13, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        task.SendStatusDetail = EnmTaskStatusDetail.SendWaitAsk;
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                                else
                                {
                                    log.Info("装载时，TV不可接收新指令。作业类型：" + task.Type.ToString() + "  iccard-" +
                                                                            task.ICCardCode + "  hallID-" + smg.DeviceCode + " FromAddress-" +
                                                                            task.FromLctAddress + " ToAddress-" + task.ToLctAddress);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.LoadFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(13, 51, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    //同时生成（14，1）加入队列中
                                    CProxy.MyProxy.UnpackUnloadOrder(task.ID);
                                }
                            }
                            #endregion
                            #region 卸载
                            else if (task.Status == EnmTaskStatus.TWaitforUnload)
                            {
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(14, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        task.SendStatusDetail = EnmTaskStatusDetail.SendWaitAsk;
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                                else
                                {
                                    log.Info("卸载时，TV不可接收新指令。作业类型：" + task.Type.ToString() + "  iccard-" +
                                                                            task.ICCardCode + "  hallID-" + smg.DeviceCode + " FromAddress-" +
                                                                            task.FromLctAddress + " ToAddress-" + task.ToLctAddress);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.UnLoadFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(14, 51, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            #endregion
                            #region 移动
                            else if (task.Status == EnmTaskStatus.TWaitforMove)
                            {
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(11, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                                else
                                {
                                    log.Info("移动时，TV不可接收新指令.作业类型-" + task.Type);
                                }
                            }
                            else if (task.Status == EnmTaskStatus.MoveFinishing)
                            {
                                bool nback = this.sendData(this.packageMessage(11, 51, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            #endregion
                            #region 故障恢复
                            else if (task.Status == EnmTaskStatus.TMUROWaitforLoad)
                            {
                                //发送装载指令
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(13, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                            }
                            else if (task.Status == EnmTaskStatus.TMUROWaitforUnload)
                            {
                                //发送卸载指令
                                if (smg.IsAvailabe == 1)
                                {
                                    bool nback = this.sendData(this.packageMessage(14, 1, smg.DeviceCode, task));
                                    if (nback)
                                    {
                                        CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    }
                                }
                            }
                            #endregion
                            #region 车厅装载，复检不通过
                            else if (task.Status == EnmTaskStatus.ReCheckInLoad)
                            {
                                bool nback = this.sendData(this.packageMessage(43, 51, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                    //同时生成（14，1）加入队列中
                                    CProxy.MyProxy.UnpackUnloadOrder(task.ID);
                                }
                            }
                            #endregion
                            #region 删除指令
                            else if (task.Status == EnmTaskStatus.WaitforDeleteTask)
                            {
                                bool nback = this.sendData(this.packageMessage(23, 1, smg.DeviceCode, task));
                                if (nback)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.SendWaitAsk);
                                }
                            }
                            #endregion
                        }
                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 报文接收
        /// </summary>
        public void ReceiveMessage()
        {
            Log log = LogFactory.GetLogger("WorkFlow.ReceiveMessage");
            try
            {
                bool hasdata = false;
                Int16[] data;
                unpackageMessage(out data, out hasdata);
                if (!hasdata)
                {
                    return;
                }
                if (data[1] == 0)
                {
                    data[1] = 1;
                }
                int warehouse = data[1];
                int code = data[6];
                Device smg = CProxy.MyProxy.FindDevice(warehouse, code);
                if (smg == null)
                {
                    log.Error("无效报文，找不到相关设备. deviceCode-" + data[6] + " warehouse-" + data[1]);
                    return;
                }
                ImplementTask task = CProxy.MyProxy.FindITaskBySmg(warehouse, code);
                if (task != null)
                {
                    if (smg.Type == EnmSMGType.Hall)
                    {
                        #region
                        #region 存车
                        if (task.Status == EnmTaskStatus.ICarInWaitFirstSwipeCard)
                        {
                            if (data[2] == 1001 && data[4] == 1)
                            {
                                //仅增加语音用
                                CProxy.MyProxy.AddSoundNotifi(smg.Warehouse, smg.DeviceCode, "18.wav");

                                //增加ETV移动至车厅作业
                                CProxy.MyProxy.AHeadMoveEtvToIHall(smg.Warehouse, smg.DeviceCode);
                            }

                            if (data[2] == 1001 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.IHallFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.ISecondSwipedWaitforCheckSize)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 1 && data[3] == 9 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1001 && data[4] == 101)
                            {
                                CProxy.MyProxy.DealICheckCar(smg.Warehouse, smg.DeviceCode, task.ID, data[25], data[23].ToString());
                            }

                            if (data[2] == 1001 && data[4] == 104)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.ICheckCarFail);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.ISecondSwipedWaitforEVDown)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 1 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1001 && data[4] == 54)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.IEVDownFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.IEVDownFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 1 && data[3] == 54 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.IEVDownFinished);
                                }
                            }
                        }
                        else if (task.Status == EnmTaskStatus.IFirstSwipedWaitforCheckSize ||
                                 task.Status == EnmTaskStatus.ICheckCarFail)
                        {
                            if (data[2] == 1001 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.IHallFinishing);
                            }
                            //取物刷一次卡离开

                            if (data[2] == 1002 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.TempHallFinishing);
                            }

                        }
                        else if (task.Status == EnmTaskStatus.IHallFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 1 && data[3] == 55 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.DealCarLeave(smg.Warehouse, smg.DeviceCode, task.ID);
                                }
                            }
                        }
                        else if (task.Status == EnmTaskStatus.ISecondSwipedWaitforCarLeave)
                        {
                            if (data[2] == 1 && data[3] == 2 && data[4] == 9999)
                            {
                                CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                            }
                            if (data[2] == 1001 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.IHallFinishing);
                            }
                        }
                        #endregion
                        #region 取车
                        else if (task.Status == EnmTaskStatus.OWaitforEVDown)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 3 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1003 && data[4] == 54)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.OEVDownFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.OEVDownFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 3 && data[3] == 54 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                        }
                        else if (task.Status == EnmTaskStatus.OWaitforEVUp)
                        {
                            if (data[2] == 1003 && data[4] == 1)
                            {
                                CProxy.MyProxy.ODealEVUp(task.ID);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.OCarOutWaitforDriveaway)
                        {
                            if (data[2] == 3 && data[3] == 2 && data[4] == 9999)
                            {
                                CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                            }
                            if (data[2] == 1003 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.OHallFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.OHallFinishing)
                        {
                            if (data[2] == 3 && data[3] == 55 && data[4] == 9999)
                            {
                                //完成作业，释放设备
                                CProxy.MyProxy.DealCompleteTask(task.ID);
                            }
                        }
                        #endregion
                        #region 取物
                        else if (task.Status == EnmTaskStatus.TempWaitforEVDown)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 2 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1002 && data[4] == 54)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.TempEVDownFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.TempEVDownFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 2 && data[3] == 54 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                        }
                        else if (task.Status == EnmTaskStatus.TempWaitforEVUp)
                        {
                            if (data[2] == 1002 && data[4] == 1)
                            {
                                CProxy.MyProxy.ODealEVUp(task.ID);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.TempOCarOutWaitforDrive)
                        {
                            if (data[2] == 2 && data[3] == 2 && data[4] == 9999)
                            {
                                CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                            }
                            if (data[2] == 1002 && data[4] == 4)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.TempHallFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.TempHallFinishing)
                        {
                            if (data[2] == 2 && data[3] == 55 && data[4] == 9999)
                            {
                                //完成作业，释放设备
                                CProxy.MyProxy.DealCompleteTask(task.ID);
                            }
                        }
                        #endregion
                        #endregion
                    }
                    else if (smg.Type == EnmSMGType.ETV)
                    {
                        #region
                        #region 装载
                        if (task.Status == EnmTaskStatus.TWaitforLoad)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 13 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }

                            if (data[2] == 1013 && data[4] == 1)
                            {
                                //处理装载完成
                                CProxy.MyProxy.DealLoadFinishing(task.ID, data[25]);
                            }

                            if (data[2] == 1043 && data[4] == 1)
                            {
                                //车厅装载完成后，如果复检外形不通过，重新分配车位
                                CProxy.MyProxy.ReCheckCarWithLoad(task.ID, data[23].ToString(), data[25]);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.LoadFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 13 && data[3] == 51 && data[4] == 9999)
                                {
                                    //处理装载完成
                                    CProxy.MyProxy.DealLoadFinished(task.ID);
                                }
                            }
                            //强制下发13，51
                            if (data[2] == 1013 && data[4] == 1)
                            {
                                //只做重发处理
                                this.sendData(this.packageMessage(13, 51, smg.DeviceCode, null));
                            }
                        }
                        else if (task.Status == EnmTaskStatus.WillWaitForUnload)
                        {
                            if (data[2] == 1013 && data[4] == 1)
                            {
                                //只做重发处理
                                this.sendData(this.packageMessage(13, 51, smg.DeviceCode, task));
                            }

                            if (data[2] == 1043 && data[4] == 1)
                            {
                                //只做重发处理
                                this.sendData(this.packageMessage(43, 51, smg.DeviceCode, task));
                            }
                        }
                        #endregion
                        #region 卸载
                        else if (task.Status == EnmTaskStatus.TWaitforUnload)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 14 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1014 && data[4] == 1)
                            {
                                //处理卸载完成
                                CProxy.MyProxy.DealUnLoadFinishing(task.ID);
                            }
                            //发送卸载后，移动到车位后检测车位上有车，
                            if (data[2] == 1016 && data[4] == 1)
                            {
                                CProxy.MyProxy.DealUnloadButHasCarBlock(task.ID);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.UnLoadFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 14 && data[3] == 51 && data[4] == 9999)
                                {
                                    //处理作业完成
                                    CProxy.MyProxy.DealCompleteTask(task.ID);
                                }
                            }
                            //强制下发13，51
                            if (data[2] == 1014 && data[4] == 1)
                            {
                                //只做重发处理
                                this.sendData(this.packageMessage(14, 51, smg.DeviceCode, null));
                            }
                        }
                        #endregion
                        #region 移动
                        else if (task.Status == EnmTaskStatus.TWaitforMove)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 11 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1011 && data[4] == 1)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.MoveFinishing);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.MoveFinishing)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 11 && data[3] == 51 && data[4] == 9999)
                                {
                                    //处理作业完成
                                    CProxy.MyProxy.DealCompleteTask(task.ID);
                                }
                            }
                        }
                        #endregion
                        #region 故障恢复
                        else if (task.Status == EnmTaskStatus.TMUROWaitforLoad)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 13 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1013 && data[4] == 1)
                            {
                                //处理装载完成
                                CProxy.MyProxy.DealLoadFinishing(task.ID, data[25]);
                            }
                        }
                        else if (task.Status == EnmTaskStatus.TMUROWaitforUnload)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 14 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }
                            if (data[2] == 1014 && data[4] == 1)
                            {
                                //处理卸载完成
                                CProxy.MyProxy.DealUnLoadFinishing(task.ID);
                            }
                        }
                        #endregion
                        #region 存车装载，复检不通过
                        else if (task.Status == EnmTaskStatus.ReCheckInLoad)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 43 && data[3] == 51 && data[4] == 9999)
                                {
                                    //处理装载完成
                                    CProxy.MyProxy.DealLoadFinished(task.ID);
                                }
                            }
                            //强制下发43，51
                            if (data[2] == 1043 && data[4] == 1)
                            {
                                //只做重发处理
                                this.sendData(this.packageMessage(43, 51, smg.DeviceCode, null));
                            }
                        }
                        #endregion
                        #region 删除指令
                        else if (task.Status == EnmTaskStatus.WaitforDeleteTask)
                        {
                            if (task.SendStatusDetail == EnmTaskStatusDetail.SendWaitAsk)
                            {
                                if (data[2] == 23 && data[3] == 1 && data[4] == 9999)
                                {
                                    CProxy.MyProxy.UpdateSendStatusDetail(task.ID, (int)EnmTaskStatusDetail.Asked);
                                }
                            }

                            if (data[2] == 1023 && data[4] == 1)
                            {
                                //修改其作业状态为等待卸载
                                CProxy.MyProxy.UpdateTaskStatus(task.ID, (int)EnmTaskStatus.TWaitforUnload);
                            }
                        }
                        #endregion
                        #endregion
                    }
                }
                else
                {
                    #region 处理第一次入库
                    if (data[2] == 1001 && data[4] == 1)
                    {
                        CProxy.MyProxy.DealCarEntrance(smg.Warehouse, smg.DeviceCode);
                    }
                    #endregion
                    #region 强制发送异常报文
                    else if (data[2] == 1003 && data[4] == 4)
                    {
                        this.sendData(this.packageMessage(3, 55, smg.DeviceCode, null));
                    }
                    #endregion
                    #region 发送（13，51）（14，51）（11，51）
                    else if (data[2] == 1013 && data[4] == 1)
                    {
                        //只做重发处理
                        this.sendData(this.packageMessage(13, 51, smg.DeviceCode, null));
                    }
                    else if (data[2] == 1014 && data[4] == 1)
                    {
                        //只做重发处理
                        this.sendData(this.packageMessage(14, 51, smg.DeviceCode, null));
                    }
                    else if (data[2] == 1011 && data[4] == 1)
                    {
                        //只做重发处理
                        this.sendData(this.packageMessage(11, 51, smg.DeviceCode, null));
                    }
                    else if (data[2] == 1001 && data[4] == 4)
                    {
                        this.sendData(this.packageMessage(1, 55, smg.DeviceCode, null));
                    }
                    else if (data[2] == 1043 && data[4] == 1)
                    {
                        this.sendData(this.packageMessage(43, 51, smg.DeviceCode, null));
                    }
                    #endregion

                }

                #region 处理故障报文
                if (data[2] == 1074 && data[4] == 7)
                {

                    this.sendData(this.packageMessage(74, 1, smg.DeviceCode, null));
                }

                #endregion

            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 处理报警信息
        /// 设备报警及状态字是一个DB块的
        /// </summary>
        public void DealAlarmInfo()
        {
            Log log = LogFactory.GetLogger("WorkFlow.DealAlarmInfo");
            try
            {
                List<Device> deviceLst = CProxy.MyProxy.FindDevicesList(warehouse);
                List<Alarm> allfaultsLst = CProxy.MyProxy.FindAlarmsList(warehouse);
                for (int i = 0; i < deviceLst.Count; i++)
                {
                    Device smg = deviceLst[i];
                    bool isUpdate = false;
                    if (smg.Type == EnmSMGType.Hall)
                    {
                        #region 车厅
                        #region 获取项名称
                        int basePoint = 1012;
                        if (smg.DeviceCode > 11)
                        {
                            basePoint += smg.DeviceCode - 11;
                        }
                        basePoint += smg.DeviceCode;

                        string DBItemName = basePoint.ToString();
                        string itemName = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemName))
                            {
                                itemName = item;
                                break;
                            }
                        }
                        if (itemName == "")
                        {
                            log.Error("更新报警时，设备-" + smg.DeviceCode + "   DB块-" + DBItemName + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        byte[] bytesAlarmBuf = this.readAlarmBytesBuffer(itemName);
                        if (bytesAlarmBuf == null || bytesAlarmBuf.Length == 0)
                        {
                            continue;
                        }

                        #region 更新报警信息
                        List<Alarm> needUpdate = new List<Alarm>();
                        List<Alarm> faultsList = allfaultsLst.FindAll(dev => dev.Warehouse == smg.Warehouse && dev.DeviceCode == smg.DeviceCode);
                        for (int f = 0; f < faultsList.Count; f++)
                        {
                            Alarm fault = faultsList[f];
                            #region
                            int faultAddrs = fault.Address;
                            int byteNum = faultAddrs / 10;
                            int bitNum = faultAddrs % 10;

                            if (byteNum > bytesAlarmBuf.Length)
                            {
                                continue;
                            }
                            int value = bytesAlarmBuf[byteNum];
                            value = value >> bitNum;
                            value = value % 2;
                            if (value != fault.Value)
                            {
                                fault.Value = (byte)value;
                                needUpdate.Add(fault);
                            }
                            //可接收新指令
                            if (faultAddrs == 297)
                            {
                                if (smg.IsAvailabe != fault.Value)
                                {
                                    smg.IsAvailabe = value;
                                    //更新设备可接收新指令  
                                    isUpdate = true;
                                }
                            }

                            #region 处理车车辆跑位
                            if (faultAddrs == 135)
                            {
                                if (value == 1)
                                {
                                    CProxy.MyProxy.DealCarTraceOut(smg.Warehouse, smg.DeviceCode);
                                }
                            }
                            #endregion

                            #endregion
                        }

                        if (needUpdate.Count > 0)
                        {
                            CProxy.MyProxy.UpdateAlarmsList(needUpdate);
                        }
                        #endregion
                        #region 更新设备状态

                        //控制模式 =38
                        int mode = 38;
                        if (mode <= bytesAlarmBuf.Length)
                        {
                            short modeValue = shortFromByte(bytesAlarmBuf[mode + 1], bytesAlarmBuf[mode]);
                            if (modeValue != (short)smg.Mode && modeValue > 0)
                            {
                                if (modeValue > 4)
                                {
                                    log.Error("devicecode-" + smg.DeviceCode + " 更新模式时，读取的值不对-value:" + modeValue);
                                }
                                else
                                {
                                    smg.Mode = (EnmModel)modeValue;
                                    isUpdate = true;
                                }
                            }
                        }
                        //存车自动步
                        int inStep = 40;
                        if (inStep <= bytesAlarmBuf.Length)
                        {
                            int inStepValue = shortFromByte(bytesAlarmBuf[inStep + 1], bytesAlarmBuf[inStep]);
                            if (inStepValue != smg.InStep)
                            {
                                smg.InStep = inStepValue;
                                isUpdate = true;

                                if (inStepValue != 0)
                                {
                                    smg.RunStep = inStepValue;
                                }
                            }
                        }

                        //取车自动步
                        int outStep = 42;
                        if (outStep <= bytesAlarmBuf.Length)
                        {
                            int outValue = shortFromByte(bytesAlarmBuf[outStep + 1], bytesAlarmBuf[outStep]);
                            if (outValue != smg.OutStep)
                            {
                                smg.OutStep = outValue;
                                isUpdate = true;

                                if (outValue != 0)
                                {
                                    smg.RunStep = outValue;
                                }
                            }
                        }

                        if (smg.InStep == 0 && smg.OutStep == 0)
                        {
                            smg.RunStep = 0;
                        }

                        #endregion
                        #region 写入车厅的工作方式
                        int workpattern = 34;
                        if (workpattern <= bytesAlarmBuf.Length)
                        {
                            int pValue = shortFromByte(bytesAlarmBuf[workpattern + 1], bytesAlarmBuf[workpattern]);
                            if (pValue != (int)smg.HallType)
                            {
                                string itname = itemName.Split(',').First() + ",INT" + workpattern + ",1";
                                WriteValueToPlc(itname, (Int16)smg.HallType);
                            }
                        }
                        #endregion

                        #endregion
                    }
                    else if (smg.Type == EnmSMGType.ETV)
                    {
                        #region ETV
                        #region 获取项名称
                        int basePoint = 1003 + 2 * (smg.DeviceCode - 1);
                        string DBItemName = basePoint.ToString();
                        string itemName = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemName))
                            {
                                itemName = item;
                                break;
                            }
                        }
                        if (itemName == "")
                        {
                            log.Error("更新报警时，设备-" + smg.DeviceCode + "  DB块-" + DBItemName + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        byte[] bytesAlarmBuf = this.readAlarmBytesBuffer(itemName);
                        if (bytesAlarmBuf == null || bytesAlarmBuf.Length == 0)
                        {
                            continue;
                        }

                        #region 更新报警信息
                        List<Alarm> needUpdate = new List<Alarm>();
                        List<Alarm> faultsList = allfaultsLst.FindAll(dev => dev.Warehouse == smg.Warehouse && dev.DeviceCode == smg.DeviceCode);
                        for (int f = 0; f < faultsList.Count; f++)
                        {
                            Alarm fault = faultsList[f];
                            #region
                            int faultAddrs = fault.Address;
                            int byteNum = faultAddrs / 10;
                            int bitNum = faultAddrs % 10;

                            if (byteNum > bytesAlarmBuf.Length)
                            {
                                continue;
                            }
                            int value = bytesAlarmBuf[byteNum];
                            value = value >> bitNum;
                            value = value % 2;
                            if (value != fault.Value)
                            {
                                fault.Value = (byte)value;
                                needUpdate.Add(fault);
                            }
                            //可接收新指令
                            if (faultAddrs == 297)
                            {
                                if (smg.IsAvailabe != fault.Value)
                                {
                                    smg.IsAvailabe = value;
                                    //更新设备可接收新指令
                                    isUpdate = true;
                                }
                            }
                            #endregion
                        }

                        if (needUpdate.Count > 0)
                        {
                            CProxy.MyProxy.UpdateAlarmsList(needUpdate);
                        }
                        #endregion
                        #region 更新设备状态

                        #region 更新地址
                        //当前边
                        int line_Num = 30;
                        int line_Value = 0;
                        if (line_Num <= bytesAlarmBuf.Length)
                        {
                            line_Value = shortFromByte(bytesAlarmBuf[line_Num + 1], bytesAlarmBuf[line_Num]);
                        }
                        //当前列
                        int colmn_Num = 32;
                        int colmn_Value = 0;
                        if (colmn_Num <= bytesAlarmBuf.Length)
                        {
                            colmn_Value = shortFromByte(bytesAlarmBuf[colmn_Num + 1], bytesAlarmBuf[colmn_Num]);
                        }
                        //当前层
                        int layer_Num = 34;
                        int layer_Value = 0;
                        if (layer_Num <= bytesAlarmBuf.Length)
                        {
                            layer_Value = shortFromByte(bytesAlarmBuf[layer_Num + 1], bytesAlarmBuf[layer_Num]);
                        }
                        #endregion
                        if (line_Value > 0 && colmn_Value > 0 && layer_Value > 0)
                        {
                            string newAddrs = line_Value.ToString() + colmn_Value.ToString().PadLeft(2, '0') + layer_Value.ToString().PadLeft(2, '0');
                            if (string.Compare(smg.Address, newAddrs) != 0)
                            {
                                smg.Address = newAddrs;
                                isUpdate = true;
                            }
                        }
                        //自动步
                        int autoStep = 36;
                        if (autoStep <= bytesAlarmBuf.Length)
                        {
                            int autoStepValue = shortFromByte(bytesAlarmBuf[autoStep + 1], bytesAlarmBuf[autoStep]);
                            if (autoStepValue != smg.RunStep)
                            {
                                smg.RunStep = autoStepValue;
                                isUpdate = true;
                            }
                        }

                        //装载步进
                        int loadStep = 38;
                        if (loadStep <= bytesAlarmBuf.Length)
                        {
                            int loadStepValue = shortFromByte(bytesAlarmBuf[loadStep + 1], bytesAlarmBuf[loadStep]);
                            if (loadStepValue != smg.InStep)
                            {
                                smg.InStep = loadStepValue;
                                isUpdate = true;
                            }
                        }

                        //卸载步进
                        int unloadStep = 40;
                        if (unloadStep <= bytesAlarmBuf.Length)
                        {
                            int unloadStepValue = shortFromByte(bytesAlarmBuf[unloadStep + 1], bytesAlarmBuf[unloadStep]);
                            if (unloadStepValue != smg.OutStep)
                            {
                                smg.OutStep = unloadStepValue;
                                isUpdate = true;
                            }
                        }

                        //控制模式
                        int mode = 50;
                        if (mode <= bytesAlarmBuf.Length)
                        {
                            short modeValue = shortFromByte(bytesAlarmBuf[mode + 1], bytesAlarmBuf[mode]);
                            if (modeValue != (short)smg.Mode && modeValue > 0)
                            {
                                if (modeValue > 4)
                                {
                                    log.Error("devicecode-" + smg.DeviceCode + " 更新模式时，读取的值不对-value:" + modeValue);
                                }
                                else
                                {
                                    smg.Mode = (EnmModel)modeValue;
                                    isUpdate = true;
                                }
                            }
                        }

                        #endregion
                        #endregion
                    }

                    #region 模式不是全自动时，强制将设备变为不可用
                    if (smg.Mode != EnmModel.Automatic)
                    {
                        if (smg.IsAble == 1)
                        {
                            smg.IsAble = 0;
                        }
                        //切换模式时，强制将作业变为故障中
                        if (smg.TaskID != 0)
                        {
                            ImplementTask itask = CProxy.MyProxy.FindITask(smg.TaskID);
                            if (itask != null)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(itask.ID, (int)EnmTaskStatus.TMURO);
                            }
                        }
                    }
                    else
                    {
                        if (smg.Type == EnmSMGType.Hall && smg.IsAble == 0)
                        {
                            smg.IsAble = 1;
                            #region 如果只有车厅作业，则可以强制复位车厅作业
                            if (smg.TaskID != 0)
                            {
                                //CProxy.MyProxy.ResetHallOnlyHasTask(smg.Warehouse, smg.DeviceCode);                                
                            }
                            #endregion                           
                        }
                    }

                    #endregion

                    if (isUpdate)
                    {
                        CProxy.MyProxy.UpdateDevice(smg);

                        string rcd = "设备 - " + smg.DeviceCode + " 更新状态，模式 - " + smg.Mode + " 可用性 - " + smg.IsAble + " 可接收新指令 - " + smg.IsAvailabe + " 自动步进 - " + smg.RunStep + " 存（装载）步进 - " + smg.InStep + " 取(卸载)步进 - " + smg.OutStep;
                        log.Debug(rcd);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 设备报警及状态字是不同的DB块的
        /// </summary>
        public void DealFaultAlarmAndStatusWord()
        {
            Log log = LogFactory.GetLogger("WorkFlow.DealFaultAlarmAndStatusWord");
            try
            {
                List<Device> deviceLst = CProxy.MyProxy.FindDevicesList(warehouse);
                List<Alarm> allfaultsLst = CProxy.MyProxy.FindAlarmsList(warehouse);
                for (int i = 0; i < deviceLst.Count; i++)
                {
                    Device smg = deviceLst[i];
                    bool isUpdate = false;
                    if (smg.Type == EnmSMGType.Hall)
                    {
                        #region 车厅
                        #region 更新报警位
                        #region 获取项名称
                        int basePoint = 1012;
                        if (smg.DeviceCode > 11)
                        {
                            basePoint += smg.DeviceCode - 11;
                        }
                        basePoint += smg.DeviceCode;

                        string DBItemName = basePoint.ToString();
                        string itemName = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemName))
                            {
                                itemName = item;
                                break;
                            }
                        }
                        if (itemName == "")
                        {
                            log.Error("更新报警时，设备-" + smg.DeviceCode + "   DB块-" + DBItemName + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        byte[] bytesAlarmBuf = this.readAlarmBytesBuffer(itemName);
                        if (bytesAlarmBuf != null && bytesAlarmBuf.Length > 0)
                        {
                            #region 更新报警信息
                            List<Alarm> needUpdate = new List<Alarm>();
                            List<Alarm> faultsList = allfaultsLst.FindAll(dev => dev.Warehouse == smg.Warehouse && dev.DeviceCode == smg.DeviceCode);
                            for (int j = 0; j < faultsList.Count; j++)
                            {
                                Alarm fault = faultsList[j];
                                #region
                                int faultAddrs = fault.Address;
                                int byteNum = faultAddrs / 10;
                                int bitNum = faultAddrs % 10;

                                if (byteNum > bytesAlarmBuf.Length)
                                {
                                    continue;
                                }
                                int value = bytesAlarmBuf[byteNum];
                                value = value >> bitNum;
                                value = value % 2;
                                if (value != fault.Value)
                                {
                                    fault.Value = (byte)value;
                                    needUpdate.Add(fault);
                                }
                                //可接收新指令
                                if (faultAddrs == isAvailHall)
                                {
                                    if (smg.IsAvailabe != fault.Value)
                                    {
                                        smg.IsAvailabe = value;
                                        isUpdate = true;
                                    }
                                }

                                #region 处理车车辆跑位
                                if (faultAddrs == traceOff)
                                {
                                    if (value == 1)
                                    {
                                        CProxy.MyProxy.DealCarTraceOut(smg.Warehouse, smg.DeviceCode);
                                    }
                                }
                                #endregion
                                #endregion
                            }

                            if (needUpdate.Count > 0)
                            {
                                CProxy.MyProxy.UpdateAlarmsList(needUpdate);
                            }
                            #endregion
                        }
                        #endregion
                        #region 更新设备状态字
                        #region 获取项名称
                        int baseWordPoint = 1212;
                        if (smg.DeviceCode > 11)
                        {
                            baseWordPoint += smg.DeviceCode - 11;
                        }
                        baseWordPoint += smg.DeviceCode;

                        string DBItemNameWord = baseWordPoint.ToString();
                        string itemNameWord = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemNameWord))
                            {
                                itemNameWord = item;
                                break;
                            }
                        }
                        if (itemNameWord == "")
                        {
                            log.Error("更新状态字时，设备-" + smg.DeviceCode + "   DB块-" + DBItemNameWord + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        //读状态字
                        byte[] bytesWordBuf = this.readAlarmBytesBuffer(itemNameWord);
                        if (bytesWordBuf != null && bytesWordBuf.Length > 0)
                        {
                            //控制模式 =8
                            int mode = 8;
                            if (mode <= bytesWordBuf.Length)
                            {
                                short modeValue = shortFromByte(bytesWordBuf[mode + 1], bytesWordBuf[mode]);
                                if (modeValue != (short)smg.Mode && modeValue > 0)
                                {
                                    if (modeValue > 4)
                                    {
                                        log.Error("devicecode-" + smg.DeviceCode + " 更新模式时，读取的值不对-value:" + modeValue);
                                    }
                                    else
                                    {
                                        smg.Mode = (EnmModel)modeValue;
                                        isUpdate = true;
                                    }

                                }
                            }
                            //存车自动步
                            int inStep = 10;
                            if (inStep <= bytesWordBuf.Length)
                            {
                                int inStepValue = shortFromByte(bytesWordBuf[inStep + 1], bytesWordBuf[inStep]);
                                if (inStepValue != smg.InStep)
                                {
                                    smg.InStep = inStepValue;
                                    isUpdate = true;

                                    if (inStepValue != 0)
                                    {
                                        smg.RunStep = inStepValue;
                                    }
                                }
                            }

                            //取车自动步
                            int outStep = 12;
                            if (outStep <= bytesWordBuf.Length)
                            {
                                int outValue = shortFromByte(bytesWordBuf[outStep + 1], bytesWordBuf[outStep]);
                                if (outValue != smg.OutStep)
                                {
                                    smg.OutStep = outValue;
                                    isUpdate = true;

                                    if (outValue != 0)
                                    {
                                        smg.RunStep = outValue;
                                    }
                                }
                            }

                            if (smg.InStep == 0 && smg.OutStep == 0)
                            {
                                smg.RunStep = 0;
                            }

                            #region 写入车厅的工作方式
                            int workpattern = 4;
                            if (workpattern <= bytesWordBuf.Length)
                            {
                                int pValue = shortFromByte(bytesWordBuf[workpattern + 1], bytesWordBuf[workpattern]);
                                if (pValue != (int)smg.HallType)
                                {
                                    string itname = itemNameWord.Split(',').First() + ",INT" + workpattern + ",1";
                                    WriteValueToPlc(itname, (Int16)smg.HallType);
                                }
                            }
                            #endregion

                            #region 写入车位信息
                            try
                            {
                                int totalpattern = 0;
                                int temppatten = 2;
                                if (isUpdate)
                                {
                                    //查询车位信息
                                    int pTotalValue;
                                    int pTempValue;

                                    #region
                                    bool nback = CProxy.MyProxy.GetLocInfo(warehouse, out pTotalValue, out pTempValue);
                                    if (nback)
                                    {
                                        if (pTotalValue != totalSpace)
                                        {
                                            totalSpace = pTotalValue;
                                            if (totalSpace == 0)
                                            {
                                                string itname = itemNameWord.Split(',').First() + ",INT" + totalpattern + ",1";
                                                WriteValueToPlc(itname, (Int16)999);
                                            }
                                        }

                                        if (pTempValue != tempSpace)
                                        {
                                            tempSpace = pTempValue;
                                            if (tempSpace == 0)
                                            {
                                                string itname = itemNameWord.Split(',').First() + ",INT" + temppatten + ",1";
                                                WriteValueToPlc(itname, (Int16)999);
                                            }
                                        }
                                    }
                                    #endregion

                                }
                            }
                            catch (Exception e1)
                            {
                                log.Error("写入车位信息异常 - " + e1.ToString());
                            }
                            #endregion
                        }
                        #endregion
                        #endregion
                    }
                    else if (smg.Type == EnmSMGType.ETV)
                    {
                        #region ETV
                        #region 更新报警位
                        #region 获取项名称
                        int basePoint = 1003 + 2 * (smg.DeviceCode - 1);
                        string DBItemName = basePoint.ToString();
                        string itemName = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemName))
                            {
                                itemName = item;
                                break;
                            }
                        }
                        if (itemName == "")
                        {
                            log.Error("更新报警时，设备-" + smg.DeviceCode + "  DB块-" + DBItemName + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        byte[] bytesAlarmBuf = this.readAlarmBytesBuffer(itemName);
                        if (bytesAlarmBuf != null && bytesAlarmBuf.Length > 0)
                        {
                            #region 更新报警信息
                            List<Alarm> needUpdate = new List<Alarm>();
                            List<Alarm> faultsList = allfaultsLst.FindAll(dev => dev.Warehouse == smg.Warehouse && dev.DeviceCode == smg.DeviceCode);
                            for (int j = 0; j < faultsList.Count; j++)
                            {
                                Alarm fault = faultsList[j];
                                #region
                                int faultAddrs = fault.Address;
                                int byteNum = faultAddrs / 10;
                                int bitNum = faultAddrs % 10;

                                if (byteNum > bytesAlarmBuf.Length)
                                {
                                    continue;
                                }
                                int value = bytesAlarmBuf[byteNum];
                                value = value >> bitNum;
                                value = value % 2;
                                if (value != fault.Value)
                                {
                                    fault.Value = (byte)value;
                                    needUpdate.Add(fault);
                                }
                                //可接收新指令
                                if (faultAddrs == isAvailTV)
                                {
                                    if (smg.IsAvailabe != fault.Value)
                                    {
                                        smg.IsAvailabe = value;
                                        isUpdate = true;
                                    }
                                }
                                #endregion
                            }

                            if (needUpdate.Count > 0)
                            {
                                CProxy.MyProxy.UpdateAlarmsList(needUpdate);
                            }
                            #endregion                           
                        }
                        #endregion

                        #region 更新状态字
                        #region 获取项名称
                        int baseWordPoint = 1203 + 2 * (smg.DeviceCode - 1);

                        string DBItemNameWord = baseWordPoint.ToString();
                        string itemNameWord = "";
                        foreach (string item in S7_Connection_Items)
                        {
                            if (item.Contains("DB" + DBItemNameWord))
                            {
                                itemNameWord = item;
                                break;
                            }
                        }
                        if (itemNameWord == "")
                        {
                            log.Error("更新状态字时，设备-" + smg.DeviceCode + "   DB块-" + DBItemNameWord + " 在注册列表中找不到！列表的数量-" + s7_Connection_Items.Length);
                            continue;
                        }
                        #endregion
                        //读状态字
                        byte[] bytesWordBuf = this.readAlarmBytesBuffer(itemNameWord);
                        if (bytesWordBuf != null && bytesWordBuf.Length > 0)
                        {
                            #region 更新设备状态                          
                            #region 更新地址
                            //当前边
                            int line_Num = 0;
                            int line_Value = 0;
                            if (line_Num <= bytesWordBuf.Length)
                            {
                                line_Value = shortFromByte(bytesWordBuf[line_Num + 1], bytesWordBuf[line_Num]);
                            }
                            //当前列
                            int colmn_Num = 2;
                            int colmn_Value = 0;
                            if (colmn_Num <= bytesWordBuf.Length)
                            {
                                colmn_Value = shortFromByte(bytesWordBuf[colmn_Num + 1], bytesWordBuf[colmn_Num]);
                            }
                            //当前层
                            int layer_Num = 4;
                            int layer_Value = 0;
                            if (layer_Num <= bytesWordBuf.Length)
                            {
                                layer_Value = shortFromByte(bytesWordBuf[layer_Num + 1], bytesWordBuf[layer_Num]);
                            }
                            #endregion
                            //边列层没有是0
                            if (line_Value > 0 && colmn_Value > 0 && layer_Value > 0)
                            {
                                string newAddrs = line_Value.ToString() + colmn_Value.ToString().PadLeft(2, '0') + layer_Value.ToString().PadLeft(2, '0');
                                if (string.Compare(smg.Address, newAddrs) != 0)
                                {
                                    smg.Address = newAddrs;
                                    isUpdate = true;
                                }
                            }

                            //自动步
                            int autoStep = 6;
                            if (autoStep <= bytesWordBuf.Length)
                            {
                                int autoStepValue = shortFromByte(bytesWordBuf[autoStep + 1], bytesWordBuf[autoStep]);
                                if (autoStepValue != smg.RunStep)
                                {
                                    smg.RunStep = autoStepValue;
                                    isUpdate = true;
                                }
                            }

                            //装载步进
                            int loadStep = 8;
                            if (loadStep <= bytesWordBuf.Length)
                            {
                                int loadStepValue = shortFromByte(bytesWordBuf[loadStep + 1], bytesWordBuf[loadStep]);
                                if (loadStepValue != smg.InStep)
                                {
                                    smg.InStep = loadStepValue;
                                    isUpdate = true;
                                }
                            }

                            //卸载步进
                            int unloadStep = 10;
                            if (unloadStep <= bytesWordBuf.Length)
                            {
                                int unloadStepValue = shortFromByte(bytesWordBuf[unloadStep + 1], bytesWordBuf[unloadStep]);
                                if (unloadStepValue != smg.OutStep)
                                {
                                    smg.OutStep = unloadStepValue;
                                    isUpdate = true;
                                }
                            }

                            //控制模式
                            int mode = 20;
                            if (mode <= bytesWordBuf.Length)
                            {
                                short modeValue = shortFromByte(bytesWordBuf[mode + 1], bytesWordBuf[mode]);
                                if (modeValue != (short)smg.Mode && modeValue > 0)
                                {
                                    if (modeValue > 4)
                                    {
                                        log.Error("devicecode-" + smg.DeviceCode + " 更新模式时，读取的值不对-value:" + modeValue);
                                    }
                                    else
                                    {
                                        smg.Mode = (EnmModel)modeValue;
                                        isUpdate = true;
                                    }

                                }
                            }
                            #endregion
                        }
                        #endregion
                        #endregion
                    }

                    #region 模式不是全自动时，强制将设备变为不可用
                    if (smg.Mode != EnmModel.Automatic)
                    {
                        if (smg.IsAble == 1)
                        {
                            smg.IsAble = 0;
                        }
                        //切换模式时，强制将作业变为故障中
                        if (smg.TaskID != 0)
                        {
                            ImplementTask itask = CProxy.MyProxy.FindITask(smg.TaskID);
                            if (itask != null && smg.Type == EnmSMGType.ETV)
                            {
                                CProxy.MyProxy.UpdateTaskStatus(itask.ID, (int)EnmTaskStatus.TMURO);
                            }
                        }
                    }
                    else
                    {
                        if (smg.Type == EnmSMGType.Hall && smg.IsAble == 0)
                        {
                            smg.IsAble = 1;
                            isUpdate = true;
                        }
                    }

                    #endregion

                    if (isUpdate)
                    {
                        CProxy.MyProxy.UpdateDevice(smg);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }

        }


        /// <summary>
        /// 报文接收
        /// </summary>
        /// <param name="data">返回信息</param>
        /// <param name="hasData">有报文</param>
        private void unpackageMessage(out Int16[] data, out bool hasData)
        {
            #region
            hasData = false;
            data = null;
            if (JudgeSocketAvailabe() == false)
            {
                return;
            }
            string recvFlag = s7_Connection_Items[1];
            object recvBuffFlag = plcAccess.ReadData(recvFlag, (DefVarType.Int).ToString());
            if (recvBuffFlag == null)
            {
                return;
            }
            //有数据要接收
            if (Convert.ToInt16(recvBuffFlag) == 9999)
            {
                string recvBuff = s7_Connection_Items[0];
                object recvData = plcAccess.ReadData(recvBuff, (DefVarType.Int).ToString());
                if (recvBuff != null)
                {
                    //清空标志字                    
                    Int16 flag = 0;
                    int nback = plcAccess.WriteData(recvFlag, flag);
                    if (nback == 1)
                    {
                        //读取数据成功，返回值
                        data = (Int16[])recvData;
                        hasData = true;
                        //记录
                        CProxy.MyProxy.AddTelegramLog(data, 2);

                        #region 打印出来吧
                        Log log = LogFactory.GetLogger("unpackageMessage");
                        StringBuilder strBuild = new StringBuilder();
                        strBuild.Append("报文接收：" + Environment.NewLine);
                        foreach (Int16 by in (short[])recvData)
                        {
                            strBuild.Append("[" + by + "]");
                        }
                        log.Info(strBuild.ToString());
                        #endregion
                    }
                }
            }
            #endregion
        }

        /// <summary>
        /// 报文发送
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool sendData(Int16[] data)
        {
            Log log = LogFactory.GetLogger("WorkFlow.sendData");
            try
            {
                if (JudgeSocketAvailabe() == false)
                {
                    return false;
                }
                string sendbuff = s7_Connection_Items[3];
                //先读发送缓冲区标志字
                object sendFlag = plcAccess.ReadData(sendbuff, (DefVarType.Int).ToString());
                if (sendFlag != null)
                {
                    //可以发送报文
                    if (Convert.ToInt16(sendFlag) == 0)
                    {
                        //写50个字
                        string sendItem = s7_Connection_Items[2];
                        int nback = plcAccess.WriteData(sendItem, data);
                        if (nback == 1)
                        {
                            //标志字置9999
                            Int16 flag = 9999;
                            nback = plcAccess.WriteData(sendbuff, flag);
                            if (nback == 1) //完成写入工作
                            {
                                //记录报文
                                CProxy.MyProxy.AddTelegramLog(data, 1);

                                #region 打印出来吧
                                StringBuilder strBuild = new StringBuilder();
                                strBuild.Append("报文发送：" + Environment.NewLine);
                                foreach (Int16 by in data)
                                {
                                    strBuild.Append("[" + by + "]");
                                }
                                log.Info(strBuild.ToString());
                                #endregion

                                return true;
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
            return false;
        }

        /// <summary>
        /// 读取报警字节数组
        /// </summary>
        /// <param name="itemName"></param>
        /// <returns></returns>
        private byte[] readAlarmBytesBuffer(string itemName)
        {
            Log log = LogFactory.GetLogger("WorkFlow.readAlarmBytesBuffer");
            try
            {
                if (JudgeSocketAvailabe() == false)
                {
                    return null;
                }
                byte[] buffer = (byte[])plcAccess.ReadData(itemName, DefVarType.Byte.ToString());
                return buffer;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
            return null;
        }

        private short[] packageMessage(int mtype, int stype, int smg, ImplementTask tsk)
        {
            short[] data = new short[50];
            if (tsk != null)
            {
                data[2] = Convert.ToInt16(mtype);
                data[3] = Convert.ToInt16(stype);
                data[6] = Convert.ToInt16(smg);
                data[11] = Convert.ToInt16(string.IsNullOrEmpty(tsk.ICCardCode) ? "0" : removeLetter(tsk.ICCardCode));
                data[23] = Convert.ToInt16(string.IsNullOrEmpty(tsk.CarSize) ? "0" : tsk.CarSize);
                data[25] = Convert.ToInt16(tsk.Distance);
                if (!string.IsNullOrEmpty(tsk.FromLctAddress))
                {
                    data[30] = Convert.ToInt16(tsk.FromLctAddress.Substring(0, 1));
                    data[31] = Convert.ToInt16(tsk.FromLctAddress.Substring(1, 2));
                    data[32] = Convert.ToInt16(tsk.FromLctAddress.Substring(3));
                }
                if (!string.IsNullOrEmpty(tsk.ToLctAddress))
                {
                    data[35] = Convert.ToInt16(tsk.ToLctAddress.Substring(0, 1));
                    data[36] = Convert.ToInt16(tsk.ToLctAddress.Substring(1, 2));
                    data[37] = Convert.ToInt16(tsk.ToLctAddress.Substring(3));
                }
                data[40] = string.IsNullOrEmpty(tsk.LocSize) ? (short)0 : Convert.ToInt16(tsk.LocSize);
                data[47] = (short)tsk.CarWeight;
            }
            else
            {
                data[2] = Convert.ToInt16(mtype);
                data[3] = Convert.ToInt16(stype);
                data[6] = Convert.ToInt16(smg);
            }
            data[0] = (short)warehouse;
            data[48] = getSerial();
            data[49] = (short)9999;
            return data;
        }

        /// <summary>
        /// 如果卡号中出现非数字的，以9来代替
        /// </summary>
        /// <param name="iccode"></param>
        /// <returns></returns>
        private string removeLetter(string iccode)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char aa in iccode)
            {
                if (char.IsDigit(aa))
                {
                    builder.Append(aa);
                }
                else
                {
                    builder.Append('9');
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// 获取报文ID
        /// </summary>
        /// <returns></returns>
        private short getSerial()
        {
            if (messageID < (short)4999)
            {
                messageID++;
            }
            else
            {
                messageID = 1;
            }
            return messageID;
        }

        /// <summary>
        /// 判断连接是否正常，不正常，进行重连
        /// </summary>
        /// <returns></returns>
        private bool JudgeSocketAvailabe()
        {
            Log log = LogFactory.GetLogger("WorkFlow.JudgeSocketAvailabe");

            if (s7_Connection_Items == null ||
                s7_Connection_Items.Length < 4)
            {
                log.Error("s7_Connection_Items 无效,无法收发报文");
                return false;
            }

            if (plcAccess == null)
            {
                log.Error("没有建立有效的socket, plcAccess为空, 无法收发报文");
                return false;
            }

            if (!plcAccess.IsConnected)
            {
                ConnectPLC();
            }

            if (!plcAccess.IsConnected)
            {
                log.Error("plcAccess没有建立连接, 无法收发报文");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 高低字节转化为整型
        /// </summary>
        /// <param name="lobyte"></param>
        /// <param name="hibyte"></param>
        /// <returns></returns>
        private short shortFromByte(byte lobyte, byte hibyte)
        {
            return Convert.ToInt16(hibyte * 256 + lobyte);
        }

        /// <summary>
        /// 向PLC中写值
        /// </summary>
        /// <param name="itemname"></param>
        /// <param name="value"></param>
        private void WriteValueToPlc(string itemname, Int16 value)
        {
            Log log = LogFactory.GetLogger("WorkFlow.WriteValueToPlc");
            try
            {
                Task.Factory.StartNew(() =>
                {

                    plcAccess.WriteData(itemname, value);

                });
                log.Info("warehouse-" + warehouse + "  向项-" + itemname + "  写值-" + value);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }
        }


    }
}
