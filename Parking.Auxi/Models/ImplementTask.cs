using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    /// <summary>
    /// 执行中的任务,一个设备只能允许有一个,
    /// 如果有避让作业，可以允许有两个
    /// </summary>
    public class ImplementTask
    {
      
        public int ID { get; set; }
        public int Warehouse { get; set; }
        public int DeviceCode { get; set; }
        public EnmTaskType Type { get; set; }
        public EnmTaskStatus Status { get; set; }       
        public EnmTaskStatusDetail SendStatusDetail { get; set; }
        public DateTime CreateDate { get; set; }      
        public DateTime SendDtime { get; set; }
        public int HallCode { get; set; }      
        public string FromLctAddress { get; set; }       
        public string ToLctAddress { get; set; }       
        public string ICCardCode { get; set; }
        public int Distance { get; set; }
        public string CarSize { get; set; }
        public int CarWeight { get; set; }       
        public int IsComplete { get; set; }
        public string LocSize { get; set; }
        public string PlateNum { get; set; }
    }

    public enum EnmTaskType
    {
        Init=0,
        SaveCar,
        GetCar,
        Transpose,
        Move,
        TempGet,
        Avoid,
        RetrySend
    }

    public enum EnmTaskStatusDetail
    {
        NoSend=0,
        SendWaitAsk,
        Asked
    }

    public enum EnmTaskStatus
    {
        Init = 0,//初始
        //存车，车厅
        ICarInWaitFirstSwipeCard,// 车厅内已经有车停好，等待刷卡
        IFirstSwipedWaitforCheckSize,// 入库车辆的第一次刷卡结束，提示用户第二次刷卡(等待下发1-9)
        ISecondSwipedWaitforCheckSize,//第二次刷卡，等待检测尺寸
        ISecondSwipedWaitforEVDown,// 第二次刷卡成功，等待检测车辆(等待下发1-1)
        ISecondSwipedWaitforCarLeave,// 第二次刷卡分配车位失败，等待车辆离开(等待下发3-1)
        IEVDownFinished,//确认入库 收到(1,54,9999)后修改
        IEVDownFinishing,//升降机下降完成(等待下发1,54)
        //存车异常
        ICheckCarFail,//检测失败(收到 1001,104)
        IHallFinishing,  //异常退出（1，55）

        //取车，车厅
        OWaitforEVDown,   //出库开始，升降机等待下降(等待下发3-1)
        OEVDownFinishing,//升降机下降完成（取）
        OEVDownWaitforTVLoad,//升降机下降等待装载（3，54，9999）
        OWaitforEVUp,//出车卸载完成，等待升降机上升(1003,1)
        OCarOutWaitforDriveaway,// 车已取出，等待用户开车(等待下发3-2)
        OHallFinishing,  //确认车辆离开（3，55）

        //取物，车厅
        TempWaitforEVDown,  //暂时取车
        TempEVDownFinishing,  //升降机下降完成
        TempEVDownWaitforTVLoad,//升降机下降等待装载（3，54，9999）
        TempWaitforEVUp,//出车卸载完成，等待升降机上升(1002,1)  
        TempOCarOutWaitforDrive,     // 车已取出，等待用户开车(下发2-2)
        TempHallFinishing,  //确认车辆离开（2，55）
        
        Finished,   //作业完成
      
        TMURO,    // 故障       
        TMURORecoverNocar,  // 故障人工确认继续
        TMURORecoverHascar, // TV故障恢复       
        TMUROWaitforUnload, // 等待卸载

        //TV
        TWaitforLoad,//等待执行装载(等待下发13-1)
        TWaitforUnload,  //等待执行卸载
        TWaitforMove,// 等待移动

        LoadFinishing,//装载完成
        UnLoadFinishing,//卸载完成
        MoveFinishing,//移动完成

        WillWaitForUnload, // 装载完成后，将其更新为等待卸载，同时生成卸载指令
        TMUROWaitforLoad,   // 等待装载

        ReCheckInLoad,  //车厅装载时复核尺寸

        WaitforDeleteTask,  //删除指令 下发（23，1）
        DeleteTaskFinishing //收到（1023，1），删除完成
    }

}
