using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Auxi
{
    
    /// <summary>
    /// 报警信息
    /// </summary>    
    public class Alarm
    {
       
        public int ID { get; set; }
        public int Address { get; set; }      
        public string Description { get; set; }
        public byte Value { get; set; }
        public EnmAlarmColor Color { get; set; }        
        /// <summary>
        /// 是否是备用
        /// </summary>
        public byte IsBackup { get; set; }
        public int Warehouse { get; set; }
        public int DeviceCode { get; set; }
    }

    public enum EnmAlarmColor
    {
        Init=0,
        Green,
        Red
    }
}
