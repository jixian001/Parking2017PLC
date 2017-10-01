using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    /// <summary>
    /// 车位信息
    /// </summary>
    public class Location
    {
       
        public int ID { get; set; }
        public int Warehouse { get; set; }
        public string Address { get; set; }
        public int LocSide { get; set; }
        public int LocColumn { get; set; }
        public int LocLayer { get; set; }
        public EnmLocationType Type { get; set; }
        public EnmLocationStatus Status { get; set; }
       
        /// <summary>
        /// 车位固有尺寸
        /// </summary>
        public string LocSize { get; set; }
        public int Region { get; set; }  
        /// <summary>
        /// 是否需要倒库
        /// </summary>
        public int NeedBackup { get; set; }
        /// <summary>
        /// 优先级序号
        /// </summary>
        public int Idx { get; set; }

        /*
         * 以下是车辆信息
         */
       
        public string ICCode { get; set; }
        public int WheelBase { get; set; }
        public int CarWeight { get; set; }
       
        public string CarSize { get; set; }
        public DateTime InDate { get; set; }
        public string PlateNum { get; set; }
        /// <summary>
        /// 车头图片路径,使用BASE64编码
        /// </summary>
        public string ImagePath { get; set; }
        /// <summary>
        /// 车头图片，使用BASE64编码
        /// </summary>
        public string ImageData { get; set; }       
    }

    public enum EnmLocationType
    {
        Init = 0,  //初始
        Normal,    //正常 1
        Hall,      //车厅 2
        Disable,   //禁用 3
        ETV,       //ETV 4
        Invalid,   //无效车位-5
        Temporary,  //缓存车位 6
        TempDisable //缓存车位，禁用时用 7
    }

    public enum EnmLocationStatus
    {
        Init = 0,  //初始
        Space,     //空闲 1
        Occupy,    //占用-2
        Entering,  //正在入库-3
        Outing,    //正在出库-4
        TempGet,   //临时取物车位  -5
        Idleness,   //无效车位-6
        WillBack,   //待回挪 -7
        Book       //预定
    }
}
