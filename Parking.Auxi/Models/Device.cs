using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    /// <summary>
    /// 设备信息
    /// </summary>
    public class Device
    {       
        public int ID { get; set; }
        public int Warehouse { get; set; }
        public int DeviceCode { get; set; }
        public EnmSMGType Type { get; set; }
        public EnmHallType HallType { get; set; }       
        public string Address { get; set; }
        public int Layer { get; set; }
        public int Region { get; set; }
        public EnmModel Mode { get; set; }       
        public int IsAble { get; set; }       
        public int IsAvailabe { get; set; }        
        public int RunStep { get; set; }      
        public int InStep { get; set; }      
        public int OutStep { get; set; }      
        public int TaskID { get; set; }       
        public int SoonTaskID { get; set; }
    }

    #region 枚举类型
    public enum EnmSMGType
    {
        Init=0,
        Hall,
        ETV
    }

    public enum EnmHallType
    {
        Init=0,
        Entrance,
        Exit,
        EnterOrExit
    }

    public enum EnmModel
    {
        Init=0,
        Maintance,
        Manual,
        StandAlone,
        /// <summary>
        /// 全自动
        /// </summary>
        Automatic
    }


    #endregion
}
